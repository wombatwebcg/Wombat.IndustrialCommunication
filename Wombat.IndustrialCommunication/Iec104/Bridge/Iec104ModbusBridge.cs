using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.Iec104.Diagnostics;
using Wombat.IndustrialCommunication.Iec104.Model;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.Iec104.ModbusBridge
{
    /// <summary>
    /// IEC104 到 Modbus TCP 的只读桥接：把主站缓存镜像到 Modbus 寄存器。
    /// 104 事件只记录脏点，由单线程刷写循环写 DataStore，避免突发时逐条抢索引锁。
    /// </summary>
    public sealed class Iec104ModbusBridge : IAsyncDisposable
    {
        private const int MaxWritesPerFlush = 2000;

        private readonly IIec104ValueSource _source;
        private readonly ModbusBridgeOptions _options;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<int, byte> _dirty = new ConcurrentDictionary<int, byte>();
        private readonly object _lifecycleGate = new object();
        private readonly ModbusCommandChannel _commandChannel;

        private long _lastTelemetryTicks;
        private long _lastDiscreteTicks;
        private long _linkDownSinceTicks;
        private CancellationTokenSource _flushCts;
        private Task _flushLoop;
        private volatile bool _running;
        private int _disposed;

        public Iec104ModbusBridge(IIec104ValueSource source, ModbusBridgeOptions options, ILogger logger = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;

            if (source.PointTable == null)
                throw new ArgumentException("数据来源必须提供点表，否则无法分配 Modbus 地址。", nameof(source));

            Map = ModbusRegisterMap.Build(source.PointTable, options);

            var server = new BridgeModbusTcpServer(options.BindAddress, options.Port) { SlaveId = options.UnitId };
            server.IsDataStale = IsDataStale;
            server.ReturnExceptionWhenStale = options.StaleReadMode == ModbusStaleReadMode.Exception;
            Server = server;
            if (logger != null) Server.UseLogger(logger);

            if (options.EnableCommands)
            {
                var sink = source as IIec104CommandSink;
                if (sink == null)
                    throw new ArgumentException("启用命令通道时，数据来源必须实现 IIec104CommandSink。", nameof(source));

                if (Map.CommandPoints.Count == 0)
                    logger?.LogWarning("已启用命令通道，但点表里没有遥控点。");

                _commandChannel = new ModbusCommandChannel(Server, Map, sink, options, logger);
            }
            else if (Map.CommandPoints.Count > 0)
            {
                // 关闭命令通道时仍要监听：写入只回“未启用”，寄存器照常清零。
                _commandChannel = new ModbusCommandChannel(
                    Server, Map, source as IIec104CommandSink, options, logger);
            }

            for (var i = 0; i < Map.Warnings.Count; i++)
            {
                logger?.LogWarning("桥接映射告警：{Warning}", Map.Warnings[i]);
            }

            logger?.LogInformation(
                "桥接映射就绪：遥测 {Telemetry} 点 / 遥信 {Discrete} 点 / 遥控 {Command} 点，点表版本 {Version:X4}",
                Map.TelemetryPoints.Count,
                Map.DiscretePoints.Count,
                Map.CommandPoints.Count,
                Map.MapVersion);
        }

        /// <summary>地址映射。</summary>
        public ModbusRegisterMap Map { get; }

        /// <summary>底层 Modbus TCP 从站。</summary>
        public ModbusTcpServer Server { get; }

        public bool IsRunning
        {
            get { return _running; }
        }

        /// <summary>启动桥接：初始化镜像、挂事件、启动从站与刷写循环。</summary>
        public async Task<OperationResult> StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_lifecycleGate)
            {
                if (_running) throw new InvalidOperationException("桥接已经启动。");
            }

            var now = DateTime.UtcNow.Ticks;
            Volatile.Write(ref _lastTelemetryTicks, now);
            Volatile.Write(ref _lastDiscreteTicks, now);
            Volatile.Write(ref _linkDownSinceTicks,
                _source.LinkState == Iec104LinkState.DataTransferActive ? 0 : now);

            InitializeDataStore();

            _source.ValueChanged += OnValueChanged;
            _source.LinkStateChanged += OnLinkStateChanged;

            OperationResult result;
            try
            {
                result = await Server.ListenAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                Unsubscribe();
                throw;
            }

            if (!result.IsSuccess)
            {
                Unsubscribe();
                _logger?.LogError("Modbus 从站启动失败：{Message}", result.Message);
                return result;
            }

            _running = true;
            _commandChannel?.Start();
            WriteHealth();

            _flushCts = new CancellationTokenSource();
            _flushLoop = Task.Run((Func<Task>)(() => FlushLoopAsync(_flushCts.Token)));

            _logger?.LogInformation("桥接已启动：{Address}:{Port}，单位号 {UnitId}",
                _options.BindAddress, _options.Port, _options.UnitId);
            return result;
        }

        /// <summary>停止桥接：先把运行状态写 0，再停从站。</summary>
        public async Task StopAsync()
        {
            _running = false;
            _commandChannel?.Stop();

            var cts = _flushCts;
            _flushCts = null;
            if (cts != null)
            {
                cts.Cancel();
                var loop = _flushLoop;
                _flushLoop = null;
                if (loop != null)
                {
                    try { await loop.ConfigureAwait(false); } catch { }
                }

                cts.Dispose();
            }

            Unsubscribe();
            WriteHealth();

            try
            {
                await Server.ShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "停止 Modbus 从站时出错。");
            }
        }

        /// <summary>
        /// 初始镜像：按点表把全部地址写成确定值，让下游在第一次总召前读到的不是随机内存。
        /// </summary>
        private void InitializeDataStore()
        {
            var dataStore = Server.DataStore;

            for (var i = 0; i < Map.TelemetryPoints.Count; i++)
            {
                dataStore.WriteInputRegistersDirect(Map.TelemetryPoints[i].Address, new ushort[ModbusRegisterMap.TelemetryRegisterStride]);
            }

            for (var i = 0; i < Map.DiscretePoints.Count; i++)
            {
                dataStore.WriteInputDiscretesDirect(Map.DiscretePoints[i].Address, new[] { false });
            }

            for (var i = 0; i < Map.CommandPoints.Count; i++)
            {
                dataStore.WriteHoldingRegistersDirect(Map.CommandPoints[i].Address, new ushort[ModbusRegisterMap.CommandRegisterStride]);
            }

            MirrorSnapshot();

            WriteHealth();

            _logger?.LogInformation("初始镜像完成：遥测 {Telemetry} 点、遥信 {Discrete} 点。",
                Map.TelemetryPoints.Count, Map.DiscretePoints.Count);
        }

        /// <summary>按当前缓存把已映射点位全部写一遍，启动与重连后使用。</summary>
        private void MirrorSnapshot()
        {
            for (var i = 0; i < Map.TelemetryPoints.Count; i++)
            {
                var mapping = Map.TelemetryPoints[i];
                Iec104Value value;
                if (_source.TryGetValue(mapping.Ioa, out value)) WritePoint(mapping, value);
            }

            for (var i = 0; i < Map.DiscretePoints.Count; i++)
            {
                var mapping = Map.DiscretePoints[i];
                Iec104Value value;
                if (_source.TryGetValue(mapping.Ioa, out value)) WritePoint(mapping, value);
            }
        }

        private async Task FlushLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_options.FlushInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    FlushOnce();
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "刷写脏点失败，等待下一周期。");
                }
            }
        }

        private void FlushOnce()
        {
            var remaining = MaxWritesPerFlush;
            foreach (var pair in _dirty)
            {
                if (!_dirty.TryRemove(pair.Key, out _)) continue;

                ModbusPointMapping mapping;
                if (!Map.TryGetMapping(pair.Key, out mapping)) continue;
                if (mapping.Area == ModbusBridgeArea.HoldingRegister) continue;

                Iec104Value value;
                if (!_source.TryGetValue(pair.Key, out value)) continue;

                WritePoint(mapping, value);

                if (--remaining <= 0)
                {
                    _logger?.LogDebug("单次刷写达到上限 {Limit}，剩余脏点留到下一周期。", MaxWritesPerFlush);
                    break;
                }
            }

            WriteHealth();
        }

        private void WritePoint(ModbusPointMapping mapping, Iec104Value value)
        {
            var dataStore = Server.DataStore;

            if (mapping.Area == ModbusBridgeArea.InputRegister)
            {
                ushort first;
                ushort second;
                ModbusFloatCodec.Encode((float)value.Numeric, _options.WordOrder, out first, out second);
                dataStore.WriteInputRegistersDirect(mapping.Address, new[] { first, second });
                return;
            }

            if (mapping.Area == ModbusBridgeArea.DiscreteInput)
            {
                dataStore.WriteInputDiscretesDirect(mapping.Address, new[] { value.Boolean });
            }
        }

        /// <summary>健康区每个周期整体重写，只有一个写入者，不存在半新半旧。</summary>
        private void WriteHealth()
        {
            var registers = new ushort[ModbusRegisterMap.HealthRegisterCount];

            registers[BridgeHealthRegister.LinkState] = ToHealthLinkState(_source.LinkState);
            registers[BridgeHealthRegister.TelemetryAge] = ElapsedSeconds(ref _lastTelemetryTicks);
            registers[BridgeHealthRegister.DiscreteAge] = ElapsedSeconds(ref _lastDiscreteTicks);
            registers[BridgeHealthRegister.TelemetryCount] = ClampCount(Map.TelemetryPoints.Count);
            registers[BridgeHealthRegister.DiscreteCount] = ClampCount(Map.DiscretePoints.Count);
            registers[BridgeHealthRegister.MapVersion] = Map.MapVersion;
            registers[BridgeHealthRegister.Running] = (ushort)(_running ? 1 : 0);
            registers[BridgeHealthRegister.Reserved] = 0;

            Server.DataStore.WriteHoldingRegistersDirect((ushort)Map.HealthBase, registers);
        }

        private static ushort ToHealthLinkState(Iec104LinkState state)
        {
            switch (state)
            {
                case Iec104LinkState.DataTransferActive:
                    return 2;

                case Iec104LinkState.Connected:
                case Iec104LinkState.Connecting:
                    return 1;

                default:
                    return 0;
            }
        }

        private static ushort ElapsedSeconds(ref long ticks)
        {
            var last = Volatile.Read(ref ticks);
            if (last == 0) return 0;

            var seconds = (DateTime.UtcNow.Ticks - last) / TimeSpan.TicksPerSecond;
            if (seconds < 0) return 0;
            return seconds > ushort.MaxValue ? ushort.MaxValue : (ushort)seconds;
        }

        private static ushort ClampCount(int count)
        {
            return count > ushort.MaxValue ? ushort.MaxValue : (ushort)count;
        }

        private void OnValueChanged(object sender, Iec104ValueEventArgs e)
        {
            if (e == null || e.Value == null) return;

            ModbusPointMapping mapping;
            if (!Map.TryGetMapping(e.Value.Ioa, out mapping)) return;

            if (mapping.Area == ModbusBridgeArea.InputRegister)
            {
                Volatile.Write(ref _lastTelemetryTicks, DateTime.UtcNow.Ticks);
            }
            else if (mapping.Area == ModbusBridgeArea.DiscreteInput)
            {
                Volatile.Write(ref _lastDiscreteTicks, DateTime.UtcNow.Ticks);
            }
            else
            {
                return;
            }

            _dirty[e.Value.Ioa] = 0;
        }

        private void OnLinkStateChanged(object sender, Iec104LinkEventArgs e)
        {
            if (e.State == Iec104LinkState.DataTransferActive)
            {
                Volatile.Write(ref _linkDownSinceTicks, 0);
            }
            else if (Volatile.Read(ref _linkDownSinceTicks) == 0)
            {
                Volatile.Write(ref _linkDownSinceTicks, DateTime.UtcNow.Ticks);
            }

            _logger?.LogInformation("104 链路状态变为 {State}：{Message}", e.State, e.Message);

            // 重连后缓存可能整体刷新过，全部重点一遍，避免漏掉断链期间攒下的变化。
            if (e.State == Iec104LinkState.DataTransferActive) MarkAllDirty();
        }

        /// <summary>断链超过 StaleTimeout 视为陈旧；链路激活时永不为陈旧。</summary>
        private bool IsDataStale()
        {
            if (_source.LinkState == Iec104LinkState.DataTransferActive) return false;

            var since = Volatile.Read(ref _linkDownSinceTicks);
            if (since == 0) return false;

            return DateTime.UtcNow.Ticks - since > _options.StaleTimeout.Ticks;
        }

        private void MarkAllDirty()
        {
            for (var i = 0; i < Map.TelemetryPoints.Count; i++) _dirty[Map.TelemetryPoints[i].Ioa] = 0;
            for (var i = 0; i < Map.DiscretePoints.Count; i++) _dirty[Map.DiscretePoints[i].Ioa] = 0;
        }

        private void Unsubscribe()
        {
            _source.ValueChanged -= OnValueChanged;
            _source.LinkStateChanged -= OnLinkStateChanged;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            await StopAsync().ConfigureAwait(false);
            Server.Dispose();
        }
    }
}

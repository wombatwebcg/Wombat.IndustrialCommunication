using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;
using Wombat.IndustrialCommunication.Helpers;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.Modbus.Data;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge
{
    /// <summary>
    /// 命令通道：监听命令寄存器写入，跑两段确认状态机，把结果写回状态/原因寄存器。
    /// 事件回调只做受理与置位，104 收发放到后台任务，绝不占用 Modbus 消息处理线程。
    /// </summary>
    internal sealed class ModbusCommandChannel
    {
        private readonly ModbusTcpServer _server;
        private readonly ModbusRegisterMap _map;
        private readonly IIec104CommandSink _sink;
        private readonly ModbusBridgeOptions _options;
        private readonly ILogger _logger;
        private readonly Dictionary<int, CommandSlot> _byIoa = new Dictionary<int, CommandSlot>();
        private readonly List<CommandSlot> _slots = new List<CommandSlot>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _sweeper;

        public ModbusCommandChannel(
            ModbusTcpServer server,
            ModbusRegisterMap map,
            IIec104CommandSink sink,
            ModbusBridgeOptions options,
            ILogger logger)
        {
            _server = server;
            _map = map;
            _sink = sink;
            _options = options;
            _logger = logger;

            for (var i = 0; i < map.CommandPoints.Count; i++)
            {
                var slot = new CommandSlot(map.CommandPoints[i]);
                _slots.Add(slot);
                _byIoa[slot.Mapping.Ioa] = slot;
            }

            if (options.AllowedCommandIoa != null && options.AllowedCommandIoa.Count > 0)
            {
                var unknown = options.AllowedCommandIoa.Where(ioa => !_byIoa.ContainsKey(ioa)).ToArray();
                for (var i = 0; i < unknown.Length; i++)
                {
                    logger?.LogWarning("遥控白名单里的 IOA {Ioa} 不在点表中，该项不会生效。", unknown[i]);
                }
            }
        }

        public void Start()
        {
            _server.DataStore.DataStoreWrittenTo += OnWritten;
            _sweeper = Task.Run((Func<Task>)(() => SweepAsync(_cts.Token)));

            if (_options.EnableCommands)
            {
                _logger?.LogInformation("命令通道已启用：{Count} 个遥控点，方式 {Mode}。",
                    _slots.Count, _options.CommandMode);
            }
            else
            {
                _logger?.LogInformation("命令通道未启用：{Count} 个遥控点只回状态 8，不执行任何动作。", _slots.Count);
            }
        }

        public void Stop()
        {
            _server.DataStore.DataStoreWrittenTo -= OnWritten;

            try { _cts.Cancel(); } catch (ObjectDisposedException) { }

            var sweeper = _sweeper;
            _sweeper = null;
            if (sweeper != null)
            {
                try { sweeper.Wait(TimeSpan.FromSeconds(2)); } catch { }
            }
        }

        private void OnWritten(object sender, DataStoreEventArgs e)
        {
            if (e == null || e.ModbusDataType != ModbusDataType.HoldingRegister) return;
            if (e.Data == null || e.Data.Option != DiscriminatedUnionOption.B) return;

            var values = e.Data.B;
            for (var i = 0; i < values.Count; i++)
            {
                ModbusPointMapping mapping;
                int offset;
                if (!_map.TryGetCommandRegister(e.StartAddress + i, out mapping, out offset)) continue;
                if (offset != 0) continue;

                HandleWrite(mapping, values[i]);
            }
        }

        private void HandleWrite(ModbusPointMapping mapping, ushort value)
        {
            // 0 是“无动作”，也是桥接层自己清零时的回环值，直接忽略。
            if (value == BridgeCommandValue.None || value > BridgeCommandValue.ConfirmOff) return;

            ClearCommandRegister(mapping);

            CommandSlot slot;
            if (!_byIoa.TryGetValue(mapping.Ioa, out slot)) return;

            lock (slot.Gate)
            {
                if (!_options.EnableCommands)
                {
                    SetState(slot, BridgeCommandState.Disabled, BridgeCommandReason.Disabled);
                    Audit(mapping, value, BridgeCommandState.Disabled, BridgeCommandReason.Disabled);
                    return;
                }

                if (_options.AllowedCommandIoa != null && !_options.AllowedCommandIoa.Contains(mapping.Ioa))
                {
                    SetState(slot, BridgeCommandState.Rejected, BridgeCommandReason.IoaNotAllowed);
                    _logger?.LogWarning("遥控 IOA {Ioa} 不在白名单内，已拒绝。", mapping.Ioa);
                    return;
                }

                switch ((BridgeCommandState)slot.State)
                {
                    case BridgeCommandState.Idle:
                    case BridgeCommandState.Succeeded:
                    case BridgeCommandState.Rejected:
                    case BridgeCommandState.ConfirmTimeout:
                    case BridgeCommandState.Disabled:
                        HandleRequest(slot, value);
                        break;

                    case BridgeCommandState.PendingOn:
                        HandlePending(slot, value, true);
                        break;

                    case BridgeCommandState.PendingOff:
                        HandlePending(slot, value, false);
                        break;

                    default:
                        // Selecting / Executing / Busy：忽略，状态保持。
                        break;
                }
            }
        }

        private void HandleRequest(CommandSlot slot, ushort value)
        {
            if (value == BridgeCommandValue.RequestOn)
            {
                BeginSelect(slot, true);
            }
            else if (value == BridgeCommandValue.RequestOff)
            {
                BeginSelect(slot, false);
            }

            // 没有待确认的选择时写 3/4，按计划忽略。
        }

        private void HandlePending(CommandSlot slot, ushort value, bool on)
        {
            switch (value)
            {
                case BridgeCommandValue.RequestOn:
                    if (!on) BeginSelect(slot, true);
                    break;

                case BridgeCommandValue.RequestOff:
                    if (on) BeginSelect(slot, false);
                    break;

                case BridgeCommandValue.ConfirmOn:
                    if (on) BeginExecute(slot, true);
                    else SetReason(slot, BridgeCommandReason.ConfirmMismatch);
                    break;

                case BridgeCommandValue.ConfirmOff:
                    if (!on) BeginExecute(slot, false);
                    else SetReason(slot, BridgeCommandReason.ConfirmMismatch);
                    break;
            }
        }

        private void BeginSelect(CommandSlot slot, bool on)
        {
            slot.Generation++;
            slot.RequestedOn = on;

            if (_sink == null)
            {
                SetState(slot, BridgeCommandState.Rejected, BridgeCommandReason.LinkUnavailable);
                return;
            }

            if (_options.CommandMode == Iec104CommandMode.Direct)
            {
                // Direct：不发选择帧，本地直接进入待确认。
                SetState(slot, on ? BridgeCommandState.PendingOn : BridgeCommandState.PendingOff,
                    BridgeCommandReason.None);
                _logger?.LogInformation("遥控 IOA {Ioa} 本地选择{Action}，等待确认。",
                    slot.Mapping.Ioa, on ? "合" : "分");
                return;
            }

            SetState(slot, BridgeCommandState.Selecting, BridgeCommandReason.None);
            _logger?.LogInformation("遥控 IOA {Ioa} 下发选择帧（{Action}）。", slot.Mapping.Ioa, on ? "合" : "分");

            var generation = slot.Generation;
            Dispatch(() => SelectAsync(slot, on, generation));
        }

        private void BeginExecute(CommandSlot slot, bool on)
        {
            slot.Generation++;
            slot.RequestedOn = on;
            SetState(slot, BridgeCommandState.Executing, BridgeCommandReason.None);
            _logger?.LogInformation("遥控 IOA {Ioa} 下发执行帧（{Action}）。", slot.Mapping.Ioa, on ? "合" : "分");

            var generation = slot.Generation;
            Dispatch(() => ExecuteAsync(slot, on, generation));
        }

        private async Task SelectAsync(CommandSlot slot, bool on, int generation)
        {
            Iec104CommandResult result;
            try
            {
                result = await _sink.SelectSingleCommandAsync(slot.Mapping.Ioa, on, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "遥控 IOA {Ioa} 选择失败。", slot.Mapping.Ioa);
                ApplyFailure(slot, generation, BridgeCommandState.Selecting, BridgeCommandReason.LinkUnavailable);
                return;
            }

            if (result != null && result.Accepted)
            {
                ApplySuccess(slot, generation, BridgeCommandState.Selecting,
                    on ? BridgeCommandState.PendingOn : BridgeCommandState.PendingOff);
                return;
            }

            ApplyFailure(slot, generation, BridgeCommandState.Selecting, MapReason(result));
        }

        private async Task ExecuteAsync(CommandSlot slot, bool on, int generation)
        {
            Iec104CommandResult result;
            try
            {
                // 选择已经做过，执行帧按 Direct 发；Direct 模式本来也不发选择帧。
                result = await _sink.SingleCommandAsync(
                    slot.Mapping.Ioa, on, Iec104CommandMode.Direct, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "遥控 IOA {Ioa} 执行失败。", slot.Mapping.Ioa);
                ApplyFailure(slot, generation, BridgeCommandState.Executing, BridgeCommandReason.LinkUnavailable);
                return;
            }

            if (result != null && result.Accepted)
            {
                ApplySuccess(slot, generation, BridgeCommandState.Executing, BridgeCommandState.Succeeded);
                return;
            }

            ApplyFailure(slot, generation, BridgeCommandState.Executing, MapReason(result));
        }

        private void ApplySuccess(CommandSlot slot, int generation, BridgeCommandState expected, BridgeCommandState next)
        {
            lock (slot.Gate)
            {
                if (generation != slot.Generation || slot.State != (ushort)expected) return;

                SetState(slot, next, BridgeCommandReason.None);
                Audit(slot.Mapping, slot.RequestedOn ? BridgeCommandValue.RequestOn : BridgeCommandValue.RequestOff,
                    next, BridgeCommandReason.None);
            }
        }

        private void ApplyFailure(CommandSlot slot, int generation, BridgeCommandState expected, BridgeCommandReason reason)
        {
            lock (slot.Gate)
            {
                if (generation != slot.Generation || slot.State != (ushort)expected) return;

                SetState(slot, BridgeCommandState.Rejected, reason);
                Audit(slot.Mapping, slot.RequestedOn ? BridgeCommandValue.RequestOn : BridgeCommandValue.RequestOff,
                    BridgeCommandState.Rejected, reason);
            }
        }

        private static BridgeCommandReason MapReason(Iec104CommandResult result)
        {
            if (result == null) return BridgeCommandReason.LinkUnavailable;
            if (result.Negative) return BridgeCommandReason.NegativeConfirm;
            if (result.TimedOut) return BridgeCommandReason.Timeout;
            return BridgeCommandReason.LinkUnavailable;
        }

        private async Task SweepAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                var now = DateTime.UtcNow;
                for (var i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];
                    lock (slot.Gate)
                    {
                        var state = (BridgeCommandState)slot.State;

                        if (state == BridgeCommandState.PendingOn || state == BridgeCommandState.PendingOff)
                        {
                            if (now - slot.Since <= _options.ConfirmTimeout) continue;

                            slot.Generation++;
                            SetState(slot, BridgeCommandState.ConfirmTimeout, BridgeCommandReason.Timeout);
                            Audit(slot.Mapping, BridgeCommandValue.None,
                                BridgeCommandState.ConfirmTimeout, BridgeCommandReason.Timeout);
                            _logger?.LogWarning("遥控 IOA {Ioa} 确认超时，本地选择已清空。", slot.Mapping.Ioa);
                        }
                        else if (state == BridgeCommandState.Selecting)
                        {
                            if (now - slot.Since <= _options.SelectTimeout) continue;

                            slot.Generation++;
                            SetState(slot, BridgeCommandState.Rejected, BridgeCommandReason.Timeout);
                            Audit(slot.Mapping, BridgeCommandValue.None,
                                BridgeCommandState.Rejected, BridgeCommandReason.Timeout);
                        }
                    }
                }
            }
        }

        private void SetState(CommandSlot slot, BridgeCommandState state, BridgeCommandReason reason)
        {
            slot.State = (ushort)state;
            slot.Reason = (ushort)reason;
            slot.Since = DateTime.UtcNow;
            WriteStatus(slot);
        }

        private void SetReason(CommandSlot slot, BridgeCommandReason reason)
        {
            slot.Reason = (ushort)reason;
            WriteStatus(slot);
            _logger?.LogWarning("遥控 IOA {Ioa} 的确认码与选择方向不符，已忽略。", slot.Mapping.Ioa);
        }

        private void WriteStatus(CommandSlot slot)
        {
            _server.DataStore.WriteHoldingRegistersDirect(
                (ushort)(slot.Mapping.Address + 1),
                new[] { slot.State, slot.Reason });
        }

        private void ClearCommandRegister(ModbusPointMapping mapping)
        {
            _server.DataStore.WriteHoldingRegistersDirect(
                mapping.Address,
                new[] { BridgeCommandValue.None });
        }

        private void Dispatch(Func<Task> operation)
        {
            var token = _cts.Token;
            Task.Run(async () =>
            {
                try
                {
                    await operation().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "遥控后台任务异常。");
                }
            }, token);
        }

        private void Audit(ModbusPointMapping mapping, ushort command, BridgeCommandState state, BridgeCommandReason reason)
        {
            _logger?.LogInformation("遥控审计：IOA={Ioa} 命令={Command} 状态={State} 原因={Reason}",
                mapping.Ioa, command, state, reason);
        }

        private sealed class CommandSlot
        {
            public CommandSlot(ModbusPointMapping mapping)
            {
                Mapping = mapping;
                Since = DateTime.UtcNow;
            }

            public readonly object Gate = new object();

            public ModbusPointMapping Mapping { get; }

            public ushort State;
            public ushort Reason;
            public DateTime Since;
            public bool RequestedOn;
            public int Generation;
        }
    }
}

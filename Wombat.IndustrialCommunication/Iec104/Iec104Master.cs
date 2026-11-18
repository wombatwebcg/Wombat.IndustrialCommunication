using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.Iec104.Diagnostics;
using Wombat.IndustrialCommunication.Iec104.Model;
using Wombat.IndustrialCommunication.Iec104.Options;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Wombat.IndustrialCommunication.Iec104.Session;
using Wombat.IndustrialCommunication.Iec104.Transport;

namespace Wombat.IndustrialCommunication.Iec104
{
    /// <summary>
    /// 104 主站。负责建链、STARTDT、总召、遥控、值缓存与可选重连。
    /// </summary>
    public sealed class Iec104Master : IAsyncDisposable
    {
        private readonly Iec104MasterOptions _options;
        private readonly Func<CancellationToken, Task<IIec104Link>> _linkFactory;
        private readonly ILogger _logger;
        private readonly object _gate = new object();

        private Iec104MasterSession _session;
        private CancellationTokenSource _runCts;
        private Task _supervisor;
        private Task _cyclic;
        private int _disposed;

        public Iec104Master(Iec104MasterOptions options, ILogger logger = null)
            : this(options, null, logger)
        {
        }

        /// <summary>
        /// 允许注入自定义链路工厂，用于测试或非 TCP 承载。
        /// </summary>
        public Iec104Master(
            Iec104MasterOptions options,
            Func<CancellationToken, Task<IIec104Link>> linkFactory,
            ILogger logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (options.K < 1) throw new ArgumentOutOfRangeException(nameof(options.K));
            if (options.W < 1) throw new ArgumentOutOfRangeException(nameof(options.W));

            _linkFactory = linkFactory ?? CreateTcpLinkAsync;
            _logger = logger;
        }

        public Iec104MasterOptions Options
        {
            get { return _options; }
        }

        /// <summary>因接收序号不连续而重新对齐的次数；非 0 说明装置计数器与协议约定不一致。</summary>
        public int SequenceResyncs
        {
            get
            {
                var session = _session;
                return session == null ? 0 : session.SequenceResyncs;
            }
        }

        /// <summary>因 N(R) 越界而按全部确认处理的次数。</summary>
        public int ClampedAcknowledgements
        {
            get
            {
                var session = _session;
                return session == null ? 0 : session.ClampedAcknowledgements;
            }
        }

        public bool IsConnected
        {
            get
            {
                var session = _session;
                return session != null && session.IsConnected;
            }
        }

        public event EventHandler<Iec104ValueEventArgs> ValueReceived;

        public event EventHandler<Iec104LinkEventArgs> LinkStateChanged;

        public event EventHandler<Iec104TraceEventArgs> FrameTraced;

        /// <summary>建立连接并完成 STARTDT。</summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            lock (_gate)
            {
                if (_session != null) throw new InvalidOperationException("已经处于连接状态。");
            }

            var session = await CreateSessionAsync(cancellationToken).ConfigureAwait(false);
            _session = session;

            if (_options.AutoReconnect || _options.CyclicInterrogation > TimeSpan.Zero)
            {
                _runCts = new CancellationTokenSource();
                if (_options.AutoReconnect)
                {
                    _supervisor = Task.Run((Func<Task>)(() => SuperviseAsync(_runCts.Token)));
                }

                if (_options.CyclicInterrogation > TimeSpan.Zero)
                {
                    _cyclic = Task.Run((Func<Task>)(() => CyclicInterrogationAsync(_runCts.Token)));
                }
            }
        }

        /// <summary>断开连接。</summary>
        public async Task DisconnectAsync()
        {
            _runCts?.Cancel();

            var session = _session;
            _session = null;
            if (session != null)
            {
                await session.StopAsync().ConfigureAwait(false);
            }

            await AwaitQuietlyAsync(_supervisor).ConfigureAwait(false);
            await AwaitQuietlyAsync(_cyclic).ConfigureAwait(false);
            _supervisor = null;
            _cyclic = null;

            _runCts?.Dispose();
            _runCts = null;
        }

        /// <summary>执行一次站总召唤。</summary>
        public async Task<Iec104InterrogationResult> InterrogateAsync(CancellationToken cancellationToken = default)
        {
            var session = RequireSession();
            return await session.InterrogateAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>单点遥控。</summary>
        public async Task<Iec104CommandResult> SingleCommandAsync(
            int ioa,
            bool on,
            Iec104CommandMode mode = Iec104CommandMode.Direct,
            CancellationToken cancellationToken = default)
        {
            var session = RequireSession();
            return await session.SingleCommandAsync(ioa, on, mode, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>只发选择帧，不发执行帧。</summary>
        public async Task<Iec104CommandResult> SelectSingleCommandAsync(
            int ioa,
            bool on,
            CancellationToken cancellationToken = default)
        {
            var session = RequireSession();
            return await session.SelectSingleCommandAsync(ioa, on, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>双点遥控。</summary>
        public async Task<Iec104CommandResult> DoubleCommandAsync(
            int ioa,
            Iec104DoublePointState state,
            Iec104CommandMode mode = Iec104CommandMode.Direct,
            CancellationToken cancellationToken = default)
        {
            var session = RequireSession();
            return await session.DoubleCommandAsync(ioa, state, mode, cancellationToken).ConfigureAwait(false);
        }

        public bool TryGetValue(int ioa, out Iec104Value value)
        {
            var session = _session;
            if (session == null)
            {
                value = null;
                return false;
            }

            return session.TryGetValue(ioa, out value);
        }

        public IReadOnlyDictionary<int, Iec104Value> Snapshot()
        {
            var session = _session;
            return session == null
                ? (IReadOnlyDictionary<int, Iec104Value>)new Dictionary<int, Iec104Value>()
                : session.Snapshot();
        }

        private async Task<Iec104MasterSession> CreateSessionAsync(CancellationToken cancellationToken)
        {
            var link = await _linkFactory(cancellationToken).ConfigureAwait(false);
            var session = new Iec104MasterSession(link, _options);

            session.ValueReceived += OnValueReceived;
            session.LinkStateChanged += OnLinkStateChanged;
            session.FrameTraced += OnFrameTraced;

            try
            {
                await session.StartAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await session.StopAsync().ConfigureAwait(false);
                throw;
            }

            _logger?.LogInformation("104 链路已激活：{Host}:{Port}", _options.Host, _options.Port);

            if (_options.InterrogateOnConnect)
            {
                try
                {
                    var result = await session.InterrogateAsync(cancellationToken).ConfigureAwait(false);
                    _logger?.LogInformation("连接后总召完成：{Received}/{Expected}", result.ReceivedCount, result.ExpectedCount);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "连接后总召失败。");
                }
            }

            return session;
        }

        private async Task SuperviseAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                var session = _session;
                if (session != null && session.IsConnected) continue;

                try
                {
                    if (session != null)
                    {
                        await session.StopAsync().ConfigureAwait(false);
                    }

                    _session = null;
                    _logger?.LogWarning("104 链路断开，{Delay} 后重连。", _options.ReconnectDelay);
                    await Task.Delay(_options.ReconnectDelay, cancellationToken).ConfigureAwait(false);

                    var next = await CreateSessionAsync(cancellationToken).ConfigureAwait(false);
                    _session = next;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "104 重连失败。");
                }
            }
        }

        private async Task CyclicInterrogationAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_options.CyclicInterrogation, cancellationToken).ConfigureAwait(false);
                    var session = _session;
                    if (session != null && session.DataTransferActive)
                    {
                        await session.InterrogateAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "周期总召失败。");
                }
            }
        }

        private async Task<IIec104Link> CreateTcpLinkAsync(CancellationToken cancellationToken)
        {
            var address = await ResolveAsync(_options.Host).ConfigureAwait(false);
            var endPoint = new IPEndPoint(address, _options.Port);
            return await Iec104TcpLink.ConnectAsync(endPoint, _options.ConnectTimeout, cancellationToken)
                .ConfigureAwait(false);
        }

        private static async Task<IPAddress> ResolveAsync(string host)
        {
            IPAddress address;
            if (IPAddress.TryParse(host, out address)) return address;

            var addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
            for (var i = 0; i < addresses.Length; i++)
            {
                if (addresses[i].AddressFamily == AddressFamily.InterNetwork) return addresses[i];
            }

            if (addresses.Length == 0) throw new InvalidOperationException($"无法解析主机名：{host}");
            return addresses[0];
        }

        private void OnValueReceived(object sender, Iec104ValueEventArgs e)
        {
            var handler = ValueReceived;
            if (handler != null)
            {
                try { handler(this, e); } catch { }
            }
        }

        private void OnLinkStateChanged(object sender, Iec104LinkEventArgs e)
        {
            var handler = LinkStateChanged;
            if (handler != null)
            {
                try { handler(this, e); } catch { }
            }
        }

        private void OnFrameTraced(object sender, Iec104TraceEventArgs e)
        {
            var handler = FrameTraced;
            if (handler != null)
            {
                try { handler(this, e); } catch { }
            }
        }

        private Iec104MasterSession RequireSession()
        {
            var session = _session;
            if (session == null) throw new InvalidOperationException("尚未连接。");
            return session;
        }

        private static async Task AwaitQuietlyAsync(Task task)
        {
            if (task == null) return;
            try { await task.ConfigureAwait(false); } catch { }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(Iec104Master));
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            await DisconnectAsync().ConfigureAwait(false);
        }
    }
}

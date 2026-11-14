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
using Wombat.Network.Transports.Tcp;

namespace Wombat.IndustrialCommunication.Iec104
{
    /// <summary>
    /// 104 从站。监听端口、按连接建立独立会话、共享同一份数据源。
    /// </summary>
    public sealed class Iec104Slave : IAsyncDisposable
    {
        private readonly Iec104SlaveOptions _options;
        private readonly Iec104DataStore _store;
        private readonly ILogger _logger;
        private readonly object _gate = new object();
        private readonly List<Iec104SlaveSession> _sessions = new List<Iec104SlaveSession>();

        private TcpTransportListener _listener;
        private CancellationTokenSource _cts;
        private Task _acceptTask;
        private int _disposed;

        public Iec104Slave(Iec104SlaveOptions options, ILogger logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (options.K < 1) throw new ArgumentOutOfRangeException(nameof(options.K));
            if (options.W < 1) throw new ArgumentOutOfRangeException(nameof(options.W));

            _store = new Iec104DataStore(options.PointTable);
            _store.ValueChanged += OnStoreValueChanged;
            _logger = logger;
        }

        public Iec104SlaveOptions Options
        {
            get { return _options; }
        }

        public Iec104DataStore DataStore
        {
            get { return _store; }
        }

        public bool IsListening
        {
            get { return _listener != null && _cts != null && !_cts.IsCancellationRequested; }
        }

        /// <summary>当前连接的客户端数量。</summary>
        public int ClientCount
        {
            get
            {
                lock (_gate) return _sessions.Count;
            }
        }

        /// <summary>实际监听地址，端口传 0 时可用于取系统分配端口。</summary>
        public IPEndPoint LocalEndPoint
        {
            get
            {
                var listener = _listener;
                return listener == null ? null : listener.LocalEndPoint as IPEndPoint;
            }
        }

        public event EventHandler<Iec104CommandRequestEventArgs> CommandRequested;

        public event EventHandler<Iec104ClientEventArgs> ClientConnected;

        public event EventHandler<Iec104ClientEventArgs> ClientDisconnected;

        public event EventHandler<Iec104TraceEventArgs> FrameTraced;

        /// <summary>开始监听。</summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_listener != null) throw new InvalidOperationException("从站已经在监听。");

            var address = Resolve(_options.BindAddress);
            var endPoint = new IPEndPoint(address, _options.Port);

            _listener = new TcpTransportListener(endPoint, Math.Max(1, _options.MaxConnections));
            await _listener.StartAsync(cancellationToken).ConfigureAwait(false);

            _cts = new CancellationTokenSource();
            _acceptTask = Task.Run((Func<Task>)AcceptLoopAsync);

            _logger?.LogInformation("104 从站已监听 {EndPoint}", LocalEndPoint);
        }

        /// <summary>停止监听并关闭所有会话。</summary>
        public async Task StopAsync()
        {
            _cts?.Cancel();

            var listener = _listener;
            _listener = null;
            if (listener != null)
            {
                try { await listener.CloseAsync().ConfigureAwait(false); } catch { }
                listener.Dispose();
            }

            var accept = _acceptTask;
            _acceptTask = null;
            if (accept != null)
            {
                try { await accept.ConfigureAwait(false); } catch { }
            }

            List<Iec104SlaveSession> sessions;
            lock (_gate)
            {
                sessions = new List<Iec104SlaveSession>(_sessions);
                _sessions.Clear();
            }

            for (var i = 0; i < sessions.Count; i++)
            {
                try { await sessions[i].StopAsync().ConfigureAwait(false); } catch { }
            }

            _cts?.Dispose();
            _cts = null;
        }

        /// <summary>写入遥测值，超出死区才触发突发上送。</summary>
        public bool SetValue(int ioa, double value, DateTime? timestamp = null)
        {
            return _store.SetNumeric(ioa, value, timestamp);
        }

        /// <summary>写入单点状态，变化才触发上送。</summary>
        public bool SetSinglePoint(int ioa, bool state, DateTime? timestamp = null)
        {
            return _store.SetBoolean(ioa, state, timestamp);
        }

        /// <summary>设置品质位，下一次上送生效。</summary>
        public bool SetQuality(int ioa, Iec104QualityFlags quality)
        {
            return _store.SetQuality(ioa, quality);
        }

        public IReadOnlyDictionary<int, Iec104Value> Snapshot()
        {
            return _store.Snapshot();
        }

        private async Task AcceptLoopAsync()
        {
            var token = _cts.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var connection = (TcpTransportConnection)await _listener.AcceptAsync(token).ConfigureAwait(false);
                    await connection.StartAsync(token).ConfigureAwait(false);

                    var link = Iec104TcpLink.Accept(connection);
                    var session = new Iec104SlaveSession(link, _options, _store);
                    session.CommandRequested += OnSessionCommandRequested;
                    session.LinkStateChanged += OnSessionLinkStateChanged;
                    session.FrameTraced += OnSessionFrameTraced;

                    lock (_gate) _sessions.Add(session);
                    session.Start();

                    _logger?.LogInformation("104 客户端已连接：{Remote}", connection.RemoteEndPoint);
                    RaiseClient(ClientConnected, session, connection.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "104 从站接受连接失败。");
            }
        }

        private void OnStoreValueChanged(object sender, Iec104Value value)
        {
            Iec104SlaveSession[] sessions;
            lock (_gate) sessions = _sessions.ToArray();

            for (var i = 0; i < sessions.Length; i++)
            {
                sessions[i].NotifyValueChanged(value.Ioa);
            }
        }

        private void OnSessionCommandRequested(object sender, Iec104CommandRequestEventArgs e)
        {
            var handler = CommandRequested;
            if (handler == null) return;

            try { handler(this, e); } catch { }
        }

        private void OnSessionLinkStateChanged(object sender, Iec104LinkEventArgs e)
        {
            if (e.State != Iec104LinkState.Disconnected && e.State != Iec104LinkState.Failed) return;

            var session = sender as Iec104SlaveSession;
            if (session == null) return;

            lock (_gate) _sessions.Remove(session);
            RaiseClient(ClientDisconnected, session, null);
        }

        private void OnSessionFrameTraced(object sender, Iec104TraceEventArgs e)
        {
            var handler = FrameTraced;
            if (handler == null) return;

            try { handler(this, e); } catch { }
        }

        private void RaiseClient(
            EventHandler<Iec104ClientEventArgs> handler,
            Iec104SlaveSession session,
            EndPoint remoteEndPoint)
        {
            if (handler == null) return;

            try { handler(this, new Iec104ClientEventArgs(session.SessionId, remoteEndPoint)); } catch { }
        }

        private static IPAddress Resolve(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return IPAddress.Any;

            IPAddress parsed;
            if (IPAddress.TryParse(address, out parsed)) return parsed;

            throw new ArgumentException($"无法解析绑定地址：{address}", nameof(address));
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            await StopAsync().ConfigureAwait(false);
        }
    }
}

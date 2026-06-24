using Microsoft.Extensions.Logging;
using Pipelines.Sockets.Unofficial;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Network.Transports.Tcp;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// TCP服务器适配器，实现IStreamResource接口，用于服务器端通信
    /// </summary>
    public class TcpServerAdapter : IServerListener, IDisposable
    {
        private const int DEFAULT_TIMEOUT_MS = 3000;
        private const int MIN_PORT = 1;
        private const int MAX_PORT = 65535;

        private readonly AsyncLock _sessionsLock = new AsyncLock();
        private readonly IPEndPoint _localEndPoint;
        private readonly List<ClientSession> _activeSessions = new List<ClientSession>();

        private CancellationTokenSource _cancellationTokenSource;
        private TcpTransportListener _listener;
        private bool _disposed;
        private int _listening;

        private int _receiveBufferSize = 8192;
        private TimeSpan _receiveTimeout = TimeSpan.Zero;
        private TimeSpan _sendTimeout = TimeSpan.FromSeconds(30);
        private int _backlog = 100;

        public TcpServerAdapter(string ip, int port)
        {
            if (string.IsNullOrEmpty(ip))
            {
                throw new ArgumentNullException(nameof(ip));
            }

            if (port < MIN_PORT || port > MAX_PORT)
            {
                throw new ArgumentOutOfRangeException(nameof(port), $"Port must be between {MIN_PORT} and {MAX_PORT}");
            }

            _localEndPoint = new IPEndPoint(ResolveAddress(ip), port);
        }

        public TcpServerAdapter(IPEndPoint ipEndPoint)
        {
            _localEndPoint = ipEndPoint ?? throw new ArgumentNullException(nameof(ipEndPoint));
        }

        public string Version => nameof(TcpServerAdapter);

        public ILogger Logger { get; private set; }

        public TimeSpan WaiteInterval { get; set; }

        public bool Connected => Volatile.Read(ref _listening) == 1;

        public TimeSpan ConnectTimeout
        {
            get => TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
            set { }
        }

        public TimeSpan ReceiveTimeout
        {
            get => _receiveTimeout;
            set => _receiveTimeout = value;
        }

        public TimeSpan SendTimeout
        {
            get => _sendTimeout;
            set => _sendTimeout = value;
        }

        public int MaxConnections
        {
            get => _backlog;
            set => _backlog = value;
        }

        public event EventHandler<DataReceivedEventArgs> DataReceived;

        public event EventHandler<SessionEventArgs> ClientConnected;

        public event EventHandler<SessionEventArgs> ClientDisconnected;

        public async Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (!Connected)
            {
                return OperationResult.CreateFailedResult("Server is not listening");
            }

            ValidateBufferArguments(buffer, offset, size);

            List<ClientSession> sessions;
            using (await _sessionsLock.LockAsync().ConfigureAwait(false))
            {
                sessions = _activeSessions.ToList();
            }

            if (sessions.Count == 0)
            {
                Logger?.LogWarning("没有活跃的客户端连接");
                return OperationResult.CreateFailedResult("No active client connections");
            }

            int successCount = 0;
            List<string> errors = null;

            foreach (var session in sessions)
            {
                var result = await session.SendSegmentAsync(buffer, offset, size, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    successCount++;
                    continue;
                }

                if (errors == null)
                {
                    errors = new List<string>();
                }

                errors.Add(result.Message);
            }

            if (successCount > 0)
            {
                return OperationResult.CreateSuccessResult();
            }

            return OperationResult.CreateFailedResult(errors == null || errors.Count == 0
                ? "Send failed for all sessions"
                : string.Join("; ", errors));
        }

        public Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            return Task.FromResult(new OperationResult<int>
            {
                IsSuccess = false,
                Message = "Server mode does not support direct Receive method. Data is handled through events."
            });
        }

        public void UseLogger(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public OperationResult Listen()
        {
            return ListenAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public OperationResult Shutdown()
        {
            return ShutdownAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> ListenAsync()
        {
            ThrowIfDisposed();

            if (Connected)
            {
                return OperationResult.CreateSuccessResult();
            }

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _listener = new TcpTransportListener(_localEndPoint, _backlog);
                await _listener.StartAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                Volatile.Write(ref _listening, 1);
                _ = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
                Logger?.LogInformation("成功在 {EndPoint} 上开始监听", _listener.LocalEndPoint);
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                CleanupListener();
                Logger?.LogError(ex, "开始监听时发生错误");
                return OperationResult.CreateFailedResult(ex);
            }
        }

        public async Task<OperationResult> ShutdownAsync()
        {
            ThrowIfDisposed();

            Volatile.Write(ref _listening, 0);

            try
            {
                var cts = Interlocked.Exchange(ref _cancellationTokenSource, null);
                cts?.Cancel();

                var listener = Interlocked.Exchange(ref _listener, null);
                if (listener != null)
                {
                    await listener.CloseAsync().ConfigureAwait(false);
                    listener.Dispose();
                }

                List<ClientSession> sessions;
                using (await _sessionsLock.LockAsync().ConfigureAwait(false))
                {
                    sessions = _activeSessions.ToList();
                    _activeSessions.Clear();
                }

                foreach (var session in sessions)
                {
                    session.Close(raiseDisconnectedEvent: true);
                }

                cts?.Dispose();
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "停止监听时发生错误");
                return OperationResult.CreateFailedResult(ex);
            }
        }

        public void StreamClose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                ShutdownAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    ShutdownAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch
                {
                }
            }

            _disposed = true;
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpTransportConnection connection = null;
                try
                {
                    connection = (TcpTransportConnection)await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                    await connection.StartAsync(cancellationToken).ConfigureAwait(false);
                    var session = new ClientSession(connection, this);

                    using (await _sessionsLock.LockAsync().ConfigureAwait(false))
                    {
                        _activeSessions.Add(session);
                    }

                    ClientConnected?.Invoke(this, new SessionEventArgs(session));
                    _ = Task.Run(() => session.RunAsync(cancellationToken));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (InvalidOperationException)
                {
                    if (cancellationToken.IsCancellationRequested || _listener == null)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "接受客户端连接时发生错误");
                    connection?.Dispose();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task RemoveSessionAsync(ClientSession session, bool raiseDisconnectedEvent)
        {
            bool removed;
            using (await _sessionsLock.LockAsync().ConfigureAwait(false))
            {
                removed = _activeSessions.Remove(session);
            }

            if (removed && raiseDisconnectedEvent)
            {
                ClientDisconnected?.Invoke(this, new SessionEventArgs(session));
            }
        }

        private void HandleReceivedFrame(ClientSession session, byte[] frame)
        {
            if (session == null || frame == null || frame.Length == 0)
            {
                return;
            }

            DataReceived?.Invoke(this, new DataReceivedEventArgs(session, frame));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TcpServerAdapter));
            }
        }

        private void CleanupListener()
        {
            Volatile.Write(ref _listening, 0);
            try
            {
                _listener?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _listener = null;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private static void ValidateBufferArguments(byte[] buffer, int offset, int size)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");
            }
        }

        private static IPAddress ResolveAddress(string ip)
        {
            if (ip.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
                ip.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.Any;
            }

            if (IPAddress.TryParse(ip, out var address))
            {
                return address;
            }

            try
            {
                return Dns.GetHostEntry(ip).AddressList?.FirstOrDefault()
                    ?? throw new ArgumentException($"Could not resolve host name: {ip}", nameof(ip));
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid IP address or host name: {ip}", nameof(ip), ex);
            }
        }

        private sealed class ClientSession : INetworkSession, IDisposable
        {
            private readonly TcpTransportConnection _connection;
            private readonly TcpServerAdapter _server;
            private readonly Stream _stream;
            private readonly AsyncLock _sendLock = new AsyncLock();
            private readonly List<byte> _pendingBuffer = new List<byte>();
            private readonly byte[] _receiveBuffer;
            private int _closed;

            public ClientSession(TcpTransportConnection connection, TcpServerAdapter server)
            {
                _connection = connection ?? throw new ArgumentNullException(nameof(connection));
                _server = server ?? throw new ArgumentNullException(nameof(server));
                _stream = StreamConnection.GetDuplex(connection.Transport, nameof(TcpServerAdapter));
                _receiveBuffer = new byte[server._receiveBufferSize];
                Id = Guid.NewGuid();
            }

            public Guid Id { get; }

            public EndPoint RemoteEndPoint => _connection.RemoteEndPoint;

            public async Task RunAsync(CancellationToken cancellationToken)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _closed) == 0)
                    {
                        try
                        {
                            var readTask = _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length, cancellationToken);
                            int bytesRead = await ReadWithTimeoutAsync(readTask, _server._receiveTimeout, cancellationToken).ConfigureAwait(false);
                            if (bytesRead == 0)
                            {
                                break;
                            }

                            AppendAndDispatchFrames(_receiveBuffer, bytesRead);
                        }
                        catch (TimeoutException)
                        {
                            continue;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    _server.Logger?.LogError(ex, "接收客户端 {EndPoint} 数据时发生错误", RemoteEndPoint);
                }
                finally
                {
                    Close(raiseDisconnectedEvent: true);
                }
            }

            public Task<OperationResult> SendAsync(byte[] data)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }

                return SendSegmentAsync(data, 0, data.Length, CancellationToken.None);
            }

            public async Task<OperationResult> SendSegmentAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
            {
                if (Volatile.Read(ref _closed) == 1)
                {
                    return OperationResult.CreateFailedResult("Client connection is closed");
                }

                ValidateBufferArguments(buffer, offset, size);

                using (await _sendLock.LockAsync().ConfigureAwait(false))
                {
                    try
                    {
                        var sendBuffer = new byte[size];
                        Buffer.BlockCopy(buffer, offset, sendBuffer, 0, size);

                        var writeTask = _stream.WriteAsync(sendBuffer, 0, sendBuffer.Length, cancellationToken);
                        await WaitWithTimeoutAsync(writeTask, _server._sendTimeout, cancellationToken).ConfigureAwait(false);
                        await WaitWithTimeoutAsync(_stream.FlushAsync(cancellationToken), _server._sendTimeout, cancellationToken).ConfigureAwait(false);
                        return OperationResult.CreateSuccessResult();
                    }
                    catch (TimeoutException)
                    {
                        return OperationResult.CreateFailedResult($"Send operation timed out after {_server._sendTimeout.TotalMilliseconds}ms");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return OperationResult.CreateFailedResult("Send operation was cancelled");
                    }
                    catch (Exception ex)
                    {
                        return OperationResult.CreateFailedResult(ex.Message);
                    }
                }
            }

            public void Close()
            {
                Close(raiseDisconnectedEvent: true);
            }

            public void Close(bool raiseDisconnectedEvent)
            {
                if (Interlocked.Exchange(ref _closed, 1) != 0)
                {
                    return;
                }

                try
                {
                    _stream.Dispose();
                }
                catch
                {
                }

                try
                {
                    _connection.CloseAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch
                {
                }

                _server.RemoveSessionAsync(this, raiseDisconnectedEvent).ConfigureAwait(false).GetAwaiter().GetResult();
            }

            public void Dispose()
            {
                Close(raiseDisconnectedEvent: true);
            }

            private void AppendAndDispatchFrames(byte[] buffer, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    _pendingBuffer.Add(buffer[i]);
                }

                while (TryExtractFrame(_pendingBuffer, out var frame))
                {
                    _server.HandleReceivedFrame(this, frame);
                }
            }

            private static bool TryExtractFrame(List<byte> buffer, out byte[] frame)
            {
                frame = null;
                if (buffer.Count == 0)
                {
                    return false;
                }

                if (LooksLikeTpkt(buffer))
                {
                    if (buffer.Count < 4)
                    {
                        return false;
                    }

                    int totalLength = (buffer[2] << 8) | buffer[3];
                    if (totalLength < 4)
                    {
                        frame = buffer.ToArray();
                        buffer.Clear();
                        return true;
                    }

                    if (buffer.Count < totalLength)
                    {
                        return false;
                    }

                    frame = buffer.GetRange(0, totalLength).ToArray();
                    buffer.RemoveRange(0, totalLength);
                    return true;
                }

                if (buffer.Count < 6)
                {
                    return false;
                }

                if (LooksLikeMbap(buffer))
                {
                    int totalLength = 6 + ((buffer[4] << 8) | buffer[5]);
                    if (totalLength < 8)
                    {
                        frame = buffer.ToArray();
                        buffer.Clear();
                        return true;
                    }

                    if (buffer.Count < totalLength)
                    {
                        return false;
                    }

                    frame = buffer.GetRange(0, totalLength).ToArray();
                    buffer.RemoveRange(0, totalLength);
                    return true;
                }

                frame = buffer.ToArray();
                buffer.Clear();
                return true;
            }

            private static bool LooksLikeTpkt(List<byte> buffer)
            {
                return buffer.Count > 0 && buffer[0] == 0x03 && (buffer.Count == 1 || buffer[1] == 0x00);
            }

            private static bool LooksLikeMbap(List<byte> buffer)
            {
                if (buffer.Count < 6)
                {
                    return false;
                }

                if (buffer[2] != 0x00 || buffer[3] != 0x00)
                {
                    return false;
                }

                int length = (buffer[4] << 8) | buffer[5];
                return length >= 2;
            }

            private static async Task<int> ReadWithTimeoutAsync(Task<int> readTask, TimeSpan timeout, CancellationToken cancellationToken)
            {
                if (timeout <= TimeSpan.Zero)
                {
                    return await readTask.ConfigureAwait(false);
                }

                var timeoutTask = Task.Delay(timeout, cancellationToken);
                var completedTask = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException();
                }

                return await readTask.ConfigureAwait(false);
            }

            private static async Task WaitWithTimeoutAsync(Task task, TimeSpan timeout, CancellationToken cancellationToken)
            {
                if (timeout <= TimeSpan.Zero)
                {
                    await task.ConfigureAwait(false);
                    return;
                }

                var timeoutTask = Task.Delay(timeout, cancellationToken);
                var completedTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException();
                }

                await task.ConfigureAwait(false);
            }
        }
    }
}

using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Network;
using Microsoft.Extensions.Logging;

namespace Wombat.Network.Sockets
{
    public sealed class TcpSocketSession
    {
        #region Fields

        private static readonly ILogger _logger;
        private TcpClient _tcpClient;
        private readonly TcpSocketServerConfiguration _configuration;
        private readonly ISegmentBufferManager _bufferManager;
        private readonly ITcpSocketServerEventDispatcher _dispatcher;
        private readonly TcpSocketServer _server;
        private readonly string _sessionKey;
        private Stream _stream;
        private ArraySegment<byte> _receiveBuffer = default(ArraySegment<byte>);
        private int _receiveBufferOffset = 0;
        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _localEndPoint;

        private int _state;
        private const int _none = 0;
        private const int _connecting = 1;
        private const int _connected = 2;
        private const int _disposed = 5;
        
        // 心跳相关字段
        private Timer _heartbeatTimer;
        private DateTime _lastReceivedHeartbeatTime;
        private int _missedHeartbeats;
        private readonly object _heartbeatLock = new object();

        #endregion

        #region Constructors

        public TcpSocketSession(
            TcpClient tcpClient,
            TcpSocketServerConfiguration configuration,
            ISegmentBufferManager bufferManager,
            ITcpSocketServerEventDispatcher dispatcher,
            TcpSocketServer server)
        {
            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");
            if (configuration == null)
                throw new ArgumentNullException("configuration");
            if (bufferManager == null)
                throw new ArgumentNullException("bufferManager");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");
            if (server == null)
                throw new ArgumentNullException("server");

            _tcpClient = tcpClient;
            _configuration = configuration;
            _bufferManager = bufferManager;
            _dispatcher = dispatcher;
            _server = server;

            _sessionKey = Guid.NewGuid().ToString();
            this.StartTime = DateTime.UtcNow;

            SetSocketOptions();

            _remoteEndPoint = this.RemoteEndPoint;
            _localEndPoint = this.LocalEndPoint;
        }

        #endregion

        #region Properties

        public string SessionKey { get { return _sessionKey; } }
        public DateTime StartTime { get; private set; }
        public TimeSpan ConnectTimeout { get { return _configuration.ConnectTimeout; } }

        private bool Connected { get { return _tcpClient != null && _tcpClient.Client.Connected; } }
        public IPEndPoint RemoteEndPoint { get { return Connected ? (IPEndPoint)_tcpClient.Client.RemoteEndPoint : _remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return Connected ? (IPEndPoint)_tcpClient.Client.LocalEndPoint : _localEndPoint; } }

        public Socket Socket { get { return Connected ? _tcpClient.Client : null; } }
        public Stream Stream { get { return _stream; } }
        public TcpSocketServer Server { get { return _server; } }

        public TcpSocketConnectionState State
        {
            get
            {
                switch (_state)
                {
                    case _none:
                        return TcpSocketConnectionState.None;
                    case _connecting:
                        return TcpSocketConnectionState.Connecting;
                    case _connected:
                        return TcpSocketConnectionState.Connected;
                    case _disposed:
                        return TcpSocketConnectionState.Closed;
                    default:
                        return TcpSocketConnectionState.Closed;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("SessionKey[{0}], RemoteEndPoint[{1}], LocalEndPoint[{2}]",
                this.SessionKey, this.RemoteEndPoint, this.LocalEndPoint);
        }

        #endregion

        #region Process

        internal async Task Start()
        {
            int origin = Interlocked.CompareExchange(ref _state, _connecting, _none);
            if (origin == _disposed)
            {
                throw new ObjectDisposedException("This tcp socket session has been disposed when connecting.");
            }
            else if (origin != _none)
            {
                throw new InvalidOperationException("This tcp socket session is in invalid state when connecting.");
            }

            try
            {
                var negotiator = NegotiateStream(_tcpClient.GetStream());
                if (!negotiator.Wait(ConnectTimeout))
                {
                    await Close(false); // ssl negotiation timeout
                    throw new TimeoutException(string.Format(
                        "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", this.RemoteEndPoint, ConnectTimeout));
                }
                _stream = negotiator.Result;

                if (_receiveBuffer == default(ArraySegment<byte>))
                    _receiveBuffer = _bufferManager.BorrowBuffer();
                _receiveBufferOffset = 0;

                if (Interlocked.CompareExchange(ref _state, _connected, _connecting) != _connecting)
                {
                    await Close(false); // connected with wrong state
                    throw new ObjectDisposedException("This tcp socket session has been disposed after connected.");
                }

                _logger?.LogDebug("Session started for [{0}] on [{1}] in dispatcher [{2}] with session count [{3}].",
                    this.RemoteEndPoint,
                    this.StartTime.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                    _dispatcher.GetType().Name,
                    this.Server.SessionCount);
                bool isErrorOccurredInUserSide = false;
                try
                {
                    await _dispatcher.OnSessionStarted(this);
                }
                catch (Exception ex) // catch all exceptions from out-side
                {
                    isErrorOccurredInUserSide = true;
                    await HandleUserSideError(ex);
                }

                if (!isErrorOccurredInUserSide)
                {
                    // 启动心跳机制
                    if (_configuration.EnableHeartbeat)
                    {
                        StartHeartbeat();
                    }
                    
                    await Process();
                }
                else
                {
                    await Close(true); // user side handle tcp connection error occurred
                }
            }
            catch (Exception ex) // catch exceptions then log then re-throw
            {
                _logger?.LogError(string.Format("Session [{0}] exception occurred, [{1}].", this, ex.Message), ex);
                await Close(true); // handle tcp connection error occurred
                throw;
            }
        }

        private async Task Process()
        {
            try
            {
                int frameLength;
                byte[] payload;
                int payloadOffset;
                int payloadCount;
                int consumedLength = 0;

                while (State == TcpSocketConnectionState.Connected)
                {
                    int receiveCount = await _stream.ReadAsync(
                        _receiveBuffer.Array,
                        _receiveBuffer.Offset + _receiveBufferOffset,
                        _receiveBuffer.Count - _receiveBufferOffset);
                    if (receiveCount == 0)
                        break;

                    SegmentBufferDeflector.ReplaceBuffer(_bufferManager, ref _receiveBuffer, ref _receiveBufferOffset, receiveCount);
                    consumedLength = 0;

                    while (true)
                    {
                        frameLength = 0;
                        payload = null;
                        payloadOffset = 0;
                        payloadCount = 0;

                        if (_configuration.FrameBuilder.Decoder.TryDecodeFrame(
                            _receiveBuffer.Array,
                            _receiveBuffer.Offset + consumedLength,
                            _receiveBufferOffset - consumedLength,
                            out frameLength, out payload, out payloadOffset, out payloadCount))
                        {
                            try
                            {
                                // 检查是否为心跳包
                                if (HeartbeatManager.IsHeartbeatPacket(payload, payloadOffset, payloadCount))
                                {
                                    // 处理接收到的心跳包
                                    ProcessHeartbeatPacket(payload, payloadOffset);
                                }
                                else
                                {
                                    // 处理正常数据包
                                    await _dispatcher.OnSessionDataReceived(this, payload, payloadOffset, payloadCount);
                                }
                            }
                            catch (Exception ex) // catch all exceptions from out-side
                            {
                                await HandleUserSideError(ex);
                            }
                            finally
                            {
                                consumedLength += frameLength;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (_receiveBuffer != null && _receiveBuffer.Array != null)
                    {
                        SegmentBufferDeflector.ShiftBuffer(_bufferManager, consumedLength, ref _receiveBuffer, ref _receiveBufferOffset);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // looking forward to a graceful quit from the ReadAsync but the inside EndRead will raise the ObjectDisposedException,
                // so a gracefully close for the socket should be a Shutdown, but we cannot avoid the Close triggers this happen.
            }
            catch (Exception ex)
            {
                await HandleReceiveOperationException(ex);
            }
            finally
            {
                await Close(true); // read async buffer returned, remote notifies closed
            }
        }

        private void SetSocketOptions()
        {
            _tcpClient.ReceiveBufferSize = _configuration.ReceiveBufferSize;
            _tcpClient.SendBufferSize = _configuration.SendBufferSize;
            _tcpClient.ReceiveTimeout = (int)_configuration.ReceiveTimeout.TotalMilliseconds;
            _tcpClient.SendTimeout = (int)_configuration.SendTimeout.TotalMilliseconds;
            _tcpClient.NoDelay = _configuration.NoDelay;
            _tcpClient.LingerState = _configuration.LingerState;

            if (_configuration.KeepAlive)
            {
                _tcpClient.Client.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.KeepAlive,
                    (int)_configuration.KeepAliveInterval.TotalMilliseconds);
            }

            _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _configuration.ReuseAddress);
        }

        private async Task<Stream> NegotiateStream(Stream stream)
        {
            if (!_configuration.SslEnabled)
                return stream;

            var validateRemoteCertificate = new RemoteCertificateValidationCallback(
                (object sender,
                X509Certificate certificate,
                X509Chain chain,
                SslPolicyErrors sslPolicyErrors)
                =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None)
                        return true;

                    if (_configuration.SslPolicyErrorsBypassed)
                        return true;
                    else
                        _logger?.LogError("Session [{0}] error occurred when validating remote certificate: [{1}], [{2}].",
                            this, this.RemoteEndPoint, sslPolicyErrors);

                    return false;
                });

            var sslStream = new SslStream(
                stream,
                false,
                validateRemoteCertificate,
                null,
                _configuration.SslEncryptionPolicy);

            if (!_configuration.SslClientCertificateRequired)
            {
                await sslStream.AuthenticateAsServerAsync(
                    _configuration.SslServerCertificate); // The X509Certificate used to authenticate the server.
            }
            else
            {
                await sslStream.AuthenticateAsServerAsync(
                    _configuration.SslServerCertificate, // The X509Certificate used to authenticate the server.
                    _configuration.SslClientCertificateRequired, // A Boolean value that specifies whether the client must supply a certificate for authentication.
                    _configuration.SslEnabledProtocols, // The SslProtocols value that represents the protocol used for authentication.
                    _configuration.SslCheckCertificateRevocation); // A Boolean value that specifies whether the certificate revocation list is checked during authentication.
            }

            // When authentication succeeds, you must check the IsEncrypted and IsSigned properties 
            // to determine what security services are used by the SslStream. 
            // Check the IsMutuallyAuthenticated property to determine whether mutual authentication occurred.
            _logger?.LogDebug(
                "Ssl Stream: SslProtocol[{0}], IsServer[{1}], IsAuthenticated[{2}], IsEncrypted[{3}], IsSigned[{4}], IsMutuallyAuthenticated[{5}], "
                + "HashAlgorithm[{6}], HashStrength[{7}], KeyExchangeAlgorithm[{8}], KeyExchangeStrength[{9}], CipherAlgorithm[{10}], CipherStrength[{11}].",
                sslStream.SslProtocol,
                sslStream.IsServer,
                sslStream.IsAuthenticated,
                sslStream.IsEncrypted,
                sslStream.IsSigned,
                sslStream.IsMutuallyAuthenticated,
                sslStream.HashAlgorithm,
                sslStream.HashStrength,
                sslStream.KeyExchangeAlgorithm,
                sslStream.KeyExchangeStrength,
                sslStream.CipherAlgorithm,
                sslStream.CipherStrength);

            return sslStream;
        }

        #endregion

        #region Heartbeat Management

        /// <summary>
        /// 启动心跳机制
        /// </summary>
        private void StartHeartbeat()
        {
            if (!_configuration.EnableHeartbeat || _state != _connected)
                return;

            lock (_heartbeatLock)
            {
                StopHeartbeat(); // 确保停止之前的定时器
                
                _lastReceivedHeartbeatTime = DateTime.UtcNow;
                _missedHeartbeats = 0;
                
                // 创建心跳定时器
                _heartbeatTimer = new Timer(
                    HeartbeatTimerCallback, 
                    null, 
                    _configuration.HeartbeatInterval, 
                    _configuration.HeartbeatInterval);
                
                HeartbeatManager.LogHeartbeat(_logger, "启动会话心跳，会话ID: {0}, 间隔: {1}秒", 
                    SessionKey, _configuration.HeartbeatInterval.TotalSeconds);
            }
        }

        /// <summary>
        /// 停止心跳机制
        /// </summary>
        private void StopHeartbeat()
        {
            lock (_heartbeatLock)
            {
                if (_heartbeatTimer != null)
                {
                    _heartbeatTimer.Dispose();
                    _heartbeatTimer = null;
                    HeartbeatManager.LogHeartbeat(_logger, "停止会话心跳，会话ID: {0}", SessionKey);
                }
            }
        }

        /// <summary>
        /// 心跳定时器回调
        /// </summary>
        private async void HeartbeatTimerCallback(object state)
        {
            if (_state != _connected)
            {
                StopHeartbeat();
                return;
            }
            
            try
            {
                // 检查是否需要发送心跳
                await SendHeartbeatPacket();
                
                // 检查心跳超时
                CheckHeartbeatTimeout();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "会话心跳处理中发生错误，会话ID: {0}", SessionKey);
            }
        }

        /// <summary>
        /// 发送心跳包
        /// </summary>
        private async Task SendHeartbeatPacket()
        {
            try
            {
                if (_state != _connected)
                    return;
                    
                byte[] heartbeatPacket = HeartbeatManager.CreateHeartbeatPacket();
                await SendAsync(heartbeatPacket);
                
                HeartbeatManager.LogHeartbeat(_logger, "服务器发送心跳包到客户端会话: {0}", this.RemoteEndPoint);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "发送心跳包时发生错误，会话ID: {0}", SessionKey);
            }
        }

        /// <summary>
        /// 处理接收到的心跳包
        /// </summary>
        private void ProcessHeartbeatPacket(byte[] data, int offset)
        {
            try
            {
                // 更新最后收到心跳的时间
                lock (_heartbeatLock)
                {
                    _lastReceivedHeartbeatTime = DateTime.UtcNow;
                    _missedHeartbeats = 0;
                }
                
                // 提取心跳包时间戳，用于计算网络延迟
                long timestamp = HeartbeatManager.ExtractTimestamp(data, offset);
                if (timestamp > 0)
                {
                    long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long delay = currentTime - timestamp;
                    
                    HeartbeatManager.LogHeartbeat(_logger, "服务器收到客户端心跳包，会话ID: {0}, 客户端: {1}, 网络延迟: {2}ms", 
                        SessionKey, this.RemoteEndPoint, delay);
                }
                else
                {
                    HeartbeatManager.LogHeartbeat(_logger, "服务器收到客户端心跳包，会话ID: {0}", SessionKey);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "处理心跳包时发生错误，会话ID: {0}", SessionKey);
            }
        }

        /// <summary>
        /// 检查心跳超时
        /// </summary>
        private void CheckHeartbeatTimeout()
        {
            if (!_configuration.EnableHeartbeat || _state != _connected)
                return;
                
            lock (_heartbeatLock)
            {
                // 计算自上次接收心跳包以来的时间
                TimeSpan elapsed = DateTime.UtcNow - _lastReceivedHeartbeatTime;
                
                // 如果超过心跳超时时间，增加计数
                if (elapsed > _configuration.HeartbeatTimeout)
                {
                    _missedHeartbeats++;
                    HeartbeatManager.LogHeartbeat(_logger, "会话心跳超时，已连续 {0}/{1} 次未收到客户端心跳，会话ID: {2}", 
                        _missedHeartbeats, _configuration.MaxMissedHeartbeats, SessionKey);
                    
                    // 如果超过最大允许的心跳丢失次数，关闭连接
                    if (_missedHeartbeats >= _configuration.MaxMissedHeartbeats)
                    {
                        HeartbeatManager.LogHeartbeat(_logger, "会话心跳连续 {0} 次超时，关闭连接，会话ID: {1}", 
                            _missedHeartbeats, SessionKey);
                            
                        // 在后台线程中关闭连接，避免定时器回调中直接同步关闭
                        Task.Run(async () => await Close(true));
                    }
                }
            }
        }
        
        #endregion

        #region Close

        public async Task Close()
        {
            await Close(true); // close by external
        }

        private async Task Close(bool shallNotifyUserSide)
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            // 停止心跳检测
            StopHeartbeat();
            
            Shutdown();

            if (shallNotifyUserSide)
            {
                _logger?.LogDebug("Session closed for [{0}] on [{1}] in dispatcher [{2}] with session count [{3}].",
                    this.RemoteEndPoint,
                    DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                    _dispatcher.GetType().Name,
                    this.Server.SessionCount - 1);
                try
                {
                    await _dispatcher.OnSessionClosed(this);
                }
                catch (Exception ex) // catch all exceptions from out-side
                {
                    await HandleUserSideError(ex);
                }
            }

            Clean();
        }

        public void Shutdown()
        {
            // The correct way to shut down the connection (especially if you are in a full-duplex conversation) 
            // is to call socket.Shutdown(SocketShutdown.Send) and give the remote party some time to close 
            // their send channel. This ensures that you receive any pending data instead of slamming the 
            // connection shut. ObjectDisposedException should never be part of the normal application flow.
            if (_tcpClient != null && _tcpClient.Connected)
            {
                _tcpClient.Client.Shutdown(SocketShutdown.Send);
            }
        }

        private void Clean()
        {
            try
            {
                try
                {
                    if (_stream != null)
                    {
                        _stream.Dispose();
                    }
                }
                catch { }
                try
                {
                    if (_tcpClient != null)
                    {
                        _tcpClient.Close();
                    }
                }
                catch { }
            }
            catch { }
            finally
            {
                _stream = null;
                _tcpClient = null;
            }

            if (_receiveBuffer != default(ArraySegment<byte>))
                _configuration.BufferManager.ReturnBuffer(_receiveBuffer);
            _receiveBuffer = default(ArraySegment<byte>);
            _receiveBufferOffset = 0;
        }

        #endregion

        #region Exception Handler

        private async Task HandleSendOperationException(Exception ex)
        {
            if (IsSocketTimeOut(ex))
            {
                await CloseIfShould(ex);
                throw new TcpSocketException(ex.Message, new TimeoutException(ex.Message, ex));
            }

            await CloseIfShould(ex);
            throw new TcpSocketException(ex.Message, ex);
        }

        private async Task HandleReceiveOperationException(Exception ex)
        {
            if (IsSocketTimeOut(ex))
            {
                await CloseIfShould(ex);
                throw new TcpSocketException(ex.Message, new TimeoutException(ex.Message, ex));
            }

            await CloseIfShould(ex);
            throw new TcpSocketException(ex.Message, ex);
        }

        private bool IsSocketTimeOut(Exception ex)
        {
            return ex is IOException
                && ex.InnerException != null
                && ex.InnerException is SocketException
                && (ex.InnerException as SocketException).SocketErrorCode == SocketError.TimedOut;
        }

        private async Task<bool> CloseIfShould(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException
                || ex is NullReferenceException // buffer array operation
                || ex is ArgumentException      // buffer array operation
                )
            {
                _logger?.LogError(ex.Message, ex);

                await Close(true); // catch specified exception then intend to close the session

                return true;
            }

            return false;
        }

        private async Task HandleUserSideError(Exception ex)
        {
            _logger?.LogError(string.Format("Session [{0}] error occurred in user side [{1}].", this, ex.Message), ex);
            await Task.CompletedTask;
        }

        #endregion

        #region Send

        public async Task SendAsync(byte[] data)
        {
            await SendAsync(data, 0, data.Length);
        }

        public async Task SendAsync(byte[] data, int offset, int count)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            if (State != TcpSocketConnectionState.Connected)
            {
                throw new InvalidOperationException("This session has not connected.");
            }

            try
            {
                byte[] frameBuffer;
                int frameBufferOffset;
                int frameBufferLength;
                _configuration.FrameBuilder.Encoder.EncodeFrame(data, offset, count, out frameBuffer, out frameBufferOffset, out frameBufferLength);

                await _stream.WriteAsync(frameBuffer, frameBufferOffset, frameBufferLength);
            }
            catch (Exception ex)
            {
                await HandleSendOperationException(ex);
            }
        }

        #endregion
    }
}

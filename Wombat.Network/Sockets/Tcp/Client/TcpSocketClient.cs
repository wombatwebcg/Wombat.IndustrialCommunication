using System;
using System.Collections.Concurrent;
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
    public class TcpSocketClient
    {
        #region Fields

        private  ILogger _logger;
        private Socket _socket;
        private NetworkStream _networkStream;
        private readonly ITcpSocketClientEventDispatcher _dispatcher;
        private readonly TcpSocketClientConfiguration _configuration;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IPEndPoint _localEndPoint;
        private Stream _stream;
        private ArraySegment<byte> _receiveBuffer = default(ArraySegment<byte>);
        private int _receiveBufferOffset = 0;

        private int _state;
        private const int _none = 0;
        private const int _connecting = 1;
        private const int _connected = 2;
        private const int _closed = 5;
        
        // 新增字段，用于异步处理
        private CancellationTokenSource _processCts;
        private Task _processTask;

        // 字段，用于存储接收到的数据 - 使用循环缓冲区模仿原生Socket行为
        private CircularBuffer _receiveDataBuffer;
        private readonly AutoResetEvent _dataAvailableEvent = new AutoResetEvent(false);
        private readonly object _dataLock = new object();
        
        // 心跳相关字段
        private Timer _heartbeatTimer;
        private DateTime _lastReceivedHeartbeatTime;
        private int _missedHeartbeats;
        private readonly object _heartbeatLock = new object();

        #endregion

        #region Constructors

        public TcpSocketClient(IPAddress remoteAddress, int remotePort, IPAddress localAddress, int localPort, ITcpSocketClientEventDispatcher dispatcher, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), new IPEndPoint(localAddress, localPort), dispatcher, configuration)
        {
        }

        public TcpSocketClient(IPAddress remoteAddress, int remotePort, IPEndPoint localEP, ITcpSocketClientEventDispatcher dispatcher, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), localEP, dispatcher, configuration)
        {
        }

        public TcpSocketClient(IPAddress remoteAddress, int remotePort, ITcpSocketClientEventDispatcher dispatcher, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), dispatcher, configuration)
        {
        }

        public TcpSocketClient(IPEndPoint remoteEP, ITcpSocketClientEventDispatcher dispatcher, TcpSocketClientConfiguration configuration = null)
            : this(remoteEP, null, dispatcher, configuration)
        {
        }

        public TcpSocketClient(IPEndPoint remoteEP, IPEndPoint localEP, ITcpSocketClientEventDispatcher dispatcher, TcpSocketClientConfiguration configuration = null)
        {
            if (remoteEP == null)
                throw new ArgumentNullException("remoteEP");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            _remoteEndPoint = remoteEP;
            _localEndPoint = localEP;
            _dispatcher = dispatcher;
            _configuration = configuration ?? new TcpSocketClientConfiguration();

            if (_configuration.BufferManager == null)
                throw new InvalidProgramException("The buffer manager in configuration cannot be null.");
            if (_configuration.FrameBuilder == null)
                throw new InvalidProgramException("The frame handler in configuration cannot be null.");
                
            // 初始化循环缓冲区
            _receiveDataBuffer = new CircularBuffer(_configuration.MaxReceiveBufferSize);
        }

        public TcpSocketClient(IPAddress remoteAddress, int remotePort, IPAddress localAddress, int localPort,
            Func<TcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<TcpSocketClient, Task> onServerConnected = null,
            Func<TcpSocketClient, Task> onServerDisconnected = null,
            TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), new IPEndPoint(localAddress, localPort),
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public TcpSocketClient(IPAddress remoteAddress, int remotePort, IPEndPoint localEP,
            Func<TcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<TcpSocketClient, Task> onServerConnected = null,
            Func<TcpSocketClient, Task> onServerDisconnected = null,
            TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), localEP,
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public TcpSocketClient(IPAddress remoteAddress, int remotePort,
            Func<TcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<TcpSocketClient, Task> onServerConnected = null,
            Func<TcpSocketClient, Task> onServerDisconnected = null,
            TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort),
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public TcpSocketClient(IPEndPoint remoteEP,
            Func<TcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<TcpSocketClient, Task> onServerConnected = null,
            Func<TcpSocketClient, Task> onServerDisconnected = null,
            TcpSocketClientConfiguration configuration = null)
            : this(remoteEP, null,
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public TcpSocketClient(IPEndPoint remoteEP, IPEndPoint localEP,
            Func<TcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<TcpSocketClient, Task> onServerConnected = null,
            Func<TcpSocketClient, Task> onServerDisconnected = null,
            TcpSocketClientConfiguration configuration = null)
            : this(remoteEP, localEP,
                 new DefaultTcpSocketClientEventDispatcher(onServerDataReceived, onServerConnected, onServerDisconnected),
                 configuration)
        {
        }

        #endregion

        #region Properties


        public TcpSocketClientConfiguration TcpSocketClientConfiguration { get { return _configuration; } }

        public bool Connected
        {
            get
            {
                return _socket != null && _socket.Connected;
            }
        }
        public IPEndPoint RemoteEndPoint { get { return Connected ? (IPEndPoint)_socket.RemoteEndPoint : _remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return Connected ? (IPEndPoint)_socket.LocalEndPoint : _localEndPoint; } }

        /// <summary>
        /// 指示接收缓冲区是否已满
        /// </summary>
        public bool IsBufferFull
        {
            get
            {
                return _receiveDataBuffer?.IsFull ?? false;
            }
        }

        /// <summary>
        /// 获取当前缓冲区使用率（百分比）
        /// </summary>
        public double BufferUsage
        {
            get
            {
                return _receiveDataBuffer?.Usage ?? 0.0;
            }
        }

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
                    case _closed:
                        return TcpSocketConnectionState.Closed;
                    default:
                        return TcpSocketConnectionState.Closed;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("RemoteEndPoint[{0}], LocalEndPoint[{1}]",
                this.RemoteEndPoint, this.LocalEndPoint);
        }

        #endregion

        #region Connect

        public async Task Connect(CancellationToken cancellationToken = default)
        {
            int origin = Interlocked.Exchange(ref _state, _connecting);
            if (!(origin == _none || origin == _closed))
            {
                await Close(false); // connecting with wrong state
                throw new InvalidOperationException("This tcp socket client is in invalid state when connecting.");
            }

            Clean(); // force to clean

            CancellationTokenSource timeoutCts = null;
            CancellationTokenSource linkedCts = null;
            
            try
            {
                timeoutCts = new CancellationTokenSource(_configuration.ConnectTimeout);
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                _socket = _localEndPoint != null ?
                    new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) :
                    new Socket(_remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                
                if (_localEndPoint != null)
                {
                    _socket.Bind(_localEndPoint);
                }
                
                SetSocketOptions();

                try
                {
                    var connectTask = _socket.ConnectAsync(_remoteEndPoint);
                    
                    if (await Task.WhenAny(connectTask, Task.Delay(_configuration.ConnectTimeout, cancellationToken)) != connectTask)
                    {
                        await Close(false); // connect timeout
                        throw new TimeoutException(string.Format(
                            "Connect to [{0}] timeout [{1}].", _remoteEndPoint, _configuration.ConnectTimeout));
                    }
                    
                    await connectTask;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    await Close(false); // operation canceled
                    throw; // 重新抛出取消异常
                }

                _networkStream = new NetworkStream(_socket, true);

                try
                {
                    var negotiateTask = NegotiateStreamAsync(_networkStream, cancellationToken);
                    
                    if (await Task.WhenAny(negotiateTask, Task.Delay(_configuration.ConnectTimeout, cancellationToken)) != negotiateTask)
                    {
                        await Close(false); // ssl negotiation timeout
                        throw new TimeoutException(string.Format(
                            "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", _remoteEndPoint, _configuration.ConnectTimeout));
                    }
                    
                    _stream = await negotiateTask;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    await Close(false); // operation canceled
                    throw; // 重新抛出取消异常
                }

                if (_receiveBuffer == default(ArraySegment<byte>))
                    _receiveBuffer = _configuration.BufferManager.BorrowBuffer();
                _receiveBufferOffset = 0;

                if (Interlocked.CompareExchange(ref _state, _connected, _connecting) != _connecting)
                {
                    await Close(false); // connected with wrong state
                    throw new InvalidOperationException("This tcp socket client is in invalid state when connected.");
                }

                _logger?.LogDebug("Connected to server [{0}] with dispatcher [{1}] on [{2}].",
                    this.RemoteEndPoint,
                    _dispatcher.GetType().Name,
                    DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));
                bool isErrorOccurredInUserSide = false;
                try
                {
                    await _dispatcher.OnServerConnected(this);
                }
                catch (Exception ex) // catch all exceptions from out-side
                {
                    isErrorOccurredInUserSide = true;
                    await HandleUserSideError(ex);
                }

                if (!isErrorOccurredInUserSide)
                {
                    // 使用取消令牌启动处理任务
                    _processCts = new CancellationTokenSource();
                    _processTask = ProcessAsync(_processCts.Token);
                    
                    // 启动心跳机制
                    if (_configuration.EnableHeartbeat)
                    {
                        StartHeartbeat();
                    }
                }
                else
                {
                    await Close(true); // user side handle tcp connection error occurred
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await Close(false); // operation canceled
                throw; // 重新抛出取消异常
            }
            catch (Exception ex) // catch exceptions then log then re-throw
            {
                _logger?.LogError(ex.Message, ex);
                await Close(true); // handle tcp connection error occurred
                throw;
            }
            finally
            {
                timeoutCts?.Dispose();
                linkedCts?.Dispose();
            }
        }
        
        private async Task ProcessAsync(CancellationToken cancellationToken)
        {
            try
            {
                int frameLength;
                byte[] payload;
                int payloadOffset;
                int payloadCount;
                int consumedLength = 0;

                while (State == TcpSocketConnectionState.Connected && !cancellationToken.IsCancellationRequested)
                {
                    // 使用带超时的读取
                    CancellationTokenSource timeoutCts = null;
                    CancellationTokenSource linkedCts = null;
                    
                    try
                    {
                        timeoutCts = new CancellationTokenSource(_configuration.ReceiveTimeout);
                        linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                        
                        var receiveTask = _stream.ReadAsync(
                            _receiveBuffer.Array,
                            _receiveBuffer.Offset + _receiveBufferOffset,
                            _receiveBuffer.Count - _receiveBufferOffset);
                            
                        // 添加超时
                        if (await Task.WhenAny(receiveTask, Task.Delay(_configuration.ReceiveTimeout, linkedCts.Token)) != receiveTask)
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                // 读取超时，可以选择继续或关闭，这里选择继续
                                _logger?.LogWarning("Read operation timed out after {0}ms, continuing", 
                                    _configuration.ReceiveTimeout.TotalMilliseconds);
                                continue;
                            }
                            else
                            {
                                // 外部请求取消
                                break;
                            }
                        }
                        
                        // 获取接收结果
                        int receiveCount = await receiveTask;
                        
                        if (receiveCount == 0)
                            break;

                        SegmentBufferDeflector.ReplaceBuffer(_configuration.BufferManager, ref _receiveBuffer, ref _receiveBufferOffset, receiveCount);
                        consumedLength = 0;
                        
                        // 处理收到的数据
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
                                        // 将接收到的数据写入循环缓冲区（累积模式，模仿原生Socket行为）
                                        if (payload != null && payloadCount > 0)
                                        {
                                            lock (_dataLock)
                                            {
                                                // 将数据追加到循环缓冲区
                                                int written = _receiveDataBuffer.WriteOverwrite(payload, payloadOffset, payloadCount);
                                                if (written > 0)
                                                {
                                                    _dataAvailableEvent.Set(); // 发出信号，表示有数据可用
                                                }
                                                
                                                // 如果缓冲区已满，记录警告
                                                if (written < payloadCount)
                                                {
                                                    _logger?.LogWarning("接收缓冲区空间不足，丢弃了 {0} 字节数据", payloadCount - written);
                                                }
                                            }
                                        }
                                        
                                        // 使用操作超时
                                        using (var dispatchCts = new CancellationTokenSource(_configuration.OperationTimeout))
                                        using (var dispatchLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(dispatchCts.Token, cancellationToken))
                                        {
                                            var dispatchTask = _dispatcher.OnServerDataReceived(this, payload, payloadOffset, payloadCount);
                                            
                                            // 添加超时
                                            if (await Task.WhenAny(dispatchTask, Task.Delay(_configuration.OperationTimeout, dispatchLinkedCts.Token)) != dispatchTask)
                                            {
                                                if (!cancellationToken.IsCancellationRequested)
                                                {
                                                    _logger?.LogWarning("Dispatch operation timed out after {0}ms", 
                                                        _configuration.OperationTimeout.TotalMilliseconds);
                                                }
                                                else
                                                {
                                                    // 外部请求取消
                                                    return;
                                                }
                                            }
                                            else
                                            {
                                                // 确保调度任务已完成
                                                await dispatchTask;
                                            }
                                        }
                                    }
                                }
                                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                                {
                                    // 外部请求取消，直接退出
                                    return;
                                }
                                catch (Exception ex)
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
                            SegmentBufferDeflector.ShiftBuffer(_configuration.BufferManager, consumedLength, ref _receiveBuffer, ref _receiveBufferOffset);
                        }
                    }
                    finally
                    {
                        timeoutCts?.Dispose();
                        linkedCts?.Dispose();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Graceful exit
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 正常取消，不需要额外处理
            }
            catch (Exception ex)
            {
                await HandleReceiveOperationException(ex);
            }
            finally
            {
                await Close(true); // 关闭连接
            }
        }

        private void SetSocketOptions()
        {
            _socket.ReceiveBufferSize = _configuration.ReceiveBufferSize;
            _socket.SendBufferSize = _configuration.SendBufferSize;
            _socket.ReceiveTimeout = (int)_configuration.ReceiveTimeout.TotalMilliseconds;
            _socket.SendTimeout = (int)_configuration.SendTimeout.TotalMilliseconds;
            
            // 设置NoDelay选项（禁用Nagle算法）
            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, _configuration.NoDelay);
            
            // 设置LingerState
            _socket.LingerState = _configuration.LingerState;

            if (_configuration.KeepAlive)
            {
                _socket.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.KeepAlive,
                    (int)_configuration.KeepAliveInterval.TotalMilliseconds);
            }

            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _configuration.ReuseAddress);
        }

        // 添加新的异步协商方法
        private async Task<Stream> NegotiateStreamAsync(Stream stream, CancellationToken cancellationToken)
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
                        _logger?.LogError("Error occurred when validating remote certificate: [{0}], [{1}].",
                            this.RemoteEndPoint, sslPolicyErrors);

                    return false;
                });

            var sslStream = new SslStream(
                stream,
                false,
                validateRemoteCertificate,
                null,
                _configuration.SslEncryptionPolicy);

            if (_configuration.SslClientCertificates == null || _configuration.SslClientCertificates.Count == 0)
            {
                await sslStream.AuthenticateAsClientAsync( // No client certificates are used in the authentication. The certificate revocation list is not checked during authentication.
                    _configuration.SslTargetHost); // The name of the server that will share this SslStream. The value specified for targetHost must match the name on the server's certificate.
            }
            else
            {
                await sslStream.AuthenticateAsClientAsync(
                    _configuration.SslTargetHost, // The name of the server that will share this SslStream. The value specified for targetHost must match the name on the server's certificate.
                    _configuration.SslClientCertificates, // The X509CertificateCollection that contains client certificates.
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

        #region Logger
        public void UseLogger(ILogger logger)
        {
            _logger = logger;
        }

        #endregion

        #region Close

        public async Task Close()
        {
            await Close(true); // close by external
        }

        private async Task Close(bool shallNotifyUserSide)
        {
            if (Interlocked.Exchange(ref _state, _closed) == _closed)
            {
                return;
            }

            // 停止心跳检测
            StopHeartbeat();
            
            Shutdown();

            if (shallNotifyUserSide)
            {
                _logger?.LogDebug("Disconnected from server [{0}] with dispatcher [{1}] on [{2}].",
                    this.RemoteEndPoint,
                    _dispatcher.GetType().Name,
                    DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));
                try
                {
                    await _dispatcher.OnServerDisconnected(this);
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
            if (_socket != null && _socket.Connected)
            {
                _socket.Shutdown(SocketShutdown.Send);
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
                    if (_networkStream != null)
                    {
                        _networkStream.Dispose();
                    }
                }
                catch { }
                try
                {
                    if (_socket != null)
                    {
                        // 关闭Socket
                        if (_socket.Connected)
                        {
                            try { _socket.Shutdown(SocketShutdown.Both); } catch { }
                            _socket.Close();
                        }
                        _socket.Dispose();
                    }
                }
                catch { }
            }
            catch { }
            finally
            {
                _stream = null;
                _networkStream = null;
                _socket = null;
                
                // 清空接收缓冲区
                _receiveDataBuffer?.Clear();
                
                // 确保取消任何正在进行的处理
                try
                {
                    _processCts?.Cancel();
                    _processCts?.Dispose();
                    _processCts = null;
                }
                catch { }
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

                await Close(false); // intend to close the session

                return true;
            }

            return false;
        }

        private async Task HandleUserSideError(Exception ex)
        {
            _logger?.LogError(string.Format("Client [{0}] error occurred in user side [{1}].", this, ex.Message), ex);
            await Task.CompletedTask;
        }

        #endregion

        #region Send

        public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            await SendAsync(data, 0, data.Length, cancellationToken);
        }

        public async Task SendAsync(byte[] data, int offset, int count, CancellationToken cancellationToken = default)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            if (State != TcpSocketConnectionState.Connected)
            {
                throw new InvalidOperationException("This client has not connected to server.");
            }

            CancellationTokenSource timeoutCts = null;
            CancellationTokenSource linkedCts = null;
            
            try
            {
                timeoutCts = new CancellationTokenSource(_configuration.SendTimeout);
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                
                byte[] frameBuffer;
                int frameBufferOffset;
                int frameBufferLength;
                _configuration.FrameBuilder.Encoder.EncodeFrame(data, offset, count, out frameBuffer, out frameBufferOffset, out frameBufferLength);

                // 使用带超时的写入
                var sendTask = _stream.WriteAsync(frameBuffer, frameBufferOffset, frameBufferLength);
                
                // 添加超时
                if (await Task.WhenAny(sendTask, Task.Delay(_configuration.SendTimeout, linkedCts.Token)) != sendTask)
                {
                    if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Send operation timed out after {_configuration.SendTimeout.TotalMilliseconds}ms");
                    }
                    // 如果是外部取消，就继续让异常抛出
                }
                
                // 确保发送任务已完成
                await sendTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 外部请求取消，直接抛出
                throw;
            }
            catch (Exception ex)
            {
                await HandleSendOperationException(ex);
            }
            finally
            {
                timeoutCts?.Dispose();
                linkedCts?.Dispose();
            }
        }

        #endregion

        #region Data Reading

        /// <summary>
        /// 获取可读取的字节数
        /// </summary>
        public int BytesAvailable
        {
            get
            {
                return _receiveDataBuffer?.Count ?? 0;
            }
        }

        /// <summary>
        /// 读取接收到的数据，如果没有数据可用，则阻塞直到有数据到达或超时
        /// </summary>
        /// <param name="buffer">存储读取数据的缓冲区</param>
        /// <param name="offset">缓冲区中的偏移量</param>
        /// <param name="count">要读取的最大字节数</param>
        /// <param name="timeout">超时时间，如果为null则使用配置的接收超时</param>
        /// <returns>实际读取的字节数</returns>
        public int Receive(byte[] buffer, int offset, int count, TimeSpan? timeout = null)
        {
            BufferValidator.ValidateBuffer(buffer, offset, count, "buffer");
            
            if (State != TcpSocketConnectionState.Connected)
            {
                throw new InvalidOperationException("This client has not connected to server.");
            }
            
            // 使用默认超时或指定的超时
            TimeSpan actualTimeout = timeout ?? _configuration.ReceiveTimeout;
            
            // 如果没有数据可用，等待数据到达
            if (BytesAvailable == 0)
            {
                if (!_dataAvailableEvent.WaitOne(actualTimeout))
                {
                    return 0; // 超时，没有数据可用
                }
            }
            
            // 从循环缓冲区读取数据
            lock (_dataLock)
            {
                return _receiveDataBuffer.Read(buffer, offset, count);
            }
        }

        /// <summary>
        /// 异步读取接收到的数据
        /// </summary>
        /// <param name="buffer">存储读取数据的缓冲区</param>
        /// <param name="offset">缓冲区中的偏移量</param>
        /// <param name="count">要读取的最大字节数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>实际读取的字节数</returns>
        public async Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            BufferValidator.ValidateBuffer(buffer, offset, count, "buffer");
            
            if (State != TcpSocketConnectionState.Connected)
            {
                throw new InvalidOperationException("This client has not connected to server.");
            }
            
            // 如果已有数据可用，立即读取
            if (BytesAvailable > 0)
            {
                return Receive(buffer, offset, count);
            }
            
            // 创建任务完成源
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            CancellationTokenRegistration registration = default;
            
            try
            {
                // 注册取消令牌
                if (cancellationToken.CanBeCanceled)
                {
                    registration = cancellationToken.Register(() => tcs.TrySetCanceled());
                }
                
                // 等待数据可用或取消
                ThreadPool.RegisterWaitForSingleObject(
                    _dataAvailableEvent, 
                    (state, timedOut) => 
                    {
                        var source = state as TaskCompletionSource<bool>;
                        if (source != null)
                        {
                            source.TrySetResult(!timedOut);
                        }
                    }, 
                    tcs, 
                    _configuration.ReceiveTimeout, 
                    true);
                
                // 等待数据可用、取消或超时
                bool dataAvailable;
                try
                {
                    dataAvailable = await tcs.Task;
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // 重新抛出取消异常
                }
                
                if (!dataAvailable)
                {
                    return 0; // 超时，没有数据可用
                }
                
                // 有数据可用，读取数据
                return Receive(buffer, offset, count);
            }
            finally
            {
                registration.Dispose();
            }
        }

        /// <summary>
        /// 清空接收缓冲区
        /// </summary>
        /// <returns>释放的字节数</returns>
        public int ClearReceiveBuffer()
        {
            lock (_dataLock)
            {
                int available = _receiveDataBuffer.Count;
                _receiveDataBuffer.Clear();
                return available;
            }
        }

        /// <summary>
        /// 读取缓冲区中的所有数据并清空缓冲区
        /// </summary>
        /// <returns>包含缓冲区中所有数据的字节数组</returns>
        public byte[] ReceiveAllBytes()
        {
            lock (_dataLock)
            {
                return _receiveDataBuffer.ReadAll();
            }
        }
        
        /// <summary>
        /// 读取缓冲区中的所有数据但不清空缓冲区
        /// </summary>
        /// <returns>包含缓冲区中所有数据的字节数组</returns>
        public byte[] PeekAllBytes()
        {
            lock (_dataLock)
            {
                return _receiveDataBuffer.GetAllData();
            }
        }
        
        /// <summary>
        /// 异步读取缓冲区中的所有数据并清空缓冲区
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>包含缓冲区中所有数据的字节数组</returns>
        public async Task<byte[]> ReceiveAllBytesAsync(CancellationToken cancellationToken = default)
        {
            // 如果没有数据可读且未取消，则等待数据
            if (BytesAvailable == 0)
            {
                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                CancellationTokenRegistration registration = default;
                
                try
                {
                    // 注册取消令牌
                    if (cancellationToken.CanBeCanceled)
                    {
                        registration = cancellationToken.Register(() => tcs.TrySetCanceled());
                    }
                    
                    // 等待数据可用或取消
                    ThreadPool.RegisterWaitForSingleObject(
                        _dataAvailableEvent, 
                        (state, timedOut) => 
                        {
                            var source = state as TaskCompletionSource<bool>;
                            if (source != null)
                            {
                                source.TrySetResult(!timedOut);
                            }
                        }, 
                        tcs, 
                        _configuration.ReceiveTimeout, 
                        true);
                    
                    // 等待数据可用、取消或超时
                    try
                    {
                        bool dataAvailable = await tcs.Task;
                        if (!dataAvailable)
                        {
                            return new byte[0]; // 超时，没有数据可用
                        }
                    }
                    catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw; // 重新抛出取消异常
                    }
                }
                finally
                {
                    registration.Dispose();
                }
            }
            
            // 同步读取所有数据
            return ReceiveAllBytes();
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
                
                HeartbeatManager.LogHeartbeat(_logger, "启动客户端心跳，间隔: {0}秒", _configuration.HeartbeatInterval.TotalSeconds);
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
                    HeartbeatManager.LogHeartbeat(_logger, "停止客户端心跳");
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
                _logger?.LogError(ex, "心跳处理中发生错误");
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
                
                HeartbeatManager.LogHeartbeat(_logger, "客户端发送心跳包到服务器: {0}", this.RemoteEndPoint);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "发送心跳包时发生错误");
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
                    
                    HeartbeatManager.LogHeartbeat(_logger, "客户端收到服务器心跳包: {0}, 网络延迟: {1}ms", 
                        this.RemoteEndPoint, delay);
                }
                else
                {
                    HeartbeatManager.LogHeartbeat(_logger, "客户端收到服务器心跳包: {0}", this.RemoteEndPoint);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "处理心跳包时发生错误");
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
                    HeartbeatManager.LogHeartbeat(_logger, "客户端心跳超时，已连续 {0}/{1} 次未收到服务器心跳", 
                        _missedHeartbeats, _configuration.MaxMissedHeartbeats);
                    
                    // 如果超过最大允许的心跳丢失次数，关闭连接
                    if (_missedHeartbeats >= _configuration.MaxMissedHeartbeats)
                    {
                        HeartbeatManager.LogHeartbeat(_logger, "客户端心跳连续 {0} 次超时，关闭连接: {1}", 
                            _missedHeartbeats, this.RemoteEndPoint);
                            
                        // 在后台线程中关闭连接，避免定时器回调中直接同步关闭
                        Task.Run(async () => await Close(true));
                    }
                }
            }
        }
        
        #endregion
    }
}

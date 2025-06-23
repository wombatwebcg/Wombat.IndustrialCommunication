using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Network;
using Microsoft.Extensions.Logging;

namespace Wombat.Network.Sockets
{
    public class UdpSocketServer
    {
        #region Fields

        private ILogger _logger;
        private UdpClient _udpServer;
        private readonly ConcurrentDictionary<string, UdpSocketSession> _sessions = new ConcurrentDictionary<string, UdpSocketSession>();
        private readonly IUdpSocketServerEventDispatcher _dispatcher;
        private readonly UdpSocketServerConfiguration _configuration;
        private readonly IPEndPoint _listenedEndPoint;
        private ArraySegment<byte> _receiveBuffer = default(ArraySegment<byte>);

        private int _state;
        private const int _none = 0;
        private const int _listening = 1;
        private const int _disposed = 5;
        
        // 异步处理相关字段
        private CancellationTokenSource _processCts;
        private Task _processTask;
        
        // 清理相关字段
        private Timer _cleanupTimer;

        #endregion

        #region Constructors

        public UdpSocketServer(int listenedPort, IUdpSocketServerEventDispatcher dispatcher, UdpSocketServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, dispatcher, configuration)
        {
        }

        public UdpSocketServer(IPAddress listenedAddress, int listenedPort, IUdpSocketServerEventDispatcher dispatcher, UdpSocketServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), dispatcher, configuration)
        {
        }

        public UdpSocketServer(IPEndPoint listenedEndPoint, IUdpSocketServerEventDispatcher dispatcher, UdpSocketServerConfiguration configuration = null)
        {
            if (listenedEndPoint == null)
                throw new ArgumentNullException("listenedEndPoint");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            _listenedEndPoint = listenedEndPoint;
            _dispatcher = dispatcher;
            _configuration = configuration ?? new UdpSocketServerConfiguration();

            if (_configuration.BufferManager == null)
                throw new InvalidProgramException("The buffer manager in configuration cannot be null.");
            if (_configuration.FrameBuilder == null)
                throw new InvalidProgramException("The frame handler in configuration cannot be null.");
        }

        // 使用委托的构造函数重载
        public UdpSocketServer(int listenedPort,
            Func<UdpSocketSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<UdpSocketSession, Task> onSessionStarted = null,
            Func<UdpSocketSession, Task> onSessionClosed = null,
            UdpSocketServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, onSessionDataReceived, onSessionStarted, onSessionClosed, configuration)
        {
        }

        public UdpSocketServer(IPAddress listenedAddress, int listenedPort,
            Func<UdpSocketSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<UdpSocketSession, Task> onSessionStarted = null,
            Func<UdpSocketSession, Task> onSessionClosed = null,
            UdpSocketServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), onSessionDataReceived, onSessionStarted, onSessionClosed, configuration)
        {
        }

        public UdpSocketServer(IPEndPoint listenedEndPoint,
            Func<UdpSocketSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<UdpSocketSession, Task> onSessionStarted = null,
            Func<UdpSocketSession, Task> onSessionClosed = null,
            UdpSocketServerConfiguration configuration = null)
            : this(listenedEndPoint,
                  new DefaultUdpSocketServerEventDispatcher(onSessionDataReceived, onSessionStarted, onSessionClosed),
                  configuration)
        {
        }

        #endregion

        #region Properties

        public UdpSocketServerConfiguration UdpSocketServerConfiguration { get { return _configuration; } }

        /// <summary>
        /// 监听的终结点
        /// </summary>
        public IPEndPoint ListenedEndPoint { get { return _listenedEndPoint; } }

        /// <summary>
        /// 本地终结点
        /// </summary>
        public IPEndPoint LocalEndPoint 
        { 
            get 
            { 
                if (_udpServer != null && _udpServer.Client != null)
                {
                    try
                    {
                        return (IPEndPoint)_udpServer.Client.LocalEndPoint;
                    }
                    catch
                    {
                        return _listenedEndPoint;
                    }
                }
                return _listenedEndPoint; 
            } 
        }

        /// <summary>
        /// 指示服务器是否正在监听
        /// </summary>
        public bool IsListening { get { return _state == _listening; } }

        /// <summary>
        /// 当前连接的会话数量
        /// </summary>
        public int SessionCount { get { return _sessions.Count; } }

        /// <summary>
        /// UDP服务器状态
        /// </summary>
        public UdpSocketConnectionState State
        {
            get
            {
                switch (_state)
                {
                    case _none:
                        return UdpSocketConnectionState.None;
                    case _listening:
                        return UdpSocketConnectionState.Active;
                    case _disposed:
                        return UdpSocketConnectionState.Closed;
                    default:
                        return UdpSocketConnectionState.Closed;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("ListenedEndPoint[{0}], LocalEndPoint[{1}], SessionCount[{2}]",
                this.ListenedEndPoint, this.LocalEndPoint, this.SessionCount);
        }

        #endregion

        #region Listen

        public async Task Listen(CancellationToken cancellationToken = default)
        {
            int origin = Interlocked.Exchange(ref _state, _listening);
            if (origin == _disposed)
            {
                throw new ObjectDisposedException("This UDP server has been disposed when starting to listen.");
            }
            else if (origin != _none)
            {
                throw new InvalidOperationException("This UDP server has already started.");
            }

            Clean(); // force to clean

            try
            {
                // 创建UDP服务器
                _udpServer = new UdpClient(_listenedEndPoint);
                SetSocketOptions();

                if (_receiveBuffer == default(ArraySegment<byte>))
                    _receiveBuffer = _configuration.BufferManager.BorrowBuffer();

                _logger?.LogDebug("UDP server started listening on [{0}] with dispatcher [{1}] on [{2}].",
                    this.ListenedEndPoint,
                    _dispatcher.GetType().Name,
                    DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));

                // 启动处理任务
                _processCts = new CancellationTokenSource();
                _processTask = ProcessAsync(_processCts.Token);
                
                // 启动清理定时器
                StartCleanupTimer();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await Close();
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.Message, ex);
                await Close();
                throw;
            }
        }

        private async Task ProcessAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (State == UdpSocketConnectionState.Active && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        UdpReceiveResult result;
                        
                        // 使用带超时的接收
                        using (var timeoutCts = new CancellationTokenSource(_configuration.ReceiveTimeout))
                        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
                        {
                            var receiveTask = _udpServer.ReceiveAsync();
                            
                            // 添加超时处理
                            if (await Task.WhenAny(receiveTask, Task.Delay(_configuration.ReceiveTimeout, linkedCts.Token)) != receiveTask)
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    // 接收超时，继续等待
                                    continue;
                                }
                                else
                                {
                                    // 外部请求取消
                                    break;
                                }
                            }
                            
                            result = await receiveTask;
                        }

                        // 处理接收到的数据
                        await ProcessReceivedData(result.Buffer, 0, result.Buffer.Length, result.RemoteEndPoint, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // 正常取消，退出循环
                        break;
                    }
                    catch (Exception ex)
                    {
                        await HandleReceiveOperationException(ex);
                        break;
                    }
                }
            }
            finally
            {
                await Close();
            }
        }

        private async Task ProcessReceivedData(byte[] data, int offset, int count, IPEndPoint clientEndPoint, CancellationToken cancellationToken)
        {
            try
            {
                // 获取或创建会话
                string sessionKey = clientEndPoint.ToString();
                UdpSocketSession session = null;
                bool isNewSession = false;
                
                if (!_sessions.TryGetValue(sessionKey, out session))
                {
                    // 检查是否达到最大会话数量限制
                    if (_sessions.Count >= _configuration.MaxClients)
                    {
                        _logger?.LogWarning("已达到最大会话数量限制 {0}，拒绝来自 {1} 的连接", 
                            _configuration.MaxClients, clientEndPoint);
                        return;
                    }
                    
                    session = new UdpSocketSession(clientEndPoint, _configuration, _dispatcher, this);
                    if (_logger != null)
                        session.UseLogger(_logger);
                    _sessions.TryAdd(sessionKey, session);
                    isNewSession = true;
                }
                
                // 更新会话活跃时间
                session.UpdateLastActiveTime();

                // 尝试解码帧
                byte[] payload;
                int payloadOffset;
                int payloadCount;
                int frameLength;

                if (_configuration.FrameBuilder.Decoder.TryDecodeFrame(
                    data, offset, count,
                    out frameLength, out payload, out payloadOffset, out payloadCount))
                {
                    // 如果是新会话，先启动会话（无论是心跳包还是普通数据包）
                    if (isNewSession)
                    {
                        try
                        {
                            await session.Start();
                        }
                        catch (Exception ex)
                        {
                            await HandleUserSideError(ex);
                            _sessions.TryRemove(sessionKey, out _);
                            return;
                        }
                    }
                    
                    // 检查是否为心跳包
                    if (HeartbeatManager.IsHeartbeatPacket(payload, payloadOffset, payloadCount))
                    {
                        session.ProcessHeartbeatPacket(payload, payloadOffset);
                    }
                    else
                    {
                        // 处理普通数据 - 确保在会话启动完成后处理
                        await session.ProcessDataReceived(payload, payloadOffset, payloadCount);
                    }
                }
                else
                {
                    // 无法解码的数据，记录警告
                    _logger?.LogWarning("无法解码来自 {0} 的UDP数据包", clientEndPoint);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                await HandleUserSideError(ex);
            }
        }

        private void SetSocketOptions()
        {
            if (_udpServer?.Client != null)
            {
                _udpServer.Client.ReceiveBufferSize = _configuration.ReceiveBufferSize;
                _udpServer.Client.SendBufferSize = _configuration.SendBufferSize;
                _udpServer.Client.ReceiveTimeout = (int)_configuration.ReceiveTimeout.TotalMilliseconds;
                _udpServer.Client.SendTimeout = (int)_configuration.SendTimeout.TotalMilliseconds;
                
                _udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _configuration.ReuseAddress);
                
                if (_configuration.DontFragment)
                {
                    _udpServer.DontFragment = true;
                }
                
                if (_configuration.Broadcast)
                {
                    _udpServer.EnableBroadcast = true;
                }
            }
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
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            // 停止清理定时器
            StopCleanupTimer();

            // 通知所有会话关闭
            foreach (var session in _sessions.Values)
            {
                try
                {
                    await session.Close();
                }
                catch (Exception ex)
                {
                    await HandleUserSideError(ex);
                }
            }

            _logger?.LogDebug("UDP server stopped listening on [{0}] with dispatcher [{1}] on [{2}].",
                this.ListenedEndPoint,
                _dispatcher.GetType().Name,
                DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));

            Clean();
        }

        private void Clean()
        {
            try
            {
                try
                {
                    _processCts?.Cancel();
                    _processCts?.Dispose();
                    _processCts = null;
                }
                catch { }
                
                try
                {
                    if (_udpServer != null)
                    {
                        _udpServer.Close();
                        _udpServer.Dispose();
                    }
                }
                catch { }
            }
            catch { }
            finally
            {
                _udpServer = null;
                _sessions.Clear();
            }

            if (_receiveBuffer != default(ArraySegment<byte>))
                _configuration.BufferManager.ReturnBuffer(_receiveBuffer);
            _receiveBuffer = default(ArraySegment<byte>);
        }

        #endregion

        #region Exception Handler

        private async Task HandleReceiveOperationException(Exception ex)
        {
            await CloseIfShould(ex);
            throw new UdpSocketException(ex.Message, ex);
        }

        private async Task HandleSendOperationException(Exception ex)
        {
            await CloseIfShould(ex);
            throw new UdpSocketException(ex.Message, ex);
        }

        private async Task<bool> CloseIfShould(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is NullReferenceException
                || ex is ArgumentException)
            {
                _logger?.LogError(ex.Message, ex);
                await Close();
                return true;
            }

            return false;
        }

        private async Task HandleUserSideError(Exception ex)
        {
            _logger?.LogError(string.Format("Server [{0}] error occurred in user side [{1}].", this, ex.Message), ex);
            await Task.CompletedTask;
        }

        #endregion

        #region Send

        public async Task SendToAsync(IPEndPoint clientEndPoint, byte[] data, CancellationToken cancellationToken = default)
        {
            await SendToAsync(clientEndPoint, data, 0, data.Length, cancellationToken);
        }

        public async Task SendToAsync(IPEndPoint clientEndPoint, byte[] data, int offset, int count, CancellationToken cancellationToken = default)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            if (State != UdpSocketConnectionState.Active)
            {
                throw new InvalidOperationException("This UDP server is not active.");
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

                // 准备发送的数据
                byte[] sendData = new byte[frameBufferLength];
                Buffer.BlockCopy(frameBuffer, frameBufferOffset, sendData, 0, frameBufferLength);

                // 使用带超时的发送
                var sendTask = _udpServer.SendAsync(sendData, sendData.Length, clientEndPoint);
                
                if (await Task.WhenAny(sendTask, Task.Delay(_configuration.SendTimeout, linkedCts.Token)) != sendTask)
                {
                    if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Send operation timed out after {_configuration.SendTimeout.TotalMilliseconds}ms");
                    }
                }
                
                await sendTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
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

        public async Task SendToAsync(string sessionKey, byte[] data, CancellationToken cancellationToken = default)
        {
            await SendToAsync(sessionKey, data, 0, data.Length, cancellationToken);
        }

        public async Task SendToAsync(string sessionKey, byte[] data, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (_sessions.TryGetValue(sessionKey, out UdpSocketSession session))
            {
                await session.SendAsync(data, offset, count, cancellationToken);
            }
            else
            {
                _logger?.LogWarning("Cannot find session [{0}].", sessionKey);
            }
        }

        public async Task SendToAsync(UdpSocketSession session, byte[] data, CancellationToken cancellationToken = default)
        {
            await SendToAsync(session, data, 0, data.Length, cancellationToken);
        }

        public async Task SendToAsync(UdpSocketSession session, byte[] data, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (_sessions.ContainsKey(session.ClientEndPoint.ToString()))
            {
                await session.SendAsync(data, offset, count, cancellationToken);
            }
            else
            {
                _logger?.LogWarning("Cannot find session [{0}].", session);
            }
        }

        public async Task BroadcastAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            await BroadcastAsync(data, 0, data.Length, cancellationToken);
        }

        public async Task BroadcastAsync(byte[] data, int offset, int count, CancellationToken cancellationToken = default)
        {
            var sessions = _sessions.Values;
            var tasks = new Task[sessions.Count];
            int index = 0;
            
            foreach (var session in sessions)
            {
                tasks[index++] = session.SendAsync(data, offset, count, cancellationToken);
            }
            
            if (tasks.Length > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        #endregion

        #region Session Management

        public bool HasSession(IPEndPoint clientEndPoint)
        {
            return _sessions.ContainsKey(clientEndPoint.ToString());
        }

        public bool HasSession(string sessionKey)
        {
            return _sessions.ContainsKey(sessionKey);
        }

        public UdpSocketSession GetSession(string sessionKey)
        {
            _sessions.TryGetValue(sessionKey, out UdpSocketSession session);
            return session;
        }

        public UdpSocketSession GetSession(IPEndPoint clientEndPoint)
        {
            return GetSession(clientEndPoint.ToString());
        }

        public async Task CloseSession(string sessionKey)
        {
            if (_sessions.TryRemove(sessionKey, out UdpSocketSession session))
            {
                await session.Close();
            }
        }

        public async Task CloseSession(IPEndPoint clientEndPoint)
        {
            await CloseSession(clientEndPoint.ToString());
        }

        public UdpSocketSession[] GetConnectedSessions()
        {
            var sessions = new UdpSocketSession[_sessions.Count];
            int index = 0;
            foreach (var session in _sessions.Values)
            {
                sessions[index++] = session;
            }
            return sessions;
        }

        #endregion

        #region Session Cleanup

        private void StartCleanupTimer()
        {
            _cleanupTimer = new Timer(
                CleanupTimerCallback, 
                null, 
                _configuration.CleanupInterval, 
                _configuration.CleanupInterval);
        }

        private void StopCleanupTimer()
        {
            if (_cleanupTimer != null)
            {
                _cleanupTimer.Dispose();
                _cleanupTimer = null;
            }
        }

        private async void CleanupTimerCallback(object state)
        {
            if (_state != _listening)
            {
                StopCleanupTimer();
                return;
            }
            
            try
            {
                var sessionsToRemove = new List<UdpSocketSession>();
                
                foreach (var session in _sessions.Values)
                {
                    // 检查会话超时
                    if (session.IsTimeout(_configuration.ClientTimeout))
                    {
                        sessionsToRemove.Add(session);
                    }
                    // 检查心跳超时（如果启用了心跳）
                    else if (session.IsHeartbeatTimeout())
                    {
                        sessionsToRemove.Add(session);
                    }
                }
                
                // 移除超时的会话
                foreach (var session in sessionsToRemove)
                {
                    if (_sessions.TryRemove(session.ClientEndPoint.ToString(), out _))
                    {
                        try
                        {
                            await session.Close();
                        }
                        catch (Exception ex)
                        {
                            await HandleUserSideError(ex);
                        }
                    }
                }
                
                if (sessionsToRemove.Count > 0)
                {
                    _logger?.LogDebug("UDP服务器清理了 {0} 个非活跃会话", sessionsToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UDP服务器会话清理过程中发生错误");
            }
        }

        #endregion
    }
} 
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Network;
using Microsoft.Extensions.Logging;

namespace Wombat.Network.Sockets
{
    public sealed class UdpSocketSession
    {
        #region Fields

        private ILogger _logger;
        private readonly UdpSocketServerConfiguration _configuration;
        private readonly IUdpSocketServerEventDispatcher _dispatcher;
        private readonly UdpSocketServer _server;
        private readonly string _sessionKey;
        private readonly IPEndPoint _clientEndPoint;

        private int _state;
        private const int _none = 0;
        private const int _connected = 2;
        private const int _disposed = 5;
        
        // 会话相关字段
        private DateTime _startTime;
        private DateTime _lastActiveTime;
        private DateTime _lastHeartbeatTime;
        private int _missedHeartbeats;
        private readonly object _heartbeatLock = new object();
        private Timer _heartbeatTimer;

        #endregion

        #region Constructors

        public UdpSocketSession(
            IPEndPoint clientEndPoint,
            UdpSocketServerConfiguration configuration,
            IUdpSocketServerEventDispatcher dispatcher,
            UdpSocketServer server)
        {
            if (clientEndPoint == null)
                throw new ArgumentNullException("clientEndPoint");
            if (configuration == null)
                throw new ArgumentNullException("configuration");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");
            if (server == null)
                throw new ArgumentNullException("server");

            _clientEndPoint = clientEndPoint;
            _configuration = configuration;
            _dispatcher = dispatcher;
            _server = server;

            _sessionKey = Guid.NewGuid().ToString();
            _startTime = DateTime.UtcNow;
            _lastActiveTime = DateTime.UtcNow;
            _lastHeartbeatTime = DateTime.UtcNow;
            _missedHeartbeats = 0;
        }

        #endregion

        #region Properties

        public string SessionKey { get { return _sessionKey; } }
        public DateTime StartTime { get { return _startTime; } }
        public DateTime LastActiveTime { get { return _lastActiveTime; } }
        public DateTime LastHeartbeatTime { get { return _lastHeartbeatTime; } }
        public int MissedHeartbeats { get { return _missedHeartbeats; } }

        public IPEndPoint ClientEndPoint { get { return _clientEndPoint; } }
        public UdpSocketServer Server { get { return _server; } }

        public UdpSocketConnectionState State
        {
            get
            {
                switch (_state)
                {
                    case _none:
                        return UdpSocketConnectionState.None;
                    case _connected:
                        return UdpSocketConnectionState.Active;
                    case _disposed:
                        return UdpSocketConnectionState.Closed;
                    default:
                        return UdpSocketConnectionState.Closed;
                }
            }
        }

        /// <summary>
        /// 获取会话持续时间
        /// </summary>
        public TimeSpan Duration
        {
            get { return DateTime.UtcNow - _startTime; }
        }

        /// <summary>
        /// 获取距离上次活动的时间
        /// </summary>
        public TimeSpan IdleTime
        {
            get { return DateTime.UtcNow - _lastActiveTime; }
        }

        public override string ToString()
        {
            return string.Format("SessionKey[{0}], ClientEndPoint[{1}], Duration[{2}]",
                this.SessionKey, this.ClientEndPoint, this.Duration);
        }

        #endregion

        #region Logger
        public void UseLogger(ILogger logger)
        {
            _logger = logger;
        }

        #endregion

        #region Session Management

        internal async Task Start()
        {
            int origin = Interlocked.CompareExchange(ref _state, _connected, _none);
            if (origin == _disposed)
            {
                throw new ObjectDisposedException("This UDP socket session has been disposed when starting.");
            }
            else if (origin != _none)
            {
                throw new InvalidOperationException("This UDP socket session is in invalid state when starting.");
            }

            _logger?.LogDebug("UDP session started for [{0}] on [{1}] with session key [{2}].",
                this.ClientEndPoint,
                this.StartTime.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                this.SessionKey);

            bool isErrorOccurredInUserSide = false;
            try
            {
                await _dispatcher.OnSessionStarted(this);
            }
            catch (Exception ex)
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
            }
            else
            {
                await Close(true);
            }
        }

        public async Task Close()
        {
            await Close(true);
        }

        private async Task Close(bool shallNotifyUserSide)
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            // 停止心跳检测
            StopHeartbeat();

            if (shallNotifyUserSide)
            {
                _logger?.LogDebug("UDP session closed for [{0}] on [{1}] with duration [{2}].",
                    this.ClientEndPoint,
                    DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                    this.Duration);
                try
                {
                    await _dispatcher.OnSessionClosed(this);
                }
                catch (Exception ex)
                {
                    await HandleUserSideError(ex);
                }
            }
        }

        #endregion

        #region Data Processing

        internal async Task ProcessDataReceived(byte[] data, int offset, int count)
        {
            var currentState = State;
            if (currentState != UdpSocketConnectionState.Active)
            {
                _logger?.LogWarning("UDP会话数据处理被跳过，会话状态: {0}, 会话ID: {1}, 客户端: {2}", 
                    currentState, SessionKey, ClientEndPoint);
                return;
            }

            UpdateLastActiveTime();

            try
            {
                await _dispatcher.OnSessionDataReceived(this, data, offset, count);
            }
            catch (Exception ex)
            {
                await HandleUserSideError(ex);
            }
        }

        internal void UpdateLastActiveTime()
        {
            _lastActiveTime = DateTime.UtcNow;
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

            if (State != UdpSocketConnectionState.Active)
            {
                throw new InvalidOperationException("This UDP session is not active.");
            }

            await _server.SendToAsync(_clientEndPoint, data, offset, count, cancellationToken);
        }

        #endregion

        #region Heartbeat Management

        private void StartHeartbeat()
        {
            if (!_configuration.EnableHeartbeat || _state != _connected)
                return;

            lock (_heartbeatLock)
            {
                StopHeartbeat();
                
                _lastHeartbeatTime = DateTime.UtcNow;
                _missedHeartbeats = 0;
                
                _heartbeatTimer = new Timer(
                    HeartbeatTimerCallback, 
                    null, 
                    _configuration.HeartbeatInterval, 
                    _configuration.HeartbeatInterval);
                
                HeartbeatManager.LogHeartbeat(_logger, "启动UDP会话心跳，会话ID: {0}, 间隔: {1}秒", 
                    SessionKey, _configuration.HeartbeatInterval.TotalSeconds);
            }
        }

        private void StopHeartbeat()
        {
            lock (_heartbeatLock)
            {
                if (_heartbeatTimer != null)
                {
                    _heartbeatTimer.Dispose();
                    _heartbeatTimer = null;
                    HeartbeatManager.LogHeartbeat(_logger, "停止UDP会话心跳，会话ID: {0}", SessionKey);
                }
            }
        }

        private async void HeartbeatTimerCallback(object state)
        {
            if (_state != _connected)
            {
                StopHeartbeat();
                return;
            }
            
            try
            {
                // 检查心跳超时
                CheckHeartbeatTimeout();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UDP会话心跳处理中发生错误，会话ID: {0}", SessionKey);
            }
        }

        internal void ProcessHeartbeatPacket(byte[] data, int offset)
        {
            try
            {
                // 更新最后收到心跳的时间
                lock (_heartbeatLock)
                {
                    _lastHeartbeatTime = DateTime.UtcNow;
                    _missedHeartbeats = 0;
                }
                
                // 提取心跳包时间戳，用于计算网络延迟
                long timestamp = HeartbeatManager.ExtractTimestamp(data, offset);
                if (timestamp > 0)
                {
                    long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long delay = currentTime - timestamp;
                    
                    HeartbeatManager.LogHeartbeat(_logger, "UDP会话收到客户端心跳包，会话ID: {0}, 客户端: {1}, 网络延迟: {2}ms", 
                        SessionKey, this.ClientEndPoint, delay);
                }
                else
                {
                    HeartbeatManager.LogHeartbeat(_logger, "UDP会话收到客户端心跳包，会话ID: {0}", SessionKey);
                }
                
                // 如果启用心跳，服务器可以选择回复心跳包
                if (_configuration.EnableHeartbeat)
                {
                    Task.Run(async () => await SendHeartbeatPacket());
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "处理UDP心跳包时发生错误，会话ID: {0}", SessionKey);
            }
        }

        private void CheckHeartbeatTimeout()
        {
            if (!_configuration.EnableHeartbeat || _state != _connected)
                return;
                
            lock (_heartbeatLock)
            {
                TimeSpan elapsed = DateTime.UtcNow - _lastHeartbeatTime;
                
                if (elapsed > _configuration.HeartbeatTimeout)
                {
                    _missedHeartbeats++;
                    HeartbeatManager.LogHeartbeat(_logger, "UDP会话心跳超时，已连续 {0}/{1} 次未收到客户端心跳，会话ID: {2}", 
                        _missedHeartbeats, _configuration.MaxMissedHeartbeats, SessionKey);
                    
                    if (_missedHeartbeats >= _configuration.MaxMissedHeartbeats)
                    {
                        HeartbeatManager.LogHeartbeat(_logger, "UDP会话心跳连续 {0} 次超时，关闭会话，会话ID: {1}", 
                            _missedHeartbeats, SessionKey);
                            
                        Task.Run(async () => await Close(true));
                    }
                }
            }
        }

        private async Task SendHeartbeatPacket()
        {
            try
            {
                if (_state != _connected)
                    return;
                    
                byte[] heartbeatPacket = HeartbeatManager.CreateHeartbeatPacket();
                await SendAsync(heartbeatPacket);
                
                HeartbeatManager.LogHeartbeat(_logger, "UDP会话发送心跳包到客户端: {0}", this.ClientEndPoint);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "发送UDP心跳包时发生错误，会话ID: {0}", SessionKey);
            }
        }

        #endregion

        #region Timeout Check

        internal bool IsTimeout(TimeSpan clientTimeout)
        {
            return IdleTime > clientTimeout;
        }

        internal bool IsHeartbeatTimeout()
        {
            if (!_configuration.EnableHeartbeat)
                return false;
                
            lock (_heartbeatLock)
            {
                return _missedHeartbeats >= _configuration.MaxMissedHeartbeats;
            }
        }

        #endregion

        #region Exception Handler

        private async Task HandleUserSideError(Exception ex)
        {
            _logger?.LogError(string.Format("UDP Session [{0}] error occurred in user side [{1}].", this, ex.Message), ex);
            await Task.CompletedTask;
        }

        #endregion
    }
} 
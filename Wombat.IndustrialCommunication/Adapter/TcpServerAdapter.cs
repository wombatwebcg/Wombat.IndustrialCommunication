using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.Network.Sockets;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// TCP服务器适配器，实现IStreamResource接口，用于服务器端通信
    /// </summary>
    public class TcpServerAdapter : IStreamResource, IDisposable
    {
        private TcpSocketServer _tcpSocketServer;
        private readonly object _lockObject = new object();
        private bool _disposed;
        private const int DEFAULT_TIMEOUT_MS = 3000;
        private const int MIN_PORT = 1;
        private const int MAX_PORT = 65535;
        private List<TcpSocketSession> _activeSessions = new List<TcpSocketSession>();
        private AsyncLock _sessionsLock = new AsyncLock();
        private IPEndPoint _localEndPoint;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口</param>
        public TcpServerAdapter(string ip, int port)
        {
            if (string.IsNullOrEmpty(ip))
                throw new ArgumentNullException(nameof(ip));
            
            if (port < MIN_PORT || port > MAX_PORT)
                throw new ArgumentOutOfRangeException(nameof(port), $"Port must be between {MIN_PORT} and {MAX_PORT}");

            IPAddress address;
            if (ip.Equals("0.0.0.0") || ip.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                address = IPAddress.Any;
            }
            else if (!IPAddress.TryParse(ip, out address))
            {
                try
                {
                    address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
                    if (address == null)
                        throw new ArgumentException($"Could not resolve host name: {ip}", nameof(ip));
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Invalid IP address or host name: {ip}", nameof(ip), ex);
                }
            }

            _localEndPoint = new IPEndPoint(address, port);
            var serverConfig = new TcpSocketServerConfiguration();
            var dispatcher = new TcpServerEventDispatcher(this);
            _tcpSocketServer = new TcpSocketServer(_localEndPoint, dispatcher, serverConfig);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ipEndPoint">IP终结点</param>
        public TcpServerAdapter(IPEndPoint ipEndPoint)
        {
            if (ipEndPoint == null)
                throw new ArgumentNullException(nameof(ipEndPoint));
            
            _localEndPoint = ipEndPoint;
            var serverConfig = new TcpSocketServerConfiguration();
            var dispatcher = new TcpServerEventDispatcher(this);
            _tcpSocketServer = new TcpSocketServer(_localEndPoint, dispatcher, serverConfig);
        }

        /// <summary>
        /// 版本
        /// </summary>
        public string Version => nameof(TcpServerAdapter);

        /// <summary>
        /// 日志记录器
        /// </summary>
        public ILogger Logger { get; private set; }

        /// <summary>
        /// 等待间隔
        /// </summary>
        public TimeSpan WaiteInterval { get; set; }

        /// <summary>
        /// 是否已连接（对于服务器，表示是否正在监听）
        /// </summary>
        public bool Connected => _tcpSocketServer?.IsListening ?? false;

        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout
        {
            get => TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
            set { /* 服务器端不使用连接超时 */ }
        }

        /// <summary>
        /// 接收超时时间
        /// </summary>
        public TimeSpan ReceiveTimeout
        {
            get => TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
            set
            {
                // 由于没有直接的属性访问方法，暂时不实现
            }
        }

        /// <summary>
        /// 发送超时时间
        /// </summary>
        public TimeSpan SendTimeout
        {
            get => TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
            set
            {
                // 由于没有直接的属性访问方法，暂时不实现
            }
        }

        /// <summary>
        /// 最大连接数
        /// </summary>
        public int MaxConnections
        {
            get => 100; // 默认值
            set
            {
                // 由于没有直接的属性访问方法，暂时不实现
            }
        }

        /// <summary>
        /// 接收到的数据处理事件
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <summary>
        /// 客户端连接事件
        /// </summary>
        public event EventHandler<SessionEventArgs> ClientConnected;

        /// <summary>
        /// 客户端断开连接事件
        /// </summary>
        public event EventHandler<SessionEventArgs> ClientDisconnected;

        /// <summary>
        /// 向所有客户端发送数据
        /// </summary>
        /// <param name="buffer">数据缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="size">大小</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpServerAdapter));

            if (!Connected)
                return OperationResult.CreateFailedResult(new InvalidOperationException("Server is not listening"));

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");

            OperationResult operation = new OperationResult();
            int successCount = 0;
            List<Exception> exceptions = new List<Exception>();

            try
            {
                using (await _sessionsLock.LockAsync())
                {
                    if (_activeSessions.Count == 0)
                    {
                        Logger?.LogWarning("没有活跃的客户端连接");
                        return OperationResult.CreateFailedResult("No active client connections");
                    }

                    foreach (var session in _activeSessions)
                    {
                        try
                        {
                            await session.SendAsync(buffer.Skip(offset).Take(size).ToArray());
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "向客户端 {EndPoint} 发送数据时发生错误", session.RemoteEndPoint);
                            exceptions.Add(ex);
                        }
                    }
                }

                if (successCount > 0)
                {
                    Logger?.LogDebug("成功向 {SuccessCount}/{TotalCount} 个客户端发送数据", 
                        successCount, _activeSessions.Count);
                    
                    // 如果至少有一个成功，我们认为操作成功
                    return operation.Complete();
                }
                else
                {
                    string errorMessage = $"向所有客户端发送数据失败，共 {exceptions.Count} 个错误";
                    Logger?.LogError(errorMessage);
                    return OperationResult.CreateFailedResult(errorMessage);
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"发送数据时发生错误: {ex.Message}";
                Logger?.LogError(ex, errorMessage);
                return OperationResult.CreateFailedResult(errorMessage);
            }
        }

        /// <summary>
        /// 接收数据（服务器适配器不实现此方法，数据接收通过事件处理）
        /// </summary>
        public Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            // 服务器模式下，接收数据是通过事件处理的，而不是通过这个方法
            return Task.FromResult(new OperationResult<int>
            {
                IsSuccess = false,
                Message = "Server mode does not support direct Receive method. Data is handled through events."
            });
        }

        /// <summary>
        /// 检测连接的健康状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接是否健康的操作结果</returns>
        public Task<OperationResult<bool>> IsConnectionHealthyAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return Task.FromResult(new OperationResult<bool>
                {
                    IsSuccess = false,
                    Message = "TcpServerAdapter is disposed",
                    ResultValue = false
                });

            // 对于服务器，健康状态主要是检查是否正在监听
            var result = new OperationResult<bool>
            {
                IsSuccess = true,
                ResultValue = Connected
            };

            return Task.FromResult(result);
        }

        /// <summary>
        /// 使用日志记录器
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public void UseLogger(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Logger.LogInformation("Logger configured for TcpServerAdapter");
        }

        /// <summary>
        /// 开始监听
        /// </summary>
        /// <returns>操作结果</returns>
        public OperationResult Listen()
        {
            return ListenAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        /// <returns>操作结果</returns>
        public OperationResult Shutdown()
        {
            return ShutdownAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步开始监听
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> ListenAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpServerAdapter));

            try
            {
                Logger?.LogInformation("开始在 {EndPoint} 上监听", _localEndPoint);
                OperationResult result = new OperationResult();
                _tcpSocketServer.Listen();
                result.IsSuccess = _tcpSocketServer.IsListening;
                
                if (result.IsSuccess)
                    Logger?.LogInformation("成功在 {EndPoint} 上开始监听", _localEndPoint);
                else
                    Logger?.LogWarning("在 {EndPoint} 上开始监听失败", _localEndPoint);
                
                return await Task.FromResult(result.Complete());
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "开始监听时发生错误");
                return OperationResult.CreateFailedResult(ex);
            }
        }

        /// <summary>
        /// 异步停止监听
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> ShutdownAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpServerAdapter));

            try
            {
                Logger?.LogInformation("停止在 {EndPoint} 上的监听", _localEndPoint);
                OperationResult result = new OperationResult();
                
                // 关闭所有会话
                using (await _sessionsLock.LockAsync())
                {
                    foreach (var session in _activeSessions.ToList())
                    {
                        try
                        {
                            await session.Close();
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "关闭客户端会话 {EndPoint} 时发生错误", session.RemoteEndPoint);
                        }
                    }
                    _activeSessions.Clear();
                }
                
                // 关闭服务器
                _tcpSocketServer.Shutdown();
                StreamClose();
                
                result.IsSuccess = !_tcpSocketServer.IsListening;
                
                if (result.IsSuccess)
                    Logger?.LogInformation("成功停止在 {EndPoint} 上的监听", _localEndPoint);
                else
                    Logger?.LogWarning("停止在 {EndPoint} 上的监听失败", _localEndPoint);
                
                return result.Complete();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "停止监听时发生错误");
                return OperationResult.CreateFailedResult(ex);
            }
        }

        /// <summary>
        /// 关闭流
        /// </summary>
        public void StreamClose()
        {
            if (_disposed)
                return;

            try
            {
                Logger?.LogDebug("关闭流");
                _tcpSocketServer.Shutdown();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "关闭流时发生错误");
            }
        }

        /// <summary>
        /// 添加会话
        /// </summary>
        /// <param name="session">会话</param>
        internal async Task AddSessionAsync(TcpSocketSession session)
        {
            using (await _sessionsLock.LockAsync())
            {
                _activeSessions.Add(session);
            }
            
            // 创建会话包装器
            var networkSession = new TcpSocketSessionWrapper(session);
            ClientConnected?.Invoke(this, new SessionEventArgs(networkSession));
        }

        /// <summary>
        /// 移除会话
        /// </summary>
        /// <param name="session">会话</param>
        internal async Task RemoveSessionAsync(TcpSocketSession session)
        {
            using (await _sessionsLock.LockAsync())
            {
                _activeSessions.Remove(session);
            }
            
            // 创建会话包装器
            var networkSession = new TcpSocketSessionWrapper(session);
            ClientDisconnected?.Invoke(this, new SessionEventArgs(networkSession));
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        /// <param name="session">会话</param>
        /// <param name="data">数据</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数量</param>
        internal void HandleReceivedData(TcpSocketSession session, byte[] data, int offset, int count)
        {
            // 创建会话包装器
            var networkSession = new TcpSocketSessionWrapper(session);
            DataReceived?.Invoke(this, new DataReceivedEventArgs(networkSession, data, offset, count));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Shutdown();
                        _activeSessions.Clear();
                        _tcpSocketServer = null;
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "释放资源时发生错误");
                    }
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// TCP服务器事件调度器
        /// </summary>
        private class TcpServerEventDispatcher : ITcpSocketServerEventDispatcher
        {
            private readonly TcpServerAdapter _adapter;

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="adapter">适配器</param>
            public TcpServerEventDispatcher(TcpServerAdapter adapter)
            {
                _adapter = adapter;
            }

            /// <summary>
            /// 会话开始事件
            /// </summary>
            /// <param name="session">会话</param>
            public async Task OnSessionStarted(TcpSocketSession session)
            {
                _adapter.Logger?.LogInformation("客户端 {EndPoint} 已连接", session.RemoteEndPoint);
                await _adapter.AddSessionAsync(session);
            }

            /// <summary>
            /// 会话数据接收事件
            /// </summary>
            /// <param name="session">会话</param>
            /// <param name="data">数据</param>
            /// <param name="offset">偏移量</param>
            /// <param name="count">数量</param>
            public async Task OnSessionDataReceived(TcpSocketSession session, byte[] data, int offset, int count)
            {
                _adapter.Logger?.LogDebug("从客户端 {EndPoint} 接收到 {Count} 字节数据", session.RemoteEndPoint, count);
                _adapter.HandleReceivedData(session, data, offset, count);
                await Task.CompletedTask;
            }

            /// <summary>
            /// 会话关闭事件
            /// </summary>
            /// <param name="session">会话</param>
            public async Task OnSessionClosed(TcpSocketSession session)
            {
                _adapter.Logger?.LogInformation("客户端 {EndPoint} 已断开连接", session.RemoteEndPoint);
                await _adapter.RemoveSessionAsync(session);
            }
        }

        // TCP会话包装器，实现INetworkSession接口
        private class TcpSocketSessionWrapper : INetworkSession
        {
            private readonly TcpSocketSession _session;

            public TcpSocketSessionWrapper(TcpSocketSession session)
            {
                _session = session;
                Id = Guid.NewGuid(); // 生成新的GUID，因为TcpSocketSession.Id是字符串
            }

            public Guid Id { get; }

            public void Close()
            {
                _session.Close();
            }

            public async Task<OperationResult> SendAsync(byte[] data)
            {
                try
                {
                    await _session.SendAsync(data);
                    return OperationResult.CreateSuccessResult();
                }
                catch (Exception ex)
                {
                    return OperationResult.CreateFailedResult(ex.Message);
                }
            }
        }
    }
} 
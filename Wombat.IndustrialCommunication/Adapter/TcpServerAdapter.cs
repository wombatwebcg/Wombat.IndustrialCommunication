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
using Wombat.Network;
using Wombat.Network.Sockets;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// TCP服务器适配器，实现IStreamResource接口，用于服务器端通信
    /// </summary>
    public class TcpServerAdapter : IStreamResource, IDisposable
    {
        private Socket _listenerSocket;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly object _lockObject = new object();
        private bool _disposed;
        private const int DEFAULT_TIMEOUT_MS = 3000;
        private const int MIN_PORT = 1;
        private const int MAX_PORT = 65535;
        private List<ClientSession> _activeSessions = new List<ClientSession>();
        private AsyncLock _sessionsLock = new AsyncLock();
        private IPEndPoint _localEndPoint;
        
        // 配置属性
        private int _receiveBufferSize = 8192;
        private int _sendBufferSize = 8192;
        private TimeSpan _receiveTimeout = TimeSpan.Zero;
        private TimeSpan _sendTimeout = TimeSpan.FromSeconds(30);
        private bool _noDelay = true;
        private bool _keepAlive = true;
        private TimeSpan _keepAliveInterval = TimeSpan.FromSeconds(30);
        private int _backlog = 100;

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
            InitializeSocket();
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
            InitializeSocket();
        }

        /// <summary>
        /// 初始化Socket
        /// </summary>
        private void InitializeSocket()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _listenerSocket = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            
            // 设置Socket选项
            _listenerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listenerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, _keepAlive);
            _listenerSocket.NoDelay = _noDelay;
            
            if (_keepAlive && _keepAliveInterval.TotalMilliseconds > 0)
            {
                _listenerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 
                    (int)_keepAliveInterval.TotalMilliseconds);
            }
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
        public bool Connected => _listenerSocket?.IsBound ?? false;

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
            get => _receiveTimeout;
            set
            {
                _receiveTimeout = value;
                _listenerSocket.ReceiveTimeout = (int)value.TotalMilliseconds;
            }
        }

        /// <summary>
        /// 发送超时时间
        /// </summary>
        public TimeSpan SendTimeout
        {
            get => _sendTimeout;
            set
            {
                _sendTimeout = value;
                _listenerSocket.SendTimeout = (int)value.TotalMilliseconds;
            }
        }

        /// <summary>
        /// 最大连接数
        /// </summary>
        public int MaxConnections
        {
            get => _backlog;
            set
            {
                _backlog = value;
                _listenerSocket.Listen(_backlog);
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
                ResultValue = _listenerSocket?.IsBound ?? false
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
                
                _listenerSocket.Bind(_localEndPoint);
                _listenerSocket.Listen(_backlog);
                
                result.IsSuccess = _listenerSocket.IsBound;
                
                if (result.IsSuccess)
                {
                    Logger?.LogInformation("成功在 {EndPoint} 上开始监听", _localEndPoint);
                    
                    // 开始接受连接循环
                    _ = Task.Run(async () => await AcceptClientsAsync(), _cancellationTokenSource.Token);
                }
                else
                {
                    Logger?.LogWarning("在 {EndPoint} 上开始监听失败", _localEndPoint);
                }
                
                return result.Complete();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "开始监听时发生错误");
                return OperationResult.CreateFailedResult(ex);
            }
        }

        /// <summary>
        /// 接受客户端连接循环
        /// </summary>
        private async Task AcceptClientsAsync()
        {
            try
            {
                while (!_disposed && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // 等待客户端连接
                        var clientSocket = await AcceptAsync(_listenerSocket);
                        
                        // 为每个客户端创建会话
                        var clientSession = new ClientSession(clientSocket, this);
                        
                        // 启动客户端会话处理
                        _ = Task.Run(async () => await clientSession.StartAsync(), _cancellationTokenSource.Token);
                    }
                    catch (ObjectDisposedException)
                    {
                        // 服务器已关闭
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "接受客户端连接时发生错误");
                        
                        // 短暂延迟后继续
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "客户端接受循环发生错误");
            }
        }

        /// <summary>
        /// 异步接受Socket连接
        /// </summary>
        private async Task<Socket> AcceptAsync(Socket listener)
        {
            return await Task.Factory.FromAsync(
                listener.BeginAccept,
                listener.EndAccept,
                null);
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
                
                // 取消接受新连接
                _cancellationTokenSource.Cancel();
                
                // 关闭所有会话
                using (await _sessionsLock.LockAsync())
                {
                    foreach (var session in _activeSessions.ToList())
                    {
                        try
                        {
                            session.Close();
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, "关闭客户端会话 {EndPoint} 时发生错误", session.RemoteEndPoint);
                        }
                    }
                    _activeSessions.Clear();
                }
                
                // 关闭服务器监听Socket
                try
                {
                    _listenerSocket?.Shutdown(SocketShutdown.Both);
                }
                catch { /* 忽略Shutdown异常 */ }
                
                _listenerSocket?.Close();
                
                result.IsSuccess = !(_listenerSocket?.IsBound ?? false);
                
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
                _cancellationTokenSource?.Cancel();
                
                try
                {
                    _listenerSocket?.Shutdown(SocketShutdown.Both);
                }
                catch { /* 忽略Shutdown异常 */ }
                
                _listenerSocket?.Close();
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
        private async Task AddSessionAsync(ClientSession session)
        {
            using (await _sessionsLock.LockAsync())
            {
                _activeSessions.Add(session);
            }
            
            // 创建会话包装器
            var networkSession = new ClientSessionWrapper(session);
            ClientConnected?.Invoke(this, new SessionEventArgs(networkSession));
        }

        /// <summary>
        /// 移除会话
        /// </summary>
        /// <param name="session">会话</param>
        private async Task RemoveSessionAsync(ClientSession session)
        {
            using (await _sessionsLock.LockAsync())
            {
                _activeSessions.Remove(session);
            }
            
            // 创建会话包装器
            var networkSession = new ClientSessionWrapper(session);
            ClientDisconnected?.Invoke(this, new SessionEventArgs(networkSession));
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        /// <param name="session">会话</param>
        /// <param name="data">数据</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数量</param>
        private void HandleReceivedData(ClientSession session, byte[] data, int offset, int count)
        {
            // 创建会话包装器
            var networkSession = new ClientSessionWrapper(session);
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
                        
                        // 清理资源
                        _activeSessions.Clear();
                        _cancellationTokenSource?.Dispose();
                        _listenerSocket?.Dispose();
                        _listenerSocket = null;
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "释放资源时发生错误");
                    }
                }

                _disposed = true;
            }
        }

        // TCP会话包装器，实现INetworkSession接口
        private class ClientSessionWrapper : INetworkSession
        {
            private readonly ClientSession _session;

            public ClientSessionWrapper(ClientSession session)
            {
                _session = session;
            }

            public Guid Id => _session.Id;

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

        /// <summary>
        /// 客户端会话类，管理单个客户端连接
        /// </summary>
        private class ClientSession : IDisposable
        {
            private readonly Socket _clientSocket;
            private readonly TcpServerAdapter _server;
            private readonly byte[] _receiveBuffer;
            private bool _disposed;
            private readonly CancellationTokenSource _cancellationTokenSource;

            public Guid Id { get; }
            public IPEndPoint RemoteEndPoint { get; }
            public IPEndPoint LocalEndPoint { get; }
            public DateTime StartTime { get; }
            public bool IsConnected => _clientSocket?.Connected ?? false;

            public ClientSession(Socket clientSocket, TcpServerAdapter server)
            {
                _clientSocket = clientSocket ?? throw new ArgumentNullException(nameof(clientSocket));
                _server = server ?? throw new ArgumentNullException(nameof(server));
                
                Id = Guid.NewGuid();
                RemoteEndPoint = (IPEndPoint)_clientSocket.RemoteEndPoint;
                LocalEndPoint = (IPEndPoint)_clientSocket.LocalEndPoint;
                StartTime = DateTime.UtcNow;
                
                _receiveBuffer = new byte[server._receiveBufferSize];
                _cancellationTokenSource = new CancellationTokenSource();
                
                // 设置客户端Socket选项
                _clientSocket.ReceiveBufferSize = server._receiveBufferSize;
                _clientSocket.SendBufferSize = server._sendBufferSize;
                _clientSocket.ReceiveTimeout = (int)server._receiveTimeout.TotalMilliseconds;
                _clientSocket.SendTimeout = (int)server._sendTimeout.TotalMilliseconds;
                _clientSocket.NoDelay = server._noDelay;
                
                if (server._keepAlive)
                {
                    _clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                }
            }

            /// <summary>
            /// 开始处理客户端连接
            /// </summary>
            public async Task StartAsync()
            {
                try
                {
                    _server.Logger?.LogInformation("客户端 {EndPoint} 已连接", RemoteEndPoint);
                    await _server.AddSessionAsync(this);
                    
                    // 开始接收数据循环
                    _ = Task.Run(async () => await ReceiveLoopAsync(), _cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    _server.Logger?.LogError(ex, "启动客户端会话时发生错误");
                    Close();
                }
            }

            /// <summary>
            /// 接收数据循环
            /// </summary>
            private async Task ReceiveLoopAsync()
            {
                try
                {
                    while (IsConnected && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        var receivedBytes = await _clientSocket.ReceiveAsync(
                            new ArraySegment<byte>(_receiveBuffer), 
                            SocketFlags.None);
                        
                        if (receivedBytes == 0)
                        {
                            // 客户端正常关闭连接
                            break;
                        }

                        _server.Logger?.LogDebug("从客户端 {EndPoint} 接收到 {Count} 字节数据", RemoteEndPoint, receivedBytes);
                        _server.HandleReceivedData(this, _receiveBuffer, 0, receivedBytes);
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset || 
                                                ex.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    // 客户端异常断开连接
                    _server.Logger?.LogDebug("客户端 {EndPoint} 异常断开连接: {Error}", RemoteEndPoint, ex.SocketErrorCode);
                }
                catch (ObjectDisposedException)
                {
                    // Socket已被释放
                }
                catch (Exception ex)
                {
                    _server.Logger?.LogError(ex, "接收客户端 {EndPoint} 数据时发生错误", RemoteEndPoint);
                }
                finally
                {
                    Close();
                }
            }

            /// <summary>
            /// 发送数据到客户端
            /// </summary>
            public async Task SendAsync(byte[] data)
            {
                if (_disposed || !IsConnected)
                    throw new InvalidOperationException("客户端连接已关闭");

                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                try
                {
                    await _clientSocket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);
                }
                catch (Exception ex)
                {
                    _server.Logger?.LogError(ex, "向客户端 {EndPoint} 发送数据时发生错误", RemoteEndPoint);
                    throw;
                }
            }

            /// <summary>
            /// 关闭客户端连接
            /// </summary>
            public void Close()
            {
                if (_disposed)
                    return;

                try
                {
                    _server.Logger?.LogInformation("客户端 {EndPoint} 已断开连接", RemoteEndPoint);
                    
                    // 异步移除会话，但不等待
                    Task.Run(async () => 
                    {
                        try 
                        {
                            await _server.RemoveSessionAsync(this);
                        }
                        catch (Exception ex)
                        {
                            _server.Logger?.LogError(ex, "移除客户端会话时发生错误");
                        }
                    });
                    
                    _cancellationTokenSource?.Cancel();
                    
                    try
                    {
                        _clientSocket?.Shutdown(SocketShutdown.Both);
                    }
                    catch { /* 忽略Shutdown异常 */ }
                    
                    _clientSocket?.Close();
                }
                catch (Exception ex)
                {
                    _server.Logger?.LogError(ex, "关闭客户端 {EndPoint} 连接时发生错误", RemoteEndPoint);
                }
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    Close();
                    _cancellationTokenSource?.Dispose();
                    _disposed = true;
                }
            }
        }
    }
} 
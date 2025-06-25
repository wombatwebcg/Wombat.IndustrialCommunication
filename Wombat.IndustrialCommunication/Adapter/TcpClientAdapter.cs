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

namespace Wombat.IndustrialCommunication
{
    public class TcpClientAdapter : IStreamResource, IDisposable
    {
        private Socket _socket;
        private IPEndPoint _remoteEndPoint;
        private TimeSpan _connectTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        private TimeSpan _receiveTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        private TimeSpan _sendTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        private readonly object _lockObject = new object();
        private bool _disposed;
        private const int DEFAULT_TIMEOUT_MS = 3000;
        private const int MIN_PORT = 1;
        private const int MAX_PORT = 65535;

        public TcpClientAdapter(string ip, int port)
        {
            if (string.IsNullOrEmpty(ip))
                throw new ArgumentNullException(nameof(ip));
            
            if (port < MIN_PORT || port > MAX_PORT)
                throw new ArgumentOutOfRangeException(nameof(port), $"Port must be between {MIN_PORT} and {MAX_PORT}");

            if (!IPAddress.TryParse(ip, out IPAddress address))
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

            _remoteEndPoint = new IPEndPoint(address, port);
        }

        public string Version => nameof(TcpClientAdapter);
        public ILogger Logger { get; private set; }
        public TimeSpan WaiteInterval { get; set; }
        public bool Connected => _socket?.Connected ?? false;

        public TimeSpan ConnectTimeout
        {
            get => _connectTimeout;
            set
            {
                _connectTimeout = value;
            }
        }

        public TimeSpan ReceiveTimeout
        {
            get => _receiveTimeout;
            set
            {
                _receiveTimeout = value;
            }
        }

        public TimeSpan SendTimeout
        {
            get => _sendTimeout;
            set
            {
                _sendTimeout = value;
            }
        }

        public async Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));

            if (!Connected)
                return OperationResult.CreateFailedResult(new InvalidOperationException("Not connected"));

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");

            OperationResult operation = new OperationResult();
            try
            {
                Logger?.LogDebug("正在发送 {Size} 字节数据到 {EndPoint}", size, _remoteEndPoint);
                
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(_sendTimeout);
                    
                    // 直接使用SendAsync，避免双重包装
                    var sendTask = _socket.SendAsync(new ArraySegment<byte>(buffer, offset, size), SocketFlags.None);
                    
                    // 使用Task.WhenAny进行超时控制
                    var completedTask = await Task.WhenAny(sendTask, Task.Delay(_sendTimeout, cts.Token));
                    
                    if (completedTask == sendTask)
                    {
                        await sendTask; // 确保任务完成
                    }
                    else
                    {
                        throw new OperationCanceledException("发送操作超时");
                    }
                }
                
                Logger?.LogDebug("成功发送 {Size} 字节数据到 {EndPoint}", size, _remoteEndPoint);
                return operation.Complete();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                string errorMessage = $"发送数据到 {_remoteEndPoint} 被取消";
                Logger?.LogWarning(errorMessage);
                return OperationResult.CreateFailedResult(errorMessage);
            }
            catch (OperationCanceledException)
            {
                string errorMessage = $"发送数据到 {_remoteEndPoint} 超时，超时设置: {SendTimeout.TotalMilliseconds}ms";
                Logger?.LogError(errorMessage);
                return OperationResult.CreateFailedResult(new TimeoutException(errorMessage));
            }
            catch (SocketException se)
            {
                string errorMessage = $"发送数据到 {_remoteEndPoint} 时发生Socket错误: {se.SocketErrorCode}, {se.Message}";
                Logger?.LogError(se, errorMessage);
                return OperationResult.CreateFailedResult(errorMessage);
            }
            catch (Exception ex)
            {
                string errorMessage = $"发送数据到 {_remoteEndPoint} 时发生错误: {ex.Message}";
                Logger?.LogError(ex, errorMessage);
                return OperationResult.CreateFailedResult(errorMessage);
            }
        }

        public async Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));

            if (!Connected)
                return new OperationResult<int> { IsSuccess = false, Message = "Not connected" };

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");

            OperationResult<int> operation = new OperationResult<int>();
            try
            {
                Logger?.LogDebug("正在从 {EndPoint} 接收最多 {Size} 字节数据", _remoteEndPoint, size);
                
                int count;
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(_receiveTimeout);
                    
                    // 直接使用ReceiveAsync，避免双重包装
                    var receiveTask = _socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, size), SocketFlags.None);
                    
                    // 使用Task.WaitAsync (对于.NET Framework，使用自定义超时处理)
                    var completedTask = await Task.WhenAny(receiveTask, Task.Delay(_receiveTimeout, cts.Token));
                    
                    if (completedTask == receiveTask)
                    {
                        count = await receiveTask;
                    }
                    else
                    {
                        throw new OperationCanceledException("接收操作超时");
                    }
                }
                
                operation.ResultValue = count;
                Logger?.LogDebug("成功从 {EndPoint} 接收 {Count} 字节数据", _remoteEndPoint, count);
                return operation.Complete();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                string errorMessage = $"从 {_remoteEndPoint} 接收数据被取消";
                Logger?.LogWarning(errorMessage);
                return new OperationResult<int> { IsSuccess = false, Message = errorMessage };
            }
            catch (OperationCanceledException)
            {
                string errorMessage = $"从 {_remoteEndPoint} 接收数据超时，超时设置: {ReceiveTimeout.TotalMilliseconds}ms";
                Logger?.LogError(errorMessage);
                return new OperationResult<int> { IsSuccess = false, Message = errorMessage, Exception = new TimeoutException(errorMessage) };
            }
            catch (SocketException se)
            {
                string errorMessage = $"从 {_remoteEndPoint} 接收数据时发生Socket错误: {se.SocketErrorCode}, {se.Message}";
                Logger?.LogError(se, errorMessage);
                return new OperationResult<int> { IsSuccess = false, Message = errorMessage, Exception = se };
            }
            catch (Exception ex)
            {
                string errorMessage = $"从 {_remoteEndPoint} 接收数据时发生错误: {ex.Message}";
                Logger?.LogError(ex, errorMessage);
                return new OperationResult<int> { IsSuccess = false, Message = errorMessage, Exception = ex };
            }
        }

        public void UseLogger(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Logger.LogInformation("Logger configured for TcpClientAdapter");
        }

        public OperationResult Connect()
        {
            return ConnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public OperationResult Disconnect()
        {
            return DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> ConnectAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));

            try
            {
                Logger?.LogInformation("Connecting to {EndPoint}", _remoteEndPoint);
                OperationResult connect = new OperationResult();
                
                // 创建新的Socket
                _socket = new Socket(_remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                
                // 设置Socket选项
                _socket.NoDelay = true;
                // 移除Socket级别的超时设置，统一使用CancellationToken控制超时
                // _socket.SendTimeout = (int)_sendTimeout.TotalMilliseconds;
                // _socket.ReceiveTimeout = (int)_receiveTimeout.TotalMilliseconds;
                
                // 使用超时进行连接
                using (var cts = new CancellationTokenSource(_connectTimeout))
                {
                    // 创建连接任务
                    var connectTask = Task.Factory.FromAsync(_socket.BeginConnect, _socket.EndConnect, _remoteEndPoint, null);
                    
                    // 等待连接完成或取消
                    await Task.Run(() => connectTask, cts.Token);
                }
                
                connect.IsSuccess = _socket.Connected;
                
                if (connect.IsSuccess)
                    Logger?.LogInformation("Successfully connected to {EndPoint}", _remoteEndPoint);
                else
                    Logger?.LogWarning("Failed to connect to {EndPoint}", _remoteEndPoint);
                
                return connect.Complete();
            }
            catch (OperationCanceledException)
            {
                string errorMessage = $"连接到 {_remoteEndPoint} 超时，超时设置: {_connectTimeout.TotalMilliseconds}ms";
                Logger?.LogError(errorMessage);
                return OperationResult.CreateFailedResult(new TimeoutException(errorMessage));
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error connecting to {EndPoint}", _remoteEndPoint);
                return OperationResult.CreateFailedResult(ex);
            }
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));

            try
            {
                Logger?.LogInformation("Disconnecting from {EndPoint}", _remoteEndPoint);
                OperationResult disConnect = new OperationResult();
                
                if (_socket != null)
                {
                    _socket.Close();
                    StreamClose();
                }
                
                disConnect.IsSuccess = !(_socket?.Connected ?? false);
                
                if (disConnect.IsSuccess)
                    Logger?.LogInformation("Successfully disconnected from {EndPoint}", _remoteEndPoint);
                else
                    Logger?.LogWarning("Failed to disconnect from {EndPoint}", _remoteEndPoint);
                
                return disConnect.Complete();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error disconnecting from {EndPoint}", _remoteEndPoint);
                return OperationResult.CreateFailedResult(ex);
            }
        }

        public void StreamClose()
        {
            if (_disposed)
                return;

            try
            {
                Logger?.LogDebug("Closing stream");
                if (_socket != null && _socket.Connected)
                {
                    _socket.Shutdown(SocketShutdown.Send);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error closing stream");
            }
        }

        /// <summary>
        /// 检测连接的健康状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接是否健康的操作结果</returns>
        public async Task<OperationResult<bool>> IsConnectionHealthyAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));

            if (!Connected)
                return new OperationResult<bool> { IsSuccess = true, ResultValue = false };

            try
            {
                // 创建一个心跳检测的取消令牌，使用较短的超时时间
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000)))
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
                {
                    // 创建一个0字节的心跳数据包
                    byte[] heartbeatPacket = new byte[1] { 0 };
                    
                    Logger?.LogDebug("正在执行连接健康检查: {EndPoint}", _remoteEndPoint);
                    
                    // 尝试发送和接收最小数据以检测连接状态
                    var sendResult = await Send(heartbeatPacket, 0, 1, linkedCts.Token);
                    if (!sendResult.IsSuccess)
                    {
                        Logger?.LogWarning("连接健康检查失败(发送): {Message}", sendResult.Message);
                        return new OperationResult<bool> { IsSuccess = true, ResultValue = false };
                    }
                    
                    Logger?.LogDebug("连接健康检查成功: {EndPoint}", _remoteEndPoint);
                    return new OperationResult<bool> { IsSuccess = true, ResultValue = true };
                }
            }
            catch (OperationCanceledException)
            {
                Logger?.LogWarning("连接健康检查超时: {EndPoint}", _remoteEndPoint);
                return new OperationResult<bool> { IsSuccess = true, ResultValue = false };
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "连接健康检查异常: {EndPoint}", _remoteEndPoint);
                return new OperationResult<bool> { IsSuccess = false, ResultValue = false, Message = ex.Message };
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
                return;

            if (disposing)
            {
                try
                {
                    Logger?.LogDebug("Disposing TcpClientAdapter");
                    if (_socket != null)
                    {
                        _socket.Close();
                        _socket.Dispose();
                        _socket = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Error during disposal");
                }
            }

            _disposed = true;
        }

        ~TcpClientAdapter()
        {
            Dispose(false);
        }
    }
}

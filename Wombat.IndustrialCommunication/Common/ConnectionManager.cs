using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 连接管理器，提供连接状态监控、健康检查和自动重连功能
    /// </summary>
    public class ConnectionManager
    {
        private readonly IDeviceClient _client;
        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);
        private DateTime _lastConnectionCheck = DateTime.MinValue;
        private int _reconnectAttempts = 0;
        
        // 状态追踪变量，用于防止递归调用
        private volatile bool _isCheckingConnection = false;
        private readonly object _stateLock = new object();
        
        /// <summary>
        /// 日志记录器
        /// </summary>
        public ILogger Logger => _client.Logger;
        
        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        public bool EnableAutoReconnect
        {
            get { return _client is IAutoReconnectClient reconnectClient && reconnectClient.EnableAutoReconnect; }
            set
            {
                if (_client is IAutoReconnectClient reconnectClient)
                {
                    reconnectClient.EnableAutoReconnect = value;
                }
            }
        }
        
        /// <summary>
        /// 最大重连尝试次数
        /// </summary>
        public int MaxReconnectAttempts
        {
            get { return _client is IAutoReconnectClient reconnectClient ? reconnectClient.MaxReconnectAttempts : 0; }
            set
            {
                if (_client is IAutoReconnectClient reconnectClient)
                {
                    reconnectClient.MaxReconnectAttempts = value;
                }
            }
        }
        
        /// <summary>
        /// 重连延迟
        /// </summary>
        public TimeSpan ReconnectDelay
        {
            get { return _client is IAutoReconnectClient reconnectClient ? reconnectClient.ReconnectDelay : TimeSpan.Zero; }
            set
            {
                if (_client is IAutoReconnectClient reconnectClient)
                {
                    reconnectClient.ReconnectDelay = value;
                }
            }
        }
        
        /// <summary>
        /// 连接检查间隔
        /// </summary>
        public TimeSpan ConnectionCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
        
        /// <summary>
        /// 短连接模式下的最大重连次数
        /// </summary>
        public int ShortConnectionReconnectAttempts { get; set; } = 1;
        
        /// <summary>
        /// 是否正在重连
        /// </summary>
        public bool IsReconnecting { get; private set; }
        
        /// <summary>
        /// 最后一次连接状态变更时间
        /// </summary>
        public DateTime LastConnectionStateChange { get; private set; } = DateTime.Now;
        
        /// <summary>
        /// 连接状态变更事件
        /// </summary>
        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="client">设备客户端</param>
        public ConnectionManager(IDeviceClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }
        
        /// <summary>
        /// 检查连接并在必要时进行重连
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> CheckAndReconnectAsync()
        {
            // 防止递归调用
            bool shouldProceed;
            lock (_stateLock)
            {
                shouldProceed = !_isCheckingConnection;
                if (shouldProceed)
                {
                    _isCheckingConnection = true;
                }
            }

            if (!shouldProceed)
            {
                Logger?.LogDebug("连接检查操作已在进行中，跳过重复调用");
                return OperationResult.CreateSuccessResult("连接检查操作已在进行中");
            }

            try
            {
                // 如果连接检查间隔未到，直接返回
                var now = DateTime.Now;
                if ((now - _lastConnectionCheck) < ConnectionCheckInterval)
                {
                    return OperationResult.CreateSuccessResult("连接检查间隔未到");
                }

                _lastConnectionCheck = now;

                // 如果已连接，直接返回
                if (_client.Connected)
                {
                    return OperationResult.CreateSuccessResult("连接正常");
                }

                // 如果启用了自动重连，尝试重连
                if (EnableAutoReconnect)
                {
                    return await ReconnectAsync();
                }

                return OperationResult.CreateFailedResult("未启用自动重连");
            }
            finally
            {
                // 清除标志位
                lock (_stateLock)
                {
                    _isCheckingConnection = false;
                }
            }
        }
        
        /// <summary>
        /// 执行重连操作
        /// </summary>
        /// <param name="isShortConnection">是否为短连接模式</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> ReconnectAsync(bool isShortConnection = false)
        {
            // 使用信号量确保一次只有一个重连操作
            if (!await _reconnectLock.WaitAsync(0))
            {
                Logger?.LogDebug("重连操作已在进行中，跳过重复调用");
                return OperationResult.CreateSuccessResult("重连操作已在进行中");
            }

            try
            {
                Logger?.LogInformation($"正在尝试重连到{_client.GetType().Name}");

                // 如果已连接，直接返回
                if (_client.Connected)
                {
                    return OperationResult.CreateSuccessResult("已经连接");
                }

                // 确定重连尝试次数
                int maxAttempts = MaxReconnectAttempts;
                if (maxAttempts <= 0)
                {
                    maxAttempts = 1; // 至少尝试一次
                }

                // 如果是短连接模式，使用短连接重连尝试次数
                if (isShortConnection && _client is IAutoReconnectClient reconnectClient)
                {
                    maxAttempts = reconnectClient.ShortConnectionReconnectAttempts;
                }

                // 重置重连计数器
                _reconnectAttempts = 0;

                // 尝试重连
                while (_reconnectAttempts < maxAttempts)
                {
                    _reconnectAttempts++;
                    Logger?.LogDebug($"重连尝试 {_reconnectAttempts}/{maxAttempts}");

                    // 执行连接操作
                    var result = await _client.ConnectAsync();
                    if (result.IsSuccess)
                    {
                        Logger?.LogInformation($"{_client.GetType().Name}重连成功，尝试次数: {_reconnectAttempts}");
                        return result;
                    }

                    // 如果达到最大尝试次数，返回失败
                    if (_reconnectAttempts >= maxAttempts)
                    {
                        Logger?.LogWarning($"{_client.GetType().Name}重连失败，达到最大尝试次数 {maxAttempts}");
                        return OperationResult.CreateFailedResult($"重连失败，已达到最大尝试次数: {maxAttempts}");
                    }

                    // 延迟后再次尝试
                    Logger?.LogDebug($"重连失败，等待 {ReconnectDelay.TotalMilliseconds}ms 后重试");
                    await Task.Delay(ReconnectDelay);
                }

                return OperationResult.CreateFailedResult("重连失败");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"{_client.GetType().Name}重连时发生异常");
                return OperationResult.CreateFailedResult($"重连异常: {ex.Message}");
            }
            finally
            {
                _reconnectLock.Release();
            }
        }
        
        /// <summary>
        /// 检查连接是否健康
        /// </summary>
        /// <returns>连接是否健康</returns>
        public async Task<bool> IsConnectionHealthyAsync()
        {
            if (!_client.Connected)
            {
                return false;
            }
            
            // 获取适配器或传输对象
            var streamResource = GetStreamResource();
            if (streamResource != null)
            {
                // 尝试调用 IsConnectionHealthyAsync 方法（如果存在）
                var isHealthyMethod = streamResource.GetType().GetMethod("IsConnectionHealthyAsync");
                if (isHealthyMethod != null)
                {
                    try
                    {
                        var result = await (Task<OperationResult<bool>>)isHealthyMethod.Invoke(streamResource, null);
                        return result.IsSuccess && result.ResultValue;
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogWarning(ex, "调用 IsConnectionHealthyAsync 方法时发生异常");
                        return false;
                    }
                }
            }
            
            // 默认情况下，如果客户端已连接，就认为连接是健康的
            return _client.Connected;
        }
        
        /// <summary>
        /// 获取连接类型名称
        /// </summary>
        /// <returns>连接类型名称</returns>
        private string GetConnectionTypeName()
        {
            if (_client.GetType().Name.IndexOf("TCP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                _client.GetType().Name.IndexOf("Tcp", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "TCP";
            }
            else if (_client.GetType().Name.IndexOf("RTU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     _client.GetType().Name.IndexOf("Serial", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "串口";
            }
            else
            {
                return _client.GetType().Name;
            }
        }
        
        /// <summary>
        /// 获取客户端的底层流资源
        /// </summary>
        /// <returns>流资源对象</returns>
        private object GetStreamResource()
        {
            try
            {
                // 尝试获取 Transport 属性
                var transportProperty = _client.GetType().GetProperty("Transport");
                if (transportProperty != null)
                {
                    var transport = transportProperty.GetValue(_client);
                    if (transport != null)
                    {
                        // 尝试获取 StreamResource 属性
                        var streamResourceProperty = transport.GetType().GetProperty("StreamResource");
                        if (streamResourceProperty != null)
                        {
                            return streamResourceProperty.GetValue(transport);
                        }
                    }
                }
                
                // 尝试直接获取 StreamResource 属性
                var directStreamResourceProperty = _client.GetType().GetProperty("StreamResource");
                if (directStreamResourceProperty != null)
                {
                    return directStreamResourceProperty.GetValue(_client);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "获取流资源时发生异常");
            }
            
            return null;
        }
        
        /// <summary>
        /// 触发连接状态变更事件
        /// </summary>
        /// <param name="isConnected">是否已连接</param>
        /// <param name="reason">状态变更原因</param>
        private void OnConnectionStateChanged(bool isConnected, string reason)
        {
            LastConnectionStateChange = DateTime.Now;
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(isConnected, reason));
        }
    }
    
    /// <summary>
    /// 连接状态变更事件参数
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; }
        
        /// <summary>
        /// 状态变更原因
        /// </summary>
        public string Reason { get; }
        
        /// <summary>
        /// 状态变更时间
        /// </summary>
        public DateTime Timestamp { get; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="isConnected">是否已连接</param>
        /// <param name="reason">状态变更原因</param>
        public ConnectionStateChangedEventArgs(bool isConnected, string reason)
        {
            IsConnected = isConnected;
            Reason = reason;
            Timestamp = DateTime.Now;
        }
    }
} 
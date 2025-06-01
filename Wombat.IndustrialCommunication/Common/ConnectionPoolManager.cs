using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 连接池配置
    /// </summary>
    public class ConnectionPoolConfig
    {
        /// <summary>
        /// 最大连接数
        /// </summary>
        public int MaxPoolSize { get; set; } = 10;
        
        /// <summary>
        /// 最小连接数（保持活跃的最小连接数）
        /// </summary>
        public int MinPoolSize { get; set; } = 1;
        
        /// <summary>
        /// 连接获取超时时间（毫秒）
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30000;
        
        /// <summary>
        /// 连接空闲超时时间（毫秒），超过此时间的空闲连接将被清理
        /// </summary>
        public int IdleTimeout { get; set; } = 60000;
        
        /// <summary>
        /// 连接最大使用次数，超过此次数后将被释放
        /// </summary>
        public int MaxUsageCount { get; set; } = 1000;
        
        /// <summary>
        /// 连接健康检查间隔（毫秒）
        /// </summary>
        public int HealthCheckInterval { get; set; } = 30000;
        
        /// <summary>
        /// 是否启用连接健康检查
        /// </summary>
        public bool EnableHealthCheck { get; set; } = true;
    }
    
    /// <summary>
    /// 连接池连接项
    /// </summary>
    /// <typeparam name="T">连接类型</typeparam>
    internal class PooledConnection<T> where T : IDeviceClient
    {
        /// <summary>
        /// 连接对象
        /// </summary>
        public T Connection { get; private set; }
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; private set; }
        
        /// <summary>
        /// 最后使用时间
        /// </summary>
        public DateTime LastUsedTime { get; set; }
        
        /// <summary>
        /// 使用次数
        /// </summary>
        public int UsageCount { get; set; }
        
        /// <summary>
        /// 是否正在使用
        /// </summary>
        public bool InUse { get; set; }
        
        /// <summary>
        /// 连接健康状态
        /// </summary>
        public bool IsHealthy { get; set; } = true;
        
        /// <summary>
        /// 最后健康检查时间
        /// </summary>
        public DateTime LastHealthCheckTime { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connection">连接对象</param>
        public PooledConnection(T connection)
        {
            Connection = connection;
            CreatedTime = DateTime.Now;
            LastUsedTime = DateTime.Now;
            LastHealthCheckTime = DateTime.Now;
        }
    }
    
    /// <summary>
    /// 连接池管理器
    /// </summary>
    /// <typeparam name="T">连接类型</typeparam>
    public class ConnectionPoolManager<T> where T : IDeviceClient
    {
        private readonly ConcurrentDictionary<string, List<PooledConnection<T>>> _connectionPool = new ConcurrentDictionary<string, List<PooledConnection<T>>>();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _connectionLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        private readonly Func<string, T> _connectionFactory;
        private readonly ConnectionPoolConfig _config;
        private readonly ILogger _logger;
        private readonly Timer _cleanupTimer;
        private readonly Timer _healthCheckTimer;
        private bool _isDisposed = false;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connectionFactory">连接工厂方法</param>
        /// <param name="config">连接池配置</param>
        /// <param name="logger">日志记录器</param>
        public ConnectionPoolManager(Func<string, T> connectionFactory, ConnectionPoolConfig config = null, ILogger logger = null)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _config = config ?? new ConnectionPoolConfig();
            _logger = logger;
            
            // 创建清理定时器，定期清理空闲连接
            _cleanupTimer = new Timer(CleanupIdleConnections, null, _config.IdleTimeout, _config.IdleTimeout);
            
            // 创建健康检查定时器，定期检查连接健康状态
            if (_config.EnableHealthCheck)
            {
                _healthCheckTimer = new Timer(CheckConnectionsHealth, null, _config.HealthCheckInterval, _config.HealthCheckInterval);
            }
        }
        
        /// <summary>
        /// 获取连接
        /// </summary>
        /// <param name="connectionId">连接标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接对象</returns>
        public async Task<T> GetConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentNullException(nameof(connectionId));
            }
            
            // 获取或创建连接锁
            var connectionLock = _connectionLocks.GetOrAdd(connectionId, new SemaphoreSlim(1, 1));
            
            try
            {
                // 尝试获取连接锁，带超时
                if (!await connectionLock.WaitAsync(_config.ConnectionTimeout, cancellationToken))
                {
                    throw new TimeoutException($"获取连接超时: {connectionId}");
                }
                
                try
                {
                    // 获取连接池
                    var connectionList = _connectionPool.GetOrAdd(connectionId, new List<PooledConnection<T>>());
                    
                    // 查找可用连接
                    var connection = connectionList.FirstOrDefault(c => !c.InUse && c.IsHealthy);
                    
                    // 如果没有可用连接且未达到最大连接数，创建新连接
                    if (connection == null && connectionList.Count < _config.MaxPoolSize)
                    {
                        _logger?.LogDebug("创建新连接: {ConnectionId}", connectionId);
                        connection = await CreateNewConnectionAsync(connectionId);
                        connectionList.Add(connection);
                    }
                    
                    // 如果仍然没有可用连接，等待连接释放
                    if (connection == null)
                    {
                        _logger?.LogDebug("等待连接释放: {ConnectionId}", connectionId);
                        
                        // 等待已有连接释放
                        var tcs = new TaskCompletionSource<bool>();
                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            cts.CancelAfter(_config.ConnectionTimeout);
                            cts.Token.Register(() => tcs.TrySetCanceled());
                            
                            await Task.WhenAny(
                                Task.Run(async () =>
                                {
                                    while (!cts.IsCancellationRequested)
                                    {
                                        await Task.Delay(100, cts.Token);
                                        connection = connectionList.FirstOrDefault(c => !c.InUse && c.IsHealthy);
                                        if (connection != null)
                                        {
                                            tcs.TrySetResult(true);
                                            break;
                                        }
                                    }
                                }, cts.Token),
                                tcs.Task
                            );
                        }
                        
                        if (connection == null)
                        {
                            throw new TimeoutException($"等待连接释放超时: {connectionId}");
                        }
                    }
                    
                    // 标记连接为正在使用
                    connection.InUse = true;
                    connection.LastUsedTime = DateTime.Now;
                    connection.UsageCount++;
                    
                    // 检查连接状态
                    if (!connection.Connection.Connected)
                    {
                        _logger?.LogDebug("连接未处于连接状态，尝试重新连接: {ConnectionId}", connectionId);
                        var connectResult = await connection.Connection.ConnectAsync();
                        
                        if (!connectResult.IsSuccess)
                        {
                            connection.IsHealthy = false;
                            connection.InUse = false;
                            throw new InvalidOperationException($"无法连接到目标设备: {connectionId}, 错误: {connectResult.Message}");
                        }
                    }
                    
                    return connection.Connection;
                }
                finally
                {
                    connectionLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取连接失败: {ConnectionId}", connectionId);
                throw;
            }
        }
        
        /// <summary>
        /// 释放连接
        /// </summary>
        /// <param name="connection">连接对象</param>
        /// <param name="connectionId">连接标识</param>
        public void ReleaseConnection(T connection, string connectionId)
        {
            if (connection == null || string.IsNullOrEmpty(connectionId))
            {
                return;
            }
            
            try
            {
                // 获取连接池
                if (_connectionPool.TryGetValue(connectionId, out var connectionList))
                {
                    // 查找连接
                    var pooledConnection = connectionList.FirstOrDefault(c => c.Connection.Equals(connection));
                    
                    if (pooledConnection != null)
                    {
                        // 检查连接使用次数是否超过限制
                        if (pooledConnection.UsageCount >= _config.MaxUsageCount)
                        {
                            _logger?.LogDebug("连接使用次数已达上限，释放连接: {ConnectionId}, UsageCount: {UsageCount}", 
                                connectionId, pooledConnection.UsageCount);
                            
                            // 从连接池中移除
                            connectionList.Remove(pooledConnection);
                            
                            // 关闭连接
                            if (pooledConnection.Connection.Connected)
                            {
                                pooledConnection.Connection.Disconnect();
                            }
                        }
                        else
                        {
                            // 标记连接为未使用
                            pooledConnection.InUse = false;
                            pooledConnection.LastUsedTime = DateTime.Now;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "释放连接失败: {ConnectionId}", connectionId);
            }
        }
        
        /// <summary>
        /// 清理指定连接池
        /// </summary>
        /// <param name="connectionId">连接标识</param>
        public async Task ClearPoolAsync(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                return;
            }
            
            try
            {
                // 获取连接锁
                var connectionLock = _connectionLocks.GetOrAdd(connectionId, new SemaphoreSlim(1, 1));
                
                await connectionLock.WaitAsync();
                
                try
                {
                    // 获取连接池
                    if (_connectionPool.TryGetValue(connectionId, out var connectionList))
                    {
                        _logger?.LogDebug("清理连接池: {ConnectionId}, 连接数: {ConnectionCount}", 
                            connectionId, connectionList.Count);
                        
                        // 关闭所有连接
                        foreach (var connection in connectionList)
                        {
                            if (connection.Connection.Connected)
                            {
                                connection.Connection.Disconnect();
                            }
                        }
                        
                        // 清空连接池
                        connectionList.Clear();
                    }
                }
                finally
                {
                    connectionLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清理连接池失败: {ConnectionId}", connectionId);
            }
        }
        
        /// <summary>
        /// 清理所有连接池
        /// </summary>
        public async Task ClearAllPoolsAsync()
        {
            try
            {
                _logger?.LogDebug("清理所有连接池");
                
                // 获取所有连接标识
                var connectionIds = _connectionPool.Keys.ToList();
                
                // 清理每个连接池
                foreach (var connectionId in connectionIds)
                {
                    await ClearPoolAsync(connectionId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清理所有连接池失败");
            }
        }
        
        /// <summary>
        /// 清理空闲连接
        /// </summary>
        private async void CleanupIdleConnections(object state)
        {
            if (_isDisposed)
            {
                return;
            }
            
            try
            {
                _logger?.LogDebug("开始清理空闲连接");
                
                // 获取当前时间
                var now = DateTime.Now;
                
                // 获取所有连接标识
                var connectionIds = _connectionPool.Keys.ToList();
                
                foreach (var connectionId in connectionIds)
                {
                    // 获取连接锁
                    var connectionLock = _connectionLocks.GetOrAdd(connectionId, new SemaphoreSlim(1, 1));
                    
                    if (await connectionLock.WaitAsync(0))
                    {
                        try
                        {
                            // 获取连接池
                            if (_connectionPool.TryGetValue(connectionId, out var connectionList))
                            {
                                // 保留的连接数量（确保至少保留最小连接数）
                                int keepCount = Math.Max(_config.MinPoolSize, connectionList.Count(c => c.InUse));
                                
                                // 查找空闲超时的连接
                                var idleConnections = connectionList
                                    .Where(c => !c.InUse && (now - c.LastUsedTime).TotalMilliseconds > _config.IdleTimeout)
                                    .OrderBy(c => c.LastUsedTime)
                                    .ToList();
                                
                                // 计算需要移除的连接数量
                                int removeCount = Math.Max(0, connectionList.Count - keepCount);
                                
                                // 移除空闲超时的连接（但确保总连接数不少于keepCount）
                                foreach (var connection in idleConnections.Take(removeCount))
                                {
                                    _logger?.LogDebug("移除空闲连接: {ConnectionId}, 空闲时间: {IdleTime}ms", 
                                        connectionId, (now - connection.LastUsedTime).TotalMilliseconds);
                                    
                                    // 从连接池中移除
                                    connectionList.Remove(connection);
                                    
                                    // 关闭连接
                                    if (connection.Connection.Connected)
                                    {
                                        connection.Connection.Disconnect();
                                    }
                                }
                            }
                        }
                        finally
                        {
                            connectionLock.Release();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清理空闲连接失败");
            }
        }
        
        /// <summary>
        /// 检查连接健康状态
        /// </summary>
        private async void CheckConnectionsHealth(object state)
        {
            if (_isDisposed || !_config.EnableHealthCheck)
            {
                return;
            }
            
            try
            {
                _logger?.LogDebug("开始检查连接健康状态");
                
                // 获取当前时间
                var now = DateTime.Now;
                
                // 获取所有连接标识
                var connectionIds = _connectionPool.Keys.ToList();
                
                foreach (var connectionId in connectionIds)
                {
                    // 获取连接锁
                    var connectionLock = _connectionLocks.GetOrAdd(connectionId, new SemaphoreSlim(1, 1));
                    
                    if (await connectionLock.WaitAsync(0))
                    {
                        try
                        {
                            // 获取连接池
                            if (_connectionPool.TryGetValue(connectionId, out var connectionList))
                            {
                                // 查找需要检查健康状态的连接（未在使用且上次检查时间超过间隔）
                                var connectionsToCheck = connectionList
                                    .Where(c => !c.InUse && (now - c.LastHealthCheckTime).TotalMilliseconds > _config.HealthCheckInterval)
                                    .ToList();
                                
                                foreach (var connection in connectionsToCheck)
                                {
                                    // 更新最后健康检查时间
                                    connection.LastHealthCheckTime = now;
                                    
                                    // 检查连接状态
                                    if (connection.Connection is IAutoReconnectClient autoReconnectClient)
                                    {
                                        try
                                        {
                                            var healthResult = await autoReconnectClient.CheckAndReconnectAsync();
                                            connection.IsHealthy = healthResult.IsSuccess;
                                            
                                            if (!connection.IsHealthy)
                                            {
                                                _logger?.LogWarning("连接健康检查失败: {ConnectionId}, 错误: {Error}", 
                                                    connectionId, healthResult.Message);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            connection.IsHealthy = false;
                                            _logger?.LogError(ex, "连接健康检查异常: {ConnectionId}", connectionId);
                                        }
                                    }
                                    else
                                    {
                                        // 如果不支持自动重连，简单检查连接状态
                                        connection.IsHealthy = connection.Connection.Connected;
                                        
                                        if (!connection.IsHealthy)
                                        {
                                            _logger?.LogWarning("连接未连接: {ConnectionId}", connectionId);
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            connectionLock.Release();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "检查连接健康状态失败");
            }
        }
        
        /// <summary>
        /// 创建新连接
        /// </summary>
        /// <param name="connectionId">连接标识</param>
        /// <returns>连接对象</returns>
        private async Task<PooledConnection<T>> CreateNewConnectionAsync(string connectionId)
        {
            try
            {
                // 创建新连接
                var connection = _connectionFactory(connectionId);
                
                // 连接到设备
                var connectResult = await connection.ConnectAsync();
                
                if (!connectResult.IsSuccess)
                {
                    throw new InvalidOperationException($"无法连接到目标设备: {connectionId}, 错误: {connectResult.Message}");
                }
                
                return new PooledConnection<T>(connection);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建连接失败: {ConnectionId}", connectionId);
                throw;
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public async Task DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }
            
            _isDisposed = true;
            
            // 停止定时器
            _cleanupTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _healthCheckTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            
            // 清理所有连接
            await ClearAllPoolsAsync();
            
            // 释放定时器
            _cleanupTimer?.Dispose();
            _healthCheckTimer?.Dispose();
        }
    }
} 
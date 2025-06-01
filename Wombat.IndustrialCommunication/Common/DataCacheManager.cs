using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 缓存配置
    /// </summary>
    public class DataCacheConfig
    {
        /// <summary>
        /// 缓存项默认过期时间（毫秒）
        /// </summary>
        public int DefaultExpiration { get; set; } = 5000;
        
        /// <summary>
        /// 缓存项最大数量
        /// </summary>
        public int MaxCacheItems { get; set; } = 10000;
        
        /// <summary>
        /// 清理间隔（毫秒）
        /// </summary>
        public int CleanupInterval { get; set; } = 60000;
        
        /// <summary>
        /// 是否启用后台更新（当缓存项接近过期时自动更新）
        /// </summary>
        public bool EnableBackgroundRefresh { get; set; } = false;
        
        /// <summary>
        /// 后台更新阈值（毫秒），当距离过期时间小于此值时进行后台更新
        /// </summary>
        public int BackgroundRefreshThreshold { get; set; } = 1000;
    }
    
    /// <summary>
    /// 缓存项
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    internal class CacheItem<T>
    {
        /// <summary>
        /// 缓存数据
        /// </summary>
        public T Data { get; set; }
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; private set; }
        
        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTime ExpirationTime { get; set; }
        
        /// <summary>
        /// 上次访问时间
        /// </summary>
        public DateTime LastAccessTime { get; set; }
        
        /// <summary>
        /// 访问次数
        /// </summary>
        public int AccessCount { get; set; }
        
        /// <summary>
        /// 是否正在更新
        /// </summary>
        public bool IsRefreshing { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="data">缓存数据</param>
        /// <param name="expirationMs">过期时间（毫秒）</param>
        public CacheItem(T data, int expirationMs)
        {
            Data = data;
            CreatedTime = DateTime.Now;
            LastAccessTime = DateTime.Now;
            ExpirationTime = DateTime.Now.AddMilliseconds(expirationMs);
            AccessCount = 1;
        }
        
        /// <summary>
        /// 更新过期时间
        /// </summary>
        /// <param name="expirationMs">过期时间（毫秒）</param>
        public void UpdateExpiration(int expirationMs)
        {
            ExpirationTime = DateTime.Now.AddMilliseconds(expirationMs);
        }
        
        /// <summary>
        /// 是否已过期
        /// </summary>
        public bool IsExpired => DateTime.Now > ExpirationTime;
        
        /// <summary>
        /// 是否需要后台更新（距离过期时间小于阈值）
        /// </summary>
        /// <param name="thresholdMs">阈值（毫秒）</param>
        /// <returns>是否需要后台更新</returns>
        public bool NeedsBackgroundRefresh(int thresholdMs)
        {
            return !IsRefreshing && (ExpirationTime - DateTime.Now).TotalMilliseconds < thresholdMs;
        }
    }
    
    /// <summary>
    /// 缓存键
    /// </summary>
    public class CacheKey
    {
        /// <summary>
        /// 设备标识
        /// </summary>
        public string DeviceId { get; set; }
        
        /// <summary>
        /// 地址
        /// </summary>
        public string Address { get; set; }
        
        /// <summary>
        /// 数据类型
        /// </summary>
        public string DataType { get; set; }
        
        /// <summary>
        /// 数据长度
        /// </summary>
        public int Length { get; set; }
        
        /// <summary>
        /// 附加信息
        /// </summary>
        public string AdditionalInfo { get; set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="deviceId">设备标识</param>
        /// <param name="address">地址</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="length">数据长度</param>
        /// <param name="additionalInfo">附加信息</param>
        public CacheKey(string deviceId, string address, string dataType = null, int length = 0, string additionalInfo = null)
        {
            DeviceId = deviceId;
            Address = address;
            DataType = dataType;
            Length = length;
            AdditionalInfo = additionalInfo;
        }
        
        /// <summary>
        /// 获取缓存键字符串
        /// </summary>
        /// <returns>缓存键字符串</returns>
        public override string ToString()
        {
            var result = $"{DeviceId}_{Address}";
            
            if (!string.IsNullOrEmpty(DataType))
            {
                result += $"_{DataType}";
            }
            
            if (Length > 0)
            {
                result += $"_{Length}";
            }
            
            if (!string.IsNullOrEmpty(AdditionalInfo))
            {
                result += $"_{AdditionalInfo}";
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取哈希码
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
        
        /// <summary>
        /// 判断相等
        /// </summary>
        /// <param name="obj">比较对象</param>
        /// <returns>是否相等</returns>
        public override bool Equals(object obj)
        {
            if (obj is CacheKey other)
            {
                return ToString().Equals(other.ToString());
            }
            
            return false;
        }
    }
    
    /// <summary>
    /// 数据缓存管理器
    /// </summary>
    public class DataCacheManager
    {
        private readonly ConcurrentDictionary<string, object> _cache = new ConcurrentDictionary<string, object>();
        private readonly DataCacheConfig _config;
        private readonly ILogger _logger;
        private readonly Timer _cleanupTimer;
        private bool _isDisposed = false;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">缓存配置</param>
        /// <param name="logger">日志记录器</param>
        public DataCacheManager(DataCacheConfig config = null, ILogger logger = null)
        {
            _config = config ?? new DataCacheConfig();
            _logger = logger;
            
            // 创建清理定时器，定期清理过期缓存项
            _cleanupTimer = new Timer(CleanupExpiredItems, null, _config.CleanupInterval, _config.CleanupInterval);
        }
        
        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="valueFactory">数据获取工厂方法，当缓存未命中时调用</param>
        /// <param name="expirationMs">过期时间（毫秒），默认使用配置的默认过期时间</param>
        /// <param name="forceRefresh">是否强制刷新缓存</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>缓存数据</returns>
        public async Task<T> GetOrAddAsync<T>(
            CacheKey key, 
            Func<CancellationToken, Task<T>> valueFactory, 
            int expirationMs = -1, 
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            
            if (valueFactory == null)
            {
                throw new ArgumentNullException(nameof(valueFactory));
            }
            
            if (expirationMs < 0)
            {
                expirationMs = _config.DefaultExpiration;
            }
            
            string cacheKey = key.ToString();
            
            // 尝试从缓存获取数据
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out object cacheObj))
            {
                var cacheItem = (CacheItem<T>)cacheObj;
                
                // 检查是否过期
                if (!cacheItem.IsExpired)
                {
                    // 更新访问信息
                    cacheItem.LastAccessTime = DateTime.Now;
                    cacheItem.AccessCount++;
                    
                    // 如果启用后台更新且接近过期，触发后台更新
                    if (_config.EnableBackgroundRefresh && cacheItem.NeedsBackgroundRefresh(_config.BackgroundRefreshThreshold))
                    {
                        RefreshCacheInBackgroundAsync(key, valueFactory, expirationMs, cancellationToken).ConfigureAwait(false);
                    }
                    
                    _logger?.LogDebug("缓存命中: {CacheKey}, 访问次数: {AccessCount}", cacheKey, cacheItem.AccessCount);
                    
                    return cacheItem.Data;
                }
                
                _logger?.LogDebug("缓存已过期: {CacheKey}", cacheKey);
            }
            
            // 缓存未命中或强制刷新，获取新数据
            try
            {
                _logger?.LogDebug("获取新数据: {CacheKey}", cacheKey);
                
                // 调用工厂方法获取数据
                T newData = await valueFactory(cancellationToken);
                
                // 创建新的缓存项
                var newCacheItem = new CacheItem<T>(newData, expirationMs);
                
                // 添加到缓存
                _cache[cacheKey] = newCacheItem;
                
                // 检查缓存项数量是否超过限制
                if (_cache.Count > _config.MaxCacheItems)
                {
                    CleanupExpiredItems(null);
                }
                
                return newData;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取数据失败: {CacheKey}", cacheKey);
                
                // 如果获取新数据失败但缓存中有过期数据，返回过期数据
                if (_cache.TryGetValue(cacheKey, out object expiredCacheObj))
                {
                    var expiredCacheItem = (CacheItem<T>)expiredCacheObj;
                    _logger?.LogWarning("返回过期数据: {CacheKey}, 过期时间: {ExpiredTime}", 
                        cacheKey, expiredCacheItem.ExpirationTime);
                    
                    return expiredCacheItem.Data;
                }
                
                throw;
            }
        }
        
        /// <summary>
        /// 在后台刷新缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="valueFactory">数据获取工厂方法</param>
        /// <param name="expirationMs">过期时间（毫秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task RefreshCacheInBackgroundAsync<T>(
            CacheKey key, 
            Func<CancellationToken, Task<T>> valueFactory, 
            int expirationMs,
            CancellationToken cancellationToken)
        {
            string cacheKey = key.ToString();
            
            if (_cache.TryGetValue(cacheKey, out object cacheObj))
            {
                var cacheItem = (CacheItem<T>)cacheObj;
                
                // 如果已经在刷新中，不重复刷新
                if (cacheItem.IsRefreshing)
                {
                    return;
                }
                
                // 标记为正在刷新
                cacheItem.IsRefreshing = true;
                
                try
                {
                    _logger?.LogDebug("后台刷新缓存: {CacheKey}", cacheKey);
                    
                    // 获取新数据
                    T newData = await valueFactory(cancellationToken);
                    
                    // 更新缓存项
                    cacheItem.Data = newData;
                    cacheItem.UpdateExpiration(expirationMs);
                    
                    _logger?.LogDebug("后台刷新缓存完成: {CacheKey}, 新过期时间: {ExpirationTime}", 
                        cacheKey, cacheItem.ExpirationTime);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "后台刷新缓存失败: {CacheKey}", cacheKey);
                }
                finally
                {
                    // 取消标记
                    cacheItem.IsRefreshing = false;
                }
            }
        }
        
        /// <summary>
        /// 移除缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>是否成功移除</returns>
        public bool Remove(CacheKey key)
        {
            if (key == null)
            {
                return false;
            }
            
            string cacheKey = key.ToString();
            
            _logger?.LogDebug("移除缓存项: {CacheKey}", cacheKey);
            
            return _cache.TryRemove(cacheKey, out _);
        }
        
        /// <summary>
        /// 批量移除缓存项
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        /// <returns>移除的数量</returns>
        public int RemoveByPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return 0;
            }
            
            int count = 0;
            
            // 查找匹配前缀的缓存键
            foreach (var key in _cache.Keys)
            {
                if (key.StartsWith(prefix))
                {
                    if (_cache.TryRemove(key, out _))
                    {
                        count++;
                    }
                }
            }
            
            _logger?.LogDebug("移除前缀为 {Prefix} 的缓存项: {Count} 个", prefix, count);
            
            return count;
        }
        
        /// <summary>
        /// 清空缓存
        /// </summary>
        public void Clear()
        {
            _logger?.LogDebug("清空缓存, 共 {Count} 个缓存项", _cache.Count);
            
            _cache.Clear();
        }
        
        /// <summary>
        /// 获取缓存项数量
        /// </summary>
        /// <returns>缓存项数量</returns>
        public int GetCount()
        {
            return _cache.Count;
        }
        
        /// <summary>
        /// 清理过期缓存项
        /// </summary>
        private void CleanupExpiredItems(object state)
        {
            if (_isDisposed)
            {
                return;
            }
            
            try
            {
                int expiredCount = 0;
                int totalCount = _cache.Count;
                
                // 如果缓存项数量未超过限制，只清理过期项
                if (totalCount <= _config.MaxCacheItems)
                {
                    foreach (var key in _cache.Keys)
                    {
                        if (_cache.TryGetValue(key, out object cacheObj))
                        {
                            // 由于缓存项类型不确定，需要使用反射判断是否过期
                            var expirationTimeProp = cacheObj.GetType().GetProperty("IsExpired");
                            bool isExpired = expirationTimeProp != null && (bool)expirationTimeProp.GetValue(cacheObj);
                            
                            if (isExpired && _cache.TryRemove(key, out _))
                            {
                                expiredCount++;
                            }
                        }
                    }
                }
                else
                {
                    // 缓存项数量超过限制，按访问时间和访问次数综合评分排序
                    var allItems = new List<Tuple<string, object, int>>();
                    
                    foreach (var kvp in _cache)
                    {
                        // 计算评分：50% 访问时间 + 50% 访问次数
                        var lastAccessTimeProp = kvp.Value.GetType().GetProperty("LastAccessTime");
                        var accessCountProp = kvp.Value.GetType().GetProperty("AccessCount");
                        
                        if (lastAccessTimeProp != null && accessCountProp != null)
                        {
                            DateTime lastAccessTime = (DateTime)lastAccessTimeProp.GetValue(kvp.Value);
                            int accessCount = (int)accessCountProp.GetValue(kvp.Value);
                            
                            // 评分：访问时间越近、访问次数越多，得分越高
                            int score = (int)((DateTime.Now - lastAccessTime).TotalSeconds) - accessCount;
                            
                            allItems.Add(new Tuple<string, object, int>(kvp.Key, kvp.Value, score));
                        }
                    }
                    
                    // 按评分从高到低排序（评分越低越重要）
                    allItems.Sort((a, b) => b.Item3.CompareTo(a.Item3));
                    
                    // 需要移除的数量
                    int removeCount = Math.Max(0, totalCount - _config.MaxCacheItems);
                    
                    // 移除低分项
                    for (int i = 0; i < removeCount && i < allItems.Count; i++)
                    {
                        if (_cache.TryRemove(allItems[i].Item1, out _))
                        {
                            expiredCount++;
                        }
                    }
                }
                
                if (expiredCount > 0)
                {
                    _logger?.LogDebug("清理过期缓存项: {ExpiredCount} 个, 剩余: {RemainingCount} 个", 
                        expiredCount, _cache.Count);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清理过期缓存项失败");
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            
            _isDisposed = true;
            
            // 停止定时器
            _cleanupTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer?.Dispose();
            
            // 清空缓存
            _cache.Clear();
        }
    }
} 
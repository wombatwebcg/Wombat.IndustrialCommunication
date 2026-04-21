using System;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 连接池运行参数。
    /// </summary>
    public class ConnectionPoolOptions
    {
        /// <summary>
        /// 池内允许的最大连接条目数。
        /// </summary>
        public int MaxConnections { get; set; } = 256;

        /// <summary>
        /// 无租约时连接可空闲的最长时间。
        /// </summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 单个租约默认超时时间。
        /// </summary>
        public TimeSpan LeaseTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 后台健康检查间隔。
        /// </summary>
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 可恢复故障的最大重试次数。
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// 重试退避基准时长。
        /// </summary>
        public TimeSpan RetryBackoff { get; set; } = TimeSpan.FromMilliseconds(200);
    }
}

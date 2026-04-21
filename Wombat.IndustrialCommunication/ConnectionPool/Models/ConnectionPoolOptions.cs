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
        /// 是否启用后台维护循环。
        /// </summary>
        public bool EnableBackgroundMaintenance { get; set; } = true;

        /// <summary>
        /// 健康检查是否仅针对无租约连接。
        /// </summary>
        public bool HealthCheckLeaseFreeOnly { get; set; } = true;

        /// <summary>
        /// 租约过期扫描周期。
        /// </summary>
        public TimeSpan LeaseExpirationSweepInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 故障态再次尝试恢复前的冷却时长。
        /// </summary>
        public TimeSpan FaultedReconnectCooldown { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 健康检查连续失败阈值，超过后转为失效。
        /// </summary>
        public int MaxConsecutiveHealthCheckFailures { get; set; } = 3;

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

using System;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 维护后台健康检查与租约扫描的触发节奏。
    /// </summary>
    internal sealed class ConnectionStateMonitor
    {
        private DateTime _lastHealthCheckUtc = DateTime.MinValue;
        private DateTime _lastLeaseSweepUtc = DateTime.MinValue;

        public bool ShouldRunHealthCheck(DateTime utcNow, ConnectionPoolOptions options)
        {
            if (options == null || options.HealthCheckInterval <= TimeSpan.Zero)
            {
                return false;
            }

            return utcNow - _lastHealthCheckUtc >= options.HealthCheckInterval;
        }

        public bool ShouldSweepExpiredLeases(DateTime utcNow, ConnectionPoolOptions options)
        {
            if (options == null || options.LeaseExpirationSweepInterval <= TimeSpan.Zero)
            {
                return false;
            }

            return utcNow - _lastLeaseSweepUtc >= options.LeaseExpirationSweepInterval;
        }

        public void MarkHealthCheck(DateTime utcNow)
        {
            _lastHealthCheckUtc = utcNow;
        }

        public void MarkLeaseSweep(DateTime utcNow)
        {
            _lastLeaseSweepUtc = utcNow;
        }

        public TimeSpan GetNextDelay(ConnectionPoolOptions options)
        {
            var candidate = TimeSpan.FromSeconds(1);
            if (options == null)
            {
                return candidate;
            }

            if (options.HealthCheckInterval > TimeSpan.Zero && options.HealthCheckInterval < candidate)
            {
                candidate = options.HealthCheckInterval;
            }

            if (options.LeaseExpirationSweepInterval > TimeSpan.Zero && options.LeaseExpirationSweepInterval < candidate)
            {
                candidate = options.LeaseExpirationSweepInterval;
            }

            return candidate < TimeSpan.FromMilliseconds(200) ? TimeSpan.FromMilliseconds(200) : candidate;
        }
    }
}

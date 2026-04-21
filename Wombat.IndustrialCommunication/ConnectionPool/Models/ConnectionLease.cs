using System;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 连接租约信息，用于表达获取-释放生命周期。
    /// </summary>
    public class ConnectionLease
    {
        /// <summary>
        /// 租约 ID，由连接池生成并用于释放。
        /// </summary>
        public string LeaseId { get; set; }

        /// <summary>
        /// 租约关联的连接标识。
        /// </summary>
        public ConnectionIdentity Identity { get; set; }

        /// <summary>
        /// 获取时间（UTC）。
        /// </summary>
        public DateTime AcquiredAtUtc { get; set; }

        /// <summary>
        /// 到期时间（UTC）。
        /// </summary>
        public DateTime ExpiresAtUtc { get; set; }

        public ConnectionLease()
        {
            LeaseId = string.Empty;
            Identity = new ConnectionIdentity();
            AcquiredAtUtc = DateTime.UtcNow;
            ExpiresAtUtc = DateTime.UtcNow;
        }
    }
}

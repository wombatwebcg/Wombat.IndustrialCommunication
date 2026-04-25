using System;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 单个连接条目的可观测快照。
    /// </summary>
    public class ConnectionEntrySnapshot
    {
        public ConnectionIdentity Identity { get; set; }

        public ConnectionEntryState State { get; set; }

        public ConnectionEntryLifecycleState LifecycleState { get; set; }

        public int ActiveLeaseCount { get; set; }

        public int FailureCount { get; set; }

        public bool HasExpiredLease { get; set; }

        public bool IsUnderMaintenance { get; set; }

        public DateTime LastActiveTimeUtc { get; set; }

        public DateTime? LastConnectedTimeUtc { get; set; }

        public DateTime? LastFailureTimeUtc { get; set; }

        public DateTime? LastRecoveredTimeUtc { get; set; }

        public DateTime? LastMaintenanceTimeUtc { get; set; }

        public string LastFailureReason { get; set; }

        public ConnectionPoolMaintenanceMode LastMaintenanceMode { get; set; }

        public ConnectionEntrySnapshot()
        {
            Identity = new ConnectionIdentity();
            LastFailureReason = string.Empty;
            LastMaintenanceMode = ConnectionPoolMaintenanceMode.Unknown;
            LifecycleState = ConnectionEntryLifecycleState.Uninitialized;
        }
    }
}

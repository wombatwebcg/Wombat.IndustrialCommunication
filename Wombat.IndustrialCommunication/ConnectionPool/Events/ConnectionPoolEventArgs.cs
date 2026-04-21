using System;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Events
{
    /// <summary>
    /// 连接池生命周期事件参数。
    /// </summary>
    public class ConnectionPoolEventArgs : EventArgs
    {
        public ConnectionIdentity Identity { get; set; }

        public ConnectionPoolEventType EventType { get; set; }

        public ConnectionEntryState State { get; set; }

        public string Message { get; set; }

        public Exception Exception { get; set; }

        public int ActiveLeaseCount { get; set; }

        public int FailureCount { get; set; }

        public ConnectionPoolMaintenanceMode TriggerMode { get; set; }

        public DateTime OccurredAtUtc { get; set; }

        public ConnectionPoolEventArgs()
        {
            Identity = new ConnectionIdentity();
            Message = string.Empty;
            TriggerMode = ConnectionPoolMaintenanceMode.Unknown;
            OccurredAtUtc = DateTime.UtcNow;
        }
    }
}

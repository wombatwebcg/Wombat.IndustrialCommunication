using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Events
{
    /// <summary>
    /// 租约生命周期事件参数。
    /// </summary>
    public class ConnectionLeaseEventArgs : ConnectionPoolEventArgs
    {
        public ConnectionLease Lease { get; set; }

        public bool LeaseExpired { get; set; }

        public ConnectionLeaseEventArgs()
        {
            Lease = new ConnectionLease();
        }
    }
}

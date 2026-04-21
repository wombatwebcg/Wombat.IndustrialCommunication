using System;
using Wombat.IndustrialCommunication.ConnectionPool.Events;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 连接池事件源接口。
    /// </summary>
    public interface IDeviceConnectionPoolEvents
    {
        /// <summary>
        /// 连接池通用生命周期事件。
        /// </summary>
        event EventHandler<ConnectionPoolEventArgs> PoolEventOccurred;

        /// <summary>
        /// 连接状态变化事件。
        /// </summary>
        event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        /// <summary>
        /// 租约生命周期事件。
        /// </summary>
        event EventHandler<ConnectionLeaseEventArgs> LeaseChanged;

        /// <summary>
        /// 后台维护事件。
        /// </summary>
        event EventHandler<ConnectionMaintenanceEventArgs> MaintenanceCompleted;
    }
}

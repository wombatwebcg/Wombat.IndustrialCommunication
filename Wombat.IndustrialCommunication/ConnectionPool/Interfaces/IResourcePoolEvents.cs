using System;
using Wombat.IndustrialCommunication.ConnectionPool.Events;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 资源池事件源接口。
    /// </summary>
    public interface IResourcePoolEvents
    {
        event EventHandler<ConnectionPoolEventArgs> PoolEventOccurred;

        event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        event EventHandler<ConnectionLeaseEventArgs> LeaseChanged;

        event EventHandler<ConnectionMaintenanceEventArgs> MaintenanceCompleted;
    }
}

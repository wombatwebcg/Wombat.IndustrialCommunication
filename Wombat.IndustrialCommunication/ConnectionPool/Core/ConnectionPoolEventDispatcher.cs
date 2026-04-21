using System;
using Wombat.IndustrialCommunication.ConnectionPool.Events;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 连接池事件分发器，统一管理事件发布与订阅。
    /// </summary>
    internal sealed class ConnectionPoolEventDispatcher : IConnectionPoolEventPublisher
    {
        private readonly object _sender;

        public ConnectionPoolEventDispatcher(object sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public event EventHandler<ConnectionPoolEventArgs> PoolEventOccurred;

        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        public event EventHandler<ConnectionLeaseEventArgs> LeaseChanged;

        public event EventHandler<ConnectionMaintenanceEventArgs> MaintenanceCompleted;

        public void Publish(ConnectionPoolEventArgs args)
        {
            PoolEventOccurred?.Invoke(_sender, args);
        }

        public void PublishStateChanged(ConnectionStateChangedEventArgs args)
        {
            ConnectionStateChanged?.Invoke(_sender, args);
        }

        public void PublishLeaseEvent(ConnectionLeaseEventArgs args)
        {
            LeaseChanged?.Invoke(_sender, args);
        }

        public void PublishMaintenanceEvent(ConnectionMaintenanceEventArgs args)
        {
            MaintenanceCompleted?.Invoke(_sender, args);
        }
    }
}

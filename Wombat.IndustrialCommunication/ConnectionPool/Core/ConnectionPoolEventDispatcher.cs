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
        private readonly bool _isolateSubscriberExceptions;

        public ConnectionPoolEventDispatcher(object sender, bool isolateSubscriberExceptions)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _isolateSubscriberExceptions = isolateSubscriberExceptions;
        }

        public event EventHandler<ConnectionPoolEventArgs> PoolEventOccurred;

        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        public event EventHandler<ConnectionLeaseEventArgs> LeaseChanged;

        public event EventHandler<ConnectionMaintenanceEventArgs> MaintenanceCompleted;

        public void Publish(ConnectionPoolEventArgs args)
        {
            Raise(PoolEventOccurred, args);
        }

        public void PublishStateChanged(ConnectionStateChangedEventArgs args)
        {
            Raise(ConnectionStateChanged, args);
        }

        public void PublishLeaseEvent(ConnectionLeaseEventArgs args)
        {
            Raise(LeaseChanged, args);
        }

        public void PublishMaintenanceEvent(ConnectionMaintenanceEventArgs args)
        {
            Raise(MaintenanceCompleted, args);
        }

        private void Raise<TEventArgs>(EventHandler<TEventArgs> handler, TEventArgs args) where TEventArgs : EventArgs
        {
            if (handler == null)
            {
                return;
            }

            var delegates = handler.GetInvocationList();
            for (var i = 0; i < delegates.Length; i++)
            {
                var subscriber = delegates[i] as EventHandler<TEventArgs>;
                if (subscriber == null)
                {
                    continue;
                }

                if (_isolateSubscriberExceptions)
                {
                    try
                    {
                        subscriber(_sender, args);
                    }
                    catch
                    {
                    }

                    continue;
                }

                subscriber(_sender, args);
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Events;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 连接池事件分发器，统一管理事件发布与订阅。
    /// </summary>
    internal sealed class ConnectionPoolEventDispatcher : IConnectionPoolEventPublisher, IDisposable
    {
        private readonly object _sender;
        private readonly bool _isolateSubscriberExceptions;
        private readonly BlockingCollection<Action> _events = new BlockingCollection<Action>();
        private readonly Task _dispatcherTask;

        public ConnectionPoolEventDispatcher(object sender, bool isolateSubscriberExceptions)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _isolateSubscriberExceptions = isolateSubscriberExceptions;
            _dispatcherTask = Task.Run((Action)DispatchLoop);
        }

        public event EventHandler<ConnectionPoolEventArgs> PoolEventOccurred;

        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        public event EventHandler<ConnectionLeaseEventArgs> LeaseChanged;

        public event EventHandler<ConnectionMaintenanceEventArgs> MaintenanceCompleted;

        public void Publish(ConnectionPoolEventArgs args)
        {
            Enqueue(() => Raise(PoolEventOccurred, args));
        }

        public void PublishStateChanged(ConnectionStateChangedEventArgs args)
        {
            Enqueue(() => Raise(ConnectionStateChanged, args));
        }

        public void PublishLeaseEvent(ConnectionLeaseEventArgs args)
        {
            Enqueue(() => Raise(LeaseChanged, args));
        }

        public void PublishMaintenanceEvent(ConnectionMaintenanceEventArgs args)
        {
            Enqueue(() => Raise(MaintenanceCompleted, args));
        }

        public void Dispose()
        {
            _events.CompleteAdding();
            try
            {
                _dispatcherTask.GetAwaiter().GetResult();
            }
            catch
            {
            }

            _events.Dispose();
        }

        private void Enqueue(Action action)
        {
            if (action == null || _events.IsAddingCompleted)
            {
                return;
            }

            try
            {
                _events.Add(action);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void DispatchLoop()
        {
            foreach (var action in _events.GetConsumingEnumerable())
            {
                try
                {
                    action();
                }
                catch
                {
                }
            }
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

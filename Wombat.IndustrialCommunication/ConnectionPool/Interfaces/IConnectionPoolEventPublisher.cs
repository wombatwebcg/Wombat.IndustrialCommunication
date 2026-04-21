using Wombat.IndustrialCommunication.ConnectionPool.Events;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 连接池内部事件发布抽象。
    /// </summary>
    public interface IConnectionPoolEventPublisher
    {
        void Publish(ConnectionPoolEventArgs args);

        void PublishStateChanged(ConnectionStateChangedEventArgs args);

        void PublishLeaseEvent(ConnectionLeaseEventArgs args);

        void PublishMaintenanceEvent(ConnectionMaintenanceEventArgs args);
    }
}

namespace Wombat.IndustrialCommunication.ConnectionPool.Events
{
    /// <summary>
    /// 连接池生命周期事件类型。
    /// </summary>
    public enum ConnectionPoolEventType
    {
        Registered = 0,
        ConnectStarting = 1,
        ConnectSucceeded = 2,
        ConnectFailed = 3,
        LeaseAcquired = 4,
        LeaseReleased = 5,
        ExecuteStarting = 6,
        ExecuteFailed = 7,
        Retrying = 8,
        Reconnecting = 9,
        Recovered = 10,
        Invalidated = 11,
        IdleCleaned = 12,
        LeaseExpired = 13,
        Disposed = 14,
        BackgroundMaintenanceCompleted = 15,
        ForceReconnectRequested = 16,
        Unregistered = 17
    }
}

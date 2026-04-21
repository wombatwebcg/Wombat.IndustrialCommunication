namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 触发连接池生命周期动作的来源。
    /// </summary>
    public enum ConnectionPoolMaintenanceMode
    {
        Unknown = 0,
        UserCall = 1,
        Background = 2,
        Cleanup = 3,
        ForceReconnect = 4,
        Dispose = 5
    }
}

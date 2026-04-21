namespace Wombat.IndustrialCommunication.ConnectionPool.Events
{
    /// <summary>
    /// 后台维护事件参数。
    /// </summary>
    public class ConnectionMaintenanceEventArgs : ConnectionPoolEventArgs
    {
        public int ScannedEntryCount { get; set; }

        public int AffectedEntryCount { get; set; }
    }
}

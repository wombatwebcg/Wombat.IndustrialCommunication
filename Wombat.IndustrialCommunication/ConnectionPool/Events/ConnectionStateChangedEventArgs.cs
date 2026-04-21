using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Events
{
    /// <summary>
    /// 连接状态变化事件参数。
    /// </summary>
    public class ConnectionStateChangedEventArgs : ConnectionPoolEventArgs
    {
        public ConnectionEntryState PreviousState { get; set; }

        public ConnectionEntryState CurrentState { get; set; }
    }
}

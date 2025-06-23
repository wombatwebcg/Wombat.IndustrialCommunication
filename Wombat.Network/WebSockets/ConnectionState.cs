namespace Wombat.Network
{
    /// <summary>
    /// 表示连接的通用状态。
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// 初始状态，表示连接尚未建立。
        /// </summary>
        None = 0,
        
        /// <summary>
        /// 正在连接状态，表示正在尝试建立连接。
        /// </summary>
        Connecting = 1,
        
        /// <summary>
        /// 已连接状态，表示连接已成功建立并可以进行数据传输。
        /// </summary>
        Connected = 2,
        
        /// <summary>
        /// 正在关闭状态，表示正在关闭连接。
        /// </summary>
        Closing = 4,
        
        /// <summary>
        /// 已关闭状态，表示连接已关闭或断开。
        /// </summary>
        Closed = 5,
    }
}

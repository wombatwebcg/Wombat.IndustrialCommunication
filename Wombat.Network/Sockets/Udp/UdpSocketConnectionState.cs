namespace Wombat.Network.Sockets
{
    /// <summary>
    /// UDP套接字连接状态枚举
    /// </summary>
    public enum UdpSocketConnectionState
    {
        /// <summary>
        /// 未初始化状态
        /// </summary>
        None = 0,
        
        /// <summary>
        /// 正在启动状态
        /// </summary>
        Starting = 1,
        
        /// <summary>
        /// 活跃状态（可收发数据）
        /// </summary>
        Active = 2,
        
        /// <summary>
        /// 已关闭状态
        /// </summary>
        Closed = 5,
    }
} 
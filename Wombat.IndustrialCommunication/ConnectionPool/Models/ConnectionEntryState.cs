namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 对外公开的连接条目稳定状态。
    /// </summary>
    public enum ConnectionEntryState
    {
        /// <summary>
        /// 当前未建立可用连接，或连接已释放。
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// 当前连接可用且空闲。
        /// </summary>
        Ready = 1,

        /// <summary>
        /// 当前连接正在被租用执行中。
        /// </summary>
        Busy = 2,

        /// <summary>
        /// 当前连接不可用，需要人工关注、维护或重建。
        /// </summary>
        Unavailable = 3
    }
}

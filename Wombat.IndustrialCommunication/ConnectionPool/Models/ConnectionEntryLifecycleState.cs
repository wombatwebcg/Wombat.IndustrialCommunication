namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 连接条目内部生命周期状态。
    /// </summary>
    public enum ConnectionEntryLifecycleState
    {
        /// <summary>
        /// 尚未创建或建立底层连接。
        /// </summary>
        Uninitialized = 0,

        /// <summary>
        /// 正在建立连接。
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// 已建立连接且可被复用。
        /// </summary>
        Ready = 2,

        /// <summary>
        /// 当前存在活跃租约。
        /// </summary>
        Leased = 3,

        /// <summary>
        /// 正在执行恢复或重连。
        /// </summary>
        Reconnecting = 4,

        /// <summary>
        /// 连接执行或建连失败。
        /// </summary>
        Faulted = 5,

        /// <summary>
        /// 已被显式失效。
        /// </summary>
        Invalidated = 6,

        /// <summary>
        /// 已释放资源。
        /// </summary>
        Disposed = 7
    }
}

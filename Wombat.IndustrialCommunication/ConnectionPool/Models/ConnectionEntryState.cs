namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 连接条目状态机定义。
    /// </summary>
    public enum ConnectionEntryState
    {
        /// <summary>
        /// 尚未创建底层连接。
        /// </summary>
        Uninitialized = 0,

        /// <summary>
        /// 正在建立连接。
        /// </summary>
        Connecting = 1,

        /// <summary>
        /// 已建立连接且可被租用。
        /// </summary>
        Ready = 2,

        /// <summary>
        /// 已被租用执行中（可有一个或多个租约）。
        /// </summary>
        Leased = 3,

        /// <summary>
        /// 连接恢复中（重连/保活重试）。
        /// </summary>
        Reconnecting = 4,

        /// <summary>
        /// 连接不可用，需要失效或重建。
        /// </summary>
        Faulted = 5,

        /// <summary>
        /// 已被上层显式失效，后续不可继续租用。
        /// </summary>
        Invalidated = 6,

        /// <summary>
        /// 已释放资源并终止生命周期。
        /// </summary>
        Disposed = 7
    }
}

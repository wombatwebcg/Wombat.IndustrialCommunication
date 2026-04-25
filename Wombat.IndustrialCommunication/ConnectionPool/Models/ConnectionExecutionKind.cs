namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 连接池执行分类。
    /// </summary>
    public enum ConnectionExecutionKind
    {
        /// <summary>
        /// 读操作，默认允许恢复性重试。
        /// </summary>
        Read = 0,

        /// <summary>
        /// 写操作，默认不执行恢复性重试。
        /// </summary>
        Write = 1,

        /// <summary>
        /// 诊断操作，默认不执行恢复性重试。
        /// </summary>
        Diagnostic = 2
    }
}

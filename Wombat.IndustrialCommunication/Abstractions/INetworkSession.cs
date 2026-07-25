using System;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 网络会话接口
    /// </summary>
    public interface INetworkSession
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// 关闭会话
        /// </summary>
        void Close();

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> SendAsync(byte[] data);
    }
} 
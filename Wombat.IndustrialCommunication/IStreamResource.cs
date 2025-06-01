using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    public interface IStreamResource : IDisposable
    {
        TimeSpan ReceiveTimeout { get; set; }
        TimeSpan SendTimeout { get; set; }
        bool Connected { get; }
        Task<OperationResult<int>> Receive(byte[] buffer, int offset, int length, CancellationToken cancellationToken);
        Task<OperationResult> Send(byte[] buffer, int offset, int length, CancellationToken cancellationToken);
        void StreamClose();
        
        /// <summary>
        /// 检测连接的健康状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接是否健康的操作结果</returns>
        Task<OperationResult<bool>> IsConnectionHealthyAsync(CancellationToken cancellationToken = default);
    }
}

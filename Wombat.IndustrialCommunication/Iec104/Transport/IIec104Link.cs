using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.Iec104.Transport
{
    /// <summary>
    /// 104 链路抽象。仅收发字节，不解析协议，便于用内存链路做测试。
    /// </summary>
    public interface IIec104Link : IDisposable
    {
        bool IsConnected { get; }

        EndPoint RemoteEndPoint { get; }

        /// <summary>读取一批字节，返回 0 表示对端关闭。</summary>
        Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken);

        /// <summary>写入完整帧。</summary>
        Task WriteAsync(byte[] buffer, CancellationToken cancellationToken);
    }
}

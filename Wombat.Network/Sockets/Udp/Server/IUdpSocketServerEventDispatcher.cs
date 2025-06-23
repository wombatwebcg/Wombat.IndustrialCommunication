using System.Net;
using System.Threading.Tasks;

namespace Wombat.Network.Sockets
{
    public interface IUdpSocketServerEventDispatcher
    {
        /// <summary>
        /// 当新客户端会话启动时触发（在UDP情况下是首次收到数据时模拟连接）
        /// </summary>
        /// <param name="session">UDP客户端会话实例</param>
        Task OnSessionStarted(UdpSocketSession session);
        
        /// <summary>
        /// 当从客户端接收到数据时触发
        /// </summary>
        /// <param name="session">UDP客户端会话实例</param>
        /// <param name="data">接收到的数据</param>
        /// <param name="offset">数据起始偏移量</param>
        /// <param name="count">数据长度</param>
        Task OnSessionDataReceived(UdpSocketSession session, byte[] data, int offset, int count);
        
        /// <summary>
        /// 当客户端会话关闭时触发（在UDP情况下是超时未收到数据时模拟断连）
        /// </summary>
        /// <param name="session">UDP客户端会话实例</param>
        Task OnSessionClosed(UdpSocketSession session);
    }
} 
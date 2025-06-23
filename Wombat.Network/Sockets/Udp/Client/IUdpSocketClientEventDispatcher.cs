using System.Net;
using System.Threading.Tasks;

namespace Wombat.Network.Sockets
{
    public interface IUdpSocketClientEventDispatcher
    {
        /// <summary>
        /// 当与服务器建立连接时触发（在UDP情况下是模拟连接状态）
        /// </summary>
        /// <param name="client">UDP客户端实例</param>
        Task OnServerConnected(UdpSocketClient client);
        
        /// <summary>
        /// 当从服务器接收到数据时触发
        /// </summary>
        /// <param name="client">UDP客户端实例</param>
        /// <param name="data">接收到的数据</param>
        /// <param name="offset">数据起始偏移量</param>
        /// <param name="count">数据长度</param>
        /// <param name="remoteEndPoint">数据来源的远程终结点</param>
        Task OnServerDataReceived(UdpSocketClient client, byte[] data, int offset, int count, IPEndPoint remoteEndPoint);
        
        /// <summary>
        /// 当与服务器断开连接时触发（在UDP情况下是结束模拟连接状态）
        /// </summary>
        /// <param name="client">UDP客户端实例</param>
        Task OnServerDisconnected(UdpSocketClient client);
    }
} 
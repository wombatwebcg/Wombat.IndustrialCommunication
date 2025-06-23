using System.Threading.Tasks;

namespace Wombat.Network.Sockets
{
    public interface ITcpSocketClientEventDispatcher
    {
        Task OnServerConnected(TcpSocketClient client);
        Task OnServerDataReceived(TcpSocketClient client, byte[] data, int offset, int count);
        Task OnServerDisconnected(TcpSocketClient client);
    }
}

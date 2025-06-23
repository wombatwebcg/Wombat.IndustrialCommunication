using System.Threading.Tasks;

namespace Wombat.Network.Sockets
{
    public interface ITcpSocketServerEventDispatcher
    {
        Task OnSessionStarted(TcpSocketSession session);
        Task OnSessionDataReceived(TcpSocketSession session, byte[] data, int offset, int count);
        Task OnSessionClosed(TcpSocketSession session);
    }
}

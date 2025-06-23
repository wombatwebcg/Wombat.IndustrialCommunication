using System.Threading.Tasks;

namespace Wombat.Network.WebSockets
{
    public interface IAsyncWebSocketServerMessageDispatcher
    {
        Task OnSessionStarted(WebSocketSession session);
        Task OnSessionTextReceived(WebSocketSession session, string text);
        Task OnSessionBinaryReceived(WebSocketSession session, byte[] data, int offset, int count);
        Task OnSessionClosed(WebSocketSession session);

        Task OnSessionFragmentationStreamOpened(WebSocketSession session, byte[] data, int offset, int count);
        Task OnSessionFragmentationStreamContinued(WebSocketSession session, byte[] data, int offset, int count);
        Task OnSessionFragmentationStreamClosed(WebSocketSession session, byte[] data, int offset, int count);
    }
}

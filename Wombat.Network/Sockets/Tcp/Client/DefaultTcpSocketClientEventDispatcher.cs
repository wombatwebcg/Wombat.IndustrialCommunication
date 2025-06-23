using System;
using System.Threading.Tasks;

namespace Wombat.Network.Sockets
{
    internal class DefaultTcpSocketClientEventDispatcher : ITcpSocketClientEventDispatcher
    {
        private Func<TcpSocketClient, byte[], int, int, Task> _onServerDataReceived;
        private Func<TcpSocketClient, Task> _onServerConnected;
        private Func<TcpSocketClient, Task> _onServerDisconnected;

        public DefaultTcpSocketClientEventDispatcher()
        {
        }

        public DefaultTcpSocketClientEventDispatcher(
            Func<TcpSocketClient, byte[], int, int, Task> onServerDataReceived,
            Func<TcpSocketClient, Task> onServerConnected,
            Func<TcpSocketClient, Task> onServerDisconnected)
            : this()
        {
            _onServerDataReceived = onServerDataReceived;
            _onServerConnected = onServerConnected;
            _onServerDisconnected = onServerDisconnected;
        }

        public async Task OnServerConnected(TcpSocketClient client)
        {
            if (_onServerConnected != null)
                await _onServerConnected(client);
        }

        public async Task OnServerDataReceived(TcpSocketClient client, byte[] data, int offset, int count)
        {
            if (_onServerDataReceived != null)
                await _onServerDataReceived(client, data, offset, count);
        }

        public async Task OnServerDisconnected(TcpSocketClient client)
        {
            if (_onServerDisconnected != null)
                await _onServerDisconnected(client);
        }
    }
}

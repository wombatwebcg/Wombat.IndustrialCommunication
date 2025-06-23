using System;
using System.Net;
using System.Threading.Tasks;

namespace Wombat.Network.Sockets
{
    internal class DefaultUdpSocketClientEventDispatcher : IUdpSocketClientEventDispatcher
    {
        private Func<UdpSocketClient, byte[], int, int, IPEndPoint, Task> _onServerDataReceived;
        private Func<UdpSocketClient, Task> _onServerConnected;
        private Func<UdpSocketClient, Task> _onServerDisconnected;

        public DefaultUdpSocketClientEventDispatcher()
        {
        }

        public DefaultUdpSocketClientEventDispatcher(
            Func<UdpSocketClient, byte[], int, int, IPEndPoint, Task> onServerDataReceived,
            Func<UdpSocketClient, Task> onServerConnected,
            Func<UdpSocketClient, Task> onServerDisconnected)
            : this()
        {
            _onServerDataReceived = onServerDataReceived;
            _onServerConnected = onServerConnected;
            _onServerDisconnected = onServerDisconnected;
        }

        public async Task OnServerConnected(UdpSocketClient client)
        {
            if (_onServerConnected != null)
                await _onServerConnected(client);
        }

        public async Task OnServerDataReceived(UdpSocketClient client, byte[] data, int offset, int count, IPEndPoint remoteEndPoint)
        {
            if (_onServerDataReceived != null)
                await _onServerDataReceived(client, data, offset, count, remoteEndPoint);
        }

        public async Task OnServerDisconnected(UdpSocketClient client)
        {
            if (_onServerDisconnected != null)
                await _onServerDisconnected(client);
        }
    }
} 
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Wombat.Network.WebSockets
{
    internal class InternalWebSocketClientMessageDispatcherImplementation : IWebSocketClientMessageDispatcher
    {
        private Func<WebSocketClient, string, Task> _onServerTextReceived;
        private Func<WebSocketClient, byte[], int, int, Task> _onServerBinaryReceived;
        private Func<WebSocketClient, Task> _onServerConnected;
        private Func<WebSocketClient, Task> _onServerDisconnected;

        private Func<WebSocketClient, byte[], int, int, Task> _onServerFragmentationStreamOpened;
        private Func<WebSocketClient, byte[], int, int, Task> _onServerFragmentationStreamContinued;
        private Func<WebSocketClient, byte[], int, int, Task> _onServerFragmentationStreamClosed;

        public InternalWebSocketClientMessageDispatcherImplementation()
        {
        }

        public InternalWebSocketClientMessageDispatcherImplementation(
            Func<WebSocketClient, string, Task> onServerTextReceived,
            Func<WebSocketClient, byte[], int, int, Task> onServerDataReceived,
            Func<WebSocketClient, Task> onServerConnected,
            Func<WebSocketClient, Task> onServerDisconnected)
            : this()
        {
            _onServerTextReceived = onServerTextReceived;
            _onServerBinaryReceived = onServerDataReceived;
            _onServerConnected = onServerConnected;
            _onServerDisconnected = onServerDisconnected;
        }

        public InternalWebSocketClientMessageDispatcherImplementation(
            Func<WebSocketClient, string, Task> onServerTextReceived,
            Func<WebSocketClient, byte[], int, int, Task> onServerDataReceived,
            Func<WebSocketClient, Task> onServerConnected,
            Func<WebSocketClient, Task> onServerDisconnected,
            Func<WebSocketClient, byte[], int, int, Task> onServerFragmentationStreamOpened,
            Func<WebSocketClient, byte[], int, int, Task> onServerFragmentationStreamContinued,
            Func<WebSocketClient, byte[], int, int, Task> onServerFragmentationStreamClosed)
            : this()
        {
            _onServerTextReceived = onServerTextReceived;
            _onServerBinaryReceived = onServerDataReceived;
            _onServerConnected = onServerConnected;
            _onServerDisconnected = onServerDisconnected;

            _onServerFragmentationStreamOpened = onServerFragmentationStreamOpened;
            _onServerFragmentationStreamContinued = onServerFragmentationStreamContinued;
            _onServerFragmentationStreamClosed = onServerFragmentationStreamClosed;
        }

        public async Task OnServerConnected(WebSocketClient client)
        {
            if (_onServerConnected != null)
                await _onServerConnected(client);
        }

        public async Task OnServerTextReceived(WebSocketClient client, string text)
        {
            if (_onServerTextReceived != null)
                await _onServerTextReceived(client, text);
        }

        public async Task OnServerBinaryReceived(WebSocketClient client, byte[] data, int offset, int count)
        {
            if (_onServerBinaryReceived != null)
                await _onServerBinaryReceived(client, data, offset, count);
        }

        public async Task OnServerDisconnected(WebSocketClient client)
        {
            if (_onServerDisconnected != null)
                await _onServerDisconnected(client);
        }

        public async Task OnServerFragmentationStreamOpened(WebSocketClient client, byte[] data, int offset, int count)
        {
            if (_onServerFragmentationStreamOpened != null)
                await _onServerFragmentationStreamOpened(client, data, offset, count);
        }

        public async Task OnServerFragmentationStreamContinued(WebSocketClient client, byte[] data, int offset, int count)
        {
            if (_onServerFragmentationStreamContinued != null)
                await _onServerFragmentationStreamContinued(client, data, offset, count);
        }

        public async Task OnServerFragmentationStreamClosed(WebSocketClient client, byte[] data, int offset, int count)
        {
            if (_onServerFragmentationStreamClosed != null)
                await _onServerFragmentationStreamClosed(client, data, offset, count);
        }
    }
}

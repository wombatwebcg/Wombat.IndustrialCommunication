using System;
using System.Net;
using System.Threading.Tasks;

namespace Wombat.Network.Sockets
{
    internal class DefaultUdpSocketServerEventDispatcher : IUdpSocketServerEventDispatcher
    {
        private Func<UdpSocketSession, Task> _onSessionStarted;
        private Func<UdpSocketSession, byte[], int, int, Task> _onSessionDataReceived;
        private Func<UdpSocketSession, Task> _onSessionClosed;

        public DefaultUdpSocketServerEventDispatcher()
        {
        }

        public DefaultUdpSocketServerEventDispatcher(
            Func<UdpSocketSession, byte[], int, int, Task> onSessionDataReceived,
            Func<UdpSocketSession, Task> onSessionStarted = null,
            Func<UdpSocketSession, Task> onSessionClosed = null)
            : this()
        {
            _onSessionDataReceived = onSessionDataReceived;
            _onSessionStarted = onSessionStarted;
            _onSessionClosed = onSessionClosed;
        }

        public async Task OnSessionStarted(UdpSocketSession session)
        {
            if (_onSessionStarted != null)
                await _onSessionStarted(session);
        }

        public async Task OnSessionDataReceived(UdpSocketSession session, byte[] data, int offset, int count)
        {
            if (_onSessionDataReceived != null)
                await _onSessionDataReceived(session, data, offset, count);
        }

        public async Task OnSessionClosed(UdpSocketSession session)
        {
            if (_onSessionClosed != null)
                await _onSessionClosed(session);
        }
    }
} 
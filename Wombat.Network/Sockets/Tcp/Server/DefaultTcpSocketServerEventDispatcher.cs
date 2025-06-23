using System;
using System.Threading.Tasks;

namespace Wombat.Network.Sockets
{
    internal class DefaultTcpSocketServerEventDispatcher : ITcpSocketServerEventDispatcher
    {
        private Func<TcpSocketSession, byte[], int, int, Task> _onSessionDataReceived;
        private Func<TcpSocketSession, Task> _onSessionStarted;
        private Func<TcpSocketSession, Task> _onSessionClosed;

        public DefaultTcpSocketServerEventDispatcher()
        {
        }

        public DefaultTcpSocketServerEventDispatcher(
            Func<TcpSocketSession, byte[], int, int, Task> onSessionDataReceived,
            Func<TcpSocketSession, Task> onSessionStarted,
            Func<TcpSocketSession, Task> onSessionClosed)
            : this()
        {
            _onSessionDataReceived = onSessionDataReceived;
            _onSessionStarted = onSessionStarted;
            _onSessionClosed = onSessionClosed;
        }

        public async Task OnSessionStarted(TcpSocketSession session)
        {
            if (_onSessionStarted != null)
                await _onSessionStarted(session);
        }

        public async Task OnSessionDataReceived(TcpSocketSession session, byte[] data, int offset, int count)
        {
            if (_onSessionDataReceived != null)
                await _onSessionDataReceived(session, data, offset, count);
        }

        public async Task OnSessionClosed(TcpSocketSession session)
        {
            if (_onSessionClosed != null)
                await _onSessionClosed(session);
        }
    }
}

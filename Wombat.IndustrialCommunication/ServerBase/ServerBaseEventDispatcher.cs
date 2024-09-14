using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Wombat.Network.Sockets;

namespace Wombat.IndustrialCommunication
{
    public class ServerBaseEventDispatcher : ITcpSocketServerEventDispatcher
    {
        public virtual async Task OnSessionStarted(TcpSocketSession session)
        {
            Console.WriteLine(string.Format("TCP session {0} has connected {1}.", session.RemoteEndPoint, session));
            await Task.CompletedTask;
        }
        
        public virtual async Task OnSessionDataReceived(TcpSocketSession session, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            Console.Write($"Client : {session.RemoteEndPoint} --> ");
            if (count < 1024 * 1024 * 1)
            {
                Console.WriteLine(text+ DateTime.Now.ToString("HH:mm:ss:fff"));
            }
            else
            {
                Console.WriteLine($"{count} Bytes{DateTime.Now.ToString("HH:mm:ss:fff")}");
            }
            await session.SendAsync(Encoding.UTF8.GetBytes(text));
        }

        public virtual async Task OnSessionClosed(TcpSocketSession session)
        {
            Console.WriteLine(string.Format("TCP session {0} has disconnected.", session));
            await Task.CompletedTask;
        }
    }
}

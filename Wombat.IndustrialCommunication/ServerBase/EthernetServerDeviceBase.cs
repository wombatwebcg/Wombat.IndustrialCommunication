using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wombat.Network;
using Wombat.Network.Sockets;


namespace Wombat.IndustrialCommunication
{
    public abstract class EthernetServerDeviceBase : DeviceServer, IEthernetServer
    {
        protected internal TcpSocketServer _tcpSocketServer;
        internal AsyncLock _lock = new AsyncLock();

        public IPEndPoint IpEndPoint{ get; set; }

        public EthernetServerDeviceBase()
        {


        }


        internal void CreatetServer(ServerBaseEventDispatcher serverBaseEventDispatcher)
        {
            var tcpSocketServerConfiguration = new TcpSocketServerConfiguration();
            var rawBufferFrameBuilder = new RawBufferFrameBuilder();
            _tcpSocketServer = new TcpSocketServer(IpEndPoint, serverBaseEventDispatcher, tcpSocketServerConfiguration);

        }


    }
}

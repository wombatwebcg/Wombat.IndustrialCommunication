using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusTcpServer: ModbusServerEthernetBase
    {
        public ModbusTcpServer():base()
        {

        }

        public ModbusTcpServer(IPEndPoint ipAndPoint) : this()
        {
            IpEndPoint = ipAndPoint;
        }

        public ModbusTcpServer(string ip, int port) : this()
        {
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            IpEndPoint = new IPEndPoint(address, port);
        }


    }
}

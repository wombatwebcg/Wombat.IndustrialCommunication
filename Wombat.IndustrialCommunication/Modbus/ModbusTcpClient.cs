using Wombat.Infrastructure;
using Wombat.IndustrialCommunication.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// ModbusTcp协议客户端
    /// </summary>
    public class ModbusTcpClient : ModbusSocketBase
    {
        public ModbusTcpClient() : base()
        {
        }

        public ModbusTcpClient(IPEndPoint ipAndPoint) : base(ipAndPoint)
        {
        }

        public ModbusTcpClient(string ip, int port) : base(ip, port)
        {
        }


    }
}


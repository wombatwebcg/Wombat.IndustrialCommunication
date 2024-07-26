
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// ModbusTcp协议客户端
    /// </summary>
    public class ModbusTcpClient : ModbusEthernetBase
    {
        public ModbusTcpClient() : base()
        {
            DataFormat = Extensions.DataTypeExtensions.EndianFormat.ABCD;

        }

        public ModbusTcpClient(IPEndPoint ipAndPoint) : base(ipAndPoint)
        {
            DataFormat = Extensions.DataTypeExtensions.EndianFormat.ABCD;
        }

        public ModbusTcpClient(string ip, int port) : base(ip, port)
        {
            DataFormat = Extensions.DataTypeExtensions.EndianFormat.ABCD;

        }

        public override string Version => "ModbusTcpClient";

    }
}


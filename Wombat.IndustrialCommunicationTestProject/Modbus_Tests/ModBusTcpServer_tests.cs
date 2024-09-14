using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.Modbus
{
    public class ModBusTcpServer_tests
    {
        ModbusTcpServer server;
        byte stationNumber = 1;//站号
        public ModBusTcpServer_tests()
        {
            var ip = IPAddress.Parse("127.0.0.1");
            server = new ModbusTcpServer(new IPEndPoint(ip, 502));
        }

        public bool ShortToBit(int value, int index)
        {
            var binaryArray = DataTypeExtensions.IntToBinaryArray(value, 16);
            var length = binaryArray.Length - 16;
            return binaryArray[length + index].ToString() == "1";
        }

        [Fact]
        public async Task 服务器开始()
        {
            server.Listen();
            while(true)
            {
                Thread.Sleep(100);
            }
        }

    }
}

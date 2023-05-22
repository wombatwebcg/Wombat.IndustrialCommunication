using Wombat.ObjectConversionExtention;
using Wombat.IndustrialCommunication.Models;
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

        internal override ValueTask<OperationResult<byte[]>> InterpretAndExtractMessageDataAsync(byte[] command)
        {
            throw new NotImplementedException();
        }

        internal override ValueTask<OperationResult<byte[]>> GetMessageContentAsync(byte[] command)
        {
            throw new NotImplementedException();
        }

        internal override Task<OperationResult> DoConnectAsync()
        {
            throw new NotImplementedException();
        }

        internal override Task<OperationResult> DoDisconnectAsync()
        {
            throw new NotImplementedException();
        }

    }
}


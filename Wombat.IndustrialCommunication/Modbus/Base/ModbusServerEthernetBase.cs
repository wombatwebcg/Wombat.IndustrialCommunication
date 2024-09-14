using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus.Data;
using Wombat.Network.Sockets;


namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Socket基类
    /// </summary>
    public  class ModbusServerEthernetBase : EthernetServerDeviceBase
    {
        public DataStore DataStore=>_modbusServerEventDispatcher.DataStore;
        ModbusServerEventDispatcher _modbusServerEventDispatcher;
        public ModbusServerEthernetBase():base()
        {

            _modbusServerEventDispatcher = new ModbusServerEventDispatcher();
        }


        public ModbusServerEthernetBase(IPEndPoint ipAndPoint) : this()
        {
            IpEndPoint = ipAndPoint;

        }

        public ModbusServerEthernetBase(string ip, int port) : this()
        {
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            IpEndPoint = new IPEndPoint(address, port);
        }

        public override bool IsListening => _tcpSocketServer?.IsListening??false;

        public override string Version => "ModbusServerEthernetBase";

        internal override OperationResult DoListen()
        {
            CreatetServer(_modbusServerEventDispatcher);
            _tcpSocketServer.Listen();
            return _tcpSocketServer.IsListening ? OperationResult.CreateSuccessResult() : OperationResult.CreateFailedResult();
        }

        internal override OperationResult DoShutdown()
        {
            _tcpSocketServer.Shutdown();
            return OperationResult.CreateSuccessResult();
        }

        internal override OperationResult<byte[]> Read(string address, int length, bool isBit = false)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult<byte[]>();
                //if (ModbusAddressParser.TryParseModbusHeader(address, out var modbusHeader))
                //{
                //    switch (modbusHeader.FunctionCode)
                //    {
                //        case 0x01:  // 读线圈 (Read Coils)
                //            return DataStore.CoilDiscretes[modbusHeader.Address]

                //        case 0x02:  // 读离散输入 (Read Discrete Inputs)
                //            return HandleReadDiscreteInputsRequest(request, transactionId, protocolId, unitId);

                //        case 0x03:  // 读保持寄存器 (Read Holding Registers)
                //            return HandleReadHoldingRegistersRequest(request, transactionId, protocolId, unitId);

                //        case 0x04:  // 读输入寄存器 (Read Input Registers)
                //            return HandleReadInputRegistersRequest(request, transactionId, protocolId, unitId);

                //        case 0x05:  // 写单个线圈 (Write Single Coil)
                //            return HandleWriteSingleCoilRequest(request, transactionId, protocolId, unitId);

                //        case 0x06:  // 写单个寄存器 (Write Single Register)
                //            return HandleWriteSingleRegisterRequest(request, transactionId, protocolId, unitId);

                //        case 0x0F:  // 写多个线圈 (Write Multiple Coils)
                //            return HandleWriteMultipleCoilsRequest(request, transactionId, protocolId, unitId);

                //        case 0x10:  // 写多个寄存器 (Write Multiple Registers)
                //            return HandleWriteMultipleRegistersRequest(request, transactionId, protocolId, unitId);

                //        default:
                //            throw new InvalidOperationException("Unsupported function code.");
                //    }
                //}
                //else
                //{
                //    result = OperationResult.CreateFailedResult<byte[]>("modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址");
                //}
                return result.Complete();
            }
        }

        internal override ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            throw new NotImplementedException();
        }

        internal override OperationResult Write(string address, byte[] data, bool isBit = false)
        {
            throw new NotImplementedException();
        }

        internal override Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            throw new NotImplementedException();
        }
    }
}

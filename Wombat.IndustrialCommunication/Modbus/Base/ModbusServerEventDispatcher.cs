using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.Network.Sockets;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus.Data;
using System.Linq;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusServerEventDispatcher : ServerBaseEventDispatcher
    {

        public DataStore DataStore => _dataStore;

        DataStore _dataStore;
        public ModbusServerEventDispatcher()
        {
            _dataStore = DataStoreFactory.CreateDefaultDataStore();

        }

        public override async Task OnSessionStarted(TcpSocketSession session)
        {
            await Task.CompletedTask;
        }
        
        public override async Task OnSessionDataReceived(TcpSocketSession session, byte[] data, int offset, int count)
        {
            byte[] arry = new byte[count];
            Array.Copy(data, offset, arry, 0, count);
            await session.SendAsync(HandleRequest(arry).ToByteArray());
        }

        public override async Task OnSessionClosed(TcpSocketSession session)
        {
            await Task.CompletedTask;
        }



        // 处理接收到的请求报文并返回响应报文
        private  ModbusTcpResponseGenerator HandleRequest(byte[] request)
        {
            // 解析事务ID (Transaction ID)
            ushort transactionId = (ushort)((request[0] << 8) | request[1]);

            // 解析协议ID (Protocol ID) - 应该是0x0000
            ushort protocolId = (ushort)((request[2] << 8) | request[3]);

            // 解析长度 (Length)
            ushort length = (ushort)((request[4] << 8) | request[5]);

            // 解析单元ID (Unit ID)
            byte Station = request[6];

            // 解析功能码 (Function Code)
            byte functionCode = request[7];

            // 根据功能码选择不同的处理方式
            switch (functionCode)
            {
                case 0x01:  // 读线圈 (Read Coils)
                    return HandleReadCoilsRequest(request, transactionId, protocolId, Station);

                case 0x02:  // 读离散输入 (Read Discrete Inputs)
                    return HandleReadDiscreteInputsRequest(request, transactionId, protocolId, Station);

                case 0x03:  // 读保持寄存器 (Read Holding Registers)
                    return HandleReadHoldingRegistersRequest(request, transactionId, protocolId, Station);

                case 0x04:  // 读输入寄存器 (Read Input Registers)
                    return HandleReadInputRegistersRequest(request, transactionId, protocolId, Station);

                case 0x05:  // 写单个线圈 (Write Single Coil)
                    return HandleWriteSingleCoilRequest(request, transactionId, protocolId, Station);

                case 0x06:  // 写单个寄存器 (Write Single Register)
                    return HandleWriteSingleRegisterRequest(request, transactionId, protocolId, Station);

                case 0x0F:  // 写多个线圈 (Write Multiple Coils)
                    return HandleWriteMultipleCoilsRequest(request, transactionId, protocolId, Station);

                case 0x10:  // 写多个寄存器 (Write Multiple Registers)
                    return HandleWriteMultipleRegistersRequest(request, transactionId, protocolId, Station);

                default:
                    throw new InvalidOperationException("Unsupported function code.");
            }
        }

        // 处理读取线圈的请求
        private  ModbusTcpResponseGenerator HandleReadCoilsRequest(byte[] request, ushort transactionId, ushort protocolId, byte Station)
        {
            // 解析起始地址和读取数量
            ushort address = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            var values = _dataStore.CoilDiscretes.Slice(address, quantity).ToArray();
            return ModbusTcpPacketGenerator.GenerateReadCoilsResponse(transactionId, protocolId, Station,
                _dataStore.CoilDiscretes.Slice(address, quantity).ToArray());
        }

        // 处理读取保持寄存器的请求
        private  ModbusTcpResponseGenerator HandleReadHoldingRegistersRequest(byte[] request, ushort transactionId, ushort protocolId, byte Station)
        {
            // 解析起始地址和读取数量
            ushort address = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            // 生成读取保持寄存器的响应报文
            return ModbusTcpPacketGenerator.GenerateReadHoldingRegistersResponse(transactionId, protocolId, Station,
                _dataStore.HoldingRegisters.Slice(address, quantity).ToArray().Select(i => (ushort)i).ToArray());
        }

        // 处理读取离散输入的请求
        private  ModbusTcpResponseGenerator HandleReadDiscreteInputsRequest(byte[] request, ushort transactionId, ushort protocolId, byte Station)
        {
            // 解析起始地址和读取数量
            ushort address = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            // 生成读取离散输入的响应报文
            return ModbusTcpPacketGenerator.GenerateReadDiscreteInputsResponse(transactionId, protocolId, Station,
                _dataStore.InputDiscretes.Slice(address, quantity).ToArray());
        }

        // 处理读取输入寄存器的请求
        private  ModbusTcpResponseGenerator HandleReadInputRegistersRequest(byte[] request, ushort transactionId, ushort protocolId, byte Station)
        {
            // 解析起始地址和读取数量
            ushort address = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            var content = _dataStore.InputRegisters.Slice(address, quantity).ToArray();
            List<ushort> registers = new List<ushort>();
            for (int i = 0; i < content.Length; i++) {
                registers.Add(ushort.Parse(content[i].ToString()));
            }
            // 生成读取输入寄存器的响应报文
            return ModbusTcpPacketGenerator.GenerateReadInputRegistersResponse(transactionId, protocolId, Station
                , registers.ToArray());
        }


        // 处理写单个线圈的请求
        private  ModbusTcpResponseGenerator HandleWriteSingleCoilRequest(byte[] request, ushort transactionId, ushort protocolId, byte Station)
        {
            // 解析线圈地址和状态
            ushort address = (ushort)((request[8] << 8) | request[9]);

            _dataStore.CoilDiscretes[address] = request[10] == 0xFF;
            return ModbusTcpPacketGenerator.GenerateWriteSingleCoilResponse(transactionId, protocolId, Station, address, 
                _dataStore.CoilDiscretes[address]);
        }

        // 处理写单个寄存器的请求
        private  ModbusTcpResponseGenerator HandleWriteSingleRegisterRequest(byte[] request, ushort transactionId, ushort protocolId, byte Station)
        {
            // 解析寄存器地址和值
            ushort address = (ushort)((request[8] << 8) | request[9]);
            ushort registerValue = (ushort)((request[10] << 8) | request[11]);

            _dataStore.HoldingRegisters[address] = registerValue;

            // 生成写单个寄存器的响应报文
            return ModbusTcpPacketGenerator.GenerateWriteSingleRegisterResponse(transactionId, protocolId, Station, address,
                (ushort)_dataStore.HoldingRegisters[address]);
        }

        // 处理写多个线圈的请求
        private  ModbusTcpResponseGenerator HandleWriteMultipleCoilsRequest(byte[] request, ushort transactionId, ushort protocolId, byte Station)
        {
            // 解析起始地址和线圈数量
            ushort address = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            // 解析线圈状态
            bool[] coilValues = new bool[quantity];
            for (int i = 0; i < quantity; i++)
            {
                int byteIndex = 13 + (i / 8);
                int bitIndex = i % 8;
                coilValues[i] = (request[byteIndex] & (1 << bitIndex)) != 0;
                _dataStore.CoilDiscretes[i + address] = coilValues[i];
            }

            // 生成写多个线圈的响应报文
            return ModbusTcpPacketGenerator.GenerateWriteMultipleCoilsResponse(transactionId, protocolId, Station, address, quantity);
        }

        // 处理写多个寄存器的请求
        private  ModbusTcpResponseGenerator HandleWriteMultipleRegistersRequest(byte[] request, ushort transactionId, ushort protocolId, byte Station)
        {
            // 解析起始地址和寄存器数量
            ushort address = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            // 数据部分的字节数
            byte byteCount = request[12];

            // 解析寄存器的值
            ushort[] registerValues = new ushort[quantity];
            for (int i = 0; i < quantity; i++)
            {
                int registerIndex = 13 + (i * 2);
                registerValues[i] = (ushort)((request[registerIndex] << 8) | request[registerIndex + 1]);
                _dataStore.HoldingRegisters[i + address] = registerValues[i];

            }

            // 生成写多个寄存器的响应报文
            return ModbusTcpPacketGenerator.GenerateWriteMultipleRegistersResponse(transactionId, protocolId, Station, address, quantity);
        }

    }
}

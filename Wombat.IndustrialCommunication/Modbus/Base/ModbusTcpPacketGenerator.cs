using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusTcpPacketGenerator
    {
        // 生成线圈类（0x01 - Read Coils）的响应报文
        public static ModbusTcpResponseGenerator GenerateReadCoilsResponse(ushort transactionId, ushort protocolId, byte Station, bool[] coilStatus)
        {
            byte functionCode = 0x01;
            int coilCount = coilStatus.Length;
            byte byteCount = (byte)((coilCount + 7) / 8);  // 8个线圈一个字节
            byte[] data = new byte[1 + byteCount];  // 第一个字节是字节数量

            // 填充数据
            data[0] = byteCount;
            for (int i = 0; i < coilCount; i++)
            {
                if (coilStatus[i])
                {
                    data[1 + (i / 8)] |= (byte)(1 << (i % 8));  // 设置对应的位
                }
            }

            return new ModbusTcpResponseGenerator(transactionId, protocolId, Station, functionCode, data);
        }

        // 生成离散输入类（0x02 - Read Discrete Inputs）的响应报文
        public static ModbusTcpResponseGenerator GenerateReadDiscreteInputsResponse(ushort transactionId, ushort protocolId, byte Station, bool[] inputStatus)
        {
            byte functionCode = 0x02;
            int inputCount = inputStatus.Length;
            byte byteCount = (byte)((inputCount + 7) / 8);  // 8个输入一个字节
            byte[] data = new byte[1 + byteCount];  // 第一个字节是字节数量

            // 填充数据
            data[0] = byteCount;
            for (int i = 0; i < inputCount; i++)
            {
                if (inputStatus[i])
                {
                    data[1 + (i / 8)] |= (byte)(1 << (i % 8));  // 设置对应的位
                }
            }

            return new ModbusTcpResponseGenerator(transactionId, protocolId, Station, functionCode, data);
        }

        // 生成保持寄存器类（0x03 - Read Holding Registers）的响应报文
        public static ModbusTcpResponseGenerator GenerateReadHoldingRegistersResponse(ushort transactionId, ushort protocolId, byte Station, ushort[] registerValues)
        {
            byte functionCode = 0x03;
            int registerCount = registerValues.Length;
            byte byteCount = (byte)(registerCount * 2);
            byte[] data = new byte[1 + byteCount];  // 第一个字节是字节数量

            // 填充数据
            data[0] = byteCount;
            for (int i = 0; i < registerCount; i++)
            {
                data[1 + (i * 2)] = (byte)(registerValues[i] >> 8);  // 高字节
                data[2 + (i * 2)] = (byte)(registerValues[i] & 0xFF);  // 低字节
            }

            return new ModbusTcpResponseGenerator(transactionId, protocolId, Station, functionCode, data);
        }

        // 生成输入寄存器类（0x04 - Read Input Registers）的响应报文
        public static ModbusTcpResponseGenerator GenerateReadInputRegistersResponse(ushort transactionId, ushort protocolId, byte Station, ushort[] registerValues)
        {
            byte functionCode = 0x04;
            int registerCount = registerValues.Length;
            byte byteCount = (byte)(registerCount * 2);
            byte[] data = new byte[1 + byteCount];  // 第一个字节是字节数量

            // 填充数据
            data[0] = byteCount;
            for (int i = 0; i < registerCount; i++)
            {
                data[1 + (i * 2)] = (byte)(registerValues[i] >> 8);  // 高字节
                data[2 + (i * 2)] = (byte)(registerValues[i] & 0xFF);  // 低字节
            }

            return new ModbusTcpResponseGenerator(transactionId, protocolId, Station, functionCode, data);
        }

        // 生成写单个线圈（0x05 - Write Single Coil）的响应报文
        public static ModbusTcpResponseGenerator GenerateWriteSingleCoilResponse(ushort transactionId, ushort protocolId, byte Station, ushort address, bool coilValue)
        {
            byte functionCode = 0x05;
            byte[] data = new byte[4];

            // 地址 (2 bytes)
            data[0] = (byte)(address >> 8);
            data[1] = (byte)(address & 0xFF);

            // 线圈值 (2 bytes)
            data[2] = coilValue ? (byte)0xFF : (byte)0x00; // FF 00 for ON, 00 00 for OFF
            data[3] = 0x00;

            return new ModbusTcpResponseGenerator(transactionId, protocolId, Station, functionCode, data);
        }

        // 生成写多个线圈（0x0F - Write Multiple Coils）的响应报文
        public static ModbusTcpResponseGenerator GenerateWriteMultipleCoilsResponse(ushort transactionId, ushort protocolId, byte Station, ushort address, ushort quantity)
        {
            byte functionCode = 0x0F;
            byte[] data = new byte[4];

            // 地址 (2 bytes)
            data[0] = (byte)(address >> 8);
            data[1] = (byte)(address & 0xFF);

            // 线圈数量 (2 bytes)
            data[2] = (byte)(quantity >> 8);
            data[3] = (byte)(quantity & 0xFF);

            return new ModbusTcpResponseGenerator(transactionId, protocolId, Station, functionCode, data);
        }

        // 生成写单个寄存器（0x06 - Write Single Register）的响应报文
        public static ModbusTcpResponseGenerator GenerateWriteSingleRegisterResponse(ushort transactionId, ushort protocolId, byte Station, ushort address, ushort registerValue)
        {
            byte functionCode = 0x06;
            byte[] data = new byte[4];

            // 地址 (2 bytes)
            data[0] = (byte)(address >> 8);
            data[1] = (byte)(address & 0xFF);

            // 寄存器值 (2 bytes)
            data[2] = (byte)(registerValue >> 8);
            data[3] = (byte)(registerValue & 0xFF);

            return new ModbusTcpResponseGenerator(transactionId, protocolId, Station, functionCode, data);
        }

        // 生成写多个寄存器（0x10 - Write Multiple Registers）的响应报文
        public static ModbusTcpResponseGenerator GenerateWriteMultipleRegistersResponse(ushort transactionId, ushort protocolId, byte Station, ushort address, ushort quantity)
        {
            byte functionCode = 0x10;
            byte[] data = new byte[4];

            // 地址 (2 bytes)
            data[0] = (byte)(address >> 8);
            data[1] = (byte)(address & 0xFF);

            // 寄存器数量 (2 bytes)
            data[2] = (byte)(quantity >> 8);
            data[3] = (byte)(quantity & 0xFF);

            return new ModbusTcpResponseGenerator(transactionId, protocolId, Station, functionCode, data);
        }
    }

    
}

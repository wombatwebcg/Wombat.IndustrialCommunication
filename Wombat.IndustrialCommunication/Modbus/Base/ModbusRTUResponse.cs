using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{

    public class ModbusRTUResponse:IDeviceReadWriteMessage
    {
        public byte Station { get; private set; }
        public byte FunctionCode { get; private set; }
        public byte[] Data { get; private set; }
        public int RegisterCount { get ; set ; }
        public string RegisterAddress { get; set; }
        public int ProtocolResponseLength { get; set; }

        public byte[] ProtocolMessageFrame { get; set; }

        public int ProtocolDataNumber => throw new NotImplementedException();

        public ModbusRTUResponse(byte[] rawData)
        {
            if (rawData == null || rawData.Length < 4)  // 最小响应长度：站号1 + 功能码1 + CRC2
                throw new ArgumentException("Invalid Modbus RTU response frame");


            ProtocolMessageFrame = rawData;
            Station = rawData[0];
            FunctionCode = rawData[1];


            // 根据功能码解析数据部分
            int dataStartIndex = 2;
            int dataLength = rawData.Length - 2 - 2;  // 排除站号、功能码和CRC

            switch (FunctionCode)
            {
                case 0x01: // 读取线圈状态
                case 0x02: // 读取离散输入状态
                    ParseReadBitsResponse(rawData, dataStartIndex, dataLength);
                    break;

                case 0x03: // 读取保持寄存器
                case 0x04: // 读取输入寄存器
                    ParseReadRegistersResponse(rawData, dataStartIndex, dataLength);
                    break;

                case 0x05: // 写单个线圈
                case 0x06: // 写单个寄存器
                    ParseWriteSingleResponse(rawData, dataStartIndex, dataLength);
                    break;

                case 0x0F: // 写多个线圈
                case 0x10: // 写多个寄存器
                    ParseWriteMultipleResponse(rawData, dataStartIndex, dataLength);
                    break;

                default:
                    break;
            }
        }

        private void ParseReadBitsResponse(byte[] rawData, int startIndex, int dataLength)
        {
            // 结构：字节数(1) + 位数据(n)
            int byteCount = rawData[startIndex];
            Data = new byte[byteCount];
            Array.Copy(rawData, startIndex + 1, Data, 0, byteCount);
        }

        private void ParseReadRegistersResponse(byte[] rawData, int startIndex, int dataLength)
        {
            // 结构：字节数(1) + 寄存器值(2*n)
            int byteCount = rawData[startIndex];
            Data = new byte[byteCount];
            Array.Copy(rawData, startIndex + 1, Data, 0, byteCount);
        }

        private void ParseWriteSingleResponse(byte[] rawData, int startIndex, int dataLength)
        {
            // 结构：地址(2) + 数据(2)
            Data = new byte[4];
            Array.Copy(rawData, startIndex, Data, 0, 4);
        }

        private void ParseWriteMultipleResponse(byte[] rawData, int startIndex, int dataLength)
        {
            // 结构：地址(2) + 数量(2)
            Data = new byte[4];
            Array.Copy(rawData, startIndex, Data, 0, 4);
        }

        // 示例：将线圈状态转换为布尔数组
        public bool[] GetCoilStatuses()
        {
            if (FunctionCode != 0x01 && FunctionCode != 0x02)
                throw new InvalidOperationException("Not a bit-read response");

            bool[] statuses = new bool[Data.Length * 8];
            for (int i = 0; i < Data.Length; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    statuses[i * 8 + j] = ((Data[i] >> j) & 0x01) == 0x01;
                }
            }
            return statuses;
        }

        // 示例：将寄存器值转换为ushort数组
        public ushort[] GetRegisterValues()
        {
            if (FunctionCode != 0x03 && FunctionCode != 0x04)
                throw new InvalidOperationException("Not a register-read response");

            ushort[] values = new ushort[Data.Length / 2];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (ushort)((Data[i * 2] << 8) | Data[i * 2 + 1]);
            }
            return values;
        }

        public byte[] ToByteArray() => ProtocolMessageFrame;

        public void Initialize(byte[] frame)
        {
            throw new NotImplementedException();
        }
    }

    // CRC16校验辅助类（需与ModbusRTURequest中使用的一致）
}

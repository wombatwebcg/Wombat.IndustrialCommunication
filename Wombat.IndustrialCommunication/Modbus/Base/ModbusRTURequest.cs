using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusRTURequest : IDeviceReadWriteMessage
    {
        public byte Station { get; private set; }
        public byte FunctionCode { get; private set; }
        public int RegisterCount { get; set; }
        public string RegisterAddress { get; set; }
        public byte[] ProtocolMessageFrame { get; set; }
        public int ProtocolDataNumber { get; set; }
        public int ProtocolResponseLength { get; set; } // 响应长度属性

        public ModbusRTURequest(byte station, byte functionCode, ushort address, int length, byte[] data = null)
        {
            Station = station;
            FunctionCode = functionCode;
            RegisterCount = length; // 假设RegisterCount是请求的长度
            int dataBytes = 0;
            byte[] frame = null;
            switch (functionCode)
            {
                case 0x01: // 读取线圈
                case 0x02: // 读取离散输入
                    frame = new byte[4];
                    frame[0] = (byte)(address >> 8);
                    frame[1] = (byte)(address & 0xFF);
                    frame[2] = (byte)(length >> 8);
                    frame[3] = (byte)(length & 0xFF);
                    // 响应长度 = 站号(1) + 功能码(1) + 字节数(1) + 数据(n) + CRC(2)
                    dataBytes = (length + 7) / 8;
                    ProtocolResponseLength = 1 + 1 + 1 + dataBytes + 2;
                    break;

                case 0x03: // 读取保持寄存器
                case 0x04: // 读取输入寄存器
                    frame = new byte[4];
                    frame[0] = (byte)(address >> 8);
                    frame[1] = (byte)(address & 0xFF);
                    frame[2] = (byte)(length >> 8);    // 直接使用寄存器数量
                    frame[3] = (byte)(length & 0xFF);
                    // 响应数据字节数 = 寄存器数量 × 2
                    dataBytes = length * 2;
                    ProtocolResponseLength = 1 + 1 + 1 + dataBytes + 2;
                    break;

                case 0x05: // 写单个线圈
                    frame = new byte[4];
                    frame[0] = (byte)(address >> 8);
                    frame[1] = (byte)(address & 0xFF);
                    frame[2] = (byte)(data[0] == 1 ? 0xFF : 0x00);
                    frame[3] = 00;
                    // 响应长度 = 站号(1) + 功能码(1) + 地址(2) + 数据(2) + CRC(2)
                    ProtocolResponseLength = 1 + 1 + 2 + 2 + 2;
                    break;

                case 0x06: // 写单个寄存器
                    frame = new byte[4];
                    frame[0] = (byte)(address >> 8);
                    frame[1] = (byte)(address & 0xFF);
                    frame[2] = data[0];
                    frame[3] = data[1];
                    ProtocolResponseLength = 1 + 1 + 2 + 2 + 2; // 同上，总长度8
                    break;

                case 0x0F: // 写多个线圈
                    frame = new byte[5 + ((length + 7) / 8)];
                    frame[0] = (byte)(address >> 8);
                    frame[1] = (byte)(address & 0xFF);
                    frame[2] = (byte)(length >> 8);
                    frame[3] = (byte)(length & 0xFF);
                    frame[4] = (byte)((length + 7) / 8);
                    Array.Copy(data, 0, frame, 5, data.Length);
                    // 响应长度 = 站号(1) + 功能码(1) + 地址(2) + 数量(2) + CRC(2)
                    ProtocolResponseLength = 1 + 1 + 2 + 2 + 2;
                    break;

                case 0x10: // 写多个寄存器
                    frame = new byte[5 + length * 2];
                    frame[0] = (byte)(address >> 8);
                    frame[1] = (byte)(address & 0xFF);
                    frame[2] = (byte)(length >> 8);
                    frame[3] = (byte)(length & 0xFF);
                    frame[4] = (byte)(length * 2);
                    Array.Copy(data, 0, frame, 5, data.Length);
                    ProtocolResponseLength = 1 + 1 + 2 + 2 + 2; // 总长度8
                    break;
            }

            // 构造完整帧并计算CRC
            byte[] completeFrame = new byte[2 + frame.Length];
            completeFrame[0] = Station;
            completeFrame[1] = FunctionCode;
            Array.Copy(frame, 0, completeFrame, 2, frame.Length);

            byte[] crc = CRC16Helper.GetCRC16(completeFrame);
            ProtocolMessageFrame = new byte[completeFrame.Length + 2];
            Array.Copy(completeFrame, 0, ProtocolMessageFrame, 0, completeFrame.Length);
            Array.Copy(crc, 0, ProtocolMessageFrame, completeFrame.Length, 2);
        }

        public void Initialize(byte[] frame)
        {
            throw new NotImplementedException();
        }
    }
}

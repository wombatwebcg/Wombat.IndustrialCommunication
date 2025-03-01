using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusRTUResponseGenerator
    {
        public byte Station { get; private set; }
        public byte FunctionCode { get; private set; }
        public byte[] Data { get; private set; }
        public byte[] ResponseFrame { get; private set; }

        public ModbusRTUResponseGenerator(byte Station, byte functionCode, byte[] data)
        {
            Station = Station;
            FunctionCode = functionCode;
            Data = data;

            // 根据 Modbus RTU 协议构造完整的响应帧
            byte[] frame = new byte[2 + data.Length]; // Unit ID + Function Code + Data
            frame[0] = Station;
            frame[1] = FunctionCode;
            Array.Copy(Data, 0, frame, 2, Data.Length);

            // 计算 CRC 校验
            byte[] crc = CalculateCRC(frame);
            // 将 CRC 加到响应帧的结尾
            ResponseFrame = new byte[frame.Length + 2];
            Array.Copy(frame, 0, ResponseFrame, 0, frame.Length);
            Array.Copy(crc, 0, ResponseFrame, frame.Length, 2);
        }

        // CRC 校验计算方法
        private byte[] CalculateCRC(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 8; i > 0; i--)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            // 返回 CRC 校验值（低字节在前，高字节在后）
            return new byte[] { (byte)(crc & 0xFF), (byte)(crc >> 8) };
        }

        // 将 Modbus RTU 响应转化为字节数组
        public byte[] ToByteArray()
        {
            return ResponseFrame;
        }
    }
}

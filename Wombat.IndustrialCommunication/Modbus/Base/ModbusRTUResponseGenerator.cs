using System;
using System.Collections.Generic;
using System.Text;
using Wombat.IndustrialCommunication.Helpers;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusRTUResponseGenerator
    {
        public byte Station { get; private set; }
        public byte FunctionCode { get; private set; }
        public byte[] Data { get; private set; }
        public byte[] ResponseFrame { get; private set; }

        public ModbusRTUResponseGenerator(byte station, byte functionCode, byte[] data)
        {
            Station = station;
            FunctionCode = functionCode;
            Data = data;

            // 根据 Modbus RTU 协议构造完整的响应帧
            byte[] frame = new byte[2 + data.Length]; // Unit ID + Function Code + Data
            frame[0] = station;
            frame[1] = functionCode;
            Array.Copy(data, 0, frame, 2, data.Length);

            // 计算 CRC 校验
            byte[] crc = CRC16Helper.GetCRC16(frame);
            // 将 CRC 加到响应帧的结尾
            ResponseFrame = new byte[frame.Length + 2];
            Array.Copy(frame, 0, ResponseFrame, 0, frame.Length);
            Array.Copy(crc, 0, ResponseFrame, frame.Length, 2);
        }

        // 将 Modbus RTU 响应转化为字节数组
        public byte[] ToByteArray()
        {
            return ResponseFrame;
        }
    }
}

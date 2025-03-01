using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus.Base
{
    public class ModbusRTURequest : IDeviceReadWriteMessage
    {
        public byte Station { get; private set; }

        public byte FunctionCode { get; private set; }

        public byte[] RequestFrame { get; private set; }

        public int RegisterCount { get; set; }

        public string RegisterAddress { get; set; }

        public byte[] ProtocolMessageFrame { get; set; }

        public int ProtocolDataNumber { get; set; }

        public int ProtocolResponseLength { get; set; }

        public ModbusRTURequest(byte station, byte functionCode, ushort address, int length, byte[] data = null)
        {
            Station = station;
            FunctionCode = functionCode;

            byte[] frame = null;
            switch (functionCode)
            {
                case 0x01: // 读取线圈状态
                    frame = new byte[4]; // 地址 + 长度
                    frame[0] = (byte)(address >> 8); // 地址高字节
                    frame[1] = (byte)(address & 0xFF); // 地址低字节
                    frame[2] = (byte)(length >> 8); // 长度高字节
                    frame[3] = (byte)(length & 0xFF); // 长度低字节
                    break;

                case 0x02: // 读取离散输入状态
                    frame = new byte[4]; // 地址 + 长度
                    frame[0] = (byte)(address >> 8); // 地址高字节
                    frame[1] = (byte)(address & 0xFF); // 地址低字节
                    frame[2] = (byte)(length >> 8); // 长度高字节
                    frame[3] = (byte)(length & 0xFF); // 长度低字节
                    break;

                case 0x03: // 读取保持寄存器
                case 0x04: // 读取输入寄存器
                    frame = new byte[4]; // 地址 + 长度
                    frame[0] = (byte)(address >> 8); // 地址高字节
                    frame[1] = (byte)(address & 0xFF); // 地址低字节
                    frame[2] = (byte)(length >> 8); // 长度高字节
                    frame[3] = (byte)(length & 0xFF); // 长度低字节
                    break;

                case 0x05: // 写单个线圈
                    frame = new byte[4]; // 地址 + 写入值
                    frame[0] = (byte)(address >> 8); // 地址高字节
                    frame[1] = (byte)(address & 0xFF); // 地址低字节
                    frame[2] = (byte)(length == 0 ? 0xFF00 : 0x0000); // 0xFF00：设置线圈ON；0x0000：设置线圈OFF
                    frame[3] = (byte)(length == 0 ? 0x00 : 0xFF); // 0xFF：写入ON；0x00：写入OFF
                    break;

                case 0x06: // 写单个保持寄存器
                    frame = new byte[4]; // 地址 + 写入值
                    frame[0] = (byte)(address >> 8); // 地址高字节
                    frame[1] = (byte)(address & 0xFF); // 地址低字节
                    frame[2] = (byte)(length >> 8); // 写入值高字节
                    frame[3] = (byte)(length & 0xFF); // 写入值低字节
                    break;

                case 0x0F: // 写多个线圈
                    frame = new byte[5 + ((length + 7) / 8)]; // 地址 + 长度 + 数据
                    frame[0] = (byte)(address >> 8); // 地址高字节
                    frame[1] = (byte)(address & 0xFF); // 地址低字节
                    frame[2] = (byte)(length >> 8); // 长度高字节
                    frame[3] = (byte)(length & 0xFF); // 长度低字节
                    frame[4] = (byte)((length + 7) / 8); // 数据字节数
                    Array.Copy(data, 0, frame, 5, data.Length); // 数据部分
                    break;

                case 0x10: // 写多个保持寄存器
                    frame = new byte[5 + length * 2]; // 地址 + 长度 + 数据
                    frame[0] = (byte)(address >> 8); // 地址高字节
                    frame[1] = (byte)(address & 0xFF); // 地址低字节
                    frame[2] = (byte)(length >> 8); // 长度高字节
                    frame[3] = (byte)(length & 0xFF); // 长度低字节
                    frame[4] = (byte)(length * 2); // 数据字节数
                    Array.Copy(data, 0, frame, 5, data.Length); // 数据部分
                    break;
            }

            // 将功能码 + 数据 + CRC 校验构成请求帧
            byte[] completeFrame = new byte[2 + frame.Length];
            completeFrame[0] = Station;
            completeFrame[1] = FunctionCode;
            Array.Copy(frame, 0, completeFrame, 2, frame.Length);

            // 计算 CRC 校验
            byte[] crc = CRC16Helper.GetCRC16(completeFrame);
            RequestFrame = new byte[completeFrame.Length + 2];
            Array.Copy(completeFrame, 0, RequestFrame, 0, completeFrame.Length);
            Array.Copy(crc, 0, RequestFrame, completeFrame.Length, 2);
        }

        public void Initialize(byte[] frame)
        {
            throw new NotImplementedException();
        }
    }
}

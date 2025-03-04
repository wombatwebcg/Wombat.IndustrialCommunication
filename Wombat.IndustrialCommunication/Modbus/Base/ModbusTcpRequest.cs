using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusTcpRequest : IDeviceReadWriteMessage
    {

        public ushort TransactionId { get; private set; }
        public byte Station { get; private set; }
        public byte FunctionCode { get; private set; }
        public byte[] ProtocolMessageFrame { get; private set; }
        public int ProtocolDataNumber { get; set; }
        public int RegisterCount { get; set; }
        public string RegisterAddress { get; set; }
        public int ProtocolResponseLength { get; set; }

        public ModbusTcpRequest(ushort transactionId, byte station, byte functionCode, ushort registerAddress, ushort length, byte[] data = null)
        {
            TransactionId = transactionId;
            Station = station;
            FunctionCode = functionCode;
            RegisterCount = length;
            ProtocolDataNumber = registerAddress;

            // 根据功能码设置寄存器地址格式
            switch (functionCode)
            {
                case 0x01:  // 线圈
                    this.RegisterAddress = (registerAddress + 1).ToString("00000");
                    break;
                case 0x02:  // 离散输入
                    this.RegisterAddress = (registerAddress + 10001).ToString();
                    break;
                case 0x03:  // 保持寄存器
                    this.RegisterAddress = (registerAddress + 40001).ToString();
                    break;
                case 0x04:  // 输入寄存器
                    this.RegisterAddress = (registerAddress + 30001).ToString();
                    break;
                default:
                    this.RegisterAddress = registerAddress.ToString();
                    break;
            }

            byte[] pdu = BuildPDU(functionCode, registerAddress, length, data);
            byte[] mbapHeader = BuildMBAPHeader(pdu.Length);

            // 合并MBAP头和PDU
            ProtocolMessageFrame = new byte[mbapHeader.Length + pdu.Length];
            Array.Copy(mbapHeader, 0, ProtocolMessageFrame, 0, mbapHeader.Length);
            Array.Copy(pdu, 0, ProtocolMessageFrame, mbapHeader.Length, pdu.Length);
        }

        private byte[] BuildPDU(byte functionCode, ushort address, ushort length, byte[] data = null)
        {
            byte[] pdu;
            byte[] addressBytes = BitConverter.GetBytes(address);
            byte[] lengthBytes;
            switch (functionCode)
            {
                // 读取类指令（读线圈、读离散输入、读保持寄存器、读输入寄存器）
                case 0x01:  // 读线圈
                    lengthBytes = BitConverter.GetBytes(length);
                    pdu = new byte[5];
                    pdu[0] = functionCode;
                    pdu[1] = addressBytes[1];
                    pdu[2] = addressBytes[0];//寄存器地址
                    pdu[3] = lengthBytes[1];
                    pdu[4] = lengthBytes[0];//表示request 寄存器的长度(寄存器个数)
                    ProtocolResponseLength = 7 + 1 + 1 + (int)(Math.Ceiling(length/8.0)); // 7 (MBAP) + 功能码 + 字节数 + 数据

                    break;
                case 0x02:  // 读离散输入
                case 0x03:  // 读保持寄存器
                case 0x04:  // 读输入寄存器
                    pdu = new byte[5];
                    lengthBytes = BitConverter.GetBytes(length/2);
                    pdu[0] = functionCode;
                    pdu[1] = addressBytes[1];
                    pdu[2] = addressBytes[0];//寄存器地址
                    pdu[3] = lengthBytes[1];
                    pdu[4] = lengthBytes[0];//表示request 寄存器的长度(寄存器个数)
                    ProtocolResponseLength = 7 + 1 + 1 + length ; // 7 (MBAP) + 功能码 + 字节数 + 数据
                    break;

                // 写单寄存器/线圈（0x05 写单线圈, 0x06 写单寄存器）
                case 0x05:  // 写单线圈
                    pdu = new byte[5];
                    pdu[0] = functionCode;
                    pdu[1] = addressBytes[1];
                    pdu[2] = addressBytes[0];//寄存器地址
                    pdu[3] = data[0];
                    pdu[4] = data[1];//表示request 寄存器的长度(寄存器个数)
                    ProtocolResponseLength = 12; // MBAP + 回显数据
                    break;


                case 0x06:  // 写单寄存器
                    pdu = new byte[5];
                    pdu[0] = functionCode;
                    pdu[1] = addressBytes[1];
                    pdu[2] = addressBytes[0];//寄存器地址
                    pdu[3] = data[0];
                    pdu[4] = data[1];//表示request 寄存器的长度(寄存器个数)
                    ProtocolResponseLength = 12; // MBAP + 回显数据
                    break;

                // 写多寄存器/线圈（0x0F 写多线圈, 0x10 写多寄存器）
                case 0x0F:  // 写多线圈

                    pdu = new byte[6 + data.Length];

                    pdu[0] = functionCode;
                    pdu[1] = addressBytes[1];
                    pdu[2] = addressBytes[0];//寄存器地址
                    pdu[3] = (byte)(length  / 256);
                    pdu[4] = (byte)(length  % 256);     //写寄存器数量(除2是两个字节一个寄存器，寄存器16位。除以256是byte最大存储255。)              
                    pdu[5] = (byte)(data.Length);          //写字节的个数
                    data.CopyTo(pdu, 6);                   //把目标值附加到数组后面
                    ProtocolResponseLength = 12; // MBAP + 回显数据
                    break;

                case 0x10:  // 写多寄存器
                    pdu = new byte[6+data.Length];
                    pdu[0] = functionCode;
                    pdu[1] = addressBytes[1];
                    pdu[2] = addressBytes[0];//寄存器地址
                    pdu[3] = (byte)(data.Length / 2 / 256);
                    pdu[4] = (byte)(data.Length / 2 % 256);//写寄存器数量(除2是两个字节一个寄存器，寄存器16位。除以256是byte最大存储255。)              
                    pdu[5] = (byte)(data.Length);          //写字节的个数
                    data.CopyTo(pdu, 6);                   //把目标值附加到数组后面
                    ProtocolResponseLength = 12; // MBAP + 回显数据

                    break;


                // 默认不支持的功能码
                default:
                    throw new NotSupportedException("Unsupported function code");
            }
            return pdu;
        }

        private byte[] BuildMBAPHeader(int pduLength)
        {
            byte[] header = new byte[7];

            // Transaction ID (大端序)
            byte[] transactionIdBytes = BitConverter.GetBytes(TransactionId);
            byte[] length = BitConverter.GetBytes(pduLength+1);
            header[0] = transactionIdBytes[1];
            header[1] = transactionIdBytes[0];
            header[2] = 0x00;
            header[3] = 0x00;//表示tcp/ip 的协议的Modbus的协议
            header[4] = length[1];
            header[5] = length[0];//表示的是该字节以后的字节长度

            // Unit ID (1字节)
            header[6] = Station; // Unit ID

            return header;


        }

        public void Initialize(byte[] frame)
        {
            // 实现帧解析逻辑（示例）
            if (frame.Length < 12) throw new ArgumentException("Invalid frame");

            TransactionId = BitConverter.ToUInt16(frame, 0);
            Station = frame[6];
            FunctionCode = frame[7];

            // 解析地址和数据（示例）
            ushort address = BitConverter.ToUInt16(new byte[] { frame[9], frame[8] }, 0); // 大端序转换
            ProtocolDataNumber = address;
            RegisterAddress = address.ToString();
        }

        public void Analysis(byte[] frame)
        {
            // 实现帧解析逻辑（示例）
            if (frame.Length < 12) throw new ArgumentException("Invalid frame");

            TransactionId = BitConverter.ToUInt16(frame, 0);
            Station = frame[6];
            FunctionCode = frame[7];

            // 解析地址和数据（示例）
            ushort address = BitConverter.ToUInt16(new byte[] { frame[9], frame[8] }, 0); // 大端序转换
            ProtocolDataNumber = address;
            RegisterAddress = address.ToString();
        }

    }
}

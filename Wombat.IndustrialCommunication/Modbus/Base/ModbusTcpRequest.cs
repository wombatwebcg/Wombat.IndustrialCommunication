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
            switch (functionCode)
            {
                // 读取类指令（读线圈、读离散输入、读保持寄存器、读输入寄存器）
                case 0x01:  // 读线圈
                case 0x02:  // 读离散输入
                    pdu = new byte[5];
                    pdu[0] = functionCode;
                    pdu[1] = (byte)(address >> 8);
                    pdu[2] = (byte)(address & 0xFF);
                    pdu[3] = (byte)(length >> 8);
                    pdu[4] = (byte)(length & 0xFF);
                    ProtocolResponseLength = 7 + 1 + 1 + (int)Math.Ceiling(length / 8.0); // MBAP + 功能码 + 字节数 + 位数据
                    break;

                case 0x03:  // 读保持寄存器
                case 0x04:  // 读输入寄存器
                    pdu = new byte[5];
                    pdu[0] = functionCode;
                    pdu[1] = (byte)(address >> 8);
                    pdu[2] = (byte)(address & 0xFF);
                    pdu[3] = (byte)(length >> 8);
                    pdu[4] = (byte)(length & 0xFF);
                    ProtocolResponseLength = 7 + 1 + 1 + length * 2; // MBAP + 功能码 + 字节数 + 寄存器数据
                    break;

                case 0x05:  // 写单线圈
                case 0x06:  // 写单寄存器
                    if (data == null || data.Length < 2)
                        throw new ArgumentException("Data must be at least 2 bytes for function code 0x05/0x06");

                    pdu = new byte[5];
                    pdu[0] = functionCode;
                    pdu[1] = (byte)(address >> 8);
                    pdu[2] = (byte)(address & 0xFF);
                    pdu[3] = data[0];
                    pdu[4] = data[1];
                    ProtocolResponseLength = 12; // MBAP + FunctionCode + Address + Value
                    break;

                case 0x0F:  // 写多线圈
                    if (data == null || data.Length == 0)
                        throw new ArgumentException("Data cannot be null or empty for function code 0x0F");

                    pdu = new byte[6 + data.Length];
                    pdu[0] = functionCode;
                    pdu[1] = (byte)(address >> 8);
                    pdu[2] = (byte)(address & 0xFF);
                    pdu[3] = (byte)(length >> 8);      // bit 数量
                    pdu[4] = (byte)(length & 0xFF);
                    pdu[5] = (byte)data.Length;        // 实际写入字节数
                    data.CopyTo(pdu, 6);
                    ProtocolResponseLength = 12; // MBAP + 回显功能码、地址、写入数量
                    break;

                case 0x10:  // 写多寄存器
                    if (data == null || data.Length == 0 || data.Length % 2 != 0)
                        throw new ArgumentException("Data must be non-empty and even-length for function code 0x10");

                    ushort registerCount = (ushort)(data.Length / 2);
                    pdu = new byte[6 + data.Length];
                    pdu[0] = functionCode;
                    pdu[1] = (byte)(address >> 8);
                    pdu[2] = (byte)(address & 0xFF);
                    pdu[3] = (byte)(registerCount >> 8);
                    pdu[4] = (byte)(registerCount & 0xFF);
                    pdu[5] = (byte)data.Length;
                    data.CopyTo(pdu, 6);
                    ProtocolResponseLength = 12; // MBAP + 回显功能码、地址、写入数量
                    break;

                default:
                    throw new NotSupportedException($"Unsupported function code: 0x{functionCode:X2}");
            }

            return pdu;
        }

        private byte[] BuildMBAPHeader(int pduLength)
        {
            byte[] header = new byte[7];

            // MBAP.Length = PDU + Unit ID (1 byte)
            int pduLengthWithUnitId = pduLength + 1;

            if (pduLengthWithUnitId > 0xFFFF)
                throw new ArgumentOutOfRangeException(nameof(pduLength), "PDU length too long for Modbus TCP");

            // Transaction ID (big-endian)
            header[0] = (byte)(TransactionId >> 8);
            header[1] = (byte)(TransactionId & 0xFF);

            // Protocol ID = 0 (Modbus)
            header[2] = 0x00;
            header[3] = 0x00;

            // Length (PDU + Unit ID), big-endian
            header[4] = (byte)(pduLengthWithUnitId >> 8);
            header[5] = (byte)(pduLengthWithUnitId & 0xFF);

            // Unit ID
            header[6] = Station;

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

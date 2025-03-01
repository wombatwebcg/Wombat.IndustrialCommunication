using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusTcpResponse : IDeviceReadWriteMessage
    {
        private static ArrayPool<byte> _byteArrayPool = ArrayPool<byte>.Shared;

        public ushort TransactionId { get; private set; }
        public ushort ProtocolId { get; private set; }
        public ushort Length { get; private set; }
        public byte Station { get; private set; }
        public byte FunctionCode { get; private set; }
        public byte[] Data { get; private set; }
        public int RegisterCount { get; set; }
        public string RegisterAddress { get; set; }
        public int ProtocolResponseLength { get; set; }
        public byte[] ProtocolMessageFrame { get; set; }
        public int ProtocolDataNumber { get; set; }

        // 构造函数：用于初始化解析响应帧
        public ModbusTcpResponse(byte[] frame)
        {
            Initialize(frame);
        }

        // 解析 Modbus TCP 响应帧
        public void Initialize(byte[] frame)
        {
            if (frame == null || frame.Length < 10)
                throw new ArgumentException("Invalid Modbus TCP frame");
            ushort lengthInFrame = BitConverter.ToUInt16(new byte[2] { frame[5], frame[4] },0);
            if (frame.Length != lengthInFrame + 6)
                throw new ArgumentException("Frame length mismatch with MBAP header length");
            // 解析 MBAP Header
            // Transaction ID（2字节）
            TransactionId = BitConverter.ToUInt16(frame, 0);
            // Protocol ID（2字节，通常是0x0000）
            ProtocolId = BitConverter.ToUInt16(frame, 2);
            // Length（2字节）
            Length = BitConverter.ToUInt16(frame, 4);
            // Unit ID（1字节）
            Station = frame[6];

            // 检查协议是否为Modbus（Protocol ID应该为0x0000）
            if (ProtocolId != 0)
            {
                throw new InvalidOperationException("Invalid Protocol ID, should be 0 for Modbus TCP");
            }

            // 解析 PDU（PDU 从第 7 个字节开始）
            int pduLength = frame.Length - 7;  // 去掉 MBAP header 的 7 字节
            byte[] pdu = new byte[pduLength];
            Array.Copy(frame, 7, pdu, 0, pduLength);

            // 解析功能码（1字节）
            FunctionCode = pdu[0];

            // 检查是否为异常功能码
            if (VerifyFunctionCode(FunctionCode, pdu[0]))
            {
                // 异常码，返回相应的错误信息
                byte exceptionCode = pdu[1];
                string errMsg = ErrMsg(exceptionCode);
                throw new InvalidOperationException($"Modbus exception: {errMsg}");
            }

            // 根据功能码解析数据
            switch (FunctionCode)
            {
                case 0x01:  // 读线圈
                case 0x02:  // 读离散输入
                case 0x03:  // 读保持寄存器
                case 0x04:  // 读输入寄存器
                    int byteCount = pdu[1];
                    Data = new byte[byteCount];
                    Array.Copy(pdu, 2, Data, 0, byteCount);
                    ProtocolResponseLength = 7 + 1 + 1 + byteCount; // MBAP + 功能码 + 字节数 + 数据
                    break;

                case 0x05:  // 写单线圈
                case 0x06:  // 写单寄存器
                    Data = new byte[4]; // 功能码 + 地址 + 写入值
                    Array.Copy(pdu, 0, Data, 0, 4);
                    ProtocolResponseLength = 12; // MBAP + 回显数据
                    break;

                case 0x0F:  // 写多线圈
                case 0x10:  // 写多寄存器
                    Data = new byte[4]; // 功能码 + 地址 + 写入数量 + 写入字节数
                    Array.Copy(pdu, 0, Data, 0, 4);
                    ProtocolResponseLength = 12; // MBAP + 回显数据
                    break;

                default:
                    throw new InvalidOperationException("Unsupported function code");
            }

            // 填充 ProtocolMessageFrame
            ProtocolMessageFrame = frame;
        }

        /// <summary>
        /// 是否为异常功能码
        /// </summary>
        /// <param name="resultCode"></param>
        /// <param name="responseCode"></param>
        /// <returns></returns>
        public static bool VerifyFunctionCode(byte resultCode, byte responseCode)
        {
            // 异常功能码的规则是，responseCode - resultCode == 128
            return responseCode - resultCode == 128;
        }

        /// <summary>
        /// 异常码描述
        /// </summary>
        /// <param name="errCode"></param>
        /// <returns></returns>
        public static string ErrMsg(byte errCode)
        {
            var err = "未知异常";
            switch (errCode)
            {
                case 0x01:
                    err = $"异常码{errCode}：非法功能";
                    break;
                case 0x02:
                    err = $"异常码{errCode}：非法数据地址";
                    break;
                case 0x03:
                    err = $"异常码{errCode}：非法数据值";
                    break;
                case 0x04:
                    err = $"异常码{errCode}：从站设备故障";
                    break;
                case 0x05:
                    err = $"异常码{errCode}：确认";
                    break;
                case 0x06:
                    err = $"异常码{errCode}：从属设备忙";
                    break;
                case 0x08:
                    err = $"异常码{errCode}：存储奇偶性差错";
                    break;
                case 0x0A:
                    err = $"异常码{errCode}：不可用网关路径";
                    break;
                case 0x0B:
                    err = $"异常码{errCode}：网关目标设备响应失败";
                    break;
            }
            return err;
        }
    }
}

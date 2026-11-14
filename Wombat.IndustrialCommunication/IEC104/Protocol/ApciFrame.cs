using System;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Protocol
{
    /// <summary>APCI 帧类别。</summary>
    public enum ApciFrameType
    {
        Information,
        Supervisory,
        Unnumbered,
    }

    /// <summary>U 帧功能。</summary>
    /// <summary>
    /// U 帧功能码。取值即控制域首字节的完整值，六种功能各占一位。
    /// </summary>
    public enum ApciFunction : byte
    {
        None = 0,
        StartDataTransferActivation = 0x07,
        StartDataTransferConfirmation = 0x0B,
        StopDataTransferActivation = 0x13,
        StopDataTransferConfirmation = 0x23,
        TestFrameActivation = 0x43,
        TestFrameConfirmation = 0x83,
    }

    /// <summary>
    /// APCI 帧：<c>0x68 | 长度 | 控制域(4) | ASDU</c>。
    /// </summary>
    public sealed class ApciFrame
    {
        private ApciFrame()
        {
        }

        public ApciFrameType FrameType { get; private set; }

        /// <summary>发送序号 N(S)，仅 I 帧有效。</summary>
        public ushort SendSequence { get; private set; }

        /// <summary>接收序号 N(R)，I 帧与 S 帧有效。</summary>
        public ushort ReceiveSequence { get; private set; }

        /// <summary>U 帧功能，仅 U 帧有效。</summary>
        public ApciFunction Function { get; private set; }

        /// <summary>ASDU 字节（不含 APCI），S 帧与 U 帧为 null。</summary>
        public byte[] Asdu { get; private set; }

        public static ApciFrame CreateInformation(ushort sendSequence, ushort receiveSequence, byte[] asdu)
        {
            ValidateSequence(sendSequence);
            ValidateSequence(receiveSequence);
            if (asdu == null || asdu.Length == 0)
                throw new ArgumentException("I 帧必须携带 ASDU。", nameof(asdu));
            if (asdu.Length > Iec104Constants.MaxAsduLength)
                throw new Iec104ProtocolException(Iec104ErrorKind.CapacityExceeded, "ASDU 超出 249 字节上限。");

            return new ApciFrame
            {
                FrameType = ApciFrameType.Information,
                SendSequence = sendSequence,
                ReceiveSequence = receiveSequence,
                Asdu = asdu,
            };
        }

        public static ApciFrame CreateSupervisory(ushort receiveSequence)
        {
            ValidateSequence(receiveSequence);
            return new ApciFrame
            {
                FrameType = ApciFrameType.Supervisory,
                ReceiveSequence = receiveSequence,
            };
        }

        public static ApciFrame CreateUnnumbered(ApciFunction function)
        {
            if (function == ApciFunction.None)
                throw new ArgumentOutOfRangeException(nameof(function));

            return new ApciFrame
            {
                FrameType = ApciFrameType.Unnumbered,
                Function = function,
            };
        }

        /// <summary>编码为完整 APDU。</summary>
        public byte[] ToBytes()
        {
            var asduLength = Asdu == null ? 0 : Asdu.Length;
            var total = Iec104Constants.ApciLength + asduLength;
            if (total > Iec104Constants.MaxApduLength)
                throw new Iec104ProtocolException(Iec104ErrorKind.CapacityExceeded, "APDU 超出 255 字节上限。");

            var buffer = new byte[total];
            buffer[0] = Iec104Constants.StartByte;
            buffer[1] = (byte)(total - 2);

            switch (FrameType)
            {
                case ApciFrameType.Information:
                    buffer[2] = (byte)((SendSequence << 1) & 0xFE);
                    buffer[3] = (byte)(SendSequence >> 7);
                    buffer[4] = (byte)((ReceiveSequence << 1) & 0xFE);
                    buffer[5] = (byte)(ReceiveSequence >> 7);
                    Buffer.BlockCopy(Asdu, 0, buffer, Iec104Constants.ApciLength, asduLength);
                    break;

                case ApciFrameType.Supervisory:
                    buffer[2] = 0x01;
                    buffer[3] = 0x00;
                    buffer[4] = (byte)((ReceiveSequence << 1) & 0xFE);
                    buffer[5] = (byte)(ReceiveSequence >> 7);
                    break;

                default:
                    buffer[2] = (byte)Function;
                    buffer[3] = 0x00;
                    buffer[4] = 0x00;
                    buffer[5] = 0x00;
                    break;
            }

            return buffer;
        }

        /// <summary>解析单个完整 APDU；缓冲不足或格式非法返回 false。</summary>
        public static bool TryParse(byte[] buffer, int offset, int count, out ApciFrame frame, out string error)
        {
            frame = null;
            error = null;

            if (buffer == null) { error = "缓冲为空。"; return false; }
            if (offset < 0 || count < 0 || offset + count > buffer.Length) { error = "偏移或长度越界。"; return false; }
            if (count < Iec104Constants.ApciLength) { error = "不足 APCI 长度。"; return false; }
            if (buffer[offset] != Iec104Constants.StartByte) { error = "启动字符不是 0x68。"; return false; }

            var lengthField = buffer[offset + 1];
            if (lengthField < 4) { error = "长度字段小于 4。"; return false; }
            if (lengthField + 2 > count) { error = "长度字段超出实际数据。"; return false; }

            var control0 = buffer[offset + 2];
            var control1 = buffer[offset + 3];
            var control2 = buffer[offset + 4];
            var control3 = buffer[offset + 5];

            if ((control0 & 0x01) == 0)
            {
                var send = (ushort)((((control1 << 8) | control0) >> 1) & Iec104Constants.SequenceMask);
                var receive = (ushort)((((control3 << 8) | control2) >> 1) & Iec104Constants.SequenceMask);
                var asduLength = lengthField - 4;
                var asdu = new byte[asduLength];
                if (asduLength > 0)
                {
                    Buffer.BlockCopy(buffer, offset + Iec104Constants.ApciLength, asdu, 0, asduLength);
                }

                frame = new ApciFrame
                {
                    FrameType = ApciFrameType.Information,
                    SendSequence = send,
                    ReceiveSequence = receive,
                    Asdu = asdu,
                };
                return true;
            }

            if ((control0 & 0x03) == 0x01)
            {
                var receive = (ushort)((((control3 << 8) | control2) >> 1) & Iec104Constants.SequenceMask);
                frame = new ApciFrame
                {
                    FrameType = ApciFrameType.Supervisory,
                    ReceiveSequence = receive,
                };
                return true;
            }

            if ((control0 & 0x03) == 0x03)
            {
                var function = (ApciFunction)control0;
                if (!IsKnownFunction(function))
                {
                    error = "未知的 U 帧功能码。";
                    return false;
                }

                frame = new ApciFrame
                {
                    FrameType = ApciFrameType.Unnumbered,
                    Function = function,
                };
                return true;
            }

            error = "无法识别的控制域格式。";
            return false;
        }

        private static bool IsKnownFunction(ApciFunction function)
        {
            switch (function)
            {
                case ApciFunction.StartDataTransferActivation:
                case ApciFunction.StartDataTransferConfirmation:
                case ApciFunction.StopDataTransferActivation:
                case ApciFunction.StopDataTransferConfirmation:
                case ApciFunction.TestFrameActivation:
                case ApciFunction.TestFrameConfirmation:
                    return true;
                default:
                    return false;
            }
        }

        private static void ValidateSequence(ushort sequence)
        {
            if (sequence > Iec104Constants.SequenceMask)
                throw new ArgumentOutOfRangeException(nameof(sequence), "序号必须在 0..32767 之间。");
        }
    }
}

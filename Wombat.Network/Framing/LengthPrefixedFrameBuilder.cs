using System;

namespace Wombat.Network
{
    /// <summary>
    /// 长度前缀帧构建器，使用类似WebSocket的帧格式，支持可选的数据掩码。
    /// </summary>
    /// <remarks>
    /// 帧格式概述：
    /// <code>
    ///  0                   1                   2                   3
    ///  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-------------+-----------------------------------------------+
    /// |M| Payload len |    Extended payload length                    |
    /// |A|     (7)     |             (16/64)                           |
    /// |S|             |   (if payload len==126/127)                   |
    /// |K|             |                                               |
    /// +-+-------------+- - - - - - - - - - - - - - - - - - - - - - - -+
    /// |     Extended payload length continued, if payload len == 127  |
    /// + - - - - - - - + - - - - - - - - - - - - - - - - - - - - - - - +
    /// |               |    Masking-key, if MASK set to 1              |
    /// +---------------+- - - - - - - - - - - - - - - - - - - - - - - -+
    /// |               |          Payload Data                         :
    /// +----------------- - - - - - - - - - - - - - - - - - - - - - - -+
    /// :                     Payload Data continued ...                :
    /// + - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
    /// |                     Payload Data continued ...                |
    /// +---------------------------------------------------------------+
    /// </code>
    /// </remarks>
    public sealed class LengthPrefixedFrameBuilder : FrameBuilder
    {
        /// <summary>
        /// 初始化 <see cref="LengthPrefixedFrameBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="isMasked">指示是否对数据进行掩码处理。</param>
        public LengthPrefixedFrameBuilder(bool isMasked = false)
            : this(new LengthPrefixedFrameEncoder(isMasked), new LengthPrefixedFrameDecoder(isMasked))
        {
        }

        /// <summary>
        /// 初始化 <see cref="LengthPrefixedFrameBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="encoder">用于编码帧的编码器。</param>
        /// <param name="decoder">用于解码帧的解码器。</param>
        public LengthPrefixedFrameBuilder(LengthPrefixedFrameEncoder encoder, LengthPrefixedFrameDecoder decoder)
            : base(encoder, decoder)
        {
        }
    }

    /// <summary>
    /// 长度前缀帧编码器，将数据编码为带长度前缀的帧格式，支持可选的数据掩码。
    /// </summary>
    public sealed class LengthPrefixedFrameEncoder : IFrameEncoder
    {
        private static readonly Random _rng = new Random(DateTime.UtcNow.Millisecond);
        private static readonly int MaskingKeyLength = 4;

        /// <summary>
        /// 初始化 <see cref="LengthPrefixedFrameEncoder"/> 类的新实例。
        /// </summary>
        /// <param name="isMasked">指示是否对数据进行掩码处理。</param>
        public LengthPrefixedFrameEncoder(bool isMasked = false)
        {
            IsMasked = isMasked;
        }

        /// <summary>
        /// 获取一个值，指示是否对数据进行掩码处理。
        /// </summary>
        public bool IsMasked { get; private set; }

        /// <summary>
        /// 将指定的数据编码为长度前缀帧格式。
        /// </summary>
        /// <param name="payload">要编码的原始数据。</param>
        /// <param name="offset">数据的起始偏移量。</param>
        /// <param name="count">要编码的数据长度。</param>
        /// <param name="frameBuffer">输出参数，编码后的帧数据缓冲区。</param>
        /// <param name="frameBufferOffset">输出参数，帧数据在缓冲区中的起始偏移量。</param>
        /// <param name="frameBufferLength">输出参数，帧数据的总长度。</param>
        /// <remarks>
        /// 帧长度编码规则：
        /// <list type="bullet">
        /// <item><description>0-125：直接使用7位表示长度</description></item>
        /// <item><description>126：使用2字节表示长度</description></item>
        /// <item><description>127：使用8字节表示长度</description></item>
        /// </list>
        /// </remarks>
        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            var buffer = Encode(payload, offset, count, IsMasked);

            frameBuffer = buffer;
            frameBufferOffset = 0;
            frameBufferLength = buffer.Length;
        }

        /// <summary>
        /// 编码数据为长度前缀帧格式的内部实现。
        /// </summary>
        /// <param name="payload">要编码的原始数据。</param>
        /// <param name="offset">数据的起始偏移量。</param>
        /// <param name="count">要编码的数据长度。</param>
        /// <param name="isMasked">指示是否对数据进行掩码处理。</param>
        /// <returns>编码后的帧数据。</returns>
        private static byte[] Encode(byte[] payload, int offset, int count, bool isMasked = false)
        {
            byte[] fragment;

            // Payload length:  7 bits, 7+16 bits, or 7+64 bits.
            // The length of the "Payload data", in bytes: 
            // if 0-125, that is the payload length.  
            // If 126, the following 2 bytes interpreted as a 16-bit unsigned integer are the payload length.  
            // If 127, the following 8 bytes interpreted as a 64-bit unsigned integer are the payload length.
            if (count < 126)
            {
                fragment = new byte[1 + (isMasked ? MaskingKeyLength : 0) + count];
                fragment[0] = (byte)count;
            }
            else if (count < 65536)
            {
                fragment = new byte[1 + 2 + (isMasked ? MaskingKeyLength : 0) + count];
                fragment[0] = (byte)126;
                fragment[1] = (byte)(count / 256);
                fragment[2] = (byte)(count % 256);
            }
            else
            {
                fragment = new byte[1 + 8 + (isMasked ? MaskingKeyLength : 0) + count];
                fragment[0] = (byte)127;

                int left = count;
                for (int i = 8; i > 0; i--)
                {
                    fragment[i] = (byte)(left % 256);
                    left = left / 256;

                    if (left == 0)
                        break;
                }
            }

            // Mask:  1 bit
            // Defines whether the "Payload data" is masked.
            if (isMasked)
                fragment[0] = (byte)(fragment[0] | 0x80);

            // Masking-key:  0 or 4 bytes
            // The masking key is a 32-bit value chosen at random by the client.
            if (isMasked)
            {
                int maskingKeyIndex = fragment.Length - (MaskingKeyLength + count);
                for (var i = maskingKeyIndex; i < maskingKeyIndex + MaskingKeyLength; i++)
                {
                    fragment[i] = (byte)_rng.Next(0, 255);
                }
                if (count > 0)
                {
                    int payloadIndex = fragment.Length - count;
                    for (var i = 0; i < count; i++)
                    {
                        fragment[payloadIndex + i] = (byte)(payload[offset + i] ^ fragment[maskingKeyIndex + i % MaskingKeyLength]);
                    }
                }
            }
            else
            {
                if (count > 0)
                {
                    int payloadIndex = fragment.Length - count;
                    Array.Copy(payload, offset, fragment, payloadIndex, count);
                }
            }

            return fragment;
        }
    }

    /// <summary>
    /// 长度前缀帧解码器，从长度前缀帧格式中解码数据，支持可选的数据掩码。
    /// </summary>
    public sealed class LengthPrefixedFrameDecoder : IFrameDecoder
    {
        private static readonly int MaskingKeyLength = 4;

        /// <summary>
        /// 初始化 <see cref="LengthPrefixedFrameDecoder"/> 类的新实例。
        /// </summary>
        /// <param name="isMasked">指示是否对数据进行掩码处理。</param>
        public LengthPrefixedFrameDecoder(bool isMasked = false)
        {
            IsMasked = isMasked;
        }

        /// <summary>
        /// 获取一个值，指示是否对数据进行掩码处理。
        /// </summary>
        public bool IsMasked { get; private set; }

        /// <summary>
        /// 尝试从指定的缓冲区中解码出一个完整的长度前缀帧。
        /// </summary>
        /// <param name="buffer">包含待解码数据的缓冲区。</param>
        /// <param name="offset">缓冲区中数据的起始偏移量。</param>
        /// <param name="count">缓冲区中可用数据的长度。</param>
        /// <param name="frameLength">输出参数，解码的帧的总长度（包括帧头）。</param>
        /// <param name="payload">输出参数，解码后的原始数据。</param>
        /// <param name="payloadOffset">输出参数，原始数据在输出缓冲区中的起始偏移量。</param>
        /// <param name="payloadCount">输出参数，原始数据的长度。</param>
        /// <returns>如果成功解码出完整帧则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            var frameHeader = DecodeHeader(buffer, offset, count);
            if (frameHeader != null && frameHeader.Length + frameHeader.PayloadLength <= count)
            {
                if (IsMasked)
                {
                    payload = DecodeMaskedPayload(buffer, offset, frameHeader.MaskingKeyOffset, frameHeader.Length, frameHeader.PayloadLength);
                    payloadOffset = 0;
                    payloadCount = payload.Length;
                }
                else
                {
                    payload = buffer;
                    payloadOffset = offset + frameHeader.Length;
                    payloadCount = frameHeader.PayloadLength;
                }

                frameLength = frameHeader.Length + frameHeader.PayloadLength;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 表示帧头信息的内部类。
        /// </summary>
        internal sealed class Header
        {
            /// <summary>
            /// 获取或设置一个值，指示数据是否被掩码。
            /// </summary>
            public bool IsMasked { get; set; }
            
            /// <summary>
            /// 获取或设置载荷数据的长度。
            /// </summary>
            public int PayloadLength { get; set; }
            
            /// <summary>
            /// 获取或设置掩码键的偏移量。
            /// </summary>
            public int MaskingKeyOffset { get; set; }
            
            /// <summary>
            /// 获取或设置帧头的长度。
            /// </summary>
            public int Length { get; set; }

            /// <summary>
            /// 返回当前帧头的字符串表示形式。
            /// </summary>
            /// <returns>包含帧头信息的字符串。</returns>
            public override string ToString()
            {
                return string.Format("IsMasked[{0}], PayloadLength[{1}], MaskingKeyOffset[{2}], Length[{3}]",
                    IsMasked, PayloadLength, MaskingKeyOffset, Length);
            }
        }

        /// <summary>
        /// 解码帧头信息。
        /// </summary>
        /// <param name="buffer">包含帧数据的缓冲区。</param>
        /// <param name="offset">数据的起始偏移量。</param>
        /// <param name="count">可用数据的长度。</param>
        /// <returns>解码后的帧头信息，如果数据不足则返回 <c>null</c>。</returns>
        private static Header DecodeHeader(byte[] buffer, int offset, int count)
        {
            if (count < 1)
                return null;

            // parse fixed header
            var header = new Header()
            {
                IsMasked = ((buffer[offset + 0] & 0x80) == 0x80),
                PayloadLength = (buffer[offset + 0] & 0x7f),
                Length = 1,
            };

            // parse extended payload length
            if (header.PayloadLength >= 126)
            {
                if (header.PayloadLength == 126)
                    header.Length += 2;
                else
                    header.Length += 8;

                if (count < header.Length)
                    return null;

                if (header.PayloadLength == 126)
                {
                    header.PayloadLength = buffer[offset + 1] * 256 + buffer[offset + 2];
                }
                else
                {
                    int totalLength = 0;
                    int level = 1;

                    for (int i = 7; i >= 0; i--)
                    {
                        totalLength += buffer[offset + i + 1] * level;
                        level *= 256;
                    }

                    header.PayloadLength = totalLength;
                }
            }

            // parse masking key
            if (header.IsMasked)
            {
                if (count < header.Length + MaskingKeyLength)
                    return null;

                header.MaskingKeyOffset = header.Length;
                header.Length += MaskingKeyLength;
            }

            return header;
        }

        private static byte[] DecodeMaskedPayload(byte[] buffer, int offset, int maskingKeyOffset, int payloadOffset, int payloadCount)
        {
            var payload = new byte[payloadCount];

            for (var i = 0; i < payloadCount; i++)
            {
                payload[i] = (byte)(buffer[offset + payloadOffset + i] ^ buffer[offset + maskingKeyOffset + i % MaskingKeyLength]);
            }

            return payload;
        }
    }
}

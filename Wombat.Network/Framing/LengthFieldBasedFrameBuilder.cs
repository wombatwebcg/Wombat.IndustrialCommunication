using System;

namespace Wombat.Network
{
    /// <summary>
    /// 表示长度字段的字节数大小。
    /// </summary>
    public enum LengthField
    {
        /// <summary>
        /// 1字节长度字段，支持最大255字节的数据。
        /// </summary>
        OneByte = 1,
        
        /// <summary>
        /// 2字节长度字段，支持最大65535字节的数据。
        /// </summary>
        TwoBytes = 2,
        
        /// <summary>
        /// 4字节长度字段，支持最大2GB的数据。
        /// </summary>
        FourBytes = 4,
        
        /// <summary>
        /// 8字节长度字段，支持非常大的数据。
        /// </summary>
        EigthBytes = 8,
    }

    /// <summary>
    /// 基于长度字段的帧构建器，使用指定大小的长度字段来标记数据的长度。
    /// </summary>
    public sealed class LengthFieldBasedFrameBuilder : FrameBuilder
    {
        /// <summary>
        /// 初始化 <see cref="LengthFieldBasedFrameBuilder"/> 类的新实例，使用默认的4字节长度字段。
        /// </summary>
        public LengthFieldBasedFrameBuilder()
            : this(LengthField.FourBytes)
        {
        }

        /// <summary>
        /// 初始化 <see cref="LengthFieldBasedFrameBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="lengthField">用于指示数据长度的长度字段大小。</param>
        public LengthFieldBasedFrameBuilder(LengthField lengthField)
            : this(new LengthFieldBasedFrameEncoder(lengthField), new LengthFieldBasedFrameDecoder(lengthField))
        {
        }

        /// <summary>
        /// 初始化 <see cref="LengthFieldBasedFrameBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="encoder">用于编码帧的编码器。</param>
        /// <param name="decoder">用于解码帧的解码器。</param>
        public LengthFieldBasedFrameBuilder(LengthFieldBasedFrameEncoder encoder, LengthFieldBasedFrameDecoder decoder)
            : base(encoder, decoder)
        {
        }
    }

    /// <summary>
    /// 基于长度字段的帧编码器，在数据前添加指定大小的长度字段。
    /// </summary>
    public sealed class LengthFieldBasedFrameEncoder : IFrameEncoder
    {
        /// <summary>
        /// 初始化 <see cref="LengthFieldBasedFrameEncoder"/> 类的新实例。
        /// </summary>
        /// <param name="lengthField">用于指示数据长度的长度字段大小。</param>
        public LengthFieldBasedFrameEncoder(LengthField lengthField)
        {
            LengthField = lengthField;
        }

        /// <summary>
        /// 获取长度字段的大小。
        /// </summary>
        public LengthField LengthField { get; private set; }

        /// <summary>
        /// 将指定的数据编码为带长度字段的帧格式。
        /// </summary>
        /// <param name="payload">要编码的原始数据。</param>
        /// <param name="offset">数据的起始偏移量。</param>
        /// <param name="count">要编码的数据长度。</param>
        /// <param name="frameBuffer">输出参数，编码后的帧数据缓冲区。</param>
        /// <param name="frameBufferOffset">输出参数，帧数据在缓冲区中的起始偏移量。</param>
        /// <param name="frameBufferLength">输出参数，帧数据的总长度。</param>
        /// <exception cref="ArgumentOutOfRangeException">当数据长度超过指定长度字段能表示的最大值时引发。</exception>
        /// <exception cref="NotSupportedException">当指定的长度字段不受支持时引发。</exception>
        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            byte[] buffer = null;

            switch (this.LengthField)
            {
                case LengthField.OneByte:
                    {
                        if (count > byte.MaxValue)
                        {
                            throw new ArgumentOutOfRangeException("count");
                        }

                        buffer = new byte[1 + count];
                        buffer[0] = (byte)count;
                        Array.Copy(payload, offset, buffer, 1, count);
                    }
                    break;
                case LengthField.TwoBytes:
                    {
                        if (count > short.MaxValue)
                        {
                            throw new ArgumentOutOfRangeException("count");
                        }

                        buffer = new byte[2 + count];
                        buffer[0] = (byte)((ushort)count >> 8);
                        buffer[1] = (byte)count;
                        Array.Copy(payload, offset, buffer, 2, count);
                    }
                    break;
                case LengthField.FourBytes:
                    {
                        buffer = new byte[4 + count];
                        uint unsignedValue = (uint)count;
                        buffer[0] = (byte)(unsignedValue >> 24);
                        buffer[1] = (byte)(unsignedValue >> 16);
                        buffer[2] = (byte)(unsignedValue >> 8);
                        buffer[3] = (byte)unsignedValue;
                        Array.Copy(payload, offset, buffer, 4, count);
                    }
                    break;
                case LengthField.EigthBytes:
                    {
                        buffer = new byte[8 + count];
                        ulong unsignedValue = (ulong)count;
                        buffer[0] = (byte)(unsignedValue >> 56);
                        buffer[1] = (byte)(unsignedValue >> 48);
                        buffer[2] = (byte)(unsignedValue >> 40);
                        buffer[3] = (byte)(unsignedValue >> 32);
                        buffer[4] = (byte)(unsignedValue >> 24);
                        buffer[5] = (byte)(unsignedValue >> 16);
                        buffer[6] = (byte)(unsignedValue >> 8);
                        buffer[7] = (byte)unsignedValue;
                        Array.Copy(payload, offset, buffer, 8, count);
                    }
                    break;
                default:
                    throw new NotSupportedException("Specified length field is not supported.");
            }

            frameBuffer = buffer;
            frameBufferOffset = 0;
            frameBufferLength = buffer.Length;
        }
    }

    /// <summary>
    /// 基于长度字段的帧解码器，从数据中读取长度字段并解码出原始数据。
    /// </summary>
    public sealed class LengthFieldBasedFrameDecoder : IFrameDecoder
    {
        /// <summary>
        /// 初始化 <see cref="LengthFieldBasedFrameDecoder"/> 类的新实例。
        /// </summary>
        /// <param name="lengthField">用于指示数据长度的长度字段大小。</param>
        public LengthFieldBasedFrameDecoder(LengthField lengthField)
        {
            LengthField = lengthField;
        }

        /// <summary>
        /// 获取长度字段的大小。
        /// </summary>
        public LengthField LengthField { get; private set; }

        /// <summary>
        /// 尝试从指定的缓冲区中解码出一个完整的基于长度字段的帧。
        /// </summary>
        /// <param name="buffer">包含待解码数据的缓冲区。</param>
        /// <param name="offset">缓冲区中数据的起始偏移量。</param>
        /// <param name="count">缓冲区中可用数据的长度。</param>
        /// <param name="frameLength">输出参数，解码的帧的总长度（包括长度字段）。</param>
        /// <param name="payload">输出参数，解码后的原始数据。</param>
        /// <param name="payloadOffset">输出参数，原始数据在输出缓冲区中的起始偏移量。</param>
        /// <param name="payloadCount">输出参数，原始数据的长度。</param>
        /// <returns>如果成功解码出完整帧则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        /// <exception cref="NotSupportedException">当指定的长度字段不受支持时引发。</exception>
        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            byte[] output = null;
            long length = 0;

            switch (this.LengthField)
            {
                case LengthField.OneByte:
                    {
                        if (count < 1)
                            return false;

                        length = buffer[offset];
                        if (count - 1 < length)
                            return false;

                        output = new byte[length];
                        Array.Copy(buffer, offset + 1, output, 0, length);
                    }
                    break;
                case LengthField.TwoBytes:
                    {
                        if (count < 2)
                            return false;

                        length = (short)(buffer[offset] << 8 | buffer[offset + 1]);
                        if (count - 2 < length)
                            return false;

                        output = new byte[length];
                        Array.Copy(buffer, offset + 2, output, 0, length);
                    }
                    break;
                case LengthField.FourBytes:
                    {
                        if (count < 4)
                            return false;

                        length = buffer[offset] << 24 |
                            buffer[offset + 1] << 16 |
                            buffer[offset + 2] << 8 |
                            buffer[offset + 3];
                        if (count - 4 < length)
                            return false;

                        output = new byte[length];
                        Array.Copy(buffer, offset + 4, output, 0, length);
                    }
                    break;
                case LengthField.EigthBytes:
                    {
                        if (count < 8)
                            return false;

                        int i1 = buffer[offset] << 24 |
                            buffer[offset + 1] << 16 |
                            buffer[offset + 2] << 8 |
                            buffer[offset + 3];
                        int i2 = buffer[offset + 4] << 24 |
                            buffer[offset + 5] << 16 |
                            buffer[offset + 6] << 8 |
                            buffer[offset + 7];

                        length = (uint)i2 | ((long)i1 << 32);
                        if (count - 8 < length)
                            return false;

                        output = new byte[length];
                        Array.Copy(buffer, offset + 8, output, 0, length);
                    }
                    break;
                default:
                    throw new NotSupportedException("Specified length field is not supported.");
            }

            payload = output;
            payloadOffset = 0;
            payloadCount = output.Length;

            frameLength = (int)this.LengthField + output.Length;

            return true;
        }
    }
}

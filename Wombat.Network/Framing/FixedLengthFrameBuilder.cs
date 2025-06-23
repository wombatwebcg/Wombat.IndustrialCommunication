using System;

namespace Wombat.Network
{
    /// <summary>
    /// 固定长度帧构建器，用于处理固定长度的数据帧。
    /// </summary>
    public sealed class FixedLengthFrameBuilder : FrameBuilder
    {
        /// <summary>
        /// 初始化 <see cref="FixedLengthFrameBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="fixedFrameLength">固定的帧长度。</param>
        public FixedLengthFrameBuilder(int fixedFrameLength)
            : this(new FixedLengthFrameEncoder(fixedFrameLength), new FixedLengthFrameDecoder(fixedFrameLength))
        {
        }

        /// <summary>
        /// 初始化 <see cref="FixedLengthFrameBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="encoder">用于编码帧的编码器。</param>
        /// <param name="decoder">用于解码帧的解码器。</param>
        public FixedLengthFrameBuilder(FixedLengthFrameEncoder encoder, FixedLengthFrameDecoder decoder)
            : base(encoder, decoder)
        {
        }
    }

    /// <summary>
    /// 固定长度帧编码器，将数据编码为固定长度的帧。
    /// </summary>
    public sealed class FixedLengthFrameEncoder : IFrameEncoder
    {
        private readonly int _fixedFrameLength;

        /// <summary>
        /// 初始化 <see cref="FixedLengthFrameEncoder"/> 类的新实例。
        /// </summary>
        /// <param name="fixedFrameLength">固定的帧长度。</param>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="fixedFrameLength"/> 小于或等于0时引发。</exception>
        public FixedLengthFrameEncoder(int fixedFrameLength)
        {
            if (fixedFrameLength <= 0)
                throw new ArgumentOutOfRangeException("fixedFrameLength");
            _fixedFrameLength = fixedFrameLength;
        }

        /// <summary>
        /// 获取固定的帧长度。
        /// </summary>
        public int FixedFrameLength { get { return _fixedFrameLength; } }

        /// <summary>
        /// 将指定的数据编码为固定长度的帧格式。
        /// </summary>
        /// <param name="payload">要编码的原始数据。</param>
        /// <param name="offset">数据的起始偏移量。</param>
        /// <param name="count">要编码的数据长度。</param>
        /// <param name="frameBuffer">输出参数，编码后的帧数据缓冲区。</param>
        /// <param name="frameBufferOffset">输出参数，帧数据在缓冲区中的起始偏移量。</param>
        /// <param name="frameBufferLength">输出参数，帧数据的总长度。</param>
        /// <remarks>
        /// 如果数据长度小于固定帧长度，将使用换行符('\n')进行填充。
        /// 如果数据长度大于固定帧长度，将截断到固定长度。
        /// </remarks>
        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            if (count == FixedFrameLength)
            {
                frameBuffer = payload;
                frameBufferOffset = offset;
                frameBufferLength = count;
            }
            else
            {
                var buffer = new byte[FixedFrameLength];
                if (count >= FixedFrameLength)
                {
                    Array.Copy(payload, offset, buffer, 0, FixedFrameLength);
                }
                else
                {
                    Array.Copy(payload, offset, buffer, 0, count);
                    for (int i = 0; i < FixedFrameLength - count; i++)
                    {
                        buffer[count + i] = (byte)'\n';
                    }
                }

                frameBuffer = buffer;
                frameBufferOffset = 0;
                frameBufferLength = buffer.Length;
            }
        }
    }

    /// <summary>
    /// 固定长度帧解码器，从数据中解码出固定长度的帧。
    /// </summary>
    public sealed class FixedLengthFrameDecoder : IFrameDecoder
    {
        private readonly int _fixedFrameLength;

        /// <summary>
        /// 初始化 <see cref="FixedLengthFrameDecoder"/> 类的新实例。
        /// </summary>
        /// <param name="fixedFrameLength">固定的帧长度。</param>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="fixedFrameLength"/> 小于或等于0时引发。</exception>
        public FixedLengthFrameDecoder(int fixedFrameLength)
        {
            if (fixedFrameLength <= 0)
                throw new ArgumentOutOfRangeException("fixedFrameLength");
            _fixedFrameLength = fixedFrameLength;
        }

        /// <summary>
        /// 获取固定的帧长度。
        /// </summary>
        public int FixedFrameLength { get { return _fixedFrameLength; } }

        /// <summary>
        /// 尝试从指定的缓冲区中解码出一个固定长度的帧。
        /// </summary>
        /// <param name="buffer">包含待解码数据的缓冲区。</param>
        /// <param name="offset">缓冲区中数据的起始偏移量。</param>
        /// <param name="count">缓冲区中可用数据的长度。</param>
        /// <param name="frameLength">输出参数，解码的帧的总长度。</param>
        /// <param name="payload">输出参数，解码后的原始数据。</param>
        /// <param name="payloadOffset">输出参数，原始数据在输出缓冲区中的起始偏移量。</param>
        /// <param name="payloadCount">输出参数，原始数据的长度。</param>
        /// <returns>如果缓冲区中有足够的数据形成完整帧则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            if (count < FixedFrameLength)
                return false;

            frameLength = FixedFrameLength;
            payload = buffer;
            payloadOffset = offset;
            payloadCount = FixedFrameLength;
            return true;
        }
    }
}

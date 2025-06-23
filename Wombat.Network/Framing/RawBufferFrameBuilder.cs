namespace Wombat.Network
{
    /// <summary>
    /// 原始缓冲区帧构建器，不对数据进行任何特殊处理，直接传递原始数据。
    /// </summary>
    public sealed class RawBufferFrameBuilder : FrameBuilder
    {
        /// <summary>
        /// 初始化 <see cref="RawBufferFrameBuilder"/> 类的新实例。
        /// </summary>
        public RawBufferFrameBuilder()
            : this(new RawBufferFrameEncoder(), new RawBufferFrameDecoder())
        {
        }

        /// <summary>
        /// 初始化 <see cref="RawBufferFrameBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="encoder">用于编码帧的编码器。</param>
        /// <param name="decoder">用于解码帧的解码器。</param>
        public RawBufferFrameBuilder(RawBufferFrameEncoder encoder, RawBufferFrameDecoder decoder)
            : base(encoder, decoder)
        {
        }
    }

    /// <summary>
    /// 原始缓冲区帧编码器，直接返回原始数据而不进行任何编码处理。
    /// </summary>
    public sealed class RawBufferFrameEncoder : IFrameEncoder
    {
        /// <summary>
        /// 初始化 <see cref="RawBufferFrameEncoder"/> 类的新实例。
        /// </summary>
        public RawBufferFrameEncoder()
        {
        }

        /// <summary>
        /// 直接返回原始数据，不进行任何编码处理。
        /// </summary>
        /// <param name="payload">要编码的原始数据。</param>
        /// <param name="offset">数据的起始偏移量。</param>
        /// <param name="count">要编码的数据长度。</param>
        /// <param name="frameBuffer">输出参数，返回原始数据缓冲区。</param>
        /// <param name="frameBufferOffset">输出参数，返回原始数据的偏移量。</param>
        /// <param name="frameBufferLength">输出参数，返回原始数据的长度。</param>
        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            frameBuffer = payload;
            frameBufferOffset = offset;
            frameBufferLength = count;
        }
    }

    /// <summary>
    /// 原始缓冲区帧解码器，直接返回接收到的所有数据而不进行任何解码处理。
    /// </summary>
    public sealed class RawBufferFrameDecoder : IFrameDecoder
    {
        /// <summary>
        /// 初始化 <see cref="RawBufferFrameDecoder"/> 类的新实例。
        /// </summary>
        public RawBufferFrameDecoder()
        {
        }

        /// <summary>
        /// 直接返回缓冲区中的所有数据，不进行任何解码处理。
        /// </summary>
        /// <param name="buffer">包含待解码数据的缓冲区。</param>
        /// <param name="offset">缓冲区中数据的起始偏移量。</param>
        /// <param name="count">缓冲区中可用数据的长度。</param>
        /// <param name="frameLength">输出参数，返回处理的数据长度。</param>
        /// <param name="payload">输出参数，返回原始数据缓冲区。</param>
        /// <param name="payloadOffset">输出参数，返回数据在缓冲区中的偏移量。</param>
        /// <param name="payloadCount">输出参数，返回数据的长度。</param>
        /// <returns>如果有数据可处理则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            if (count <= 0)
                return false;

            frameLength = count;
            payload = buffer;
            payloadOffset = offset;
            payloadCount = count;
            return true;
        }
    }
}

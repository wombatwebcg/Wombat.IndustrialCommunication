namespace Wombat.Network
{
    /// <summary>
    /// 定义帧编码器的契约，用于将原始数据编码为特定格式的帧。
    /// </summary>
    public interface IFrameEncoder
    {
        /// <summary>
        /// 将指定的数据编码为帧格式。
        /// </summary>
        /// <param name="payload">要编码的原始数据。</param>
        /// <param name="offset">数据的起始偏移量。</param>
        /// <param name="count">要编码的数据长度。</param>
        /// <param name="frameBuffer">输出参数，编码后的帧数据缓冲区。</param>
        /// <param name="frameBufferOffset">输出参数，帧数据在缓冲区中的起始偏移量。</param>
        /// <param name="frameBufferLength">输出参数，帧数据的总长度。</param>
        void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength);
    }
}

namespace Wombat.Network
{
    /// <summary>
    /// 定义帧解码器的契约，用于从接收的数据中解码出原始数据。
    /// </summary>
    public interface IFrameDecoder
    {
        /// <summary>
        /// 尝试从指定的缓冲区中解码出一个完整的帧。
        /// </summary>
        /// <param name="buffer">包含待解码数据的缓冲区。</param>
        /// <param name="offset">缓冲区中数据的起始偏移量。</param>
        /// <param name="count">缓冲区中可用数据的长度。</param>
        /// <param name="frameLength">输出参数，解码的帧的总长度（包括帧头）。</param>
        /// <param name="payload">输出参数，解码后的原始数据。</param>
        /// <param name="payloadOffset">输出参数，原始数据在输出缓冲区中的起始偏移量。</param>
        /// <param name="payloadCount">输出参数，原始数据的长度。</param>
        /// <returns>如果成功解码出完整帧则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount);
    }
}

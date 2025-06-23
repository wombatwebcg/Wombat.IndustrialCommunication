namespace Wombat.Network
{
    /// <summary>
    /// 定义帧构建器的契约，用于封装数据帧的编码和解码功能。
    /// </summary>
    public interface IFrameBuilder
    {
        /// <summary>
        /// 获取帧编码器，用于将数据编码为特定格式的帧。
        /// </summary>
        IFrameEncoder Encoder { get; }
        
        /// <summary>
        /// 获取帧解码器，用于从接收的数据中解码出原始数据。
        /// </summary>
        IFrameDecoder Decoder { get; }
    }
}

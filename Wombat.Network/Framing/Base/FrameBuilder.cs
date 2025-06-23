using System;

namespace Wombat.Network
{
    /// <summary>
    /// 帧构建器的基类实现，封装了帧编码器和解码器的组合。
    /// </summary>
    public class FrameBuilder : IFrameBuilder
    {
        /// <summary>
        /// 初始化 <see cref="FrameBuilder"/> 类的新实例。
        /// </summary>
        /// <param name="encoder">用于编码帧的编码器。</param>
        /// <param name="decoder">用于解码帧的解码器。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="encoder"/> 或 <paramref name="decoder"/> 为 <c>null</c> 时引发。</exception>
        public FrameBuilder(IFrameEncoder encoder, IFrameDecoder decoder)
        {
            if (encoder == null)
                throw new ArgumentNullException("encoder");
            if (decoder == null)
                throw new ArgumentNullException("decoder");

            this.Encoder = encoder;
            this.Decoder = decoder;
        }

        /// <summary>
        /// 获取帧编码器，用于将数据编码为特定格式的帧。
        /// </summary>
        public IFrameEncoder Encoder { get; private set; }
        
        /// <summary>
        /// 获取帧解码器，用于从接收的数据中解码出原始数据。
        /// </summary>
        public IFrameDecoder Decoder { get; private set; }
    }
}

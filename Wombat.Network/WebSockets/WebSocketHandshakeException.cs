using System;

namespace Wombat.Network.WebSockets
{
    /// <summary>
    /// 表示WebSocket握手过程中发生的异常。
    /// </summary>
    [Serializable]
    public class WebSocketHandshakeException : Exception
    {
        /// <summary>
        /// 初始化 <see cref="WebSocketHandshakeException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public WebSocketHandshakeException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 初始化 <see cref="WebSocketHandshakeException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的异常。</param>
        public WebSocketHandshakeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

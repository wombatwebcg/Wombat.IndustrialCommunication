using System;

namespace Wombat.Network.WebSockets
{
    /// <summary>
    /// 表示WebSocket操作中发生的异常。
    /// </summary>
    [Serializable]
    public class WebSocketException : Exception
    {
        /// <summary>
        /// 初始化 <see cref="WebSocketException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public WebSocketException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 初始化 <see cref="WebSocketException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的异常。</param>
        public WebSocketException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

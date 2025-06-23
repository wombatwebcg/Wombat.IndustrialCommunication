using System;

namespace Wombat.Network
{
    /// <summary>
    /// 表示TCP套接字操作中发生的异常。
    /// </summary>
    [Serializable]
    public class TcpSocketException : Exception
    {
        /// <summary>
        /// 初始化 <see cref="TcpSocketException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public TcpSocketException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 初始化 <see cref="TcpSocketException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的异常。</param>
        public TcpSocketException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

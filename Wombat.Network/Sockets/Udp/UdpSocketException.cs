using System;

namespace Wombat.Network
{
    /// <summary>
    /// 表示UDP套接字操作中发生的异常。
    /// </summary>
    [Serializable]
    public class UdpSocketException : Exception
    {
        /// <summary>
        /// 初始化 <see cref="UdpSocketException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public UdpSocketException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 初始化 <see cref="UdpSocketException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的异常。</param>
        public UdpSocketException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
} 
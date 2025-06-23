using System;

namespace Wombat.Network
{
    /// <summary>
    /// 表示无法分配缓冲区时引发的异常。
    /// </summary>
    /// <remarks>
    /// 当缓冲区管理器无法从现有内存段中分配缓冲区时，通常会引发此异常。
    /// 这可能是由于所有缓冲区都已被使用、达到分配限制或重试次数超出限制等原因导致的。
    /// </remarks>
    [Serializable]
    public class UnableToAllocateBufferException : Exception
    {
        /// <summary>
        /// 初始化 <see cref="UnableToAllocateBufferException"/> 类的新实例。
        /// </summary>
        public UnableToAllocateBufferException()
            : base("无法分配缓冲区")
        {
        }

        /// <summary>
        /// 初始化 <see cref="UnableToAllocateBufferException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public UnableToAllocateBufferException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 初始化 <see cref="UnableToAllocateBufferException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的异常。</param>
        public UnableToAllocateBufferException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

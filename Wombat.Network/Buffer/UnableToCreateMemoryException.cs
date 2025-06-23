using System;

namespace Wombat.Network
{
    /// <summary>
    /// 表示无法创建内存时引发的异常。
    /// </summary>
    /// <remarks>
    /// 当缓冲区管理器无法分配或创建新的内存段时，通常会引发此异常。
    /// 这可能是由于内存不足、配置限制或其他内存管理策略导致的。
    /// </remarks>
    [Serializable]
    public class UnableToCreateMemoryException : Exception
    {
        /// <summary>
        /// 初始化 <see cref="UnableToCreateMemoryException"/> 类的新实例。
        /// </summary>
        public UnableToCreateMemoryException()
            : base("无法创建内存")
        {
        }

        /// <summary>
        /// 初始化 <see cref="UnableToCreateMemoryException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public UnableToCreateMemoryException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 初始化 <see cref="UnableToCreateMemoryException"/> 类的新实例。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="innerException">导致当前异常的异常。</param>
        public UnableToCreateMemoryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
} 
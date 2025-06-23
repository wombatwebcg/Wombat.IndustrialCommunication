using System;

namespace Wombat.Network
{
    /// <summary>
    /// 提供缓冲区参数验证的实用工具方法。
    /// </summary>
    /// <remarks>
    /// 此类包含用于验证缓冲区参数有效性的静态方法，
    /// 确保缓冲区操作的安全性并提供清晰的错误信息。
    /// 这些验证方法通常在网络通信的关键路径上使用，以防止缓冲区溢出和无效访问。
    /// </remarks>
    public class BufferValidator
    {
        /// <summary>
        /// 验证缓冲区及其偏移量和计数参数的有效性。
        /// </summary>
        /// <param name="buffer">要验证的字节数组缓冲区。</param>
        /// <param name="offset">缓冲区中的偏移量。</param>
        /// <param name="count">要操作的字节数。</param>
        /// <param name="bufferParameterName">缓冲区参数的名称，用于异常消息中。如果为 <c>null</c>，则使用默认名称 "buffer"。</param>
        /// <param name="offsetParameterName">偏移量参数的名称，用于异常消息中。如果为 <c>null</c>，则使用默认名称 "offset"。</param>
        /// <param name="countParameterName">计数参数的名称，用于异常消息中。如果为 <c>null</c>，则使用默认名称 "count"。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="buffer"/> 为 <c>null</c> 时引发。</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// 当 <paramref name="offset"/> 小于0或大于缓冲区长度时引发，
        /// 或当 <paramref name="count"/> 小于0或大于缓冲区剩余长度时引发。
        /// </exception>
        public static void ValidateBuffer(byte[] buffer, int offset, int count,
            string bufferParameterName = null,
            string offsetParameterName = null,
            string countParameterName = null)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(!string.IsNullOrEmpty(bufferParameterName) ? bufferParameterName : "buffer");
            }

            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(!string.IsNullOrEmpty(offsetParameterName) ? offsetParameterName : "offset");
            }

            if (count < 0 || count > (buffer.Length - offset))
            {
                throw new ArgumentOutOfRangeException(!string.IsNullOrEmpty(countParameterName) ? countParameterName : "count");
            }
        }

        /// <summary>
        /// 验证数组段的有效性。
        /// </summary>
        /// <typeparam name="T">数组段中元素的类型。</typeparam>
        /// <param name="arraySegment">要验证的数组段。</param>
        /// <param name="arraySegmentParameterName">数组段参数的名称，用于异常消息中。如果为 <c>null</c>，则使用默认名称 "arraySegment"。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="arraySegment"/> 的数组为 <c>null</c> 时引发。</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// 当数组段的偏移量小于0或大于数组长度时引发，
        /// 或当数组段的计数小于0或大于数组剩余长度时引发。
        /// </exception>
        public static void ValidateArraySegment<T>(ArraySegment<T> arraySegment, string arraySegmentParameterName = null)
        {
            if (arraySegment.Array == null)
            {
                throw new ArgumentNullException((!string.IsNullOrEmpty(arraySegmentParameterName) ? arraySegmentParameterName : "arraySegment") + ".Array");
            }

            if (arraySegment.Offset < 0 || arraySegment.Offset > arraySegment.Array.Length)
            {
                throw new ArgumentOutOfRangeException((!string.IsNullOrEmpty(arraySegmentParameterName) ? arraySegmentParameterName : "arraySegment") + ".Offset");
            }

            if (arraySegment.Count < 0 || arraySegment.Count > (arraySegment.Array.Length - arraySegment.Offset))
            {
                throw new ArgumentOutOfRangeException((!string.IsNullOrEmpty(arraySegmentParameterName) ? arraySegmentParameterName : "arraySegment") + ".Count");
            }
        }
    }
}

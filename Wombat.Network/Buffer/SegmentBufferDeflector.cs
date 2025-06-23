using System;

namespace Wombat.Network
{
    /// <summary>
    /// 提供分段缓冲区操作的实用工具方法，用于高效管理网络通信中的缓冲区数据。
    /// </summary>
    /// <remarks>
    /// 此类包含用于缓冲区追加、移位和替换操作的静态方法，
    /// 这些操作在网络数据处理中经常需要，特别是在处理分包、组包和缓冲区管理时。
    /// 所有方法都与 <see cref="ISegmentBufferManager"/> 配合使用以实现高效的内存管理。
    /// </remarks>
    public class SegmentBufferDeflector
    {
        /// <summary>
        /// 将接收到的数据追加到会话缓冲区中，必要时自动扩展缓冲区大小。
        /// </summary>
        /// <param name="bufferManager">用于管理缓冲区分配和回收的管理器。</param>
        /// <param name="receiveBuffer">包含新接收数据的缓冲区。</param>
        /// <param name="receiveCount">新接收数据的字节数。</param>
        /// <param name="sessionBuffer">会话缓冲区的引用，可能会被替换为更大的缓冲区。</param>
        /// <param name="sessionBufferCount">会话缓冲区中现有数据的字节数的引用。</param>
        /// <remarks>
        /// 如果会话缓冲区空间不足以容纳新数据，此方法会自动分配一个更大的缓冲区，
        /// 将现有数据复制到新缓冲区中，然后回收旧缓冲区。新缓冲区的大小至少是所需大小的两倍。
        /// </remarks>
        public static void AppendBuffer(
            ISegmentBufferManager bufferManager,
            ref ArraySegment<byte> receiveBuffer,
            int receiveCount,
            ref ArraySegment<byte> sessionBuffer,
            ref int sessionBufferCount)
        {
            if (sessionBuffer.Count < (sessionBufferCount + receiveCount))
            {
                ArraySegment<byte> autoExpandedBuffer = bufferManager.BorrowBuffer();
                if (autoExpandedBuffer.Count < (sessionBufferCount + receiveCount) * 2)
                {
                    bufferManager.ReturnBuffer(autoExpandedBuffer);
                    autoExpandedBuffer = new ArraySegment<byte>(new byte[(sessionBufferCount + receiveCount) * 2]);
                }

                Array.Copy(sessionBuffer.Array, sessionBuffer.Offset, autoExpandedBuffer.Array, autoExpandedBuffer.Offset, sessionBufferCount);

                var discardBuffer = sessionBuffer;
                sessionBuffer = autoExpandedBuffer;
                bufferManager.ReturnBuffer(discardBuffer);
            }

            Array.Copy(receiveBuffer.Array, receiveBuffer.Offset, sessionBuffer.Array, sessionBuffer.Offset + sessionBufferCount, receiveCount);
            sessionBufferCount = sessionBufferCount + receiveCount;
        }

        /// <summary>
        /// 将会话缓冲区中的数据向左移位，移除指定位置之前的数据。
        /// </summary>
        /// <param name="bufferManager">用于管理缓冲区分配和回收的管理器。</param>
        /// <param name="shiftStart">移位起始位置，此位置之前的数据将被移除。</param>
        /// <param name="sessionBuffer">要进行移位操作的会话缓冲区的引用。</param>
        /// <param name="sessionBufferCount">会话缓冲区中数据字节数的引用。</param>
        /// <remarks>
        /// 此方法用于在处理完部分数据后，将剩余的未处理数据移动到缓冲区的开始位置。
        /// 如果需要移动的数据量较小，则直接在原缓冲区中移动；否则使用临时缓冲区进行复制操作。
        /// </remarks>
        public static void ShiftBuffer(
            ISegmentBufferManager bufferManager,
            int shiftStart,
            ref ArraySegment<byte> sessionBuffer,
            ref int sessionBufferCount)
        {
            if ((sessionBufferCount - shiftStart) < shiftStart)
            {
                Array.Copy(sessionBuffer.Array, sessionBuffer.Offset + shiftStart, sessionBuffer.Array, sessionBuffer.Offset, sessionBufferCount - shiftStart);
                sessionBufferCount = sessionBufferCount - shiftStart;
            }
            else
            {
                ArraySegment<byte> copyBuffer = bufferManager.BorrowBuffer();
                if (copyBuffer.Count < (sessionBufferCount - shiftStart))
                {
                    bufferManager.ReturnBuffer(copyBuffer);
                    copyBuffer = new ArraySegment<byte>(new byte[sessionBufferCount - shiftStart]);
                }

                Array.Copy(sessionBuffer.Array, sessionBuffer.Offset + shiftStart, copyBuffer.Array, copyBuffer.Offset, sessionBufferCount - shiftStart);
                Array.Copy(copyBuffer.Array, copyBuffer.Offset, sessionBuffer.Array, sessionBuffer.Offset, sessionBufferCount - shiftStart);
                sessionBufferCount = sessionBufferCount - shiftStart;

                bufferManager.ReturnBuffer(copyBuffer);
            }
        }

        /// <summary>
        /// 替换接收缓冲区，必要时扩展缓冲区大小以容纳更多数据。
        /// </summary>
        /// <param name="bufferManager">用于管理缓冲区分配和回收的管理器。</param>
        /// <param name="receiveBuffer">接收缓冲区的引用，可能会被替换为更大的缓冲区。</param>
        /// <param name="receiveBufferOffset">接收缓冲区中数据偏移量的引用。</param>
        /// <param name="receiveCount">新接收的数据字节数。</param>
        /// <remarks>
        /// 如果当前接收缓冲区的剩余空间不足以容纳新数据，此方法会分配一个更大的缓冲区，
        /// 将现有数据复制到新缓冲区中，然后回收旧缓冲区。新缓冲区的大小至少是所需大小的两倍。
        /// </remarks>
        public static void ReplaceBuffer(
            ISegmentBufferManager bufferManager,
            ref ArraySegment<byte> receiveBuffer,
            ref int receiveBufferOffset,
            int receiveCount)
        {
            if ((receiveBufferOffset + receiveCount) < receiveBuffer.Count)
            {
                receiveBufferOffset = receiveBufferOffset + receiveCount;
            }
            else
            {
                ArraySegment<byte> autoExpandedBuffer = bufferManager.BorrowBuffer();
                if (autoExpandedBuffer.Count < (receiveBufferOffset + receiveCount) * 2)
                {
                    bufferManager.ReturnBuffer(autoExpandedBuffer);
                    autoExpandedBuffer = new ArraySegment<byte>(new byte[(receiveBufferOffset + receiveCount) * 2]);
                }

                Array.Copy(receiveBuffer.Array, receiveBuffer.Offset, autoExpandedBuffer.Array, autoExpandedBuffer.Offset, receiveBufferOffset + receiveCount);
                receiveBufferOffset = receiveBufferOffset + receiveCount;

                var discardBuffer = receiveBuffer;
                receiveBuffer = autoExpandedBuffer;
                bufferManager.ReturnBuffer(discardBuffer);
            }
        }
    }
}

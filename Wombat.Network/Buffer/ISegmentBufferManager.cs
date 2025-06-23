using System;
using System.Collections.Generic;

namespace Wombat.Network
{
    /// <summary>
    /// 定义分段缓冲区管理器的契约，用于高效管理网络通信中的内存缓冲区。
    /// </summary>
    /// <remarks>
    /// 分段缓冲区管理器通过维护大块内存段并将其分割为较小的缓冲区来提高内存管理效率，
    /// 减少频繁的内存分配和回收，特别适用于高并发的网络通信场景。
    /// </remarks>
    public interface ISegmentBufferManager
    {
        /// <summary>
        /// 获取缓冲区块的大小（以字节为单位）。
        /// </summary>
        int ChunkSize { get; }

        /// <summary>
        /// 借用一个缓冲区块。
        /// </summary>
        /// <returns>可用的缓冲区块。</returns>
        /// <exception cref="InvalidOperationException">当无法分配缓冲区时可能引发此异常。</exception>
        ArraySegment<byte> BorrowBuffer();

        /// <summary>
        /// 借用指定数量的缓冲区块。
        /// </summary>
        /// <param name="count">要借用的缓冲区块数量。</param>
        /// <returns>包含指定数量缓冲区块的集合。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="count"/> 小于或等于0时引发。</exception>
        /// <exception cref="InvalidOperationException">当无法分配足够的缓冲区时可能引发此异常。</exception>
        IEnumerable<ArraySegment<byte>> BorrowBuffers(int count);

        /// <summary>
        /// 归还一个缓冲区块，使其可以被重新使用。
        /// </summary>
        /// <param name="buffer">要归还的缓冲区块。</param>
        void ReturnBuffer(ArraySegment<byte> buffer);

        /// <summary>
        /// 归还多个缓冲区块，使其可以被重新使用。
        /// </summary>
        /// <param name="buffers">要归还的缓冲区块集合。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="buffers"/> 为 <c>null</c> 时引发。</exception>
        void ReturnBuffers(IEnumerable<ArraySegment<byte>> buffers);

        /// <summary>
        /// 归还多个缓冲区块，使其可以被重新使用。
        /// </summary>
        /// <param name="buffers">要归还的缓冲区块数组。</param>
        void ReturnBuffers(params ArraySegment<byte>[] buffers);
    }
}

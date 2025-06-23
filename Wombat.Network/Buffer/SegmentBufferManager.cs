using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Wombat.Network
{
    /// <summary>
    /// A manager to handle buffers for the socket connections.
    /// </summary>
    /// <remarks>
    /// When used in an async call a buffer is pinned. Large numbers of pinned buffers
    /// cause problem with the GC (in particular it causes heap fragmentation).
    /// This class maintains a set of large segments and gives clients pieces of these
    /// segments that they can use for their buffers. The alternative to this would be to
    /// create many small arrays which it then maintained. This methodology should be slightly
    /// better than the many small array methodology because in creating only a few very
    /// large objects it will force these objects to be placed on the LOH. Since the
    /// objects are on the LOH they are at this time not subject to compacting which would
    /// require an update of all GC roots as would be the case with lots of smaller arrays
    /// that were in the normal heap.
    /// </remarks>
    public class SegmentBufferManager : ISegmentBufferManager
    {
        private const int TrialsCount = 100;

        private static SegmentBufferManager _defaultBufferManager;

        private readonly int _segmentChunks;
        private readonly int _chunkSize;
        private readonly int _segmentSize;
        private readonly bool _allowedToCreateMemory;

        private readonly ConcurrentStack<ArraySegment<byte>> _buffers = new ConcurrentStack<ArraySegment<byte>>();

        private readonly List<byte[]> _segments;
        private readonly object _creatingNewSegmentLock = new object();

        /// <summary>
        /// 获取默认的分段缓冲区管理器实例。
        /// </summary>
        /// <value>默认配置为1024个1KB缓冲区的管理器实例。</value>
        public static SegmentBufferManager Default
        {
            get
            {
                // default to 1024 1kb buffers if people don't want to manage it on their own;
                if (_defaultBufferManager == null)
                    _defaultBufferManager = new SegmentBufferManager(1024, 1024, 1);
                return _defaultBufferManager;
            }
        }

        /// <summary>
        /// 设置默认的分段缓冲区管理器。
        /// </summary>
        /// <param name="manager">要设置的管理器实例。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="manager"/> 为 <c>null</c> 时引发。</exception>
        public static void SetDefaultBufferManager(SegmentBufferManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");
            _defaultBufferManager = manager;
        }

        /// <summary>
        /// 获取每个缓冲区块的大小（以字节为单位）。
        /// </summary>
        public int ChunkSize
        {
            get { return _chunkSize; }
        }

        /// <summary>
        /// 获取当前内存段的数量。
        /// </summary>
        public int SegmentsCount
        {
            get { return _segments.Count; }
        }

        /// <summary>
        /// 获取每个内存段包含的缓冲区块数量。
        /// </summary>
        public int SegmentChunksCount
        {
            get { return _segmentChunks; }
        }

        /// <summary>
        /// 获取当前可用的缓冲区块数量。
        /// </summary>
        public int AvailableBuffers
        {
            get { return _buffers.Count; }
        }

        /// <summary>
        /// 获取所有内存段的总大小（以字节为单位）。
        /// </summary>
        public int TotalBufferSize
        {
            get { return _segments.Count * _segmentSize; }
        }

        /// <summary>
        /// 初始化 <see cref="SegmentBufferManager"/> 类的新实例。
        /// </summary>
        /// <param name="segmentChunks">每个内存段包含的缓冲区块数量。</param>
        /// <param name="chunkSize">每个缓冲区块的大小（以字节为单位）。</param>
        public SegmentBufferManager(int segmentChunks, int chunkSize)
            : this(segmentChunks, chunkSize, 1) { }

        /// <summary>
        /// 初始化 <see cref="SegmentBufferManager"/> 类的新实例。
        /// </summary>
        /// <param name="segmentChunks">每个内存段包含的缓冲区块数量。</param>
        /// <param name="chunkSize">每个缓冲区块的大小（以字节为单位）。</param>
        /// <param name="initialSegments">初始创建的内存段数量。</param>
        public SegmentBufferManager(int segmentChunks, int chunkSize, int initialSegments)
            : this(segmentChunks, chunkSize, initialSegments, true) { }

        /// <summary>
        /// 初始化 <see cref="SegmentBufferManager"/> 类的新实例。
        /// </summary>
        /// <param name="segmentChunks">每个内存段包含的缓冲区块数量。</param>
        /// <param name="chunkSize">每个缓冲区块的大小（以字节为单位）。</param>
        /// <param name="initialSegments">初始创建的内存段数量。</param>
        /// <param name="allowedToCreateMemory">指示当缓冲区不足时是否允许创建新的内存段。如果为 <c>false</c>，则在缓冲区不足时会引发异常。</param>
        /// <exception cref="ArgumentException">当 <paramref name="segmentChunks"/>、<paramref name="chunkSize"/> 小于或等于0，或 <paramref name="initialSegments"/> 小于0时引发。</exception>
        public SegmentBufferManager(int segmentChunks, int chunkSize, int initialSegments, bool allowedToCreateMemory)
        {
            if (segmentChunks <= 0)
                throw new ArgumentException("segmentChunks");
            if (chunkSize <= 0)
                throw new ArgumentException("chunkSize");
            if (initialSegments < 0)
                throw new ArgumentException("initialSegments");

            _segmentChunks = segmentChunks;
            _chunkSize = chunkSize;
            _segmentSize = _segmentChunks * _chunkSize;

            _segments = new List<byte[]>();

            _allowedToCreateMemory = true;
            for (int i = 0; i < initialSegments; i++)
            {
                CreateNewSegment(true);
            }
            _allowedToCreateMemory = allowedToCreateMemory;
        }

        /// <summary>
        /// 创建新的内存段并将其分割为缓冲区块。
        /// </summary>
        /// <param name="forceCreation">指示是否强制创建新段，即使当前可用缓冲区足够。</param>
        /// <exception cref="UnableToCreateMemoryException">当不允许创建内存时引发。</exception>
        private void CreateNewSegment(bool forceCreation)
        {
            if (!_allowedToCreateMemory)
                throw new UnableToCreateMemoryException();

            lock (_creatingNewSegmentLock)
            {
                if (!forceCreation && _buffers.Count > _segmentChunks / 2)
                    return;

                var bytes = new byte[_segmentSize];
                _segments.Add(bytes);
                for (int i = 0; i < _segmentChunks; i++)
                {
                    var chunk = new ArraySegment<byte>(bytes, i * _chunkSize, _chunkSize);
                    _buffers.Push(chunk);
                }
            }
        }

        /// <summary>
        /// 借用一个缓冲区块。
        /// </summary>
        /// <returns>可用的缓冲区块。</returns>
        /// <exception cref="UnableToAllocateBufferException">当经过多次尝试后仍无法分配缓冲区时引发。</exception>
        public ArraySegment<byte> BorrowBuffer()
        {
            int trial = 0;
            while (trial < TrialsCount)
            {
                ArraySegment<byte> result;
                if (_buffers.TryPop(out result))
                    return result;
                CreateNewSegment(false);
                trial++;
            }
            throw new UnableToAllocateBufferException();
        }

        /// <summary>
        /// 借用指定数量的缓冲区块。
        /// </summary>
        /// <param name="count">要借用的缓冲区块数量。</param>
        /// <returns>包含指定数量缓冲区块的集合。</returns>
        /// <exception cref="UnableToAllocateBufferException">当经过多次尝试后仍无法分配足够缓冲区时引发。</exception>
        public IEnumerable<ArraySegment<byte>> BorrowBuffers(int count)
        {
            var result = new ArraySegment<byte>[count];
            var trial = 0;
            var totalReceived = 0;

            try
            {
                while (trial < TrialsCount)
                {
                    ArraySegment<byte> piece;
                    while (totalReceived < count)
                    {
                        if (!_buffers.TryPop(out piece))
                            break;
                        result[totalReceived] = piece;
                        ++totalReceived;
                    }
                    if (totalReceived == count)
                        return result;
                    CreateNewSegment(false);
                    trial++;
                }
                throw new UnableToAllocateBufferException();
            }
            catch
            {
                if (totalReceived > 0)
                    ReturnBuffers(result.Take(totalReceived));
                throw;
            }
        }

        /// <summary>
        /// 归还一个缓冲区块，使其可以被重新使用。
        /// </summary>
        /// <param name="buffer">要归还的缓冲区块。</param>
        public void ReturnBuffer(ArraySegment<byte> buffer)
        {
            if (ValidateBuffer(buffer))
            {
                _buffers.Push(buffer);
            }
        }

        /// <summary>
        /// 归还多个缓冲区块，使其可以被重新使用。
        /// </summary>
        /// <param name="buffers">要归还的缓冲区块集合。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="buffers"/> 为 <c>null</c> 时引发。</exception>
        public void ReturnBuffers(IEnumerable<ArraySegment<byte>> buffers)
        {
            if (buffers == null)
                throw new ArgumentNullException("buffers");

            foreach (var buf in buffers)
            {
                if (ValidateBuffer(buf))
                {
                    _buffers.Push(buf);
                }
            }
        }

        /// <summary>
        /// 归还多个缓冲区块，使其可以被重新使用。
        /// </summary>
        /// <param name="buffers">要归还的缓冲区块数组。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="buffers"/> 为 <c>null</c> 时引发。</exception>
        public void ReturnBuffers(params ArraySegment<byte>[] buffers)
        {
            if (buffers == null)
                throw new ArgumentNullException("buffers");

            foreach (var buf in buffers)
            {
                if (ValidateBuffer(buf))
                {
                    _buffers.Push(buf);
                }
            }
        }

        /// <summary>
        /// 验证缓冲区块是否有效。
        /// </summary>
        /// <param name="buffer">要验证的缓冲区块。</param>
        /// <returns>如果缓冲区块有效则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        private bool ValidateBuffer(ArraySegment<byte> buffer)
        {
            if (buffer.Array == null || buffer.Count == 0 || buffer.Array.Length < buffer.Offset + buffer.Count)
                return false;

            if (buffer.Count != _chunkSize)
                return false;

            return true;
        }
    }
}

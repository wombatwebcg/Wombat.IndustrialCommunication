using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Wombat.Extensions.DataTypeExtensions;


namespace Wombat.IndustrialCommunication
{
    public class MemoryLite<T>
    {
        private readonly ReaderWriterLockSlim[] _locks; // 对应的分段锁
        private volatile int _size; // 总大小
        private Memory<T> _memory;
        private readonly int _segmentCount;



        public MemoryLite(T[] source,int start,int length , int numberOfSegments = 4)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            var totalSize = source.Length;
            if (totalSize <= 0)
            {
                throw new ArgumentException("Total size must be greater than zero.");
            }

            if (numberOfSegments <= 0 || numberOfSegments > totalSize)
            {
                numberOfSegments = 1;
            }

            _size = totalSize;
            _locks = new ReaderWriterLockSlim[numberOfSegments];
            _memory = new Memory<T>(source,start,length);
            _segmentCount = numberOfSegments;
            for (int i = 0; i < numberOfSegments; i++)
            {
                _locks[i] = new ReaderWriterLockSlim(); // 初始化每个段的锁
            }

        }

        public MemoryLite(int totalSize, int numberOfSegments = 4)
        {
            if (totalSize <= 0)
            {
                throw new ArgumentException("Total size must be greater than zero.");
            }

            if (numberOfSegments <= 0 || numberOfSegments > totalSize)
            {
                numberOfSegments = 1;
            }

            _size = totalSize;
            _memory = new Memory<T>(new T[totalSize]);
            _locks = new ReaderWriterLockSlim[numberOfSegments];
            _segmentCount = numberOfSegments;
            for (int i = 0; i < numberOfSegments; i++)
            {
                _locks[i] = new ReaderWriterLockSlim(); // 初始化每个段的锁
            }
        }



        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _size)
                    throw new ArgumentOutOfRangeException(nameof(index));
                int segmentIndex = GetSegmentIndex(index);
                LockSegment(segmentIndex);
                try
                {
                    return _memory.Span[index];
                }
                finally
                {
                    UnlockSegment(segmentIndex);
                }
            }
            set
            {
                if (index < 0 || index >= _size)
                    throw new ArgumentOutOfRangeException(nameof(index));

                int segmentIndex = GetSegmentIndex(index);
                LockSegment(segmentIndex);
                try
                {
                    _memory.Span[index] = value;
                }
                finally
                {
                    UnlockSegment(segmentIndex);
                }
            }
        }

        private void LockSegment(int index)
        {
            Monitor.Enter(_locks[GetSegmentIndex(index)]);
        }

        private void UnlockSegment(int index)
        {
            Monitor.Exit(_locks[GetSegmentIndex(index)]);
        }

        /// <summary>
        /// 通过索引找到对应的段
        /// </summary>
        private int GetSegmentIndex(int index)
        {
            return index % _segmentCount;
        }

        /// <summary>
        /// 安全读取指定位置的数据
        /// </summary>
        public T Read(int index)
        {
            if (index >= _size)
            {
                throw new IndexOutOfRangeException();
            }

            int segmentIndex = GetSegmentIndex(index);
            _locks[segmentIndex].EnterReadLock();
            try
            {
                return _memory.Span[index];
            }
            finally
            {
                _locks[segmentIndex].ExitReadLock();
            }
        }

        /// <summary>
        /// 安全写入指定位置的数据
        /// </summary>
        public void Write(int index, T value)
        {
            if (index >= _size)
            {
                throw new IndexOutOfRangeException();
            }

            int segmentIndex = GetSegmentIndex(index);

            _locks[segmentIndex].EnterWriteLock();
            try
            {
                _memory.Span[index] = value;
            }
            finally
            {
                _locks[segmentIndex].ExitWriteLock();
            }
        }

        public Memory<T> GetMemory()
        {
            return _memory;
        }


        public Memory<T> Slice(int index, int length)
        {
            return _memory.Slice(index, length);
        }


        public Memory<T> Slice(int index)
        {
            return _memory.Slice(index, 1);
        }

        ///// <summary>
        ///// 扩展内存大小
        ///// 注意：这是一个简化示例，扩展时整个内存会重新分段并锁定，较为昂贵
        ///// </summary>
        //public void Resize(int newSize)
        //{
        //    if (newSize <= _totalSize)
        //    {
        //        throw new ArgumentException("New size must be larger than the current size.");
        //    }

        //    // 对所有段加写锁，避免并发冲突
        //    foreach (var rwLock in _locks)
        //    {
        //        rwLock.EnterWriteLock();
        //    }

        //    try
        //    {
        //        // 扩展逻辑，重新分配内存和段
        //        int newSegmentCount = (_totalSize + _locks.Length - 1) / _locks.Length;
        //        var newSegments = new Memory<T>[newSegmentCount];

        //        // 复制旧数据
        //        for (int i = 0; i < _segments.Length; i++)
        //        {
        //            _segments[i].CopyTo(newSegments[i]);
        //        }

        //        // 替换
        //        _segments = newSegments;
        //    }
        //    finally
        //    {
        //        foreach (var rwLock in _locks)
        //        {
        //            rwLock.ExitWriteLock();
        //        }
        //    }
        //}

        /// <summary>
        /// 获取内存总大小
        /// </summary>
        public int Size
        {
            get { return _size; }
        }

        /// <summary>
        /// 释放锁，清理资源
        /// </summary>
        public void Dispose()
        {
            foreach (var rwLock in _locks)
            {
                rwLock.Dispose();
            }
        }


    }


}

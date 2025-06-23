using System;
using System.Threading;

namespace Wombat.Network.Sockets
{
    /// <summary>
    /// 高效的线程安全循环缓冲区，用于模仿原生Socket的接收缓冲区行为
    /// </summary>
    public class CircularBuffer
    {
        private readonly byte[] _buffer;
        private readonly int _capacity;
        private readonly object _lock = new object();
        
        private int _head = 0;      // 写入位置
        private int _tail = 0;      // 读取位置
        private int _count = 0;     // 当前数据量
        private bool _isFull = false;

        /// <summary>
        /// 初始化循环缓冲区
        /// </summary>
        /// <param name="capacity">缓冲区容量</param>
        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than zero", nameof(capacity));
                
            _capacity = capacity;
            _buffer = new byte[capacity];
        }

        /// <summary>
        /// 缓冲区容量
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// 当前可用数据字节数
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        /// <summary>
        /// 剩余可写入空间
        /// </summary>
        public int Available
        {
            get
            {
                lock (_lock)
                {
                    return _capacity - _count;
                }
            }
        }

        /// <summary>
        /// 缓冲区是否为空
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                lock (_lock)
                {
                    return _count == 0;
                }
            }
        }

        /// <summary>
        /// 缓冲区是否已满
        /// </summary>
        public bool IsFull
        {
            get
            {
                lock (_lock)
                {
                    return _isFull;
                }
            }
        }

        /// <summary>
        /// 获取缓冲区使用率（百分比）
        /// </summary>
        public double Usage
        {
            get
            {
                lock (_lock)
                {
                    return (_count / (double)_capacity) * 100.0;
                }
            }
        }

        /// <summary>
        /// 向缓冲区写入数据
        /// </summary>
        /// <param name="data">要写入的数据</param>
        /// <param name="offset">数据偏移量</param>
        /// <param name="count">要写入的字节数</param>
        /// <returns>实际写入的字节数</returns>
        public int Write(byte[] data, int offset, int count)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset >= data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0)
                return 0;

            lock (_lock)
            {
                // 计算实际可写入的字节数
                int available = _capacity - _count;
                int toWrite = Math.Min(count, available);
                
                if (toWrite == 0)
                    return 0; // 缓冲区已满

                // 写入数据
                for (int i = 0; i < toWrite; i++)
                {
                    _buffer[_head] = data[offset + i];
                    _head = (_head + 1) % _capacity;
                }

                _count += toWrite;
                _isFull = _count == _capacity;

                return toWrite;
            }
        }

        /// <summary>
        /// 从缓冲区读取数据
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">目标缓冲区偏移量</param>
        /// <param name="count">要读取的字节数</param>
        /// <returns>实际读取的字节数</returns>
        public int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0)
                return 0;

            lock (_lock)
            {
                // 计算实际可读取的字节数
                int toRead = Math.Min(count, _count);
                
                if (toRead == 0)
                    return 0; // 无数据可读

                // 读取数据
                for (int i = 0; i < toRead; i++)
                {
                    buffer[offset + i] = _buffer[_tail];
                    _tail = (_tail + 1) % _capacity;
                }

                _count -= toRead;
                _isFull = false;

                return toRead;
            }
        }

        /// <summary>
        /// 查看数据但不移除（类似于原生Socket的MSG_PEEK）
        /// </summary>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">目标缓冲区偏移量</param>
        /// <param name="count">要查看的字节数</param>
        /// <returns>实际查看的字节数</returns>
        public int Peek(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0)
                return 0;

            lock (_lock)
            {
                // 计算实际可查看的字节数
                int toPeek = Math.Min(count, _count);
                
                if (toPeek == 0)
                    return 0; // 无数据可查看

                // 查看数据但不移动tail指针
                int tempTail = _tail;
                for (int i = 0; i < toPeek; i++)
                {
                    buffer[offset + i] = _buffer[tempTail];
                    tempTail = (tempTail + 1) % _capacity;
                }

                return toPeek;
            }
        }

        /// <summary>
        /// 获取所有可用数据的副本
        /// </summary>
        /// <returns>包含所有数据的字节数组</returns>
        public byte[] GetAllData()
        {
            lock (_lock)
            {
                if (_count == 0)
                    return new byte[0];

                byte[] result = new byte[_count];
                int tempTail = _tail;
                
                for (int i = 0; i < _count; i++)
                {
                    result[i] = _buffer[tempTail];
                    tempTail = (tempTail + 1) % _capacity;
                }

                return result;
            }
        }

        /// <summary>
        /// 读取所有数据并清空缓冲区
        /// </summary>
        /// <returns>包含所有数据的字节数组</returns>
        public byte[] ReadAll()
        {
            lock (_lock)
            {
                if (_count == 0)
                    return new byte[0];

                byte[] result = new byte[_count];
                
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = _buffer[_tail];
                    _tail = (_tail + 1) % _capacity;
                }

                _count = 0;
                _isFull = false;

                return result;
            }
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _head = 0;
                _tail = 0;
                _count = 0;
                _isFull = false;
            }
        }

        /// <summary>
        /// 尝试写入数据，如果缓冲区满则丢弃旧数据
        /// </summary>
        /// <param name="data">要写入的数据</param>
        /// <param name="offset">数据偏移量</param>
        /// <param name="count">要写入的字节数</param>
        /// <returns>实际写入的字节数</returns>
        public int WriteOverwrite(byte[] data, int offset, int count)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset >= data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > data.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0)
                return 0;

            lock (_lock)
            {
                int toWrite = Math.Min(count, _capacity);
                
                // 如果写入的数据量大于可用空间，移动tail指针以腾出空间
                int needed = toWrite;
                int available = _capacity - _count;
                
                if (needed > available)
                {
                    int toDiscard = needed - available;
                    _tail = (_tail + toDiscard) % _capacity;
                    _count -= toDiscard;
                }

                // 写入数据
                for (int i = 0; i < toWrite; i++)
                {
                    _buffer[_head] = data[offset + i];
                    _head = (_head + 1) % _capacity;
                }

                _count += toWrite;
                _isFull = _count == _capacity;

                return toWrite;
            }
        }
    }
} 
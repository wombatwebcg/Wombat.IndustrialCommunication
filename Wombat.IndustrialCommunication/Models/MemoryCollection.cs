
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;



namespace Wombat.IndustrialCommunication
{

    public class MemoryCollection<T>
    {
        private Memory<T> _memory;
        internal volatile int _count;
        private readonly object[] _locks;
        private readonly int _segmentCount;

        public MemoryCollection(int capacity, int segmentCount = 4)
        {
            _memory = new Memory<T>(new T[capacity]);
            _count = 0;
            _segmentCount = segmentCount;
            _locks = new object[_segmentCount];
            for (int i = 0; i < _segmentCount; i++)
            {
                _locks[i] = new object();
            }
        }


        public MemoryCollection(IList<T> data, int segmentCount = 4)
        {
            if (data == null)
                throw new ArgumentException("The input list cannot be null.");

            _memory = new Memory<T>(new T[data.Count]);
            _count = data.Count;

            _segmentCount = segmentCount;
            _locks = new object[_segmentCount];
            for (int i = 0; i < _segmentCount; i++)
            {
                _locks[i] = new object();
            }

            if (data is T[] array)
            {
                array.AsSpan().CopyTo(_memory.Span);
            }
            else if (data is List<T> list)
            {
                list.ToArray().AsSpan().CopyTo(_memory.Span);

            }
            else
            {
                for (int i = 0; i < data.Count; i++)
                {
                    _memory.Span[i] = data[i];
                }
            }
        }


        public int Count => Interlocked.CompareExchange(ref _count, 0, 0);

        public int Capacity => _memory.Length;

        private int GetSegment(int index)
        {
            return index % _segmentCount;
        }

        private void LockSegment(int index)
        {
            Monitor.Enter(_locks[GetSegment(index)]);
        }

        private void UnlockSegment(int index)
        {
            Monitor.Exit(_locks[GetSegment(index)]);
        }

        private void LockAllSegments()
        {
            for (int i = 0; i < _segmentCount; i++)
            {
                Monitor.Enter(_locks[i]);
            }
        }

        private void UnlockAllSegments()
        {
            for (int i = 0; i < _segmentCount; i++)
            {
                Monitor.Exit(_locks[i]);
            }
        }
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                LockSegment(index);
                try
                {
                    return _memory.Span[index];
                }
                finally
                {
                    UnlockSegment(index);
                }
            }
            set
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                LockSegment(index);
                try
                {
                    _memory.Span[index] = value;
                }
                finally
                {
                    UnlockSegment(index);
                }
            }
        }

        protected virtual void ClearItems()
        {
            LockAllSegments();
            try
            {
                _memory.Span.Slice(0, _count).Clear();
                _count = 0;
            }
            finally
            {
                UnlockAllSegments();
            }
        }

        protected virtual void InsertItem(int index, T item)
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            LockSegment(index);
            try
            {
                _memory.Span[index] = item;
            }
            finally
            {
                UnlockSegment(index);
            }
        }

        protected virtual void RemoveItem(int index)
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            LockSegment(index);
            try
            {
                for (int i = index; i < _count - 1; i++)
                {
                    LockSegment(i + 1); // 锁定下一个部分
                    try
                    {
                        _memory.Span[i] = _memory.Span[i + 1];
                    }
                    finally
                    {
                        UnlockSegment(i + 1);
                    }
                }

                Interlocked.Decrement(ref _count);
            }
            finally
            {
                UnlockSegment(index);
            }
        }

        protected virtual void SetItem(int index, T item)
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            LockSegment(index);
            try
            {
                _memory.Span[index] = item;
            }
            finally
            {
                UnlockSegment(index);
            }
        }

        public void Add(T item)
        {
            int newCount = Interlocked.Increment(ref _count) - 1;
            if (newCount >= _memory.Length)
            {
                Interlocked.Decrement(ref _count);
                throw new InvalidOperationException("Memory is full.");
            }

            LockSegment(newCount);
            try
            {
                _memory.Span[newCount] = item;
            }
            finally
            {
                UnlockSegment(newCount);

            }
        }

        public async Task AddAsync(T item)
        {
            await Task.Run(() => Add(item));
        }

        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _count)
                return false;

            LockSegment(index);
            try
            {
                for (int i = index; i < _count - 1; i++)
                {
                    LockSegment(i + 1);  // 锁定下一个部分
                    try
                    {
                        _memory.Span[i] = _memory.Span[i + 1];
                    }
                    finally
                    {
                        UnlockSegment(i + 1);
                    }
                }

                Interlocked.Decrement(ref _count);
                return true;
            }
            finally
            {
                UnlockSegment(index);
            }
        }

        public async Task<bool> RemoveAtAsync(int index)
        {
            return await Task.Run(() => RemoveAt(index));
        }


        public Span<T> Slice(int index, int size)
        {
            if (index < 0 || index + size >= _count || index > _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _memory.Span.Slice(index, size);
        }

        public void Clear()
        {
            for (int i = 0; i < _segmentCount; i++)
            {
                Monitor.Enter(_locks[i]);
            }

            try
            {
                _memory.Span.Slice(0, _count).Clear();
                _count = 0;
            }
            finally
            {
                for (int i = 0; i < _segmentCount; i++)
                {
                    Monitor.Exit(_locks[i]);
                }
            }
        }

        public async Task ClearAsync()
        {
            await Task.Run(() => Clear());
        }

        public bool Contains(T item)
        {
            for (int i = 0; i < _count; i++)
            {
                LockSegment(i);
                try
                {
                    if (_memory.Span[i]?.Equals(item) == true)
                        return true;
                }
                finally
                {
                    UnlockSegment(i);
                }
            }
            return false;
        }

        public async Task<bool> ContainsAsync(T item)
        {
            return await Task.Run(() => Contains(item));
        }

        public T[] ToArray()
        {
            var result = new T[_count];

            for (int i = 0; i < _count; i++)
            {
                LockSegment(i);
                try
                {
                    result[i] = _memory.Span[i];
                }
                finally
                {
                    UnlockSegment(i);
                }
            }
            return result;
        }

        public async Task<T[]> ToArrayAsync()
        {
            return await Task.Run(() => ToArray());
        }
    }
}

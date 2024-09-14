
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;



namespace Wombat.IndustrialCommunication
{

    public class MemoryDictionary<T>: MemoryCollection<T>
    {
        private Memory<T> _memory;
        private readonly object[] _locks;
        private readonly int _segmentCount;
        private readonly ConcurrentDictionary<string, int> _nameIndexMap = new ConcurrentDictionary<string, int>();

        public MemoryDictionary(int capacity, int segmentCount = 4):base(capacity,segmentCount)
        {

        }

        public MemoryDictionary(IList<T> data, int segmentCount = 4) : this(data.Count, segmentCount)
        {
        }


        public T this[string name]
        {
            get
            {
                if (_nameIndexMap.TryGetValue(name, out int index))
                {
                    return this[index];
                }
                throw new KeyNotFoundException($"The name '{name}' was not found.");
            }
            set
            {
                if (_nameIndexMap.TryGetValue(name, out int index))
                {
                    this[index] = value;
                }
                else
                {
                    throw new KeyNotFoundException($"The name '{name}' was not found.");
                }
            }
        }

        volatile int count1 =0;
        public void Add(string name, T item)
        {
            Add(item);
            int newCount = Interlocked.Increment(ref _count) - 1;
            _nameIndexMap[name] = newCount;
        }

        public bool RemoveByName(string name)
        {
            if (_nameIndexMap.TryRemove(name, out int index))
            {
                return RemoveAt(index);
            }
            return false;
        }

        public bool ContainsName(string name)
        {
            return _nameIndexMap.ContainsKey(name);
        }

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

    }
}

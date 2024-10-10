using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace Wombat.IndustrialCommunication.Models
{

    public class PLCMemory<TData, TStatus>
    {
        private readonly int _segmentCount;  // 段的数量
        private readonly ConcurrentDictionary<int, TData>[] _dataSegments;  // 每段存储PLC数据
        private readonly ConcurrentDictionary<int, TStatus>[] _statusSegments;  // 每段存储PLC的IO状态
        private readonly ReaderWriterLockSlim[] _locks;  // 每段有一个独立的读写锁

        // 构造函数支持段数量的配置
        public PLCMemory(int segmentCount = 16)
        {
            _segmentCount = segmentCount;
            _dataSegments = new ConcurrentDictionary<int, TData>[segmentCount];
            _statusSegments = new ConcurrentDictionary<int, TStatus>[segmentCount];
            _locks = new ReaderWriterLockSlim[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                _dataSegments[i] = new ConcurrentDictionary<int, TData>();
                _statusSegments[i] = new ConcurrentDictionary<int, TStatus>();
                _locks[i] = new ReaderWriterLockSlim();
            }
        }

        // 根据地址找到对应的段索引
        private int GetSegmentIndex(int address)
        {
            return address % _segmentCount;
        }

        // 同步接口：获取PLC数据
        public TData GetData(int address)
        {
            int segmentIndex = GetSegmentIndex(address);
            var segmentLock = _locks[segmentIndex];

            segmentLock.EnterReadLock();
            try
            {
                return _dataSegments[segmentIndex].TryGetValue(address, out var value) ? value : default;
            }
            finally
            {
                segmentLock.ExitReadLock();
            }
        }

        // 同步接口：设置PLC数据
        public void SetData(int address, TData value)
        {
            int segmentIndex = GetSegmentIndex(address);
            var segmentLock = _locks[segmentIndex];

            segmentLock.EnterWriteLock();
            try
            {
                _dataSegments[segmentIndex][address] = value;
            }
            finally
            {
                segmentLock.ExitWriteLock();
            }
        }

        public TStatus GetIOStatus(int address)
        {
            int segmentIndex = GetSegmentIndex(address);
            var segmentLock = _locks[segmentIndex];

            segmentLock.EnterReadLock();
            try
            {
                return _statusSegments[segmentIndex].TryGetValue(address, out var status) ? status : default;
            }
            finally
            {
                segmentLock.ExitReadLock();
            }
        }

        // 同步接口：设置IO状态
        public void SetIOStatus(int address, TStatus status)
        {
            int segmentIndex = GetSegmentIndex(address);
            var segmentLock = _locks[segmentIndex];

            segmentLock.EnterWriteLock();
            try
            {
                _statusSegments[segmentIndex][address] = status;
            }
            finally
            {
                segmentLock.ExitWriteLock();
            }
        }

        // 异步接口：获取PLC数据
        public async Task<TData> GetDataAsync(int address)
        {
            int segmentIndex = GetSegmentIndex(address);
            var segmentLock = _locks[segmentIndex];

            return await Task.Run(() =>
            {
                segmentLock.EnterReadLock();
                try
                {
                    return _dataSegments[segmentIndex].TryGetValue(address, out var value) ? value : default;
                }
                finally
                {
                    segmentLock.ExitReadLock();
                }
            });
        }

        // 异步接口：设置PLC数据
        public async Task SetDataAsync(int address, TData value)
        {
            int segmentIndex = GetSegmentIndex(address);
            var segmentLock = _locks[segmentIndex];

            await Task.Run(() =>
            {
                segmentLock.EnterWriteLock();
                try
                {
                    _dataSegments[segmentIndex][address] = value;
                }
                finally
                {
                    segmentLock.ExitWriteLock();
                }
            });
        }

        // 异步接口：获取IO状态
        public async Task<TStatus> GetIOStatusAsync(int address)
        {
            int segmentIndex = GetSegmentIndex(address);
            var segmentLock = _locks[segmentIndex];

            return await Task.Run(() =>
            {
                segmentLock.EnterReadLock();
                try
                {
                    return _statusSegments[segmentIndex].TryGetValue(address, out var status) ? status : default;
                }
                finally
                {
                    segmentLock.ExitReadLock();
                }
            });
        }

        // 异步接口：设置IO状态
        public async Task SetIOStatusAsync(int address, TStatus status)
        {
            int segmentIndex = GetSegmentIndex(address);
            var segmentLock = _locks[segmentIndex];

            await Task.Run(() =>
            {
                segmentLock.EnterWriteLock();
                try
                {
                    _statusSegments[segmentIndex][address] = status;
                }
                finally
                {
                    segmentLock.ExitWriteLock();
                }
            });
        }
    }
}

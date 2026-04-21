using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 默认设备连接池实现。
    /// </summary>
    public class DeviceConnectionPool : IDeviceConnectionPool
    {
        private readonly ConcurrentDictionary<ConnectionIdentity, PooledConnectionEntry> _entries;
        private readonly IPooledDeviceConnectionFactory _factory;
        private readonly PooledOperationExecutor _executor;
        private readonly AsyncLock _poolLock;
        private bool _disposed;

        public DeviceConnectionPool(ConnectionPoolOptions options, IPooledDeviceConnectionFactory factory)
        {
            Options = options ?? new ConnectionPoolOptions();
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _executor = new PooledOperationExecutor();
            _entries = new ConcurrentDictionary<ConnectionIdentity, PooledConnectionEntry>();
            _poolLock = new AsyncLock();
        }

        public ConnectionPoolOptions Options { get; private set; }

        public OperationResult Register(DeviceConnectionDescriptor descriptor)
        {
            ThrowIfDisposed();
            if (descriptor == null || descriptor.Identity == null)
            {
                return OperationResult.CreateFailedResult("连接描述不能为空");
            }

            if (_entries.Count >= Options.MaxConnections && !_entries.ContainsKey(descriptor.Identity))
            {
                return OperationResult.CreateFailedResult("连接池容量已满");
            }

            var created = _factory.Create(descriptor);
            if (!created.IsSuccess)
            {
                return OperationResult.CreateFailedResult(created);
            }

            var entry = new PooledConnectionEntry(descriptor, created.ResultValue);
            _entries[descriptor.Identity] = entry;
            return OperationResult.CreateSuccessResult();
        }

        public OperationResult<ConnectionLease> Acquire(ConnectionIdentity identity)
        {
            return AcquireAsync(identity).GetAwaiter().GetResult();
        }

        public async Task<OperationResult<ConnectionLease>> AcquireAsync(ConnectionIdentity identity, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            if (identity == null)
            {
                return OperationResult.CreateFailedResult<ConnectionLease>("连接标识不能为空");
            }

            PooledConnectionEntry entry;
            if (!_entries.TryGetValue(identity, out entry))
            {
                return OperationResult.CreateFailedResult<ConnectionLease>("连接未注册");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return OperationResult.CreateFailedResult<ConnectionLease>("操作已取消");
            }

            return await entry.AcquireAsync(Options.LeaseTimeout).ConfigureAwait(false);
        }

        public OperationResult Release(ConnectionLease lease)
        {
            ThrowIfDisposed();
            if (lease == null || lease.Identity == null)
            {
                return OperationResult.CreateFailedResult("租约不能为空");
            }

            PooledConnectionEntry entry;
            if (!_entries.TryGetValue(lease.Identity, out entry))
            {
                return OperationResult.CreateFailedResult("连接条目不存在");
            }

            return entry.ReleaseAsync(lease).GetAwaiter().GetResult();
        }

        public OperationResult Invalidate(ConnectionIdentity identity, string reason)
        {
            ThrowIfDisposed();
            if (identity == null)
            {
                return OperationResult.CreateFailedResult("连接标识不能为空");
            }

            PooledConnectionEntry entry;
            if (!_entries.TryGetValue(identity, out entry))
            {
                return OperationResult.CreateFailedResult("连接条目不存在");
            }

            return entry.InvalidateAsync(reason).GetAwaiter().GetResult();
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult<T>>> action, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            if (identity == null)
            {
                return OperationResult.CreateFailedResult<T>("连接标识不能为空");
            }

            PooledConnectionEntry entry;
            if (!_entries.TryGetValue(identity, out entry))
            {
                return OperationResult.CreateFailedResult<T>("连接未注册");
            }

            var lease = await AcquireAsync(identity, cancellationToken).ConfigureAwait(false);
            if (!lease.IsSuccess)
            {
                return OperationResult.CreateFailedResult<T>(lease);
            }

            try
            {
                return await _executor.ExecuteAsync(entry, action, Options).ConfigureAwait(false);
            }
            finally
            {
                Release(lease.ResultValue);
            }
        }

        public async Task<OperationResult> ExecuteAsync(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult>> action, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            if (action == null)
            {
                return OperationResult.CreateFailedResult("执行委托不能为空");
            }

            var wrapped = await ExecuteAsync<object>(identity, async client =>
            {
                var result = await action(client).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    return OperationResult.CreateSuccessResult<object>(null);
                }

                return OperationResult.CreateFailedResult<object>(result);
            }, cancellationToken).ConfigureAwait(false);

            return wrapped.IsSuccess ? OperationResult.CreateSuccessResult() : OperationResult.CreateFailedResult(wrapped);
        }

        public async Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointReadRequest> points, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();

            var normalized = PointListOperationHelper.NormalizeReadRequests(points);
            if (!normalized.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IList<DevicePointReadResult>>(normalized);
            }

            return await ExecuteAsync<IList<DevicePointReadResult>>(identity, client =>
                PointListOperationHelper.ReadPointsAsync(client, normalized.ResultValue), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointWriteRequest> points, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();

            var normalized = PointListOperationHelper.NormalizeWriteRequests(points);
            if (!normalized.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IList<DevicePointWriteResult>>(normalized);
            }

            return await ExecuteAsync<IList<DevicePointWriteResult>>(identity, client =>
                PointListOperationHelper.WritePointsAsync(client, normalized.ResultValue), cancellationToken).ConfigureAwait(false);
        }

        public OperationResult<int> CleanupIdle()
        {
            ThrowIfDisposed();

            var removed = 0;
            var candidates = _entries.ToArray();
            foreach (var pair in candidates)
            {
                if (!pair.Value.CanCleanup(Options.IdleTimeout))
                {
                    continue;
                }

                PooledConnectionEntry removedEntry;
                if (_entries.TryRemove(pair.Key, out removedEntry))
                {
                    removedEntry.DisposeAsync().GetAwaiter().GetResult();
                    removed++;
                }
            }

            return OperationResult.CreateSuccessResult(removed);
        }

        public OperationResult<IDictionary<ConnectionIdentity, ConnectionEntryState>> GetStates()
        {
            ThrowIfDisposed();
            IDictionary<ConnectionIdentity, ConnectionEntryState> snapshot = _entries.ToDictionary(t => t.Key, t => t.Value.State);
            return OperationResult.CreateSuccessResult(snapshot);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            using (_poolLock.Lock())
            {
                if (_disposed)
                {
                    return;
                }

                foreach (var entry in _entries.Values)
                {
                    entry.DisposeAsync().GetAwaiter().GetResult();
                }

                _entries.Clear();
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DeviceConnectionPool));
            }
        }
    }
}

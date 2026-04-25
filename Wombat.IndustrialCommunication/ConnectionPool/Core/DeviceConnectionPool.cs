using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Events;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 默认设备连接池实现。
    /// </summary>
    public class DeviceConnectionPool : IDeviceConnectionPool, IConnectionPoolEventPublisher
    {
        private readonly ConcurrentDictionary<ConnectionIdentity, PooledConnectionEntry> _entries;
        private readonly IPooledDeviceConnectionFactory _factory;
        private readonly PooledOperationExecutor _executor;
        private readonly ConnectionPoolEventDispatcher _eventDispatcher;
        private readonly ConnectionPoolMaintenanceService _maintenanceService;
        private readonly AsyncLock _poolLock;
        private readonly CancellationTokenSource _maintenanceCancellationTokenSource;
        private readonly Task _maintenanceTask;
        private bool _disposed;

        public DeviceConnectionPool(ConnectionPoolOptions options, IPooledDeviceConnectionFactory factory)
        {
            Options = options ?? new ConnectionPoolOptions();
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _executor = new PooledOperationExecutor();
            _eventDispatcher = new ConnectionPoolEventDispatcher(this, Options.IsolateEventSubscriberExceptions);
            _entries = new ConcurrentDictionary<ConnectionIdentity, PooledConnectionEntry>();
            _poolLock = new AsyncLock();
            var monitor = new ConnectionStateMonitor();
            _maintenanceService = new ConnectionPoolMaintenanceService(
                Options,
                monitor,
                GetEntriesForMaintenance,
                CleanupIdleCore,
                PublishMaintenanceEvent);
            _maintenanceCancellationTokenSource = new CancellationTokenSource();
            if (Options.EnableBackgroundMaintenance)
            {
                _maintenanceTask = Task.Run(() => _maintenanceService.RunAsync(_maintenanceCancellationTokenSource.Token));
            }
        }

        public ConnectionPoolOptions Options { get; private set; }

        public event EventHandler<ConnectionPoolEventArgs> PoolEventOccurred
        {
            add { _eventDispatcher.PoolEventOccurred += value; }
            remove { _eventDispatcher.PoolEventOccurred -= value; }
        }

        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged
        {
            add { _eventDispatcher.ConnectionStateChanged += value; }
            remove { _eventDispatcher.ConnectionStateChanged -= value; }
        }

        public event EventHandler<ConnectionLeaseEventArgs> LeaseChanged
        {
            add { _eventDispatcher.LeaseChanged += value; }
            remove { _eventDispatcher.LeaseChanged -= value; }
        }

        public event EventHandler<ConnectionMaintenanceEventArgs> MaintenanceCompleted
        {
            add { _eventDispatcher.MaintenanceCompleted += value; }
            remove { _eventDispatcher.MaintenanceCompleted -= value; }
        }

        public OperationResult Register(DeviceConnectionDescriptor descriptor)
        {
            if (descriptor == null || descriptor.Identity == null)
            {
                return OperationResult.CreateFailedResult("连接描述不能为空");
            }

            ConnectionPoolEventArgs registerEvent;
            using (_poolLock.Lock())
            {
                ThrowIfDisposed();
                if (_entries.ContainsKey(descriptor.Identity))
                {
                    return OperationResult.CreateFailedResult("连接已注册");
                }

                if (_entries.Count >= Options.MaxConnections)
                {
                    return OperationResult.CreateFailedResult("连接池容量已满");
                }

                var created = _factory.Create(descriptor);
                if (!created.IsSuccess)
                {
                    return OperationResult.CreateFailedResult(created);
                }

                var entry = new PooledConnectionEntry(descriptor, created.ResultValue, this);
                if (!_entries.TryAdd(descriptor.Identity, entry))
                {
                    created.ResultValue.Disconnect();
                    return OperationResult.CreateFailedResult("连接已注册");
                }

                registerEvent = new ConnectionPoolEventArgs
                {
                    Identity = descriptor.Identity,
                    EventType = ConnectionPoolEventType.Registered,
                    State = entry.State,
                    LifecycleState = entry.LifecycleState,
                    Message = "连接条目注册成功",
                    TriggerMode = ConnectionPoolMaintenanceMode.UserCall
                };
            }

            Publish(registerEvent);
            return OperationResult.CreateSuccessResult();
        }

        public OperationResult<ConnectionLease> Acquire(ConnectionIdentity identity)
        {
            return AcquireAsync(identity).GetAwaiter().GetResult();
        }

        public async Task<OperationResult<ConnectionLease>> AcquireAsync(ConnectionIdentity identity, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult<ConnectionLease>("连接标识不能为空");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return OperationResult.CreateFailedResult<ConnectionLease>("操作已取消");
            }

            PooledConnectionEntry entry;
            using (_poolLock.Lock())
            {
                ThrowIfDisposed();
                if (!_entries.TryGetValue(identity, out entry))
                {
                    return OperationResult.CreateFailedResult<ConnectionLease>("连接未注册");
                }
            }

            var lease = await entry.AcquireAsync(Options.LeaseTimeout, ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
            if (!lease.IsSuccess)
            {
                return lease;
            }

            using (_poolLock.Lock())
            {
                if (_disposed)
                {
                    entry.ReleaseAsync(lease.ResultValue, false, ConnectionPoolMaintenanceMode.Dispose).GetAwaiter().GetResult();
                    return OperationResult.CreateFailedResult<ConnectionLease>("连接池已释放");
                }

                PooledConnectionEntry current;
                if (!_entries.TryGetValue(identity, out current) || !ReferenceEquals(current, entry))
                {
                    entry.ReleaseAsync(lease.ResultValue, false, ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
                    return OperationResult.CreateFailedResult<ConnectionLease>("连接条目已移除");
                }
            }

            return lease;
        }

        public OperationResult Release(ConnectionLease lease)
        {
            if (lease == null || lease.Identity == null)
            {
                return OperationResult.CreateFailedResult("租约不能为空");
            }

            PooledConnectionEntry entry;
            using (_poolLock.Lock())
            {
                _entries.TryGetValue(lease.Identity, out entry);
            }

            if (entry == null)
            {
                return OperationResult.CreateSuccessResult("连接条目已移除，租约已自动关闭");
            }

            return entry.ReleaseAsync(lease, false, ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
        }

        public OperationResult Invalidate(ConnectionIdentity identity, string reason)
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult("连接标识不能为空");
            }

            PooledConnectionEntry entry;
            using (_poolLock.Lock())
            {
                ThrowIfDisposed();
                if (!_entries.TryGetValue(identity, out entry))
                {
                    return OperationResult.CreateFailedResult("连接条目不存在");
                }
            }

            return entry.InvalidateAsync(reason, ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
        }

        public OperationResult Unregister(ConnectionIdentity identity, string reason)
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult("连接标识不能为空");
            }

            PooledConnectionEntry removedEntry;
            using (_poolLock.Lock())
            {
                ThrowIfDisposed();

                PooledConnectionEntry entry;
                if (!_entries.TryGetValue(identity, out entry))
                {
                    return OperationResult.CreateFailedResult("连接条目不存在");
                }

                var prepare = entry.PrepareForRemovalAsync(false, ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
                if (!prepare.IsSuccess)
                {
                    return prepare;
                }

                if (!_entries.TryRemove(identity, out removedEntry))
                {
                    entry.CancelPendingRemovalAsync(ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
                    return OperationResult.CreateFailedResult("连接条目移除失败");
                }
            }

            var dispose = removedEntry.DisposeAsync(ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
            Publish(new ConnectionPoolEventArgs
            {
                Identity = identity,
                EventType = ConnectionPoolEventType.Unregistered,
                State = ConnectionEntryState.Disconnected,
                LifecycleState = ConnectionEntryLifecycleState.Disposed,
                Message = string.IsNullOrWhiteSpace(reason) ? "连接条目已注销" : reason,
                Exception = dispose.Exception,
                TriggerMode = ConnectionPoolMaintenanceMode.UserCall
            });

            return dispose.IsSuccess
                ? OperationResult.CreateSuccessResult("连接条目已注销")
                : OperationResult.CreateFailedResult(dispose);
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult<T>>> action, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ExecuteAsync(identity, action, ConnectionExecutionOptions.CreateDiagnostic(), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult<T>>> action, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult<T>("连接标识不能为空");
            }

            PooledConnectionEntry entry;
            using (_poolLock.Lock())
            {
                ThrowIfDisposed();
                if (!_entries.TryGetValue(identity, out entry))
                {
                    return OperationResult.CreateFailedResult<T>("连接未注册");
                }
            }

            var lease = await AcquireAsync(identity, cancellationToken).ConfigureAwait(false);
            if (!lease.IsSuccess)
            {
                return OperationResult.CreateFailedResult<T>(lease);
            }

            try
            {
                return await _executor.ExecuteAsync(entry, action, Options, executionOptions).ConfigureAwait(false);
            }
            finally
            {
                Release(lease.ResultValue);
            }
        }

        public async Task<OperationResult> ExecuteAsync(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult>> action, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ExecuteAsync(identity, action, ConnectionExecutionOptions.CreateDiagnostic(), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult> ExecuteAsync(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult>> action, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
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
            }, executionOptions, cancellationToken).ConfigureAwait(false);

            if (wrapped.IsSuccess)
            {
                return new OperationResult().SetInfo(wrapped).Complete();
            }

            return OperationResult.CreateFailedResult(wrapped);
        }

        public async Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointReadRequest> points, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ReadPointsAsync(identity, points, ConnectionExecutionOptions.CreateRead(), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointReadRequest> points, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();

            var normalized = PointListOperationHelper.NormalizeReadRequests(points);
            if (!normalized.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IList<DevicePointReadResult>>(normalized);
            }

            return await ExecuteAsync<IList<DevicePointReadResult>>(identity, client =>
                PointListOperationHelper.ReadPointsAsync(client, normalized.ResultValue), executionOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointWriteRequest> points, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await WritePointsAsync(identity, points, ConnectionExecutionOptions.CreateWrite(), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointWriteRequest> points, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();

            var normalized = PointListOperationHelper.NormalizeWriteRequests(points);
            if (!normalized.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IList<DevicePointWriteResult>>(normalized);
            }

            return await ExecuteAsync<IList<DevicePointWriteResult>>(identity, client =>
                PointListOperationHelper.WritePointsAsync(client, normalized.ResultValue), executionOptions, cancellationToken).ConfigureAwait(false);
        }

        public OperationResult<int> CleanupIdle()
        {
            ThrowIfDisposed();
            return OperationResult.CreateSuccessResult(CleanupIdleCore());
        }

        private int CleanupIdleCore()
        {
            var removedEntries = new List<KeyValuePair<ConnectionIdentity, PooledConnectionEntry>>();
            using (_poolLock.Lock())
            {
                if (_disposed)
                {
                    return 0;
                }

                var candidates = _entries.ToArray();
                foreach (var pair in candidates)
                {
                    if (!pair.Value.CanCleanup(Options.IdleTimeout))
                    {
                        continue;
                    }

                    var prepare = pair.Value.PrepareForRemovalAsync(false, ConnectionPoolMaintenanceMode.Cleanup).GetAwaiter().GetResult();
                    if (!prepare.IsSuccess)
                    {
                        continue;
                    }

                    PooledConnectionEntry removedEntry;
                    if (_entries.TryRemove(pair.Key, out removedEntry))
                    {
                        removedEntries.Add(new KeyValuePair<ConnectionIdentity, PooledConnectionEntry>(pair.Key, removedEntry));
                    }
                    else
                    {
                        pair.Value.CancelPendingRemovalAsync(ConnectionPoolMaintenanceMode.Cleanup).GetAwaiter().GetResult();
                    }
                }
            }

            foreach (var pair in removedEntries)
            {
                pair.Value.DisposeAsync(ConnectionPoolMaintenanceMode.Cleanup).GetAwaiter().GetResult();
                Publish(new ConnectionPoolEventArgs
                {
                    Identity = pair.Key,
                    EventType = ConnectionPoolEventType.IdleCleaned,
                    State = ConnectionEntryState.Disconnected,
                    LifecycleState = ConnectionEntryLifecycleState.Disposed,
                    Message = "空闲连接已回收",
                    TriggerMode = ConnectionPoolMaintenanceMode.Cleanup
                });
            }

            return removedEntries.Count;
        }

        public OperationResult<int> CleanupExpiredLeases()
        {
            ThrowIfDisposed();

            var expired = 0;
            var utcNow = DateTime.UtcNow;
            foreach (var entry in _entries.Values)
            {
                var result = entry.ExpireLeasesAsync(utcNow, ConnectionPoolMaintenanceMode.Background).GetAwaiter().GetResult();
                if (result.IsSuccess)
                {
                    expired += result.ResultValue;
                }
            }

            return OperationResult.CreateSuccessResult(expired);
        }

        public OperationResult<IDictionary<ConnectionIdentity, ConnectionEntryState>> GetStates()
        {
            ThrowIfDisposed();
            IDictionary<ConnectionIdentity, ConnectionEntryState> snapshot = _entries.ToDictionary(t => t.Key, t => t.Value.CreateSnapshot().State);
            return OperationResult.CreateSuccessResult(snapshot);
        }

        public OperationResult<ConnectionEntrySnapshot> GetState(ConnectionIdentity identity)
        {
            ThrowIfDisposed();
            if (identity == null)
            {
                return OperationResult.CreateFailedResult<ConnectionEntrySnapshot>("连接标识不能为空");
            }

            PooledConnectionEntry entry;
            if (!_entries.TryGetValue(identity, out entry))
            {
                return OperationResult.CreateFailedResult<ConnectionEntrySnapshot>("连接条目不存在");
            }

            return OperationResult.CreateSuccessResult(entry.CreateSnapshot());
        }

        public OperationResult<IList<ConnectionEntrySnapshot>> GetEntrySnapshots()
        {
            ThrowIfDisposed();
            IList<ConnectionEntrySnapshot> snapshots = _entries.Values.Select(t => t.CreateSnapshot()).ToList();
            return OperationResult.CreateSuccessResult(snapshots);
        }

        public OperationResult<ConnectionPoolSnapshot> GetPoolSnapshot()
        {
            ThrowIfDisposed();
            var entries = _entries.Values.Select(t => t.CreateSnapshot()).ToList();
            var snapshot = ConnectionPoolSnapshotBuilder.Build(entries);
            return OperationResult.CreateSuccessResult(snapshot);
        }

        public OperationResult ForceReconnect(ConnectionIdentity identity, string reason)
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult("连接标识不能为空");
            }

            PooledConnectionEntry entry;
            using (_poolLock.Lock())
            {
                ThrowIfDisposed();
                if (!_entries.TryGetValue(identity, out entry))
                {
                    return OperationResult.CreateFailedResult("连接条目不存在");
                }
            }

            return entry.ForceReconnectAsync(reason).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            List<PooledConnectionEntry> entriesToDispose = null;
            using (_poolLock.Lock())
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _maintenanceCancellationTokenSource.Cancel();
                entriesToDispose = new List<PooledConnectionEntry>();
                foreach (var pair in _entries.ToArray())
                {
                    pair.Value.PrepareForRemovalAsync(true, ConnectionPoolMaintenanceMode.Dispose).GetAwaiter().GetResult();
                    PooledConnectionEntry removedEntry;
                    if (_entries.TryRemove(pair.Key, out removedEntry))
                    {
                        entriesToDispose.Add(removedEntry);
                    }
                }
            }

            if (_maintenanceTask != null)
            {
                try
                {
                    _maintenanceTask.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                }
                catch (AggregateException ex)
                {
                    if (!(ex.InnerException is OperationCanceledException))
                    {
                        throw;
                    }
                }
            }

            foreach (var entry in entriesToDispose)
            {
                entry.DisposeAsync(ConnectionPoolMaintenanceMode.Dispose).GetAwaiter().GetResult();
            }

            _maintenanceCancellationTokenSource.Dispose();
        }

        public void Publish(ConnectionPoolEventArgs args)
        {
            _eventDispatcher.Publish(args);
        }

        public void PublishStateChanged(ConnectionStateChangedEventArgs args)
        {
            _eventDispatcher.PublishStateChanged(args);
        }

        public void PublishLeaseEvent(ConnectionLeaseEventArgs args)
        {
            _eventDispatcher.PublishLeaseEvent(args);
        }

        public void PublishMaintenanceEvent(ConnectionMaintenanceEventArgs args)
        {
            _eventDispatcher.PublishMaintenanceEvent(args);
        }

        private IList<PooledConnectionEntry> GetEntriesForMaintenance()
        {
            return _entries.Values.ToList();
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

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
    /// 通用资源池基类，封装客户端与服务端共享的核心池化逻辑。
    /// </summary>
    public abstract class ResourcePool<TResource> : IResourcePool<TResource>, IConnectionPoolEventPublisher
    {
        private readonly ConcurrentDictionary<ConnectionIdentity, PooledResourceEntry<TResource>> _entries;
        private readonly IPooledResourceConnectionFactory<TResource> _factory;
        private readonly PooledResourceExecutor<TResource> _executor;
        private readonly ConnectionPoolEventDispatcher _eventDispatcher;
        private readonly ConnectionControlScheduler<TResource> _controlScheduler;
        private readonly ConnectionPoolMaintenanceService<TResource> _maintenanceService;
        private readonly AsyncLock _poolLock;
        private readonly CancellationTokenSource _maintenanceCancellationTokenSource;
        private readonly Task _maintenanceTask;
        private readonly ResourceRole _poolRole;
        private readonly string _roleMismatchMessage;
        private bool _disposed;

        protected ResourcePool(
            ConnectionPoolOptions options,
            IPooledResourceConnectionFactory<TResource> factory,
            ResourceRole poolRole,
            string roleMismatchMessage)
        {
            Options = options ?? new ConnectionPoolOptions();
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _poolRole = poolRole;
            _roleMismatchMessage = string.IsNullOrWhiteSpace(roleMismatchMessage) ? "资源角色不匹配" : roleMismatchMessage;
            _executor = new PooledResourceExecutor<TResource>();
            _eventDispatcher = new ConnectionPoolEventDispatcher(this, Options.IsolateEventSubscriberExceptions);
            _controlScheduler = new ConnectionControlScheduler<TResource>(Options);
            _entries = new ConcurrentDictionary<ConnectionIdentity, PooledResourceEntry<TResource>>();
            _poolLock = new AsyncLock();
            var monitor = new ConnectionStateMonitor();
            _maintenanceService = new ConnectionPoolMaintenanceService<TResource>(
                Options,
                monitor,
                GetEntriesForMaintenance,
                CleanupIdleCore,
                QueueRecovery,
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

        public OperationResult Register(ResourceDescriptor descriptor)
        {
            if (descriptor == null || descriptor.Identity == null)
            {
                return OperationResult.CreateFailedResult("连接描述不能为空");
            }

            if (descriptor.ResourceRole != ResourceRole.Unknown && descriptor.ResourceRole != _poolRole)
            {
                return OperationResult.CreateFailedResult(_roleMismatchMessage);
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

                var normalizedDescriptor = NormalizeDescriptor(descriptor);
                var created = _factory.Create(normalizedDescriptor);
                if (!created.IsSuccess)
                {
                    return OperationResult.CreateFailedResult(created);
                }

                var entry = new PooledResourceEntry<TResource>(normalizedDescriptor, created.ResultValue, this);
                if (!_entries.TryAdd(normalizedDescriptor.Identity, entry))
                {
                    created.ResultValue.DisconnectOrShutdown();
                    return OperationResult.CreateFailedResult("连接已注册");
                }

                registerEvent = new ConnectionPoolEventArgs
                {
                    Identity = normalizedDescriptor.Identity,
                    ResourceRole = normalizedDescriptor.ResourceRole,
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

            PooledResourceEntry<TResource> entry;
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

            var releaseAfterLock = false;
            using (_poolLock.Lock())
            {
                if (_disposed)
                {
                    releaseAfterLock = true;
                }
                else
                {
                    PooledResourceEntry<TResource> current;
                    if (!_entries.TryGetValue(identity, out current) || !ReferenceEquals(current, entry))
                    {
                        releaseAfterLock = true;
                    }
                }
            }

            if (releaseAfterLock)
            {
                entry.ReleaseAsync(lease.ResultValue, false, _disposed ? ConnectionPoolMaintenanceMode.Dispose : ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
                if (_disposed)
                {
                    return OperationResult.CreateFailedResult<ConnectionLease>("连接池已释放");
                }

                return OperationResult.CreateFailedResult<ConnectionLease>("连接条目已移除");
            }

            return lease;
        }

        public OperationResult Release(ConnectionLease lease)
        {
            if (lease == null || lease.Identity == null)
            {
                return OperationResult.CreateFailedResult("租约不能为空");
            }

            PooledResourceEntry<TResource> entry;
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

            PooledResourceEntry<TResource> entry;
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

        public async Task<OperationResult> ForceCloseAsync(ConnectionIdentity identity, string reason, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult("连接标识不能为空");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                var cancelled = OperationResult.CreateFailedResult("操作已取消");
                cancelled.IsCancelled = true;
                return cancelled.Complete();
            }

            PooledResourceEntry<TResource> entry;
            using (_poolLock.Lock())
            {
                ThrowIfDisposed();
                if (!_entries.TryGetValue(identity, out entry))
                {
                    return OperationResult.CreateFailedResult("连接条目不存在");
                }
            }

            return await _controlScheduler.ForceCloseAsync(entry, reason, cancellationToken).ConfigureAwait(false);
        }

        public OperationResult Unregister(ConnectionIdentity identity, string reason)
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult("连接标识不能为空");
            }

            PooledResourceEntry<TResource> entry;
            using (_poolLock.Lock())
            {
                ThrowIfDisposed();

                if (!_entries.TryGetValue(identity, out entry))
                {
                    return OperationResult.CreateFailedResult("连接条目不存在");
                }
            }

            var prepare = entry.PrepareForRemovalAsync(false, ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
            if (!prepare.IsSuccess)
            {
                return prepare;
            }

            PooledResourceEntry<TResource> removedEntry = null;
            var removed = false;
            var disposedDuringRemove = false;
            using (_poolLock.Lock())
            {
                if (_disposed)
                {
                    disposedDuringRemove = true;
                }
                else
                {
                    PooledResourceEntry<TResource> current;
                    if (_entries.TryGetValue(identity, out current) && ReferenceEquals(current, entry))
                    {
                        removed = _entries.TryRemove(identity, out removedEntry);
                    }
                }
            }

            if (disposedDuringRemove)
            {
                entry.CancelPendingRemovalAsync(ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
                return OperationResult.CreateFailedResult("连接池已释放");
            }

            if (!removed)
            {
                entry.CancelPendingRemovalAsync(ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
                return OperationResult.CreateFailedResult("连接条目移除失败");
            }

            var dispose = removedEntry.DisposeAsync(ConnectionPoolMaintenanceMode.UserCall).GetAwaiter().GetResult();
            Publish(new ConnectionPoolEventArgs
            {
                Identity = identity,
                ResourceRole = _poolRole,
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

        public async Task<OperationResult<T>> ExecuteAsync<T>(ConnectionIdentity identity, Func<TResource, Task<OperationResult<T>>> action, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ExecuteAsync(identity, action, ConnectionExecutionOptions.CreateDiagnostic(), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(ConnectionIdentity identity, Func<TResource, Task<OperationResult<T>>> action, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ExecuteWithExecutionTokenAsync(identity, (resource, executionToken) => action(resource), executionOptions, cancellationToken).ConfigureAwait(false);
        }

        protected async Task<OperationResult<T>> ExecuteWithExecutionTokenAsync<T>(ConnectionIdentity identity, Func<TResource, CancellationToken, Task<OperationResult<T>>> action, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult<T>("连接标识不能为空");
            }

            if (action == null)
            {
                return OperationResult.CreateFailedResult<T>("执行委托不能为空");
            }

            PooledResourceEntry<TResource> entry;
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
                return await _executor.ExecuteAsync(entry, action, Options, executionOptions, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Release(lease.ResultValue);
            }
        }

        public async Task<OperationResult> ExecuteAsync(ConnectionIdentity identity, Func<TResource, Task<OperationResult>> action, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ExecuteAsync(identity, action, ConnectionExecutionOptions.CreateDiagnostic(), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult> ExecuteAsync(ConnectionIdentity identity, Func<TResource, Task<OperationResult>> action, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            if (action == null)
            {
                return OperationResult.CreateFailedResult("执行委托不能为空");
            }

            var wrapped = await ExecuteWithExecutionTokenAsync<object>(identity, async (resource, executionToken) =>
            {
                var result = await action(resource).ConfigureAwait(false);
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

        public OperationResult<int> CleanupIdle()
        {
            ThrowIfDisposed();
            return OperationResult.CreateSuccessResult(CleanupIdleCore());
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
            IDictionary<ConnectionIdentity, ConnectionEntryState> snapshot = _entries.ToDictionary(t => t.Key, t => t.Value.GetCachedSnapshot().State);
            return OperationResult.CreateSuccessResult(snapshot);
        }

        public OperationResult<ConnectionEntrySnapshot> GetState(ConnectionIdentity identity)
        {
            ThrowIfDisposed();
            if (identity == null)
            {
                return OperationResult.CreateFailedResult<ConnectionEntrySnapshot>("连接标识不能为空");
            }

            PooledResourceEntry<TResource> entry;
            if (!_entries.TryGetValue(identity, out entry))
            {
                return OperationResult.CreateFailedResult<ConnectionEntrySnapshot>("连接条目不存在");
            }

            return OperationResult.CreateSuccessResult(entry.GetCachedSnapshot());
        }

        public OperationResult<IList<ConnectionEntrySnapshot>> GetEntrySnapshots()
        {
            ThrowIfDisposed();
            IList<ConnectionEntrySnapshot> snapshots = _entries.Values.Select(t => t.GetCachedSnapshot()).ToList();
            return OperationResult.CreateSuccessResult(snapshots);
        }

        public OperationResult<ConnectionPoolSnapshot> GetPoolSnapshot()
        {
            ThrowIfDisposed();
            var entries = _entries.Values.Select(t => t.GetCachedSnapshot()).ToList();
            var snapshot = ConnectionPoolSnapshotBuilder.Build(entries);
            return OperationResult.CreateSuccessResult(snapshot);
        }

        public OperationResult ForceReconnect(ConnectionIdentity identity, string reason)
        {
            return ForceReconnectAsync(identity, reason).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> ForceReconnectAsync(ConnectionIdentity identity, string reason, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult("连接标识不能为空");
            }

            PooledResourceEntry<TResource> entry;
            using (_poolLock.Lock())
            {
                ThrowIfDisposed();
                if (!_entries.TryGetValue(identity, out entry))
                {
                    return OperationResult.CreateFailedResult("连接条目不存在");
                }
            }

            return await _controlScheduler.ForceReconnectAsync(entry, reason, cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<IDictionary<ConnectionIdentity, OperationResult>>> ForceCloseManyAsync(IEnumerable<ConnectionIdentity> identities, string reason, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await RunManyAsync(identities, identity => ForceCloseAsync(identity, reason, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<IDictionary<ConnectionIdentity, OperationResult>>> ForceReconnectManyAsync(IEnumerable<ConnectionIdentity> identities, string reason, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await RunManyAsync(identities, identity => ForceReconnectAsync(identity, reason, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<IDictionary<ConnectionIdentity, OperationResult>>> RecoverManyAsync(IEnumerable<ConnectionIdentity> identities, string reason, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await RunManyAsync(identities, identity => RecoverAsync(identity, reason, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        internal async Task<OperationResult> RecoverAsync(ConnectionIdentity identity, string reason, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult("连接标识不能为空");
            }

            PooledResourceEntry<TResource> entry;
            using (_poolLock.Lock())
            {
                ThrowIfDisposed();
                if (!_entries.TryGetValue(identity, out entry))
                {
                    return OperationResult.CreateFailedResult("连接条目不存在");
                }
            }

            return await _controlScheduler.RecoverAsync(entry, reason, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            List<KeyValuePair<ConnectionIdentity, PooledResourceEntry<TResource>>> candidates = null;
            var entriesToDispose = new List<PooledResourceEntry<TResource>>();
            using (_poolLock.Lock())
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _maintenanceCancellationTokenSource.Cancel();
                candidates = _entries.ToArray().ToList();
            }

            foreach (var pair in candidates)
            {
                pair.Value.PrepareForRemovalAsync(true, ConnectionPoolMaintenanceMode.Dispose).GetAwaiter().GetResult();
                PooledResourceEntry<TResource> removedEntry;
                using (_poolLock.Lock())
                {
                    _entries.TryRemove(pair.Key, out removedEntry);
                }

                if (removedEntry != null)
                {
                    entriesToDispose.Add(removedEntry);
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
            _eventDispatcher.Dispose();
            _controlScheduler.Dispose();
        }

        public void Publish(ConnectionPoolEventArgs args)
        {
            NormalizeEventRole(args);
            _eventDispatcher.Publish(args);
            QueueRecovery(args);
        }

        public void PublishStateChanged(ConnectionStateChangedEventArgs args)
        {
            NormalizeEventRole(args);
            _eventDispatcher.PublishStateChanged(args);
        }

        public void PublishLeaseEvent(ConnectionLeaseEventArgs args)
        {
            NormalizeEventRole(args);
            _eventDispatcher.PublishLeaseEvent(args);
        }

        public void PublishMaintenanceEvent(ConnectionMaintenanceEventArgs args)
        {
            NormalizeEventRole(args);
            _eventDispatcher.PublishMaintenanceEvent(args);
        }

        protected OperationResult<PooledResourceEntry<TResource>> GetRegisteredEntry(ConnectionIdentity identity, string notFoundMessage)
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult<PooledResourceEntry<TResource>>("连接标识不能为空");
            }

            using (_poolLock.Lock())
            {
                ThrowIfDisposed();
                PooledResourceEntry<TResource> entry;
                if (!_entries.TryGetValue(identity, out entry))
                {
                    var message = string.IsNullOrWhiteSpace(notFoundMessage) ? "连接未注册" : notFoundMessage;
                    return OperationResult.CreateFailedResult<PooledResourceEntry<TResource>>(message);
                }

                return OperationResult.CreateSuccessResult(entry);
            }
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private ResourceDescriptor NormalizeDescriptor(ResourceDescriptor descriptor)
        {
            if (descriptor.ResourceRole == ResourceRole.Unknown)
            {
                descriptor.ResourceRole = _poolRole;
            }

            return descriptor;
        }

        private int CleanupIdleCore()
        {
            var removedEntries = new List<KeyValuePair<ConnectionIdentity, PooledResourceEntry<TResource>>>();
            KeyValuePair<ConnectionIdentity, PooledResourceEntry<TResource>>[] candidates;
            using (_poolLock.Lock())
            {
                if (_disposed)
                {
                    return 0;
                }

                candidates = _entries.ToArray();
            }

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

                PooledResourceEntry<TResource> removedEntry = null;
                var removed = false;
                using (_poolLock.Lock())
                {
                    if (!_disposed)
                    {
                        PooledResourceEntry<TResource> current;
                        if (_entries.TryGetValue(pair.Key, out current) && ReferenceEquals(current, pair.Value))
                        {
                            removed = _entries.TryRemove(pair.Key, out removedEntry);
                        }
                    }
                }

                if (removed)
                {
                    removedEntries.Add(new KeyValuePair<ConnectionIdentity, PooledResourceEntry<TResource>>(pair.Key, removedEntry));
                }
                else
                {
                    pair.Value.CancelPendingRemovalAsync(ConnectionPoolMaintenanceMode.Cleanup).GetAwaiter().GetResult();
                }
            }

            foreach (var pair in removedEntries)
            {
                pair.Value.DisposeAsync(ConnectionPoolMaintenanceMode.Cleanup).GetAwaiter().GetResult();
                Publish(new ConnectionPoolEventArgs
                {
                    Identity = pair.Key,
                    ResourceRole = _poolRole,
                    EventType = ConnectionPoolEventType.IdleCleaned,
                    State = ConnectionEntryState.Disconnected,
                    LifecycleState = ConnectionEntryLifecycleState.Disposed,
                    Message = "空闲连接已回收",
                    TriggerMode = ConnectionPoolMaintenanceMode.Cleanup
                });
            }

            return removedEntries.Count;
        }

        private IList<PooledResourceEntry<TResource>> GetEntriesForMaintenance()
        {
            return _entries.Values.ToList();
        }

        private async Task<OperationResult<IDictionary<ConnectionIdentity, OperationResult>>> RunManyAsync(IEnumerable<ConnectionIdentity> identities, Func<ConnectionIdentity, Task<OperationResult>> action, CancellationToken cancellationToken)
        {
            if (identities == null)
            {
                return OperationResult.CreateFailedResult<IDictionary<ConnectionIdentity, OperationResult>>("连接标识集合不能为空");
            }

            var distinct = identities.Where(identity => identity != null).Distinct().ToList();
            var tasks = distinct.Select(async identity =>
            {
                var result = cancellationToken.IsCancellationRequested
                    ? OperationResult.CreateFailedResult("操作已取消")
                    : await action(identity).ConfigureAwait(false);
                return new KeyValuePair<ConnectionIdentity, OperationResult>(identity, result);
            }).ToList();

            var pairs = await Task.WhenAll(tasks).ConfigureAwait(false);
            var results = pairs.ToDictionary(pair => pair.Key, pair => pair.Value);
            var failed = results.Values.Count(result => result == null || !result.IsSuccess);
            var aggregate = failed == 0
                ? OperationResult.CreateSuccessResult<IDictionary<ConnectionIdentity, OperationResult>>(results)
                : OperationResult.CreateFailedResult<IDictionary<ConnectionIdentity, OperationResult>>(string.Format("批量控制完成，失败 {0}/{1}", failed, results.Count));
            aggregate.ResultValue = results;
            return aggregate.Complete();
        }

        private void QueueRecovery(PooledResourceEntry<TResource> entry, string reason)
        {
            if (entry != null)
            {
                _controlScheduler.QueueRecover(entry, reason);
            }
        }

        private void QueueRecovery(ConnectionPoolEventArgs args)
        {
            if (args == null || args.Identity == null || args.LifecycleState != ConnectionEntryLifecycleState.Faulted || args.TriggerMode == ConnectionPoolMaintenanceMode.ForceClose)
            {
                return;
            }

            PooledResourceEntry<TResource> entry;
            if (_entries.TryGetValue(args.Identity, out entry))
            {
                _controlScheduler.QueueRecover(entry, args.Message);
            }
        }

        private void NormalizeEventRole(ConnectionPoolEventArgs args)
        {
            if (args == null)
            {
                return;
            }

            if (args.ResourceRole == ResourceRole.Unknown)
            {
                args.ResourceRole = _poolRole;
            }
        }
    }
}

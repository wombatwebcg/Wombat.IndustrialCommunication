using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 连接池条目。
    /// </summary>
    public class PooledConnectionEntry
    {
        private readonly AsyncLock _entryLock = new AsyncLock();
        private readonly IDictionary<string, ConnectionLease> _leases = new Dictionary<string, ConnectionLease>(StringComparer.OrdinalIgnoreCase);
        private int _failureCount;

        public PooledConnectionEntry(DeviceConnectionDescriptor descriptor, IPooledDeviceConnection connection)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            LastActiveTimeUtc = DateTime.UtcNow;
            State = ConnectionEntryState.Uninitialized;
        }

        public DeviceConnectionDescriptor Descriptor { get; private set; }

        public ConnectionIdentity Identity => Descriptor.Identity;

        public IPooledDeviceConnection Connection { get; private set; }

        public ConnectionEntryState State { get; private set; }

        public int ActiveLeaseCount => _leases.Count;

        public DateTime LastActiveTimeUtc { get; private set; }

        public int FailureCount => _failureCount;

        public async Task<OperationResult> EnsureConnectedAsync()
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (State == ConnectionEntryState.Invalidated || State == ConnectionEntryState.Disposed)
                {
                    return OperationResult.CreateFailedResult("连接条目不可用");
                }

                var result = await Connection.EnsureConnectedAsync().ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    State = _leases.Count > 0 ? ConnectionEntryState.Leased : ConnectionEntryState.Ready;
                    LastActiveTimeUtc = DateTime.UtcNow;
                }
                else
                {
                    State = ConnectionEntryState.Faulted;
                    _failureCount++;
                }

                return result;
            }
        }

        public async Task<OperationResult<ConnectionLease>> AcquireAsync(TimeSpan leaseTimeout)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (State == ConnectionEntryState.Invalidated || State == ConnectionEntryState.Disposed)
                {
                    return OperationResult.CreateFailedResult<ConnectionLease>("连接条目不可租用");
                }

                var ready = await Connection.EnsureConnectedAsync().ConfigureAwait(false);
                if (!ready.IsSuccess)
                {
                    State = ConnectionEntryState.Faulted;
                    _failureCount++;
                    return OperationResult.CreateFailedResult<ConnectionLease>(ready);
                }

                var lease = new ConnectionLease
                {
                    LeaseId = Guid.NewGuid().ToString("N"),
                    Identity = Identity,
                    AcquiredAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = DateTime.UtcNow.Add(leaseTimeout)
                };

                _leases[lease.LeaseId] = lease;
                State = ConnectionEntryState.Leased;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateSuccessResult(lease);
            }
        }

        public async Task<OperationResult> ReleaseAsync(ConnectionLease lease)
        {
            if (lease == null)
            {
                return OperationResult.CreateFailedResult("租约不能为空");
            }

            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                ConnectionLease existing;
                if (!_leases.TryGetValue(lease.LeaseId, out existing))
                {
                    return OperationResult.CreateFailedResult("租约不存在或已释放");
                }

                _leases.Remove(lease.LeaseId);
                LastActiveTimeUtc = DateTime.UtcNow;
                State = _leases.Count > 0 ? ConnectionEntryState.Leased :
                    (State == ConnectionEntryState.Invalidated ? ConnectionEntryState.Invalidated : ConnectionEntryState.Ready);
                return OperationResult.CreateSuccessResult();
            }
        }

        public async Task<OperationResult> InvalidateAsync(string reason)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                State = ConnectionEntryState.Invalidated;
                return Connection.Invalidate(reason);
            }
        }

        public async Task<OperationResult> MarkFailureAsync()
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                _failureCount++;
                State = ConnectionEntryState.Faulted;
                return OperationResult.CreateFailedResult("连接执行失败");
            }
        }

        public async Task<OperationResult> ResetFailureAsync()
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                _failureCount = 0;
                if (State != ConnectionEntryState.Invalidated && State != ConnectionEntryState.Disposed)
                {
                    State = _leases.Count > 0 ? ConnectionEntryState.Leased : ConnectionEntryState.Ready;
                }
                return OperationResult.CreateSuccessResult();
            }
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(Func<IDeviceClient, Task<OperationResult<T>>> action)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                var result = await Connection.ExecuteAsync(action).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;

                if (result.IsSuccess)
                {
                    if (_failureCount > 0)
                    {
                        _failureCount = 0;
                    }
                    State = _leases.Count > 0 ? ConnectionEntryState.Leased : ConnectionEntryState.Ready;
                }
                else
                {
                    _failureCount++;
                    State = ConnectionEntryState.Faulted;
                }

                return result;
            }
        }

        public async Task<OperationResult> ExecuteAsync(Func<IDeviceClient, Task<OperationResult>> action)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                var result = await Connection.ExecuteAsync(action).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;

                if (result.IsSuccess)
                {
                    if (_failureCount > 0)
                    {
                        _failureCount = 0;
                    }
                    State = _leases.Count > 0 ? ConnectionEntryState.Leased : ConnectionEntryState.Ready;
                }
                else
                {
                    _failureCount++;
                    State = ConnectionEntryState.Faulted;
                }

                return result;
            }
        }

        public bool CanCleanup(TimeSpan idleTimeout)
        {
            return _leases.Count == 0
                && (State == ConnectionEntryState.Ready || State == ConnectionEntryState.Faulted || State == ConnectionEntryState.Invalidated)
                && DateTime.UtcNow - LastActiveTimeUtc >= idleTimeout;
        }

        public async Task<OperationResult> DisposeAsync()
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                _leases.Clear();
                var disconnect = Connection.Disconnect();
                State = ConnectionEntryState.Disposed;
                return disconnect;
            }
        }
    }
}

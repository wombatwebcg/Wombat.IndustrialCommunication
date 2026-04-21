using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Events;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 连接池条目，负责统一维护单设备连接的生命周期。
    /// </summary>
    public class PooledConnectionEntry
    {
        private readonly AsyncLock _entryLock = new AsyncLock();
        private readonly IDictionary<string, ConnectionLease> _leases = new Dictionary<string, ConnectionLease>(StringComparer.OrdinalIgnoreCase);
        private readonly IConnectionPoolEventPublisher _eventPublisher;
        private int _failureCount;
        private int _consecutiveHealthCheckFailures;
        private bool _isUnderMaintenance;
        private bool _isRemoving;
        private string _lastFailureReason;
        private DateTime? _lastConnectedTimeUtc;
        private DateTime? _lastFailureTimeUtc;
        private DateTime? _lastRecoveredTimeUtc;
        private DateTime? _lastMaintenanceTimeUtc;
        private ConnectionPoolMaintenanceMode _lastMaintenanceMode;

        public PooledConnectionEntry(DeviceConnectionDescriptor descriptor, IPooledDeviceConnection connection, IConnectionPoolEventPublisher eventPublisher)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _eventPublisher = eventPublisher;
            LastActiveTimeUtc = DateTime.UtcNow;
            State = ConnectionEntryState.Uninitialized;
            _lastFailureReason = string.Empty;
            _lastMaintenanceMode = ConnectionPoolMaintenanceMode.Unknown;
        }

        public DeviceConnectionDescriptor Descriptor { get; private set; }

        public ConnectionIdentity Identity => Descriptor.Identity;

        public IPooledDeviceConnection Connection { get; private set; }

        public ConnectionEntryState State { get; private set; }

        public int ActiveLeaseCount
        {
            get
            {
                using (_entryLock.Lock())
                {
                    return _leases.Count;
                }
            }
        }

        public DateTime LastActiveTimeUtc { get; private set; }

        public int FailureCount => _failureCount;

        public async Task<OperationResult> EnsureConnectedAsync(ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                return await EnsureConnectedCoreAsync(mode, State == ConnectionEntryState.Reconnecting).ConfigureAwait(false);
            }
        }

        public async Task<OperationResult<ConnectionLease>> AcquireAsync(TimeSpan leaseTimeout, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (_isRemoving || State == ConnectionEntryState.Invalidated || State == ConnectionEntryState.Disposed)
                {
                    return OperationResult.CreateFailedResult<ConnectionLease>("连接条目不可租用");
                }

                var ready = await EnsureConnectedCoreAsync(mode, false).ConfigureAwait(false);
                if (!ready.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<ConnectionLease>(ready);
                }

                var now = DateTime.UtcNow;
                var lease = new ConnectionLease
                {
                    LeaseId = Guid.NewGuid().ToString("N"),
                    Identity = Identity,
                    AcquiredAtUtc = now,
                    ExpiresAtUtc = now.Add(leaseTimeout),
                    Source = mode.ToString()
                };

                _leases[lease.LeaseId] = lease;
                LastActiveTimeUtc = now;
                TransitionState(ConnectionEntryState.Leased, ConnectionPoolEventType.LeaseAcquired, "连接租约获取成功", mode, null, false);
                PublishLeaseEvent(ConnectionPoolEventType.LeaseAcquired, lease, false, "连接租约已获取", mode, null);
                return OperationResult.CreateSuccessResult(lease);
            }
        }

        public async Task<OperationResult> ReleaseAsync(ConnectionLease lease, bool leaseExpired = false, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
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
                UpdateStateAfterLeaseMutation(mode);
                PublishLeaseEvent(leaseExpired ? ConnectionPoolEventType.LeaseExpired : ConnectionPoolEventType.LeaseReleased,
                    existing,
                    leaseExpired,
                    leaseExpired ? "租约已过期释放" : "租约已释放",
                    mode,
                    null);
                return OperationResult.CreateSuccessResult();
            }
        }

        public async Task<OperationResult> InvalidateAsync(string reason, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                _lastFailureReason = string.IsNullOrEmpty(reason) ? "连接已失效" : reason;
                _lastFailureTimeUtc = DateTime.UtcNow;
                var result = Connection.Invalidate(_lastFailureReason);
                TransitionState(ConnectionEntryState.Invalidated, ConnectionPoolEventType.Invalidated, _lastFailureReason, mode, result.Exception, true);
                return result;
            }
        }

        public async Task<OperationResult> MarkFailureAsync(string reason = null, Exception exception = null, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                _failureCount++;
                _lastFailureReason = string.IsNullOrEmpty(reason) ? "连接执行失败" : reason;
                _lastFailureTimeUtc = DateTime.UtcNow;
                TransitionState(ConnectionEntryState.Faulted, ConnectionPoolEventType.ExecuteFailed, _lastFailureReason, mode, exception, true);
                return exception == null
                    ? OperationResult.CreateFailedResult(_lastFailureReason)
                    : OperationResult.CreateFailedResult(exception);
            }
        }

        public async Task<OperationResult> PrepareForRemovalAsync(bool allowActiveLeases, ConnectionPoolMaintenanceMode mode)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (State == ConnectionEntryState.Disposed)
                {
                    return OperationResult.CreateSuccessResult();
                }

                if (_isRemoving)
                {
                    return OperationResult.CreateFailedResult("连接条目正在移除");
                }

                if (!allowActiveLeases && _leases.Count > 0)
                {
                    return OperationResult.CreateFailedResult("存在活跃租约，无法移除连接条目");
                }

                _isRemoving = true;
                _lastMaintenanceTimeUtc = DateTime.UtcNow;
                _lastMaintenanceMode = mode;
                return OperationResult.CreateSuccessResult();
            }
        }

        public async Task CancelPendingRemovalAsync(ConnectionPoolMaintenanceMode mode)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (State != ConnectionEntryState.Disposed)
                {
                    _isRemoving = false;
                    _lastMaintenanceTimeUtc = DateTime.UtcNow;
                    _lastMaintenanceMode = mode;
                }
            }
        }

        public async Task<OperationResult> NotifyRetryingAsync(int attempt, int maxRetry, TimeSpan retryBackoff, ConnectionPoolMaintenanceMode mode)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                PublishEvent(ConnectionPoolEventType.Retrying,
                    State,
                    string.Format("第 {0}/{1} 次恢复重试，退避 {2} ms", attempt, maxRetry + 1, retryBackoff.TotalMilliseconds),
                    mode,
                    null);
                return OperationResult.CreateSuccessResult();
            }
        }

        public async Task<OperationResult> TryRecoverAsync(string reason, ConnectionPoolMaintenanceMode mode)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (State == ConnectionEntryState.Invalidated || State == ConnectionEntryState.Disposed)
                {
                    return OperationResult.CreateFailedResult("连接条目不可恢复");
                }

                PublishEvent(ConnectionPoolEventType.Reconnecting, State, string.IsNullOrEmpty(reason) ? "开始重连恢复" : reason, mode, null);
                TransitionState(ConnectionEntryState.Reconnecting, ConnectionPoolEventType.Reconnecting, "连接恢复中", mode, null, false);
                try
                {
                    Connection.Disconnect();
                }
                catch
                {
                }

                var recovered = await EnsureConnectedCoreAsync(mode, true).ConfigureAwait(false);
                if (!recovered.IsSuccess)
                {
                    return recovered;
                }

                await ResetFailureAsync(mode).ConfigureAwait(false);
                _lastRecoveredTimeUtc = DateTime.UtcNow;
                PublishEvent(ConnectionPoolEventType.Recovered, State, "连接恢复成功", mode, null);
                return OperationResult.CreateSuccessResult();
            }
        }

        public async Task<OperationResult> ResetFailureAsync(ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                _failureCount = 0;
                _consecutiveHealthCheckFailures = 0;
                if (State != ConnectionEntryState.Invalidated && State != ConnectionEntryState.Disposed)
                {
                    TransitionState(GetOperationalState(), ConnectionPoolEventType.Recovered, "连接已恢复", mode, null, false);
                }

                return OperationResult.CreateSuccessResult();
            }
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(Func<IDeviceClient, Task<OperationResult<T>>> action, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                PublishEvent(ConnectionPoolEventType.ExecuteStarting, State, "开始执行连接操作", mode, null);
                var result = await Connection.ExecuteAsync(action).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;

                if (result.IsSuccess)
                {
                    _failureCount = 0;
                    _consecutiveHealthCheckFailures = 0;
                    TransitionState(GetOperationalState(), ConnectionPoolEventType.ConnectSucceeded, "连接执行成功", mode, null, false);
                }
                else
                {
                    _failureCount++;
                    _lastFailureReason = string.IsNullOrWhiteSpace(result.Message) ? "连接执行失败" : result.Message;
                    _lastFailureTimeUtc = DateTime.UtcNow;
                    TransitionState(ConnectionEntryState.Faulted, ConnectionPoolEventType.ExecuteFailed, _lastFailureReason, mode, result.Exception, true);
                }

                return result;
            }
        }

        public async Task<OperationResult> ExecuteAsync(Func<IDeviceClient, Task<OperationResult>> action, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                PublishEvent(ConnectionPoolEventType.ExecuteStarting, State, "开始执行连接操作", mode, null);
                var result = await Connection.ExecuteAsync(action).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;

                if (result.IsSuccess)
                {
                    _failureCount = 0;
                    _consecutiveHealthCheckFailures = 0;
                    TransitionState(GetOperationalState(), ConnectionPoolEventType.ConnectSucceeded, "连接执行成功", mode, null, false);
                }
                else
                {
                    _failureCount++;
                    _lastFailureReason = string.IsNullOrWhiteSpace(result.Message) ? "连接执行失败" : result.Message;
                    _lastFailureTimeUtc = DateTime.UtcNow;
                    TransitionState(ConnectionEntryState.Faulted, ConnectionPoolEventType.ExecuteFailed, _lastFailureReason, mode, result.Exception, true);
                }

                return result;
            }
        }

        public bool CanCleanup(TimeSpan idleTimeout)
        {
            using (_entryLock.Lock())
            {
                return !_isRemoving
                    && _leases.Count == 0
                    && (State == ConnectionEntryState.Ready || State == ConnectionEntryState.Faulted || State == ConnectionEntryState.Invalidated)
                    && DateTime.UtcNow - LastActiveTimeUtc >= idleTimeout;
            }
        }

        public async Task<OperationResult<int>> ExpireLeasesAsync(DateTime utcNow, ConnectionPoolMaintenanceMode mode)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (_isRemoving || State == ConnectionEntryState.Disposed)
                {
                    return OperationResult.CreateSuccessResult(0);
                }

                var expired = _leases.Values.Where(l => l.IsExpired(utcNow)).ToList();
                foreach (var lease in expired)
                {
                    _leases.Remove(lease.LeaseId);
                    PublishLeaseEvent(ConnectionPoolEventType.LeaseExpired, lease, true, "检测到过期租约并已释放", mode, null);
                }

                if (expired.Count > 0)
                {
                    LastActiveTimeUtc = utcNow;
                    UpdateStateAfterLeaseMutation(mode);
                }

                return OperationResult.CreateSuccessResult(expired.Count);
            }
        }

        public async Task<OperationResult> RunHealthCheckAsync(ConnectionPoolOptions options, ConnectionPoolMaintenanceMode mode)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                _isUnderMaintenance = true;
                _lastMaintenanceTimeUtc = DateTime.UtcNow;
                _lastMaintenanceMode = mode;

                try
                {
                    if (_isRemoving)
                    {
                        return OperationResult.CreateSuccessResult("连接条目正在移除，跳过健康检查");
                    }

                    if ((options?.HealthCheckLeaseFreeOnly ?? true) && _leases.Count > 0)
                    {
                        return OperationResult.CreateSuccessResult("存在活跃租约，跳过健康检查");
                    }

                    if (State == ConnectionEntryState.Invalidated || State == ConnectionEntryState.Disposed)
                    {
                        return OperationResult.CreateSuccessResult("当前状态无需健康检查");
                    }

                    var result = await EnsureConnectedCoreAsync(mode, State == ConnectionEntryState.Faulted).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        _consecutiveHealthCheckFailures = 0;
                        return result;
                    }

                    _consecutiveHealthCheckFailures++;
                    if (_consecutiveHealthCheckFailures >= (options?.MaxConsecutiveHealthCheckFailures ?? 3))
                    {
                        await InvalidateAsync("健康检查连续失败，连接已失效", mode).ConfigureAwait(false);
                    }

                    return result;
                }
                finally
                {
                    _isUnderMaintenance = false;
                    _lastMaintenanceTimeUtc = DateTime.UtcNow;
                    _lastMaintenanceMode = mode;
                }
            }
        }

        public async Task<OperationResult> ForceReconnectAsync(string reason)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (_leases.Count > 0)
                {
                    return OperationResult.CreateFailedResult("存在活跃租约，无法强制重连");
                }

                PublishEvent(ConnectionPoolEventType.ForceReconnectRequested,
                    State,
                    string.IsNullOrEmpty(reason) ? "请求强制重连" : reason,
                    ConnectionPoolMaintenanceMode.ForceReconnect,
                    null);
            }

            return await TryRecoverAsync(reason, ConnectionPoolMaintenanceMode.ForceReconnect).ConfigureAwait(false);
        }

        public ConnectionEntrySnapshot CreateSnapshot()
        {
            using (_entryLock.Lock())
            {
                return new ConnectionEntrySnapshot
                {
                    Identity = Identity,
                    State = State,
                    ActiveLeaseCount = _leases.Count,
                    FailureCount = _failureCount,
                    HasExpiredLease = _leases.Values.Any(l => l.IsExpired(DateTime.UtcNow)),
                    IsUnderMaintenance = _isUnderMaintenance,
                    LastActiveTimeUtc = LastActiveTimeUtc,
                    LastConnectedTimeUtc = _lastConnectedTimeUtc,
                    LastFailureTimeUtc = _lastFailureTimeUtc,
                    LastRecoveredTimeUtc = _lastRecoveredTimeUtc,
                    LastMaintenanceTimeUtc = _lastMaintenanceTimeUtc,
                    LastFailureReason = _lastFailureReason,
                    LastMaintenanceMode = _lastMaintenanceMode
                };
            }
        }

        public async Task<OperationResult> DisposeAsync(ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.Dispose)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                var activeLeases = _leases.Values.ToList();
                foreach (var lease in activeLeases)
                {
                    PublishLeaseEvent(ConnectionPoolEventType.LeaseReleased,
                        lease,
                        false,
                        "连接条目释放，租约已关闭",
                        mode,
                        null);
                }

                _leases.Clear();
                _isRemoving = true;
                var disconnect = Connection.Disconnect();
                TransitionState(ConnectionEntryState.Disposed, ConnectionPoolEventType.Disposed, "连接条目已释放", mode, disconnect.Exception, false);
                return disconnect;
            }
        }

        private async Task<OperationResult> EnsureConnectedCoreAsync(ConnectionPoolMaintenanceMode mode, bool reconnecting)
        {
            if (_isRemoving || State == ConnectionEntryState.Invalidated || State == ConnectionEntryState.Disposed)
            {
                return OperationResult.CreateFailedResult("连接条目不可用");
            }

            TransitionState(reconnecting ? ConnectionEntryState.Reconnecting : ConnectionEntryState.Connecting,
                reconnecting ? ConnectionPoolEventType.Reconnecting : ConnectionPoolEventType.ConnectStarting,
                reconnecting ? "连接恢复中" : "开始建立连接",
                mode,
                null,
                false);

            var result = await Connection.EnsureConnectedAsync().ConfigureAwait(false);
            if (result.IsSuccess)
            {
                _failureCount = 0;
                _consecutiveHealthCheckFailures = 0;
                _lastConnectedTimeUtc = DateTime.UtcNow;
                _lastMaintenanceMode = mode;
                LastActiveTimeUtc = DateTime.UtcNow;
                TransitionState(GetOperationalState(),
                    ConnectionPoolEventType.ConnectSucceeded,
                    reconnecting ? "连接恢复成功" : "连接建立成功",
                    mode,
                    null,
                    false);
            }
            else
            {
                _failureCount++;
                _lastFailureReason = string.IsNullOrWhiteSpace(result.Message) ? "连接建立失败" : result.Message;
                _lastFailureTimeUtc = DateTime.UtcNow;
                _lastMaintenanceMode = mode;
                TransitionState(ConnectionEntryState.Faulted,
                    ConnectionPoolEventType.ConnectFailed,
                    _lastFailureReason,
                    mode,
                    result.Exception,
                    true);
            }

            return result;
        }

        private void UpdateStateAfterLeaseMutation(ConnectionPoolMaintenanceMode mode)
        {
            if (State == ConnectionEntryState.Invalidated || State == ConnectionEntryState.Disposed)
            {
                return;
            }

            TransitionState(GetOperationalState(), ConnectionPoolEventType.LeaseReleased, "连接租约状态已更新", mode, null, false);
        }

        private ConnectionEntryState GetOperationalState()
        {
            return _leases.Count > 0 ? ConnectionEntryState.Leased : ConnectionEntryState.Ready;
        }

        private void TransitionState(ConnectionEntryState newState, ConnectionPoolEventType eventType, string message, ConnectionPoolMaintenanceMode mode, Exception exception, bool failure)
        {
            var previousState = State;
            State = newState;
            LastActiveTimeUtc = DateTime.UtcNow;
            _lastMaintenanceMode = mode;

            if (failure && !string.IsNullOrWhiteSpace(message))
            {
                _lastFailureReason = message;
            }

            PublishEvent(eventType, newState, message, mode, exception);

            if (previousState != newState)
            {
                _eventPublisher?.PublishStateChanged(new ConnectionStateChangedEventArgs
                {
                    Identity = Identity,
                    EventType = eventType,
                    PreviousState = previousState,
                    CurrentState = newState,
                    State = newState,
                    Message = message ?? string.Empty,
                    Exception = exception,
                    ActiveLeaseCount = _leases.Count,
                    FailureCount = _failureCount,
                    TriggerMode = mode,
                    OccurredAtUtc = DateTime.UtcNow
                });
            }
        }

        private void PublishEvent(ConnectionPoolEventType eventType, ConnectionEntryState state, string message, ConnectionPoolMaintenanceMode mode, Exception exception)
        {
            _eventPublisher?.Publish(new ConnectionPoolEventArgs
            {
                Identity = Identity,
                EventType = eventType,
                State = state,
                Message = message ?? string.Empty,
                Exception = exception,
                ActiveLeaseCount = _leases.Count,
                FailureCount = _failureCount,
                TriggerMode = mode,
                OccurredAtUtc = DateTime.UtcNow
            });
        }

        private void PublishLeaseEvent(ConnectionPoolEventType eventType, ConnectionLease lease, bool leaseExpired, string message, ConnectionPoolMaintenanceMode mode, Exception exception)
        {
            _eventPublisher?.PublishLeaseEvent(new ConnectionLeaseEventArgs
            {
                Identity = Identity,
                EventType = eventType,
                State = State,
                Lease = lease,
                LeaseExpired = leaseExpired,
                Message = message ?? string.Empty,
                Exception = exception,
                ActiveLeaseCount = _leases.Count,
                FailureCount = _failureCount,
                TriggerMode = mode,
                OccurredAtUtc = DateTime.UtcNow
            });
        }
    }
}

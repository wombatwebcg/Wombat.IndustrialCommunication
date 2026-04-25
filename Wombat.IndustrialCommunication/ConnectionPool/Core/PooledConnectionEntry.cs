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
        private static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(3);
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
        private ConnectionEntryLifecycleState _lifecycleState;

        public PooledConnectionEntry(DeviceConnectionDescriptor descriptor, IPooledDeviceConnection connection, IConnectionPoolEventPublisher eventPublisher)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _eventPublisher = eventPublisher;
            LastActiveTimeUtc = DateTime.UtcNow;
            _lifecycleState = ConnectionEntryLifecycleState.Uninitialized;
            _lastFailureReason = string.Empty;
            _lastMaintenanceMode = ConnectionPoolMaintenanceMode.Unknown;
        }

        public DeviceConnectionDescriptor Descriptor { get; private set; }

        public ConnectionIdentity Identity => Descriptor.Identity;

        public IPooledDeviceConnection Connection { get; private set; }

        public ConnectionEntryState State => MapPublicState(_lifecycleState);

        public ConnectionEntryLifecycleState LifecycleState => _lifecycleState;

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
            var notifications = new List<Action>();
            OperationResult result;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                result = await EnsureConnectedCoreAsync(mode, _lifecycleState == ConnectionEntryLifecycleState.Reconnecting, notifications).ConfigureAwait(false);
            }

            PublishNotifications(notifications);
            return result;
        }

        public async Task<OperationResult<ConnectionLease>> AcquireAsync(TimeSpan leaseTimeout, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            var notifications = new List<Action>();
            OperationResult<ConnectionLease> result;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (_isRemoving || IsTerminalLifecycleState())
                {
                    result = OperationResult.CreateFailedResult<ConnectionLease>("连接条目不可租用");
                }
                else
                {
                    var ready = await EnsureConnectedCoreAsync(mode, false, notifications).ConfigureAwait(false);
                    if (!ready.IsSuccess)
                    {
                        result = OperationResult.CreateFailedResult<ConnectionLease>(ready);
                    }
                    else
                    {
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
                        TransitionState(ConnectionEntryLifecycleState.Leased, ConnectionPoolEventType.LeaseAcquired, "连接租约获取成功", mode, null, false, notifications);
                        QueueLeaseEvent(ConnectionPoolEventType.LeaseAcquired, lease, false, "连接租约已获取", mode, null, notifications);
                        result = OperationResult.CreateSuccessResult(lease);
                    }
                }
            }

            PublishNotifications(notifications);
            return result;
        }

        public async Task<OperationResult> ReleaseAsync(ConnectionLease lease, bool leaseExpired = false, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            if (lease == null)
            {
                return OperationResult.CreateFailedResult("租约不能为空");
            }

            var notifications = new List<Action>();
            OperationResult result;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                ConnectionLease existing;
                if (!_leases.TryGetValue(lease.LeaseId, out existing))
                {
                    result = OperationResult.CreateFailedResult("租约不存在或已释放");
                }
                else
                {
                    _leases.Remove(lease.LeaseId);
                    LastActiveTimeUtc = DateTime.UtcNow;
                    UpdateStateAfterLeaseMutation(mode, notifications);
                    QueueLeaseEvent(
                        leaseExpired ? ConnectionPoolEventType.LeaseExpired : ConnectionPoolEventType.LeaseReleased,
                        existing,
                        leaseExpired,
                        leaseExpired ? "租约已过期释放" : "租约已释放",
                        mode,
                        null,
                        notifications);
                    result = OperationResult.CreateSuccessResult();
                }
            }

            PublishNotifications(notifications);
            return result;
        }

        public async Task<OperationResult> InvalidateAsync(string reason, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            var notifications = new List<Action>();
            OperationResult result;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                result = InvalidateCore(reason, mode, notifications);
            }

            PublishNotifications(notifications);
            return result;
        }

        public async Task<OperationResult> MarkFailureAsync(string reason = null, Exception exception = null, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            var notifications = new List<Action>();
            OperationResult result;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                _failureCount++;
                _lastFailureReason = string.IsNullOrEmpty(reason) ? "连接执行失败" : reason;
                _lastFailureTimeUtc = DateTime.UtcNow;
                TransitionState(ConnectionEntryLifecycleState.Faulted, ConnectionPoolEventType.ExecuteFailed, _lastFailureReason, mode, exception, true, notifications);
                result = exception == null
                    ? OperationResult.CreateFailedResult(_lastFailureReason)
                    : OperationResult.CreateFailedResult(exception);
            }

            PublishNotifications(notifications);
            return result;
        }

        public async Task<OperationResult> PrepareForRemovalAsync(bool allowActiveLeases, ConnectionPoolMaintenanceMode mode)
        {
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (_lifecycleState == ConnectionEntryLifecycleState.Disposed)
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
                if (_lifecycleState != ConnectionEntryLifecycleState.Disposed)
                {
                    _isRemoving = false;
                    _lastMaintenanceTimeUtc = DateTime.UtcNow;
                    _lastMaintenanceMode = mode;
                }
            }
        }

        public async Task<OperationResult> NotifyRetryingAsync(int attempt, int maxRetry, TimeSpan retryBackoff, ConnectionPoolMaintenanceMode mode)
        {
            var notifications = new List<Action>();
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                QueuePoolEvent(
                    ConnectionPoolEventType.Retrying,
                    State,
                    _lifecycleState,
                    string.Format("第 {0}/{1} 次恢复重试，退避 {2} ms", attempt, maxRetry + 1, retryBackoff.TotalMilliseconds),
                    mode,
                    null,
                    notifications);
            }

            PublishNotifications(notifications);
            return OperationResult.CreateSuccessResult();
        }

        public async Task<OperationResult> TryRecoverAsync(string reason, ConnectionPoolMaintenanceMode mode)
        {
            var notifications = new List<Action>();
            OperationResult result;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                result = await TryRecoverCoreAsync(reason, mode, notifications).ConfigureAwait(false);
            }

            PublishNotifications(notifications);
            return result;
        }

        public async Task<OperationResult> ResetFailureAsync(ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            var notifications = new List<Action>();
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                ResetFailureCore(mode, notifications);
            }

            PublishNotifications(notifications);
            return OperationResult.CreateSuccessResult();
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(Func<IDeviceClient, Task<OperationResult<T>>> action, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            var notifications = new List<Action>();
            OperationResult<T> result;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                QueuePoolEvent(ConnectionPoolEventType.ExecuteStarting, State, _lifecycleState, "开始执行连接操作", mode, null, notifications);
                result = await Connection.ExecuteAsync(action).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;

                if (result.IsSuccess)
                {
                    _failureCount = 0;
                    _consecutiveHealthCheckFailures = 0;
                    TransitionState(GetOperationalState(), ConnectionPoolEventType.ConnectSucceeded, "连接执行成功", mode, null, false, notifications);
                }
                else
                {
                    _failureCount++;
                    _lastFailureReason = string.IsNullOrWhiteSpace(result.Message) ? "连接执行失败" : result.Message;
                    _lastFailureTimeUtc = DateTime.UtcNow;
                    TransitionState(ConnectionEntryLifecycleState.Faulted, ConnectionPoolEventType.ExecuteFailed, _lastFailureReason, mode, result.Exception, true, notifications);
                }
            }

            PublishNotifications(notifications);
            return result;
        }

        public async Task<OperationResult> ExecuteAsync(Func<IDeviceClient, Task<OperationResult>> action, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            var notifications = new List<Action>();
            OperationResult result;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                QueuePoolEvent(ConnectionPoolEventType.ExecuteStarting, State, _lifecycleState, "开始执行连接操作", mode, null, notifications);
                result = await Connection.ExecuteAsync(action).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;

                if (result.IsSuccess)
                {
                    _failureCount = 0;
                    _consecutiveHealthCheckFailures = 0;
                    TransitionState(GetOperationalState(), ConnectionPoolEventType.ConnectSucceeded, "连接执行成功", mode, null, false, notifications);
                }
                else
                {
                    _failureCount++;
                    _lastFailureReason = string.IsNullOrWhiteSpace(result.Message) ? "连接执行失败" : result.Message;
                    _lastFailureTimeUtc = DateTime.UtcNow;
                    TransitionState(ConnectionEntryLifecycleState.Faulted, ConnectionPoolEventType.ExecuteFailed, _lastFailureReason, mode, result.Exception, true, notifications);
                }
            }

            PublishNotifications(notifications);
            return result;
        }

        public bool CanCleanup(TimeSpan idleTimeout)
        {
            using (_entryLock.Lock())
            {
                return !_isRemoving
                    && _leases.Count == 0
                    && (_lifecycleState == ConnectionEntryLifecycleState.Ready
                        || _lifecycleState == ConnectionEntryLifecycleState.Faulted
                        || _lifecycleState == ConnectionEntryLifecycleState.Invalidated)
                    && DateTime.UtcNow - LastActiveTimeUtc >= idleTimeout;
            }
        }

        public async Task<OperationResult<int>> ExpireLeasesAsync(DateTime utcNow, ConnectionPoolMaintenanceMode mode)
        {
            var notifications = new List<Action>();
            OperationResult<int> result;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (_isRemoving || _lifecycleState == ConnectionEntryLifecycleState.Disposed)
                {
                    result = OperationResult.CreateSuccessResult(0);
                }
                else
                {
                    var expired = _leases.Values.Where(l => l.IsExpired(utcNow)).ToList();
                    for (var i = 0; i < expired.Count; i++)
                    {
                        var lease = expired[i];
                        _leases.Remove(lease.LeaseId);
                        QueueLeaseEvent(ConnectionPoolEventType.LeaseExpired, lease, true, "检测到过期租约并已释放", mode, null, notifications);
                    }

                    if (expired.Count > 0)
                    {
                        LastActiveTimeUtc = utcNow;
                        UpdateStateAfterLeaseMutation(mode, notifications);
                    }

                    result = OperationResult.CreateSuccessResult(expired.Count);
                }
            }

            PublishNotifications(notifications);
            return result;
        }

        public async Task<OperationResult> RunHealthCheckAsync(ConnectionPoolOptions options, ConnectionPoolMaintenanceMode mode)
        {
            var notifications = new List<Action>();
            OperationResult result;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                _isUnderMaintenance = true;
                _lastMaintenanceTimeUtc = DateTime.UtcNow;
                _lastMaintenanceMode = mode;

                try
                {
                    if (_isRemoving)
                    {
                        result = OperationResult.CreateSuccessResult("连接条目正在移除，跳过健康检查");
                    }
                    else if ((options?.HealthCheckLeaseFreeOnly ?? true) && _leases.Count > 0)
                    {
                        result = OperationResult.CreateSuccessResult("存在活跃租约，跳过健康检查");
                    }
                    else if (IsTerminalLifecycleState())
                    {
                        result = OperationResult.CreateSuccessResult("当前状态无需健康检查");
                    }
                    else
                    {
                        result = await RunHealthCheckCoreAsync(options, mode, notifications).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _isUnderMaintenance = false;
                    _lastMaintenanceTimeUtc = DateTime.UtcNow;
                    _lastMaintenanceMode = mode;
                }
            }

            PublishNotifications(notifications);
            return result;
        }

        public async Task<OperationResult> ForceReconnectAsync(string reason)
        {
            var notifications = new List<Action>();
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (_leases.Count > 0)
                {
                    return OperationResult.CreateFailedResult("存在活跃租约，无法强制重连");
                }

                QueuePoolEvent(
                    ConnectionPoolEventType.ForceReconnectRequested,
                    State,
                    _lifecycleState,
                    string.IsNullOrEmpty(reason) ? "请求强制重连" : reason,
                    ConnectionPoolMaintenanceMode.ForceReconnect,
                    null,
                    notifications);
            }

            PublishNotifications(notifications);
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
                    LifecycleState = _lifecycleState,
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
            var notifications = new List<Action>();
            OperationResult disconnect;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                var activeLeases = _leases.Values.ToList();
                for (var i = 0; i < activeLeases.Count; i++)
                {
                    QueueLeaseEvent(ConnectionPoolEventType.LeaseReleased, activeLeases[i], false, "连接条目释放，租约已关闭", mode, null, notifications);
                }

                _leases.Clear();
                _isRemoving = true;
                disconnect = Connection.Disconnect();
                TransitionState(ConnectionEntryLifecycleState.Disposed, ConnectionPoolEventType.Disposed, "连接条目已释放", mode, disconnect.Exception, false, notifications);
            }

            PublishNotifications(notifications);
            return disconnect;
        }

        private async Task<OperationResult> EnsureConnectedCoreAsync(ConnectionPoolMaintenanceMode mode, bool reconnecting, IList<Action> notifications)
        {
            if (_isRemoving || IsTerminalLifecycleState())
            {
                return OperationResult.CreateFailedResult("连接条目不可用");
            }

            TransitionState(
                reconnecting ? ConnectionEntryLifecycleState.Reconnecting : ConnectionEntryLifecycleState.Connecting,
                reconnecting ? ConnectionPoolEventType.Reconnecting : ConnectionPoolEventType.ConnectStarting,
                reconnecting ? "连接恢复中" : "开始建立连接",
                mode,
                null,
                false,
                notifications);

            var result = await Connection.EnsureConnectedAsync().ConfigureAwait(false);
            if (result.IsSuccess)
            {
                _failureCount = 0;
                _consecutiveHealthCheckFailures = 0;
                _lastConnectedTimeUtc = DateTime.UtcNow;
                _lastMaintenanceMode = mode;
                LastActiveTimeUtc = DateTime.UtcNow;
                TransitionState(
                    GetOperationalState(),
                    ConnectionPoolEventType.ConnectSucceeded,
                    reconnecting ? "连接恢复成功" : "连接建立成功",
                    mode,
                    null,
                    false,
                    notifications);
            }
            else
            {
                _failureCount++;
                _lastFailureReason = string.IsNullOrWhiteSpace(result.Message) ? "连接建立失败" : result.Message;
                _lastFailureTimeUtc = DateTime.UtcNow;
                _lastMaintenanceMode = mode;
                TransitionState(ConnectionEntryLifecycleState.Faulted, ConnectionPoolEventType.ConnectFailed, _lastFailureReason, mode, result.Exception, true, notifications);
            }

            return result;
        }

        private async Task<OperationResult> RunHealthCheckCoreAsync(ConnectionPoolOptions options, ConnectionPoolMaintenanceMode mode, IList<Action> notifications)
        {
            var utcNow = DateTime.UtcNow;
            var hasEstablishedSession = Connection.State == ConnectionEntryLifecycleState.Ready
                || Connection.State == ConnectionEntryLifecycleState.Leased
                || (Connection.Client != null && Connection.Client.Connected);
            if (hasEstablishedSession)
            {
                var probeResult = await Connection.ProbeAsync(GetProbeTimeout(options)).ConfigureAwait(false);
                if (probeResult.IsSuccess)
                {
                    _failureCount = 0;
                    _consecutiveHealthCheckFailures = 0;
                    LastActiveTimeUtc = utcNow;
                    TransitionState(GetOperationalState(), ConnectionPoolEventType.ConnectSucceeded, "连接探活成功", mode, null, false, notifications);
                    return probeResult;
                }

                _failureCount++;
                _consecutiveHealthCheckFailures++;
                _lastFailureReason = string.IsNullOrWhiteSpace(probeResult.Message) ? "连接探活失败" : probeResult.Message;
                _lastFailureTimeUtc = utcNow;
                TransitionState(ConnectionEntryLifecycleState.Faulted, ConnectionPoolEventType.ConnectFailed, _lastFailureReason, mode, probeResult.Exception, true, notifications);

                if (_consecutiveHealthCheckFailures >= GetMaxConsecutiveHealthCheckFailures(options))
                {
                    return InvalidateCore("健康检查连续失败，连接已失效", mode, notifications);
                }

                if (CanRecoverNow(options, utcNow))
                {
                    return await TryRecoverCoreAsync("探活失败，尝试恢复连接", mode, notifications).ConfigureAwait(false);
                }

                return probeResult;
            }

            if (_lifecycleState == ConnectionEntryLifecycleState.Faulted && !CanRecoverNow(options, utcNow))
            {
                return OperationResult.CreateSuccessResult("恢复冷却中，跳过本轮恢复");
            }

            if (_lifecycleState == ConnectionEntryLifecycleState.Faulted)
            {
                return await TryRecoverCoreAsync("健康检查触发恢复", mode, notifications).ConfigureAwait(false);
            }

            return await EnsureConnectedCoreAsync(mode, _lifecycleState == ConnectionEntryLifecycleState.Reconnecting, notifications).ConfigureAwait(false);
        }

        private async Task<OperationResult> TryRecoverCoreAsync(string reason, ConnectionPoolMaintenanceMode mode, IList<Action> notifications)
        {
            if (IsTerminalLifecycleState())
            {
                return OperationResult.CreateFailedResult("连接条目不可恢复");
            }

            QueuePoolEvent(ConnectionPoolEventType.Reconnecting, State, _lifecycleState, string.IsNullOrEmpty(reason) ? "开始重连恢复" : reason, mode, null, notifications);
            TransitionState(ConnectionEntryLifecycleState.Reconnecting, ConnectionPoolEventType.Reconnecting, "连接恢复中", mode, null, false, notifications);
            try
            {
                Connection.Disconnect();
            }
            catch
            {
            }

            var recovered = await EnsureConnectedCoreAsync(mode, true, notifications).ConfigureAwait(false);
            if (!recovered.IsSuccess)
            {
                return recovered;
            }

            ResetFailureCore(mode, notifications);
            _lastRecoveredTimeUtc = DateTime.UtcNow;
            QueuePoolEvent(ConnectionPoolEventType.Recovered, State, _lifecycleState, "连接恢复成功", mode, null, notifications);
            return OperationResult.CreateSuccessResult();
        }

        private OperationResult InvalidateCore(string reason, ConnectionPoolMaintenanceMode mode, IList<Action> notifications)
        {
            _lastFailureReason = string.IsNullOrEmpty(reason) ? "连接已失效" : reason;
            _lastFailureTimeUtc = DateTime.UtcNow;
            var result = Connection.Invalidate(_lastFailureReason);
            TransitionState(ConnectionEntryLifecycleState.Invalidated, ConnectionPoolEventType.Invalidated, _lastFailureReason, mode, result.Exception, true, notifications);
            return result;
        }

        private void ResetFailureCore(ConnectionPoolMaintenanceMode mode, IList<Action> notifications)
        {
            _failureCount = 0;
            _consecutiveHealthCheckFailures = 0;
            if (!IsTerminalLifecycleState())
            {
                TransitionState(GetOperationalState(), ConnectionPoolEventType.Recovered, "连接已恢复", mode, null, false, notifications);
            }
        }

        private void UpdateStateAfterLeaseMutation(ConnectionPoolMaintenanceMode mode, IList<Action> notifications)
        {
            if (IsTerminalLifecycleState())
            {
                return;
            }

            TransitionState(GetOperationalState(), ConnectionPoolEventType.LeaseReleased, "连接租约状态已更新", mode, null, false, notifications);
        }

        private ConnectionEntryLifecycleState GetOperationalState()
        {
            return _leases.Count > 0 ? ConnectionEntryLifecycleState.Leased : ConnectionEntryLifecycleState.Ready;
        }

        private void TransitionState(ConnectionEntryLifecycleState newState, ConnectionPoolEventType eventType, string message, ConnectionPoolMaintenanceMode mode, Exception exception, bool failure, IList<Action> notifications)
        {
            var previousLifecycleState = _lifecycleState;
            var previousState = MapPublicState(previousLifecycleState);
            _lifecycleState = newState;
            var currentState = MapPublicState(newState);
            LastActiveTimeUtc = DateTime.UtcNow;
            _lastMaintenanceMode = mode;

            if (failure && !string.IsNullOrWhiteSpace(message))
            {
                _lastFailureReason = message;
            }

            QueuePoolEvent(eventType, currentState, newState, message, mode, exception, notifications);
            if (previousLifecycleState != newState || previousState != currentState)
            {
                QueueStateChangedEvent(previousState, currentState, previousLifecycleState, newState, eventType, message, mode, exception, notifications);
            }
        }

        private void QueuePoolEvent(ConnectionPoolEventType eventType, ConnectionEntryState state, ConnectionEntryLifecycleState lifecycleState, string message, ConnectionPoolMaintenanceMode mode, Exception exception, IList<Action> notifications)
        {
            var args = new ConnectionPoolEventArgs
            {
                Identity = Identity,
                EventType = eventType,
                State = state,
                LifecycleState = lifecycleState,
                Message = message ?? string.Empty,
                Exception = exception,
                ActiveLeaseCount = _leases.Count,
                FailureCount = _failureCount,
                TriggerMode = mode,
                OccurredAtUtc = DateTime.UtcNow
            };

            notifications.Add(() => _eventPublisher?.Publish(args));
        }

        private void QueueStateChangedEvent(ConnectionEntryState previousState, ConnectionEntryState currentState, ConnectionEntryLifecycleState previousLifecycleState, ConnectionEntryLifecycleState currentLifecycleState, ConnectionPoolEventType eventType, string message, ConnectionPoolMaintenanceMode mode, Exception exception, IList<Action> notifications)
        {
            var args = new ConnectionStateChangedEventArgs
            {
                Identity = Identity,
                EventType = eventType,
                PreviousState = previousState,
                CurrentState = currentState,
                PreviousLifecycleState = previousLifecycleState,
                CurrentLifecycleState = currentLifecycleState,
                State = currentState,
                LifecycleState = currentLifecycleState,
                Message = message ?? string.Empty,
                Exception = exception,
                ActiveLeaseCount = _leases.Count,
                FailureCount = _failureCount,
                TriggerMode = mode,
                OccurredAtUtc = DateTime.UtcNow
            };

            notifications.Add(() => _eventPublisher?.PublishStateChanged(args));
        }

        private void QueueLeaseEvent(ConnectionPoolEventType eventType, ConnectionLease lease, bool leaseExpired, string message, ConnectionPoolMaintenanceMode mode, Exception exception, IList<Action> notifications)
        {
            var args = new ConnectionLeaseEventArgs
            {
                Identity = Identity,
                EventType = eventType,
                State = State,
                LifecycleState = _lifecycleState,
                Lease = lease,
                LeaseExpired = leaseExpired,
                Message = message ?? string.Empty,
                Exception = exception,
                ActiveLeaseCount = _leases.Count,
                FailureCount = _failureCount,
                TriggerMode = mode,
                OccurredAtUtc = DateTime.UtcNow
            };

            notifications.Add(() => _eventPublisher?.PublishLeaseEvent(args));
        }

        private void PublishNotifications(IList<Action> notifications)
        {
            if (notifications == null || notifications.Count == 0)
            {
                return;
            }

            for (var i = 0; i < notifications.Count; i++)
            {
                notifications[i]();
            }
        }

        private bool CanRecoverNow(ConnectionPoolOptions options, DateTime utcNow)
        {
            var cooldown = options == null ? TimeSpan.Zero : options.FaultedReconnectCooldown;
            return cooldown <= TimeSpan.Zero
                || !_lastFailureTimeUtc.HasValue
                || utcNow - _lastFailureTimeUtc.Value >= cooldown;
        }

        private static TimeSpan GetProbeTimeout(ConnectionPoolOptions options)
        {
            if (options == null || options.ProbeTimeout <= TimeSpan.Zero)
            {
                return DefaultProbeTimeout;
            }

            return options.ProbeTimeout;
        }

        private static int GetMaxConsecutiveHealthCheckFailures(ConnectionPoolOptions options)
        {
            if (options == null || options.MaxConsecutiveHealthCheckFailures <= 0)
            {
                return 3;
            }

            return options.MaxConsecutiveHealthCheckFailures;
        }

        private bool IsTerminalLifecycleState()
        {
            return _lifecycleState == ConnectionEntryLifecycleState.Invalidated
                || _lifecycleState == ConnectionEntryLifecycleState.Disposed;
        }

        private static ConnectionEntryState MapPublicState(ConnectionEntryLifecycleState lifecycleState)
        {
            switch (lifecycleState)
            {
                case ConnectionEntryLifecycleState.Ready:
                    return ConnectionEntryState.Ready;
                case ConnectionEntryLifecycleState.Leased:
                    return ConnectionEntryState.Busy;
                case ConnectionEntryLifecycleState.Faulted:
                case ConnectionEntryLifecycleState.Invalidated:
                    return ConnectionEntryState.Unavailable;
                case ConnectionEntryLifecycleState.Uninitialized:
                case ConnectionEntryLifecycleState.Connecting:
                case ConnectionEntryLifecycleState.Reconnecting:
                case ConnectionEntryLifecycleState.Disposed:
                default:
                    return ConnectionEntryState.Disconnected;
            }
        }
    }
}

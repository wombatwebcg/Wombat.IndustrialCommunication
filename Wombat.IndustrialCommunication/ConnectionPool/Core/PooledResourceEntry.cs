using System;
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
    /// 连接池条目，负责统一维护单设备连接的生命周期。
    /// </summary>
    public class PooledResourceEntry<TResource>
    {
        private static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(3);
        private const string ForceCloseRequestedMessage = "连接正在强制关闭";
        private const string ForceClosedExecutionMessage = "连接已被强制关闭，读取已终止";
        private const string ForceClosedSuccessMessage = "连接已强制关闭";
        private const string OperationCancelledMessage = "操作已取消";
        private readonly AsyncLock _entryLock = new AsyncLock();
        private readonly IDictionary<string, ConnectionLease> _leases = new Dictionary<string, ConnectionLease>(StringComparer.OrdinalIgnoreCase);
        private readonly IConnectionPoolEventPublisher _eventPublisher;
        private CancellationTokenSource _activeExecutionCancellationTokenSource;
        private int _activeExecutionCount;
        private TaskCompletionSource<bool> _activeExecutionDrainSource;
        private int _failureCount;
        private int _consecutiveHealthCheckFailures;
        private bool _isUnderMaintenance;
        private bool _isRemoving;
        private bool _forceClosing;
        private string _forceCloseReason;
        private string _lastFailureReason;
        private DateTime? _lastConnectedTimeUtc;
        private DateTime? _lastFailureTimeUtc;
        private DateTime? _lastRecoveredTimeUtc;
        private DateTime? _lastMaintenanceTimeUtc;
        private ConnectionPoolMaintenanceMode _lastMaintenanceMode;
        private ConnectionEntryLifecycleState _lifecycleState;
        private ConnectionEntrySnapshot _latestSnapshot;
        private int _stateVersion;

        public PooledResourceEntry(ResourceDescriptor descriptor, IPooledResourceConnection<TResource> connection, IConnectionPoolEventPublisher eventPublisher)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _eventPublisher = eventPublisher;
            LastActiveTimeUtc = DateTime.UtcNow;
            _lifecycleState = ConnectionEntryLifecycleState.Uninitialized;
            _lastFailureReason = string.Empty;
            _forceCloseReason = string.Empty;
            _lastMaintenanceMode = ConnectionPoolMaintenanceMode.Unknown;
            _activeExecutionCancellationTokenSource = new CancellationTokenSource();
            _activeExecutionDrainSource = CreateDrainSource(true);
            _latestSnapshot = CreateSnapshotCore(LastActiveTimeUtc);
            _stateVersion = 0;
        }

        public ResourceDescriptor Descriptor { get; private set; }

        public ConnectionIdentity Identity => Descriptor.Identity;

        public IPooledResourceConnection<TResource> Connection { get; private set; }

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

        public bool IsForceClosingRequested
        {
            get
            {
                using (_entryLock.Lock())
                {
                    return IsForceClosingRequestedCore();
                }
            }
        }

        public CancellationToken GetExecutionCancellationToken()
        {
            using (_entryLock.Lock())
            {
                return _activeExecutionCancellationTokenSource.Token;
            }
        }

        public async Task<OperationResult> EnsureAvailableAsync(ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
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
                else if (IsForceClosingRequestedCore())
                {
                    result = OperationResult.CreateFailedResult<ConnectionLease>(ForceCloseRequestedMessage);
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
                    result = IsForceClosingRequestedCore() || IsTerminalLifecycleState()
                        ? OperationResult.CreateSuccessResult("租约不存在或已释放")
                        : OperationResult.CreateFailedResult("租约不存在或已释放");
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

        public async Task<OperationResult> ForceCloseAsync(string reason, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return CreateCancelledOperationResult();
            }

            var notifications = new List<Action>();
            var effectiveReason = string.IsNullOrWhiteSpace(reason) ? "请求强制关闭连接" : reason;
            var shouldCancelExecutions = false;
            var shouldDisconnect = false;
            Task drainTask;

            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (_lifecycleState == ConnectionEntryLifecycleState.Disposed)
                {
                    return OperationResult.CreateSuccessResult("连接条目已释放");
                }

                if (_lifecycleState == ConnectionEntryLifecycleState.Invalidated && !_forceClosing)
                {
                    return OperationResult.CreateSuccessResult("连接条目已处于已关闭状态");
                }

                if (!_forceClosing)
                {
                    _forceClosing = true;
                    _forceCloseReason = effectiveReason;
                    TransitionState(ConnectionEntryLifecycleState.ForceClosing, ConnectionPoolEventType.ForceCloseRequested, effectiveReason, ConnectionPoolMaintenanceMode.ForceClose, null, false, notifications);
                    QueuePoolEvent(ConnectionPoolEventType.ForceCloseCancelling, State, _lifecycleState, "正在取消活跃执行并关闭底层连接", ConnectionPoolMaintenanceMode.ForceClose, null, notifications);
                    shouldCancelExecutions = true;
                    shouldDisconnect = true;
                }

                drainTask = _activeExecutionDrainSource.Task;
            }

            PublishNotifications(notifications);

            if (shouldCancelExecutions)
            {
                TryCancelActiveExecutions();
            }

            OperationResult disconnectResult = OperationResult.CreateSuccessResult();
            if (shouldDisconnect)
            {
                disconnectResult = Connection.DisconnectOrShutdown();
            }

            var drained = await WaitForDrainAsync(drainTask, cancellationToken).ConfigureAwait(false);
            if (!drained)
            {
                return CreateCancelledOperationResult();
            }

            notifications = new List<Action>();
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (_lifecycleState == ConnectionEntryLifecycleState.Invalidated && !_forceClosing)
                {
                    return OperationResult.CreateSuccessResult("连接条目已处于已关闭状态");
                }

                ReleaseAllLeasesCore(ConnectionPoolMaintenanceMode.ForceClose, "连接被强制关闭，租约已自动回收", notifications);
                _forceClosing = false;
                TransitionState(ConnectionEntryLifecycleState.Invalidated, ConnectionPoolEventType.ForceClosed, ForceClosedSuccessMessage, ConnectionPoolMaintenanceMode.ForceClose, disconnectResult.Exception, !disconnectResult.IsSuccess, notifications);
            }

            PublishNotifications(notifications);
            return disconnectResult.IsSuccess
                ? OperationResult.CreateSuccessResult(ForceClosedSuccessMessage)
                : OperationResult.CreateFailedResult(disconnectResult);
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
                if (IsForceClosingRequestedCore())
                {
                    result = OperationResult.CreateSuccessResult(ForceCloseRequestedMessage);
                }
                else
                {
                    _failureCount++;
                    _lastFailureReason = string.IsNullOrEmpty(reason) ? "连接执行失败" : reason;
                    _lastFailureTimeUtc = DateTime.UtcNow;
                    TransitionState(ConnectionEntryLifecycleState.Faulted, ConnectionPoolEventType.ExecuteFailed, _lastFailureReason, mode, exception, true, notifications);
                    result = exception == null
                        ? OperationResult.CreateFailedResult(_lastFailureReason)
                        : OperationResult.CreateFailedResult(exception);
                }
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
                IncrementStateVersionCore();
                RefreshSnapshotCore(_lastMaintenanceTimeUtc.Value);
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
                    IncrementStateVersionCore();
                    RefreshSnapshotCore(_lastMaintenanceTimeUtc.Value);
                }
            }
        }

        public async Task<OperationResult> NotifyRetryingAsync(int attempt, int maxRetry, TimeSpan retryBackoff, ConnectionPoolMaintenanceMode mode)
        {
            var notifications = new List<Action>();
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (!IsForceClosingRequestedCore())
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
                RefreshSnapshotCore(DateTime.UtcNow);
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

        public async Task<OperationResult<T>> ExecuteAsync<T>(Func<TResource, CancellationToken, Task<OperationResult<T>>> action, CancellationToken cancellationToken, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            if (action == null)
            {
                return OperationResult.CreateFailedResult<T>("执行委托不能为空");
            }

            var notifications = new List<Action>();
            OperationResult<T> result;
            var startVersion = 0;
            var blocked = false;
            var executionCancellationRequested = false;
            CancellationTokenSource linkedCancellationTokenSource = null;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                if (_isRemoving || IsTerminalLifecycleState())
                {
                    result = OperationResult.CreateFailedResult<T>("连接条目不可用");
                    RefreshSnapshotCore(DateTime.UtcNow);
                    blocked = true;
                }
                else if (IsForceClosingRequestedCore())
                {
                    result = CreateExecutionCancelledResultCore<T>(null, true);
                    RefreshSnapshotCore(DateTime.UtcNow);
                    blocked = true;
                }
                else
                {
                    startVersion = _stateVersion;
                    RegisterActiveExecutionCore();
                    linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _activeExecutionCancellationTokenSource.Token);
                    QueuePoolEvent(ConnectionPoolEventType.ExecuteStarting, State, _lifecycleState, "开始执行连接操作", mode, null, notifications);
                    LastActiveTimeUtc = DateTime.UtcNow;
                    RefreshSnapshotCore(LastActiveTimeUtc);
                    result = null;
                }
            }

            if (blocked)
            {
                PublishNotifications(notifications);
                return result;
            }

            try
            {
                result = await Connection.ExecuteAsync(resource => action(resource, linkedCancellationTokenSource.Token)).ConfigureAwait(false);
                executionCancellationRequested = linkedCancellationTokenSource != null && linkedCancellationTokenSource.IsCancellationRequested;
            }
            finally
            {
                if (linkedCancellationTokenSource != null)
                {
                    executionCancellationRequested = executionCancellationRequested || linkedCancellationTokenSource.IsCancellationRequested;
                    linkedCancellationTokenSource.Dispose();
                }
            }

            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                var utcNow = DateTime.UtcNow;
                LastActiveTimeUtc = utcNow;
                result = NormalizeExecutionResultCore(result, executionCancellationRequested);
                CompleteActiveExecutionCore();

                if (_isRemoving || IsTerminalLifecycleState() || _stateVersion != startVersion)
                {
                    RefreshSnapshotCore(utcNow);
                }
                else if (result != null && result.IsSuccess)
                {
                    _failureCount = 0;
                    _consecutiveHealthCheckFailures = 0;
                    TransitionState(GetOperationalState(), ConnectionPoolEventType.ConnectSucceeded, "连接执行成功", mode, null, false, notifications);
                }
                else if (result != null && result.IsCancelled)
                {
                    RefreshSnapshotCore(utcNow);
                }
                else
                {
                    _failureCount++;
                    _lastFailureReason = result == null || string.IsNullOrWhiteSpace(result.Message) ? "连接执行失败" : result.Message;
                    _lastFailureTimeUtc = utcNow;
                    TransitionState(ConnectionEntryLifecycleState.Faulted, ConnectionPoolEventType.ExecuteFailed, _lastFailureReason, mode, result == null ? null : result.Exception, true, notifications);
                }
            }

            PublishNotifications(notifications);
            return result;
        }

        public async Task<OperationResult> ExecuteAsync(Func<TResource, CancellationToken, Task<OperationResult>> action, CancellationToken cancellationToken, ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.UserCall)
        {
            var wrapped = await ExecuteAsync<object>(async (resource, executionToken) =>
            {
                var result = await action(resource, executionToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    return OperationResult.CreateSuccessResult<object>(null);
                }

                var wrappedResult = OperationResult.CreateFailedResult<object>(result);
                wrappedResult.IsCancelled = result.IsCancelled;
                return wrappedResult;
            }, cancellationToken, mode).ConfigureAwait(false);

            if (wrapped.IsSuccess)
            {
                return new OperationResult().SetInfo(wrapped).Complete();
            }

            var failed = OperationResult.CreateFailedResult(wrapped);
            failed.IsCancelled = wrapped.IsCancelled;
            return failed.Complete();
        }

        public bool CanCleanup(TimeSpan idleTimeout)
        {
            using (_entryLock.Lock())
            {
                return !_isRemoving
                    && !_forceClosing
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
                if (_isRemoving || _lifecycleState == ConnectionEntryLifecycleState.Disposed || _forceClosing)
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
                    else if (_forceClosing)
                    {
                        result = OperationResult.CreateSuccessResult("连接条目正在强制关闭，跳过健康检查");
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
                    RefreshSnapshotCore(_lastMaintenanceTimeUtc.Value);
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
                if (_forceClosing)
                {
                    return OperationResult.CreateFailedResult("连接条目正在强制关闭，无法强制重连");
                }

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
                return CreateSnapshotCore(DateTime.UtcNow);
            }
        }

        public ConnectionEntrySnapshot GetCachedSnapshot()
        {
            var snapshot = _latestSnapshot;
            if (snapshot == null)
            {
                return CreateSnapshot();
            }

            return CloneSnapshot(snapshot);
        }

        public async Task<OperationResult> DisposeAsync(ConnectionPoolMaintenanceMode mode = ConnectionPoolMaintenanceMode.Dispose)
        {
            var notifications = new List<Action>();
            OperationResult disconnect;
            using (await _entryLock.LockAsync().ConfigureAwait(false))
            {
                ReleaseAllLeasesCore(mode, "连接条目释放，租约已关闭", notifications);
                _isRemoving = true;
                _forceClosing = false;
                TryCancelActiveExecutions();
                disconnect = Connection.DisconnectOrShutdown();
                TransitionState(ConnectionEntryLifecycleState.Disposed, ConnectionPoolEventType.Disposed, "连接条目已释放", mode, disconnect.Exception, false, notifications);
            }

            PublishNotifications(notifications);
            _activeExecutionCancellationTokenSource.Dispose();
            return disconnect;
        }

        private async Task<OperationResult> EnsureConnectedCoreAsync(ConnectionPoolMaintenanceMode mode, bool reconnecting, IList<Action> notifications)
        {
            if (_isRemoving || IsTerminalLifecycleState())
            {
                return OperationResult.CreateFailedResult("连接条目不可用");
            }

            if (IsForceClosingRequestedCore())
            {
                return OperationResult.CreateFailedResult(ForceCloseRequestedMessage);
            }

            TransitionState(
                reconnecting ? ConnectionEntryLifecycleState.Reconnecting : ConnectionEntryLifecycleState.Connecting,
                reconnecting ? ConnectionPoolEventType.Reconnecting : ConnectionPoolEventType.ConnectStarting,
                reconnecting ? "连接恢复中" : "开始建立连接",
                mode,
                null,
                false,
                notifications);

            var result = await Connection.EnsureAvailableAsync().ConfigureAwait(false);
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
                || Connection.IsAvailable;
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

            if (IsForceClosingRequestedCore())
            {
                return OperationResult.CreateFailedResult(ForceCloseRequestedMessage);
            }

            QueuePoolEvent(ConnectionPoolEventType.Reconnecting, State, _lifecycleState, string.IsNullOrEmpty(reason) ? "开始重连恢复" : reason, mode, null, notifications);
            TransitionState(ConnectionEntryLifecycleState.Reconnecting, ConnectionPoolEventType.Reconnecting, "连接恢复中", mode, null, false, notifications);
            try
            {
                Connection.DisconnectOrShutdown();
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
            _forceClosing = false;
            TransitionState(ConnectionEntryLifecycleState.Invalidated, ConnectionPoolEventType.Invalidated, _lastFailureReason, mode, result.Exception, true, notifications);
            return result;
        }

        private void ResetFailureCore(ConnectionPoolMaintenanceMode mode, IList<Action> notifications)
        {
            _failureCount = 0;
            _consecutiveHealthCheckFailures = 0;
            if (!IsTerminalLifecycleState() && !_forceClosing)
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

            if (_forceClosing)
            {
                RefreshSnapshotCore(DateTime.UtcNow);
                return;
            }

            TransitionState(GetOperationalState(), ConnectionPoolEventType.LeaseReleased, "连接租约状态已更新", mode, null, false, notifications);
        }

        private ConnectionEntryLifecycleState GetOperationalState()
        {
            return _leases.Count > 0 ? ConnectionEntryLifecycleState.Leased : ConnectionEntryLifecycleState.Ready;
        }

        private bool IsForceClosingRequestedCore()
        {
            return _forceClosing || _lifecycleState == ConnectionEntryLifecycleState.ForceClosing;
        }

        private void RegisterActiveExecutionCore()
        {
            if (_activeExecutionCount == 0)
            {
                _activeExecutionDrainSource = CreateDrainSource(false);
            }

            _activeExecutionCount++;
            IncrementStateVersionCore();
            RefreshSnapshotCore(DateTime.UtcNow);
        }

        private void CompleteActiveExecutionCore()
        {
            if (_activeExecutionCount > 0)
            {
                _activeExecutionCount--;
            }

            if (_activeExecutionCount == 0 && _activeExecutionDrainSource != null)
            {
                _activeExecutionDrainSource.TrySetResult(true);
            }

            IncrementStateVersionCore();
            RefreshSnapshotCore(DateTime.UtcNow);
        }

        private void ReleaseAllLeasesCore(ConnectionPoolMaintenanceMode mode, string message, IList<Action> notifications)
        {
            var activeLeases = _leases.Values.ToList();
            for (var i = 0; i < activeLeases.Count; i++)
            {
                QueueLeaseEvent(ConnectionPoolEventType.LeaseReleased, activeLeases[i], false, message, mode, null, notifications);
            }

            _leases.Clear();
        }

        private void TryCancelActiveExecutions()
        {
            try
            {
                _activeExecutionCancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private OperationResult<T> NormalizeExecutionResultCore<T>(OperationResult<T> result, bool cancellationRequested)
        {
            if (!cancellationRequested && (result == null || !result.IsCancelled))
            {
                return result;
            }

            return CreateExecutionCancelledResultCore<T>(result, IsForceClosingRequestedCore());
        }

        public OperationResult<T> CreateCancelledExecutionResult<T>(CancellationToken cancellationToken)
        {
            using (_entryLock.Lock())
            {
                return CreateExecutionCancelledResultCore<T>(null, IsForceClosingRequestedCore());
            }
        }

        private OperationResult<T> CreateExecutionCancelledResultCore<T>(OperationResult source, bool forceClosed)
        {
            var cancelled = OperationResult.CreateFailedResult<T>(forceClosed ? ForceClosedExecutionMessage : OperationCancelledMessage);
            cancelled.IsCancelled = true;
            if (source != null)
            {
                cancelled.Exception = source.Exception;
                MergeOperationTrace(cancelled, source);
            }

            return cancelled.Complete();
        }

        private OperationResult CreateCancelledOperationResult()
        {
            var cancelled = OperationResult.CreateFailedResult(OperationCancelledMessage);
            cancelled.IsCancelled = true;
            return cancelled.Complete();
        }

        private void TransitionState(ConnectionEntryLifecycleState newState, ConnectionPoolEventType eventType, string message, ConnectionPoolMaintenanceMode mode, Exception exception, bool failure, IList<Action> notifications)
        {
            var previousLifecycleState = _lifecycleState;
            var previousState = MapPublicState(previousLifecycleState);
            _lifecycleState = newState;
            var currentState = MapPublicState(newState);
            LastActiveTimeUtc = DateTime.UtcNow;
            _lastMaintenanceMode = mode;
            IncrementStateVersionCore();

            if (failure && !string.IsNullOrWhiteSpace(message))
            {
                _lastFailureReason = message;
            }

            RefreshSnapshotCore(LastActiveTimeUtc);
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
                ResourceRole = Descriptor.ResourceRole,
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
                ResourceRole = Descriptor.ResourceRole,
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
                ResourceRole = Descriptor.ResourceRole,
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

        private static async Task<bool> WaitForDrainAsync(Task drainTask, CancellationToken cancellationToken)
        {
            if (drainTask == null)
            {
                return true;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                await drainTask.ConfigureAwait(false);
                return true;
            }

            var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var completedTask = await Task.WhenAny(drainTask, cancellationTask).ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, drainTask))
            {
                return false;
            }

            await drainTask.ConfigureAwait(false);
            return true;
        }

        private static TaskCompletionSource<bool> CreateDrainSource(bool completed)
        {
            var drainSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (completed)
            {
                drainSource.TrySetResult(true);
            }

            return drainSource;
        }

        private void IncrementStateVersionCore()
        {
            _stateVersion++;
        }

        private void RefreshSnapshotCore(DateTime capturedAtUtc)
        {
            _latestSnapshot = CreateSnapshotCore(capturedAtUtc);
        }

        private static void MergeOperationTrace(OperationResult target, OperationResult source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (source.Requsts != null && source.Requsts.Count > 0)
            {
                for (var i = 0; i < source.Requsts.Count; i++)
                {
                    var request = source.Requsts[i];
                    if (!target.Requsts.Contains(request))
                    {
                        target.Requsts.Add(request);
                    }
                }
            }

            if (source.Responses != null && source.Responses.Count > 0)
            {
                for (var i = 0; i < source.Responses.Count; i++)
                {
                    var response = source.Responses[i];
                    if (!target.Responses.Contains(response))
                    {
                        target.Responses.Add(response);
                    }
                }
            }

            if (source.OperationInfo != null && source.OperationInfo.Count > 0)
            {
                for (var i = 0; i < source.OperationInfo.Count; i++)
                {
                    var info = source.OperationInfo[i];
                    if (!string.IsNullOrWhiteSpace(info) && !target.OperationInfo.Contains(info))
                    {
                        target.OperationInfo.Add(info);
                    }
                }
            }
        }

        private ConnectionEntrySnapshot CreateSnapshotCore(DateTime capturedAtUtc)
        {
            return new ConnectionEntrySnapshot
            {
                Identity = Identity,
                State = State,
                LifecycleState = _lifecycleState,
                ActiveLeaseCount = _leases.Count,
                FailureCount = _failureCount,
                HasExpiredLease = _leases.Values.Any(l => l.IsExpired(capturedAtUtc)),
                IsUnderMaintenance = _isUnderMaintenance,
                LastActiveTimeUtc = LastActiveTimeUtc,
                CapturedAtUtc = capturedAtUtc,
                LastConnectedTimeUtc = _lastConnectedTimeUtc,
                LastFailureTimeUtc = _lastFailureTimeUtc,
                LastRecoveredTimeUtc = _lastRecoveredTimeUtc,
                LastMaintenanceTimeUtc = _lastMaintenanceTimeUtc,
                LastFailureReason = _lastFailureReason,
                LastMaintenanceMode = _lastMaintenanceMode
            };
        }

        private static ConnectionEntrySnapshot CloneSnapshot(ConnectionEntrySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new ConnectionEntrySnapshot();
            }

            return new ConnectionEntrySnapshot
            {
                Identity = snapshot.Identity,
                State = snapshot.State,
                LifecycleState = snapshot.LifecycleState,
                ActiveLeaseCount = snapshot.ActiveLeaseCount,
                FailureCount = snapshot.FailureCount,
                HasExpiredLease = snapshot.HasExpiredLease,
                IsUnderMaintenance = snapshot.IsUnderMaintenance,
                LastActiveTimeUtc = snapshot.LastActiveTimeUtc,
                CapturedAtUtc = snapshot.CapturedAtUtc,
                LastConnectedTimeUtc = snapshot.LastConnectedTimeUtc,
                LastFailureTimeUtc = snapshot.LastFailureTimeUtc,
                LastRecoveredTimeUtc = snapshot.LastRecoveredTimeUtc,
                LastMaintenanceTimeUtc = snapshot.LastMaintenanceTimeUtc,
                LastFailureReason = snapshot.LastFailureReason,
                LastMaintenanceMode = snapshot.LastMaintenanceMode
            };
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
                case ConnectionEntryLifecycleState.ForceClosing:
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

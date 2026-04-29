using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public abstract class BasePooledResourceConnection<TResource> : IPooledResourceConnection<TResource>
    {
        private readonly AsyncLock _sync = new AsyncLock();

        protected BasePooledResourceConnection(ConnectionIdentity identity, TResource resource)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Resource = resource;
            State = ConnectionEntryLifecycleState.Uninitialized;
            LastActiveTimeUtc = DateTime.UtcNow;
        }

        public ConnectionIdentity Identity { get; private set; }
        public ConnectionEntryLifecycleState State { get; protected set; }
        public DateTime LastActiveTimeUtc { get; protected set; }
        public bool IsAvailable => IsAvailableCore();
        public TResource Resource { get; private set; }

        public OperationResult EnsureAvailable()
        {
            return EnsureAvailableAsync().GetAwaiter().GetResult();
        }

        public async Task<OperationResult> EnsureAvailableAsync()
        {
            using (await _sync.LockAsync().ConfigureAwait(false))
            {
                if (State == ConnectionEntryLifecycleState.Invalidated || State == ConnectionEntryLifecycleState.Disposed)
                {
                    return OperationResult.CreateFailedResult("资源已失效或已释放");
                }

                if (IsAvailableCore())
                {
                    State = ConnectionEntryLifecycleState.Ready;
                    LastActiveTimeUtc = DateTime.UtcNow;
                    return OperationResult.CreateSuccessResult();
                }

                if (State != ConnectionEntryLifecycleState.Uninitialized)
                {
                    BestEffortDisconnectOrShutdownCore();
                }
                State = ConnectionEntryLifecycleState.Connecting;
                var result = await EnsureAvailableCoreAsync().ConfigureAwait(false);
                State = result.IsSuccess ? ConnectionEntryLifecycleState.Ready : ConnectionEntryLifecycleState.Faulted;
                if (result.IsSuccess)
                {
                    LastActiveTimeUtc = DateTime.UtcNow;
                }

                return result;
            }
        }

        public async Task<OperationResult> ProbeAsync(TimeSpan timeout)
        {
            using (await _sync.LockAsync().ConfigureAwait(false))
            {
                if (State == ConnectionEntryLifecycleState.Invalidated || State == ConnectionEntryLifecycleState.Disposed)
                {
                    return OperationResult.CreateFailedResult("资源已失效或已释放");
                }

                if (!IsAvailableCore())
                {
                    State = ConnectionEntryLifecycleState.Faulted;
                    return OperationResult.CreateFailedResult("底层资源不可用");
                }

                var result = await ExecuteProbeWithTimeoutAsync(timeout).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                State = result.IsSuccess ? ConnectionEntryLifecycleState.Ready : ConnectionEntryLifecycleState.Faulted;
                return result;
            }
        }

        public OperationResult Invalidate(string reason)
        {
            State = ConnectionEntryLifecycleState.Invalidated;
            LastActiveTimeUtc = DateTime.UtcNow;
            BestEffortDisconnectOrShutdownCore();
            return OperationResult.CreateFailedResult(string.IsNullOrEmpty(reason) ? "资源已失效" : reason);
        }

        public OperationResult DisconnectOrShutdown()
        {
            try
            {
                var result = DisconnectOrShutdownCore();
                LastActiveTimeUtc = DateTime.UtcNow;
                State = result.IsSuccess ? ConnectionEntryLifecycleState.Uninitialized : ConnectionEntryLifecycleState.Faulted;
                return result;
            }
            catch (Exception ex)
            {
                State = ConnectionEntryLifecycleState.Faulted;
                return OperationResult.CreateFailedResult(ex);
            }
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(Func<TResource, Task<OperationResult<T>>> action)
        {
            if (action == null)
            {
                return OperationResult.CreateFailedResult<T>("执行委托不能为空");
            }

            var ready = await EnsureAvailableAsync().ConfigureAwait(false);
            if (!ready.IsSuccess)
            {
                return OperationResult.CreateFailedResult<T>(ready);
            }

            try
            {
                var result = await action(Resource).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                State = IsAvailableCore() ? ConnectionEntryLifecycleState.Ready : ConnectionEntryLifecycleState.Faulted;
                return result;
            }
            catch (Exception ex)
            {
                State = ConnectionEntryLifecycleState.Faulted;
                return OperationResult.CreateFailedResult<T>(ex);
            }
        }

        public async Task<OperationResult> ExecuteAsync(Func<TResource, Task<OperationResult>> action)
        {
            if (action == null)
            {
                return OperationResult.CreateFailedResult("执行委托不能为空");
            }

            var ready = await EnsureAvailableAsync().ConfigureAwait(false);
            if (!ready.IsSuccess)
            {
                return ready;
            }

            try
            {
                var result = await action(Resource).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                State = IsAvailableCore() ? ConnectionEntryLifecycleState.Ready : ConnectionEntryLifecycleState.Faulted;
                return result;
            }
            catch (Exception ex)
            {
                State = ConnectionEntryLifecycleState.Faulted;
                return OperationResult.CreateFailedResult(ex);
            }
        }

        protected abstract bool IsAvailableCore();
        protected abstract Task<OperationResult> EnsureAvailableCoreAsync();
        protected abstract OperationResult DisconnectOrShutdownCore();

        protected virtual Task<OperationResult> ProbeCoreAsync()
        {
            return Task.FromResult(IsAvailableCore() ? OperationResult.CreateSuccessResult("资源探活成功") : OperationResult.CreateFailedResult("底层资源不可用"));
        }

        private async Task<OperationResult> ExecuteProbeWithTimeoutAsync(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
            {
                return await ProbeCoreAsync().ConfigureAwait(false);
            }

            var probeTask = ProbeCoreAsync();
            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(probeTask, timeoutTask).ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, probeTask))
            {
                return OperationResult.CreateFailedResult(string.Format("资源探活超时，超时时间 {0} ms", timeout.TotalMilliseconds));
            }

            return await probeTask.ConfigureAwait(false);
        }

        private void BestEffortDisconnectOrShutdownCore()
        {
            try { DisconnectOrShutdownCore(); } catch { }
        }
    }
}

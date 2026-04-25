using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    /// <summary>
    /// 池化连接通用基类。
    /// </summary>
    public abstract class BasePooledDeviceConnection : IPooledDeviceConnection
    {
        private readonly AsyncLock _sync = new AsyncLock();

        protected BasePooledDeviceConnection(ConnectionIdentity identity, IDeviceClient client)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Client.IsLongConnection = true;
            State = ConnectionEntryLifecycleState.Uninitialized;
            LastActiveTimeUtc = DateTime.UtcNow;
        }

        public ConnectionIdentity Identity { get; private set; }

        public ConnectionEntryLifecycleState State { get; protected set; }

        public DateTime LastActiveTimeUtc { get; protected set; }

        public IDeviceClient Client { get; private set; }

        public OperationResult EnsureConnected()
        {
            return EnsureConnectedAsync().GetAwaiter().GetResult();
        }

        public async Task<OperationResult> EnsureConnectedAsync()
        {
            using (await _sync.LockAsync().ConfigureAwait(false))
            {
                if (State == ConnectionEntryLifecycleState.Invalidated || State == ConnectionEntryLifecycleState.Disposed)
                {
                    return OperationResult.CreateFailedResult("连接已失效或已释放");
                }

                if (Client.Connected)
                {
                    State = ConnectionEntryLifecycleState.Ready;
                    LastActiveTimeUtc = DateTime.UtcNow;
                    return OperationResult.CreateSuccessResult();
                }

                BestEffortDisconnectCore();
                State = ConnectionEntryLifecycleState.Connecting;
                var result = await Client.ConnectAsync().ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    State = ConnectionEntryLifecycleState.Ready;
                    LastActiveTimeUtc = DateTime.UtcNow;
                }
                else
                {
                    State = ConnectionEntryLifecycleState.Faulted;
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
                    return OperationResult.CreateFailedResult("连接已失效或已释放");
                }

                if (!Client.Connected)
                {
                    State = ConnectionEntryLifecycleState.Faulted;
                    return OperationResult.CreateFailedResult("底层连接未建立");
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
            BestEffortDisconnectCore();
            return OperationResult.CreateFailedResult(string.IsNullOrEmpty(reason) ? "连接已失效" : reason);
        }

        public OperationResult Disconnect()
        {
            try
            {
                var result = Client.Disconnect();
                LastActiveTimeUtc = DateTime.UtcNow;
                State = ConnectionEntryLifecycleState.Disposed;
                return result;
            }
            catch (Exception ex)
            {
                State = ConnectionEntryLifecycleState.Faulted;
                return OperationResult.CreateFailedResult(ex);
            }
        }

        public async Task<OperationResult<T>> ExecuteAsync<T>(Func<IDeviceClient, Task<OperationResult<T>>> action)
        {
            if (action == null)
            {
                return OperationResult.CreateFailedResult<T>("执行委托不能为空");
            }

            var ready = await EnsureConnectedAsync().ConfigureAwait(false);
            if (!ready.IsSuccess)
            {
                return OperationResult.CreateFailedResult<T>(ready);
            }

            try
            {
                var result = await action(Client).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                State = Client.Connected ? ConnectionEntryLifecycleState.Ready : ConnectionEntryLifecycleState.Faulted;
                return result;
            }
            catch (Exception ex)
            {
                State = ConnectionEntryLifecycleState.Faulted;
                return OperationResult.CreateFailedResult<T>(ex);
            }
        }

        public async Task<OperationResult> ExecuteAsync(Func<IDeviceClient, Task<OperationResult>> action)
        {
            if (action == null)
            {
                return OperationResult.CreateFailedResult("执行委托不能为空");
            }

            var ready = await EnsureConnectedAsync().ConfigureAwait(false);
            if (!ready.IsSuccess)
            {
                return ready;
            }

            try
            {
                var result = await action(Client).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                State = Client.Connected ? ConnectionEntryLifecycleState.Ready : ConnectionEntryLifecycleState.Faulted;
                return result;
            }
            catch (Exception ex)
            {
                State = ConnectionEntryLifecycleState.Faulted;
                return OperationResult.CreateFailedResult(ex);
            }
        }

        protected virtual Task<OperationResult> ProbeCoreAsync()
        {
            return Task.FromResult(Client.Connected
                ? OperationResult.CreateSuccessResult("连接探活成功")
                : OperationResult.CreateFailedResult("底层连接未建立"));
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
                return OperationResult.CreateFailedResult(string.Format("连接探活超时，超时时间 {0} ms", timeout.TotalMilliseconds));
            }

            return await probeTask.ConfigureAwait(false);
        }

        private void BestEffortDisconnectCore()
        {
            try
            {
                Client.Disconnect();
            }
            catch
            {
            }
        }
    }
}

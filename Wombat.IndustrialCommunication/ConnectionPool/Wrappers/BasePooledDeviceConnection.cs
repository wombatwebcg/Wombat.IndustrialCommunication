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
            State = ConnectionEntryState.Uninitialized;
            LastActiveTimeUtc = DateTime.UtcNow;
        }

        public ConnectionIdentity Identity { get; private set; }

        public ConnectionEntryState State { get; protected set; }

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
                if (State == ConnectionEntryState.Invalidated || State == ConnectionEntryState.Disposed)
                {
                    return OperationResult.CreateFailedResult("连接已失效或已释放");
                }

                if (Client.Connected)
                {
                    State = ConnectionEntryState.Ready;
                    LastActiveTimeUtc = DateTime.UtcNow;
                    return OperationResult.CreateSuccessResult();
                }

                State = ConnectionEntryState.Connecting;
                var result = await Client.ConnectAsync().ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    State = ConnectionEntryState.Ready;
                    LastActiveTimeUtc = DateTime.UtcNow;
                }
                else
                {
                    State = ConnectionEntryState.Faulted;
                }

                return result;
            }
        }

        public OperationResult Invalidate(string reason)
        {
            State = ConnectionEntryState.Invalidated;
            return OperationResult.CreateFailedResult(string.IsNullOrEmpty(reason) ? "连接已失效" : reason);
        }

        public OperationResult Disconnect()
        {
            try
            {
                var result = Client.Disconnect();
                State = ConnectionEntryState.Disposed;
                return result;
            }
            catch (Exception ex)
            {
                State = ConnectionEntryState.Faulted;
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
                State = ConnectionEntryState.Leased;
                var result = await action(Client).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                State = Client.Connected ? ConnectionEntryState.Ready : ConnectionEntryState.Faulted;
                return result;
            }
            catch (Exception ex)
            {
                State = ConnectionEntryState.Faulted;
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
                State = ConnectionEntryState.Leased;
                var result = await action(Client).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                State = Client.Connected ? ConnectionEntryState.Ready : ConnectionEntryState.Faulted;
                return result;
            }
            catch (Exception ex)
            {
                State = ConnectionEntryState.Faulted;
                return OperationResult.CreateFailedResult(ex);
            }
        }
    }
}

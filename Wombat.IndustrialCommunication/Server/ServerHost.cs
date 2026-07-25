using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.Servers
{
    public sealed class ServerHost : IAsyncDisposable
    {
        private readonly SemaphoreSlim _lifecycleLock = new SemaphoreSlim(1, 1);
        private bool _disposed;

        public ServerHost(string id, IDeviceServer server)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Server id is required.", nameof(id));
            Id = id;
            Server = server ?? throw new ArgumentNullException(nameof(server));
        }

        public string Id { get; }

        public IDeviceServer Server { get; }

        public bool IsRunning => Server.IsListening;

        public async Task<OperationResult> StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (Server.IsListening) return OperationResult.CreateFailedResult("Server is already running.");
                return await Server.ListenAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }

        public async Task<OperationResult> StopAsync()
        {
            if (_disposed) return OperationResult.CreateSuccessResult();
            await _lifecycleLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await Server.ShutdownAsync().ConfigureAwait(false);
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            await StopAsync().ConfigureAwait(false);
            _disposed = true;
            (Server as IDisposable)?.Dispose();
            _lifecycleLock.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ServerHost));
        }
    }
}

using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;

namespace Wombat.IndustrialCommunication.Extensions.Bluetooth
{
    public class BluetoothStreamAdapter : IStreamResource
    {
        private readonly IBluetoothChannel _channel;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private bool _disposed;

        public BluetoothStreamAdapter(IBluetoothChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        public ILogger Logger { get; set; }

        public bool Connected => !_disposed && _channel.Connected;

        public TimeSpan ConnectTimeout
        {
            get => _channel.ConnectTimeout;
            set => _channel.ConnectTimeout = value;
        }

        public TimeSpan ReceiveTimeout
        {
            get => _channel.ReceiveTimeout;
            set => _channel.ReceiveTimeout = value;
        }

        public TimeSpan SendTimeout
        {
            get => _channel.SendTimeout;
            set => _channel.SendTimeout = value;
        }

        public OperationResult Connect()
        {
            return ConnectAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public Task<OperationResult> ConnectAsync()
        {
            return ConnectAsync(CancellationToken.None);
        }

        public async Task<OperationResult> ConnectAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var result = await _channel.ConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                Logger?.LogWarning("蓝牙连接失败: {Message}", result.Message);
            }
            return result;
        }

        public OperationResult Disconnect()
        {
            return DisconnectAsync().GetAwaiter().GetResult();
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            if (_disposed)
            {
                return OperationResult.CreateSuccessResult();
            }

            var result = await _channel.DisconnectAsync().ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                Logger?.LogWarning("蓝牙断开失败: {Message}", result.Message);
            }
            return result;
        }

        public async Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ValidateBufferRange(buffer, offset, size);

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!Connected)
                {
                    return OperationResult.CreateFailedResult("蓝牙未连接");
                }

                try
                {
                    return await _channel.SendAsync(buffer, offset, size, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "蓝牙发送异常");
                    return OperationResult.CreateFailedResult("蓝牙发送异常: " + ex.Message);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ValidateBufferRange(buffer, offset, size);

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!Connected)
                {
                    return OperationResult.CreateFailedResult<int>("蓝牙未连接");
                }

                try
                {
                    return await _channel.ReceiveAsync(buffer, offset, size, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "蓝牙接收异常");
                    return OperationResult.CreateFailedResult<int>("蓝牙接收异常: " + ex.Message);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public void StreamClose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _channel.DisconnectAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                StreamClose();
                _channel.Dispose();
            }
            finally
            {
                _disposed = true;
            }
        }

        private static void ValidateBufferRange(byte[] buffer, int offset, int size)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BluetoothStreamAdapter));
            }
        }
    }
}

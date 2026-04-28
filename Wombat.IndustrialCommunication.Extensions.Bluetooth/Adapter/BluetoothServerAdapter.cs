using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;

namespace Wombat.IndustrialCommunication.Extensions.Bluetooth
{
    /// <summary>
    /// 将蓝牙通道封装为单连接服务端监听模型。
    /// </summary>
    public class BluetoothServerAdapter : IServerListener
    {
        private readonly IBluetoothChannel _channel;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _listeningCts;
        private Task _listeningTask;
        private BluetoothServerSession _session;
        private bool _disposed;
        private bool _isListening;

        public BluetoothServerAdapter(IBluetoothChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        public ILogger Logger { get; set; }

        public bool Connected => !_disposed && _isListening && _channel.Connected;

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

        public event EventHandler<DataReceivedEventArgs> DataReceived;

        public event EventHandler<SessionEventArgs> ClientConnected;

        public event EventHandler<SessionEventArgs> ClientDisconnected;

        public async Task<OperationResult> ListenAsync()
        {
            ThrowIfDisposed();

            if (_isListening)
            {
                return OperationResult.CreateSuccessResult("蓝牙服务已在监听");
            }

            var connectResult = await _channel.ConnectAsync(CancellationToken.None).ConfigureAwait(false);
            if (!connectResult.IsSuccess)
            {
                Logger?.LogWarning("启动蓝牙服务监听失败: {Message}", connectResult.Message);
                return connectResult;
            }

            _session = new BluetoothServerSession(Guid.NewGuid(), this);
            _listeningCts = new CancellationTokenSource();
            _listeningTask = Task.Run(() => ListeningLoopAsync(_listeningCts.Token), _listeningCts.Token);
            _isListening = true;

            ClientConnected?.Invoke(this, new SessionEventArgs(_session));
            Logger?.LogInformation("蓝牙服务端监听已启动");
            return OperationResult.CreateSuccessResult();
        }

        public async Task<OperationResult> ShutdownAsync()
        {
            if (_disposed)
            {
                return OperationResult.CreateSuccessResult();
            }

            _isListening = false;
            _listeningCts?.Cancel();

            if (_listeningTask != null)
            {
                try
                {
                    await _listeningTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            var session = _session;
            _session = null;
            if (session != null)
            {
                ClientDisconnected?.Invoke(this, new SessionEventArgs(session));
            }

            _listeningTask = null;
            _listeningCts?.Dispose();
            _listeningCts = null;

            var disconnectResult = await _channel.DisconnectAsync().ConfigureAwait(false);
            if (!disconnectResult.IsSuccess)
            {
                Logger?.LogWarning("停止蓝牙服务监听时断开连接失败: {Message}", disconnectResult.Message);
            }

            Logger?.LogInformation("蓝牙服务端监听已停止");
            return disconnectResult.IsSuccess ? OperationResult.CreateSuccessResult() : disconnectResult;
        }

        public async Task<OperationResult<int>> Receive(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ValidateBufferRange(buffer, offset, length);

            if (!Connected)
            {
                return OperationResult.CreateFailedResult<int>("蓝牙服务未监听");
            }

            return await _channel.ReceiveAsync(buffer, offset, length, cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult> Send(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ValidateBufferRange(buffer, offset, length);

            if (!Connected)
            {
                return OperationResult.CreateFailedResult("蓝牙服务未监听");
            }

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await _channel.SendAsync(buffer, offset, length, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
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
                ShutdownAsync().GetAwaiter().GetResult();
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
                _sendLock.Dispose();
            }
        }

        internal Task<OperationResult> SendAsync(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return Send(data, 0, data.Length, CancellationToken.None);
        }

        private async Task ListeningLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var receiveResult = await _channel.ReceiveAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (!receiveResult.IsSuccess)
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Logger?.LogWarning("蓝牙服务端接收失败: {Message}", receiveResult.Message);
                            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                        }

                        continue;
                    }

                    if (receiveResult.ResultValue <= 0)
                    {
                        await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var data = new byte[receiveResult.ResultValue];
                    Array.Copy(buffer, 0, data, 0, data.Length);

                    if (_session != null)
                    {
                        DataReceived?.Invoke(this, new DataReceivedEventArgs(_session, data));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    Logger?.LogError(ex, "蓝牙服务端监听循环异常");
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
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
                throw new ObjectDisposedException(nameof(BluetoothServerAdapter));
            }
        }

        private sealed class BluetoothServerSession : INetworkSession
        {
            private readonly BluetoothServerAdapter _adapter;

            public BluetoothServerSession(Guid id, BluetoothServerAdapter adapter)
            {
                Id = id;
                _adapter = adapter;
            }

            public Guid Id { get; }

            public void Close()
            {
            }

            public Task<OperationResult> SendAsync(byte[] data)
            {
                return _adapter.SendAsync(data);
            }
        }
    }
}

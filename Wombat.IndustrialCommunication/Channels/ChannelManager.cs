using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Abstractions;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.Channels
{
    public interface IChannelManager : IAsyncDisposable
    {
        event EventHandler<ChannelStateChangedEventArgs> StateChanged;
        ValueTask AddAsync(ChannelOptions options, CancellationToken cancellationToken = default);
        ValueTask RemoveAsync(string channelId);
        ValueTask RestartAsync(string channelId, ChannelOptions options, CancellationToken cancellationToken = default);
        ValueTask<TResult> ExecuteAsync<TResult>(string channelId, Func<IProtocolClient, CancellationToken, ValueTask<TResult>> operation, CancellationToken cancellationToken = default);
        bool TryGetSnapshot(string channelId, out ChannelSnapshot snapshot);
    }

    public sealed class ChannelManager : IChannelManager
    {
        private readonly Dictionary<string, ChannelRuntime> _channels = new Dictionary<string, ChannelRuntime>(StringComparer.Ordinal);
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly Func<ChannelOptions, IProtocolClient> _factory;
        private bool _disposed;
        private Task _disposeTask;

        public ChannelManager() : this(CreateClient) { }
        public ChannelManager(Func<ChannelOptions, IProtocolClient> factory) { _factory = factory ?? throw new ArgumentNullException(nameof(factory)); }

        public event EventHandler<ChannelStateChangedEventArgs> StateChanged;

        public async ValueTask AddAsync(ChannelOptions options, CancellationToken cancellationToken = default)
        {
            Validate(options);
            var snapshot = Snapshot(options);
            var runtimeOptions = new ChannelRuntimeOptions(snapshot);
            var runtime = new ChannelRuntime(runtimeOptions, _factory(snapshot));
            runtime.StateChanged += ForwardStateChanged;
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                if (_channels.ContainsKey(options.Id)) throw new InvalidOperationException($"Channel '{options.Id}' already exists.");
                _channels.Add(options.Id, runtime);
            }
            finally { _gate.Release(); }

            await runtime.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask RemoveAsync(string channelId)
        {
            ChannelRuntime runtime = null;
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_channels.TryGetValue(channelId, out runtime)) _channels.Remove(channelId);
            }
            finally { _gate.Release(); }
            if (runtime != null) await runtime.DisposeAsync().ConfigureAwait(false);
        }

        public async ValueTask RestartAsync(string channelId, ChannelOptions options, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(channelId, options?.Id, StringComparison.Ordinal)) throw new ArgumentException("Restart options must use the same channel id.", nameof(options));
            await RemoveAsync(channelId).ConfigureAwait(false);
            await AddAsync(options, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask<TResult> ExecuteAsync<TResult>(string channelId, Func<IProtocolClient, CancellationToken, ValueTask<TResult>> operation, CancellationToken cancellationToken = default)
        {
            ChannelRuntime runtime;
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                if (!_channels.TryGetValue(channelId, out runtime)) throw new KeyNotFoundException($"Channel '{channelId}' was not found.");
            }
            finally { _gate.Release(); }
            return await runtime.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);
        }

        public bool TryGetSnapshot(string channelId, out ChannelSnapshot snapshot)
        {
            _gate.Wait();
            try
            {
                if (_channels.TryGetValue(channelId, out var runtime))
                {
                    snapshot = runtime.GetSnapshot();
                    return true;
                }
                snapshot = null;
                return false;
            }
            finally { _gate.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            Task disposeTask;
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposeTask == null)
                {
                    _disposed = true;
                    var runtimes = new ChannelRuntime[_channels.Count];
                    _channels.Values.CopyTo(runtimes, 0);
                    _channels.Clear();
                    _disposeTask = DisposeRuntimesAsync(runtimes);
                }
                disposeTask = _disposeTask;
            }
            finally { _gate.Release(); }
            await disposeTask.ConfigureAwait(false);
        }

        private static async Task DisposeRuntimesAsync(ChannelRuntime[] runtimes)
        {
            foreach (var runtime in runtimes) await runtime.DisposeAsync().ConfigureAwait(false);
        }

        private void ForwardStateChanged(object sender, ChannelStateChangedEventArgs e)
        {
            try { StateChanged?.Invoke(this, e); }
            catch { }
        }

        private static IProtocolClient CreateClient(ChannelOptions options)
        {
            if (options is ModbusTcpChannelOptions tcp)
            {
                return new ModbusTcpClient(tcp.Host, tcp.Port) { ConnectTimeout = tcp.ConnectTimeout, SendTimeout = tcp.OperationTimeout, ReceiveTimeout = tcp.OperationTimeout, Retries = 0 };
            }
            if (options is ModbusRtuChannelOptions rtu)
            {
                return new ModbusRtuClient(rtu.PortName, rtu.BaudRate, rtu.DataBits, rtu.StopBits, rtu.Parity, rtu.Handshake) { ConnectTimeout = rtu.ConnectTimeout, SendTimeout = rtu.OperationTimeout, ReceiveTimeout = rtu.OperationTimeout, Retries = 0 };
            }
            if (options is SiemensS7ChannelOptions s7)
            {
                return new SiemensClient(s7.Host, s7.Port, s7.Version, s7.Slot, s7.Rack) { ConnectTimeout = s7.ConnectTimeout, SendTimeout = s7.OperationTimeout, ReceiveTimeout = s7.OperationTimeout, Retries = 0 };
            }
            if (options is FinsTcpChannelOptions fins)
            {
                return new FinsClient(fins.Host, fins.Port) { ConnectTimeout = fins.ConnectTimeout, SendTimeout = fins.OperationTimeout, ReceiveTimeout = fins.OperationTimeout, Retries = 0 };
            }
            throw new NotSupportedException($"Channel options '{options.GetType().Name}' are not supported.");
        }

        private static void Validate(ChannelOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.MaxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(options.MaxConcurrency));
            if (options.ConnectTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.ConnectTimeout));
            if (options.OperationTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.OperationTimeout));
            if (options.Reconnect == null || options.Reconnect.MaxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(options.Reconnect));
        }

        private static ChannelOptions Snapshot(ChannelOptions options)
        {
            ChannelOptions snapshot = options switch
            {
                ModbusTcpChannelOptions tcp => new ModbusTcpChannelOptions(tcp.Id, tcp.Host, tcp.Port),
                ModbusRtuChannelOptions rtu => new ModbusRtuChannelOptions(rtu.Id, rtu.PortName)
                {
                    BaudRate = rtu.BaudRate, DataBits = rtu.DataBits, Parity = rtu.Parity, StopBits = rtu.StopBits, Handshake = rtu.Handshake
                },
                SiemensS7ChannelOptions s7 => new SiemensS7ChannelOptions(s7.Id, s7.Host, s7.Version, s7.Port) { Slot = s7.Slot, Rack = s7.Rack },
                FinsTcpChannelOptions fins => new FinsTcpChannelOptions(fins.Id, fins.Host, fins.Port),
                _ => throw new NotSupportedException($"Channel options '{options.GetType().Name}' are not supported.")
            };
            snapshot.MaxConcurrency = options.MaxConcurrency;
            snapshot.ConnectTimeout = options.ConnectTimeout;
            snapshot.OperationTimeout = options.OperationTimeout;
            snapshot.Reconnect = new ReconnectOptions
            {
                InitialDelay = options.Reconnect.InitialDelay,
                MaxDelay = options.Reconnect.MaxDelay,
                MaxAttempts = options.Reconnect.MaxAttempts
            };
            return snapshot;
        }

        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ChannelManager)); }
    }
}

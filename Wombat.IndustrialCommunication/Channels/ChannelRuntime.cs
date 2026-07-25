using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Abstractions;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.Channels
{
    internal sealed class ChannelRuntime : IAsyncDisposable
    {
        private readonly ChannelOptions _options;
        private readonly IProtocolClient _client;
        private readonly SemaphoreSlim _operations;
        private readonly SemaphoreSlim _lifecycle = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _stopping = new CancellationTokenSource();
        private readonly object _snapshotLock = new object();
        private readonly ChannelSnapshot _snapshot;
        private Task _connectionTask;

        internal ChannelRuntime(ChannelOptions options, IProtocolClient client)
        {
            _options = options;
            _client = client;
            _operations = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
            _snapshot = new ChannelSnapshot(options.Id) { State = ChannelState.Created };
        }

        internal event EventHandler<ChannelStateChangedEventArgs> StateChanged;
        internal IProtocolClient Client => _client;

        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            await GetConnectionTaskAsync(false, cancellationToken).ConfigureAwait(false);
        }

        internal async ValueTask<TResult> ExecuteAsync<TResult>(Func<IProtocolClient, CancellationToken, ValueTask<TResult>> operation, CancellationToken cancellationToken)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            IncrementWaiting(1);
            var entered = false;
            using (var timeout = new CancellationTokenSource(_options.OperationTimeout))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token, timeout.Token))
            {
                try
                {
                    await _operations.WaitAsync(linked.Token).ConfigureAwait(false);
                    entered = true;
                    IncrementWaiting(-1);
                    IncrementActive(1);
                    await GetConnectionTaskAsync(true, linked.Token).ConfigureAwait(false);
                    var result = await operation(_client, linked.Token).ConfigureAwait(false);
                    await RecordResultAsync(result as OperationResult).ConfigureAwait(false);
                    return result;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_stopping.IsCancellationRequested && timeout.IsCancellationRequested)
                {
                    throw new ChannelException("Channel operation timed out.", entered ? OperationFailureKind.ReceiveTimeout : OperationFailureKind.QueueTimeout);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    if (IsTransportFailure(ex)) await MarkFaultedAsync(OperationFailureKind.TransportFailure).ConfigureAwait(false);
                    throw;
                }
                finally
                {
                    if (entered)
                    {
                        IncrementActive(-1);
                        _operations.Release();
                    }
                    else IncrementWaiting(-1);
                }
            }
        }

        internal ChannelSnapshot GetSnapshot()
        {
            lock (_snapshotLock)
            {
                return new ChannelSnapshot(_snapshot.ChannelId)
                {
                    State = _snapshot.State,
                    ConnectedAtUtc = _snapshot.ConnectedAtUtc,
                    LastOperationAtUtc = _snapshot.LastOperationAtUtc,
                    LastSuccessAtUtc = _snapshot.LastSuccessAtUtc,
                    LastFailureAtUtc = _snapshot.LastFailureAtUtc,
                    LastError = _snapshot.LastError,
                    ConsecutiveFailures = _snapshot.ConsecutiveFailures,
                    WaitingOperations = _snapshot.WaitingOperations,
                    ActiveOperations = _snapshot.ActiveOperations,
                    ReconnectCount = _snapshot.ReconnectCount
                };
            }
        }

        internal async Task StopAsync()
        {
            await _lifecycle.WaitAsync().ConfigureAwait(false);
            try
            {
                if (GetSnapshot().State == ChannelState.Stopped) return;
                SetState(ChannelState.Stopping);
                _stopping.Cancel();
            }
            finally { _lifecycle.Release(); }

            for (var i = 0; i < _options.MaxConcurrency; i++) await _operations.WaitAsync().ConfigureAwait(false);
            try { await _client.DisconnectAsync().ConfigureAwait(false); }
            finally
            {
                SetState(ChannelState.Stopped);
                for (var i = 0; i < _options.MaxConcurrency; i++) _operations.Release();
            }
        }

        private async Task GetConnectionTaskAsync(bool reconnect, CancellationToken cancellationToken)
        {
            Task task;
            await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var state = GetSnapshot().State;
                if (state == ChannelState.Stopping || state == ChannelState.Stopped) throw new ObjectDisposedException(_options.Id);
                if (state == ChannelState.Online && _client.Connected) return;
                if (state == ChannelState.Online) SetState(ChannelState.Faulted);
                if (_connectionTask == null || _connectionTask.IsCompleted)
                    _connectionTask = ConnectCoreAsync(reconnect || state == ChannelState.Faulted);
                task = _connectionTask;
            }
            finally { _lifecycle.Release(); }
            await WaitAsync(task, cancellationToken).ConfigureAwait(false);
        }

        private async Task ConnectCoreAsync(bool reconnect)
        {
            SetState(reconnect ? ChannelState.Reconnecting : ChannelState.Connecting);
            var attempts = reconnect ? Math.Max(1, _options.Reconnect.MaxAttempts) : 1;
            OperationResult last = null;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                if (attempt > 0)
                {
                    var delay = TimeSpan.FromMilliseconds(Math.Min(_options.Reconnect.MaxDelay.TotalMilliseconds,
                        _options.Reconnect.InitialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)));
                    await Task.Delay(delay, _stopping.Token).ConfigureAwait(false);
                }
                using (var timeout = new CancellationTokenSource(_options.ConnectTimeout))
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token, timeout.Token))
                {
                    try
                    {
                        last = await _client.ConnectAsync(linked.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (timeout.IsCancellationRequested && !_stopping.IsCancellationRequested)
                    {
                        last = new OperationResult { IsSuccess = false, FailureKind = OperationFailureKind.ConnectTimeout, Message = "Channel connection timed out." };
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { last = OperationResult.CreateFromException(ex); }
                }
                if (last.IsSuccess)
                {
                    _stopping.Token.ThrowIfCancellationRequested();
                    lock (_snapshotLock)
                    {
                        _snapshot.ConnectedAtUtc = DateTimeOffset.UtcNow;
                        if (reconnect) _snapshot.ReconnectCount++;
                    }
                    SetState(ChannelState.Online);
                    return;
                }
                if (last.FailureKind == OperationFailureKind.Cancelled && _stopping.IsCancellationRequested) throw new OperationCanceledException(_stopping.Token);
            }
            await MarkFaultedAsync(last?.FailureKind ?? OperationFailureKind.TransportFailure).ConfigureAwait(false);
            throw new ChannelException(last?.Message ?? "Channel connection failed.", last?.FailureKind ?? OperationFailureKind.TransportFailure, last?.Exception);
        }

        private async Task RecordResultAsync(OperationResult result)
        {
            var now = DateTimeOffset.UtcNow;
            lock (_snapshotLock)
            {
                _snapshot.LastOperationAtUtc = now;
                if (result == null || result.IsSuccess)
                {
                    _snapshot.LastSuccessAtUtc = now;
                    _snapshot.LastError = OperationFailureKind.None;
                    _snapshot.ConsecutiveFailures = 0;
                }
                else
                {
                    _snapshot.LastFailureAtUtc = now;
                    _snapshot.LastError = result.FailureKind;
                    _snapshot.ConsecutiveFailures++;
                }
            }
            if (result != null && IsTransportFailure(result.FailureKind)) await MarkFaultedAsync(result.FailureKind, false).ConfigureAwait(false);
        }

        private Task MarkFaultedAsync(OperationFailureKind failureKind) => MarkFaultedAsync(failureKind, true);

        private async Task MarkFaultedAsync(OperationFailureKind failureKind, bool recordFailure)
        {
            if (_stopping.IsCancellationRequested) return;
            if (recordFailure) lock (_snapshotLock)
            {
                _snapshot.LastFailureAtUtc = DateTimeOffset.UtcNow;
                _snapshot.LastError = failureKind;
                _snapshot.ConsecutiveFailures++;
            }
            SetState(ChannelState.Faulted);
            await _client.DisconnectAsync().ConfigureAwait(false);
        }

        private static bool IsTransportFailure(Exception ex) => ex is IOException || ex is SocketException || ex is ObjectDisposedException;
        private static bool IsTransportFailure(OperationFailureKind kind) => kind == OperationFailureKind.ConnectionClosed || kind == OperationFailureKind.Transport || kind == OperationFailureKind.TransportFailure || kind == OperationFailureKind.SendTimeout || kind == OperationFailureKind.ReceiveTimeout || kind == OperationFailureKind.OutcomeUnknown;

        private static async Task WaitAsync(Task task, CancellationToken cancellationToken)
        {
            if (task.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                await task.ConfigureAwait(false);
                return;
            }
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                if (task != await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false)) throw new OperationCanceledException(cancellationToken);
            }
            await task.ConfigureAwait(false);
        }

        private void SetState(ChannelState state)
        {
            ChannelState previous;
            lock (_snapshotLock)
            {
                previous = _snapshot.State;
                if (previous == state) return;
                if (previous == ChannelState.Stopped || (previous == ChannelState.Stopping && state != ChannelState.Stopped)) return;
                if (!IsAllowedTransition(previous, state)) throw new InvalidOperationException($"Invalid channel state transition: {previous} -> {state}.");
                _snapshot.State = state;
            }
            try { StateChanged?.Invoke(this, new ChannelStateChangedEventArgs(_options.Id, previous, state)); }
            catch { }
        }

        private void IncrementWaiting(int value) { lock (_snapshotLock) _snapshot.WaitingOperations += value; }
        private void IncrementActive(int value) { lock (_snapshotLock) _snapshot.ActiveOperations += value; }

        private static bool IsAllowedTransition(ChannelState previous, ChannelState current)
        {
            switch (previous)
            {
                case ChannelState.Created: return current == ChannelState.Connecting || current == ChannelState.Stopping;
                case ChannelState.Connecting: return current == ChannelState.Online || current == ChannelState.Faulted || current == ChannelState.Stopping;
                case ChannelState.Online: return current == ChannelState.Faulted || current == ChannelState.Stopping;
                case ChannelState.Faulted: return current == ChannelState.Reconnecting || current == ChannelState.Stopping;
                case ChannelState.Reconnecting: return current == ChannelState.Online || current == ChannelState.Faulted || current == ChannelState.Stopping;
                case ChannelState.Stopping: return current == ChannelState.Stopped;
                default: return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            (_client as IDisposable)?.Dispose();
            _operations.Dispose();
            _lifecycle.Dispose();
            _stopping.Dispose();
        }
    }
}

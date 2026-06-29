using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    internal sealed class ConnectionControlScheduler<TResource> : IDisposable
    {
        private readonly ConnectionPoolOptions _options;
        private readonly SemaphoreSlim _recoverGate;
        private readonly SemaphoreSlim _forceCloseGate;
        private readonly ConcurrentDictionary<ConnectionIdentity, byte> _recovering;
        private readonly CancellationTokenSource _shutdown;
        private readonly object _randomLock = new object();
        private readonly Random _random = new Random();

        public ConnectionControlScheduler(ConnectionPoolOptions options)
        {
            _options = options ?? new ConnectionPoolOptions();
            _recoverGate = CreateGate(_options.MaxConcurrentRecoveries);
            _forceCloseGate = CreateGate(_options.MaxConcurrentForceCloses);
            _recovering = new ConcurrentDictionary<ConnectionIdentity, byte>();
            _shutdown = new CancellationTokenSource();
        }

        public Task<OperationResult> RecoverAsync(PooledResourceEntry<TResource> entry, string reason, CancellationToken cancellationToken)
        {
            if (entry == null)
            {
                return Task.FromResult(OperationResult.CreateFailedResult("连接条目不能为空"));
            }

            return RunRecoverAsync(entry, reason, cancellationToken);
        }

        public void QueueRecover(PooledResourceEntry<TResource> entry, string reason)
        {
            if (entry == null || _shutdown.IsCancellationRequested)
            {
                return;
            }

            if (!_recovering.TryAdd(entry.Identity, 0))
            {
                return;
            }

            Task.Run((Func<Task>)(async () =>
            {
                try
                {
                    await RunRecoverAsync(entry, reason, _shutdown.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    byte ignored;
                    _recovering.TryRemove(entry.Identity, out ignored);
                }
            }));
        }

        public Task<OperationResult> ForceCloseAsync(PooledResourceEntry<TResource> entry, string reason, CancellationToken cancellationToken)
        {
            if (entry == null)
            {
                return Task.FromResult(OperationResult.CreateFailedResult("连接条目不能为空"));
            }

            return RunWithGateAsync(_forceCloseGate, token => entry.ForceCloseAsync(reason, token), cancellationToken);
        }

        public Task<OperationResult> ForceReconnectAsync(PooledResourceEntry<TResource> entry, string reason, CancellationToken cancellationToken)
        {
            if (entry == null)
            {
                return Task.FromResult(OperationResult.CreateFailedResult("连接条目不能为空"));
            }

            return RunWithGateAsync(_forceCloseGate, token => entry.ForceReconnectAsync(reason), cancellationToken);
        }

        public void Dispose()
        {
            _shutdown.Cancel();
            _shutdown.Dispose();
            _recoverGate.Dispose();
            _forceCloseGate.Dispose();
        }

        private async Task<OperationResult> RunRecoverAsync(PooledResourceEntry<TResource> entry, string reason, CancellationToken cancellationToken)
        {
            return await RunWithGateAsync(_recoverGate, async token =>
            {
                var maxRetry = _options.MaxRetryCount < 0 ? 0 : _options.MaxRetryCount;
                OperationResult last = null;
                for (var attempt = 0; attempt <= maxRetry; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    var delay = CalculateRecoveryDelay(entry, attempt);
                    if (delay > TimeSpan.Zero)
                    {
                        if (attempt > 0)
                        {
                            await entry.NotifyRetryingAsync(attempt, maxRetry, delay, ConnectionPoolMaintenanceMode.Background).ConfigureAwait(false);
                        }

                        await Task.Delay(delay, token).ConfigureAwait(false);
                    }

                    last = await entry.TryRecoverAsync(reason, ConnectionPoolMaintenanceMode.Background).ConfigureAwait(false);
                    if (last.IsSuccess)
                    {
                        return last;
                    }
                }

                return last ?? OperationResult.CreateFailedResult("连接恢复失败");
            }, cancellationToken).ConfigureAwait(false);
        }

        private async Task<OperationResult> RunWithGateAsync(SemaphoreSlim gate, Func<CancellationToken, Task<OperationResult>> action, CancellationToken cancellationToken)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token))
            {
                await gate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    return await action(linked.Token).ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            }
        }

        private TimeSpan CalculateRecoveryDelay(PooledResourceEntry<TResource> entry, int attempt)
        {
            var baseBackoff = _options.RetryBackoff > TimeSpan.Zero ? _options.RetryBackoff : TimeSpan.FromMilliseconds(200);
            var multiplier = 1 << Math.Min(attempt, 4);
            var backoffMs = baseBackoff.TotalMilliseconds * multiplier;
            var cooldownMs = _options.FaultedReconnectCooldown > TimeSpan.Zero ? _options.FaultedReconnectCooldown.TotalMilliseconds : 0;
            var delayMs = Math.Max(backoffMs, cooldownMs);
            lock (_randomLock)
            {
                delayMs += _random.Next(0, Math.Max(1, (int)baseBackoff.TotalMilliseconds));
            }

            return TimeSpan.FromMilliseconds(Math.Min(delayMs, 30000));
        }

        private static SemaphoreSlim CreateGate(int maxConcurrency)
        {
            var bounded = maxConcurrency <= 0 ? 1 : maxConcurrency;
            return new SemaphoreSlim(bounded, bounded);
        }
    }
}

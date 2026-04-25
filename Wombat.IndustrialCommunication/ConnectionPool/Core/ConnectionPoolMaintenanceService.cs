using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Events;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 连接池后台维护服务。
    /// </summary>
    internal sealed class ConnectionPoolMaintenanceService
    {
        private readonly ConnectionPoolOptions _options;
        private readonly ConnectionStateMonitor _monitor;
        private readonly Func<IList<PooledConnectionEntry>> _entryProvider;
        private readonly Func<int> _cleanupIdle;
        private readonly Action<ConnectionMaintenanceEventArgs> _publishMaintenance;

        public ConnectionPoolMaintenanceService(
            ConnectionPoolOptions options,
            ConnectionStateMonitor monitor,
            Func<IList<PooledConnectionEntry>> entryProvider,
            Func<int> cleanupIdle,
            Action<ConnectionMaintenanceEventArgs> publishMaintenance)
        {
            _options = options ?? new ConnectionPoolOptions();
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _entryProvider = entryProvider ?? throw new ArgumentNullException(nameof(entryProvider));
            _cleanupIdle = cleanupIdle ?? throw new ArgumentNullException(nameof(cleanupIdle));
            _publishMaintenance = publishMaintenance ?? throw new ArgumentNullException(nameof(publishMaintenance));
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var utcNow = DateTime.UtcNow;
                var scanned = 0;
                var affected = 0;
                var entries = _entryProvider();

                if (_monitor.ShouldSweepExpiredLeases(utcNow, _options))
                {
                    foreach (var entry in entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var expired = await entry.ExpireLeasesAsync(utcNow, ConnectionPoolMaintenanceMode.Background).ConfigureAwait(false);
                        if (expired.IsSuccess)
                        {
                            affected += expired.ResultValue;
                        }
                        scanned++;
                    }

                    _monitor.MarkLeaseSweep(utcNow);
                }

                if (_monitor.ShouldRunHealthCheck(utcNow, _options))
                {
                    var maintenanceResult = await RunHealthChecksAsync(entries, cancellationToken).ConfigureAwait(false);
                    scanned += maintenanceResult.ScannedCount;
                    affected += maintenanceResult.AffectedCount;

                    _monitor.MarkHealthCheck(utcNow);
                }

                affected += _cleanupIdle();
                _publishMaintenance(new ConnectionMaintenanceEventArgs
                {
                    EventType = ConnectionPoolEventType.BackgroundMaintenanceCompleted,
                    Message = "后台维护已完成",
                    State = ConnectionEntryState.Ready,
                    LifecycleState = ConnectionEntryLifecycleState.Ready,
                    TriggerMode = ConnectionPoolMaintenanceMode.Background,
                    ScannedEntryCount = scanned,
                    AffectedEntryCount = affected,
                    OccurredAtUtc = DateTime.UtcNow
                });

                await Task.Delay(_monitor.GetNextDelay(_options), cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<MaintenanceBatchResult> RunHealthChecksAsync(IList<PooledConnectionEntry> entries, CancellationToken cancellationToken)
        {
            var maxConcurrency = _options == null ? 1 : _options.MaxConcurrentMaintenanceOperations;
            if (maxConcurrency <= 1)
            {
                var sequentialResult = new MaintenanceBatchResult();
                for (var i = 0; i < entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var healthResult = await entries[i].RunHealthCheckAsync(_options, ConnectionPoolMaintenanceMode.Background).ConfigureAwait(false);
                    sequentialResult.ScannedCount++;
                    if (!healthResult.IsSuccess)
                    {
                        sequentialResult.AffectedCount++;
                    }
                }

                return sequentialResult;
            }

            var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = new List<Task<OperationResult>>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = entries[i];
                tasks.Add(RunHealthCheckWithGateAsync(entry, gate, cancellationToken));
            }

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            var batchResult = new MaintenanceBatchResult { ScannedCount = results.Length };
            for (var i = 0; i < results.Length; i++)
            {
                if (!results[i].IsSuccess)
                {
                    batchResult.AffectedCount++;
                }
            }

            return batchResult;
        }

        private async Task<OperationResult> RunHealthCheckWithGateAsync(PooledConnectionEntry entry, SemaphoreSlim gate, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await entry.RunHealthCheckAsync(_options, ConnectionPoolMaintenanceMode.Background).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private sealed class MaintenanceBatchResult
        {
            public int ScannedCount { get; set; }

            public int AffectedCount { get; set; }
        }
    }
}

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
                    foreach (var entry in entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var healthResult = await entry.RunHealthCheckAsync(_options, ConnectionPoolMaintenanceMode.Background).ConfigureAwait(false);
                        if (!healthResult.IsSuccess)
                        {
                            affected++;
                        }
                        scanned++;
                    }

                    _monitor.MarkHealthCheck(utcNow);
                }

                affected += _cleanupIdle();
                _publishMaintenance(new ConnectionMaintenanceEventArgs
                {
                    EventType = ConnectionPoolEventType.BackgroundMaintenanceCompleted,
                    Message = "后台维护已完成",
                    State = ConnectionEntryState.Ready,
                    TriggerMode = ConnectionPoolMaintenanceMode.Background,
                    ScannedEntryCount = scanned,
                    AffectedEntryCount = affected,
                    OccurredAtUtc = DateTime.UtcNow
                });

                await Task.Delay(_monitor.GetNextDelay(_options), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

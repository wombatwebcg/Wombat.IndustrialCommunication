using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Events;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ConnectionPoolTests
{
    public class ConnectionPoolMaintenanceTests
    {
        [Fact]
        public async Task Should_Run_Background_Maintenance_And_Publish_Event()
        {
            var identity = new ConnectionIdentity { DeviceId = "bg", ProtocolType = "Mock", Endpoint = "bg" };
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = true,
                HealthCheckInterval = TimeSpan.FromMilliseconds(30),
                LeaseExpirationSweepInterval = TimeSpan.FromMilliseconds(30),
                IdleTimeout = TimeSpan.FromSeconds(5)
            };

            var pool = new DeviceClientPool(options, new MaintenanceConnectionFactory());
            var maintenanceRaised = false;
            pool.MaintenanceCompleted += (sender, args) =>
            {
                if (args.EventType == ConnectionPoolEventType.BackgroundMaintenanceCompleted)
                {
                    maintenanceRaised = true;
                }
            };

            pool.Register(new ResourceDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });
            await pool.AcquireAsync(identity);
            for (var i = 0; i < 20 && !maintenanceRaised; i++)
            {
                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.True(maintenanceRaised);
        }

        [Fact]
        public async Task Should_Unregister_Safely_When_Background_Maintenance_Is_Enabled()
        {
            var identity = new ConnectionIdentity { DeviceId = "bg-remove", ProtocolType = "Mock", Endpoint = "bg-remove" };
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = true,
                HealthCheckInterval = TimeSpan.FromMilliseconds(20),
                LeaseExpirationSweepInterval = TimeSpan.FromMilliseconds(20),
                IdleTimeout = TimeSpan.FromSeconds(5)
            };

            var pool = new DeviceClientPool(options, new MaintenanceConnectionFactory());
            pool.Register(new ResourceDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

            await Task.Delay(80).ConfigureAwait(false);
            var unregister = pool.Unregister(identity, "后台维护下注销");
            await Task.Delay(80).ConfigureAwait(false);

            Assert.True(unregister.IsSuccess);
            Assert.False(pool.GetState(identity).IsSuccess);
        }

        [Fact]
        public async Task Should_Prefer_Probe_For_Healthy_Connected_Entries()
        {
            var identity = new ConnectionIdentity { DeviceId = "probe-first", ProtocolType = "Mock", Endpoint = "probe-first" };
            var factory = new MaintenanceConnectionFactory(descriptor => new MaintenanceConnection(descriptor.Identity)
            {
                ProbeDelay = TimeSpan.FromMilliseconds(10)
            });
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = true,
                HealthCheckInterval = TimeSpan.FromMilliseconds(20),
                LeaseExpirationSweepInterval = TimeSpan.FromMilliseconds(20),
                IdleTimeout = TimeSpan.FromSeconds(5)
            };
            var pool = new DeviceClientPool(options, factory);
            pool.Register(new ResourceDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

            var lease = await pool.AcquireAsync(identity).ConfigureAwait(false);
            Assert.True(lease.IsSuccess);
            Assert.True(pool.Release(lease.ResultValue).IsSuccess);

            var connection = factory.Get(identity);
            await WaitUntilAsync(() => Volatile.Read(ref connection.ProbeCount) > 0).ConfigureAwait(false);

            Assert.True(connection.ProbeCount > 0);
            Assert.Equal(1, connection.EnsureAvailableCount);
        }

        [Fact]
        public async Task Should_Apply_Recovery_Cooldown_Before_Retrying_Again()
        {
            var identity = new ConnectionIdentity { DeviceId = "cooldown", ProtocolType = "Mock", Endpoint = "cooldown" };
            var factory = new MaintenanceConnectionFactory(descriptor => new MaintenanceConnection(descriptor.Identity)
            {
                ProbeShouldFail = true,
                RemainingEnsureSuccessCount = 1
            });
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = true,
                HealthCheckInterval = TimeSpan.FromMilliseconds(20),
                LeaseExpirationSweepInterval = TimeSpan.FromMilliseconds(20),
                IdleTimeout = TimeSpan.FromSeconds(5),
                FaultedReconnectCooldown = TimeSpan.FromMilliseconds(200),
                MaxConsecutiveHealthCheckFailures = 10
            };
            var pool = new DeviceClientPool(options, factory);
            pool.Register(new ResourceDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

            var lease = await pool.AcquireAsync(identity).ConfigureAwait(false);
            Assert.True(lease.IsSuccess);
            Assert.True(pool.Release(lease.ResultValue).IsSuccess);

            var connection = factory.Get(identity);
            await WaitUntilAsync(() => Volatile.Read(ref connection.EnsureAvailableCount) >= 2).ConfigureAwait(false);
            var countAfterFirstRecovery = connection.EnsureAvailableCount;

            await Task.Delay(100).ConfigureAwait(false);

            Assert.Equal(countAfterFirstRecovery, connection.EnsureAvailableCount);
        }

        [Fact]
        public async Task Should_Limit_Maintenance_Probe_Concurrency()
        {
            var identities = Enumerable.Range(0, 6)
                .Select(i => new ConnectionIdentity { DeviceId = "concurrency-" + i, ProtocolType = "Mock", Endpoint = "concurrency-" + i })
                .ToArray();
            var tracker = new ProbeConcurrencyTracker();
            var factory = new MaintenanceConnectionFactory(descriptor => new MaintenanceConnection(descriptor.Identity, tracker)
            {
                ProbeDelay = TimeSpan.FromMilliseconds(80)
            });
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = true,
                HealthCheckInterval = TimeSpan.FromMilliseconds(20),
                LeaseExpirationSweepInterval = TimeSpan.FromMilliseconds(20),
                IdleTimeout = TimeSpan.FromSeconds(5),
                MaxConcurrentMaintenanceOperations = 2
            };
            var pool = new DeviceClientPool(options, factory);
            var maintenanceCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.MaintenanceCompleted += (sender, args) =>
            {
                if (args.EventType == ConnectionPoolEventType.BackgroundMaintenanceCompleted)
                {
                    maintenanceCompleted.TrySetResult(true);
                }
            };

            for (var i = 0; i < identities.Length; i++)
            {
                pool.Register(new ResourceDescriptor { Identity = identities[i], DeviceConnectionType = DeviceConnectionType.ModbusTcp });
                var lease = await pool.AcquireAsync(identities[i]).ConfigureAwait(false);
                Assert.True(lease.IsSuccess);
                Assert.True(pool.Release(lease.ResultValue).IsSuccess);
            }

            await maintenanceCompleted.Task.ConfigureAwait(false);
            await WaitUntilAsync(() => tracker.MaxObservedConcurrency >= 2).ConfigureAwait(false);

            Assert.True(tracker.MaxObservedConcurrency <= 2);
        }

        [Fact]
        public async Task Should_Disconnect_When_Invalidated_By_Health_Check_Failures()
        {
            var identity = new ConnectionIdentity { DeviceId = "invalidate-disconnect", ProtocolType = "Mock", Endpoint = "invalidate-disconnect" };
            var factory = new MaintenanceConnectionFactory(descriptor => new MaintenanceConnection(descriptor.Identity)
            {
                ProbeShouldFail = true,
                RemainingEnsureSuccessCount = 1
            });
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = true,
                HealthCheckInterval = TimeSpan.FromMilliseconds(20),
                LeaseExpirationSweepInterval = TimeSpan.FromMilliseconds(20),
                IdleTimeout = TimeSpan.FromSeconds(5),
                MaxConsecutiveHealthCheckFailures = 1
            };
            var pool = new DeviceClientPool(options, factory);
            pool.Register(new ResourceDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

            var lease = await pool.AcquireAsync(identity).ConfigureAwait(false);
            Assert.True(lease.IsSuccess);
            Assert.True(pool.Release(lease.ResultValue).IsSuccess);

            var connection = factory.Get(identity);
            await WaitUntilAsync(() => Volatile.Read(ref connection.InvalidateCount) > 0).ConfigureAwait(false);

            Assert.True(connection.DisconnectCount > 0);
        }

        private sealed class MaintenanceConnectionFactory : IPooledResourceConnectionFactory<IDeviceClient>
        {
            private readonly Func<ResourceDescriptor, MaintenanceConnection> _connectionFactory;
            private readonly ConcurrentDictionary<ConnectionIdentity, MaintenanceConnection> _connections = new ConcurrentDictionary<ConnectionIdentity, MaintenanceConnection>();

            public MaintenanceConnectionFactory()
                : this(descriptor => new MaintenanceConnection(descriptor.Identity))
            {
            }

            public MaintenanceConnectionFactory(Func<ResourceDescriptor, MaintenanceConnection> connectionFactory)
            {
                _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            }

            public OperationResult<IPooledResourceConnection<IDeviceClient>> Create(ResourceDescriptor descriptor)
            {
                var connection = _connectionFactory(descriptor);
                _connections[descriptor.Identity] = connection;
                return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceClient>>(connection);
            }

            public Task<OperationResult<IPooledResourceConnection<IDeviceClient>>> CreateAsync(ResourceDescriptor descriptor)
            {
                return Task.FromResult(Create(descriptor));
            }

            public MaintenanceConnection Get(ConnectionIdentity identity)
            {
                MaintenanceConnection connection;
                return _connections.TryGetValue(identity, out connection) ? connection : null;
            }
        }

        private sealed class MaintenanceConnection : IPooledResourceConnection<IDeviceClient>
        {
            private readonly ProbeConcurrencyTracker _tracker;

            public MaintenanceConnection(ConnectionIdentity identity, ProbeConcurrencyTracker tracker = null)
            {
                Identity = identity;
                State = ConnectionEntryLifecycleState.Uninitialized;
                LastActiveTimeUtc = DateTime.UtcNow;
                _tracker = tracker;
            }

            public ConnectionIdentity Identity { get; private set; }

            public ConnectionEntryLifecycleState State { get; private set; }

            public DateTime LastActiveTimeUtc { get; private set; }

            public bool IsAvailable => State == ConnectionEntryLifecycleState.Ready || State == ConnectionEntryLifecycleState.Leased;

            public IDeviceClient Resource => null;

            public TimeSpan ProbeDelay { get; set; }

            public bool ProbeShouldFail { get; set; }

            public int RemainingEnsureSuccessCount { get; set; } = int.MaxValue;

            public int EnsureAvailableCount;

            public int ProbeCount;

            public int DisconnectCount;

            public int InvalidateCount;

            public OperationResult EnsureAvailable()
            {
                EnsureAvailableCount++;
                if (RemainingEnsureSuccessCount <= 0)
                {
                    State = ConnectionEntryLifecycleState.Faulted;
                    LastActiveTimeUtc = DateTime.UtcNow;
                    return OperationResult.CreateFailedResult("模拟建连失败");
                }

                RemainingEnsureSuccessCount--;
                State = ConnectionEntryLifecycleState.Ready;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateSuccessResult();
            }

            public Task<OperationResult> EnsureAvailableAsync()
            {
                return Task.FromResult(EnsureAvailable());
            }

            public async Task<OperationResult> ProbeAsync(TimeSpan timeout)
            {
                ProbeCount++;
                _tracker?.Enter();
                try
                {
                    if (ProbeDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(ProbeDelay).ConfigureAwait(false);
                    }

                    LastActiveTimeUtc = DateTime.UtcNow;
                    if (ProbeShouldFail)
                    {
                        State = ConnectionEntryLifecycleState.Faulted;
                        return OperationResult.CreateFailedResult("模拟探活失败");
                    }

                    State = ConnectionEntryLifecycleState.Ready;
                    return OperationResult.CreateSuccessResult();
                }
                finally
                {
                    _tracker?.Exit();
                }
            }

            public OperationResult Invalidate(string reason)
            {
                InvalidateCount++;
                DisconnectCount++;
                State = ConnectionEntryLifecycleState.Invalidated;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateFailedResult(reason);
            }

            public OperationResult DisconnectOrShutdown()
            {
                DisconnectCount++;
                State = ConnectionEntryLifecycleState.Disposed;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateSuccessResult();
            }

            public async Task<OperationResult<T>> ExecuteAsync<T>(Func<IDeviceClient, Task<OperationResult<T>>> action)
            {
                LastActiveTimeUtc = DateTime.UtcNow;
                return await action(null).ConfigureAwait(false);
            }

            public async Task<OperationResult> ExecuteAsync(Func<IDeviceClient, Task<OperationResult>> action)
            {
                LastActiveTimeUtc = DateTime.UtcNow;
                return await action(null).ConfigureAwait(false);
            }
        }

        private sealed class ProbeConcurrencyTracker
        {
            private int _currentConcurrency;
            private int _maxObservedConcurrency;

            public int MaxObservedConcurrency => _maxObservedConcurrency;

            public void Enter()
            {
                var current = Interlocked.Increment(ref _currentConcurrency);
                while (true)
                {
                    var observed = _maxObservedConcurrency;
                    if (current <= observed)
                    {
                        return;
                    }

                    if (Interlocked.CompareExchange(ref _maxObservedConcurrency, current, observed) == observed)
                    {
                        return;
                    }
                }
            }

            public void Exit()
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.True(condition(), "等待条件成立超时");
        }
    }
}



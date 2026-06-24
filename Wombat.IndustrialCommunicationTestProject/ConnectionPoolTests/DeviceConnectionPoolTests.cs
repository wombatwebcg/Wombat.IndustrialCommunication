using System;
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
    public class DeviceClientPoolTests
    {
        [Fact]
        public async Task Should_Reuse_Connection_For_Same_Identity()
        {
            var options = new ConnectionPoolOptions { LeaseTimeout = TimeSpan.FromSeconds(5) };
            var pool = new DeviceClientPool(options, new FakePooledConnectionFactory());

            var identity = new ConnectionIdentity { DeviceId = "dev1", ProtocolType = "ModbusTcp", Endpoint = "127.0.0.1:502" };
            var descriptor = ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity);

            Assert.True(pool.Register(descriptor).IsSuccess);
            var lease1 = await pool.AcquireAsync(identity);
            var lease2 = await pool.AcquireAsync(identity);

            Assert.True(lease1.IsSuccess);
            Assert.True(lease2.IsSuccess);
            Assert.Equal(identity, lease1.ResultValue.Identity);
            Assert.Equal(identity, lease2.ResultValue.Identity);

            Assert.True(pool.Release(lease1.ResultValue).IsSuccess);
            Assert.True(pool.Release(lease2.ResultValue).IsSuccess);
        }

        [Fact]
        public void Should_Reject_Duplicate_Register()
        {
            var identity = new ConnectionIdentity { DeviceId = "dup", ProtocolType = "Mock", Endpoint = "dup" };
            var descriptor = ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity);
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());

            var first = pool.Register(descriptor);
            var second = pool.Register(descriptor);

            Assert.True(first.IsSuccess);
            Assert.False(second.IsSuccess);
        }

        [Fact]
        public async Task Should_Register_Atomically_Under_Concurrent_Duplicate_Register()
        {
            var identity = new ConnectionIdentity { DeviceId = "dup-concurrent", ProtocolType = "Mock", Endpoint = "dup-concurrent" };
            var descriptor = ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity);
            var factory = new CountingPooledConnectionFactory();
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, factory);

            var tasks = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => pool.Register(descriptor)))
                .ToArray();

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            Assert.Equal(1, results.Count(t => t.IsSuccess));
            Assert.Equal(1, factory.CreateCount);
        }

        [Fact]
        public async Task Should_Allow_Parallel_Execution_For_Same_And_Different_Devices()
        {
            var options = new ConnectionPoolOptions { LeaseTimeout = TimeSpan.FromSeconds(5), MaxRetryCount = 0 };
            var pool = new DeviceClientPool(options, new FakePooledConnectionFactory());

            var idA = new ConnectionIdentity { DeviceId = "A", ProtocolType = "Mock", Endpoint = "A" };
            var idB = new ConnectionIdentity { DeviceId = "B", ProtocolType = "Mock", Endpoint = "B" };
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(idA));
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(idB));

            var releaseExecutions = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var allStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var startedCount = 0;
            var sameDeviceConcurrency = 0;
            var sameDeviceMaxConcurrency = 0;
            var differentDeviceStarted = 0;

            Func<ConnectionIdentity, Task<OperationResult>> execute = async currentIdentity =>
            {
                var isSameDevice = currentIdentity.Equals(idA);
                if (isSameDevice)
                {
                    var currentConcurrency = Interlocked.Increment(ref sameDeviceConcurrency);
                    TryUpdateMaxConcurrency(ref sameDeviceMaxConcurrency, currentConcurrency);
                }
                else
                {
                    Interlocked.Increment(ref differentDeviceStarted);
                }

                if (Interlocked.Increment(ref startedCount) == 3)
                {
                    allStarted.TrySetResult(true);
                }

                await releaseExecutions.Task.ConfigureAwait(false);

                if (isSameDevice)
                {
                    Interlocked.Decrement(ref sameDeviceConcurrency);
                }

                return OperationResult.CreateSuccessResult();
            };

            var sameA1 = pool.ExecuteAsync(idA, _ => execute(idA));
            var sameA2 = pool.ExecuteAsync(idA, _ => execute(idA));
            var differentB = pool.ExecuteAsync(idB, _ => execute(idB));

            var started = await Task.WhenAny(allStarted.Task, Task.Delay(200)).ConfigureAwait(false);
            releaseExecutions.TrySetResult(true);
            var results = await Task.WhenAll(sameA1, sameA2, differentB).ConfigureAwait(false);

            Assert.All(results, r => Assert.True(r.IsSuccess));
            Assert.Same(allStarted.Task, started);
            Assert.True(sameDeviceMaxConcurrency >= 2, "同设备连接在当前实现下应允许并发执行");
            Assert.True(differentDeviceStarted >= 1, "不同设备执行不应被同设备连接阻塞");
        }

        [Fact]
        public void Should_Cleanup_Idle_Entries()
        {
            var options = new ConnectionPoolOptions
            {
                LeaseTimeout = TimeSpan.FromSeconds(1),
                IdleTimeout = TimeSpan.FromMilliseconds(1),
                EnableBackgroundMaintenance = false
            };
            var pool = new DeviceClientPool(options, new FakePooledConnectionFactory());

            var identity = new ConnectionIdentity { DeviceId = "cleanup", ProtocolType = "Mock", Endpoint = "cleanup" };
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            var lease = pool.Acquire(identity);
            Assert.True(lease.IsSuccess);
            Assert.True(pool.Release(lease.ResultValue).IsSuccess);

            Task.Delay(20).GetAwaiter().GetResult();
            var cleaned = pool.CleanupIdle();
            Assert.True(cleaned.IsSuccess);
            Assert.True(cleaned.ResultValue >= 1);
        }

        [Fact]
        public void Should_Unregister_Entry_When_No_Active_Lease()
        {
            var identity = new ConnectionIdentity { DeviceId = "unregister", ProtocolType = "Mock", Endpoint = "unregister" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            var result = pool.Unregister(identity, "测试注销");
            var snapshot = pool.GetState(identity);

            Assert.True(result.IsSuccess);
            Assert.False(snapshot.IsSuccess);
        }

        [Fact]
        public void Should_Reject_Unregister_When_Lease_Is_Active()
        {
            var identity = new ConnectionIdentity { DeviceId = "unregister-active", ProtocolType = "Mock", Endpoint = "unregister-active" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            var lease = pool.Acquire(identity);
            Assert.True(lease.IsSuccess);

            var result = pool.Unregister(identity, "测试注销");

            Assert.False(result.IsSuccess);
            Assert.True(pool.Release(lease.ResultValue).IsSuccess);
        }

        [Fact]
        public void Should_Return_Detailed_Snapshot()
        {
            var identity = new ConnectionIdentity { DeviceId = "snapshot", ProtocolType = "Mock", Endpoint = "snapshot" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            var snapshot = pool.GetState(identity);

            Assert.True(snapshot.IsSuccess);
            Assert.Equal(identity, snapshot.ResultValue.Identity);
            Assert.Equal(ConnectionEntryState.Disconnected, snapshot.ResultValue.State);
            Assert.Equal(ConnectionEntryLifecycleState.Uninitialized, snapshot.ResultValue.LifecycleState);
        }

        [Fact]
        public async Task Should_Project_Pool_Snapshot_To_Four_Public_States()
        {
            var disconnected = new ConnectionIdentity { DeviceId = "pool-disconnected", ProtocolType = "Mock", Endpoint = "pool-disconnected" };
            var ready = new ConnectionIdentity { DeviceId = "pool-ready", ProtocolType = "Mock", Endpoint = "pool-ready" };
            var busy = new ConnectionIdentity { DeviceId = "pool-busy", ProtocolType = "Mock", Endpoint = "pool-busy" };
            var unavailable = new ConnectionIdentity { DeviceId = "pool-unavailable", ProtocolType = "Mock", Endpoint = "pool-unavailable" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());

            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(disconnected));
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(ready));
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(busy));
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(unavailable));

            var readyLease = await pool.AcquireAsync(ready).ConfigureAwait(false);
            Assert.True(readyLease.IsSuccess);
            Assert.True(pool.Release(readyLease.ResultValue).IsSuccess);

            var busyLease = await pool.AcquireAsync(busy).ConfigureAwait(false);
            Assert.True(busyLease.IsSuccess);

            var invalidated = pool.Invalidate(unavailable, "模拟失效");
            Assert.False(invalidated.IsSuccess);

            var snapshot = pool.GetPoolSnapshot();

            Assert.True(snapshot.IsSuccess);
            Assert.Equal(4, snapshot.ResultValue.TotalEntries);
            Assert.Equal(1, snapshot.ResultValue.DisconnectedEntries);
            Assert.Equal(1, snapshot.ResultValue.ReadyEntries);
            Assert.Equal(1, snapshot.ResultValue.BusyEntries);
            Assert.Equal(1, snapshot.ResultValue.UnavailableEntries);
            Assert.True(pool.Release(busyLease.ResultValue).IsSuccess);
        }

        [Fact]
        public async Task Should_Publish_Lifecycle_Events()
        {
            var identity = new ConnectionIdentity { DeviceId = "evented", ProtocolType = "Mock", Endpoint = "evented" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            var events = new List<ConnectionPoolEventType>();
            pool.PoolEventOccurred += (sender, args) => events.Add(args.EventType);

            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var lease = await pool.AcquireAsync(identity);
            Assert.True(lease.IsSuccess);
            Assert.True(pool.Release(lease.ResultValue).IsSuccess);

            Assert.Contains(ConnectionPoolEventType.Registered, events);
            Assert.Contains(ConnectionPoolEventType.ConnectStarting, events);
            Assert.Contains(ConnectionPoolEventType.ConnectSucceeded, events);
            Assert.Contains(ConnectionPoolEventType.LeaseAcquired, events);
            Assert.Contains(ConnectionPoolEventType.LeaseReleased, events);
        }

        [Fact]
        public void Should_Cleanup_Expired_Leases()
        {
            var identity = new ConnectionIdentity { DeviceId = "expire", ProtocolType = "Mock", Endpoint = "expire" };
            var options = new ConnectionPoolOptions
            {
                LeaseTimeout = TimeSpan.FromMilliseconds(5),
                EnableBackgroundMaintenance = false
            };
            var pool = new DeviceClientPool(options, new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            var lease = pool.Acquire(identity);
            Assert.True(lease.IsSuccess);
            Task.Delay(20).GetAwaiter().GetResult();

            var cleanup = pool.CleanupExpiredLeases();
            var snapshot = pool.GetState(identity);

            Assert.True(cleanup.IsSuccess);
            Assert.True(cleanup.ResultValue >= 1);
            Assert.True(snapshot.IsSuccess);
            Assert.Equal(ConnectionEntryState.Ready, snapshot.ResultValue.State);
            Assert.Equal(ConnectionEntryLifecycleState.Ready, snapshot.ResultValue.LifecycleState);
            Assert.Equal(0, snapshot.ResultValue.ActiveLeaseCount);
        }

        [Fact]
        public void Should_Force_Reconnect_And_Expose_Pool_Snapshot()
        {
            var identity = new ConnectionIdentity { DeviceId = "reconnect", ProtocolType = "Mock", Endpoint = "reconnect" };
            var factory = new FakePooledConnectionFactory();
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, factory);
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            var force = pool.ForceReconnect(identity, "测试重连");
            var snapshot = pool.GetPoolSnapshot();

            Assert.True(force.IsSuccess);
            Assert.True(snapshot.IsSuccess);
            Assert.True(snapshot.ResultValue.TotalEntries >= 1);
            Assert.True(snapshot.ResultValue.Entries.Any(t => t.Identity.Equals(identity)));
        }

        [Fact]
        public async Task Should_Reject_Force_Reconnect_When_Lease_Is_Active()
        {
            var identity = new ConnectionIdentity { DeviceId = "reconnect-active", ProtocolType = "Mock", Endpoint = "reconnect-active" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            var lease = await pool.AcquireAsync(identity).ConfigureAwait(false);
            Assert.True(lease.IsSuccess);

            var force = pool.ForceReconnect(identity, "测试重连");

            Assert.False(force.IsSuccess);
            Assert.True(pool.Release(lease.ResultValue).IsSuccess);
        }

        [Fact]
        public async Task Should_Allow_Release_After_Pool_Disposed()
        {
            var identity = new ConnectionIdentity { DeviceId = "dispose-release", ProtocolType = "Mock", Endpoint = "dispose-release" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            var lease = await pool.AcquireAsync(identity).ConfigureAwait(false);
            Assert.True(lease.IsSuccess);

            pool.Dispose();
            var release = pool.Release(lease.ResultValue);

            Assert.True(release.IsSuccess);
        }

        [Fact]
        public void Should_Expose_Segregated_Interfaces()
        {
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());

            Assert.IsAssignableFrom<IResourcePoolQuery>(pool);
            Assert.IsAssignableFrom<IResourcePoolControl>(pool);
            Assert.IsAssignableFrom<IResourcePoolExecution<IDeviceClient>>(pool);
            Assert.IsAssignableFrom<IResourcePoolEvents>(pool);
            Assert.IsAssignableFrom<IDeviceClientPool>(pool);
        }

        [Fact]
        public async Task Should_Not_Retry_Diagnostic_Execution_By_Default()
        {
            var identity = new ConnectionIdentity { DeviceId = "diag-default", ProtocolType = "Mock", Endpoint = "diag-default" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false, MaxRetryCount = 3 }, new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var executeCount = 0;

            var result = await pool.ExecuteAsync<int>(identity, client =>
            {
                executeCount++;
                return Task.FromResult(OperationResult.CreateFailedResult<int>(new TimeoutException("diagnostic timeout")));
            }).ConfigureAwait(false);

            Assert.False(result.IsSuccess);
            Assert.Equal(1, executeCount);
            Assert.Contains(result.OperationInfo, t => t.Contains("Diagnostic"));
        }

        [Fact]
        public async Task Should_Retry_Read_Execution_By_Default()
        {
            var identity = new ConnectionIdentity { DeviceId = "read-default", ProtocolType = "Mock", Endpoint = "read-default" };
            var pool = new DeviceClientPool(
                new ConnectionPoolOptions
                {
                    EnableBackgroundMaintenance = false,
                    MaxRetryCount = 2,
                    RetryBackoff = TimeSpan.FromMilliseconds(1)
                },
                new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var executeCount = 0;

            var result = await pool.ExecuteAsync<int>(identity, client =>
            {
                executeCount++;
                if (executeCount == 1)
                {
                    return Task.FromResult(OperationResult.CreateFailedResult<int>(new TimeoutException("read timeout")));
                }

                return Task.FromResult(OperationResult.CreateSuccessResult(42));
            }, ConnectionExecutionOptions.CreateRead()).ConfigureAwait(false);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, executeCount);
            Assert.Equal(42, result.ResultValue);
            Assert.Contains(result.OperationInfo, t => t.Contains("Read") && t.Contains("重试"));
        }

        [Fact]
        public async Task Should_Not_Retry_Write_Execution_By_Default()
        {
            var identity = new ConnectionIdentity { DeviceId = "write-default", ProtocolType = "Mock", Endpoint = "write-default" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false, MaxRetryCount = 3 }, new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var executeCount = 0;

            var result = await pool.ExecuteAsync<int>(identity, client =>
            {
                executeCount++;
                return Task.FromResult(OperationResult.CreateFailedResult<int>(new TimeoutException("write timeout")));
            }, ConnectionExecutionOptions.CreateWrite()).ConfigureAwait(false);

            Assert.False(result.IsSuccess);
            Assert.Equal(1, executeCount);
            Assert.Contains(result.OperationInfo, t => t.Contains("Write"));
        }

        [Fact]
        public async Task Should_Allow_Write_Execution_To_Override_Retry_Policy()
        {
            var identity = new ConnectionIdentity { DeviceId = "write-override", ProtocolType = "Mock", Endpoint = "write-override" };
            var pool = new DeviceClientPool(
                new ConnectionPoolOptions
                {
                    EnableBackgroundMaintenance = false,
                    MaxRetryCount = 0,
                    RetryBackoff = TimeSpan.FromMilliseconds(1)
                },
                new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var executeCount = 0;

            var options = ConnectionExecutionOptions.CreateWrite();
            options.EnableRetry = true;
            options.MaxRetryCount = 1;
            options.RetryBackoff = TimeSpan.FromMilliseconds(1);

            var result = await pool.ExecuteAsync<int>(identity, client =>
            {
                executeCount++;
                if (executeCount == 1)
                {
                    return Task.FromResult(OperationResult.CreateFailedResult<int>(new TimeoutException("write timeout")));
                }

                return Task.FromResult(OperationResult.CreateSuccessResult(7));
            }, options).ConfigureAwait(false);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, executeCount);
            Assert.Equal(7, result.ResultValue);
        }

        [Fact]
        public async Task Should_Force_Close_Entry_Idempotently_And_Release_Leases()
        {
            var identity = new ConnectionIdentity { DeviceId = "force-close", ProtocolType = "Mock", Endpoint = "force-close" };
            var factory = new InspectablePooledConnectionFactory();
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, factory);
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            var lease = await pool.AcquireAsync(identity).ConfigureAwait(false);
            Assert.True(lease.IsSuccess);

            var firstForceClose = await pool.ForceCloseAsync(identity, "测试强制关闭").ConfigureAwait(false);
            var secondForceClose = await pool.ForceCloseAsync(identity, "重复强制关闭").ConfigureAwait(false);
            var snapshot = pool.GetState(identity);
            var acquire = await pool.AcquireAsync(identity).ConfigureAwait(false);
            var release = pool.Release(lease.ResultValue);

            Assert.True(firstForceClose.IsSuccess);
            Assert.True(secondForceClose.IsSuccess);
            Assert.NotNull(factory.LastConnection);
            Assert.True(factory.LastConnection.DisconnectCount >= 1);
            Assert.True(snapshot.IsSuccess);
            Assert.Equal(ConnectionEntryState.Unavailable, snapshot.ResultValue.State);
            Assert.Equal(ConnectionEntryLifecycleState.Faulted, snapshot.ResultValue.LifecycleState);
            Assert.Equal(0, snapshot.ResultValue.ActiveLeaseCount);
            Assert.True(acquire.IsSuccess);
            Assert.True(pool.Release(acquire.ResultValue).IsSuccess);
            Assert.False(release.IsSuccess);
        }

        [Fact]
        public async Task Should_Stop_Retrying_When_Force_Close_Is_Requested()
        {
            var identity = new ConnectionIdentity { DeviceId = "force-close-retry", ProtocolType = "Mock", Endpoint = "force-close-retry" };
            var descriptor = ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity);
            var connection = new TestObjectConnection(identity);
            var entry = new PooledResourceEntry<object>(descriptor, connection, new NullEventPublisher());
            var executor = new PooledResourceExecutor<object>();
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = false,
                MaxRetryCount = 3,
                RetryBackoff = TimeSpan.FromSeconds(5)
            };
            var executionOptions = ConnectionExecutionOptions.CreateRead();
            var firstAttemptStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var executeCount = 0;

            var executeTask = executor.ExecuteAsync<int>(entry, (resource, cancellationToken) =>
            {
                Interlocked.Increment(ref executeCount);
                firstAttemptStarted.TrySetResult(true);
                return Task.FromResult(OperationResult.CreateFailedResult<int>(new TimeoutException("read timeout")));
            }, options, executionOptions);

            await firstAttemptStarted.Task.ConfigureAwait(false);
            var forceClose = await entry.ForceCloseAsync("测试强制关闭", CancellationToken.None).ConfigureAwait(false);
            var executeResult = await executeTask.ConfigureAwait(false);

            Assert.True(forceClose.IsSuccess);
            Assert.False(executeResult.IsSuccess);
            Assert.True(executeResult.IsCancelled);
            Assert.Equal(1, executeCount);
            Assert.Equal(ConnectionEntryLifecycleState.Faulted, entry.LifecycleState);
            Assert.True(connection.DisconnectCount >= 1);
            Assert.Contains("强制关闭", executeResult.Message);
        }

        private sealed class FakePooledConnectionFactory : IPooledResourceConnectionFactory<IDeviceClient>
        {
            public OperationResult<IPooledResourceConnection<IDeviceClient>> Create(ResourceDescriptor descriptor)
            {
                return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceClient>>(new FakePooledConnection(descriptor.Identity));
            }

            public Task<OperationResult<IPooledResourceConnection<IDeviceClient>>> CreateAsync(ResourceDescriptor descriptor)
            {
                return Task.FromResult(Create(descriptor));
            }
        }

        private sealed class InspectablePooledConnectionFactory : IPooledResourceConnectionFactory<IDeviceClient>
        {
            public InspectablePooledConnection LastConnection { get; private set; }

            public OperationResult<IPooledResourceConnection<IDeviceClient>> Create(ResourceDescriptor descriptor)
            {
                LastConnection = new InspectablePooledConnection(descriptor.Identity);
                return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceClient>>(LastConnection);
            }

            public Task<OperationResult<IPooledResourceConnection<IDeviceClient>>> CreateAsync(ResourceDescriptor descriptor)
            {
                return Task.FromResult(Create(descriptor));
            }
        }

        private sealed class CountingPooledConnectionFactory : IPooledResourceConnectionFactory<IDeviceClient>
        {
            private int _createCount;

            public int CreateCount => _createCount;

            public OperationResult<IPooledResourceConnection<IDeviceClient>> Create(ResourceDescriptor descriptor)
            {
                Interlocked.Increment(ref _createCount);
                return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceClient>>(new FakePooledConnection(descriptor.Identity));
            }

            public Task<OperationResult<IPooledResourceConnection<IDeviceClient>>> CreateAsync(ResourceDescriptor descriptor)
            {
                return Task.FromResult(Create(descriptor));
            }
        }

        private sealed class FakePooledConnection : IPooledResourceConnection<IDeviceClient>
        {
            private int _connectCount;

            public FakePooledConnection(ConnectionIdentity identity)
            {
                Identity = identity;
                State = ConnectionEntryLifecycleState.Uninitialized;
                LastActiveTimeUtc = DateTime.UtcNow;
            }

            public ConnectionIdentity Identity { get; private set; }

            public ConnectionEntryLifecycleState State { get; private set; }

            public DateTime LastActiveTimeUtc { get; private set; }

            public bool IsAvailable => State == ConnectionEntryLifecycleState.Ready || State == ConnectionEntryLifecycleState.Leased;

            public IDeviceClient Resource => null;

            public OperationResult EnsureAvailable()
            {
                _connectCount++;
                State = ConnectionEntryLifecycleState.Ready;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateSuccessResult();
            }

            public Task<OperationResult> EnsureAvailableAsync()
            {
                return Task.FromResult(EnsureAvailable());
            }

            public Task<OperationResult> ProbeAsync(TimeSpan timeout)
            {
                LastActiveTimeUtc = DateTime.UtcNow;
                return Task.FromResult(State == ConnectionEntryLifecycleState.Faulted
                    ? OperationResult.CreateFailedResult("连接已失效")
                    : OperationResult.CreateSuccessResult());
            }

            public OperationResult Invalidate(string reason)
            {
                State = ConnectionEntryLifecycleState.Faulted;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateFailedResult(reason);
            }

            public OperationResult DisconnectOrShutdown()
            {
                State = ConnectionEntryLifecycleState.Disposed;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateSuccessResult();
            }

            public async Task<OperationResult<T>> ExecuteAsync<T>(Func<IDeviceClient, Task<OperationResult<T>>> action)
            {
                var result = await action(null);
                LastActiveTimeUtc = DateTime.UtcNow;
                return result;
            }

            public async Task<OperationResult> ExecuteAsync(Func<IDeviceClient, Task<OperationResult>> action)
            {
                var result = await action(null);
                LastActiveTimeUtc = DateTime.UtcNow;
                return result;
            }
        }

        private sealed class InspectablePooledConnection : IPooledResourceConnection<IDeviceClient>
        {
            public InspectablePooledConnection(ConnectionIdentity identity)
            {
                Identity = identity;
                State = ConnectionEntryLifecycleState.Uninitialized;
                LastActiveTimeUtc = DateTime.UtcNow;
            }

            public int DisconnectCount { get; private set; }

            public ConnectionIdentity Identity { get; private set; }

            public ConnectionEntryLifecycleState State { get; private set; }

            public DateTime LastActiveTimeUtc { get; private set; }

            public bool IsAvailable => State == ConnectionEntryLifecycleState.Ready || State == ConnectionEntryLifecycleState.Leased;

            public IDeviceClient Resource => null;

            public OperationResult EnsureAvailable()
            {
                State = ConnectionEntryLifecycleState.Ready;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateSuccessResult();
            }

            public Task<OperationResult> EnsureAvailableAsync()
            {
                return Task.FromResult(EnsureAvailable());
            }

            public Task<OperationResult> ProbeAsync(TimeSpan timeout)
            {
                LastActiveTimeUtc = DateTime.UtcNow;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public OperationResult Invalidate(string reason)
            {
                State = ConnectionEntryLifecycleState.Faulted;
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
                var result = await action(null).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                return result;
            }

            public async Task<OperationResult> ExecuteAsync(Func<IDeviceClient, Task<OperationResult>> action)
            {
                var result = await action(null).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                return result;
            }
        }

        private sealed class TestObjectConnection : IPooledResourceConnection<object>
        {
            public TestObjectConnection(ConnectionIdentity identity)
            {
                Identity = identity;
                State = ConnectionEntryLifecycleState.Uninitialized;
                LastActiveTimeUtc = DateTime.UtcNow;
                Resource = new object();
            }

            public int DisconnectCount { get; private set; }

            public ConnectionIdentity Identity { get; private set; }

            public ConnectionEntryLifecycleState State { get; private set; }

            public DateTime LastActiveTimeUtc { get; private set; }

            public bool IsAvailable => State == ConnectionEntryLifecycleState.Ready || State == ConnectionEntryLifecycleState.Leased;

            public object Resource { get; private set; }

            public OperationResult EnsureAvailable()
            {
                State = ConnectionEntryLifecycleState.Ready;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateSuccessResult();
            }

            public Task<OperationResult> EnsureAvailableAsync()
            {
                return Task.FromResult(EnsureAvailable());
            }

            public Task<OperationResult> ProbeAsync(TimeSpan timeout)
            {
                LastActiveTimeUtc = DateTime.UtcNow;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public OperationResult Invalidate(string reason)
            {
                State = ConnectionEntryLifecycleState.Faulted;
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

            public async Task<OperationResult<T>> ExecuteAsync<T>(Func<object, Task<OperationResult<T>>> action)
            {
                var result = await action(Resource).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                return result;
            }

            public async Task<OperationResult> ExecuteAsync(Func<object, Task<OperationResult>> action)
            {
                var result = await action(Resource).ConfigureAwait(false);
                LastActiveTimeUtc = DateTime.UtcNow;
                return result;
            }
        }

        private sealed class NullEventPublisher : IConnectionPoolEventPublisher
        {
            public void Publish(ConnectionPoolEventArgs args)
            {
            }

            public void PublishStateChanged(ConnectionStateChangedEventArgs args)
            {
            }

            public void PublishLeaseEvent(ConnectionLeaseEventArgs args)
            {
            }

            public void PublishMaintenanceEvent(ConnectionMaintenanceEventArgs args)
            {
            }
        }

        private static void TryUpdateMaxConcurrency(ref int maxValue, int candidate)
        {
            while (true)
            {
                var snapshot = maxValue;
                if (candidate <= snapshot)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref maxValue, candidate, snapshot) == snapshot)
                {
                    return;
                }
            }
        }
    }
}



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
            var options = new ConnectionPoolOptions { LeaseTimeout = TimeSpan.FromSeconds(5), MaxRetryCount = 0, MaxConcurrentExecutionsPerEntry = 0 };
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
        public async Task Should_Queue_Executions_Serially_By_Default()
        {
            var identity = new ConnectionIdentity { DeviceId = "serial", ProtocolType = "Mock", Endpoint = "serial" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false, MaxRetryCount = 0 }, new FakePooledConnectionFactory());
            var order = new ConcurrentQueue<int>();
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            var first = pool.ExecuteAsync(identity, async _ =>
            {
                order.Enqueue(1);
                firstStarted.TrySetResult(true);
                await releaseFirst.Task.ConfigureAwait(false);
                return OperationResult.CreateSuccessResult();
            });
            await firstStarted.Task.ConfigureAwait(false);
            var second = pool.ExecuteAsync(identity, _ =>
            {
                order.Enqueue(2);
                return Task.FromResult(OperationResult.CreateSuccessResult());
            });

            await Task.Delay(50).ConfigureAwait(false);
            Assert.Equal(new[] { 1 }, order.ToArray());
            releaseFirst.TrySetResult(true);
            Assert.All(await Task.WhenAll(first, second).ConfigureAwait(false), result => Assert.True(result.IsSuccess));
            Assert.Equal(new[] { 1, 2 }, order.ToArray());
        }

        [Fact]
        public async Task Should_Respect_Per_Entry_Execution_Limit()
        {
            var identity = new ConnectionIdentity { DeviceId = "limit", ProtocolType = "Mock", Endpoint = "limit" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false, MaxRetryCount = 0, MaxConcurrentExecutionsPerEntry = 2 }, new FakePooledConnectionFactory());
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var active = 0;
            var maximum = 0;
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));

            Func<IDeviceClient, Task<OperationResult>> action = async _ =>
            {
                TryUpdateMaxConcurrency(ref maximum, Interlocked.Increment(ref active));
                await release.Task.ConfigureAwait(false);
                Interlocked.Decrement(ref active);
                return OperationResult.CreateSuccessResult();
            };
            var executions = Enumerable.Range(0, 4).Select(_ => pool.ExecuteAsync(identity, action)).ToArray();

            for (var i = 0; i < 50 && Volatile.Read(ref active) < 2; i++) await Task.Delay(10).ConfigureAwait(false);
            Assert.Equal(2, Volatile.Read(ref active));
            release.TrySetResult(true);
            Assert.All(await Task.WhenAll(executions).ConfigureAwait(false), result => Assert.True(result.IsSuccess));
            Assert.Equal(2, maximum);
        }

        [Fact]
        public async Task Should_Cancel_While_Waiting_For_Execution_Slot()
        {
            var identity = new ConnectionIdentity { DeviceId = "wait-cancel", ProtocolType = "Mock", Endpoint = "wait-cancel" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false, MaxRetryCount = 0 }, new FakePooledConnectionFactory());
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var first = pool.ExecuteAsync(identity, async _ => { started.TrySetResult(true); await release.Task.ConfigureAwait(false); return OperationResult.CreateSuccessResult(); });
            await started.Task.ConfigureAwait(false);
            using var cancellation = new CancellationTokenSource(50);

            var waiting = await pool.ExecuteAsync(identity, _ => Task.FromResult(OperationResult.CreateSuccessResult()), cancellation.Token).ConfigureAwait(false);

            Assert.False(waiting.IsSuccess);
            Assert.True(waiting.IsCancelled);
            release.TrySetResult(true);
            Assert.True((await first.ConfigureAwait(false)).IsSuccess);
        }

        [Fact]
        public async Task Should_Cancel_Queued_Execution_When_Force_Closed()
        {
            var identity = new ConnectionIdentity { DeviceId = "wait-close", ProtocolType = "Mock", Endpoint = "wait-close" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false, MaxRetryCount = 0 }, new FakePooledConnectionFactory());
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var first = pool.ExecuteAsync(identity, async _ => { started.TrySetResult(true); await release.Task.ConfigureAwait(false); return OperationResult.CreateSuccessResult(); });
            await started.Task.ConfigureAwait(false);
            var waiting = pool.ExecuteAsync(identity, _ => Task.FromResult(OperationResult.CreateSuccessResult()));
            await Task.Delay(50).ConfigureAwait(false);

            Assert.True((await pool.ForceCloseAsync(identity, "测试关闭等待者").ConfigureAwait(false)).IsSuccess);
            var completed = await Task.WhenAny(waiting, Task.Delay(500)).ConfigureAwait(false);
            Assert.Same(waiting, completed);
            Assert.True((await waiting.ConfigureAwait(false)).IsCancelled);
            release.TrySetResult(true);
            Assert.True((await first.ConfigureAwait(false)).IsCancelled);
        }

        [Fact]
        public async Task Should_Not_Expire_Lease_While_Execution_Is_Active()
        {
            var identity = new ConnectionIdentity { DeviceId = "lease-exec", ProtocolType = "Mock", Endpoint = "lease-exec" };
            var pool = new DeviceClientPool(
                new ConnectionPoolOptions
                {
                    EnableBackgroundMaintenance = false,
                    LeaseTimeout = TimeSpan.FromMilliseconds(50),
                    MaxRetryCount = 0
                },
                new FakePooledConnectionFactory());
            var executionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseExecution = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Assert.True(pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity)).IsSuccess);
            var execution = pool.ExecuteAsync(identity, async _ =>
            {
                executionStarted.TrySetResult(true);
                await releaseExecution.Task.ConfigureAwait(false);
                return OperationResult.CreateSuccessResult();
            });

            await executionStarted.Task.ConfigureAwait(false);
            await Task.Delay(80).ConfigureAwait(false);

            var expired = pool.CleanupExpiredLeases();
            var snapshot = pool.GetState(identity);

            releaseExecution.TrySetResult(true);
            var executionResult = await execution.ConfigureAwait(false);

            Assert.True(expired.IsSuccess);
            Assert.Equal(0, expired.ResultValue);
            Assert.True(snapshot.IsSuccess);
            Assert.Equal(1, snapshot.ResultValue.ActiveLeaseCount);
            Assert.True(executionResult.IsSuccess);
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
            var events = new ConcurrentBag<ConnectionPoolEventType>();
            pool.PoolEventOccurred += (sender, args) => events.Add(args.EventType);

            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var lease = await pool.AcquireAsync(identity);
            Assert.True(lease.IsSuccess);
            Assert.True(pool.Release(lease.ResultValue).IsSuccess);

            for (var i = 0; i < 50 && !events.Contains(ConnectionPoolEventType.LeaseReleased); i++)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }

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
        public async Task Should_Force_Reconnect_After_Background_Fault()
        {
            var identity = new ConnectionIdentity { DeviceId = "reconnect-faulted", ProtocolType = "Mock", Endpoint = "reconnect-faulted" };
            var descriptor = ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity);
            var connection = new FakePooledConnection(identity);
            var entry = new PooledResourceEntry<IDeviceClient>(descriptor, connection, new NullEventPublisher());

            var ready = await entry.EnsureAvailableAsync().ConfigureAwait(false);
            Assert.True(ready.IsSuccess);

            var faulted = await entry.MarkFailureAsync("模拟后台故障", null, ConnectionPoolMaintenanceMode.Background).ConfigureAwait(false);
            Assert.False(faulted.IsSuccess);
            Assert.Equal(ConnectionEntryLifecycleState.Faulted, entry.LifecycleState);

            var force = await entry.ForceReconnectAsync("测试重连").ConfigureAwait(false);

            Assert.True(force.IsSuccess);
            Assert.Equal(ConnectionEntryLifecycleState.Ready, entry.LifecycleState);
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
        public async Task Should_Classify_Recoverable_Failures_Without_Misclassifying_Business_Errors()
        {
            var recoverableFailures = new OperationResult[]
            {
                OperationResult.CreateFromException(new TimeoutException("timeout")),
                OperationResult.CreateFromException(new System.IO.IOException("transport failed")),
                OperationResult.CreateFailedResult("连接已关闭"),
                OperationResult.CreateFailedResult("读取超时")
            };

            for (var i = 0; i < recoverableFailures.Length; i++)
            {
                var identity = new ConnectionIdentity { DeviceId = "recoverable-" + i, ProtocolType = "Mock", Endpoint = "recoverable-" + i };
                var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false, MaxRetryCount = 1, RetryBackoff = TimeSpan.FromMilliseconds(1) }, new FakePooledConnectionFactory());
                pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
                var attempts = 0;

                var result = await pool.ExecuteAsync<int>(identity, client => Task.FromResult(++attempts == 1 ? OperationResult.CreateFailedResult<int>(recoverableFailures[i]) : OperationResult.CreateSuccessResult(1)), ConnectionExecutionOptions.CreateRead()).ConfigureAwait(false);

                Assert.True(result.IsSuccess, "恢复案例 " + i + " 尝试 " + attempts + " 次后失败: " + result.Message + " / " + string.Join(" | ", result.OperationInfo));
                Assert.Equal(2, attempts);
            }

            var businessIdentity = new ConnectionIdentity { DeviceId = "business", ProtocolType = "Mock", Endpoint = "business" };
            var businessPool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false, MaxRetryCount = 2 }, new FakePooledConnectionFactory());
            businessPool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(businessIdentity));
            var businessAttempts = 0;
            var businessException = new ArgumentException("invalid port");
            var businessFailure = OperationResult.CreateFailedResult<int>("connection parameter port is invalid");
            businessFailure.Exception = businessException;
            businessFailure.FailureKind = OperationFailureKind.Validation;

            var businessResult = await businessPool.ExecuteAsync<int>(businessIdentity, client => { businessAttempts++; return Task.FromResult(businessFailure); }, ConnectionExecutionOptions.CreateRead()).ConfigureAwait(false);

            Assert.False(businessResult.IsSuccess);
            Assert.Equal(1, businessAttempts);
            Assert.Same(businessException, businessResult.Exception);
            Assert.Equal(ConnectionEntryState.Ready, businessPool.GetState(businessIdentity).ResultValue.State);
        }

        [Fact]
        public async Task Should_Auto_Release_Lease_And_Allow_Repeated_Release()
        {
            var identity = new ConnectionIdentity { DeviceId = "lease-dispose", ProtocolType = "Mock", Endpoint = "lease-dispose" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var acquired = await pool.AcquireAsync(identity).ConfigureAwait(false);

            acquired.ResultValue.Dispose();
            var repeated = await pool.ReleaseAsync(acquired.ResultValue).ConfigureAwait(false);

            Assert.True(repeated.IsSuccess);
            Assert.Equal(0, pool.GetState(identity).ResultValue.ActiveLeaseCount);
        }

        [Fact]
        public async Task Should_Preserve_Retry_Event_Order_And_Attempt_Count()
        {
            var identity = new ConnectionIdentity { DeviceId = "retry-events", ProtocolType = "Mock", Endpoint = "retry-events" };
            var events = new ConcurrentQueue<ConnectionPoolEventType>();
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false, MaxRetryCount = 1, RetryBackoff = TimeSpan.FromMilliseconds(1) }, new FakePooledConnectionFactory());
            pool.PoolEventOccurred += (sender, args) => events.Enqueue(args.EventType);
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var attempts = 0;

            var result = await pool.ExecuteAsync<int>(identity, client => Task.FromResult(++attempts == 1 ? OperationResult.CreateFailedResult<int>(new TimeoutException("timeout")) : OperationResult.CreateSuccessResult(1)), ConnectionExecutionOptions.CreateRead()).ConfigureAwait(false);
            for (var i = 0; i < 50 && !events.Contains(ConnectionPoolEventType.Retrying); i++)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }

            var ordered = events.ToList();
            Assert.True(result.IsSuccess);
            Assert.Equal(2, attempts);
            Assert.True(ordered.IndexOf(ConnectionPoolEventType.ExecuteFailed) < ordered.IndexOf(ConnectionPoolEventType.Retrying));
            Assert.True(ordered.IndexOf(ConnectionPoolEventType.Retrying) < ordered.LastIndexOf(ConnectionPoolEventType.ConnectSucceeded));
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
            Assert.False(acquire.IsSuccess);
            Assert.True(release.IsSuccess);
        }

        [Fact]
        public async Task Should_Cancel_Acquire_While_Connecting()
        {
            var identity = new ConnectionIdentity { DeviceId = "cancel-connect", ProtocolType = "Mock", Endpoint = "cancel-connect" };
            var factory = new BlockingPooledConnectionFactory();
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, factory);
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            using var cancellation = new CancellationTokenSource(100);

            var acquire = await pool.AcquireAsync(identity, cancellation.Token).ConfigureAwait(false);

            Assert.False(acquire.IsSuccess);
            Assert.True(acquire.IsCancelled);
            factory.CompleteConnect();
            await pool.DisposeAsync().ConfigureAwait(false);
        }

        [Fact]
        public async Task Should_Dispose_Without_Waiting_For_Connect()
        {
            var identity = new ConnectionIdentity { DeviceId = "dispose-connect", ProtocolType = "Mock", Endpoint = "dispose-connect" };
            var factory = new BlockingPooledConnectionFactory();
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, factory);
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var acquire = pool.AcquireAsync(identity);
            await factory.ConnectStarted.Task.ConfigureAwait(false);

            var dispose = pool.DisposeAsync();
            for (var i = 0; i < 50 && factory.DisconnectCount == 0; i++)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }
            Assert.True(factory.DisconnectCount > 0);
            Assert.True((await acquire.ConfigureAwait(false)).IsCancelled);
            factory.CompleteConnect();
            await dispose.ConfigureAwait(false);
        }

        [Fact]
        public async Task Should_Release_Async_Safely_During_Dispose()
        {
            var identity = new ConnectionIdentity { DeviceId = "release-dispose", ProtocolType = "Mock", Endpoint = "release-dispose" };
            var pool = new DeviceClientPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity));
            var lease = await pool.AcquireAsync(identity).ConfigureAwait(false);

            var dispose = pool.DisposeAsync();
            var release = await pool.ReleaseAsync(lease.ResultValue).ConfigureAwait(false);
            await dispose.ConfigureAwait(false);

            Assert.True(release.IsSuccess);
        }

        [Fact]
        public async Task Should_Force_Close_Without_Waiting_For_Active_Execution_To_Drain()
        {
            var identity = new ConnectionIdentity { DeviceId = "force-close-no-drain", ProtocolType = "Mock", Endpoint = "force-close-no-drain" };
            var descriptor = ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(identity);
            var connection = new TestObjectConnection(identity);
            var events = new List<ConnectionPoolEventType>();
            var entry = new PooledResourceEntry<object>(descriptor, connection, new RecordingEventPublisher(events));
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseExecution = new TaskCompletionSource<OperationResult<object>>(TaskCreationOptions.RunContinuationsAsynchronously);

            var executeTask = entry.ExecuteAsync<object>((resource, cancellationToken) =>
            {
                started.TrySetResult(true);
                return releaseExecution.Task;
            }, CancellationToken.None);

            await started.Task.ConfigureAwait(false);

            var forceCloseTask = entry.ForceCloseAsync("测试强制关闭", CancellationToken.None);
            var completed = await Task.WhenAny(forceCloseTask, Task.Delay(200)).ConfigureAwait(false);

            Assert.Same(forceCloseTask, completed);
            Assert.True(forceCloseTask.Result.IsSuccess);
            Assert.False(executeTask.IsCompleted);
            Assert.Equal(0, entry.ActiveLeaseCount);
            Assert.Equal(ConnectionEntryLifecycleState.Faulted, entry.LifecycleState);
            Assert.DoesNotContain(ConnectionPoolEventType.ForceCloseDrained, events);

            releaseExecution.SetResult(OperationResult.CreateSuccessResult<object>(new object()));
            var executeResult = await executeTask.ConfigureAwait(false);

            Assert.False(executeResult.IsSuccess);
            Assert.True(executeResult.IsCancelled);
            Assert.Contains("强制关闭", executeResult.Message);
            Assert.Contains(ConnectionPoolEventType.ForceCloseDrained, events);
        }

        [Fact]
        public async Task Should_Force_Close_Many_And_Return_Per_Entry_Results()
        {
            var pool = new DeviceClientPool(
                new ConnectionPoolOptions { EnableBackgroundMaintenance = false, MaxConcurrentForceCloses = 2 },
                new FakePooledConnectionFactory());
            var idA = new ConnectionIdentity { DeviceId = "close-many-a", ProtocolType = "Mock", Endpoint = "close-many-a" };
            var idB = new ConnectionIdentity { DeviceId = "close-many-b", ProtocolType = "Mock", Endpoint = "close-many-b" };

            Assert.True(pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(idA)).IsSuccess);
            Assert.True(pool.Register(ConnectionPoolTestDescriptors.CreateModbusTcpClientDescriptor(idB)).IsSuccess);

            var result = await pool.ForceCloseManyAsync(new[] { idA, idB }, "批量关闭").ConfigureAwait(false);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.ResultValue.Count);
            Assert.All(result.ResultValue.Values, item => Assert.True(item.IsSuccess));
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

        private sealed class BlockingPooledConnectionFactory : IPooledResourceConnectionFactory<IDeviceClient>
        {
            private readonly TaskCompletionSource<OperationResult> _connect = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<bool> ConnectStarted { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public int DisconnectCount { get; private set; }

            public OperationResult<IPooledResourceConnection<IDeviceClient>> Create(ResourceDescriptor descriptor)
            {
                return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceClient>>(new BlockingPooledConnection(this, descriptor.Identity));
            }

            public Task<OperationResult<IPooledResourceConnection<IDeviceClient>>> CreateAsync(ResourceDescriptor descriptor) => Task.FromResult(Create(descriptor));

            public void CompleteConnect() => _connect.TrySetResult(OperationResult.CreateSuccessResult());

            private sealed class BlockingPooledConnection : IPooledResourceConnection<IDeviceClient>
            {
                private readonly BlockingPooledConnectionFactory _owner;

                public BlockingPooledConnection(BlockingPooledConnectionFactory owner, ConnectionIdentity identity) { _owner = owner; Identity = identity; }
                public ConnectionIdentity Identity { get; }
                public ConnectionEntryLifecycleState State { get; private set; }
                public DateTime LastActiveTimeUtc => DateTime.UtcNow;
                public bool IsAvailable => false;
                public IDeviceClient Resource => null;
                public OperationResult EnsureAvailable() => EnsureAvailableAsync().GetAwaiter().GetResult();
                public async Task<OperationResult> EnsureAvailableAsync() { _owner.ConnectStarted.TrySetResult(true); return await _owner._connect.Task.ConfigureAwait(false); }
                public Task<OperationResult> ProbeAsync(TimeSpan timeout) => Task.FromResult(OperationResult.CreateSuccessResult());
                public OperationResult Invalidate(string reason) => OperationResult.CreateFailedResult(reason);
                public OperationResult DisconnectOrShutdown() { _owner.DisconnectCount++; State = ConnectionEntryLifecycleState.Disposed; return OperationResult.CreateSuccessResult(); }
                public Task<OperationResult<T>> ExecuteAsync<T>(Func<IDeviceClient, Task<OperationResult<T>>> action) => action(null);
                public Task<OperationResult> ExecuteAsync(Func<IDeviceClient, Task<OperationResult>> action) => action(null);
            }
        }

        private sealed class RecordingEventPublisher : IConnectionPoolEventPublisher
        {
            private readonly IList<ConnectionPoolEventType> _events;

            public RecordingEventPublisher(IList<ConnectionPoolEventType> events)
            {
                _events = events;
            }

            public void Publish(ConnectionPoolEventArgs args)
            {
                if (args != null)
                {
                    _events.Add(args.EventType);
                }
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



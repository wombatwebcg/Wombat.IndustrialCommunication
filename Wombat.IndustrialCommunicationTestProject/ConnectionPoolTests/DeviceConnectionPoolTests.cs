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
    public class DeviceConnectionPoolTests
    {
        [Fact]
        public async Task Should_Reuse_Connection_For_Same_Identity()
        {
            var options = new ConnectionPoolOptions { LeaseTimeout = TimeSpan.FromSeconds(5) };
            var pool = new DeviceConnectionPool(options, new FakePooledConnectionFactory());

            var identity = new ConnectionIdentity { DeviceId = "dev1", ProtocolType = "ModbusTcp", Endpoint = "127.0.0.1:502" };
            var descriptor = new DeviceConnectionDescriptor
            {
                Identity = identity,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp
            };

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
            var descriptor = new DeviceConnectionDescriptor
            {
                Identity = identity,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp
            };
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());

            var first = pool.Register(descriptor);
            var second = pool.Register(descriptor);

            Assert.True(first.IsSuccess);
            Assert.False(second.IsSuccess);
        }

        [Fact]
        public async Task Should_Register_Atomically_Under_Concurrent_Duplicate_Register()
        {
            var identity = new ConnectionIdentity { DeviceId = "dup-concurrent", ProtocolType = "Mock", Endpoint = "dup-concurrent" };
            var descriptor = new DeviceConnectionDescriptor
            {
                Identity = identity,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp
            };
            var factory = new CountingPooledConnectionFactory();
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, factory);

            var tasks = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => pool.Register(descriptor)))
                .ToArray();

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            Assert.Equal(1, results.Count(t => t.IsSuccess));
            Assert.Equal(1, factory.CreateCount);
        }

        [Fact]
        public async Task Should_Serialize_Same_Device_And_Allow_Parallel_Different_Devices()
        {
            var options = new ConnectionPoolOptions { LeaseTimeout = TimeSpan.FromSeconds(5), MaxRetryCount = 0 };
            var pool = new DeviceConnectionPool(options, new FakePooledConnectionFactory());

            var idA = new ConnectionIdentity { DeviceId = "A", ProtocolType = "Mock", Endpoint = "A" };
            var idB = new ConnectionIdentity { DeviceId = "B", ProtocolType = "Mock", Endpoint = "B" };
            pool.Register(new DeviceConnectionDescriptor { Identity = idA, DeviceConnectionType = DeviceConnectionType.ModbusTcp });
            pool.Register(new DeviceConnectionDescriptor { Identity = idB, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

            var sameA1 = pool.ExecuteAsync(idA, async _ => { await Task.Delay(120); return OperationResult.CreateSuccessResult(); });
            var sameA2 = pool.ExecuteAsync(idA, async _ => { await Task.Delay(120); return OperationResult.CreateSuccessResult(); });
            var differentB = pool.ExecuteAsync(idB, async _ => { await Task.Delay(120); return OperationResult.CreateSuccessResult(); });

            var startedAt = DateTime.UtcNow;
            var results = await Task.WhenAll(sameA1, sameA2, differentB);
            var elapsed = DateTime.UtcNow - startedAt;

            Assert.All(results, r => Assert.True(r.IsSuccess));
            Assert.True(elapsed >= TimeSpan.FromMilliseconds(220), "同设备执行应串行，耗时应接近叠加");
            Assert.True(elapsed < TimeSpan.FromMilliseconds(360), "不同设备执行应并行，不应完全串行");
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
            var pool = new DeviceConnectionPool(options, new FakePooledConnectionFactory());

            var identity = new ConnectionIdentity { DeviceId = "cleanup", ProtocolType = "Mock", Endpoint = "cleanup" };
            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

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
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

            var result = pool.Unregister(identity, "测试注销");
            var snapshot = pool.GetState(identity);

            Assert.True(result.IsSuccess);
            Assert.False(snapshot.IsSuccess);
        }

        [Fact]
        public void Should_Reject_Unregister_When_Lease_Is_Active()
        {
            var identity = new ConnectionIdentity { DeviceId = "unregister-active", ProtocolType = "Mock", Endpoint = "unregister-active" };
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

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
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

            var snapshot = pool.GetState(identity);

            Assert.True(snapshot.IsSuccess);
            Assert.Equal(identity, snapshot.ResultValue.Identity);
            Assert.Equal(ConnectionEntryState.Uninitialized, snapshot.ResultValue.State);
        }

        [Fact]
        public async Task Should_Publish_Lifecycle_Events()
        {
            var identity = new ConnectionIdentity { DeviceId = "evented", ProtocolType = "Mock", Endpoint = "evented" };
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            var events = new List<ConnectionPoolEventType>();
            pool.PoolEventOccurred += (sender, args) => events.Add(args.EventType);

            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });
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
            var pool = new DeviceConnectionPool(options, new FakePooledConnectionFactory());
            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

            var lease = pool.Acquire(identity);
            Assert.True(lease.IsSuccess);
            Task.Delay(20).GetAwaiter().GetResult();

            var cleanup = pool.CleanupExpiredLeases();
            var snapshot = pool.GetState(identity);

            Assert.True(cleanup.IsSuccess);
            Assert.True(cleanup.ResultValue >= 1);
            Assert.True(snapshot.IsSuccess);
            Assert.Equal(ConnectionEntryState.Ready, snapshot.ResultValue.State);
            Assert.Equal(0, snapshot.ResultValue.ActiveLeaseCount);
        }

        [Fact]
        public void Should_Force_Reconnect_And_Expose_Pool_Snapshot()
        {
            var identity = new ConnectionIdentity { DeviceId = "reconnect", ProtocolType = "Mock", Endpoint = "reconnect" };
            var factory = new FakePooledConnectionFactory();
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, factory);
            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

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
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

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
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());
            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

            var lease = await pool.AcquireAsync(identity).ConfigureAwait(false);
            Assert.True(lease.IsSuccess);

            pool.Dispose();
            var release = pool.Release(lease.ResultValue);

            Assert.True(release.IsSuccess);
        }

        [Fact]
        public void Should_Expose_Segregated_Interfaces()
        {
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new FakePooledConnectionFactory());

            Assert.IsAssignableFrom<IDeviceConnectionPoolQuery>(pool);
            Assert.IsAssignableFrom<IDeviceConnectionPoolControl>(pool);
            Assert.IsAssignableFrom<IDeviceConnectionPoolExecution>(pool);
            Assert.IsAssignableFrom<IDeviceConnectionPoolEvents>(pool);
        }

        private sealed class FakePooledConnectionFactory : IPooledDeviceConnectionFactory
        {
            public OperationResult<IPooledDeviceConnection> Create(DeviceConnectionDescriptor descriptor)
            {
                return OperationResult.CreateSuccessResult<IPooledDeviceConnection>(new FakePooledConnection(descriptor.Identity));
            }

            public Task<OperationResult<IPooledDeviceConnection>> CreateAsync(DeviceConnectionDescriptor descriptor)
            {
                return Task.FromResult(Create(descriptor));
            }
        }

        private sealed class CountingPooledConnectionFactory : IPooledDeviceConnectionFactory
        {
            private int _createCount;

            public int CreateCount => _createCount;

            public OperationResult<IPooledDeviceConnection> Create(DeviceConnectionDescriptor descriptor)
            {
                Interlocked.Increment(ref _createCount);
                return OperationResult.CreateSuccessResult<IPooledDeviceConnection>(new FakePooledConnection(descriptor.Identity));
            }

            public Task<OperationResult<IPooledDeviceConnection>> CreateAsync(DeviceConnectionDescriptor descriptor)
            {
                return Task.FromResult(Create(descriptor));
            }
        }

        private sealed class FakePooledConnection : IPooledDeviceConnection
        {
            private int _connectCount;

            public FakePooledConnection(ConnectionIdentity identity)
            {
                Identity = identity;
                State = ConnectionEntryState.Uninitialized;
                LastActiveTimeUtc = DateTime.UtcNow;
            }

            public ConnectionIdentity Identity { get; private set; }

            public ConnectionEntryState State { get; private set; }

            public DateTime LastActiveTimeUtc { get; private set; }

            public IDeviceClient Client => null;

            public OperationResult EnsureConnected()
            {
                _connectCount++;
                State = ConnectionEntryState.Ready;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateSuccessResult();
            }

            public Task<OperationResult> EnsureConnectedAsync()
            {
                return Task.FromResult(EnsureConnected());
            }

            public OperationResult Invalidate(string reason)
            {
                State = ConnectionEntryState.Invalidated;
                return OperationResult.CreateFailedResult(reason);
            }

            public OperationResult Disconnect()
            {
                State = ConnectionEntryState.Disposed;
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
    }
}

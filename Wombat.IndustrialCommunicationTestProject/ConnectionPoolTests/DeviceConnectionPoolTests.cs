using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
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
                IdleTimeout = TimeSpan.FromMilliseconds(1)
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

        private sealed class FakePooledConnection : IPooledDeviceConnection
        {
            public FakePooledConnection(ConnectionIdentity identity)
            {
                Identity = identity;
                State = ConnectionEntryState.Ready;
                LastActiveTimeUtc = DateTime.UtcNow;
            }

            public ConnectionIdentity Identity { get; private set; }

            public ConnectionEntryState State { get; private set; }

            public DateTime LastActiveTimeUtc { get; private set; }

            public IDeviceClient Client => null;

            public OperationResult EnsureConnected()
            {
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

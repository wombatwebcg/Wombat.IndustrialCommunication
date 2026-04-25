using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Events;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ConnectionPoolTests
{
    public class ConnectionPoolEventsTests
    {
        [Fact]
        public async Task Should_Raise_State_And_Lease_Events()
        {
            var identity = new ConnectionIdentity { DeviceId = "evt", ProtocolType = "Mock", Endpoint = "evt" };
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new EventConnectionFactory());
            var states = new List<ConnectionEntryState>();
            var leases = new List<ConnectionPoolEventType>();

            pool.ConnectionStateChanged += (sender, args) => states.Add(args.CurrentState);
            pool.LeaseChanged += (sender, args) => leases.Add(args.EventType);

            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });
            var lease = await pool.AcquireAsync(identity);
            Assert.True(lease.IsSuccess);
            Assert.True(pool.Release(lease.ResultValue).IsSuccess);

            Assert.Contains(ConnectionEntryState.Disconnected, states);
            Assert.Contains(ConnectionEntryState.Ready, states);
            Assert.Contains(ConnectionEntryState.Busy, states);
            Assert.Contains(ConnectionPoolEventType.LeaseAcquired, leases);
            Assert.Contains(ConnectionPoolEventType.LeaseReleased, leases);
        }

        [Fact]
        public async Task Should_Isolate_Subscriber_Exception_And_Keep_Main_Flow_Running()
        {
            var identity = new ConnectionIdentity { DeviceId = "evt-isolate", ProtocolType = "Mock", Endpoint = "evt-isolate" };
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new EventConnectionFactory());
            var invokedCount = 0;

            pool.LeaseChanged += (sender, args) =>
            {
                if (args.EventType == ConnectionPoolEventType.LeaseAcquired)
                {
                    throw new InvalidOperationException("subscriber boom");
                }
            };
            pool.LeaseChanged += (sender, args) =>
            {
                if (args.EventType == ConnectionPoolEventType.LeaseAcquired)
                {
                    invokedCount++;
                }
            };

            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });
            var lease = await pool.AcquireAsync(identity).ConfigureAwait(false);

            Assert.True(lease.IsSuccess);
            Assert.Equal(1, invokedCount);
            Assert.True(pool.Release(lease.ResultValue).IsSuccess);
        }

        [Fact]
        public async Task Should_Dispatch_Events_Outside_Entry_Lock()
        {
            var identity = new ConnectionIdentity { DeviceId = "evt-outside-lock", ProtocolType = "Mock", Endpoint = "evt-outside-lock" };
            var pool = new DeviceConnectionPool(new ConnectionPoolOptions { EnableBackgroundMaintenance = false }, new EventConnectionFactory());
            var handlerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handlerRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });
            var lease = await pool.AcquireAsync(identity).ConfigureAwait(false);
            Assert.True(lease.IsSuccess);

            pool.LeaseChanged += (sender, args) =>
            {
                if (args.EventType == ConnectionPoolEventType.LeaseReleased)
                {
                    handlerStarted.TrySetResult(true);
                    handlerRelease.Task.GetAwaiter().GetResult();
                }
            };

            var releaseTask = Task.Run(() => pool.Release(lease.ResultValue));
            await handlerStarted.Task.ConfigureAwait(false);

            var snapshotTask = Task.Run(() => pool.GetState(identity));
            var completed = await Task.WhenAny(snapshotTask, Task.Delay(200)).ConfigureAwait(false);

            handlerRelease.TrySetResult(true);
            var releaseResult = await releaseTask.ConfigureAwait(false);

            Assert.Same(snapshotTask, completed);
            Assert.True(snapshotTask.Result.IsSuccess);
            Assert.True(releaseResult.IsSuccess);
        }

        private sealed class EventConnectionFactory : IPooledDeviceConnectionFactory
        {
            public OperationResult<IPooledDeviceConnection> Create(DeviceConnectionDescriptor descriptor)
            {
                return OperationResult.CreateSuccessResult<IPooledDeviceConnection>(new EventConnection(descriptor.Identity));
            }

            public Task<OperationResult<IPooledDeviceConnection>> CreateAsync(DeviceConnectionDescriptor descriptor)
            {
                return Task.FromResult(Create(descriptor));
            }
        }

        private sealed class EventConnection : IPooledDeviceConnection
        {
            public EventConnection(ConnectionIdentity identity)
            {
                Identity = identity;
                State = ConnectionEntryLifecycleState.Uninitialized;
                LastActiveTimeUtc = DateTime.UtcNow;
            }

            public ConnectionIdentity Identity { get; private set; }

            public ConnectionEntryLifecycleState State { get; private set; }

            public DateTime LastActiveTimeUtc { get; private set; }

            public IDeviceClient Client => null;

            public OperationResult EnsureConnected()
            {
                State = ConnectionEntryLifecycleState.Ready;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateSuccessResult();
            }

            public Task<OperationResult> EnsureConnectedAsync()
            {
                return Task.FromResult(EnsureConnected());
            }

            public Task<OperationResult> ProbeAsync(TimeSpan timeout)
            {
                LastActiveTimeUtc = DateTime.UtcNow;
                return Task.FromResult(State == ConnectionEntryLifecycleState.Invalidated
                    ? OperationResult.CreateFailedResult("连接已失效")
                    : OperationResult.CreateSuccessResult());
            }

            public OperationResult Invalidate(string reason)
            {
                State = ConnectionEntryLifecycleState.Invalidated;
                LastActiveTimeUtc = DateTime.UtcNow;
                return OperationResult.CreateFailedResult(reason);
            }

            public OperationResult Disconnect()
            {
                State = ConnectionEntryLifecycleState.Disposed;
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
    }
}

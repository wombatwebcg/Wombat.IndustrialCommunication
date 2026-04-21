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

            Assert.Contains(ConnectionEntryState.Connecting, states);
            Assert.Contains(ConnectionEntryState.Ready, states);
            Assert.Contains(ConnectionEntryState.Leased, states);
            Assert.Contains(ConnectionPoolEventType.LeaseAcquired, leases);
            Assert.Contains(ConnectionPoolEventType.LeaseReleased, leases);
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
                State = ConnectionEntryState.Uninitialized;
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

using System;
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

            var pool = new DeviceConnectionPool(options, new MaintenanceConnectionFactory());
            var maintenanceRaised = false;
            pool.MaintenanceCompleted += (sender, args) =>
            {
                if (args.EventType == ConnectionPoolEventType.BackgroundMaintenanceCompleted)
                {
                    maintenanceRaised = true;
                }
            };

            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });
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

            var pool = new DeviceConnectionPool(options, new MaintenanceConnectionFactory());
            pool.Register(new DeviceConnectionDescriptor { Identity = identity, DeviceConnectionType = DeviceConnectionType.ModbusTcp });

            await Task.Delay(80).ConfigureAwait(false);
            var unregister = pool.Unregister(identity, "后台维护下注销");
            await Task.Delay(80).ConfigureAwait(false);

            Assert.True(unregister.IsSuccess);
            Assert.False(pool.GetState(identity).IsSuccess);
        }

        private sealed class MaintenanceConnectionFactory : IPooledDeviceConnectionFactory
        {
            public OperationResult<IPooledDeviceConnection> Create(DeviceConnectionDescriptor descriptor)
            {
                return OperationResult.CreateSuccessResult<IPooledDeviceConnection>(new MaintenanceConnection(descriptor.Identity));
            }

            public Task<OperationResult<IPooledDeviceConnection>> CreateAsync(DeviceConnectionDescriptor descriptor)
            {
                return Task.FromResult(Create(descriptor));
            }
        }

        private sealed class MaintenanceConnection : IPooledDeviceConnection
        {
            public MaintenanceConnection(ConnectionIdentity identity)
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
    }
}

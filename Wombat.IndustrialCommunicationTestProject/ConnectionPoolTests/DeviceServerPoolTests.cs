using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ConnectionPoolTests
{
    public class DeviceServerPoolTests
    {
        [Fact]
        public async Task StartAsync_Should_Retry_And_Succeed()
        {
            var identity = new ConnectionIdentity { DeviceId = "srv-retry-ok", ProtocolType = "MockServer", Endpoint = "srv-retry-ok" };
            var factory = new FakeServerPooledConnectionFactory(new FakeServerPooledConnection(identity)
            {
                FailEnsureAvailableTimes = 1
            });
            var pool = new DeviceServerPool(new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = false,
                MaxRetryCount = 2,
                RetryBackoff = TimeSpan.FromMilliseconds(1)
            }, factory);

            var registerResult = pool.Register(new ResourceDescriptor
            {
                Identity = identity,
                ResourceRole = ResourceRole.Server,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp
            });
            Assert.True(registerResult.IsSuccess);

            var startResult = await pool.StartAsync(identity).ConfigureAwait(false);

            Assert.True(startResult.IsSuccess);
            Assert.Equal(2, factory.Connection.EnsureAvailableCallCount);
        }

        [Fact]
        public async Task StartAsync_Should_Return_PortConflict_Message_When_ListenPort_InUse()
        {
            var identity = new ConnectionIdentity { DeviceId = "srv-port-conflict", ProtocolType = "MockServer", Endpoint = "srv-port-conflict" };
            var factory = new FakeServerPooledConnectionFactory(new FakeServerPooledConnection(identity)
            {
                FailEnsureAvailableTimes = 3,
                FailureMessage = "Address already in use"
            });
            var pool = new DeviceServerPool(new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = false,
                MaxRetryCount = 1,
                RetryBackoff = TimeSpan.FromMilliseconds(1)
            }, factory);

            var registerResult = pool.Register(new ResourceDescriptor
            {
                Identity = identity,
                ResourceRole = ResourceRole.Server,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp
            });
            Assert.True(registerResult.IsSuccess);

            var startResult = await pool.StartAsync(identity).ConfigureAwait(false);

            Assert.False(startResult.IsSuccess);
            Assert.Contains("端口", startResult.Message);
        }

        [Fact]
        public async Task StopAsync_Should_Call_DisconnectOrShutdown()
        {
            var identity = new ConnectionIdentity { DeviceId = "srv-stop", ProtocolType = "MockServer", Endpoint = "srv-stop" };
            var connection = new FakeServerPooledConnection(identity);
            var factory = new FakeServerPooledConnectionFactory(connection);
            var pool = new DeviceServerPool(new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = false
            }, factory);

            var registerResult = pool.Register(new ResourceDescriptor
            {
                Identity = identity,
                ResourceRole = ResourceRole.Server,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp
            });
            Assert.True(registerResult.IsSuccess);
            Assert.True((await pool.StartAsync(identity).ConfigureAwait(false)).IsSuccess);

            var stopResult = await pool.StopAsync(identity, "测试停止").ConfigureAwait(false);

            Assert.True(stopResult.IsSuccess);
            Assert.Equal(1, connection.DisconnectCallCount);
        }

        private sealed class FakeServerPooledConnectionFactory : IPooledResourceConnectionFactory<IDeviceServer>
        {
            public FakeServerPooledConnectionFactory(FakeServerPooledConnection connection)
            {
                Connection = connection;
            }

            public FakeServerPooledConnection Connection { get; }

            public OperationResult<IPooledResourceConnection<IDeviceServer>> Create(ResourceDescriptor descriptor)
            {
                return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceServer>>(Connection);
            }

            public Task<OperationResult<IPooledResourceConnection<IDeviceServer>>> CreateAsync(ResourceDescriptor descriptor)
            {
                return Task.FromResult(Create(descriptor));
            }
        }

        private sealed class FakeServerPooledConnection : IPooledResourceConnection<IDeviceServer>
        {
            public FakeServerPooledConnection(ConnectionIdentity identity)
            {
                Identity = identity;
                LastActiveTimeUtc = DateTime.UtcNow;
                State = ConnectionEntryLifecycleState.Uninitialized;
            }

            public int FailEnsureAvailableTimes { get; set; }

            public string FailureMessage { get; set; } = "mock ensure failed";

            public int EnsureAvailableCallCount { get; private set; }

            public int DisconnectCallCount { get; private set; }

            public ConnectionIdentity Identity { get; private set; }

            public ConnectionEntryLifecycleState State { get; private set; }

            public DateTime LastActiveTimeUtc { get; private set; }

            public bool IsAvailable { get; private set; }

            public IDeviceServer Resource => null;

            public OperationResult EnsureAvailable()
            {
                return EnsureAvailableAsync().GetAwaiter().GetResult();
            }

            public Task<OperationResult> EnsureAvailableAsync()
            {
                EnsureAvailableCallCount++;
                LastActiveTimeUtc = DateTime.UtcNow;
                if (FailEnsureAvailableTimes > 0)
                {
                    FailEnsureAvailableTimes--;
                    State = ConnectionEntryLifecycleState.Faulted;
                    IsAvailable = false;
                    return Task.FromResult(OperationResult.CreateFailedResult(FailureMessage));
                }

                State = ConnectionEntryLifecycleState.Ready;
                IsAvailable = true;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult> ProbeAsync(TimeSpan timeout)
            {
                return Task.FromResult(IsAvailable
                    ? OperationResult.CreateSuccessResult()
                    : OperationResult.CreateFailedResult("not available"));
            }

            public OperationResult Invalidate(string reason)
            {
                State = ConnectionEntryLifecycleState.Invalidated;
                IsAvailable = false;
                return OperationResult.CreateFailedResult(reason);
            }

            public OperationResult DisconnectOrShutdown()
            {
                DisconnectCallCount++;
                State = ConnectionEntryLifecycleState.Uninitialized;
                IsAvailable = false;
                return OperationResult.CreateSuccessResult();
            }

            public Task<OperationResult<T>> ExecuteAsync<T>(Func<IDeviceServer, Task<OperationResult<T>>> action)
            {
                return action(Resource);
            }

            public Task<OperationResult> ExecuteAsync(Func<IDeviceServer, Task<OperationResult>> action)
            {
                return action(Resource);
            }
        }
    }
}



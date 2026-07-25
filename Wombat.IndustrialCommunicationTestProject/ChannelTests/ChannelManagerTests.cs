using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.Channels;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.ChannelTests
{
    public class ChannelManagerTests
    {
        [Fact]
        public async Task SameChannel_IsStrictlySerial()
        {
            var client = new FakeClient();
            await using var manager = CreateManager(client);
            await manager.AddAsync(Options("serial"));
            var active = 0;
            var maximum = 0;

            async ValueTask<int> Operation(IProtocolClient _, CancellationToken token)
            {
                var current = Interlocked.Increment(ref active);
                maximum = Math.Max(maximum, current);
                await Task.Delay(30, token);
                Interlocked.Decrement(ref active);
                return current;
            }

            await Task.WhenAll(
                manager.ExecuteAsync("serial", Operation).AsTask(),
                manager.ExecuteAsync("serial", Operation).AsTask(),
                manager.ExecuteAsync("serial", Operation).AsTask());

            Assert.Equal(1, maximum);
        }

        [Fact]
        public async Task QueuedOperation_CanBeCancelled()
        {
            await using var manager = CreateManager(new FakeClient());
            await manager.AddAsync(Options("cancel"));
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = manager.ExecuteAsync("cancel", async (_, token) =>
            {
                entered.SetResult(true);
                await release.Task;
                return 1;
            }).AsTask();
            await entered.Task;
            using var cancellation = new CancellationTokenSource();
            var queued = manager.ExecuteAsync("cancel", (_, __) => new ValueTask<int>(2), cancellation.Token).AsTask();

            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
            release.SetResult(true);
            await first;
        }

        [Fact]
        public async Task Stop_CancelsActiveOperationAndDisconnects()
        {
            var client = new FakeClient();
            await using var manager = CreateManager(client);
            await manager.AddAsync(Options("stop"));
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = manager.ExecuteAsync("stop", async (_, token) =>
            {
                entered.SetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                return 1;
            }).AsTask();
            await entered.Task;

            await manager.RemoveAsync("stop");

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
            Assert.False(client.Connected);
            Assert.Equal(1, client.DisconnectCount);
        }

        [Fact]
        public async Task ConcurrentRequests_AfterFaultShareReconnect()
        {
            var client = new FakeClient();
            await using var manager = CreateManager(client);
            var options = Options("recover");
            options.MaxConcurrency = 3;
            await manager.AddAsync(options);
            var failed = await manager.ExecuteAsync("recover", (_, __) => new ValueTask<OperationResult>(new OperationResult
            {
                IsSuccess = false,
                FailureKind = OperationFailureKind.ConnectionClosed
            }));
            Assert.False(failed.IsSuccess);

            await Task.WhenAll(
                manager.ExecuteAsync("recover", (_, __) => new ValueTask<int>(1)).AsTask(),
                manager.ExecuteAsync("recover", (_, __) => new ValueTask<int>(2)).AsTask(),
                manager.ExecuteAsync("recover", (_, __) => new ValueTask<int>(3)).AsTask());

            Assert.Equal(2, client.ConnectCount);
            Assert.True(manager.TryGetSnapshot("recover", out var snapshot));
            Assert.Equal(ChannelState.Online, snapshot.State);
            Assert.Equal(1, snapshot.ReconnectCount);
        }

        [Fact]
        public async Task Restart_ReplacesStoppedRuntime()
        {
            var clients = new Queue<FakeClient>(new[] { new FakeClient(), new FakeClient() });
            await using var manager = new ChannelManager(_ => clients.Dequeue());
            await manager.AddAsync(Options("restart"));

            await manager.RestartAsync("restart", Options("restart"));
            var value = await manager.ExecuteAsync("restart", (_, __) => new ValueTask<int>(42));

            Assert.Equal(42, value);
            Assert.Empty(clients);
        }

        [Fact]
        public async Task WaitingForSharedReconnect_CanBeCancelledWithoutCancellingReconnect()
        {
            var client = new FakeClient();
            await using var manager = CreateManager(client);
            var options = Options("shared-cancel");
            options.MaxConcurrency = 2;
            await manager.AddAsync(options);
            await manager.ExecuteAsync("shared-cancel", (_, __) => new ValueTask<OperationResult>(new OperationResult
            {
                IsSuccess = false,
                FailureKind = OperationFailureKind.ConnectionClosed
            }));
            client.ConnectDelay = TimeSpan.FromMilliseconds(150);
            var survivor = manager.ExecuteAsync("shared-cancel", (_, __) => new ValueTask<int>(1)).AsTask();
            using var cancellation = new CancellationTokenSource(30);
            var cancelled = manager.ExecuteAsync("shared-cancel", (_, __) => new ValueTask<int>(2), cancellation.Token).AsTask();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            Assert.Equal(1, await survivor);
            Assert.Equal(2, client.ConnectCount);
        }

        [Fact]
        public async Task StateSubscriberException_DoesNotBreakChannel()
        {
            await using var manager = CreateManager(new FakeClient());
            manager.StateChanged += (_, __) => throw new InvalidOperationException("subscriber failure");

            await manager.AddAsync(Options("events"));
            var result = await manager.ExecuteAsync("events", (_, __) => new ValueTask<int>(7));

            Assert.Equal(7, result);
            Assert.True(manager.TryGetSnapshot("events", out var snapshot));
            Assert.Equal(ChannelState.Online, snapshot.State);
        }

        [Fact]
        public async Task ModbusTcpOptions_CreateWorkingSharedChannel()
        {
            var port = GetFreePort();
            using var server = new ModbusTcpServer("127.0.0.1", port) { SlaveId = 1 };
            server.DataStore.HoldingRegisters[0] = 0x1234;
            Assert.True((await server.StartAsync()).IsSuccess);
            await using var manager = new ChannelManager();
            try
            {
                await manager.AddAsync(new ModbusTcpChannelOptions("modbus", "127.0.0.1", port));

                var result = await manager.ExecuteAsync("modbus", (client, _) =>
                    new ValueTask<OperationResult<ushort>>(((ModbusTcpClient)client).ReadHoldingRegisterAsync(1, 0)));

                Assert.True(result.IsSuccess, result.Message);
                Assert.Equal((ushort)0x1234, result.ResultValue);
            }
            finally { await server.StopAsync(); }
        }

        [Fact]
        public async Task SiemensOptions_CreateWorkingChannel()
        {
            var port = GetFreePort();
            using var server = new S7TcpServer("127.0.0.1", port);
            server.SetSiemensVersion(SiemensVersion.S7_1200);
            server.SetRackSlot(0, 0);
            Assert.True((await server.StartAsync()).IsSuccess);
            await using var manager = new ChannelManager();
            try
            {
                await manager.AddAsync(new SiemensS7ChannelOptions("s7", "127.0.0.1", SiemensVersion.S7_1200, port));

                var result = await manager.ExecuteAsync("s7", (client, _) =>
                    ((SiemensClient)client).ReadAsync("DB1.DBW0", 1, DataTypeEnums.UInt16));

                Assert.True(result.IsSuccess, result.Message);
            }
            finally { await server.StopAsync(); }
        }

        [Fact]
        public async Task FinsOptions_CompleteHandshakeAndCreateChannel()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = Task.Run(async () =>
            {
                using var socket = await listener.AcceptTcpClientAsync();
                var stream = socket.GetStream();
                var request = new byte[20];
                await stream.ReadExactlyAsync(request);
                var response = new byte[24]
                {
                    0x46, 0x49, 0x4e, 0x53, 0, 0, 0, 0x10,
                    0, 0, 0, 1, 0, 0, 0, 0,
                    0, 0, 0, 1, 0, 0, 0, 2
                };
                await stream.WriteAsync(response);
                await stream.FlushAsync();
            });
            await using var manager = new ChannelManager();
            try
            {
                await manager.AddAsync(new FinsTcpChannelOptions("fins", "127.0.0.1", port));

                var nodes = await manager.ExecuteAsync("fins", (client, _) =>
                {
                    var fins = (FinsClient)client;
                    return new ValueTask<(byte Source, byte Destination)>((fins.SourceNodeAddress, fins.DestinationNodeAddress));
                });

                Assert.Equal((byte)1, nodes.Source);
                Assert.Equal((byte)2, nodes.Destination);
            }
            finally
            {
                await manager.RemoveAsync("fins");
                listener.Stop();
                await server;
            }
        }

        [Fact]
        public async Task FinsHandshake_PropagatesCancellation()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = listener.AcceptTcpClientAsync();
            using var client = new FinsClient("127.0.0.1", port) { ConnectTimeout = TimeSpan.FromSeconds(10) };
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            try
            {
                var connecting = client.ConnectAsync(cancellation.Token);
                using var socket = await server;
                var result = await connecting;
                Assert.False(result.IsSuccess);
                Assert.Equal(OperationFailureKind.Cancelled, result.FailureKind);
            }
            finally
            {
                await client.DisconnectAsync();
                listener.Stop();
            }
        }

        private static ChannelManager CreateManager(FakeClient client) => new ChannelManager(_ => client);

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static ModbusTcpChannelOptions Options(string id) => new ModbusTcpChannelOptions(id, "127.0.0.1")
        {
            OperationTimeout = TimeSpan.FromSeconds(2),
            ConnectTimeout = TimeSpan.FromSeconds(2)
        };

        private sealed class FakeClient : IProtocolClient
        {
            private int _connectCount;

            public bool Connected { get; private set; }
            public int ConnectCount => _connectCount;
            public int DisconnectCount { get; private set; }
            public TimeSpan ConnectDelay { get; set; } = TimeSpan.FromMilliseconds(20);

            public async Task<OperationResult> ConnectAsync(CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _connectCount);
                await Task.Delay(ConnectDelay, cancellationToken);
                Connected = true;
                return OperationResult.CreateSuccessResult();
            }

            public Task<OperationResult> DisconnectAsync()
            {
                DisconnectCount++;
                Connected = false;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }
        }
    }
}

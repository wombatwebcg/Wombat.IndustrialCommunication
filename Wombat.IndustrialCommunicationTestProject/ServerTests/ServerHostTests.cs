using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.Servers;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ServerTests
{
    public class ServerHostTests
    {
        [Fact]
        public async Task Start_Stop_And_Duplicate_Start_Are_Explicit()
        {
            await using var host = new ServerHost("modbus", new ModbusTcpServer("127.0.0.1", GetFreePort()));

            Assert.True((await host.StartAsync()).IsSuccess);
            Assert.True(host.IsRunning);
            Assert.False((await host.StartAsync()).IsSuccess);
            Assert.True((await host.StopAsync()).IsSuccess);
            Assert.False(host.IsRunning);
        }

        [Fact]
        public async Task Port_Conflict_Is_Reported()
        {
            int port = GetFreePort();
            await using var first = new ServerHost("first", new ModbusTcpServer("127.0.0.1", port));
            await using var second = new ServerHost("second", new ModbusTcpServer("127.0.0.1", port));

            Assert.True((await first.StartAsync()).IsSuccess);
            Assert.False((await second.StartAsync()).IsSuccess);
        }

        [Fact]
        public async Task Cancelled_Start_Does_Not_Listen()
        {
            await using var host = new ServerHost("cancelled", new ModbusTcpServer("127.0.0.1", GetFreePort()));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.StartAsync(cancellation.Token));
            Assert.False(host.IsRunning);
        }

        [Fact]
        public async Task Stop_Closes_All_Concurrent_Sessions()
        {
            int port = GetFreePort();
            var server = new ModbusTcpServer("127.0.0.1", port) { MaxConnections = 2 };
            await using var host = new ServerHost("sessions", server);
            Assert.True((await host.StartAsync()).IsSuccess);

            using var first = new TcpClient();
            using var second = new TcpClient();
            await first.ConnectAsync(IPAddress.Loopback, port);
            await second.ConnectAsync(IPAddress.Loopback, port);

            Assert.True((await host.StopAsync()).IsSuccess);
            Assert.Equal(0, await first.GetStream().ReadAsync(new byte[1], 0, 1));
            Assert.Equal(0, await second.GetStream().ReadAsync(new byte[1], 0, 1));
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}

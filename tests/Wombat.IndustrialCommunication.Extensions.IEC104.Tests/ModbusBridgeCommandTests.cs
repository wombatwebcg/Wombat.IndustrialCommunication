using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;
using Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Tests
{
    public class ModbusBridgeCommandTests
    {
        private const byte Station = 1;
        private const int CommandIndex = 0;

        private readonly ConcurrentQueue<Frame> _frames = new ConcurrentQueue<Frame>();

        private struct Frame
        {
            public bool IsSelect;
            public bool? On;
        }

        [Fact]
        public async Task SelectThenConfirmShouldComplete()
        {
            await using var harness = await SlaveHarness.CreateAsync(requireSelectBeforeOperate: true);
            await harness.Master.InterrogateAsync();
            harness.Slave.CommandRequested += (_, e) => _frames.Enqueue(new Frame { IsSelect = e.IsSelect, On = e.SinglePointValue });

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var address = bridge.Map.CommandPoints[CommandIndex].Address;
            await WriteCommandAsync(client, address, BridgeCommandValue.RequestOn);
            await WaitForStateAsync(client, bridge, BridgeCommandState.PendingOn);

            Assert.Equal(0, await ReadCommandRegisterAsync(client, address));

            await WriteCommandAsync(client, address, BridgeCommandValue.ConfirmOn);
            await WaitForStateAsync(client, bridge, BridgeCommandState.Succeeded);

            var frames = _frames.ToArray();
            Assert.Equal(2, frames.Length);
            Assert.True(frames[0].IsSelect);
            Assert.True(frames[0].On);
            Assert.False(frames[1].IsSelect);
            Assert.True(frames[1].On);
        }

        [Fact]
        public async Task OverrideShouldReselectWithoutExecuting()
        {
            await using var harness = await SlaveHarness.CreateAsync(requireSelectBeforeOperate: true);
            await harness.Master.InterrogateAsync();
            harness.Slave.CommandRequested += (_, e) => _frames.Enqueue(new Frame { IsSelect = e.IsSelect, On = e.SinglePointValue });

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var address = bridge.Map.CommandPoints[CommandIndex].Address;
            await WriteCommandAsync(client, address, BridgeCommandValue.RequestOn);
            await WaitForStateAsync(client, bridge, BridgeCommandState.PendingOn);

            await WriteCommandAsync(client, address, BridgeCommandValue.RequestOff);
            await WaitForStateAsync(client, bridge, BridgeCommandState.PendingOff, 5000);

            await Task.Delay(200);

            var frames = _frames.ToArray();
            Assert.True(frames.Length >= 2, "覆盖请求应重新发选择帧");
            foreach (var frame in frames) Assert.True(frame.IsSelect, "覆盖只重新选择，不能产生执行帧");
            Assert.Equal(false, frames[frames.Length - 1].On);
        }

        [Fact]
        public async Task ConfirmMismatchShouldBeIgnored()
        {
            await using var harness = await SlaveHarness.CreateAsync(requireSelectBeforeOperate: true);
            await harness.Master.InterrogateAsync();
            harness.Slave.CommandRequested += (_, e) => _frames.Enqueue(new Frame { IsSelect = e.IsSelect, On = e.SinglePointValue });

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var address = bridge.Map.CommandPoints[CommandIndex].Address;
            await WriteCommandAsync(client, address, BridgeCommandValue.RequestOn);
            await WaitForStateAsync(client, bridge, BridgeCommandState.PendingOn);

            await WriteCommandAsync(client, address, BridgeCommandValue.ConfirmOff);
            await Task.Delay(300);

            var status = await ReadStatusAsync(client, bridge);
            Assert.Equal((ushort)BridgeCommandState.PendingOn, status[0]);
            Assert.Equal((ushort)BridgeCommandReason.ConfirmMismatch, status[1]);
            Assert.Single(_frames);
        }

        [Fact]
        public async Task PendingConfirmShouldExpire()
        {
            await using var harness = await SlaveHarness.CreateAsync(requireSelectBeforeOperate: true);
            await harness.Master.InterrogateAsync();

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port, options => options.ConfirmTimeout = TimeSpan.FromMilliseconds(500)));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var address = bridge.Map.CommandPoints[CommandIndex].Address;
            await WriteCommandAsync(client, address, BridgeCommandValue.RequestOff);
            await WaitForStateAsync(client, bridge, BridgeCommandState.PendingOff);
            await WaitForStateAsync(client, bridge, BridgeCommandState.ConfirmTimeout, 5000);

            var status = await ReadStatusAsync(client, bridge);
            Assert.Equal((ushort)BridgeCommandReason.Timeout, status[1]);
        }

        [Fact]
        public async Task DisabledChannelShouldReportDisabled()
        {
            await using var harness = await SlaveHarness.CreateAsync(requireSelectBeforeOperate: true);
            await harness.Master.InterrogateAsync();
            harness.Slave.CommandRequested += (_, e) => _frames.Enqueue(new Frame { IsSelect = e.IsSelect, On = e.SinglePointValue });

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port, options => options.EnableCommands = false));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var address = bridge.Map.CommandPoints[CommandIndex].Address;
            await WriteCommandAsync(client, address, BridgeCommandValue.RequestOn);

            await WaitForStateAsync(client, bridge, BridgeCommandState.Disabled);

            var status = await ReadStatusAsync(client, bridge);
            Assert.Equal((ushort)BridgeCommandReason.Disabled, status[1]);
            Assert.Equal(0, await ReadCommandRegisterAsync(client, address));
            Assert.Empty(_frames);
        }

        [Fact]
        public async Task IoaOutsideWhitelistShouldBeRejected()
        {
            await using var harness = await SlaveHarness.CreateAsync(requireSelectBeforeOperate: true);
            await harness.Master.InterrogateAsync();
            harness.Slave.CommandRequested += (_, e) => _frames.Enqueue(new Frame { IsSelect = e.IsSelect, On = e.SinglePointValue });

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port, options => options.AllowedCommandIoa = new[] { 1 }));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var address = bridge.Map.CommandPoints[CommandIndex].Address;
            await WriteCommandAsync(client, address, BridgeCommandValue.RequestOn);

            await WaitForStateAsync(client, bridge, BridgeCommandState.Rejected);

            var status = await ReadStatusAsync(client, bridge);
            Assert.Equal((ushort)BridgeCommandReason.IoaNotAllowed, status[1]);
            Assert.Empty(_frames);
        }

        [Fact]
        public async Task DirectModeShouldSkipSelectFrame()
        {
            await using var harness = await SlaveHarness.CreateAsync();
            await harness.Master.InterrogateAsync();
            harness.Slave.CommandRequested += (_, e) => _frames.Enqueue(new Frame { IsSelect = e.IsSelect, On = e.SinglePointValue });

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port, options => options.CommandMode = Iec104CommandMode.Direct));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var address = bridge.Map.CommandPoints[CommandIndex].Address;
            await WriteCommandAsync(client, address, BridgeCommandValue.RequestOn);
            await WaitForStateAsync(client, bridge, BridgeCommandState.PendingOn);

            Assert.Empty(_frames);

            await WriteCommandAsync(client, address, BridgeCommandValue.ConfirmOn);
            await WaitForStateAsync(client, bridge, BridgeCommandState.Succeeded);

            var frames = _frames.ToArray();
            Assert.Single(frames);
            Assert.False(frames[0].IsSelect);
            Assert.True(frames[0].On);
        }

        private static ModbusBridgeOptions BuildOptions(int port, Action<ModbusBridgeOptions> configure = null)
        {
            var options = new ModbusBridgeOptions
            {
                BindAddress = "127.0.0.1",
                Port = port,
                UnitId = Station,
                DiscreteBase = 0,
                TelemetryBase = 0,
                CommandBase = 0,
                HealthBase = 1000,
                EnableCommands = true,
                CommandMode = Iec104CommandMode.SelectAndExecute,
            };

            configure?.Invoke(options);
            return options;
        }

        private static Task WriteCommandAsync(ModbusTcpClient client, ushort address, ushort value)
        {
            return client.WriteHoldingRegisterAsync(Station, address, value);
        }

        private static async Task<ushort> ReadCommandRegisterAsync(ModbusTcpClient client, ushort address)
        {
            var result = await client.ReadHoldingRegisterAsync(Station, address);
            Assert.True(result.IsSuccess, result.Message);
            return result.ResultValue;
        }

        private static async Task<ushort[]> ReadStatusAsync(ModbusTcpClient client, Iec104ModbusBridge bridge)
        {
            var mapping = bridge.Map.CommandPoints[CommandIndex];
            var result = await client.ReadHoldingRegistersAsync(Station, (ushort)(mapping.Address + 1), 2);
            Assert.True(result.IsSuccess, result.Message);
            return result.ResultValue;
        }

        private static async Task WaitForStateAsync(
            ModbusTcpClient client,
            Iec104ModbusBridge bridge,
            BridgeCommandState expected,
            int timeoutMs = 3000)
        {
            var stopwatch = Stopwatch.StartNew();
            ushort[] status = null;

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                status = await ReadStatusAsync(client, bridge);
                if (status[0] == (ushort)expected) return;
                await Task.Delay(10);
            }

            Assert.True(false, string.Format(
                "等待状态 {0} 超时，当前状态 {1}，原因 {2}", expected, status?[0], status?[1]));
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}

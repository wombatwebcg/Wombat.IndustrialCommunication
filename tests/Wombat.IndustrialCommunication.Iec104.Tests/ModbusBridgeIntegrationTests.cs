using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Iec104.ModbusBridge;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunication.Iec104.Tests
{
    public class ModbusBridgeIntegrationTests
    {
        private const byte Station = 1;

        [Fact]
        public async Task MirrorShouldMatchIec104Cache()
        {
            await using var harness = await SlaveHarness.CreateAsync();

            for (var i = 0; i < SlaveHarness.TelemetryCount; i++)
            {
                harness.Slave.SetValue(SlaveHarness.TelemetryBase + i, i * 1.25d);
            }

            for (var i = 0; i < SlaveHarness.DiscreteCount; i++)
            {
                harness.Slave.SetSinglePoint(SlaveHarness.DiscreteBase + i, i % 3 == 0);
            }

            await harness.Master.InterrogateAsync();

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            var connected = await client.ConnectAsync();
            Assert.True(connected.IsSuccess, connected.Message);

            var discreteStart = (ushort)bridge.Map.DiscretePoints[0].Address;
            var discretes = await client.ReadDiscreteInputsAsync(Station, discreteStart, SlaveHarness.DiscreteCount);
            Assert.True(discretes.IsSuccess, discretes.Message);

            for (var i = 0; i < SlaveHarness.DiscreteCount; i++)
            {
                var ioa = SlaveHarness.DiscreteBase + i;
                Assert.True(harness.Master.TryGetValue(ioa, out var cached), "缓存缺少 IOA " + ioa);
                Assert.Equal(cached.Boolean, discretes.ResultValue[i]);
            }

            var registers = await ReadAllInputRegistersAsync(client, (ushort)bridge.Map.TelemetryPoints[0].Address, 1636);

            for (var i = 0; i < SlaveHarness.TelemetryCount; i++)
            {
                var ioa = SlaveHarness.TelemetryBase + i;
                Assert.True(harness.Master.TryGetValue(ioa, out var cached), "缓存缺少 IOA " + ioa);

                var value = ModbusFloatCodec.Decode(registers[i * 2], registers[i * 2 + 1], ModbusFloatWordOrder.Abcd);
                Assert.Equal((float)cached.Numeric, value);
            }

            var health = await ReadHealthAsync(client, bridge);
            Assert.Equal(2, health[BridgeHealthRegister.LinkState]);
            Assert.Equal(818, health[BridgeHealthRegister.TelemetryCount]);
            Assert.Equal(298, health[BridgeHealthRegister.DiscreteCount]);
            Assert.Equal(bridge.Map.MapVersion, health[BridgeHealthRegister.MapVersion]);
            Assert.Equal(1, health[BridgeHealthRegister.Running]);
        }

        [Fact]
        public async Task ChangeShouldBecomeVisibleWithinOneSecond()
        {
            await using var harness = await SlaveHarness.CreateAsync();
            await harness.Master.InterrogateAsync();

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            const int index = 5;
            var ioa = SlaveHarness.TelemetryBase + index;
            var address = (ushort)bridge.Map.TelemetryPoints[index].Address;

            Assert.True(harness.Slave.SetValue(ioa, 42.5d), "从站拒绝了写入");

            var stopwatch = Stopwatch.StartNew();
            float read = 0f;
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(3))
            {
                var batch = await client.ReadInputRegistersAsync(Station, address, 2);
                Assert.True(batch.IsSuccess, batch.Message);
                read = ModbusFloatCodec.Decode(batch.ResultValue[0], batch.ResultValue[1], ModbusFloatWordOrder.Abcd);
                if (read == 42.5f) break;
                await Task.Delay(10);
            }

            Assert.Equal(42.5f, read);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), "刷新耗时 " + stopwatch.ElapsedMilliseconds + "ms");
        }

        [Fact]
        public async Task InitialImageShouldBeZeroBeforeInterrogation()
        {
            await using var harness = await SlaveHarness.CreateAsync();

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var registers = await ReadAllInputRegistersAsync(client, 0, 1636);
            foreach (var value in registers) Assert.Equal(0, value);

            var discretes = await client.ReadDiscreteInputsAsync(
                Station,
                (ushort)bridge.Map.DiscretePoints[0].Address,
                SlaveHarness.DiscreteCount);

            Assert.True(discretes.IsSuccess, discretes.Message);
            foreach (var value in discretes.ResultValue) Assert.False(value);
        }

        [Fact]
        public async Task HealthShouldFollowLinkLoss()
        {
            await using var harness = await SlaveHarness.CreateAsync();

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port));

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var health = await ReadHealthAsync(client, bridge);
            Assert.Equal(2, health[BridgeHealthRegister.LinkState]);

            await harness.Slave.StopAsync();

            var stopwatch = Stopwatch.StartNew();
            var linkState = health[BridgeHealthRegister.LinkState];
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                health = await ReadHealthAsync(client, bridge);
                linkState = health[BridgeHealthRegister.LinkState];
                if (linkState == 0) break;
                await Task.Delay(100);
            }

            Assert.Equal(0, linkState);

            await Task.Delay(1500);
            health = await ReadHealthAsync(client, bridge);
            Assert.True(health[BridgeHealthRegister.TelemetryAge] >= 1,
                "遥测刷新秒数没有增长：" + health[BridgeHealthRegister.TelemetryAge]);
            Assert.Equal(1, health[BridgeHealthRegister.Running]);
        }

        private static ModbusBridgeOptions BuildOptions(int port)
        {
            return new ModbusBridgeOptions
            {
                BindAddress = "127.0.0.1",
                Port = port,
                UnitId = Station,
                DiscreteBase = 0,
                TelemetryBase = 0,
                CommandBase = 0,
                HealthBase = 1000,
            };
        }

        private static async Task<ushort[]> ReadAllInputRegistersAsync(ModbusTcpClient client, ushort start, int total)
        {
            var values = new List<ushort>(total);
            var address = start;
            var remaining = total;

            while (remaining > 0)
            {
                var count = Math.Min(125, remaining);
                var batch = await client.ReadInputRegistersAsync(Station, (ushort)address, (ushort)count);
                Assert.True(batch.IsSuccess, batch.Message);
                values.AddRange(batch.ResultValue);
                address = (ushort)(address + count);
                remaining -= count;
            }

            return values.ToArray();
        }

        private static async Task<ushort[]> ReadHealthAsync(ModbusTcpClient client, Iec104ModbusBridge bridge)
        {
            var result = await client.ReadHoldingRegistersAsync(
                Station,
                (ushort)bridge.Map.HealthBase,
                ModbusRegisterMap.HealthRegisterCount);

            Assert.True(result.IsSuccess, result.Message);
            return result.ResultValue;
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

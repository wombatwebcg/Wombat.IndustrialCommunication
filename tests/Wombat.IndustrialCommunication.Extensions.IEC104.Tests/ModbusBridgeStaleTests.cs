using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Tests
{
    public class ModbusBridgeStaleTests
    {
        private const byte Station = 1;

        [Fact]
        public async Task LastValueModeShouldKeepServingAfterLinkLoss()
        {
            await using var harness = await SlaveHarness.CreateAsync();
            await harness.Master.InterrogateAsync();
            harness.Slave.SetValue(SlaveHarness.TelemetryBase, 42.5d);
            await Task.Delay(200);

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port, ModbusStaleReadMode.LastValue));

            await bridge.StartAsync();
            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var address = (ushort)bridge.Map.TelemetryPoints[0].Address;
            var before = await client.ReadInputRegistersAsync(Station, address, 2);
            Assert.True(before.IsSuccess, before.Message);

            await harness.Slave.StopAsync();
            await Task.Delay(1500);

            var after = await client.ReadInputRegistersAsync(Station, address, 2);
            Assert.True(after.IsSuccess, after.Message);
            Assert.Equal(before.ResultValue, after.ResultValue);
        }

        [Fact]
        public async Task ExceptionModeShouldFailWhenLinkIsStale()
        {
            await using var harness = await SlaveHarness.CreateAsync();
            await harness.Master.InterrogateAsync();

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                BuildOptions(port, ModbusStaleReadMode.Exception));

            await bridge.StartAsync();
            using var client = new ModbusTcpClient("127.0.0.1", port);
            await client.ConnectAsync();

            var address = (ushort)bridge.Map.TelemetryPoints[0].Address;
            var initial = await client.ReadInputRegistersAsync(Station, address, 2);
            Assert.True(initial.IsSuccess, initial.Message);

            await harness.Slave.StopAsync();

            var stopwatch = Stopwatch.StartNew();
            var failed = false;
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                var result = await client.ReadInputRegistersAsync(Station, address, 2);
                if (!result.IsSuccess)
                {
                    failed = true;
                    break;
                }

                await Task.Delay(50);
            }

            Assert.True(failed, "断链超时后读请求应返回异常");

            var health = await client.ReadHoldingRegistersAsync(Station, 1000, ModbusRegisterMap.HealthRegisterCount);
            Assert.True(health.IsSuccess, "健康区必须始终可读：" + health.Message);
        }

        private static ModbusBridgeOptions BuildOptions(int port, ModbusStaleReadMode mode)
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
                StaleReadMode = mode,
                StaleTimeout = TimeSpan.FromMilliseconds(500),
            };
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

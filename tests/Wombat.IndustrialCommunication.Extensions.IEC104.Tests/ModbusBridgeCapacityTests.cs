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
    public class ModbusBridgeCapacityTests
    {
        private const byte Station = 1;

        /// <summary>
        /// 3 个客户端同时按 1 秒周期轮询 1116 点。默认 5 秒便于 CI，
        /// 现场容量验证设置环境变量 BRIDGE_CAPACITY_SECONDS=600 跑满 10 分钟。
        /// </summary>
        [Fact]
        [Trait("Category", "Capacity")]
        public async Task ThreeClientsShouldPollWithoutErrors()
        {
            var seconds = 5;
            var configured = Environment.GetEnvironmentVariable("BRIDGE_CAPACITY_SECONDS");
            if (int.TryParse(configured, out var parsed) && parsed > 0) seconds = parsed;

            await using var harness = await SlaveHarness.CreateAsync();
            await harness.Master.InterrogateAsync();

            var port = GetFreePort();
            await using var bridge = new Iec104ModbusBridge(
                new Iec104MasterValueSource(harness.Master),
                new ModbusBridgeOptions
                {
                    BindAddress = "127.0.0.1",
                    Port = port,
                    UnitId = Station,
                    DiscreteBase = 0,
                    TelemetryBase = 0,
                    CommandBase = 0,
                    HealthBase = 1000,
                });

            var started = await bridge.StartAsync();
            Assert.True(started.IsSuccess, started.Message);

            var duration = TimeSpan.FromSeconds(seconds);
            var tasks = new[]
            {
                PollAsync(port, duration),
                PollAsync(port, duration),
                PollAsync(port, duration),
            };

            var failures = await Task.WhenAll(tasks);

            Assert.All(failures, count => Assert.Equal(0, count));
        }

        private static async Task<int> PollAsync(int port, TimeSpan duration)
        {
            using var client = new ModbusTcpClient("127.0.0.1", port);
            var connected = await client.ConnectAsync();
            if (!connected.IsSuccess) return -1;

            var failures = 0;
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < duration)
            {
                var discretes = await client.ReadDiscreteInputsAsync(Station, 1000, 298);
                if (!discretes.IsSuccess) failures++;

                for (var start = 0; start < 1636; start += 125)
                {
                    var count = Math.Min(125, 1636 - start);
                    var batch = await client.ReadInputRegistersAsync(Station, (ushort)start, (ushort)count);
                    if (!batch.IsSuccess) failures++;
                }

                var health = await client.ReadHoldingRegistersAsync(Station, 1000, ModbusRegisterMap.HealthRegisterCount);
                if (!health.IsSuccess || health.ResultValue[BridgeHealthRegister.Running] != 1) failures++;

                await Task.Delay(1000);
            }

            return failures;
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

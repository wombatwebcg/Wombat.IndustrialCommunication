using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTest.ModbusTests
{
    [CollectionDefinition("ModbusTcpClientPoolRead", DisableParallelization = true)]
    public sealed class ModbusTcpClientPoolReadCollection
    {
    }

    [Collection("ModbusTcpClientPoolRead")]
    public class ModbusTcpClientPoolReadTest
    {
        private const string TestIp = "127.0.0.1";
        private const int TestPort = 9001;
        private const string DeviceId = "modbus-tcp-pool-read";
        private readonly ITestOutputHelper _output;

        public ModbusTcpClientPoolReadTest(ITestOutputHelper output = null)
        {
            _output = output;
        }

        [Fact]
        public async Task Test_ReadRange_Stations21To29_Register40001_UsingConnectionPool()
        {
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = false,
                MaxRetryCount = 1,
                RetryBackoff = TimeSpan.FromMilliseconds(50),
                LeaseTimeout = TimeSpan.FromSeconds(10),
                IdleTimeout = TimeSpan.FromMinutes(5)
            };

            using (var pool = new DeviceClientPool(options, new DefaultPooledDeviceClientConnectionFactory()))
            {
                var identity = new ConnectionIdentity
                {
                    DeviceId = DeviceId,
                    ProtocolType = "ModbusTcp",
                    Endpoint = $"{TestIp}:{TestPort}"
                };

                var descriptor = new ResourceDescriptor
                {
                    Identity = identity,
                    DeviceConnectionType = DeviceConnectionType.ModbusTcp,
                    ConnectionParameters = new ModbusTcpClientConnectionParameters
                    {
                        Ip = TestIp,
                        Port = TestPort,
                        ConnectTimeoutMilliseconds = 3000,
                        ReceiveTimeoutMilliseconds = 3000,
                        SendTimeoutMilliseconds = 3000,
                        BatchReadStationIntervalMilliseconds = 100
                    }
                };

                var registerResult = pool.Register(descriptor);
                Assert.True(registerResult.IsSuccess, $"注册 ModbusTcp 连接失败: {registerResult.Message}");
                _output?.WriteLine($"注册 ModbusTcp 连接成功: {TestIp}:{TestPort}");

                var readResult = await pool.ExecuteAsync<Dictionary<string, (DataTypeEnums, object)>>(
                    identity,
                    async client =>
                    {
                        var batchReadData = new Dictionary<string, DataTypeEnums>();
                        for (byte station = 21; station <= 29; station++)
                        {
                            batchReadData[$"{station};40001"] = DataTypeEnums.UInt16;
                        }

                        return await client.BatchReadAsync(batchReadData).ConfigureAwait(false);
                    }).ConfigureAwait(false);

                Assert.True(readResult.IsSuccess, $"批量读取失败: {readResult.Message}");
                _output?.WriteLine($"批量读取成功，共读取 {readResult.ResultValue.Count} 个地址");

                foreach (var kvp in readResult.ResultValue)
                {
                    _output?.WriteLine($"  地址: {kvp.Key}, 数据类型: {kvp.Value.Item1}, 值: {kvp.Value.Item2}");
                }
            }
        }
    }
}

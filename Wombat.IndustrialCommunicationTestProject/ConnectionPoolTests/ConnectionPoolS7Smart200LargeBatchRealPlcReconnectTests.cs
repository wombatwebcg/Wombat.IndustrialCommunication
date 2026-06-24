using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.PLC;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTest.ConnectionPoolTests
{
    [CollectionDefinition("ConnectionPool S7Smart200 RealPlc", DisableParallelization = true)]
    public sealed class ConnectionPoolS7Smart200RealPlcCollection
    {
    }

    [Collection("ConnectionPool S7Smart200 RealPlc")]
    public class ConnectionPoolS7Smart200LargeBatchRealPlcReconnectTests
    {
        private const string PlcIp = "192.168.10.100";
        private const int PlcPort = 102;
        private const SiemensVersion PlcVersion = SiemensVersion.S7_200Smart;
        private const int ConnectTimeoutSeconds = 5;
        private const int OperationTimeoutSeconds = 5;
        private const int WriteReadbackDelayMilliseconds = 150;
        private const int ReconnectPollIntervalSeconds = 2;

        private readonly ITestOutputHelper _output;

        public ConnectionPoolS7Smart200LargeBatchRealPlcReconnectTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ConnectionPool_Smart200_BatchRead_ShouldReconnectAndRoundTrip_ContinuousRandomAndMixedAddresses_Over200()
        {
            await ExecuteReadWriteRoundTripWithDedicatedPoolAsync(
                "连接池连续批量读回写校验 - 256 个连续字节地址",
                BuildContinuousByteScenario(baseByteAddress: 1000, byteCount: 256, seed: 2026062401)).ConfigureAwait(false);

            await ExecuteReadWriteRoundTripWithDedicatedPoolAsync(
                "连接池随机批量读回写校验 - 220 个离散字节地址",
                BuildSparseByteScenario(baseByteAddress: 2000, addressCount: 220, maxOffset: 900, seed: 2026062402)).ConfigureAwait(false);

            await ExecuteReadWriteRoundTripWithDedicatedPoolAsync(
                "连接池混合批量读回写校验 - Bool/Byte/Word/DWord 共 248 个地址",
                BuildMixedScenario(baseByteAddress: 3000, seed: 2026062403)).ConfigureAwait(false);
        }

        [Fact]
        public async Task ConnectionPool_Smart200_BatchWrite_ShouldReconnectAndRoundTrip_RandomAndMixedAddresses_Over200()
        {
            await ExecuteReadWriteRoundTripWithDedicatedPoolAsync(
                "连接池随机批量写入回读校验 - 240 个离散字节地址",
                BuildSparseByteScenario(baseByteAddress: 4200, addressCount: 240, maxOffset: 1200, seed: 2026062404)).ConfigureAwait(false);

            await ExecuteReadWriteRoundTripWithDedicatedPoolAsync(
                "连接池混合批量写入回读校验 - Bool/Byte/Word/DWord 共 248 个地址",
                BuildMixedScenario(baseByteAddress: 5000, seed: 2026062405)).ConfigureAwait(false);
        }

        private async Task ExecuteReadWriteRoundTripWithDedicatedPoolAsync(
            string scenarioName,
            Dictionary<string, (DataTypeEnums, object)> expectedValues)
        {
            using (var pool = CreatePool())
            {
                var registerResult = pool.Register(CreateDescriptor());
                Assert.True(registerResult.IsSuccess, "注册 SiemensS7 连接池失败: " + registerResult.Message);

                await WaitUntilPoolReadyAsync(pool, $"开始场景 {scenarioName}").ConfigureAwait(false);
                await ExecuteRoundTripScenarioWithReconnectAsync(pool, scenarioName, expectedValues).ConfigureAwait(false);
            }
        }

        private async Task WaitUntilPoolReadyAsync(DeviceClientPool pool, string reason)
        {
            int attempt = 0;
            while (true)
            {
                attempt++;
                Log($"等待连接池接通 PLC，第 {attempt} 次尝试: {PlcIp}:{PlcPort}, 机型={PlcVersion}, 原因={reason}");
                var readyResult = await pool.ExecuteAsync(CreateIdentity(), _ =>
                    Task.FromResult(OperationResult.CreateSuccessResult("连接池探活成功"))).ConfigureAwait(false);
                DumpOperationResult($"Pool Connect Attempt {attempt}", readyResult);
                if (readyResult.IsSuccess)
                {
                    Log($"连接池已可用，连接尝试次数: {attempt}");
                    return;
                }

                Log($"连接池尚未可用，{ReconnectPollIntervalSeconds} 秒后继续等待。错误: {readyResult.Message}");
                await Task.Delay(TimeSpan.FromSeconds(ReconnectPollIntervalSeconds)).ConfigureAwait(false);
            }
        }

        private async Task ExecuteRoundTripScenarioWithReconnectAsync(
            DeviceClientPool pool,
            string scenarioName,
            Dictionary<string, (DataTypeEnums, object)> expectedValues)
        {
            while (true)
            {
                try
                {
                    await WaitUntilPoolReadyAsync(pool, $"场景 {scenarioName} 执行前探活").ConfigureAwait(false);
                    await ExecuteRoundTripScenarioAsync(pool, scenarioName, expectedValues).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    Log($"连接池场景执行中断，准备等待重连后重试: {scenarioName}, 错误={ex.Message}");
                    var forceReconnect = pool.ForceReconnect(CreateIdentity(), $"场景失败后强制重连: {scenarioName}");
                    DumpOperationResult($"ForceReconnect - {scenarioName}", forceReconnect);
                    await WaitUntilPoolReadyAsync(pool, $"场景 {scenarioName} 执行失败后重连").ConfigureAwait(false);
                }
            }
        }

        private async Task ExecuteRoundTripScenarioAsync(
            DeviceClientPool pool,
            string scenarioName,
            Dictionary<string, (DataTypeEnums, object)> expectedValues)
        {
            Log($"开始连接池场景: {scenarioName}");
            Log($"地址数量: {expectedValues.Count}");
            Log($"示例地址: {string.Join(", ", expectedValues.Keys.Take(8))}");

            var batchWriteResult = await pool.ExecuteAsync(
                CreateIdentity(),
                async client => await client.BatchWriteAsync(expectedValues).ConfigureAwait(false)).ConfigureAwait(false);
            DumpOperationResult($"{scenarioName} - Pool BatchWrite", batchWriteResult);
            if (!batchWriteResult.IsSuccess)
            {
                throw new InvalidOperationException($"场景[{scenarioName}]连接池批量写入失败: {batchWriteResult.Message}");
            }

            Log($"连接池批量写入完成: {batchWriteResult.Message}");
            await Task.Delay(WriteReadbackDelayMilliseconds).ConfigureAwait(false);

            var readRequest = expectedValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Item1);
            var batchReadResult = await pool.ExecuteAsync<Dictionary<string, (DataTypeEnums, object)>>(
                CreateIdentity(),
                async client => await client.BatchReadAsync(readRequest).ConfigureAwait(false)).ConfigureAwait(false);
            DumpOperationResult($"{scenarioName} - Pool BatchRead", batchReadResult);
            if (!batchReadResult.IsSuccess)
            {
                throw new InvalidOperationException($"场景[{scenarioName}]连接池批量读取失败: {batchReadResult.Message}");
            }

            Log($"连接池批量读取完成: {batchReadResult.Message}");
            Assert.Equal(expectedValues.Count, batchReadResult.ResultValue.Count);
            AssertBatchValuesEqual(scenarioName, expectedValues, batchReadResult.ResultValue);
            Log($"连接池场景通过: {scenarioName}");
        }

        private static DeviceClientPool CreatePool()
        {
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = false,
                MaxRetryCount = 1,
                RetryBackoff = TimeSpan.FromMilliseconds(200),
                LeaseTimeout = TimeSpan.FromSeconds(20),
                IdleTimeout = TimeSpan.FromMinutes(5)
            };

            return new DeviceClientPool(options, new DefaultPooledDeviceClientConnectionFactory());
        }

        private static ResourceDescriptor CreateDescriptor()
        {
            return new ResourceDescriptor
            {
                Identity = CreateIdentity(),
                ResourceRole = ResourceRole.Client,
                DeviceConnectionType = DeviceConnectionType.SiemensS7,
                ConnectionParameters = new SiemensS7ClientConnectionParameters
                {
                    Ip = PlcIp,
                    Port = PlcPort,
                    SiemensVersion = PlcVersion,
                    Slot = 0,
                    Rack = 0,
                    ConnectTimeoutMilliseconds = ConnectTimeoutSeconds * 1000,
                    ReceiveTimeoutMilliseconds = OperationTimeoutSeconds * 1000,
                    SendTimeoutMilliseconds = OperationTimeoutSeconds * 1000
                }
            };
        }

        private static ConnectionIdentity CreateIdentity()
        {
            return new ConnectionIdentity
            {
                DeviceId = "siemens-s7-smart200-realplc-pool",
                ProtocolType = "SiemensS7",
                Endpoint = PlcIp + ":" + PlcPort
            };
        }

        private void AssertBatchValuesEqual(
            string scenarioName,
            Dictionary<string, (DataTypeEnums, object)> expectedValues,
            Dictionary<string, (DataTypeEnums, object)> actualValues)
        {
            foreach (var expected in expectedValues)
            {
                Assert.True(actualValues.ContainsKey(expected.Key), $"场景[{scenarioName}]缺少地址: {expected.Key}");

                var actual = actualValues[expected.Key];
                Assert.Equal(expected.Value.Item1, actual.Item1);
                Assert.NotNull(actual.Item2);

                switch (expected.Value.Item1)
                {
                    case DataTypeEnums.Bool:
                        Assert.Equal((bool)expected.Value.Item2, Assert.IsType<bool>(actual.Item2));
                        break;
                    case DataTypeEnums.Byte:
                        Assert.Equal((byte)expected.Value.Item2, Assert.IsType<byte>(actual.Item2));
                        break;
                    case DataTypeEnums.Int16:
                        Assert.Equal((short)expected.Value.Item2, Assert.IsType<short>(actual.Item2));
                        break;
                    case DataTypeEnums.Int32:
                        Assert.Equal((int)expected.Value.Item2, Assert.IsType<int>(actual.Item2));
                        break;
                    default:
                        Assert.True(false, $"场景[{scenarioName}]未处理的数据类型: {expected.Value.Item1}");
                        break;
                }
            }
        }

        private static Dictionary<string, (DataTypeEnums, object)> BuildContinuousByteScenario(int baseByteAddress, int byteCount, int seed)
        {
            var random = new Random(seed);
            var result = new Dictionary<string, (DataTypeEnums, object)>(byteCount);
            for (int i = 0; i < byteCount; i++)
            {
                result[$"VB{baseByteAddress + i}"] = (DataTypeEnums.Byte, (object)(byte)random.Next(0, 256));
            }

            return result;
        }

        private static Dictionary<string, (DataTypeEnums, object)> BuildSparseByteScenario(
            int baseByteAddress,
            int addressCount,
            int maxOffset,
            int seed)
        {
            var random = new Random(seed);
            var offsets = Enumerable.Range(0, maxOffset)
                .OrderBy(_ => random.Next())
                .Take(addressCount)
                .OrderBy(offset => offset)
                .ToArray();

            var result = new Dictionary<string, (DataTypeEnums, object)>(addressCount);
            foreach (var offset in offsets)
            {
                result[$"VB{baseByteAddress + offset}"] = (DataTypeEnums.Byte, (object)(byte)random.Next(0, 256));
            }

            return result;
        }

        private static Dictionary<string, (DataTypeEnums, object)> BuildMixedScenario(int baseByteAddress, int seed)
        {
            var random = new Random(seed);
            var result = new Dictionary<string, (DataTypeEnums, object)>(248);

            for (int i = 0; i < 96; i++)
            {
                int byteOffset = i / 8;
                int bitOffset = i % 8;
                result[$"V{baseByteAddress + byteOffset}.{bitOffset}"] = (DataTypeEnums.Bool, (object)(random.Next(0, 2) == 1));
            }

            for (int i = 0; i < 64; i++)
            {
                result[$"VB{baseByteAddress + 200 + i}"] = (DataTypeEnums.Byte, (object)(byte)random.Next(0, 256));
            }

            for (int i = 0; i < 56; i++)
            {
                result[$"VW{baseByteAddress + 400 + i * 2}"] = (DataTypeEnums.Int16, (object)(short)random.Next(short.MinValue, short.MaxValue));
            }

            for (int i = 0; i < 32; i++)
            {
                result[$"VD{baseByteAddress + 700 + i * 4}"] = (DataTypeEnums.Int32, random.Next(int.MinValue, int.MaxValue));
            }

            return result;
        }

        private void Log(string message)
        {
            _output?.WriteLine(message);
        }

        private void DumpOperationResult(string title, OperationResult result)
        {
            if (result == null)
            {
                Log($"{title}: result is null");
                return;
            }

            Log($"{title}: IsSuccess={result.IsSuccess}, Message={result.Message}");

            if (result.OperationInfo != null && result.OperationInfo.Count > 0)
            {
                foreach (var info in result.OperationInfo.Take(10))
                {
                    Log($"{title}: Info={info}");
                }
            }

            if (result.Requsts != null && result.Requsts.Count > 0)
            {
                foreach (var request in result.Requsts.Take(5))
                {
                    Log($"{title}: Request={request}");
                }
            }

            if (result.Responses != null && result.Responses.Count > 0)
            {
                foreach (var response in result.Responses.Take(5))
                {
                    Log($"{title}: Response={response}");
                }
            }
        }
    }
}

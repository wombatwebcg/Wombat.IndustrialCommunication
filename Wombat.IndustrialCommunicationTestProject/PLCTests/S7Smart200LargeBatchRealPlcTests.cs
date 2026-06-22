using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.PLC;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    [CollectionDefinition("S7Smart200RealPlc", DisableParallelization = true)]
    public sealed class S7Smart200RealPlcCollection
    {
    }

    [Collection("S7Smart200RealPlc")]
    public class S7Smart200LargeBatchRealPlcTests
    {
        private const string PlcIp = "192.168.10.100";
        private const int PlcPort = 102;
        private const SiemensVersion PlcVersion = SiemensVersion.S7_200Smart;
        private const int ConnectTimeoutSeconds = 5;
        private const int OperationTimeoutSeconds = 5;
        private const int WriteReadbackDelayMilliseconds = 150;

        private readonly ITestOutputHelper _output;

        public S7Smart200LargeBatchRealPlcTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Smart200_BatchRead_ShouldRoundTrip_ContinuousRandomAndMixedAddresses_Over200()
        {
            await ExecuteReadWriteRoundTripWithDedicatedClientAsync(
                "连续批量读回写校验 - 256 个连续字节地址",
                BuildContinuousByteScenario(baseByteAddress: 1000, byteCount: 256, seed: 2026062201)).ConfigureAwait(false);

            await ExecuteReadWriteRoundTripWithDedicatedClientAsync(
                "随机批量读回写校验 - 220 个离散字节地址",
                BuildSparseByteScenario(baseByteAddress: 2000, addressCount: 220, maxOffset: 900, seed: 2026062202)).ConfigureAwait(false);

            await ExecuteReadWriteRoundTripWithDedicatedClientAsync(
                "混合批量读回写校验 - Bool/Byte/Word/DWord 共 248 个地址",
                BuildMixedScenario(baseByteAddress: 3000, seed: 2026062203)).ConfigureAwait(false);
        }

        [Fact]
        public async Task Smart200_BatchWrite_ShouldRoundTrip_RandomAndMixedAddresses_Over200()
        {
            await ExecuteReadWriteRoundTripWithDedicatedClientAsync(
                "随机批量写入回读校验 - 240 个离散字节地址",
                BuildSparseByteScenario(baseByteAddress: 4200, addressCount: 240, maxOffset: 1200, seed: 2026062204)).ConfigureAwait(false);

            await ExecuteReadWriteRoundTripWithDedicatedClientAsync(
                "混合批量写入回读校验 - Bool/Byte/Word/DWord 共 248 个地址",
                BuildMixedScenario(baseByteAddress: 5000, seed: 2026062205)).ConfigureAwait(false);
        }

        private SiemensClient CreateClient()
        {
            return new SiemensClient(PlcIp, PlcPort, PlcVersion)
            {
                ConnectTimeout = TimeSpan.FromSeconds(ConnectTimeoutSeconds),
                ReceiveTimeout = TimeSpan.FromSeconds(OperationTimeoutSeconds),
                SendTimeout = TimeSpan.FromSeconds(OperationTimeoutSeconds),
                IsLongConnection = true,
                EnableAutoReconnect = true,
                DirtyResponseRetryAttempts = 1
            };
        }

        private async Task ConnectAsync(SiemensClient client)
        {
            Log($"连接真实 PLC: {PlcIp}:{PlcPort}, 机型={PlcVersion}");
            var connectResult = await client.ConnectAsync().ConfigureAwait(false);
            Assert.True(connectResult.IsSuccess, $"连接真实 PLC 失败: {connectResult.Message}");
        }

        private async Task ExecuteReadWriteRoundTripWithDedicatedClientAsync(
            string scenarioName,
            Dictionary<string, (DataTypeEnums, object)> expectedValues)
        {
            using var client = CreateClient();
            await ConnectAsync(client).ConfigureAwait(false);
            await ExecuteRoundTripScenarioAsync(client, scenarioName, expectedValues).ConfigureAwait(false);
            await client.DisconnectAsync().ConfigureAwait(false);
        }

        private async Task ExecuteRoundTripScenarioAsync(
            SiemensClient client,
            string scenarioName,
            Dictionary<string, (DataTypeEnums, object)> expectedValues)
        {
            Log($"开始场景: {scenarioName}");
            Log($"地址数量: {expectedValues.Count}");
            Log($"示例地址: {string.Join(", ", expectedValues.Keys.Take(8))}");

            var batchWriteResult = await client.BatchWriteAsync(expectedValues).ConfigureAwait(false);
            DumpOperationResult($"{scenarioName} - BatchWrite", batchWriteResult);
            Assert.True(batchWriteResult.IsSuccess, $"场景[{scenarioName}]批量写入失败: {batchWriteResult.Message}");
            Log($"批量写入完成: {batchWriteResult.Message}");

            await Task.Delay(WriteReadbackDelayMilliseconds).ConfigureAwait(false);

            var readRequest = expectedValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Item1);
            var batchReadResult = await client.BatchReadAsync(readRequest).ConfigureAwait(false);
            DumpOperationResult($"{scenarioName} - BatchRead", batchReadResult);
            Assert.True(batchReadResult.IsSuccess, $"场景[{scenarioName}]批量读取失败: {batchReadResult.Message}");
            Log($"批量读取完成: {batchReadResult.Message}");

            Assert.Equal(expectedValues.Count, batchReadResult.ResultValue.Count);
            AssertBatchValuesEqual(scenarioName, expectedValues, batchReadResult.ResultValue);
            Log($"场景通过: {scenarioName}");
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Channels;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.ChannelTests
{
    [Collection("S7Smart200RealPlc")]
    [Trait("Category", "RealPlc")]
    public class S7Smart200ChannelPoolRealPlcTests
    {
        private const string PlcIp = "192.168.2.20";
        private const int PlcPort = 102;
        private const SiemensVersion PlcVersion = SiemensVersion.S7_200Smart;
        private const int ChannelCount = 6;
        private static readonly TimeSpan RecoveryWindow = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan RecoveryPollInterval = TimeSpan.FromSeconds(1);

        [Fact]
        public async Task SixChannelsShouldReadAndWriteConcurrently()
        {
            await using var manager = new ChannelManager();
            var channelIds = Enumerable.Range(0, ChannelCount)
                .Select(index => $"s7-smart200-pool-{index + 1}")
                .ToArray();

            foreach (var channelId in channelIds)
            {
                await AddChannelWithRecoveryAsync(manager, new SiemensS7ChannelOptions(channelId, PlcIp, PlcVersion, PlcPort)
                {
                    ConnectTimeout = TimeSpan.FromSeconds(5),
                    OperationTimeout = TimeSpan.FromSeconds(5),
                    Rack = 0,
                    Slot = 0
                });
            }

            await Task.WhenAll(channelIds.Select((channelId, index) => ExecuteRoundTripWithRecoveryAsync(
                manager, channelId, BuildMixedScenario(1000 + index * 1000, 2026072700 + index))));

            foreach (var channelId in channelIds)
            {
                Assert.True(manager.TryGetSnapshot(channelId, out var snapshot));
                Assert.Equal(ChannelState.Online, snapshot.State);
                Assert.Equal(0, snapshot.ActiveOperations);
                Assert.Equal(0, snapshot.WaitingOperations);
            }

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.ExecuteAsync(
                    channelIds[0],
                    async (client, token) =>
                    {
                        var siemens = Assert.IsType<SiemensClient>(client);
                        return await siemens.BatchReadAsync(new Dictionary<string, DataTypeEnums> { ["VB1000"] = DataTypeEnums.Byte }, token);
                    },
                    cancellation.Token).AsTask());
            }

            var disconnected = await manager.ExecuteAsync(channelIds[0], async (client, _) =>
                await Assert.IsType<SiemensClient>(client).DisconnectAsync());
            Assert.True(disconnected.IsSuccess, disconnected.Message);

            var recovered = await manager.ExecuteAsync(channelIds[0], async (client, token) =>
                await Assert.IsType<SiemensClient>(client).BatchReadAsync(
                    new Dictionary<string, DataTypeEnums> { ["VB1000"] = DataTypeEnums.Byte }, token));
            Assert.True(recovered.IsSuccess, recovered.Message);
            Assert.True(manager.TryGetSnapshot(channelIds[0], out var recoveredSnapshot));
            Assert.Equal(ChannelState.Online, recoveredSnapshot.State);
            Assert.Equal(1, recoveredSnapshot.ReconnectCount);
        }

        private static async Task AddChannelWithRecoveryAsync(ChannelManager manager, SiemensS7ChannelOptions options)
        {
            var deadline = DateTimeOffset.UtcNow + RecoveryWindow;
            Exception lastException = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    await manager.AddAsync(options);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    await manager.RemoveAsync(options.Id);
                    await Task.Delay(RecoveryPollInterval);
                }
            }
            Assert.True(false, $"{options.Id} 在 {RecoveryWindow} 内未能连接 {PlcIp}:{PlcPort}。最后异常: {lastException?.Message}");
        }

        private static async Task ExecuteRoundTripWithRecoveryAsync(
            ChannelManager manager,
            string channelId,
            Dictionary<string, (DataTypeEnums Type, object Value)> expected)
        {
            var deadline = DateTimeOffset.UtcNow + RecoveryWindow;
            Exception lastException = null;
            OperationResult<Dictionary<string, (DataTypeEnums, object)>> lastResult = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    var result = await ExecuteRoundTripAsync(manager, channelId, expected);
                    if (result.IsSuccess)
                    {
                        Assert.Equal(expected.Count, result.ResultValue.Count);
                        foreach (var item in expected)
                        {
                            Assert.True(result.ResultValue.TryGetValue(item.Key, out var actual), $"缺少地址: {item.Key}");
                            Assert.Equal(item.Value.Type, actual.Item1);
                            Assert.Equal(item.Value.Value, actual.Item2);
                        }
                        return;
                    }
                    lastResult = result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
                await Task.Delay(RecoveryPollInterval);
            }
            Assert.True(false, $"{channelId} 在 {RecoveryWindow} 内未恢复。最后结果: {lastResult?.Message}; 最后异常: {lastException?.Message}");
        }

        private static Task<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> ExecuteRoundTripAsync(
            ChannelManager manager,
            string channelId,
            Dictionary<string, (DataTypeEnums Type, object Value)> expected)
        {
            return manager.ExecuteAsync(channelId, async (client, cancellationToken) =>
            {
                var siemens = Assert.IsType<SiemensClient>(client);
                var write = await siemens.BatchWriteAsync(expected, cancellationToken);
                if (!write.IsSuccess)
                    return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>(write);

                await Task.Delay(150, cancellationToken);
                return await siemens.BatchReadAsync(
                    expected.ToDictionary(item => item.Key, item => item.Value.Type),
                    cancellationToken);
            }).AsTask();
        }

        private static Dictionary<string, (DataTypeEnums Type, object Value)> BuildMixedScenario(int baseAddress, int seed)
        {
            var random = new Random(seed);
            var result = new Dictionary<string, (DataTypeEnums, object)>(248);

            for (int i = 0; i < 96; i++)
                result[$"V{baseAddress + i / 8}.{i % 8}"] = (DataTypeEnums.Bool, random.Next(0, 2) == 1);

            for (int i = 0; i < 64; i++)
                result[$"VB{baseAddress + 200 + i}"] = (DataTypeEnums.Byte, (byte)random.Next(0, 256));

            for (int i = 0; i < 56; i++)
                result[$"VW{baseAddress + 400 + i * 2}"] = (DataTypeEnums.Int16, (short)random.Next(short.MinValue, short.MaxValue));

            for (int i = 0; i < 32; i++)
                result[$"VD{baseAddress + 700 + i * 4}"] = (DataTypeEnums.Int32, random.Next(int.MinValue, int.MaxValue));

            return result;
        }
    }
}

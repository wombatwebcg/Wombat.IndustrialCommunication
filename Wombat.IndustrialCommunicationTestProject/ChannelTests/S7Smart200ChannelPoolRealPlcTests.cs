using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string PlcIp = "192.168.1.10";
        private const int PlcPort = 102;
        private const SiemensVersion PlcVersion = SiemensVersion.S7_200Smart;
        private const int ChannelCount = 6;

        [Fact]
        public async Task SixChannelsShouldReadAndWriteConcurrently()
        {
            await using var manager = new ChannelManager();
            var channelIds = Enumerable.Range(0, ChannelCount)
                .Select(index => $"s7-smart200-pool-{index + 1}")
                .ToArray();

            foreach (var channelId in channelIds)
            {
                await manager.AddAsync(new SiemensS7ChannelOptions(channelId, PlcIp, PlcVersion, PlcPort)
                {
                    ConnectTimeout = TimeSpan.FromSeconds(5),
                    OperationTimeout = TimeSpan.FromSeconds(5),
                    Rack = 0,
                    Slot = 0
                });
            }

            await Task.WhenAll(channelIds.Select((channelId, index) => ExecuteRoundTripAsync(
                manager, channelId, BuildMixedScenario(1000 + index * 1000, 2026072700 + index))));

            foreach (var channelId in channelIds)
            {
                Assert.True(manager.TryGetSnapshot(channelId, out var snapshot));
                Assert.Equal(ChannelState.Online, snapshot.State);
            }
        }

        private static async Task ExecuteRoundTripAsync(
            ChannelManager manager,
            string channelId,
            Dictionary<string, (DataTypeEnums Type, object Value)> expected)
        {
            var result = await manager.ExecuteAsync(channelId, async (client, cancellationToken) =>
            {
                var siemens = Assert.IsType<SiemensClient>(client);
                var write = await siemens.BatchWriteAsync(expected, cancellationToken);
                Assert.True(write.IsSuccess, $"{channelId} 写入失败: {write.Message}");

                await Task.Delay(150, cancellationToken);
                return await siemens.BatchReadAsync(
                    expected.ToDictionary(item => item.Key, item => item.Value.Type),
                    cancellationToken);
            });

            Assert.True(result.IsSuccess, $"{channelId} 读取失败: {result.Message}");
            Assert.Equal(expected.Count, result.ResultValue.Count);

            foreach (var item in expected)
            {
                Assert.True(result.ResultValue.TryGetValue(item.Key, out var actual), $"缺少地址: {item.Key}");
                Assert.Equal(item.Value.Type, actual.Item1);
                Assert.Equal(item.Value.Value, actual.Item2);
            }
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

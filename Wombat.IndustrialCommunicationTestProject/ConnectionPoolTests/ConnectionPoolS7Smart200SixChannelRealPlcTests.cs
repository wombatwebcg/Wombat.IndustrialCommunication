using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    [Collection("ConnectionPool S7Smart200 RealPlc")]
    public class ConnectionPoolS7Smart200SixChannelRealPlcTests
    {
        private const string PlcIp = "192.168.1.10";
        private const int ChannelCount = 6;
        private const int BaseAddress = 7000;
        private const int ChannelBlockSize = 128;
        private readonly ITestOutputHelper _output;

        public ConnectionPoolS7Smart200SixChannelRealPlcTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "RealPlc")]
        public async Task ConnectionPool_Smart200_SixChannels_Should_Cover_ReadWrite_And_Lifecycle()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            using var pool = new DeviceClientPool(new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = false,
                MaxRetryCount = 1,
                RetryBackoff = TimeSpan.FromMilliseconds(100),
                MaxConcurrentExecutionsPerEntry = 1,
                LeaseTimeout = TimeSpan.FromSeconds(30)
            }, new DefaultPooledDeviceClientConnectionFactory());

            var identities = Enumerable.Range(0, ChannelCount).Select(CreateIdentity).ToArray();
            foreach (var identity in identities)
            {
                Assert.True(pool.Register(CreateDescriptor(identity)).IsSuccess);
            }

            var backups = new OperationResult<byte[]>[ChannelCount];
            for (var channel = 0; channel < ChannelCount; channel++)
            {
                backups[channel] = await ReadBlockAsync(pool, identities[channel], Address(channel, "VB", 0), timeout.Token).ConfigureAwait(false);
            }
            Assert.All(backups, result => Assert.True(result.IsSuccess, result.Message));

            try
            {
                var started = 0;
                var allStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var channelResults = await Task.WhenAll(Enumerable.Range(0, ChannelCount).Select(channel => RunChannelAsync(
                    pool,
                    identities[channel],
                    channel,
                    () => { if (Interlocked.Increment(ref started) == ChannelCount) allStarted.TrySetResult(true); },
                    allStarted.Task,
                    timeout.Token))).ConfigureAwait(false);
                Assert.All(channelResults, result => Assert.True(result.IsSuccess, result.Message));
                Assert.Equal(ChannelCount, started);

                var snapshots = pool.GetEntrySnapshots();
                Assert.True(snapshots.IsSuccess);
                Assert.Equal(ChannelCount, snapshots.ResultValue.Count);

                foreach (var identity in identities)
                {
                    var lease = await pool.AcquireAsync(identity, timeout.Token).ConfigureAwait(false);
                    Assert.True(lease.IsSuccess, lease.Message);
                    lease.ResultValue.Dispose();
                    Assert.True((await pool.ReleaseAsync(lease.ResultValue).ConfigureAwait(false)).IsSuccess);

                    var reconnect = await pool.ForceReconnectAsync(identity, "六通道实体 PLC 恢复测试", timeout.Token).ConfigureAwait(false);
                    Assert.True(reconnect.IsSuccess, reconnect.Message);
                }

                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                var cancelledAcquire = await pool.AcquireAsync(identities[0], cancelled.Token).ConfigureAwait(false);
                Assert.False(cancelledAcquire.IsSuccess);
            }
            finally
            {
                for (var channel = 0; channel < ChannelCount; channel++)
                {
                    var restore = await pool.ExecuteAsync(identities[channel], client => client.WriteAsync(Address(channel, "VB", 0), backups[channel].ResultValue), ConnectionExecutionOptions.CreateWrite(), CancellationToken.None).ConfigureAwait(false);
                    Assert.True(restore.IsSuccess, "恢复通道 " + channel + " 原始 V 区失败: " + restore.Message);
                    var restored = await ReadBlockAsync(pool, identities[channel], Address(channel, "VB", 0), CancellationToken.None).ConfigureAwait(false);
                    Assert.True(restored.IsSuccess && backups[channel].ResultValue.SequenceEqual(restored.ResultValue), "通道 " + channel + " 原始 V 区回读校验失败");
                }
            }

            foreach (var identity in identities)
            {
                Assert.True(pool.Unregister(identity, "六通道测试完成").IsSuccess);
            }
        }

        private async Task<OperationResult> RunChannelAsync(DeviceClientPool pool, ConnectionIdentity identity, int channel, Action signalStarted, Task allStarted, CancellationToken cancellationToken)
        {
            var result = await pool.ExecuteAsync(identity, async client =>
            {
                signalStarted();
                if (await Task.WhenAny(allStarted, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)).ConfigureAwait(false) != allStarted)
                {
                    return Failed(channel, "六通道并发启动屏障");
                }

                var byteAddress = Address(channel, "VB", 0);
                var bitAddress = Address(channel, "V", 1) + ".3";
                var wordAddress = Address(channel, "VW", 2);
                var dwordAddress = Address(channel, "VD", 8);
                var arrayAddress = Address(channel, "VB", 32);
                var stringAddress = Address(channel, "VB", 64);

                if (!await RoundTrip(() => client.WriteAsync(byteAddress, (byte)(40 + channel)), () => client.ReadByteAsync(byteAddress), (byte)(40 + channel)).ConfigureAwait(false)) return Failed(channel, "Byte");
                if (!await RoundTrip(() => client.WriteAsync(bitAddress, channel % 2 == 0), () => client.ReadBooleanAsync(bitAddress), channel % 2 == 0).ConfigureAwait(false)) return Failed(channel, "Bool");
                if (!await RoundTrip(() => client.WriteAsync(wordAddress, (short)(-1200 - channel)), () => client.ReadInt16Async(wordAddress), (short)(-1200 - channel)).ConfigureAwait(false)) return Failed(channel, "Int16");
                if (!await RoundTrip(() => client.WriteAsync(wordAddress, (ushort)(52000 + channel)), () => client.ReadUInt16Async(wordAddress), (ushort)(52000 + channel)).ConfigureAwait(false)) return Failed(channel, "UInt16");
                if (!await RoundTrip(() => client.WriteAsync(dwordAddress, -123456700 - channel), () => client.ReadInt32Async(dwordAddress), -123456700 - channel).ConfigureAwait(false)) return Failed(channel, "Int32");
                if (!await RoundTrip(() => client.WriteAsync(dwordAddress, 3234567000u + (uint)channel), () => client.ReadUInt32Async(dwordAddress), 3234567000u + (uint)channel).ConfigureAwait(false)) return Failed(channel, "UInt32");
                if (!await RoundTrip(() => client.WriteAsync(dwordAddress, -1234567890123L - channel), () => client.ReadInt64Async(dwordAddress), -1234567890123L - channel).ConfigureAwait(false)) return Failed(channel, "Int64");
                if (!await RoundTrip(() => client.WriteAsync(dwordAddress, 1234567890123UL + (ulong)channel), () => client.ReadUInt64Async(dwordAddress), 1234567890123UL + (ulong)channel).ConfigureAwait(false)) return Failed(channel, "UInt64");
                if (!await RoundTrip(() => client.WriteAsync(dwordAddress, 12.5f + channel), () => client.ReadFloatAsync(dwordAddress), 12.5f + channel).ConfigureAwait(false)) return Failed(channel, "Float");
                if (!await RoundTrip(() => client.WriteAsync(dwordAddress, 1234.5d + channel), () => client.ReadDoubleAsync(dwordAddress), 1234.5d + channel).ConfigureAwait(false)) return Failed(channel, "Double");

                var bytes = new byte[] { (byte)channel, 1, 2, 3, 4, 5, 6, 7 };
                var byteWrite = await client.WriteAsync(arrayAddress, bytes).ConfigureAwait(false);
                var byteRead = await client.ReadByteAsync(arrayAddress, bytes.Length).ConfigureAwait(false);
                if (!byteWrite.IsSuccess || !byteRead.IsSuccess || !bytes.SequenceEqual(byteRead.ResultValue)) return Failed(channel, "Byte[]");

                var text = "CH" + channel + "-SMART200";
                if (!await RoundTrip(() => client.WriteAsync(stringAddress, text), () => client.ReadStringAsync(stringAddress, text.Length), text).ConfigureAwait(false)) return Failed(channel, "String");

                var batchValues = new Dictionary<string, (DataTypeEnums, object)>
                {
                    [Address(channel, "V", 48) + ".1"] = (DataTypeEnums.Bool, channel % 2 == 1),
                    [Address(channel, "VB", 50)] = (DataTypeEnums.Byte, (byte)(100 + channel)),
                    [Address(channel, "VW", 52)] = (DataTypeEnums.Int16, (short)(2000 + channel)),
                    [Address(channel, "VD", 56)] = (DataTypeEnums.Int32, 300000 + channel)
                };
                var batchWrite = await client.BatchWriteAsync(batchValues, cancellationToken).ConfigureAwait(false);
                var batchRead = await client.BatchReadAsync(batchValues.ToDictionary(item => item.Key, item => item.Value.Item1), cancellationToken).ConfigureAwait(false);
                if (!batchWrite.IsSuccess || !batchRead.IsSuccess || batchRead.ResultValue.Count != batchValues.Count) return Failed(channel, "Batch");

                _output.WriteLine("通道 {0} 完成全部读写操作，identity={1}", channel, identity.DeviceId);
                return OperationResult.CreateSuccessResult();
            }, ConnectionExecutionOptions.CreateWrite(), cancellationToken).ConfigureAwait(false);

            return result;
        }

        private static async Task<bool> RoundTrip<T>(Func<Task<OperationResult>> write, Func<ValueTask<OperationResult<T>>> read, T expected)
        {
            var writeResult = await write().ConfigureAwait(false);
            if (!writeResult.IsSuccess)
            {
                return false;
            }

            var readResult = await read().ConfigureAwait(false);
            return readResult.IsSuccess && EqualityComparer<T>.Default.Equals(expected, readResult.ResultValue);
        }

        private static async Task<OperationResult<byte[]>> ReadBlockAsync(DeviceClientPool pool, ConnectionIdentity identity, string address, CancellationToken cancellationToken)
        {
            return await pool.ExecuteAsync(identity, async client => await client.ReadByteAsync(address, ChannelBlockSize).ConfigureAwait(false), ConnectionExecutionOptions.CreateRead(), cancellationToken).ConfigureAwait(false);
        }

        private static OperationResult Failed(int channel, string operation)
        {
            return OperationResult.CreateFailedResult("通道 " + channel + " 的 " + operation + " 读写校验失败");
        }

        private static string Address(int channel, string prefix, int offset)
        {
            return prefix + (BaseAddress + channel * ChannelBlockSize + offset);
        }

        private static ConnectionIdentity CreateIdentity(int channel)
        {
            return new ConnectionIdentity
            {
                DeviceId = "smart200-192.168.1.10-physical-channel-" + channel,
                ProtocolType = "SiemensS7",
                Endpoint = PlcIp + ":102/channel/" + channel
            };
        }

        private static ResourceDescriptor CreateDescriptor(ConnectionIdentity identity)
        {
            return new ResourceDescriptor
            {
                Identity = identity,
                ResourceRole = ResourceRole.Client,
                DeviceConnectionType = DeviceConnectionType.SiemensS7,
                ConnectionParameters = new SiemensS7ClientConnectionParameters
                {
                    Ip = PlcIp,
                    Port = 102,
                    SiemensVersion = SiemensVersion.S7_200Smart,
                    Rack = 0,
                    Slot = 0,
                    ConnectTimeoutMilliseconds = 5000,
                    ReceiveTimeoutMilliseconds = 5000,
                    SendTimeoutMilliseconds = 5000
                }
            };
        }
    }
}

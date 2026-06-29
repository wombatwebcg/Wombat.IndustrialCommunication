using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    [CollectionDefinition("ConnectionPool S7 Stable Hundred", DisableParallelization = true)]
    public sealed class ConnectionPoolS7StableHundredCollection
    {
    }

    [Collection("ConnectionPool S7 Stable Hundred")]
    public class ConnectionPoolS7StableHundredClientBatchReadTests : IClassFixture<S7HundredServerFixture>
    {
        private const int ServerCount = 100;
        private const int BasePort = 21000;
        private const int StressLoopCount = 50;
        private const int OperationTimeoutMilliseconds = 5000;
        private readonly S7HundredServerFixture _fixture;
        private readonly ITestOutputHelper _output;

        public ConnectionPoolS7StableHundredClientBatchReadTests(S7HundredServerFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        [Trait("Category", "Stress")]
        public async Task ConnectionPool_S7_100Servers_100Clients_Should_BatchRead_Stably()
        {
            if (!_fixture.IsAvailable)
            {
                _output.WriteLine(_fixture.StartupErrorMessage);
                return;
            }

            using (var pool = CreatePool())
            {
                var identities = Enumerable.Range(0, ServerCount).Select(CreateIdentity).ToArray();
                foreach (var identity in identities)
                {
                    var register = pool.Register(CreateDescriptor(identity));
                    Assert.True(register.IsSuccess, "注册 S7 连接失败: " + identity.DeviceId + ", message=" + register.Message);
                }

                await WarmupConnectionsAsync(pool, identities).ConfigureAwait(false);

                var expectedValues = BuildMixedScenario(baseByteAddress: 3000, seed: 2026062901);
                await SeedServersAsync(pool, identities, expectedValues).ConfigureAwait(false);

                var readRequest = expectedValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Item1);
                for (var round = 1; round <= StressLoopCount; round++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var reads = identities.Select(identity => pool.ExecuteAsync<Dictionary<string, (DataTypeEnums, object)>>(
                        identity,
                        async client => await client.BatchReadAsync(readRequest).ConfigureAwait(false))).ToArray();

                    var results = await Task.WhenAll(reads).ConfigureAwait(false);
                    stopwatch.Stop();

                    Assert.All(results, result => Assert.True(result.IsSuccess, "批量读取失败: " + result.Message));
                    for (var i = 0; i < results.Length; i++)
                    {
                        AssertBatchValuesEqual(identities[i].DeviceId, expectedValues, results[i].ResultValue);
                    }

                    _output.WriteLine("第 {0}/{1} 轮完成: clients={2}, addresses={3}, elapsedMs={4}",
                        round,
                        StressLoopCount,
                        identities.Length,
                        readRequest.Count,
                        stopwatch.ElapsedMilliseconds);
                }
            }
        }

        [Fact]
        [Trait("Category", "Stress")]
        public async Task ConnectionPool_S7_100Servers_100Clients_Should_Recover_When_Servers_Randomly_Drop_10Times_In_2Minutes()
        {
            if (!_fixture.IsAvailable)
            {
                _output.WriteLine(_fixture.StartupErrorMessage);
                return;
            }

            using (var pool = CreatePool())
            {
                var identities = Enumerable.Range(0, ServerCount).Select(CreateIdentity).ToArray();
                foreach (var identity in identities)
                {
                    var register = pool.Register(CreateDescriptor(identity));
                    Assert.True(register.IsSuccess, "注册 S7 连接失败: " + identity.DeviceId + ", message=" + register.Message);
                }

                await WarmupConnectionsAsync(pool, identities).ConfigureAwait(false);

                var expectedValues = BuildMixedScenario(baseByteAddress: 3000, seed: 2026062901);
                await SeedServersAsync(pool, identities, expectedValues).ConfigureAwait(false);

                var readRequest = expectedValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Item1);
                var stopAtUtc = DateTime.UtcNow.AddMinutes(2);
                var dropTask = RunRandomServerDropsAsync(stopAtUtc).ConfigureAwait(false);
                var successCount = 0;
                var failureCount = 0;
                var round = 0;

                while (DateTime.UtcNow < stopAtUtc)
                {
                    round++;
                    var stopwatch = Stopwatch.StartNew();
                    var results = await ReadAllAsync(pool, identities, readRequest).ConfigureAwait(false);
                    stopwatch.Stop();

                    var roundSuccess = 0;
                    var roundFailure = 0;
                    for (var i = 0; i < results.Length; i++)
                    {
                        if (results[i].IsSuccess)
                        {
                            AssertBatchValuesEqual(identities[i].DeviceId, expectedValues, results[i].ResultValue);
                            roundSuccess++;
                            continue;
                        }

                        roundFailure++;
                    }

                    successCount += roundSuccess;
                    failureCount += roundFailure;
                    _output.WriteLine("掉线恢复测试第 {0} 轮: success={1}, failure={2}, elapsedMs={3}",
                        round,
                        roundSuccess,
                        roundFailure,
                        stopwatch.ElapsedMilliseconds);
                }

                await dropTask;
                await AssertAllRecoveredAsync(pool, identities, readRequest, expectedValues).ConfigureAwait(false);

                Assert.True(successCount > 0, "掉线恢复测试没有任何成功读取");
                _output.WriteLine("掉线恢复测试完成: rounds={0}, totalSuccess={1}, totalFailure={2}", round, successCount, failureCount);
            }
        }

        private static DeviceClientPool CreatePool()
        {
            return new DeviceClientPool(
                new ConnectionPoolOptions
                {
                    MaxConnections = ServerCount + 10,
                    EnableBackgroundMaintenance = true,
                    HealthCheckInterval = TimeSpan.FromSeconds(10),
                    MaxConcurrentHealthChecks = 16,
                    MaxConcurrentRecoveries = 16,
                    MaxConcurrentForceCloses = 32,
                    MaxRetryCount = 1,
                    RetryBackoff = TimeSpan.FromMilliseconds(100),
                    LeaseTimeout = TimeSpan.FromSeconds(30),
                    IdleTimeout = TimeSpan.FromMinutes(5),
                    MaxConcurrentExecutionsPerEntry = 1
                },
                new DefaultPooledDeviceClientConnectionFactory());
        }

        private static async Task WarmupConnectionsAsync(DeviceClientPool pool, ConnectionIdentity[] identities)
        {
            var tasks = identities.Select(identity => pool.ExecuteAsync(identity, _ =>
                Task.FromResult(OperationResult.CreateSuccessResult("connected")))).ToArray();
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            Assert.All(results, result => Assert.True(result.IsSuccess, "连接预热失败: " + result.Message));
        }

        private static async Task SeedServersAsync(
            DeviceClientPool pool,
            ConnectionIdentity[] identities,
            Dictionary<string, (DataTypeEnums, object)> expectedValues)
        {
            var tasks = identities.Select(identity => pool.ExecuteAsync(identity, client =>
                client.BatchWriteAsync(expectedValues).AsTask())).ToArray();
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            Assert.All(results, result => Assert.True(result.IsSuccess, "批量写入种子数据失败: " + result.Message));
            await Task.Delay(150).ConfigureAwait(false);
        }

        private static Task<OperationResult<Dictionary<string, (DataTypeEnums, object)>>[]> ReadAllAsync(
            DeviceClientPool pool,
            ConnectionIdentity[] identities,
            Dictionary<string, DataTypeEnums> readRequest)
        {
            var reads = identities.Select(identity => pool.ExecuteAsync<Dictionary<string, (DataTypeEnums, object)>>(
                identity,
                async client => await client.BatchReadAsync(readRequest).ConfigureAwait(false))).ToArray();
            return Task.WhenAll(reads);
        }

        private async Task RunRandomServerDropsAsync(DateTime stopAtUtc)
        {
            var random = new Random(2026062902);
            var startedAtUtc = DateTime.UtcNow;
            var totalMilliseconds = Math.Max(1, (stopAtUtc - startedAtUtc).TotalMilliseconds);
            var offsets = Enumerable.Range(0, 10)
                .Select(_ => random.Next(0, (int)totalMilliseconds))
                .OrderBy(value => value)
                .ToArray();

            for (var i = 0; i < offsets.Length; i++)
            {
                var dueAtUtc = startedAtUtc.AddMilliseconds(offsets[i]);
                var delay = dueAtUtc - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                }

                var serverIndex = random.Next(0, ServerCount);
                var downTime = TimeSpan.FromMilliseconds(random.Next(1000, 3000));
                _output.WriteLine("随机掉线 {0}/10: server={1}, downtimeMs={2}", i + 1, serverIndex, downTime.TotalMilliseconds);
                await _fixture.DropServerAsync(serverIndex, downTime).ConfigureAwait(false);
            }
        }

        private static async Task AssertAllRecoveredAsync(
            DeviceClientPool pool,
            ConnectionIdentity[] identities,
            Dictionary<string, DataTypeEnums> readRequest,
            Dictionary<string, (DataTypeEnums, object)> expectedValues)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            OperationResult<Dictionary<string, (DataTypeEnums, object)>>[] lastResults = null;
            while (DateTime.UtcNow < deadline)
            {
                lastResults = await ReadAllAsync(pool, identities, readRequest).ConfigureAwait(false);
                if (lastResults.All(result => result.IsSuccess))
                {
                    for (var i = 0; i < lastResults.Length; i++)
                    {
                        AssertBatchValuesEqual(identities[i].DeviceId, expectedValues, lastResults[i].ResultValue);
                    }

                    return;
                }

                await Task.Delay(1000).ConfigureAwait(false);
            }

            var failed = lastResults == null ? identities.Length : lastResults.Count(result => result == null || !result.IsSuccess);
            Assert.True(false, "随机掉线结束后仍有连接未恢复，失败数: " + failed);
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
                    Ip = "127.0.0.1",
                    Port = BasePort + int.Parse(identity.DeviceId.Substring("s7-stable-".Length)),
                    SiemensVersion = SiemensVersion.S7_1200,
                    Slot = 0,
                    Rack = 0,
                    ConnectTimeoutMilliseconds = OperationTimeoutMilliseconds,
                    ReceiveTimeoutMilliseconds = OperationTimeoutMilliseconds,
                    SendTimeoutMilliseconds = OperationTimeoutMilliseconds
                }
            };
        }

        private static ConnectionIdentity CreateIdentity(int index)
        {
            return new ConnectionIdentity
            {
                DeviceId = "s7-stable-" + index,
                ProtocolType = "SiemensS7",
                Endpoint = "127.0.0.1:" + (BasePort + index)
            };
        }

        private static Dictionary<string, (DataTypeEnums, object)> BuildMixedScenario(int baseByteAddress, int seed)
        {
            var random = new Random(seed);
            var result = new Dictionary<string, (DataTypeEnums, object)>(248);

            for (var i = 0; i < 96; i++)
            {
                result["DB1.DBX" + (baseByteAddress + i / 8) + "." + (i % 8)] = (DataTypeEnums.Bool, (object)(random.Next(0, 2) == 1));
            }

            for (var i = 0; i < 64; i++)
            {
                result["DB1.DBB" + (baseByteAddress + 200 + i)] = (DataTypeEnums.Byte, (object)(byte)random.Next(0, 256));
            }

            for (var i = 0; i < 56; i++)
            {
                result["DB1.DBW" + (baseByteAddress + 400 + i * 2)] = (DataTypeEnums.Int16, (object)(short)random.Next(short.MinValue, short.MaxValue));
            }

            for (var i = 0; i < 32; i++)
            {
                result["DB1.DBD" + (baseByteAddress + 700 + i * 4)] = (DataTypeEnums.Int32, random.Next(int.MinValue, int.MaxValue));
            }

            return result;
        }

        private static void AssertBatchValuesEqual(
            string name,
            Dictionary<string, (DataTypeEnums, object)> expectedValues,
            Dictionary<string, (DataTypeEnums, object)> actualValues)
        {
            Assert.Equal(expectedValues.Count, actualValues.Count);
            foreach (var expected in expectedValues)
            {
                Assert.True(actualValues.ContainsKey(expected.Key), name + " 缺少地址: " + expected.Key);
                var actual = actualValues[expected.Key];
                Assert.Equal(expected.Value.Item1, actual.Item1);
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
                        throw new NotSupportedException("未处理的数据类型: " + expected.Value.Item1);
                }
            }
        }
    }

    public sealed class S7HundredServerFixture : IAsyncLifetime, IDisposable
    {
        private readonly List<S7TcpServer> _servers = new List<S7TcpServer>();
        private readonly object _syncRoot = new object();

        public string StartupErrorMessage { get; private set; }

        public bool IsAvailable => string.IsNullOrWhiteSpace(StartupErrorMessage);

        public async Task InitializeAsync()
        {
            try
            {
                for (var i = 0; i < 100; i++)
                {
                    var server = new S7TcpServer("127.0.0.1", 21000 + i);
                    server.SetSiemensVersion(SiemensVersion.S7_1200);
                    server.SetRackSlot(0, 0);
                    var db = server.CreateDataBlock(1, 4096);
                    if (!db.IsSuccess)
                    {
                        throw new InvalidOperationException("创建 S7 DB1 失败: port=" + (21000 + i) + ", message=" + db.Message);
                    }

                    var listen = server.Listen();
                    if (!listen.IsSuccess)
                    {
                        throw new InvalidOperationException("启动 S7TcpServer 失败: port=" + (21000 + i) + ", message=" + listen.Message);
                    }

                    _servers.Add(server);
                }

                await Task.Delay(300).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StartupErrorMessage = "无法启动 100 个本地 S7TcpServer，可能端口被占用。原始错误: " + ex.Message;
                await DisposeAsync().ConfigureAwait(false);
            }
        }

        public Task DisposeAsync()
        {
            for (var i = _servers.Count - 1; i >= 0; i--)
            {
                try
                {
                    _servers[i].Shutdown();
                }
                finally
                {
                    _servers[i].Dispose();
                }
            }

            _servers.Clear();
            return Task.CompletedTask;
        }

        public async Task DropServerAsync(int index, TimeSpan downTime)
        {
            S7TcpServer server;
            lock (_syncRoot)
            {
                server = _servers[index];
                var shutdown = server.Shutdown();
                if (!shutdown.IsSuccess)
                {
                    throw new InvalidOperationException("停止 S7TcpServer 失败: index=" + index + ", message=" + shutdown.Message);
                }
            }

            await Task.Delay(downTime).ConfigureAwait(false);

            lock (_syncRoot)
            {
                var listen = server.Listen();
                if (!listen.IsSuccess)
                {
                    throw new InvalidOperationException("恢复 S7TcpServer 失败: index=" + index + ", message=" + listen.Message);
                }
            }
        }

        public void Dispose()
        {
        }
    }
}

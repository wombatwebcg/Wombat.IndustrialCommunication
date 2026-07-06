using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.ServerTest
{
    internal static class Program
    {
        private const string Ip = "127.0.0.1";
        private const int BasePort = 21000;
        private const int ServerCount = 100;
        private const int OperationTimeoutMilliseconds = 5000;
        private const int DropWindowMilliseconds = 60000;
        private const int DropsPerWindow = 2;
        private const int MinDropDurationMilliseconds = 10000;
        private const int MaxDropDurationMilliseconds = 15000;

        private static ILoggerFactory? _loggerFactory;
        private static ILogger? _logger;
        private static DeviceServerPool? _serverPool;
        private static ConnectionIdentity[]? _identities;

        private static async Task Main()
        {
            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, args) =>
            {
                args.Cancel = true;
                stop.Cancel();
            };

            try
            {
                _loggerFactory = CreateLoggerFactory();
                _logger = _loggerFactory.CreateLogger("S7HundredServerPool");
                _serverPool = CreateServerPool();
                _identities = Enumerable.Range(0, ServerCount).Select(CreateIdentity).ToArray();

                RegisterServers();
                await StartServersAsync(stop.Token).ConfigureAwait(false);
                await InitializeServersAsync().ConfigureAwait(false);

                _logger.LogInformation("已启动 {Count} 个 S7 虚拟服务器: {StartPort}-{EndPort}", ServerCount, BasePort, BasePort + ServerCount - 1);
                _logger.LogInformation("按 Ctrl+C 退出；后台会每 {WindowMs} ms 随机掉线 {DropCount} 次，每次 {MinDownMs}-{MaxDownMs} ms。",
                    DropWindowMilliseconds,
                    DropsPerWindow,
                    MinDropDurationMilliseconds,
                    MaxDropDurationMilliseconds);

                await RunRandomServerDropsAsync(stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                _logger?.LogInformation("收到退出信号，正在停止 S7 虚拟服务器。");
            }
            finally
            {
                DisposeResources();
            }
        }

        private static ILoggerFactory CreateLoggerFactory()
        {
            return LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss.fff ";
                    options.UseUtcTimestamp = false;
                    options.ColorBehavior = LoggerColorBehavior.Enabled;
                });
                builder.SetMinimumLevel(LogLevel.Information);
            });
        }

        private static DeviceServerPool CreateServerPool()
        {
            return new DeviceServerPool(
                new ConnectionPoolOptions
                {
                    MaxConnections = ServerCount + 10,
                    MaxRetryCount = 1,
                    RetryBackoff = TimeSpan.FromMilliseconds(200),
                    EnableBackgroundMaintenance = false
                },
                new DefaultPooledDeviceServerConnectionFactory());
        }

        private static void RegisterServers()
        {
            var identities = _identities ?? Array.Empty<ConnectionIdentity>();
            foreach (var identity in identities)
            {
                EnsureSuccess(_serverPool!.Register(CreateDescriptor(identity)), "注册 S7 服务端失败: " + identity.DeviceId);
            }
        }

        private static async Task StartServersAsync(CancellationToken cancellationToken)
        {
            var identities = _identities ?? Array.Empty<ConnectionIdentity>();
            foreach (var identity in identities)
            {
                EnsureSuccess(await _serverPool!.StartAsync(identity, cancellationToken).ConfigureAwait(false), "启动 S7 服务端失败: " + identity.DeviceId);
            }
        }

        private static async Task InitializeServersAsync()
        {
            var identities = _identities ?? Array.Empty<ConnectionIdentity>();
            foreach (var identity in identities)
            {
                EnsureSuccess(
                    await _serverPool!.ExecuteAsync(
                        identity,
                        server =>
                        {
                            if (server is not S7TcpServer s7Server)
                            {
                                return Task.FromResult(OperationResult.CreateFailedResult("当前连接不是 S7TcpServer"));
                            }

                            s7Server.SetSiemensVersion(SiemensVersion.S7_1200);
                            s7Server.SetRackSlot(0, 0);
                            //s7Server.UseLogger(_loggerFactory!.CreateLogger<S7TcpServer>());
                            //s7Server.EnableDataMonitoring(true);
                            s7Server.PacketTraced -= HandleS7PacketTraced;
                            s7Server.PacketTraced += HandleS7PacketTraced;
                            s7Server.DataRead -= HandleS7DataRead;
                            s7Server.DataRead += HandleS7DataRead;
                            s7Server.DataWritten -= HandleS7DataWritten;
                            s7Server.DataWritten += HandleS7DataWritten;

                            var createDbResult = s7Server.CreateDataBlock(1, 4096);
                            return Task.FromResult(createDbResult.IsSuccess
                                ? OperationResult.CreateSuccessResult("S7 服务端初始化完成")
                                : OperationResult.CreateFailedResult("创建 S7 DB1 失败: " + BuildErrorMessage(createDbResult)));
                        }).ConfigureAwait(false),
                    "初始化 S7 服务端失败: " + identity.DeviceId);
            }
        }

        private static async Task RunRandomServerDropsAsync(CancellationToken cancellationToken)
        {
            var identities = _identities ?? Array.Empty<ConnectionIdentity>();
            var tasks = identities
                .Select((identity, index) => RunServerRandomDropsAsync(index, identity, cancellationToken))
                .ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static async Task RunServerRandomDropsAsync(int index, ConnectionIdentity identity, CancellationToken cancellationToken)
        {
            var random = new Random(unchecked(Environment.TickCount * 31 + index));

            while (!cancellationToken.IsCancellationRequested)
            {
                var windowStartedAtUtc = DateTime.UtcNow;
                var offsets = Enumerable.Range(0, DropsPerWindow)
                    .Select(_ => random.Next(0, DropWindowMilliseconds))
                    .OrderBy(value => value)
                    .ToArray();

                for (var i = 0; i < offsets.Length; i++)
                {
                    var dueAtUtc = windowStartedAtUtc.AddMilliseconds(offsets[i]);
                    var delay = dueAtUtc - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }

                    var downTime = TimeSpan.FromMilliseconds(random.Next(MinDropDurationMilliseconds, MaxDropDurationMilliseconds));
                    await DropServerAsync(identity, downTime, cancellationToken).ConfigureAwait(false);
                }

                var nextWindowDelay = windowStartedAtUtc.AddMilliseconds(DropWindowMilliseconds) - DateTime.UtcNow;
                if (nextWindowDelay > TimeSpan.Zero)
                {
                    await Task.Delay(nextWindowDelay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static async Task DropServerAsync(ConnectionIdentity identity, TimeSpan downTime, CancellationToken cancellationToken)
        {
            var logger = _logger ?? throw new InvalidOperationException("日志未初始化");
            logger.LogWarning("随机掉线: {DeviceId} {Endpoint}, downtimeMs={DowntimeMs}", identity.DeviceId, identity.Endpoint, downTime.TotalMilliseconds);

            EnsureSuccess(await _serverPool!.StopAsync(identity, "随机掉线", cancellationToken).ConfigureAwait(false), "停止 S7 服务端失败: " + identity.DeviceId);
            logger.LogWarning("停机后 TCP 探测: {DeviceId} {Endpoint}, canConnect={CanConnect}", identity.DeviceId, identity.Endpoint, await CanConnectAsync(identity, cancellationToken).ConfigureAwait(false));
            await Task.Delay(downTime, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(await _serverPool.StartAsync(identity, cancellationToken).ConfigureAwait(false), "恢复 S7 服务端失败: " + identity.DeviceId);
            logger.LogInformation("恢复后 TCP 探测: {DeviceId} {Endpoint}, canConnect={CanConnect}", identity.DeviceId, identity.Endpoint, await CanConnectAsync(identity, cancellationToken).ConfigureAwait(false));

            logger.LogInformation("已恢复: {DeviceId} {Endpoint}", identity.DeviceId, identity.Endpoint);
        }

        private static ResourceDescriptor CreateDescriptor(ConnectionIdentity identity)
        {
            return new ResourceDescriptor
            {
                Identity = identity,
                ResourceRole = ResourceRole.Server,
                ConnectionType = DeviceConnectionType.SiemensS7.ToString(),
                DeviceConnectionType = DeviceConnectionType.SiemensS7,
                ConnectionParameters = new SiemensS7ServerConnectionParameters
                {
                    Ip = Ip,
                    Port = BasePort + int.Parse(identity.DeviceId.Substring("s7-server-".Length)),
                    MaxConnections = 10,
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
                DeviceId = "s7-server-" + index,
                ProtocolType = "SiemensS7",
                Endpoint = Ip + ":" + (BasePort + index)
            };
        }

        private static async Task<bool> CanConnectAsync(ConnectionIdentity identity, CancellationToken cancellationToken)
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(OperationTimeoutMilliseconds);

            try
            {
                await client.ConnectAsync(Ip, BasePort + int.Parse(identity.DeviceId.Substring("s7-server-".Length)), timeout.Token).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void HandleS7PacketTraced(object? sender, PacketTraceEventArgs args)
        {
            _logger?.LogDebug(
                "S7 Packet {Direction} {Meaning} Data={Data}",
                args.Direction,
                args.Meaning,
                BitConverter.ToString(args.Data ?? Array.Empty<byte>()).Replace("-", " "));
        }

        private static void HandleS7DataRead(object? sender, S7DataStoreEventArgs args)
        {
            _logger?.LogDebug(
                "S7 DataRead Area={Area} DB={DbNumber} Start={StartAddress} Length={Length} Data={Data}",
                args.Area,
                args.DbNumber,
                args.StartAddress,
                args.Length,
                FormatBytes(args.Data));
        }

        private static void HandleS7DataWritten(object? sender, S7DataStoreEventArgs args)
        {
            _logger?.LogDebug(
                "S7 DataWritten Area={Area} DB={DbNumber} Start={StartAddress} Length={Length} Data={Data}",
                args.Area,
                args.DbNumber,
                args.StartAddress,
                args.Length,
                FormatBytes(args.Data));
        }

        private static void EnsureSuccess(OperationResult result, string errorMessage)
        {
            if (result != null && result.IsSuccess)
            {
                return;
            }

            var details = result == null ? "结果为空" : BuildErrorMessage(result);
            throw new InvalidOperationException(errorMessage + "。详细信息: " + details);
        }

        private static string BuildErrorMessage(OperationResult result)
        {
            var info = result.OperationInfo == null
                ? string.Empty
                : string.Join(" | ", result.OperationInfo.Where(item => !string.IsNullOrWhiteSpace(item)));

            if (string.IsNullOrWhiteSpace(info))
            {
                return string.IsNullOrWhiteSpace(result.Message) ? "未返回错误信息" : result.Message;
            }

            return string.IsNullOrWhiteSpace(result.Message) ? info : result.Message + " | " + info;
        }

        private static string FormatBytes(IEnumerable<byte>? data)
        {
            if (data == null)
            {
                return "(null)";
            }

            var bytes = data.ToArray();
            return bytes.Length == 0 ? "(empty)" : BitConverter.ToString(bytes).Replace("-", " ");
        }

        private static void DisposeResources()
        {
            if (_serverPool != null && _identities != null)
            {
                foreach (var identity in _identities)
                {
                    try
                    {
                        _serverPool.Stop(identity, "测试程序退出，停止 S7 服务端");
                    }
                    catch
                    {
                    }
                }

                _serverPool.Dispose();
            }

            _loggerFactory?.Dispose();
        }
    }
}

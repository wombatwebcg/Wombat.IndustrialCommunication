using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.ServerTest
{
    internal static class Program
    {
        private const string S7Ip = "172.168.1.99";
        private const int S7Port = 102;
        private const string ModbusIp = "172.168.1.99";
        private const int ModbusPort = 502;

        private static readonly ConnectionIdentity S7Identity = new ConnectionIdentity
        {
            DeviceId = "S7ServerPool",
            ProtocolType = "SiemensS7",
            Endpoint = S7Ip + ":" + S7Port
        };

        private static readonly ConnectionIdentity ModbusIdentity = new ConnectionIdentity
        {
            DeviceId = "ModbusTcpServerPool",
            ProtocolType = "ModbusTcp",
            Endpoint = ModbusIp + ":" + ModbusPort
        };

        private static ILoggerFactory? _loggerFactory;
        private static ILogger? _logger;
        private static DeviceServerPool? _serverPool;

        private static async Task Main(string[] args)
        {
            try
            {
                _loggerFactory = CreateLoggerFactory();
                _logger = _loggerFactory.CreateLogger("ServerPoolTest");
                _serverPool = CreateServerPool();

                RegisterServers(_serverPool);
                await StartServersAsync(_serverPool).ConfigureAwait(false);
                await InitializeServersAsync(_serverPool).ConfigureAwait(false);

                _logger.LogInformation("服务端连接池已启动");
                _logger.LogInformation("Modbus TCP: {Endpoint}，默认测试值: 站号1 保持寄存器0 = 1234", ModbusIdentity.Endpoint);
                _logger.LogInformation("S7 TCP: {Endpoint}，默认测试值: DB1.DBB0 = 0x80", S7Identity.Endpoint);
                _logger.LogInformation("实物测试时可直接连接上述两个端口，按任意键退出。");

                Console.ReadKey(intercept: true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "服务器连接池测试启动失败");
                throw;
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
                builder.SetMinimumLevel(LogLevel.Debug);
            });
        }

        private static DeviceServerPool CreateServerPool()
        {
            var options = new ConnectionPoolOptions
            {
                MaxConnections = 8,
                MaxRetryCount = 1,
                RetryBackoff = TimeSpan.FromMilliseconds(200),
                EnableBackgroundMaintenance = true,
                HealthCheckInterval = TimeSpan.FromSeconds(30)
            };

            return new DeviceServerPool(options, new DefaultPooledDeviceServerConnectionFactory());
        }

        private static void RegisterServers(DeviceServerPool serverPool)
        {
            EnsureSuccess(serverPool.Register(CreateModbusDescriptor()), "注册 Modbus TCP 服务端失败");
            EnsureSuccess(serverPool.Register(CreateS7Descriptor()), "注册 S7 服务端失败");
        }

        private static async Task StartServersAsync(DeviceServerPool serverPool)
        {
            EnsureSuccess(await serverPool.StartAsync(ModbusIdentity).ConfigureAwait(false), "启动 Modbus TCP 服务端失败");
            EnsureSuccess(await serverPool.StartAsync(S7Identity).ConfigureAwait(false), "启动 S7 服务端失败");
        }

        private static async Task InitializeServersAsync(DeviceServerPool serverPool)
        {
            EnsureSuccess(
                await serverPool.ExecuteAsync(
                    ModbusIdentity,
                    server =>
                    {
                        if (server is ModbusTcpServer modbusServer)
                        {
                            modbusServer.UseLogger(_loggerFactory!.CreateLogger<ModbusTcpServer>());
                            modbusServer.SlaveId = 1;
                        }

                        var writeResult = server.Write(DataTypeEnums.UInt16, "1;6;0", (ushort)1234);
                        if (!writeResult.IsSuccess)
                        {
                            return Task.FromResult(OperationResult.CreateFailedResult(
                                "初始化 Modbus 保持寄存器失败: " + BuildErrorMessage(writeResult)));
                        }

                        return Task.FromResult(OperationResult.CreateSuccessResult("Modbus 服务端初始化完成"));
                    }).ConfigureAwait(false),
                "初始化 Modbus TCP 服务端失败");

            EnsureSuccess(
                await serverPool.ExecuteAsync(
                    S7Identity,
                    server =>
                    {
                        if (server is not S7TcpServer s7Server)
                        {
                            return Task.FromResult(OperationResult.CreateFailedResult("当前连接不是 S7TcpServer"));
                        }

                        s7Server.SetSiemensVersion(SiemensVersion.S7_1200);
                        s7Server.UseLogger(_loggerFactory!.CreateLogger<S7TcpServer>());
                        s7Server.EnableDataMonitoring(true);
                        s7Server.DataRead -= HandleS7DataRead;
                        s7Server.DataRead += HandleS7DataRead;
                        s7Server.DataWritten -= HandleS7DataWritten;
                        s7Server.DataWritten += HandleS7DataWritten;

                        var createDbResult = s7Server.CreateDataBlock(1, 256);
                        if (!createDbResult.IsSuccess)
                        {
                            return Task.FromResult(OperationResult.CreateFailedResult(
                                "创建 S7 DB1 失败: " + BuildErrorMessage(createDbResult)));
                        }

                        var writeDbResult = s7Server.WriteDB(1, 0, new byte[] { 128 });
                        if (!writeDbResult.IsSuccess)
                        {
                            return Task.FromResult(OperationResult.CreateFailedResult(
                                "初始化 S7 DB1.DBB0 失败: " + BuildErrorMessage(writeDbResult)));
                        }

                        return Task.FromResult(OperationResult.CreateSuccessResult("S7 服务端初始化完成"));
                    }).ConfigureAwait(false),
                "初始化 S7 服务端失败");
        }

        private static ResourceDescriptor CreateModbusDescriptor()
        {
            return new ResourceDescriptor
            {
                Identity = ModbusIdentity,
                ResourceRole = ResourceRole.Server,
                ConnectionType = DeviceConnectionType.ModbusTcp.ToString(),
                DeviceConnectionType = DeviceConnectionType.ModbusTcp,
                Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ip"] = ModbusIp,
                    ["port"] = ModbusPort,
                    ["maxConnections"] = 10,
                    ["receiveTimeoutMilliseconds"] = 5000,
                    ["sendTimeoutMilliseconds"] = 5000
                }
            };
        }

        private static ResourceDescriptor CreateS7Descriptor()
        {
            return new ResourceDescriptor
            {
                Identity = S7Identity,
                ResourceRole = ResourceRole.Server,
                ConnectionType = DeviceConnectionType.SiemensS7.ToString(),
                DeviceConnectionType = DeviceConnectionType.SiemensS7,
                Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ip"] = S7Ip,
                    ["port"] = S7Port,
                    ["maxConnections"] = 10,
                    ["receiveTimeoutMilliseconds"] = 5000,
                    ["sendTimeoutMilliseconds"] = 5000
                }
            };
        }

        private static void HandleS7DataRead(object? sender, S7DataStoreEventArgs args)
        {
            _logger?.LogInformation(
                "S7 DataRead Area={Area} DB={DbNumber} Start={StartAddress} Length={Length} Data={Data}",
                args.Area,
                args.DbNumber,
                args.StartAddress,
                args.Length,
                FormatBytes(args.Data));
        }

        private static void HandleS7DataWritten(object? sender, S7DataStoreEventArgs args)
        {
            _logger?.LogInformation(
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

            if (string.IsNullOrWhiteSpace(result.Message))
            {
                return info;
            }

            return result.Message + " | " + info;
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
            if (_serverPool != null)
            {
                try
                {
                    _serverPool.Stop(ModbusIdentity, "测试程序退出，停止 Modbus TCP 服务端");
                }
                catch
                {
                }

                try
                {
                    _serverPool.Stop(S7Identity, "测试程序退出，停止 S7 服务端");
                }
                catch
                {
                }

                _serverPool.Dispose();
                _serverPool = null;
            }

            _loggerFactory?.Dispose();
            _loggerFactory = null;
            _logger = null;
        }
    }
}

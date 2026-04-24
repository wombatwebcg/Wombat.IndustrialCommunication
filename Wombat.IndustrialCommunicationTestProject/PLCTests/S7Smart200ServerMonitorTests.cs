using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Linq;
using System.Threading;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    /// <summary>
    /// S7-200 Smart 实体PLC对接时的手动监视测试。
    /// 设置环境变量 WOMBAT_RUN_S7_SMART200_MONITOR=1 后单独运行此测试，
    /// 可在控制台查看原始请求/响应报文以及 DB1.DBB10 的访问情况。
    /// </summary>
    public class S7Smart200ServerMonitorTests : IDisposable
    {
        private ILoggerFactory _loggerFactory;
        private S7TcpServer _server;

        [Fact]
        [Trait("Category", "Manual")]
        public void Start_S7Smart200_Server_And_MonitorPackets()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("WOMBAT_RUN_S7_SMART200_MONITOR"), "1", StringComparison.Ordinal))
            {
                return;
            }

            int monitorMinutes = 30;
            var monitorMinutesText = Environment.GetEnvironmentVariable("WOMBAT_S7_MONITOR_MINUTES");
            if (!string.IsNullOrWhiteSpace(monitorMinutesText) && int.TryParse(monitorMinutesText, out var parsedMinutes) && parsedMinutes > 0)
            {
                monitorMinutes = parsedMinutes;
            }

            _loggerFactory = LoggerFactory.Create(builder =>
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

            var logger = _loggerFactory.CreateLogger<S7Smart200ServerMonitorTests>();

            _server = new S7TcpServer("172.168.1.99", 102);
            _server.UseLogger(logger);
            _server.EnableDataMonitoring(true);
            _server.SetSiemensVersion(SiemensVersion.S7_200Smart);

            _server.DataRead += (sender, args) =>
            {
                logger.LogInformation(
                    "DataRead Area={Area} DB={DbNumber} Start={StartAddress} Length={Length} Data={Data}",
                    args.Area,
                    args.DbNumber,
                    args.StartAddress,
                    args.Length,
                    FormatBytes(args.Data));
            };

            _server.DataWritten += (sender, args) =>
            {
                logger.LogInformation(
                    "DataWritten Area={Area} DB={DbNumber} Start={StartAddress} Length={Length} Data={Data}",
                    args.Area,
                    args.DbNumber,
                    args.StartAddress,
                    args.Length,
                    FormatBytes(args.Data));
            };

            var createDbResult = _server.CreateDataBlock(1, 256);
            Assert.True(createDbResult.IsSuccess, createDbResult.Message);

            var writeDbResult = _server.WriteDB(1, 10, new byte[] { 0x11 });
            Assert.True(writeDbResult.IsSuccess, writeDbResult.Message);

            var listenResult = _server.Listen();
            Assert.True(listenResult.IsSuccess, listenResult.Message);

            logger.LogInformation("S7-200Smart 监视服务器已启动，监听地址: 172.168.1.99:102");
            logger.LogInformation("当前固定测试值: DB1.DBB10 = 0x11");
            logger.LogInformation("请让实体PLC持续读取 DB1.DBB10，控制台会输出原始十六进制报文和 DataRead 事件");
            logger.LogInformation("本次监听时长: {Minutes} 分钟，可手动停止测试运行", monitorMinutes);

            Thread.Sleep(TimeSpan.FromMinutes(monitorMinutes));
        }

        private static string FormatBytes(System.Collections.Generic.IEnumerable<byte> data)
        {
            if (data == null)
            {
                return "(null)";
            }

            var bytes = data.ToArray();
            return bytes.Length == 0 ? "(empty)" : BitConverter.ToString(bytes).Replace("-", " ");
        }

        public void Dispose()
        {
            try
            {
                _server?.Shutdown();
            }
            catch
            {
            }
            finally
            {
                _server?.Dispose();
                _loggerFactory?.Dispose();
            }
        }
    }
}

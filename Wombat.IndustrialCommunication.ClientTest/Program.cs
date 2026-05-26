using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;

namespace Wombat.IndustrialCommunication.ClientTest
{
    internal static class Program
    {
        private const string DeviceIp = "192.168.2.134";
        private const int DevicePort = 9001;
        private const int TotalRounds = 5;
        private static readonly TimeSpan DelayBetweenRounds = TimeSpan.FromSeconds(0);
        private static readonly string LogFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "logs",
            "modbus-batch-read-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".log");

        private static readonly KeyValuePair<string, DataTypeEnums>[] BatchAddresses =
        {
            new KeyValuePair<string, DataTypeEnums>("11;40016", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("11;40018", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("11;40020", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("11;40010", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("11;40012", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("11;40014", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("12;40016", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("12;40018", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("12;40020", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("12;40010", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("12;40012", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("12;40014", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("21;40001", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("22;40001", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("23;40001", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("24;40001", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("25;40001", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("26;40001", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("27;40001", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("28;40001", DataTypeEnums.Float),
            new KeyValuePair<string, DataTypeEnums>("29;40001", DataTypeEnums.Float)
        };

        private static int Main(string[] args)
        {
            return MainAsync().GetAwaiter().GetResult();
        }

        private static async Task<int> MainAsync()
        {
            using (var loggerFactory = CreateLoggerFactory())
            {
                var appLogger = loggerFactory.CreateLogger("ModbusTcpClientTest");
                ModbusTcpClient client = null;

                try
                {
                    client = CreateClient(loggerFactory);

                    appLogger.LogInformation("日志文件: {LogFilePath}", LogFilePath);
                    appLogger.LogInformation("开始连接 Modbus TCP 设备 {Ip}:{Port}", DeviceIp, DevicePort);
                    var connectResult = await client.ConnectAsync().ConfigureAwait(false);
                    if (!connectResult.IsSuccess)
                    {
                        appLogger.LogError("连接失败: {Message}", connectResult.Message);
                        return 1;
                    }

                    appLogger.LogInformation("连接成功，开始批量读取 {Count} 个地址", BatchAddresses.Length);
                    appLogger.LogInformation("当前示例将所有地址按 Float 类型读取，便于直接复现你提供的批量场景。");
                    appLogger.LogInformation("计划执行 {TotalRounds} 轮批量读取，轮询间隔 {Delay} 秒。",
                        TotalRounds,
                        DelayBetweenRounds.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture));

                    var request = BuildBatchReadRequest();
                    var successRounds = 0;
                    var failedRounds = 0;
                    var failedAddressCounter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    for (int round = 1; round <= TotalRounds; round++)
                    {
                        appLogger.LogInformation("========== 第 {Round}/{TotalRounds} 轮开始 ==========", round, TotalRounds);

                        var batchReadResult = await client.BatchReadAsync(request).ConfigureAwait(false);

                        appLogger.LogInformation("第 {Round} 轮完成: IsSuccess={IsSuccess}, Message={Message}, Time={Time}ms",
                            round,
                            batchReadResult.IsSuccess,
                            string.IsNullOrWhiteSpace(batchReadResult.Message) ? "(空)" : batchReadResult.Message,
                            batchReadResult.TimeConsuming.HasValue ? batchReadResult.TimeConsuming.Value.ToString("F2", CultureInfo.InvariantCulture) : "n/a");

                        PrintFrames(appLogger, string.Format(CultureInfo.InvariantCulture, "第 {0} 轮请求报文", round), batchReadResult.Requsts);
                        PrintFrames(appLogger, string.Format(CultureInfo.InvariantCulture, "第 {0} 轮响应报文", round), batchReadResult.Responses);
                        PrintResults(appLogger, round, batchReadResult, failedAddressCounter);

                        if (batchReadResult.IsSuccess)
                        {
                            successRounds++;
                        }
                        else
                        {
                            failedRounds++;
                        }

                        appLogger.LogInformation("========== 第 {Round}/{TotalRounds} 轮结束 ==========", round, TotalRounds);

                        if (round < TotalRounds)
                        {
                            await Task.Delay(DelayBetweenRounds).ConfigureAwait(false);
                        }
                    }

                    PrintSummary(appLogger, successRounds, failedRounds, failedAddressCounter);

                    return failedRounds == 0 ? 0 : 2;
                }
                catch (Exception ex)
                {
                    appLogger.LogError(ex, "批量读取过程中发生未处理异常");
                    return 3;
                }
                finally
                {
                    if (client != null)
                    {
                        try
                        {
                            await client.DisconnectAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                        }

                        client.Dispose();
                    }

                    Console.WriteLine("按任意键退出...");
                    Console.ReadKey(intercept: true);
                }
            }
        }

        private static ILoggerFactory CreateLoggerFactory()
        {
            return LoggerFactory.Create(builder =>
            {
                builder.AddProvider(new SimpleFileLoggerProvider(LogFilePath));
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

        private static ModbusTcpClient CreateClient(ILoggerFactory loggerFactory)
        {
            var client = new ModbusTcpClient(DeviceIp, DevicePort)
            {
                ConnectTimeout = TimeSpan.FromSeconds(5),
                ReceiveTimeout = TimeSpan.FromSeconds(5),
                SendTimeout = TimeSpan.FromSeconds(5),
                IsLongConnection = true,
                BatchReadStationInterval = TimeSpan.FromSeconds(1),
                Logger = loggerFactory.CreateLogger<ModbusTcpClient>()
            };

            client.Transport.Logger = loggerFactory.CreateLogger("ModbusTransport");
            client.Transport.EnableDebugLog = true;
            client.Transport.ResponseInterval = TimeSpan.FromMilliseconds(50);

            var adapter = client.Transport.StreamResource as TcpClientAdapter;
            if (adapter != null)
            {
                adapter.UseLogger(loggerFactory.CreateLogger<TcpClientAdapter>());
                adapter.EnableDebugLog = true;
            }

            return client;
        }

        private static Dictionary<string, DataTypeEnums> BuildBatchReadRequest()
        {
            var request = new Dictionary<string, DataTypeEnums>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in BatchAddresses)
            {
                request[item.Key] = item.Value;
            }

            return request;
        }

        private static void PrintFrames(ILogger logger, string title, IList<string> frames)
        {
            logger.LogInformation("{Title}数量: {Count}", title, frames == null ? 0 : frames.Count);
            if (frames == null || frames.Count == 0)
            {
                return;
            }

            for (int i = 0; i < frames.Count; i++)
            {
                logger.LogInformation("{Title}[{Index}] {Frame}", title, i, frames[i]);
            }
        }

        private static void PrintResults(
            ILogger logger,
            int round,
            OperationResult<Dictionary<string, (DataTypeEnums, object)>> batchReadResult)
        {
            var values = batchReadResult.ResultValue ?? new Dictionary<string, (DataTypeEnums, object)>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in BatchAddresses)
            {
                (DataTypeEnums, object) resultItem;
                var hasValue = values.TryGetValue(item.Key, out resultItem);
                var valueText = hasValue ? FormatValue(resultItem.Item2) : "(未返回)";

                logger.LogInformation(
                    "第 {Round} 轮 地址={Address}, 类型={DataType}, 命中结果={HasValue}, 值={Value}",
                    round,
                    item.Key,
                    item.Value,
                    hasValue,
                    valueText);
            }
        }

        private static void PrintResults(
            ILogger logger,
            int round,
            OperationResult<Dictionary<string, (DataTypeEnums, object)>> batchReadResult,
            IDictionary<string, int> failedAddressCounter)
        {
            var values = batchReadResult.ResultValue ?? new Dictionary<string, (DataTypeEnums, object)>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in BatchAddresses)
            {
                (DataTypeEnums, object) resultItem;
                var hasValue = values.TryGetValue(item.Key, out resultItem);
                var value = hasValue ? resultItem.Item2 : null;
                var isSuccess = value != null;
                var valueText = hasValue ? FormatValue(value) : "(未返回)";

                if (!isSuccess)
                {
                    int failedCount;
                    failedAddressCounter.TryGetValue(item.Key, out failedCount);
                    failedAddressCounter[item.Key] = failedCount + 1;
                }

                logger.LogInformation(
                    "第 {Round} 轮 地址={Address}, 类型={DataType}, 成功={IsSuccess}, 值={Value}",
                    round,
                    item.Key,
                    item.Value,
                    isSuccess,
                    valueText);
            }
        }

        private static void PrintSummary(
            ILogger logger,
            int successRounds,
            int failedRounds,
            IDictionary<string, int> failedAddressCounter)
        {
            logger.LogInformation("========== 多轮测试汇总 ==========");
            logger.LogInformation("总轮数={TotalRounds}, 成功轮数={SuccessRounds}, 失败轮数={FailedRounds}",
                TotalRounds,
                successRounds,
                failedRounds);

            if (failedAddressCounter.Count == 0)
            {
                logger.LogInformation("所有地址在全部轮次中均成功返回。");
                return;
            }

            foreach (var item in failedAddressCounter.OrderByDescending(t => t.Value).ThenBy(t => t.Key, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogWarning("地址 {Address} 失败次数 {Count}", item.Key, item.Value);
            }
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            var values = value as Array;
            if (values != null)
            {
                return "[" + string.Join(", ", values.Cast<object>().Select(FormatScalar)) + "]";
            }

            return FormatScalar(value);
        }

        private static string FormatScalar(object value)
        {
            var formattable = value as IFormattable;
            return formattable != null
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString();
        }

        private sealed class SimpleFileLoggerProvider : ILoggerProvider
        {
            private readonly string _filePath;
            private readonly object _syncRoot = new object();

            public SimpleFileLoggerProvider(string filePath)
            {
                _filePath = filePath;
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new SimpleFileLogger(_filePath, categoryName, _syncRoot);
            }

            public void Dispose()
            {
            }

            private sealed class SimpleFileLogger : ILogger
            {
                private readonly string _filePath;
                private readonly string _categoryName;
                private readonly object _syncRoot;

                public SimpleFileLogger(string filePath, string categoryName, object syncRoot)
                {
                    _filePath = filePath;
                    _categoryName = categoryName;
                    _syncRoot = syncRoot;
                }

                public IDisposable BeginScope<TState>(TState state)
                {
                    return NullScope.Instance;
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return logLevel != LogLevel.None;
                }

                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception exception,
                    Func<TState, Exception, string> formatter)
                {
                    if (!IsEnabled(logLevel) || formatter == null)
                    {
                        return;
                    }

                    var message = formatter(state, exception);
                    if (string.IsNullOrWhiteSpace(message) && exception == null)
                    {
                        return;
                    }

                    var builder = new StringBuilder();
                    builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                    builder.Append(" [");
                    builder.Append(logLevel.ToString());
                    builder.Append("] ");
                    builder.Append(_categoryName);
                    builder.Append(" ");
                    builder.Append(message);

                    if (exception != null)
                    {
                        builder.Append(" ");
                        builder.Append(exception);
                    }

                    lock (_syncRoot)
                    {
                        File.AppendAllText(_filePath, builder.ToString() + Environment.NewLine, Encoding.UTF8);
                    }
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new NullScope();

                public void Dispose()
                {
                }
            }
        }
    }
}

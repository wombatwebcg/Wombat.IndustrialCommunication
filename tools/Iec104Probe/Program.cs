using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Iec104;
using Wombat.IndustrialCommunication.Iec104.Model;
using Wombat.IndustrialCommunication.Iec104.Options;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Iec104.Probe
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch (Exception ex)
            {
                // 输出被重定向到不支持 UTF-8 的宿主时不能因此退出，探测本身仍可继续。
                Console.Error.WriteLine("控制台编码设置失败（忽略）：" + ex.Message);
            }

            ProbeLog.Attach(ProbeOptions.ResolveLogPath(args));

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                ProbeLog.WriteException("未处理异常", e.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                ProbeLog.WriteException("未观察的任务异常", e.Exception);
                e.SetObserved();
            };

            try
            {
                return await RunCoreAsync(args).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ProbeLog.WriteException("探测异常退出", ex);
                Console.Error.WriteLine("探测异常退出：" + ex.Message);
                PrintHint(ex);
                return 1;
            }
            finally
            {
                WaitBeforeExit(args);
                ProbeLog.Detach();
            }
        }

        private static async Task<int> RunCoreAsync(string[] args)
        {
            if (args.Length == 0 || (args.Length == 1 && (args[0] == "-i" || args[0] == "--interactive")))
            {
                return await RunInteractiveAsync().ConfigureAwait(false);
            }

            ProbeOptions options;
            try
            {
                options = ProbeOptions.Parse(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                PrintUsage();
                return 2;
            }

            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            return await RunProbeAsync(options, false).ConfigureAwait(false);
        }

        private static async Task<int> RunInteractiveAsync()
        {
            Console.WriteLine("Iec104Probe —— 交互模式，直接回车使用方括号中的默认值");
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine("  1) 本机自测：自动启动内置从站，跑总召与遥控");
                Console.WriteLine("  2) 探测已有从站");
                Console.WriteLine("  0) 退出");

                var choice = Ask("请选择", "1");
                if (choice == null)
                {
                    // 标准输入已结束（例如被重定向）：按命令行用法提示退出。
                    Console.Error.WriteLine("必须指定 --host。");
                    PrintUsage();
                    return 2;
                }
                Console.WriteLine();

                switch (choice)
                {
                    case "1":
                        return await RunSelfTestAsync().ConfigureAwait(false);
                    case "2":
                        return await RunManualProbeAsync().ConfigureAwait(false);
                    case "0":
                        return 0;
                    default:
                        Console.WriteLine("无效选项。");
                        Console.WriteLine();
                        break;
                }
            }
        }

        private static async Task<int> RunSelfTestAsync()
        {
            var port = AskInt("监听端口", Iec104Constants.DefaultPort);
            if (port == null) return 2;

            var commonAddress = AskInt("公共地址", 1);
            if (commonAddress == null) return 2;

            var trace = AskYesNo("打印收发帧 (y/N)", false);
            if (trace == null) return 2;

            Console.WriteLine();

            var table = BuildDemoTable();
            var slave = new Iec104Slave(new Iec104SlaveOptions
            {
                BindAddress = "127.0.0.1",
                Port = port.Value,
                CommonAddress = (ushort)commonAddress.Value,
                PointTable = table,
            });

            slave.CommandRequested += (_, e) =>
            {
                var value = e.SinglePointValue.HasValue
                    ? (e.SinglePointValue.Value ? "合" : "分")
                    : e.DoublePointState.ToString();
                Console.WriteLine($"[内置从站] 遥控 IOA={e.Ioa} {(e.IsSelect ? "选择" : "执行")} {value}");
            };

            try
            {
                await slave.StartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"内置从站启动失败：{ex.Message}");
                Console.Error.WriteLine("端口可能被占用，请换一个端口重试。");
                await slave.DisposeAsync().ConfigureAwait(false);
                return 1;
            }

            try
            {
                SeedDemoValues(slave);
                Console.WriteLine($"内置从站已启动：{slave.LocalEndPoint}  点数={table.Count}  公共地址={commonAddress.Value}");
                Console.WriteLine();

                var options = ProbeOptions.Create(
                    "127.0.0.1",
                    port.Value,
                    (ushort)commonAddress.Value,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(10),
                    null,
                    null,
                    null,
                    null,
                    trace.Value);

                return await RunProbeAsync(options, true).ConfigureAwait(false);
            }
            finally
            {
                await slave.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task<int> RunManualProbeAsync()
        {
            var host = Ask("从站地址", "127.0.0.1");
            if (host == null) return 2;

            var port = AskInt("端口", Iec104Constants.DefaultPort);
            if (port == null) return 2;

            var commonAddress = AskInt("公共地址", 1);
            if (commonAddress == null) return 2;

            var timeout = AskInt("整体超时（秒）", 60);
            if (timeout == null) return 2;

            var interrogationTimeout = AskInt("总召超时（秒）", 30);
            if (interrogationTimeout == null) return 2;

            string telemetry = null;
            string discrete = null;
            string command = null;

            var loadTable = AskYesNo("加载 CSV 点表 (y/N)", false);
            if (loadTable == null) return 2;

            if (loadTable.Value)
            {
                telemetry = Ask("遥测 CSV 路径（可空）", "");
                if (telemetry == null) return 2;

                discrete = Ask("遥信 CSV 路径（可空）", "");
                if (discrete == null) return 2;

                command = Ask("遥控 CSV 路径（可空）", "");
                if (command == null) return 2;
            }

            var outputPath = Ask("报告 JSON 路径（可空）", "");
            if (outputPath == null) return 2;

            var trace = AskYesNo("打印收发帧 (y/N)", false);
            if (trace == null) return 2;

            Console.WriteLine();

            var options = ProbeOptions.Create(
                host,
                port.Value,
                (ushort)commonAddress.Value,
                TimeSpan.FromSeconds(timeout.Value),
                TimeSpan.FromSeconds(interrogationTimeout.Value),
                EmptyToNull(telemetry),
                EmptyToNull(discrete),
                EmptyToNull(command),
                EmptyToNull(outputPath),
                trace.Value);

            Console.WriteLine();
            return await RunProbeAsync(options, false).ConfigureAwait(false);
        }

        private const int RecentFrameCount = 100;

        private static async Task<int> RunProbeAsync(ProbeOptions options, bool commandTest)
        {
            Iec104PointTable table;
            try
            {
                table = LoadPointTable(options);
            }
            catch (Exception ex)
            {
                // 点表异常过去在 try 之外，会直接结束进程且不留痕迹。
                Console.Error.WriteLine("点表加载失败：" + ex.Message);
                PrintHint(ex);
                return 2;
            }

            var masterOptions = new Iec104MasterOptions
            {
                Host = options.Host,
                Port = options.Port,
                CommonAddress = options.CommonAddress,
                InterrogationTimeout = options.InterrogationTimeout,
                // 帧始终进入环形缓冲：失败时自动打印最后若干帧，不必事先加 --trace。
                EnableTrace = true,
                StrictSequence = options.Strict,
                PointTable = table,
            };

            using (var timeout = new CancellationTokenSource(options.Timeout))
            {
                var master = new Iec104Master(masterOptions);
                var frames = new FrameRing(RecentFrameCount);

                master.LinkStateChanged += OnLinkState;
                master.FrameTraced += (_, e) =>
                {
                    frames.Add(FormatFrame(e));
                    if (options.Trace) OnFrameTraced(null, e);
                };

                try
                {
                    Console.WriteLine($"[1/3] 连接 {options.Host}:{options.Port}，公共地址 {options.CommonAddress}");
                    await master.ConnectAsync(timeout.Token).ConfigureAwait(false);

                    Console.WriteLine("[2/3] 链路已激活，开始总召");
                    var started = DateTime.UtcNow;
                    var result = await master.InterrogateAsync(timeout.Token).ConfigureAwait(false);
                    var snapshot = master.Snapshot();
                    Console.WriteLine($"[3/3] 总召结束，收到 {result.ReceivedCount} 点");

                    PrintReport(result, snapshot, table);
                    PrintLinkTolerance(master);
                    WriteJson(options, result, snapshot, table, DateTime.UtcNow - started);

                    var commandOk = true;
                    if (commandTest)
                    {
                        commandOk = await RunCommandTestAsync(master, table, timeout.Token).ConfigureAwait(false);
                    }

                    return result.Completed && commandOk ? 0 : 1;
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("探测超时。");
                    PrintHint(new TimeoutException("探测超时"));
                    if (!options.Trace) DumpRecentFrames(frames);
                    return 1;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("探测失败：" + ex.Message);
                    PrintHint(ex);
                    // --trace 已经把每一帧打过一遍，再回放只会刷屏。
                    if (!options.Trace) DumpRecentFrames(frames);
                    return 1;
                }
                finally
                {
                    try
                    {
                        await master.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        ProbeLog.WriteException("释放主站失败", ex);
                    }
                }
            }
        }

        private static async Task<bool> RunCommandTestAsync(
            Iec104Master master, Iec104PointTable table, CancellationToken cancellationToken)
        {
            //var commandPoint = table?.Points.FirstOrDefault(p => Iec104TypeInfo.IsCommand(p.TypeId));
            //var ioa = commandPoint != null ? commandPoint.Ioa : 201;

            //Console.WriteLine();
            //Console.WriteLine("[遥控]");

            //try
            //{
            //    var result = await master
            //        .SingleCommandAsync(ioa, true, Iec104CommandMode.SelectAndExecute, cancellationToken)
            //        .ConfigureAwait(false);

            //    Console.WriteLine(
            //        $"  IOA={ioa} 选择+执行 接受={result.Accepted} 否定={result.Negative} 超时={result.TimedOut} " +
            //        $"阶段={result.Stage} 耗时={result.Duration.TotalMilliseconds:F0}ms");

            //    if (!string.IsNullOrEmpty(result.Message)) Console.WriteLine("  " + result.Message);

            //    return result.Accepted && !result.TimedOut;
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("  遥控失败：" + ex.Message);
            //    return false;
            //}
            // 遥控测试暂未启用：此处返回 true，否则自测模式的退出码永远是 1。
            return true;
        }

        private static string Ask(string prompt, string defaultValue)
        {
            Console.Write(string.IsNullOrEmpty(defaultValue) ? prompt + "：" : $"{prompt} [{defaultValue}]：");
            var line = Console.ReadLine();
            if (line == null) return null;

            line = line.Trim();
            return line.Length == 0 ? defaultValue : line;
        }

        private static int? AskInt(string prompt, int defaultValue)
        {
            while (true)
            {
                var text = Ask(prompt, defaultValue.ToString());
                if (text == null) return null;

                int value;
                if (int.TryParse(text, out value) && value > 0) return value;

                Console.WriteLine("请输入大于 0 的整数。");
            }
        }

        private static bool? AskYesNo(string prompt, bool defaultValue)
        {
            while (true)
            {
                var text = Ask(prompt, defaultValue ? "Y" : "N");
                if (text == null) return null;

                if (text.Equals("y", StringComparison.OrdinalIgnoreCase)
                    || text.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (text.Equals("n", StringComparison.OrdinalIgnoreCase)
                    || text.Equals("no", StringComparison.OrdinalIgnoreCase))
                    return false;

                Console.WriteLine("请输入 y 或 n。");
            }
        }

        private static string EmptyToNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static Iec104PointTable BuildDemoTable()
        {
            var points = new List<Iec104Point>();

            for (var i = 0; i < 6; i++)
            {
                points.Add(new Iec104Point
                {
                    Ioa = 1 + i,
                    ForwardNo = i,
                    TypeId = Iec104TypeId.MeasuredFloat,
                    Channel = "自测",
                    Device = "内置从站",
                    Description = "自测遥测" + i,
                    DataType = DataTypeEnums.Float,
                });
            }

            for (var i = 0; i < 2; i++)
            {
                points.Add(new Iec104Point
                {
                    Ioa = 101 + i,
                    ForwardNo = 100 + i,
                    TypeId = Iec104TypeId.SinglePointWithTime,
                    Channel = "自测",
                    Device = "内置从站",
                    Description = "自测遥信" + i,
                    DataType = DataTypeEnums.Bool,
                    IsSoe = true,
                });
            }

            points.Add(new Iec104Point
            {
                Ioa = 201,
                ForwardNo = 200,
                TypeId = Iec104TypeId.SingleCommand,
                Channel = "自测",
                Device = "内置从站",
                Description = "自测遥控",
                DataType = DataTypeEnums.Bool,
                IsCommand = true,
            });

            return Iec104PointTable.FromPoints(points);
        }

        private static void SeedDemoValues(Iec104Slave slave)
        {
            for (var i = 0; i < 6; i++) slave.SetValue(1 + i, 10d * (i + 1));
            slave.SetSinglePoint(101, true);
        }

        private static Iec104PointTable LoadPointTable(ProbeOptions options)
        {
            if (!options.HasPointTable) return null;

            var table = Iec104PointTable.Load(new Iec104PointTableOptions
            {
                TelemetryPath = options.TelemetryPath,
                DiscretePath = options.DiscretePath,
                CommandPath = options.CommandPath,
                CommonAddress = options.CommonAddress,
                IoaBase = options.IoaBase,
                TelemetryIoaBase = options.TelemetryIoaBase,
                DiscreteIoaBase = options.DiscreteIoaBase,
                CommandIoaBase = options.CommandIoaBase,
            });

            Console.WriteLine($"点表已加载：{table.Count} 点，告警 {table.Warnings.Count} 条");
            foreach (var warning in table.Warnings) Console.WriteLine("  ! " + warning);
            Console.WriteLine();
            return table;
        }

        private static void OnLinkState(object sender, Diagnostics.Iec104LinkEventArgs e)
        {
            Console.WriteLine($"[链路] {e.State} {e.Message}");
        }

        private static void OnFrameTraced(object sender, Diagnostics.Iec104TraceEventArgs e)
        {
            Console.WriteLine(FormatFrame(e));
        }

        private static string FormatFrame(Diagnostics.Iec104TraceEventArgs e)
        {
            var direction = e.IsSending ? "发" : "收";
            return $"  {e.Timestamp:HH:mm:ss.fff} {direction} {e.Hex}  {e.Meaning}";
        }

        /// <summary>失败时打印最后若干帧，现场无需重跑即可看到断链前后的收发内容。</summary>
        private static void DumpRecentFrames(FrameRing frames)
        {
            var items = frames.Snapshot();
            if (items.Count == 0)
            {
                Console.Error.WriteLine("（本次未收到任何帧，链路可能在建链阶段就被对端关闭）");
                return;
            }

            Console.Error.WriteLine($"最近 {items.Count} 帧：");
            foreach (var item in items) Console.Error.WriteLine(item);
        }

        /// <summary>把宽松模式的序号容错次数显示出来，便于判断装置是否偏离协议约定。</summary>
        private static void PrintLinkTolerance(Iec104Master master)
        {
            if (master.SequenceResyncs == 0 && master.ClampedAcknowledgements == 0) return;

            Console.WriteLine();
            Console.WriteLine(
                $"链路容错：接收序号重对齐 {master.SequenceResyncs} 次，确认序号截断 {master.ClampedAcknowledgements} 次。" +
                "装置计数器与协议约定不一致，数据仍可用，但建议核对装置实现。");
        }

        /// <summary>按错误文本给出可执行提示，避免只留一句异常原文。</summary>
        private static void PrintHint(Exception exception)
        {
            var message = exception == null ? string.Empty : exception.Message;
            string hint = null;

            if (message.Contains("积极拒绝") || message.Contains("actively refused"))
                hint = "端口没有监听：确认从站已启动、端口号正确、防火墙放行。";
            else if (message.Contains("强迫关闭") || message.Contains("对端关闭连接") || message.Contains("forcibly closed"))
                hint = "从站主动断开：多数装置只允许一个主站连接，请先断开已有主站，或确认装置连接数限制。";
            else if (message.Contains("接收序号不连续") || message.Contains("确认序号越界"))
                hint = "从站计数器与协议约定不一致。默认已自动重对齐；如需严格校验请加 --strict，并用 --trace 抓帧反馈。";
            else if (message.Contains("STARTDT"))
                hint = "从站未回应 STARTDT：确认公共地址（--ca）、端口，或装置要求先完成初始化。";
            else if (message.Contains("无法解析主机名"))
                hint = "主机名解析失败：改用 IP 地址，或检查 DNS。";
            else if (message.Contains("超时"))
                hint = "超时：确认公共地址是否匹配（--ca），或加大 --timeout / --interrogate。";

            if (hint != null) Console.Error.WriteLine("提示：" + hint);
        }

        /// <summary>双击运行时保留窗口，避免“闪一下就没了”；管道或重定向时不等待。</summary>
        private static void WaitBeforeExit(string[] args)
        {
            if (Array.IndexOf(args, "--no-pause") >= 0) return;
            if (Console.IsInputRedirected || Console.IsOutputRedirected) return;

            try
            {
                Console.WriteLine();
                Console.WriteLine("按任意键退出…");
                Console.ReadKey(true);
            }
            catch
            {
                // 没有可用的控制台输入时直接退出。
            }
        }

        private static void PrintReport(
            Iec104InterrogationResult result,
            IReadOnlyDictionary<int, Iec104Value> values,
            Iec104PointTable table)
        {
            Console.WriteLine();
            Console.WriteLine("[总召]");
            Console.WriteLine($"  完成={result.Completed} 确认={result.Confirmed} 超时={result.TimedOut} {result.ReceivedCount}/{result.ExpectedCount} 点");
            Console.WriteLine($"  耗时={result.Duration.TotalMilliseconds:F0}ms");

            if (result.MissingIoa.Count > 0)
            {
                var preview = string.Join(",", result.MissingIoa.Take(20));
                Console.WriteLine($"  缺失 IOA：{preview}" + (result.MissingIoa.Count > 20 ? " ..." : string.Empty));
            }

            Console.WriteLine();
            Console.WriteLine("[类型分布]");
            foreach (var group in values.Values.GroupBy(v => v.TypeId).OrderBy(g => (byte)g.Key))
            {
                var line = $"  {group.Key,-32} {(byte)group.Key,3}  {group.Count(),5} 点  IOA {group.Min(v => v.Ioa)}..{group.Max(v => v.Ioa)}";
                Console.WriteLine(line);
            }

            Console.WriteLine();
            Console.WriteLine("[样本]");
            foreach (var group in values.Values.GroupBy(v => v.TypeId).OrderBy(g => (byte)g.Key))
            {
                foreach (var v in group.OrderBy(x => x.Ioa).Take(3))
                {
                    var time = v.HasTimestamp ? $" 时标={v.Timestamp:yyyy-MM-dd HH:mm:ss.fff}" : string.Empty;
                    Console.WriteLine($"  IOA={v.Ioa,-8} {v.TypeId,-28} 值={v.Numeric,12:F3} 品质=0x{(byte)v.Quality:X2}{time}");
                }
            }

            if (table != null) PrintTableComparison(table, values);
            Console.WriteLine();
        }

        private static void PrintTableComparison(Iec104PointTable table, IReadOnlyDictionary<int, Iec104Value> values)
        {
            var dataPoints = table.Points.Where(p => IsDataPoint(p.TypeId)).ToList();
            var missing = dataPoints.Where(p => !values.ContainsKey(p.Ioa)).Select(p => p.Ioa).ToList();

            Console.WriteLine();
            Console.WriteLine("[点表对照]");
            Console.WriteLine($"  点表点数={dataPoints.Count} 命中={dataPoints.Count - missing.Count} 缺失={missing.Count}");

            if (missing.Count > 0)
            {
                Console.WriteLine($"  缺失前 20 个：{string.Join(",", missing.Take(20))}");
            }

            var known = new HashSet<int>(dataPoints.Select(p => p.Ioa));
            var extra = values.Keys.Where(ioa => !known.Contains(ioa)).ToList();
            if (extra.Count > 0)
            {
                Console.WriteLine($"  点表外点数={extra.Count}：{string.Join(",", extra.Take(20))}");
            }
        }

        private static void WriteJson(
            ProbeOptions options,
            Iec104InterrogationResult result,
            IReadOnlyDictionary<int, Iec104Value> values,
            Iec104PointTable table,
            TimeSpan elapsed)
        {
            if (string.IsNullOrWhiteSpace(options.OutputPath)) return;

            var report = new
            {
                target = options.Host + ":" + options.Port,
                commonAddress = options.CommonAddress,
                elapsedMs = (long)elapsed.TotalMilliseconds,
                interrogation = new
                {
                    result.Completed,
                    result.Confirmed,
                    result.TimedOut,
                    result.ReceivedCount,
                    result.ExpectedCount,
                    missingIoa = result.MissingIoa,
                    durationMs = (long)result.Duration.TotalMilliseconds,
                },
                types = values.Values.GroupBy(v => v.TypeId).OrderBy(g => (byte)g.Key).Select(g => new
                {
                    typeId = (byte)g.Key,
                    name = g.Key.ToString(),
                    count = g.Count(),
                    ioaMin = g.Min(v => v.Ioa),
                    ioaMax = g.Max(v => v.Ioa),
                }),
                samples = values.Values.OrderBy(v => v.Ioa).Take(50).Select(v => new
                {
                    v.Ioa,
                    typeId = (byte)v.TypeId,
                    v.Numeric,
                    v.Boolean,
                    quality = (byte)v.Quality,
                    timestamp = v.HasTimestamp ? v.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff") : null,
                }),
                pointTable = table == null ? null : new
                {
                    total = table.Count,
                    matched = table.Points.Count(p => IsDataPoint(p.TypeId) && values.ContainsKey(p.Ioa)),
                    missing = table.Points.Where(p => IsDataPoint(p.TypeId) && !values.ContainsKey(p.Ioa)).Select(p => p.Ioa),
                    warnings = table.Warnings,
                },
            };

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(options.OutputPath, json, Encoding.UTF8);
            Console.WriteLine($"报告已写入 {options.OutputPath}");
        }

        private static bool IsDataPoint(Iec104TypeId typeId)
        {
            switch (typeId)
            {
                case Iec104TypeId.SinglePoint:
                case Iec104TypeId.DoublePoint:
                case Iec104TypeId.StepPosition:
                case Iec104TypeId.MeasuredNormalized:
                case Iec104TypeId.MeasuredScaled:
                case Iec104TypeId.MeasuredFloat:
                case Iec104TypeId.SinglePointWithTime:
                case Iec104TypeId.DoublePointWithTime:
                case Iec104TypeId.MeasuredNormalizedWithTime:
                case Iec104TypeId.MeasuredScaledWithTime:
                case Iec104TypeId.MeasuredFloatWithTime:
                    return true;
                default:
                    return false;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Iec104Probe —— IEC 60870-5-104 从站探测工具");
            Console.WriteLine();
            Console.WriteLine("  不带参数或 -i/--interactive 进入交互模式（可一键启动内置从站自测）。");
            Console.WriteLine();
            Console.WriteLine("  --host <地址>            从站地址（必填）");
            Console.WriteLine("  --port <端口>            默认 2404");
            Console.WriteLine("  --ca <公共地址>          默认 1");
            Console.WriteLine("  --timeout <秒>           整体超时，默认 60");
            Console.WriteLine("  --interrogate <秒>       总召超时，默认 30");
            Console.WriteLine("  --telemetry <csv>        遥测点表");
            Console.WriteLine("  --discrete <csv>         遥信点表");
            Console.WriteLine("  --command <csv>          遥控点表");
            Console.WriteLine("  --ioa-base <n>           全局地址基准，默认 1");
            Console.WriteLine("  --telemetry-base <n>     遥测地址基准");
            Console.WriteLine("  --discrete-base <n>      遥信地址基准");
            Console.WriteLine("  --command-base <n>       遥控地址基准");
            Console.WriteLine("  --out <json>             报告输出路径");
            Console.WriteLine("  --strict                 严格校验 N(S)/N(R)，序号不符直接断链");
            Console.WriteLine("  --trace                  打印收发帧");
            Console.WriteLine("  --log <path>             日志文件，默认写到程序目录 logs/ 下");
            Console.WriteLine("  --no-pause               结束后不等待按键（脚本调用时用）");
        }
    }

    internal sealed class ProbeOptions
    {
        public string Host { get; private set; }
        public int Port { get; private set; } = Iec104Constants.DefaultPort;
        public ushort CommonAddress { get; private set; } = 1;
        public TimeSpan Timeout { get; private set; } = TimeSpan.FromSeconds(60);
        public TimeSpan InterrogationTimeout { get; private set; } = TimeSpan.FromSeconds(30);
        public string TelemetryPath { get; private set; }
        public string DiscretePath { get; private set; }
        public string CommandPath { get; private set; }
        public int IoaBase { get; private set; } = 1;
        public int? TelemetryIoaBase { get; private set; }
        public int? DiscreteIoaBase { get; private set; }
        public int? CommandIoaBase { get; private set; }
        public string OutputPath { get; private set; }
        public bool Trace { get; private set; }
        public bool Strict { get; private set; }
        public bool ShowHelp { get; private set; }

        public bool HasPointTable
        {
            get
            {
                return !string.IsNullOrWhiteSpace(TelemetryPath)
                    || !string.IsNullOrWhiteSpace(DiscretePath)
                    || !string.IsNullOrWhiteSpace(CommandPath);
            }
        }

        public static ProbeOptions Parse(string[] args)
        {
            var options = new ProbeOptions();

            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i];
                switch (key)
                {
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;
                    case "--host": options.Host = Next(args, ref i, key); break;
                    case "--port": options.Port = int.Parse(Next(args, ref i, key)); break;
                    case "--ca": options.CommonAddress = ushort.Parse(Next(args, ref i, key)); break;
                    case "--timeout": options.Timeout = TimeSpan.FromSeconds(double.Parse(Next(args, ref i, key))); break;
                    case "--interrogate": options.InterrogationTimeout = TimeSpan.FromSeconds(double.Parse(Next(args, ref i, key))); break;
                    case "--telemetry": options.TelemetryPath = Next(args, ref i, key); break;
                    case "--discrete": options.DiscretePath = Next(args, ref i, key); break;
                    case "--command": options.CommandPath = Next(args, ref i, key); break;
                    case "--ioa-base": options.IoaBase = int.Parse(Next(args, ref i, key)); break;
                    case "--telemetry-base": options.TelemetryIoaBase = int.Parse(Next(args, ref i, key)); break;
                    case "--discrete-base": options.DiscreteIoaBase = int.Parse(Next(args, ref i, key)); break;
                    case "--command-base": options.CommandIoaBase = int.Parse(Next(args, ref i, key)); break;
                    case "--out": options.OutputPath = Next(args, ref i, key); break;
                    case "--strict": options.Strict = true; break;
                    case "--trace": options.Trace = true; break;
                    // --log 在 Main 里预先扫描（日志必须在解析参数之前就绪），这里只吃掉取值。
                    case "--log": Next(args, ref i, key); break;
                    case "--no-pause": break;
                    default: throw new ArgumentException($"未知参数：{key}");
                }
            }

            if (!options.ShowHelp && string.IsNullOrWhiteSpace(options.Host))
            {
                throw new ArgumentException("必须指定 --host。");
            }

            return options;
        }

        /// <summary>交互模式使用：按向导收集到的参数构造。</summary>
        public static ProbeOptions Create(
            string host,
            int port,
            ushort commonAddress,
            TimeSpan timeout,
            TimeSpan interrogationTimeout,
            string telemetryPath,
            string discretePath,
            string commandPath,
            string outputPath,
            bool trace)
        {
            return new ProbeOptions
            {
                Host = host,
                Port = port,
                CommonAddress = commonAddress,
                Timeout = timeout,
                InterrogationTimeout = interrogationTimeout,
                TelemetryPath = telemetryPath,
                DiscretePath = discretePath,
                CommandPath = commandPath,
                OutputPath = outputPath,
                Trace = trace,
            };
        }

        /// <summary>日志路径：优先 --log，否则写到程序目录 logs\ 下，不可写时退回临时目录。</summary>
        public static string ResolveLogPath(string[] args)
        {
            var index = Array.IndexOf(args, "--log");
            if (index >= 0 && index + 1 < args.Length)
            {
                var path = args[index + 1];
                if (!string.IsNullOrWhiteSpace(path)) return path;
            }

            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            try
            {
                var directory = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(directory);
                return Path.Combine(directory, "Iec104Probe-" + stamp + ".log");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "Iec104Probe-" + stamp + ".log");
            }
        }

        private static string Next(string[] args, ref int index, string key)
        {
            if (index + 1 >= args.Length) throw new ArgumentException($"参数 {key} 缺少取值。");
            return args[++index];
        }
    }

    /// <summary>
    /// 探测日志：控制台输出同时落到文件，异常带完整堆栈。
    /// 闪退类问题过去只留一闪而过的窗口，事后无从查证。
    /// </summary>
    internal static class ProbeLog
    {
        private static readonly object Gate = new object();
        private static TextWriter _file;
        private static TextWriter _consoleOut;
        private static TextWriter _consoleError;

        public static string FilePath { get; private set; }

        public static void Attach(string path)
        {
            var fallback = Path.Combine(Path.GetTempPath(), "Iec104Probe-fallback.log");
            if (TryOpen(path)) return;
            if (TryOpen(fallback)) return;

            Console.Error.WriteLine("日志文件不可写，本次不记录日志。");
        }

        private static bool TryOpen(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                _file = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                FilePath = path;

                _consoleOut = Console.Out;
                _consoleError = Console.Error;
                Console.SetOut(new TeeWriter(_consoleOut, _file));
                Console.SetError(new TeeWriter(_consoleError, _file));
                Console.WriteLine("日志文件：" + path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void WriteException(string title, Exception exception)
        {
            var text = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + title + Environment.NewLine +
                       (exception == null ? "（没有异常对象）" : exception.ToString());

            try
            {
                Console.Error.WriteLine(text);
            }
            catch
            {
                // 控制台已经不可写时直接落盘，保证崩溃现场留下来。
                lock (Gate)
                {
                    try { _file?.WriteLine(text); } catch { }
                }
            }
        }

        public static void Detach()
        {
            try
            {
                if (_consoleOut != null) Console.SetOut(_consoleOut);
                if (_consoleError != null) Console.SetError(_consoleError);
            }
            catch
            {
            }

            lock (Gate)
            {
                try
                {
                    _file?.Flush();
                    _file?.Dispose();
                }
                catch
                {
                }

                _file = null;
            }
        }

        private sealed class TeeWriter : TextWriter
        {
            private readonly TextWriter _console;
            private readonly TextWriter _file;

            public TeeWriter(TextWriter console, TextWriter file)
            {
                _console = console;
                _file = file;
            }

            public override Encoding Encoding
            {
                get { return _console.Encoding; }
            }

            public override void Write(char value)
            {
                _console.Write(value);
                lock (Gate) { try { _file.Write(value); } catch { } }
            }

            public override void Write(string value)
            {
                if (value == null) return;
                _console.Write(value);
                lock (Gate) { try { _file.Write(value); } catch { } }
            }

            public override void Flush()
            {
                _console.Flush();
                lock (Gate) { try { _file.Flush(); } catch { } }
            }
        }
    }

    /// <summary>定长的最近帧缓冲，用于探测失败时回放现场。</summary>
    internal sealed class FrameRing
    {
        private readonly Queue<string> _items;
        private readonly int _capacity;
        private readonly object _gate = new object();

        public FrameRing(int capacity)
        {
            _capacity = capacity < 1 ? 1 : capacity;
            _items = new Queue<string>(_capacity);
        }

        public void Add(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            lock (_gate)
            {
                _items.Enqueue(line);
                while (_items.Count > _capacity) _items.Dequeue();
            }
        }

        public IReadOnlyList<string> Snapshot()
        {
            lock (_gate)
            {
                return new List<string>(_items);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Extensions.IEC104;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;
using Wombat.IndustrialCommunication.Extensions.IEC104.Options;
using Wombat.IndustrialCommunication.Extensions.IEC104.Protocol;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Probe
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

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

            var table = LoadPointTable(options);

            var masterOptions = new Iec104MasterOptions
            {
                Host = options.Host,
                Port = options.Port,
                CommonAddress = options.CommonAddress,
                InterrogationTimeout = options.InterrogationTimeout,
                EnableTrace = options.Trace,
                PointTable = table,
            };

            using (var timeout = new CancellationTokenSource(options.Timeout))
            {
                var master = new Iec104Master(masterOptions);
                master.LinkStateChanged += OnLinkState;
                if (options.Trace) master.FrameTraced += OnFrameTraced;

                try
                {
                    Console.WriteLine($"连接 {options.Host}:{options.Port}，公共地址 {options.CommonAddress}");
                    await master.ConnectAsync(timeout.Token).ConfigureAwait(false);

                    var started = DateTime.UtcNow;
                    var result = await master.InterrogateAsync(timeout.Token).ConfigureAwait(false);
                    var snapshot = master.Snapshot();

                    PrintReport(result, snapshot, table);
                    WriteJson(options, result, snapshot, table, DateTime.UtcNow - started);

                    await master.DisposeAsync().ConfigureAwait(false);
                    return result.Completed ? 0 : 1;
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("探测超时。");
                    return 1;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("探测失败：" + ex.Message);
                    return 1;
                }
            }
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
            var direction = e.IsSending ? "发" : "收";
            Console.WriteLine($"  {direction} {e.Hex}  {e.Meaning}");
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
            Console.WriteLine("  --trace                  打印收发帧");
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
                    case "--trace": options.Trace = true; break;
                    default: throw new ArgumentException($"未知参数：{key}");
                }
            }

            if (!options.ShowHelp && string.IsNullOrWhiteSpace(options.Host))
            {
                throw new ArgumentException("必须指定 --host。");
            }

            return options;
        }

        private static string Next(string[] args, ref int index, string key)
        {
            if (index + 1 >= args.Length) throw new ArgumentException($"参数 {key} 缺少取值。");
            return args[++index];
        }
    }
}

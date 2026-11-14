using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Extensions.IEC104;
using Wombat.IndustrialCommunication.Extensions.IEC104.Diagnostics;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;
using Wombat.IndustrialCommunication.Extensions.IEC104.Options;
using Wombat.IndustrialCommunication.Extensions.IEC104.Protocol;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Simulator
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            SimulatorOptions options;
            try
            {
                options = SimulatorOptions.Parse(args);
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

            var table = options.HasPointTable ? LoadTable(options) : BuildDemoTable();
            var slave = new Iec104Slave(new Iec104SlaveOptions
            {
                BindAddress = options.BindAddress,
                Port = options.Port,
                CommonAddress = options.CommonAddress,
                PointTable = table,
                CyclicPeriod = TimeSpan.FromSeconds(options.CyclicSeconds),
                SendEndOfInitialization = options.InitializationFrame,
                RequireSelectBeforeOperate = options.RequireSelectBeforeOperate,
                EnableTrace = options.Trace,
            });

            slave.ClientConnected += (_, e) =>
                Console.WriteLine($"[连接] {e.RemoteEndPoint} 当前 {slave.ClientCount} 个客户端");
            slave.ClientDisconnected += (_, e) =>
                Console.WriteLine($"[断开] 当前 {slave.ClientCount} 个客户端");

            slave.CommandRequested += (_, e) =>
            {
                var value = e.SinglePointValue.HasValue ? (e.SinglePointValue.Value ? "合" : "分") : e.DoublePointState.ToString();
                Console.WriteLine($"[遥控] IOA={e.Ioa} {(e.IsSelect ? "选择" : "执行")} {value} 已接受");
            };

            if (options.Trace)
            {
                slave.FrameTraced += (_, e) =>
                {
                    var direction = e.IsSending ? "发" : "收";
                    Console.WriteLine($"  {direction} {e.Hex}  {e.Meaning}");
                };
            }

            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                await slave.StartAsync(cts.Token).ConfigureAwait(false);
                Console.WriteLine($"从站已启动：{slave.LocalEndPoint}  点数={table.Count}  公共地址={options.CommonAddress}");
                Console.WriteLine("命令：set <IOA> <值> | toggle <IOA> | invalid <IOA> <0|1> | status | quit");

                var simulate = options.Simulate
                    ? Task.Run(() => SimulateAsync(slave, table, cts.Token))
                    : Task.CompletedTask;

                await ConsoleLoopAsync(slave, table, cts).ConfigureAwait(false);

                cts.Cancel();
                try { await simulate.ConfigureAwait(false); } catch { }
                await slave.StopAsync().ConfigureAwait(false);
            }

            return 0;
        }

        private static async Task ConsoleLoopAsync(
            Iec104Slave slave, Iec104PointTable table, CancellationTokenSource cts)
        {
            while (!cts.IsCancellationRequested)
            {
                string line;
                try
                {
                    line = await Console.In.ReadLineAsync().ConfigureAwait(false);
                }
                catch
                {
                    break;
                }

                if (line == null)
                {
                    // 非交互运行（stdin 被重定向或来自管道）时保持服务，直到 Ctrl+C 或进程终止。
                    if (Console.IsInputRedirected)
                    {
                        try { await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false); } catch { }
                        return;
                    }

                    break;
                }
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLowerInvariant();

                try
                {
                    switch (command)
                    {
                        case "quit":
                        case "exit":
                            cts.Cancel();
                            return;

                        case "status":
                            Console.WriteLine($"状态：监听={slave.IsListening} 客户端={slave.ClientCount} 点数={table.Count}");
                            break;

                        case "set":
                            Require(parts, 3);
                            slave.SetValue(int.Parse(parts[1]), double.Parse(parts[2]));
                            Console.WriteLine($"IOA {parts[1]} 遥测置为 {parts[2]}");
                            break;

                        case "toggle":
                            Require(parts, 2);
                            {
                                var ioa = int.Parse(parts[1]);
                                Iec104Value value;
                                var next = !(slave.DataStore.TryGet(ioa, out value) && value.Boolean);
                                slave.SetSinglePoint(ioa, next);
                                Console.WriteLine($"IOA {ioa} 遥信置为 {(next ? "合" : "分")}");
                            }
                            break;

                        case "invalid":
                            Require(parts, 3);
                            {
                                var ioa = int.Parse(parts[1]);
                                var invalid = parts[2] != "0";
                                slave.SetQuality(ioa, invalid ? Iec104QualityFlags.Invalid : Iec104QualityFlags.None);
                                Console.WriteLine($"IOA {ioa} 品质 {(invalid ? "无效" : "有效")}");
                            }
                            break;

                        default:
                            Console.WriteLine("未知命令。");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("命令执行失败：" + ex.Message);
                }
            }
        }

        private static void Require(string[] parts, int count)
        {
            if (parts.Length < count) throw new ArgumentException("参数不足。");
        }

        private static async Task SimulateAsync(
            Iec104Slave slave, Iec104PointTable table, CancellationToken cancellationToken)
        {
            var telemetry = table.Points
                .Where(p => p.TypeId == Iec104TypeId.MeasuredFloat || p.TypeId == Iec104TypeId.MeasuredFloatWithTime)
                .Take(20)
                .ToList();

            var discrete = table.Points
                .Where(p => p.TypeId == Iec104TypeId.SinglePoint || p.TypeId == Iec104TypeId.SinglePointWithTime)
                .Take(6)
                .ToList();

            var random = new Random(20261113);
            var phase = 0d;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                    phase += 0.25;

                    for (var i = 0; i < telemetry.Count; i++)
                    {
                        var value = 50d + 40d * Math.Sin(phase + i * 0.1) + random.NextDouble();
                        slave.SetValue(telemetry[i].Ioa, Math.Round(value, 3));
                    }

                    for (var i = 0; i < discrete.Count; i++)
                    {
                        if (random.Next(4) == 0)
                        {
                            Iec104Value current;
                            var next = !(slave.DataStore.TryGet(discrete[i].Ioa, out current) && current.Boolean);
                            slave.SetSinglePoint(discrete[i].Ioa, next);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static Iec104PointTable LoadTable(SimulatorOptions options)
        {
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
                PreferTimeTaggedSoe = options.SoeWithTime,
                CommandTypeId = options.CommandTypeId,
            });

            Console.WriteLine($"点表已加载：{table.Count} 点");
            foreach (var warning in table.Warnings) Console.WriteLine("  ! " + warning);
            return table;
        }

        private static Iec104PointTable BuildDemoTable()
        {
            var points = new List<Iec104Point>();

            for (var i = 0; i < 10; i++)
            {
                points.Add(new Iec104Point
                {
                    Ioa = 1 + i,
                    ForwardNo = i,
                    TypeId = Iec104TypeId.MeasuredFloat,
                    Channel = "演示",
                    Device = "模拟仪表",
                    Description = "演示遥测" + i,
                    DataType = DataTypeEnums.Float,
                });
            }

            for (var i = 0; i < 4; i++)
            {
                points.Add(new Iec104Point
                {
                    Ioa = 101 + i,
                    ForwardNo = 100 + i,
                    TypeId = Iec104TypeId.SinglePointWithTime,
                    Channel = "演示",
                    Device = "模拟保护",
                    Description = "演示遥信" + i,
                    DataType = DataTypeEnums.Bool,
                    IsSoe = true,
                });
            }

            points.Add(new Iec104Point
            {
                Ioa = 201,
                ForwardNo = 200,
                TypeId = Iec104TypeId.SingleCommand,
                Channel = "演示",
                Device = "模拟保护",
                Description = "演示遥控",
                DataType = DataTypeEnums.Bool,
                IsCommand = true,
            });

            Console.WriteLine("未指定点表，使用内置演示点表（10 遥测 / 4 遥信 / 1 遥控）。");
            return Iec104PointTable.FromPoints(points);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Iec104SlaveSimulator —— 用 CSV 点表模拟一台 104 从站");
            Console.WriteLine();
            Console.WriteLine("  --telemetry <csv>        遥测点表");
            Console.WriteLine("  --discrete <csv>         遥信点表");
            Console.WriteLine("  --command <csv>          遥控点表");
            Console.WriteLine("  --port <端口>            默认 2404");
            Console.WriteLine("  --bind <地址>            默认 0.0.0.0");
            Console.WriteLine("  --ca <公共地址>          默认 1");
            Console.WriteLine("  --ioa-base <n>           全局地址基准，默认 1");
            Console.WriteLine("  --telemetry-base <n>     遥测地址基准");
            Console.WriteLine("  --discrete-base <n>      遥信地址基准");
            Console.WriteLine("  --command-base <n>       遥控地址基准");
            Console.WriteLine("  --cyclic <秒>            周期上送间隔，默认 0 关闭");
            Console.WriteLine("  --soe <true|false>       遥信是否带时标，默认 true");
            Console.WriteLine("  --select-before-operate  强制选择-执行");
            Console.WriteLine("  --no-init-frame          不发初始化结束帧");
            Console.WriteLine("  --simulate               自动生成变化数据");
            Console.WriteLine("  --trace                  打印收发帧");
        }
    }

    internal sealed class SimulatorOptions
    {
        public string TelemetryPath { get; private set; }
        public string DiscretePath { get; private set; }
        public string CommandPath { get; private set; }
        public string BindAddress { get; private set; } = "0.0.0.0";
        public int Port { get; private set; } = Iec104Constants.DefaultPort;
        public ushort CommonAddress { get; private set; } = 1;
        public int IoaBase { get; private set; } = 1;
        public int? TelemetryIoaBase { get; private set; }
        public int? DiscreteIoaBase { get; private set; }
        public int? CommandIoaBase { get; private set; }
        public double CyclicSeconds { get; private set; }
        public bool SoeWithTime { get; private set; } = true;
        public bool RequireSelectBeforeOperate { get; private set; }
        public bool InitializationFrame { get; private set; } = true;
        public bool Simulate { get; private set; }
        public bool Trace { get; private set; }
        public bool ShowHelp { get; private set; }
        public Iec104TypeId CommandTypeId { get; private set; } = Iec104TypeId.SingleCommand;

        public bool HasPointTable
        {
            get
            {
                return !string.IsNullOrWhiteSpace(TelemetryPath)
                    || !string.IsNullOrWhiteSpace(DiscretePath)
                    || !string.IsNullOrWhiteSpace(CommandPath);
            }
        }

        public static SimulatorOptions Parse(string[] args)
        {
            var options = new SimulatorOptions();

            for (var i = 0; i < args.Length; i++)
            {
                var key = args[i];
                switch (key)
                {
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;
                    case "--telemetry": options.TelemetryPath = Next(args, ref i, key); break;
                    case "--discrete": options.DiscretePath = Next(args, ref i, key); break;
                    case "--command": options.CommandPath = Next(args, ref i, key); break;
                    case "--port": options.Port = int.Parse(Next(args, ref i, key)); break;
                    case "--bind": options.BindAddress = Next(args, ref i, key); break;
                    case "--ca": options.CommonAddress = ushort.Parse(Next(args, ref i, key)); break;
                    case "--ioa-base": options.IoaBase = int.Parse(Next(args, ref i, key)); break;
                    case "--telemetry-base": options.TelemetryIoaBase = int.Parse(Next(args, ref i, key)); break;
                    case "--discrete-base": options.DiscreteIoaBase = int.Parse(Next(args, ref i, key)); break;
                    case "--command-base": options.CommandIoaBase = int.Parse(Next(args, ref i, key)); break;
                    case "--cyclic": options.CyclicSeconds = double.Parse(Next(args, ref i, key)); break;
                    case "--soe": options.SoeWithTime = bool.Parse(Next(args, ref i, key)); break;
                    case "--select-before-operate": options.RequireSelectBeforeOperate = true; break;
                    case "--no-init-frame": options.InitializationFrame = false; break;
                    case "--double-command": options.CommandTypeId = Iec104TypeId.DoubleCommand; break;
                    case "--simulate": options.Simulate = true; break;
                    case "--trace": options.Trace = true; break;
                    default: throw new ArgumentException($"未知参数：{key}");
                }
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;
using Wombat.IndustrialCommunication.Extensions.IEC104.Options;
using Wombat.IndustrialCommunication.Extensions.IEC104.Protocol;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge
{
    /// <summary>
    /// 桥接配置文件。所有字段可选，缺省值即生产默认值；CSV 相对路径按配置文件所在目录解析。
    /// 校验只收集问题，不抛异常；一次性改完再重启，别一条条试。
    /// </summary>
    public sealed class ModbusBridgeConfiguration
    {
        private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

        private string _baseDirectory = Environment.CurrentDirectory;

        public ModbusEndpointSettings Modbus { get; set; } = new ModbusEndpointSettings();

        public CommandChannelSettings Command { get; set; } = new CommandChannelSettings();

        public StaleDataSettings Stale { get; set; } = new StaleDataSettings();

        public Iec104ConnectionSettings Iec104 { get; set; } = new Iec104ConnectionSettings();

        public PointTableSettings PointTable { get; set; } = new PointTableSettings();

        /// <summary>从文件加载。</summary>
        public static ModbusBridgeConfiguration Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("配置路径不能为空。", nameof(path));

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                throw new ModbusBridgeConfigurationException(
                    new[] { string.Format("读取配置文件失败：{0}（{1}）", path, ex.Message) });
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            return Parse(json, string.IsNullOrEmpty(directory) ? Environment.CurrentDirectory : directory);
        }

        /// <summary>
        /// 从 JSON 文本加载，baseDirectory 用于解析 CSV 相对路径。
        /// 这里用的是反射式序列化，只在启动时调用一次；AOT/裁剪场景需要保留这些配置类型。
        /// </summary>
#pragma warning disable IL2026, IL3050
        public static ModbusBridgeConfiguration Parse(string json, string baseDirectory = null)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            ModbusBridgeConfiguration configuration;
            try
            {
                configuration = JsonSerializer.Deserialize<ModbusBridgeConfiguration>(json, SerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new ModbusBridgeConfigurationException(new[]
                {
                    string.Format("配置文件解析失败（行 {0}，位置 {1}）：{2}",
                        ex.LineNumber, ex.BytePositionInLine, ex.Message),
                });
            }

            if (configuration == null)
                throw new ModbusBridgeConfigurationException(new[] { "配置文件内容为空。" });

            configuration.Normalize(baseDirectory);
            return configuration;
        }
#pragma warning restore IL2026, IL3050

        /// <summary>逐项校验，返回全部问题。</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Modbus.BindAddress)) errors.Add("modbus.bindAddress 不能为空。");
            if (Modbus.Port < 1 || Modbus.Port > 65535) errors.Add("modbus.port 必须在 1..65535 之间。");
            if (Modbus.UnitId < 1 || Modbus.UnitId > 247) errors.Add("modbus.unitId 必须在 1..247 之间。");
            if (Modbus.DiscreteBase < 0) errors.Add("modbus.discreteBase 不能为负。");
            if (Modbus.TelemetryBase < 0) errors.Add("modbus.telemetryBase 不能为负。");
            if (Modbus.CommandBase < 0) errors.Add("modbus.commandBase 不能为负。");
            if (Modbus.HealthBase < 0) errors.Add("modbus.healthBase 不能为负。");
            if (Modbus.HealthBase + ModbusRegisterMap.HealthRegisterCount - 1 > ModbusRegisterMap.MaxAddress)
                errors.Add("modbus.healthBase 加上健康区长度后超出 65534。");
            if (Modbus.FlushIntervalMs <= 0) errors.Add("modbus.flushIntervalMs 必须大于 0。");

            if (Command.ConfirmTimeoutSeconds <= 0) errors.Add("command.confirmTimeoutSeconds 必须大于 0。");
            if (Command.SelectTimeoutSeconds <= 0) errors.Add("command.selectTimeoutSeconds 必须大于 0。");
            if (Command.AllowedIoa != null)
            {
                for (var i = 0; i < Command.AllowedIoa.Length; i++)
                {
                    if (Command.AllowedIoa[i] <= 0) errors.Add("command.allowedIoa 里的 IOA 必须大于 0。");
                }
            }

            if (Stale.TimeoutSeconds <= 0) errors.Add("stale.timeoutSeconds 必须大于 0。");

            if (string.IsNullOrWhiteSpace(Iec104.Host)) errors.Add("iec104.host 不能为空。");
            if (Iec104.Port < 1 || Iec104.Port > 65535) errors.Add("iec104.port 必须在 1..65535 之间。");
            if (Iec104.CommonAddress < 1 || Iec104.CommonAddress > 65535)
                errors.Add("iec104.commonAddress 必须在 1..65535 之间。");
            if (Iec104.K < 1) errors.Add("iec104.k 必须大于等于 1。");
            if (Iec104.W < 1) errors.Add("iec104.w 必须大于等于 1。");
            if (Iec104.ReconnectDelaySeconds <= 0) errors.Add("iec104.reconnectDelaySeconds 必须大于 0。");
            if (Iec104.CyclicInterrogationSeconds < 0) errors.Add("iec104.cyclicInterrogationSeconds 不能为负。");

            if (string.IsNullOrWhiteSpace(PointTable.TelemetryPath)
                && string.IsNullOrWhiteSpace(PointTable.DiscretePath)
                && string.IsNullOrWhiteSpace(PointTable.CommandPath))
            {
                errors.Add("pointTable 至少要配置一张 CSV。");
            }

            CheckFile(errors, "pointTable.telemetryPath", PointTable.TelemetryPath);
            CheckFile(errors, "pointTable.discretePath", PointTable.DiscretePath);
            CheckFile(errors, "pointTable.commandPath", PointTable.CommandPath);

            return errors;
        }

        /// <summary>校验不通过时一次性抛出全部问题。</summary>
        public void EnsureValid()
        {
            var errors = Validate();
            if (errors.Count > 0) throw new ModbusBridgeConfigurationException(errors);
        }

        /// <summary>按配置导入点表。</summary>
        public Iec104PointTable LoadPointTable()
        {
            return Iec104PointTable.Load(new Iec104PointTableOptions
            {
                TelemetryPath = Resolve(PointTable.TelemetryPath),
                DiscretePath = Resolve(PointTable.DiscretePath),
                CommandPath = Resolve(PointTable.CommandPath),
                CommonAddress = (ushort)PointTable.CommonAddress,
                IoaBase = PointTable.IoaBase,
                TelemetryIoaBase = PointTable.TelemetryIoaBase,
                DiscreteIoaBase = PointTable.DiscreteIoaBase,
                CommandIoaBase = PointTable.CommandIoaBase,
                DiscreteTypeOverride = PointTable.DiscreteTypeOverride,
                PreferTimeTaggedSoe = PointTable.PreferTimeTaggedSoe,
                CommandTypeId = PointTable.CommandTypeId,
            });
        }

        /// <summary>转成桥接参数。</summary>
        public ModbusBridgeOptions CreateOptions()
        {
            return new ModbusBridgeOptions
            {
                BindAddress = Modbus.BindAddress,
                Port = Modbus.Port,
                UnitId = (byte)Modbus.UnitId,
                DiscreteBase = Modbus.DiscreteBase,
                TelemetryBase = Modbus.TelemetryBase,
                CommandBase = Modbus.CommandBase,
                HealthBase = Modbus.HealthBase,
                WordOrder = Modbus.WordOrder,
                FlushInterval = TimeSpan.FromMilliseconds(Modbus.FlushIntervalMs),
                EnableCommands = Command.Enabled,
                CommandMode = Command.Mode,
                AllowedCommandIoa = Command.AllowedIoa,
                ConfirmTimeout = TimeSpan.FromSeconds(Command.ConfirmTimeoutSeconds),
                SelectTimeout = TimeSpan.FromSeconds(Command.SelectTimeoutSeconds),
                StaleReadMode = Stale.ReadMode,
                StaleTimeout = TimeSpan.FromSeconds(Stale.TimeoutSeconds),
            };
        }

        /// <summary>转成 104 主站参数。</summary>
        public Iec104MasterOptions CreateMasterOptions(Iec104PointTable table)
        {
            return new Iec104MasterOptions
            {
                Host = Iec104.Host,
                Port = Iec104.Port,
                CommonAddress = (ushort)Iec104.CommonAddress,
                K = Iec104.K,
                W = Iec104.W,
                ConnectTimeout = TimeSpan.FromSeconds(Iec104.ConnectTimeoutSeconds),
                T1 = TimeSpan.FromSeconds(Iec104.T1Seconds),
                T2 = TimeSpan.FromSeconds(Iec104.T2Seconds),
                T3 = TimeSpan.FromSeconds(Iec104.T3Seconds),
                InterrogationTimeout = TimeSpan.FromSeconds(Iec104.InterrogationTimeoutSeconds),
                CommandTimeout = TimeSpan.FromSeconds(Iec104.CommandTimeoutSeconds),
                AutoReconnect = Iec104.AutoReconnect,
                ReconnectDelay = TimeSpan.FromSeconds(Iec104.ReconnectDelaySeconds),
                CyclicInterrogation = TimeSpan.FromSeconds(Iec104.CyclicInterrogationSeconds),
                InterrogateOnConnect = Iec104.InterrogateOnConnect,
                PointTable = table,
            };
        }

        private void Normalize(string baseDirectory)
        {
            _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
                ? Environment.CurrentDirectory
                : baseDirectory;

            if (Modbus == null) Modbus = new ModbusEndpointSettings();
            if (Command == null) Command = new CommandChannelSettings();
            if (Stale == null) Stale = new StaleDataSettings();
            if (Iec104 == null) Iec104 = new Iec104ConnectionSettings();
            if (PointTable == null) PointTable = new PointTableSettings();
        }

        private void CheckFile(List<string> errors, string name, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            var resolved = Resolve(path);
            if (!File.Exists(resolved)) errors.Add(string.Format("{0} 指向的文件不存在：{1}", name, resolved));
        }

        private string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            return Path.IsPathRooted(path) ? path : Path.Combine(_baseDirectory, path);
        }

#pragma warning disable IL3050
        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
#pragma warning restore IL3050
    }

    /// <summary>Modbus 从站与地址基准。</summary>
    public sealed class ModbusEndpointSettings
    {
        public string BindAddress { get; set; } = "0.0.0.0";

        public int Port { get; set; } = 502;

        public int UnitId { get; set; } = 1;

        public int DiscreteBase { get; set; }

        public int TelemetryBase { get; set; }

        public int CommandBase { get; set; }

        public int HealthBase { get; set; } = 1000;

        public ModbusFloatWordOrder WordOrder { get; set; } = ModbusFloatWordOrder.Abcd;

        public int FlushIntervalMs { get; set; } = 50;
    }

    /// <summary>命令通道。</summary>
    public sealed class CommandChannelSettings
    {
        public bool Enabled { get; set; }

        public Iec104CommandMode Mode { get; set; } = Iec104CommandMode.SelectAndExecute;

        public int[] AllowedIoa { get; set; }

        public double ConfirmTimeoutSeconds { get; set; } = 30;

        public double SelectTimeoutSeconds { get; set; } = 15;
    }

    /// <summary>断链读策略。</summary>
    public sealed class StaleDataSettings
    {
        public ModbusStaleReadMode ReadMode { get; set; } = ModbusStaleReadMode.LastValue;

        public double TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>104 连接参数。</summary>
    public sealed class Iec104ConnectionSettings
    {
        public string Host { get; set; } = "127.0.0.1";

        public int Port { get; set; } = 2404;

        public int CommonAddress { get; set; } = 1;

        public int K { get; set; } = 12;

        public int W { get; set; } = 8;

        public double ConnectTimeoutSeconds { get; set; } = 10;

        public double T1Seconds { get; set; } = 15;

        public double T2Seconds { get; set; } = 10;

        public double T3Seconds { get; set; } = 20;

        public double InterrogationTimeoutSeconds { get; set; } = 30;

        public double CommandTimeoutSeconds { get; set; } = 15;

        public bool AutoReconnect { get; set; } = true;

        public double ReconnectDelaySeconds { get; set; } = 5;

        public double CyclicInterrogationSeconds { get; set; }

        public bool InterrogateOnConnect { get; set; } = true;
    }

    /// <summary>点表 CSV 与地址基准。</summary>
    public sealed class PointTableSettings
    {
        public string TelemetryPath { get; set; }

        public string DiscretePath { get; set; }

        public string CommandPath { get; set; }

        public int CommonAddress { get; set; } = 1;

        public int IoaBase { get; set; } = 1;

        public int? TelemetryIoaBase { get; set; }

        public int? DiscreteIoaBase { get; set; }

        public int? CommandIoaBase { get; set; }

        public Iec104TypeId? DiscreteTypeOverride { get; set; }

        public bool PreferTimeTaggedSoe { get; set; } = true;

        public Iec104TypeId CommandTypeId { get; set; } = Iec104TypeId.SingleCommand;
    }
}

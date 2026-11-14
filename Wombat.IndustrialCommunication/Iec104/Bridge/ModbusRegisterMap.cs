using System;
using System.Collections.Generic;
using System.Text;
using Wombat.IndustrialCommunication.Iec104.Model;
using Wombat.IndustrialCommunication.Iec104.Protocol;

namespace Wombat.IndustrialCommunication.Iec104.ModbusBridge
{
    /// <summary>点位落在哪个 Modbus 区。</summary>
    public enum ModbusBridgeArea
    {
        /// <summary>1x 离散输入，只读。</summary>
        DiscreteInput = 0,

        /// <summary>3x 输入寄存器，只读。</summary>
        InputRegister = 1,

        /// <summary>4x 保持寄存器，可读写。</summary>
        HoldingRegister = 2,
    }

    /// <summary>一个 104 点在 Modbus 侧的地址。</summary>
    public sealed class ModbusPointMapping
    {
        internal ModbusPointMapping(Iec104Point point, ModbusBridgeArea area, ushort address, int registerCount)
        {
            Point = point;
            Area = area;
            Address = address;
            RegisterCount = registerCount;
        }

        public Iec104Point Point { get; }

        public ModbusBridgeArea Area { get; }

        /// <summary>点位起始地址；遥测与命令点占连续多个寄存器。</summary>
        public ushort Address { get; }

        public int RegisterCount { get; }

        public int Ioa
        {
            get { return Point.Ioa; }
        }

        public int ForwardNo
        {
            get { return Point.ForwardNo; }
        }
    }

    /// <summary>映射导出的一行，供生成寄存器表。</summary>
    public sealed class ModbusRegisterRow
    {
        internal ModbusRegisterRow(ModbusPointMapping mapping)
        {
            Mapping = mapping;
        }

        public ModbusPointMapping Mapping { get; }

        public ModbusBridgeArea Area
        {
            get { return Mapping.Area; }
        }

        public int Address
        {
            get { return Mapping.Address; }
        }

        public int RegisterCount
        {
            get { return Mapping.RegisterCount; }
        }

        public int ForwardNo
        {
            get { return Mapping.ForwardNo; }
        }

        public int Ioa
        {
            get { return Mapping.Ioa; }
        }

        public Iec104TypeId TypeId
        {
            get { return Mapping.Point.TypeId; }
        }

        public string Description
        {
            get { return Mapping.Point.Description; }
        }
    }

    /// <summary>点表与地址配置不自洽时抛出，异常里带全部错误，便于一次改完。</summary>
    public sealed class ModbusBridgeConfigurationException : Exception
    {
        public ModbusBridgeConfigurationException(IReadOnlyList<string> errors)
            : base(string.Join(Environment.NewLine, errors))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }

    /// <summary>
    /// 地址映射：按“基准 + 转发号”分配，而不是按点数组下标。
    /// 追加点位时已有地址不变，插队则会平移，所以启动时会打印点表版本哈希。
    /// </summary>
    public sealed class ModbusRegisterMap
    {
        /// <summary>健康区占用的寄存器数。</summary>
        public const int HealthRegisterCount = 8;

        /// <summary>遥测每个点占用的寄存器数。</summary>
        public const int TelemetryRegisterStride = 2;

        /// <summary>命令每个点占用的寄存器数。</summary>
        public const int CommandRegisterStride = 3;

        /// <summary>核心库 DataStore 的数组长度是 65535，可用下标为 0..65534。</summary>
        public const int MaxAddress = 65534;

        private readonly Dictionary<int, ModbusPointMapping> _byIoa;
        private readonly Dictionary<int, ModbusPointMapping> _commandByRegister;

        private ModbusRegisterMap(
            IReadOnlyList<ModbusPointMapping> telemetry,
            IReadOnlyList<ModbusPointMapping> discrete,
            IReadOnlyList<ModbusPointMapping> command,
            int discreteBase,
            int telemetryBase,
            int commandBase,
            int healthBase,
            ushort mapVersion,
            IReadOnlyList<string> warnings)
        {
            TelemetryPoints = telemetry;
            DiscretePoints = discrete;
            CommandPoints = command;
            DiscreteBase = discreteBase;
            TelemetryBase = telemetryBase;
            CommandBase = commandBase;
            HealthBase = healthBase;
            MapVersion = mapVersion;
            Warnings = warnings;

            _byIoa = new Dictionary<int, ModbusPointMapping>(telemetry.Count + discrete.Count + command.Count);
            Add(telemetry);
            Add(discrete);
            Add(command);

            _commandByRegister = new Dictionary<int, ModbusPointMapping>(command.Count * CommandRegisterStride);
            for (var i = 0; i < command.Count; i++)
            {
                var mapping = command[i];
                for (var offset = 0; offset < mapping.RegisterCount; offset++)
                {
                    _commandByRegister[mapping.Address + offset] = mapping;
                }
            }
        }

        public IReadOnlyList<ModbusPointMapping> TelemetryPoints { get; }

        public IReadOnlyList<ModbusPointMapping> DiscretePoints { get; }

        public IReadOnlyList<ModbusPointMapping> CommandPoints { get; }

        public int DiscreteBase { get; }

        public int TelemetryBase { get; }

        public int CommandBase { get; }

        public int HealthBase { get; }

        /// <summary>映射表 CRC16，写入健康区，供下游判断点表是否被改动。</summary>
        public ushort MapVersion { get; }

        /// <summary>点表导入告警，不影响映射。</summary>
        public IReadOnlyList<string> Warnings { get; }

        public bool TryGetMapping(int ioa, out ModbusPointMapping mapping)
        {
            return _byIoa.TryGetValue(ioa, out mapping);
        }

        /// <summary>
        /// 按寄存器地址查命令点。offset 0 是命令寄存器，1 是状态，2 是原因。
        /// 只处理 4x 命令区，其他地址返回 false。
        /// </summary>
        public bool TryGetCommandRegister(int address, out ModbusPointMapping mapping, out int offset)
        {
            if (_commandByRegister.TryGetValue(address, out mapping))
            {
                offset = address - mapping.Address;
                return true;
            }

            offset = 0;
            return false;
        }

        /// <summary>点表版本哈希的十六进制文本，用于日志与发布记录比对。</summary>
        public string VersionText
        {
            get { return MapVersion.ToString("X4"); }
        }

        /// <summary>导出为 CSV，供生成寄存器表与现场比对。</summary>
        public string ExportCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("区域,地址,寄存器数,转发号,IOA,类型,描述");

            var rows = Export();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                builder.Append(AreaName(row.Area)).Append(',')
                    .Append(row.Address).Append(',')
                    .Append(row.RegisterCount).Append(',')
                    .Append(row.ForwardNo).Append(',')
                    .Append(row.Ioa).Append(',')
                    .Append(row.TypeId).Append(',')
                    .Append(EscapeCsv(row.Description))
                    .AppendLine();
            }

            return builder.ToString();
        }

        /// <summary>把 CSV 写到文件，UTF-8。</summary>
        public void ExportCsv(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("路径不能为空。", nameof(path));
            System.IO.File.WriteAllText(path, ExportCsv(), Encoding.UTF8);
        }

        private static string AreaName(ModbusBridgeArea area)
        {
            switch (area)
            {
                case ModbusBridgeArea.DiscreteInput:
                    return "离散输入";

                case ModbusBridgeArea.InputRegister:
                    return "输入寄存器";

                default:
                    return "保持寄存器";
            }
        }

        private static string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return text;

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>按地址顺序导出全部映射行。</summary>
        public IReadOnlyList<ModbusRegisterRow> Export()
        {
            var rows = new List<ModbusRegisterRow>(
                DiscretePoints.Count + TelemetryPoints.Count + CommandPoints.Count);

            Append(rows, DiscretePoints);
            Append(rows, TelemetryPoints);
            Append(rows, CommandPoints);
            return rows;
        }

        /// <summary>按点表与配置构造映射；有任何不自洽都一次性报出来。</summary>
        public static ModbusRegisterMap Build(Iec104PointTable table, ModbusBridgeOptions options)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.Validate();

            var errors = new List<string>();
            var telemetrySeen = new HashSet<int>();
            var discreteSeen = new HashSet<int>();
            var commandSeen = new HashSet<int>();

            var telemetry = new List<ModbusPointMapping>();
            var discrete = new List<ModbusPointMapping>();
            var command = new List<ModbusPointMapping>();

            for (var i = 0; i < table.Points.Count; i++)
            {
                var point = table.Points[i];
                if (point.ForwardNo < 0)
                {
                    errors.Add($"IOA {point.Ioa}（{point.Description}）的转发号为负：{point.ForwardNo}。");
                    continue;
                }

                var kind = Classify(point, errors);
                if (kind == PointKind.Ignored) continue;

                switch (kind)
                {
                    case PointKind.Telemetry:
                        AddPoint(telemetry, telemetrySeen, point, ModbusBridgeArea.InputRegister,
                            TelemetryRegisterStride, options.TelemetryBase, "遥测", errors);
                        break;

                    case PointKind.Discrete:
                        AddPoint(discrete, discreteSeen, point, ModbusBridgeArea.DiscreteInput,
                            1, options.DiscreteBase, "遥信", errors);
                        break;

                    case PointKind.Command:
                        AddPoint(command, commandSeen, point, ModbusBridgeArea.HoldingRegister,
                            CommandRegisterStride, options.CommandBase, "遥控", errors);
                        break;
                }
            }

            telemetry.Sort(CompareByAddress);
            discrete.Sort(CompareByAddress);
            command.Sort(CompareByAddress);

            CheckRegions(options, command, errors);

            if (errors.Count > 0) throw new ModbusBridgeConfigurationException(errors);

            var warnings = new List<string>(table.Warnings);
            AppendGapWarnings(telemetry, TelemetryRegisterStride, "遥测", warnings);
            AppendGapWarnings(discrete, 1, "遥信", warnings);

            var version = ComputeVersion(telemetry, discrete, command);

            return new ModbusRegisterMap(
                telemetry,
                discrete,
                command,
                options.DiscreteBase,
                options.TelemetryBase,
                options.CommandBase,
                options.HealthBase,
                version,
                warnings);
        }

        private enum PointKind
        {
            Ignored,
            Telemetry,
            Discrete,
            Command,
        }

        private static PointKind Classify(Iec104Point point, List<string> errors)
        {
            if (point.IsCommand || Iec104TypeInfo.IsCommand(point.TypeId)) return PointKind.Command;

            switch (point.TypeId)
            {
                case Iec104TypeId.MeasuredFloat:
                case Iec104TypeId.MeasuredFloatWithTime:
                case Iec104TypeId.MeasuredNormalized:
                case Iec104TypeId.MeasuredNormalizedWithTime:
                case Iec104TypeId.MeasuredScaled:
                case Iec104TypeId.MeasuredScaledWithTime:
                    return PointKind.Telemetry;

                case Iec104TypeId.SinglePoint:
                case Iec104TypeId.SinglePointWithTime:
                case Iec104TypeId.SinglePointWithTime24:
                case Iec104TypeId.DoublePoint:
                case Iec104TypeId.DoublePointWithTime:
                case Iec104TypeId.DoublePointWithTime24:
                    return PointKind.Discrete;

                default:
                    errors.Add($"IOA {point.Ioa}（{point.Description}）的类型 {point.TypeId} 无法映射到 Modbus 寄存器。");
                    return PointKind.Ignored;
            }
        }

        private static void AddPoint(
            List<ModbusPointMapping> target,
            HashSet<int> seen,
            Iec104Point point,
            ModbusBridgeArea area,
            int stride,
            int baseAddress,
            string areaName,
            List<string> errors)
        {
            if (!seen.Add(point.ForwardNo))
            {
                errors.Add($"{areaName}转发号 {point.ForwardNo} 重复（IOA {point.Ioa}，{point.Description}）。");
                return;
            }

            var address = baseAddress + (long)point.ForwardNo * stride;
            var last = address + stride - 1;
            if (last > MaxAddress)
            {
                errors.Add(
                    $"{areaName}转发号 {point.ForwardNo} 映射到地址 {last}，超出 0..{MaxAddress} 范围。");
                return;
            }

            target.Add(new ModbusPointMapping(point, area, (ushort)address, stride));
        }

        private static void CheckRegions(
            ModbusBridgeOptions options,
            List<ModbusPointMapping> command,
            List<string> errors)
        {
            if (options.HealthBase + HealthRegisterCount - 1 > MaxAddress)
            {
                errors.Add($"健康区 {options.HealthBase}..{options.HealthBase + HealthRegisterCount - 1} 越界。");
                return;
            }

            if (command.Count == 0) return;

            var commandStart = command[0].Address;
            var commandEnd = command[command.Count - 1].Address + command[command.Count - 1].RegisterCount - 1;
            var healthStart = options.HealthBase;
            var healthEnd = options.HealthBase + HealthRegisterCount - 1;

            if (commandStart <= healthEnd && healthStart <= commandEnd)
            {
                errors.Add(
                    $"命令区 {commandStart}..{commandEnd} 与健康区 {healthStart}..{healthEnd} 重叠，请调整 CommandBase 或 HealthBase。");
            }
        }

        private static void AppendGapWarnings(
            List<ModbusPointMapping> points,
            int stride,
            string areaName,
            List<string> warnings)
        {
            if (points.Count < 2) return;

            var gaps = 0;
            for (var i = 1; i < points.Count; i++)
            {
                if (points[i].Address != points[i - 1].Address + stride) gaps++;
            }

            if (gaps > 0)
            {
                warnings.Add($"{areaName}转发号不连续，地址中共有 {gaps} 处空隙；下游批量读需要按段拆分。");
            }
        }

        private static int CompareByAddress(ModbusPointMapping left, ModbusPointMapping right)
        {
            return left.Address.CompareTo(right.Address);
        }

        private static ushort ComputeVersion(
            List<ModbusPointMapping> telemetry,
            List<ModbusPointMapping> discrete,
            List<ModbusPointMapping> command)
        {
            var builder = new StringBuilder();
            AppendVersion(builder, discrete);
            AppendVersion(builder, telemetry);
            AppendVersion(builder, command);
            return Crc16(builder.ToString());
        }

        private static void AppendVersion(StringBuilder builder, List<ModbusPointMapping> points)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var mapping = points[i];
                builder.Append(mapping.Area).Append('|')
                    .Append(mapping.Point.ForwardNo).Append('|')
                    .Append(mapping.Point.Ioa).Append('|')
                    .Append(mapping.Address).Append(';');
            }
        }

        /// <summary>标准 Modbus CRC16（多项式 0xA001，初值 0xFFFF）。</summary>
        private static ushort Crc16(string text)
        {
            ushort crc = 0xFFFF;
            for (var i = 0; i < text.Length; i++)
            {
                crc ^= (byte)text[i];
                for (var bit = 0; bit < 8; bit++)
                {
                    var carry = (crc & 1) != 0;
                    crc >>= 1;
                    if (carry) crc ^= 0xA001;
                }
            }

            return crc;
        }

        private void Add(IReadOnlyList<ModbusPointMapping> points)
        {
            for (var i = 0; i < points.Count; i++)
            {
                _byIoa[points[i].Ioa] = points[i];
            }
        }

        private static void Append(List<ModbusRegisterRow> rows, IReadOnlyList<ModbusPointMapping> points)
        {
            for (var i = 0; i < points.Count; i++)
            {
                rows.Add(new ModbusRegisterRow(points[i]));
            }
        }
    }
}

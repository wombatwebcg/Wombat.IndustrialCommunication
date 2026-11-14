using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Iec104.Model
{
    using Wombat.IndustrialCommunication.Iec104.Protocol;

    /// <summary>点表导入选项。</summary>
    public sealed class Iec104PointTableOptions
    {
        /// <summary>遥测 CSV 路径，可为空。</summary>
        public string TelemetryPath { get; set; }

        /// <summary>遥信 CSV 路径，可为空。</summary>
        public string DiscretePath { get; set; }

        /// <summary>遥控 CSV 路径，可为空。</summary>
        public string CommandPath { get; set; }

        public ushort CommonAddress { get; set; } = 1;

        /// <summary>Offset 模式下的地址基准：Ioa = 转发号 + IoaBase。</summary>
        public int IoaBase { get; set; } = 1;

        /// <summary>遥测地址基准；为空时使用 IoaBase。</summary>
        public int? TelemetryIoaBase { get; set; }

        /// <summary>遥信地址基准；为空时使用 IoaBase。</summary>
        public int? DiscreteIoaBase { get; set; }

        /// <summary>遥控地址基准；为空时使用 IoaBase。</summary>
        public int? CommandIoaBase { get; set; }

        /// <summary>强制指定遥信类型；为空时按点表与 SOE 标志推断。</summary>
        public Iec104TypeId? DiscreteTypeOverride { get; set; }

        /// <summary>遥信为 SOE 时是否使用带时标类型。</summary>
        public bool PreferTimeTaggedSoe { get; set; } = true;

        /// <summary>遥控点类型。点表没有该列，只能由配置给出。</summary>
        public Iec104TypeId CommandTypeId { get; set; } = Iec104TypeId.SingleCommand;

        /// <summary>遥控点缺省总召唤分组。</summary>
        public int InterrogationGroup { get; set; } = 1;
    }

    /// <summary>
    /// 点表：按 IOA 索引，附带导入过程的告警。
    /// </summary>
    public sealed class Iec104PointTable
    {
        private readonly Dictionary<int, Iec104Point> _byIoa;
        private readonly Dictionary<Iec104TypeId, List<Iec104Point>> _byType;

        private Iec104PointTable(IReadOnlyList<Iec104Point> points, IReadOnlyList<string> warnings)
        {
            Points = points;
            Warnings = warnings;

            _byIoa = new Dictionary<int, Iec104Point>(points.Count);
            _byType = new Dictionary<Iec104TypeId, List<Iec104Point>>();

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (!_byIoa.ContainsKey(point.Ioa))
                {
                    _byIoa.Add(point.Ioa, point);
                }

                if (!_byType.TryGetValue(point.TypeId, out var list))
                {
                    list = new List<Iec104Point>();
                    _byType.Add(point.TypeId, list);
                }

                list.Add(point);
            }
        }

        /// <summary>全部点位。</summary>
        public IReadOnlyList<Iec104Point> Points { get; }

        /// <summary>导入告警。</summary>
        public IReadOnlyList<string> Warnings { get; }

        public int Count
        {
            get { return Points.Count; }
        }

        /// <summary>按 IOA 查找点位。</summary>
        public bool TryGetPoint(int ioa, out Iec104Point point)
        {
            return _byIoa.TryGetValue(ioa, out point);
        }

        /// <summary>取某一类型的全部点位，保持导入顺序。</summary>
        public IReadOnlyList<Iec104Point> GetPoints(Iec104TypeId typeId)
        {
            List<Iec104Point> list;
            return _byType.TryGetValue(typeId, out list) ? list : Array.Empty<Iec104Point>();
        }

        /// <summary>按点表导入三张 CSV。</summary>
        public static Iec104PointTable Load(Iec104PointTableOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var warnings = new List<string>();
            var points = new List<Iec104Point>();
            var descriptionCounts = new Dictionary<string, int>();

            if (!string.IsNullOrWhiteSpace(options.TelemetryPath))
            {
                Iec104CsvImporter.ImportTelemetry(options, points, warnings, new HashSet<int>(), descriptionCounts);
            }

            if (!string.IsNullOrWhiteSpace(options.DiscretePath))
            {
                Iec104CsvImporter.ImportDiscrete(options, points, warnings, new HashSet<int>(), descriptionCounts);
            }

            if (!string.IsNullOrWhiteSpace(options.CommandPath))
            {
                Iec104CsvImporter.ImportCommand(options, points, warnings, new HashSet<int>(), descriptionCounts);
            }

            Iec104CsvImporter.AppendDuplicateDescriptionWarning(descriptionCounts, warnings);
            Iec104CsvImporter.AppendIoaCollisionWarning(points, warnings);

            return new Iec104PointTable(points, warnings);
        }

        /// <summary>用现有点位构造点表，便于测试与自定义来源。</summary>
        public static Iec104PointTable FromPoints(IEnumerable<Iec104Point> points, IEnumerable<string> warnings = null)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            var list = new List<Iec104Point>(points);
            var warningList = warnings == null ? new List<string>() : new List<string>(warnings);
            return new Iec104PointTable(list, warningList);
        }
    }

    /// <summary>CSV 导入实现，按表头名称取列，不依赖列顺序。</summary>
    internal static class Iec104CsvImporter
    {
        private static readonly Dictionary<string, Iec104TypeId> TypeNames = new Dictionary<string, Iec104TypeId>(StringComparer.Ordinal)
        {
            { "单点", Iec104TypeId.SinglePoint },
            { "双点", Iec104TypeId.DoublePoint },
            { "带时标的单点", Iec104TypeId.SinglePointWithTime },
            { "带时标的双点", Iec104TypeId.DoublePointWithTime },
            { "不带时标的短浮点数测量值", Iec104TypeId.MeasuredFloat },
            { "带时标的短浮点数测量值", Iec104TypeId.MeasuredFloatWithTime },
            { "不带时标的归一化测量值", Iec104TypeId.MeasuredNormalized },
            { "不带时标的标度化测量值", Iec104TypeId.MeasuredScaled },
            { "短浮点数", Iec104TypeId.MeasuredFloat },
        };

        public static void ImportTelemetry(
            Iec104PointTableOptions options,
            List<Iec104Point> points,
            List<string> warnings,
            HashSet<int> seenForwardNumbers,
            Dictionary<string, int> descriptionCounts)
        {
            var rows = ReadRows(options.TelemetryPath, warnings);
            var zeroDeadband = 0;

            foreach (var row in rows)
            {
                var forwardNo = row.GetInt("转发号", -1);
                if (forwardNo < 0)
                {
                    warnings.Add($"遥测第 {row.LineNumber} 行缺少有效转发号，已跳过。");
                    continue;
                }

                if (!TryResolveIoa(forwardNo, options, options.TelemetryIoaBase, "遥测", row.LineNumber, warnings, out var ioa))
                {
                    continue;
                }

                RegisterForwardNumber(forwardNo, "遥测", seenForwardNumbers, warnings);

                var typeName = row.Get("ASDU类型");
                var typeId = Iec104TypeId.MeasuredFloat;
                if (!string.IsNullOrEmpty(typeName))
                {
                    if (!TypeNames.TryGetValue(typeName, out typeId))
                    {
                        warnings.Add($"遥测第 {row.LineNumber} 行类型「{typeName}」无法识别，按短浮点处理。");
                        typeId = Iec104TypeId.MeasuredFloat;
                    }
                }

                var deadband = row.GetDouble("变化死区", 0d);
                if (deadband <= 0d) zeroDeadband++;

                var description = row.Get("测点描述");
                CountDescription(description, descriptionCounts);

                points.Add(new Iec104Point
                {
                    CommonAddress = options.CommonAddress,
                    Ioa = ioa,
                    ForwardNo = forwardNo,
                    TypeId = typeId,
                    Channel = row.Get("通道"),
                    Device = row.Get("设备"),
                    Description = description,
                    Scale = row.GetDouble("系数", 1d),
                    Offset = row.GetDouble("偏移值", 0d),
                    Deadband = deadband,
                    DataType = ToDataType(typeId),
                    InterrogationGroup = options.InterrogationGroup,
                });
            }

            if (zeroDeadband > 0)
            {
                warnings.Add($"遥测中有 {zeroDeadband} 个点变化死区为 0，从站将不做变化抑制。");
            }
        }

        public static void ImportDiscrete(
            Iec104PointTableOptions options,
            List<Iec104Point> points,
            List<string> warnings,
            HashSet<int> seenForwardNumbers,
            Dictionary<string, int> descriptionCounts)
        {
            var rows = ReadRows(options.DiscretePath, warnings);

            foreach (var row in rows)
            {
                var forwardNo = row.GetInt("转发号", -1);
                if (forwardNo < 0)
                {
                    warnings.Add($"遥信第 {row.LineNumber} 行缺少有效转发号，已跳过。");
                    continue;
                }

                if (!TryResolveIoa(forwardNo, options, options.DiscreteIoaBase, "遥信", row.LineNumber, warnings, out var ioa))
                {
                    continue;
                }

                RegisterForwardNumber(forwardNo, "遥信", seenForwardNumbers, warnings);

                var isSoe = row.Get("是否转发SOE") == "是";
                var typeName = row.Get("遥信类型");
                var typeId = ResolveDiscreteType(typeName, isSoe, options, row.LineNumber, warnings);

                var description = row.Get("测点描述");
                CountDescription(description, descriptionCounts);

                points.Add(new Iec104Point
                {
                    CommonAddress = options.CommonAddress,
                    Ioa = ioa,
                    ForwardNo = forwardNo,
                    TypeId = typeId,
                    Channel = row.Get("通道"),
                    Device = row.Get("设备"),
                    Description = description,
                    Invert = row.Get("极性") == "反极性",
                    IsSoe = isSoe,
                    DataType = DataTypeEnums.Bool,
                    InterrogationGroup = options.InterrogationGroup,
                });
            }
        }

        public static void ImportCommand(
            Iec104PointTableOptions options,
            List<Iec104Point> points,
            List<string> warnings,
            HashSet<int> seenForwardNumbers,
            Dictionary<string, int> descriptionCounts)
        {
            var rows = ReadRows(options.CommandPath, warnings);

            foreach (var row in rows)
            {
                var forwardNo = row.GetInt("转发号", -1);
                if (forwardNo < 0)
                {
                    warnings.Add($"遥控第 {row.LineNumber} 行缺少有效转发号，已跳过。");
                    continue;
                }

                if (!TryResolveIoa(forwardNo, options, options.CommandIoaBase, "遥控", row.LineNumber, warnings, out var ioa))
                {
                    continue;
                }

                RegisterForwardNumber(forwardNo, "遥控", seenForwardNumbers, warnings);

                var description = row.Get("测点描述");
                CountDescription(description, descriptionCounts);

                points.Add(new Iec104Point
                {
                    CommonAddress = options.CommonAddress,
                    Ioa = ioa,
                    ForwardNo = forwardNo,
                    TypeId = options.CommandTypeId,
                    Channel = row.Get("通道"),
                    Device = row.Get("设备"),
                    Description = description,
                    IsCommand = true,
                    DataType = DataTypeEnums.Bool,
                    InterrogationGroup = options.InterrogationGroup,
                });
            }
        }

        public static void AppendDuplicateDescriptionWarning(Dictionary<string, int> descriptionCounts, List<string> warnings)
        {
            var duplicateGroups = 0;
            var duplicateRows = 0;

            foreach (var pair in descriptionCounts)
            {
                if (pair.Value > 1)
                {
                    duplicateGroups++;
                    duplicateRows += pair.Value;
                }
            }

            if (duplicateGroups > 0)
            {
                warnings.Add($"测点描述重复 {duplicateGroups} 组，涉及 {duplicateRows} 行；匹配请使用公共地址与 IOA，不要用描述。");
            }
        }

        private static Iec104TypeId ResolveDiscreteType(
            string typeName,
            bool isSoe,
            Iec104PointTableOptions options,
            int lineNumber,
            List<string> warnings)
        {
            if (options.DiscreteTypeOverride.HasValue)
            {
                return options.DiscreteTypeOverride.Value;
            }

            var isDouble = typeName == "双点";
            if (typeName != "单点" && typeName != "双点" && !string.IsNullOrEmpty(typeName))
            {
                warnings.Add($"遥信第 {lineNumber} 行类型「{typeName}」无法识别，按单点处理。");
            }

            if (isSoe && options.PreferTimeTaggedSoe)
            {
                return isDouble ? Iec104TypeId.DoublePointWithTime : Iec104TypeId.SinglePointWithTime;
            }

            return isDouble ? Iec104TypeId.DoublePoint : Iec104TypeId.SinglePoint;
        }

        private static bool TryResolveIoa(
            int forwardNo,
            Iec104PointTableOptions options,
            int? categoryBase,
            string category,
            int lineNumber,
            List<string> warnings,
            out int ioa)
        {
            ioa = 0;
            ioa = forwardNo + (categoryBase ?? options.IoaBase);

            if (ioa <= 0 || ioa > Iec104Constants.MaxInformationObjectAddress)
            {
                warnings.Add($"{category}第 {lineNumber} 行转发号 {forwardNo} 映射出的 IOA {ioa} 非法，已跳过。");
                return false;
            }

            return true;
        }

        private static void RegisterForwardNumber(
            int forwardNo,
            string category,
            HashSet<int> seen,
            List<string> warnings)
        {
            if (!seen.Add(forwardNo))
            {
                warnings.Add($"{category}中转发号 {forwardNo} 重复，请检查点表。");
            }
        }

        public static void AppendIoaCollisionWarning(List<Iec104Point> points, List<string> warnings)
        {
            var collisions = 0;
            var seen = new Dictionary<int, Iec104TypeId>();
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                Iec104TypeId existing;
                if (seen.TryGetValue(point.Ioa, out existing))
                {
                    if (existing != point.TypeId)
                    {
                        collisions++;
                    }

                    continue;
                }

                seen.Add(point.Ioa, point.TypeId);
            }

            if (collisions > 0)
            {
                warnings.Add($"有 {collisions} 个 IOA 在不同类别间冲突，默认 IoaBase 未按类别分段；请配置 TelemetryIoaBase、DiscreteIoaBase、CommandIoaBase。");
            }
        }

        private static void CountDescription(string description, Dictionary<string, int> counts)
        {
            if (string.IsNullOrEmpty(description)) return;

            int value;
            counts.TryGetValue(description, out value);
            counts[description] = value + 1;
        }

        private static DataTypeEnums ToDataType(Iec104TypeId typeId)
        {
            switch (typeId)
            {
                case Iec104TypeId.MeasuredFloat:
                case Iec104TypeId.MeasuredFloatWithTime:
                    return DataTypeEnums.Float;
                case Iec104TypeId.MeasuredNormalized:
                case Iec104TypeId.MeasuredScaled:
                case Iec104TypeId.MeasuredNormalizedWithTime:
                case Iec104TypeId.MeasuredScaledWithTime:
                    return DataTypeEnums.Int16;
                default:
                    return DataTypeEnums.Bool;
            }
        }

        private static List<CsvRow> ReadRows(string path, List<string> warnings)
        {
            var rows = new List<CsvRow>();
            if (!File.Exists(path))
            {
                warnings.Add($"文件不存在：{path}");
                return rows;
            }

            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                var headerLine = reader.ReadLine();
                if (headerLine == null)
                {
                    warnings.Add($"文件为空：{path}");
                    return rows;
                }

                var headers = SplitCsvLine(headerLine);
                for (var i = 0; i < headers.Count; i++)
                {
                    headers[i] = headers[i].Trim().TrimStart('\uFEFF');
                }

                var lineNumber = 1;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = SplitCsvLine(line);
                    var row = new CsvRow(headers, values, lineNumber);
                    rows.Add(row);
                }
            }

            return rows;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(ch);
                    }
                }
                else if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == ',')
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }

            values.Add(current.ToString());
            return values;
        }

        private sealed class CsvRow
        {
            private readonly Dictionary<string, string> _values;

            public CsvRow(List<string> headers, List<string> values, int lineNumber)
            {
                LineNumber = lineNumber;
                _values = new Dictionary<string, string>(StringComparer.Ordinal);

                for (var i = 0; i < headers.Count && i < values.Count; i++)
                {
                    var key = headers[i];
                    if (string.IsNullOrEmpty(key) || _values.ContainsKey(key)) continue;
                    _values.Add(key, values[i].Trim());
                }
            }

            public int LineNumber { get; }

            public string Get(string column)
            {
                string value;
                return _values.TryGetValue(column, out value) ? value : string.Empty;
            }

            public int GetInt(string column, int defaultValue)
            {
                int value;
                return int.TryParse(Get(column), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                    ? value
                    : defaultValue;
            }

            public double GetDouble(string column, double defaultValue)
            {
                double value;
                return double.TryParse(Get(column), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    ? value
                    : defaultValue;
            }
        }
    }
}

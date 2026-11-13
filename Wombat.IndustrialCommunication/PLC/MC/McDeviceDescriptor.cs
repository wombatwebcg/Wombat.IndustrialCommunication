using System;
using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 三菱软元件描述符：同时承载 1E / Q-3E / iQ-R-3E 三套码值、进制、位/字属性、
    /// 可用系列掩码、1E 别名映射与每点字数。
    /// </summary>
    public sealed class McDeviceDescriptor
    {
        /// <summary>FX/Q/L/iQ-R 通用（使用 3E 帧的系列）</summary>
        private const McSeries Qlr = McSeries.Fx | McSeries.Q | McSeries.L | McSeries.IqR;

        /// <summary>全系列可用</summary>
        private const McSeries AllSeries = McSeries.Fx | McSeries.A | McSeries.Q | McSeries.L | McSeries.IqR;

        private static readonly List<McDeviceDescriptor> _all = new List<McDeviceDescriptor>();
        private static readonly List<McDeviceDescriptor> _matchOrder = new List<McDeviceDescriptor>();
        private static readonly Dictionary<McDeviceKind, McDeviceDescriptor> _byKind =
            new Dictionary<McDeviceKind, McDeviceDescriptor>();
        private static readonly Dictionary<ushort, McDeviceDescriptor> _byCode1E =
            new Dictionary<ushort, McDeviceDescriptor>();
        private static readonly Dictionary<ushort, McDeviceDescriptor> _byCodeQ3E =
            new Dictionary<ushort, McDeviceDescriptor>();
        private static readonly Dictionary<ushort, McDeviceDescriptor> _byCodeR3E =
            new Dictionary<ushort, McDeviceDescriptor>();

        static McDeviceDescriptor()
        {
            // 前缀     种类  1E码     Q-3E码 iQ-R码  位   进制 可用系列      别名       别名偏移 每点字数
            Add("SM", McDeviceKind.SM, 0x0000, 0x91, 0x0091, true, 10, AllSeries, McDeviceKind.M, 8000, 1);
            Add("SD", McDeviceKind.SD, 0x0000, 0xA9, 0x00A9, false, 10, AllSeries, McDeviceKind.D, 8000, 1);
            Add("X", McDeviceKind.X, 0x5820, 0x9C, 0x009C, true, 16, AllSeries, McDeviceKind.None, 0, 1);
            Add("Y", McDeviceKind.Y, 0x5920, 0x9D, 0x009D, true, 16, AllSeries, McDeviceKind.None, 0, 1);
            Add("M", McDeviceKind.M, 0x4D20, 0x90, 0x0090, true, 10, AllSeries, McDeviceKind.None, 0, 1);
            Add("L", McDeviceKind.L, 0x0000, 0x92, 0x0092, true, 10, Qlr | McSeries.A, McDeviceKind.M, 0, 1);
            Add("F", McDeviceKind.F, 0x4620, 0x93, 0x0093, true, 10, AllSeries, McDeviceKind.None, 0, 1);
            Add("V", McDeviceKind.V, 0x0000, 0x94, 0x0094, true, 10, Qlr, McDeviceKind.None, 0, 1);
            Add("B", McDeviceKind.B, 0x4220, 0xA0, 0x00A0, true, 16, AllSeries, McDeviceKind.None, 0, 1);
            Add("D", McDeviceKind.D, 0x4420, 0xA8, 0x00A8, false, 10, AllSeries, McDeviceKind.None, 0, 1);
            Add("W", McDeviceKind.W, 0x5720, 0xB4, 0x00B4, false, 16, AllSeries, McDeviceKind.None, 0, 1);
            Add("R", McDeviceKind.R, 0x5220, 0xAF, 0x00AF, false, 10, AllSeries, McDeviceKind.None, 0, 1);
            Add("ZR", McDeviceKind.ZR, 0x0000, 0xB0, 0x00B0, false, 16, Qlr, McDeviceKind.None, 0, 1);
            Add("Z", McDeviceKind.Z, 0x0000, 0xCC, 0x00CC, false, 10, Qlr, McDeviceKind.None, 0, 1);
            Add("SB", McDeviceKind.SB, 0x0000, 0xA1, 0x00A1, true, 16, Qlr, McDeviceKind.None, 0, 1);
            Add("SW", McDeviceKind.SW, 0x0000, 0xB5, 0x00B5, false, 16, Qlr, McDeviceKind.None, 0, 1);
            Add("S", McDeviceKind.S, 0x0000, 0x98, 0x0098, true, 10, Qlr | McSeries.A, McDeviceKind.M, 0, 1);
            Add("DX", McDeviceKind.DX, 0x0000, 0xA2, 0x00A2, true, 16, Qlr, McDeviceKind.None, 0, 1);
            Add("DY", McDeviceKind.DY, 0x0000, 0xA3, 0x00A3, true, 16, Qlr, McDeviceKind.None, 0, 1);
            Add("TS", McDeviceKind.TS, 0x5453, 0xC1, 0x00C1, true, 10, AllSeries, McDeviceKind.None, 0, 1);
            Add("TC", McDeviceKind.TC, 0x5443, 0xC0, 0x00C0, true, 10, AllSeries, McDeviceKind.None, 0, 1);
            Add("TN", McDeviceKind.TN, 0x544E, 0xC2, 0x00C2, false, 10, AllSeries, McDeviceKind.None, 0, 1);
            Add("SS", McDeviceKind.SS, 0x0000, 0xC7, 0x00C7, true, 10, Qlr, McDeviceKind.None, 0, 1);
            Add("SC", McDeviceKind.SC, 0x0000, 0xC6, 0x00C6, true, 10, Qlr, McDeviceKind.None, 0, 1);
            Add("SN", McDeviceKind.SN, 0x0000, 0xC8, 0x00C8, true, 10, Qlr, McDeviceKind.None, 0, 1);
            Add("CS", McDeviceKind.CS, 0x4353, 0xC4, 0x00C4, true, 10, AllSeries, McDeviceKind.None, 0, 1);
            Add("CC", McDeviceKind.CC, 0x4343, 0xC3, 0x00C3, true, 10, AllSeries, McDeviceKind.None, 0, 1);
            Add("CN", McDeviceKind.CN, 0x434E, 0xC5, 0x00C5, false, 10, AllSeries, McDeviceKind.None, 0, 1);

            // iQ-R 独有
            Add("RD", McDeviceKind.RD, 0x0000, 0x00, 0x002C, false, 10, McSeries.IqR, McDeviceKind.None, 0, 1);
            Add("LZ", McDeviceKind.LZ, 0x0000, 0x00, 0x0062, false, 10, McSeries.IqR, McDeviceKind.None, 0, 1);
            Add("LTS", McDeviceKind.LTS, 0x0000, 0x00, 0x0051, true, 10, McSeries.IqR, McDeviceKind.None, 0, 1);
            Add("LTC", McDeviceKind.LTC, 0x0000, 0x00, 0x0050, true, 10, McSeries.IqR, McDeviceKind.None, 0, 1);
            Add("LTN", McDeviceKind.LTN, 0x0000, 0x00, 0x0052, false, 10, McSeries.IqR, McDeviceKind.None, 0, 4);
            Add("LSTS", McDeviceKind.LSTS, 0x0000, 0x00, 0x0059, true, 10, McSeries.IqR, McDeviceKind.None, 0, 1);
            Add("LSTC", McDeviceKind.LSTC, 0x0000, 0x00, 0x0058, true, 10, McSeries.IqR, McDeviceKind.None, 0, 1);
            Add("LSTN", McDeviceKind.LSTN, 0x0000, 0x00, 0x005A, false, 10, McSeries.IqR, McDeviceKind.None, 0, 4);
            Add("LCS", McDeviceKind.LCS, 0x0000, 0x00, 0x0055, true, 10, McSeries.IqR, McDeviceKind.None, 0, 1);
            Add("LCC", McDeviceKind.LCC, 0x0000, 0x00, 0x0054, true, 10, McSeries.IqR, McDeviceKind.None, 0, 1);
            Add("LCN", McDeviceKind.LCN, 0x0000, 0x00, 0x0056, false, 10, McSeries.IqR, McDeviceKind.None, 0, 4);

            // 服务端按码反查：1E 的别名软元件（SM/SD/L/S）不会出现在 1E 报文里，不参与反查
            foreach (var descriptor in _all)
            {
                if (descriptor.Code1E != 0 && descriptor.AliasOf == McDeviceKind.None)
                    _byCode1E[descriptor.Code1E] = descriptor;
                if (descriptor.CodeQ3E != 0)
                    _byCodeQ3E[descriptor.CodeQ3E] = descriptor;
                if (descriptor.CodeR3E != 0)
                    _byCodeR3E[descriptor.CodeR3E] = descriptor;
            }

            // 最长前缀优先：LSTN 先于 LSTS/LTS/L，SW/SB/SD/SN/SC/SS 先于 S，ZR 先于 Z，DX/DY 先于 D
            _matchOrder.AddRange(_all);
            _matchOrder.Sort(delegate (McDeviceDescriptor x, McDeviceDescriptor y)
            {
                int byLength = y.Prefix.Length.CompareTo(x.Prefix.Length);
                return byLength != 0 ? byLength : string.CompareOrdinal(x.Prefix, y.Prefix);
            });
        }

        private static void Add(string prefix, McDeviceKind kind, ushort code1E, byte codeQ3E, ushort codeR3E,
            bool isBit, int radix, McSeries seriesMask, McDeviceKind aliasOf, int aliasOffset, int wordsPerPoint)
        {
            var descriptor = new McDeviceDescriptor(prefix, kind, code1E, codeQ3E, codeR3E,
                isBit, radix, seriesMask, aliasOf, aliasOffset, wordsPerPoint);
            _all.Add(descriptor);
            _byKind[kind] = descriptor;
        }

        private McDeviceDescriptor(string prefix, McDeviceKind kind, ushort code1E, byte codeQ3E, ushort codeR3E,
            bool isBit, int radix, McSeries seriesMask, McDeviceKind aliasOf, int aliasOffset, int wordsPerPoint)
        {
            Prefix = prefix;
            Kind = kind;
            Code1E = code1E;
            CodeQ3E = codeQ3E;
            CodeR3E = codeR3E;
            IsBit = isBit;
            Radix = radix;
            SeriesMask = seriesMask;
            AliasOf = aliasOf;
            AliasOffset = aliasOffset;
            WordsPerPoint = wordsPerPoint;
        }

        /// <summary>软元件前缀（大写）</summary>
        public string Prefix { get; private set; }

        /// <summary>归一化种类</summary>
        public McDeviceKind Kind { get; private set; }

        /// <summary>A-1E 软元件码（2 字节 ASCII，0 表示 1E 不支持）</summary>
        public ushort Code1E { get; private set; }

        /// <summary>Q/L 3E 软元件码（1 字节，0 表示不支持）</summary>
        public byte CodeQ3E { get; private set; }

        /// <summary>iQ-R 3E 软元件码（2 字节，0 表示不支持）</summary>
        public ushort CodeR3E { get; private set; }

        /// <summary>是否位软元件</summary>
        public bool IsBit { get; private set; }

        /// <summary>默认编号进制（X/Y 在 FX 系列由 <see cref="McProfile"/> 覆盖为 8）</summary>
        public int Radix { get; private set; }

        /// <summary>可用系列掩码</summary>
        public McSeries SeriesMask { get; private set; }

        /// <summary>1E 下的等价软元件（None 表示无别名）</summary>
        public McDeviceKind AliasOf { get; private set; }

        /// <summary>1E 别名的编号偏移（手册：SM1000~SM1255 对应 M9000~M9255，即偏移 8000）</summary>
        public int AliasOffset { get; private set; }

        /// <summary>每个元素占用的字数（iQ-R 长定时器为 4）</summary>
        public int WordsPerPoint { get; private set; }

        /// <summary>全部描述符</summary>
        public static IReadOnlyList<McDeviceDescriptor> All
        {
            get { return _all.AsReadOnly(); }
        }

        /// <summary>按种类查找描述符</summary>
        public static McDeviceDescriptor Find(McDeviceKind kind)
        {
            McDeviceDescriptor descriptor;
            if (!_byKind.TryGetValue(kind, out descriptor))
                throw new ArgumentException($"未知的 MC 软元件种类: {kind}", nameof(kind));
            return descriptor;
        }

        /// <summary>
        /// 按报文里的软元件码反查描述符（服务端解析请求用）。
        /// 1E 用 2 字节 ASCII 码；3E/4E 的 Q/L 系用 1 字节码，iQ-R 用 2 字节码。
        /// </summary>
        /// <param name="frameType">帧类型</param>
        /// <param name="isIqR">是否 iQ-R 子指令（仅 3E/4E 有效）</param>
        /// <param name="code">报文中的软元件码</param>
        /// <returns>找不到时返回 null（由调用方回 C05B 软元件指定错误）</returns>
        public static McDeviceDescriptor FindByCode(McFrameType frameType, bool isIqR, ushort code)
        {
            McDeviceDescriptor descriptor;
            if (frameType == McFrameType.A1E)
                return _byCode1E.TryGetValue(code, out descriptor) ? descriptor : null;

            if (isIqR)
                return _byCodeR3E.TryGetValue(code, out descriptor) ? descriptor : null;

            if (code > 0xFF)
                return null;

            return _byCodeQ3E.TryGetValue(code, out descriptor) ? descriptor : null;
        }

        /// <summary>
        /// 按最长前缀匹配软元件。调用方需保证 text 已转为大写。
        /// </summary>
        public static bool TryMatchPrefix(string text, out McDeviceDescriptor descriptor, out int prefixLength)
        {
            descriptor = null;
            prefixLength = 0;
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < _matchOrder.Count; i++)
            {
                var candidate = _matchOrder[i];
                if (text.Length >= candidate.Prefix.Length &&
                    string.CompareOrdinal(text, 0, candidate.Prefix, 0, candidate.Prefix.Length) == 0)
                {
                    descriptor = candidate;
                    prefixLength = candidate.Prefix.Length;
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Prefix;
        }
    }
}

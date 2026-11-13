using System;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 协议「帧类型 × PLC 系列」派生规则：子指令、软元件码宽度、设备号宽度、
    /// X/Y 编号进制、可用软元件与点数上限。
    /// </summary>
    public sealed class McProfile
    {
        private const McSeries ValidSeries = McSeries.Fx | McSeries.A | McSeries.Q | McSeries.L | McSeries.IqR;

        /// <summary>创建派生规则</summary>
        /// <param name="frameType">帧类型</param>
        /// <param name="series">PLC 系列</param>
        public McProfile(McFrameType frameType, McSeries series)
        {
            int seriesValue = (int)series;
            if ((series & ~ValidSeries) != 0 || (seriesValue & (seriesValue - 1)) != 0)
                throw new ArgumentException($"无效的三菱系列: {series}", nameof(series));

            if (frameType == McFrameType.A1E && series != McSeries.A)
                throw new ArgumentException("A-1E 帧只适用于 A 系列（McSeries.A）", nameof(series));

            if (frameType != McFrameType.A1E && series == McSeries.A)
                throw new ArgumentException("A 系列只支持 A-1E 帧，其余系列请使用 Q3E / Q4E", nameof(series));

            FrameType = frameType;
            Series = series;
        }

        /// <summary>帧类型</summary>
        public McFrameType FrameType { get; private set; }

        /// <summary>PLC 系列</summary>
        public McSeries Series { get; private set; }

        /// <summary>是否为 iQ-R 系列（2 字节软元件码、4 字节设备号、子指令 0002/0003）</summary>
        public bool IsIqR
        {
            get { return Series == McSeries.IqR; }
        }

        /// <summary>设备号字节宽度：3E/4E 下 Q/L 为 3，iQ-R 为 4；1E 固定 4</summary>
        public int AddressWidth
        {
            get { return FrameType == McFrameType.A1E || IsIqR ? 4 : 3; }
        }

        /// <summary>软元件码字节宽度：1E 与 iQ-R 为 2，Q/L 为 1</summary>
        public int DeviceCodeWidth
        {
            get { return FrameType == McFrameType.A1E || IsIqR ? 2 : 1; }
        }

        /// <summary>取子指令，1E 帧没有子指令</summary>
        public ushort GetSubCommand(bool isBit)
        {
            if (FrameType == McFrameType.A1E)
                throw new NotSupportedException("A-1E 帧没有子指令字段");

            if (IsIqR)
                return isBit ? (ushort)0x0003 : (ushort)0x0002;

            return isBit ? (ushort)0x0001 : (ushort)0x0000;
        }

        /// <summary>取软元件码字节（小端）</summary>
        public byte[] GetDeviceCode(McDeviceDescriptor device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            if (FrameType == McFrameType.A1E)
                return new[] { (byte)(device.Code1E & 0xFF), (byte)(device.Code1E >> 8) };

            if (IsIqR)
                return new[] { (byte)(device.CodeR3E & 0xFF), (byte)(device.CodeR3E >> 8) };

            return new[] { device.CodeQ3E };
        }

        private int? _addressRadixOverride;

        /// <summary>
        /// X/Y 编号进制的手工覆盖（null 表示按系列默认规则）。
        /// 现场编号规则与默认规则不一致时使用，避免为此修改库。
        /// </summary>
        public int? AddressRadixOverride
        {
            get { return _addressRadixOverride; }
            set
            {
                if (value.HasValue && value.Value != 8 && value.Value != 10 && value.Value != 16)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "进制只能是 8、10 或 16");
                _addressRadixOverride = value;
            }
        }

        /// <summary>取软元件编号进制（FX 的 X/Y 按 8 进制）</summary>
        public int GetRadix(McDeviceDescriptor device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            if (device.Kind == McDeviceKind.X || device.Kind == McDeviceKind.Y)
            {
                if (_addressRadixOverride.HasValue)
                    return _addressRadixOverride.Value;

                if (Series == McSeries.Fx)
                    return 8;
            }

            return device.Radix;
        }

        /// <summary>单帧最大点数（按手册每通信最大点数）</summary>
        public int GetMaxPoints(bool isBit)
        {
            return McFrameCodec.GetMaxPoints(FrameType, IsIqR, isBit);
        }

        /// <summary>批量合并时使用的保守上限（Q 与 iQ-R 都不会越界）</summary>
        public int GetBatchMaxPoints(bool isBit)
        {
            if (FrameType == McFrameType.A1E)
                return GetMaxPoints(isBit);

            return isBit ? 7168 : 960;
        }

        /// <summary>校验软元件在当前「帧 × 系列」下可用，不可用时抛出 <see cref="NotSupportedException"/></summary>
        public void EnsureSupported(McDeviceDescriptor device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            if ((device.SeriesMask & Series) == 0)
                throw new NotSupportedException($"三菱 {GetSeriesName(Series)} 系列不支持软元件 {device.Prefix}");

            if (FrameType == McFrameType.A1E)
            {
                if (device.Code1E == 0 && device.AliasOf == McDeviceKind.None)
                    throw new NotSupportedException($"A-1E 帧不支持软元件 {device.Prefix}");
                return;
            }

            if (IsIqR ? device.CodeR3E == 0 : device.CodeQ3E == 0)
                throw new NotSupportedException($"三菱 {FrameType} 帧不支持软元件 {device.Prefix}");
        }

        /// <summary>系列名称</summary>
        public static string GetSeriesName(McSeries series)
        {
            switch (series)
            {
                case McSeries.Fx: return "FX";
                case McSeries.A: return "A";
                case McSeries.Q: return "Q";
                case McSeries.L: return "L";
                case McSeries.IqR: return "iQ-R";
                default: return series.ToString();
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{FrameType}/{GetSeriesName(Series)}";
        }
    }
}

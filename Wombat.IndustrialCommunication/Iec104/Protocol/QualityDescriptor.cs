using System;

namespace Wombat.IndustrialCommunication.Iec104.Protocol
{
    /// <summary>
    /// 品质描述位。注意 bit0 在 QDS 中为 OV（溢出），在 SIQ/DIQ 中为 SPI/DPI，
    /// 因此单点/双点的状态位不要与本枚举的 Overflow 混用。
    /// </summary>
    [Flags]
    public enum Iec104QualityFlags : byte
    {
        None = 0x00,
        Overflow = 0x01,
        Blocked = 0x10,
        Substituted = 0x20,
        NotTopical = 0x40,
        Invalid = 0x80,
    }

    /// <summary>双点信息状态。</summary>
    public enum Iec104DoublePointState : byte
    {
        NotPermitted = 0,
        Off = 1,
        On = 2,
        Indeterminate = 3,
    }
}

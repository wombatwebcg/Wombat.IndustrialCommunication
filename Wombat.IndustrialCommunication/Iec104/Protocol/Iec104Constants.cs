using System;

namespace Wombat.IndustrialCommunication.Iec104.Protocol
{
    /// <summary>
    /// 104 协议常量。所有魔法数字集中在此，协议层不得出现裸数字。
    /// </summary>
    public static class Iec104Constants
    {
        /// <summary>默认服务端口。</summary>
        public const int DefaultPort = 2404;

        /// <summary>APCI 启动字符。</summary>
        public const byte StartByte = 0x68;

        /// <summary>APDU 最大总长（含启动字符与长度字段）。</summary>
        public const int MaxApduLength = 255;

        /// <summary>APDU 长度字段上限。</summary>
        public const int MaxLengthField = MaxApduLength - 2;

        /// <summary>ASDU 最大长度。</summary>
        public const int MaxAsduLength = MaxLengthField - 4;

        /// <summary>APCI 固定长度：启动字符 + 长度 + 四字节控制域。</summary>
        public const int ApciLength = 6;

        /// <summary>ASDU 头部长度：类型标识 + 可变结构限定词 + 传送原因 + 公共地址。</summary>
        public const int AsduHeaderLength = 6;

        /// <summary>信息对象地址长度。</summary>
        public const int InformationObjectAddressLength = 3;

        /// <summary>信息对象地址上限。</summary>
        public const int MaxInformationObjectAddress = 0xFFFFFF;

        /// <summary>可变结构限定词中对象个数上限。</summary>
        public const int MaxObjectsPerAsdu = 127;

        /// <summary>默认 k 值：未确认 I 帧上限。</summary>
        public const int DefaultK = 12;

        /// <summary>默认 w 值：累计确认阈值。</summary>
        public const int DefaultW = 8;

        /// <summary>默认 t0：建链超时。</summary>
        public static readonly TimeSpan DefaultT0 = TimeSpan.FromSeconds(30);

        /// <summary>默认 t1：发送或测试帧的确认超时。</summary>
        public static readonly TimeSpan DefaultT1 = TimeSpan.FromSeconds(15);

        /// <summary>默认 t2：接收后回 S 帧的上限等待。</summary>
        public static readonly TimeSpan DefaultT2 = TimeSpan.FromSeconds(10);

        /// <summary>默认 t3：空闲后发测试帧的间隔。</summary>
        public static readonly TimeSpan DefaultT3 = TimeSpan.FromSeconds(20);

        /// <summary>站总召唤的限定词。</summary>
        public const byte QualifierStationInterrogation = 20;

        /// <summary>序号掩码（15 位）。</summary>
        public const ushort SequenceMask = 0x7FFF;
    }
}

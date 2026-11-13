using System;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 三菱 MC 协议归一化后的软元件种类。
    /// 服务端数据区与客户端缓存一律按此枚举索引，协议码值只存在于编解码层。
    /// </summary>
    public enum McDeviceKind
    {
        /// <summary>未指定</summary>
        None = 0,
        /// <summary>特殊辅助继电器 SM</summary>
        SM,
        /// <summary>特殊数据寄存器 SD</summary>
        SD,
        /// <summary>输入继电器 X</summary>
        X,
        /// <summary>输出继电器 Y</summary>
        Y,
        /// <summary>内部继电器 M</summary>
        M,
        /// <summary>锁存继电器 L</summary>
        L,
        /// <summary>报警器 F</summary>
        F,
        /// <summary>边沿继电器 V</summary>
        V,
        /// <summary>链接继电器 B</summary>
        B,
        /// <summary>数据寄存器 D</summary>
        D,
        /// <summary>链接寄存器 W</summary>
        W,
        /// <summary>文件寄存器 R</summary>
        R,
        /// <summary>文件寄存器 ZR</summary>
        ZR,
        /// <summary>变址寄存器 Z</summary>
        Z,
        /// <summary>链接特殊继电器 SB</summary>
        SB,
        /// <summary>链接特殊寄存器 SW</summary>
        SW,
        /// <summary>步进继电器 S</summary>
        S,
        /// <summary>直接访问输入 DX</summary>
        DX,
        /// <summary>直接访问输出 DY</summary>
        DY,
        /// <summary>定时器触点 TS</summary>
        TS,
        /// <summary>定时器线圈 TC</summary>
        TC,
        /// <summary>定时器当前值 TN</summary>
        TN,
        /// <summary>累计定时器触点 SS</summary>
        SS,
        /// <summary>累计定时器线圈 SC</summary>
        SC,
        /// <summary>累计定时器当前值 SN</summary>
        SN,
        /// <summary>计数器触点 CS</summary>
        CS,
        /// <summary>计数器线圈 CC</summary>
        CC,
        /// <summary>计数器当前值 CN</summary>
        CN,
        /// <summary>刷新数据寄存器 RD（仅 iQ-R）</summary>
        RD,
        /// <summary>长索引寄存器 LZ（仅 iQ-R）</summary>
        LZ,
        /// <summary>长定时器触点 LTS（仅 iQ-R）</summary>
        LTS,
        /// <summary>长定时器线圈 LTC（仅 iQ-R）</summary>
        LTC,
        /// <summary>长定时器当前值 LTN（仅 iQ-R，4 字/点）</summary>
        LTN,
        /// <summary>长累计定时器触点 LSTS（仅 iQ-R）</summary>
        LSTS,
        /// <summary>长累计定时器线圈 LSTC（仅 iQ-R）</summary>
        LSTC,
        /// <summary>长累计定时器当前值 LSTN（仅 iQ-R，4 字/点）</summary>
        LSTN,
        /// <summary>长计数器触点 LCS（仅 iQ-R）</summary>
        LCS,
        /// <summary>长计数器线圈 LCC（仅 iQ-R）</summary>
        LCC,
        /// <summary>长计数器当前值 LCN（仅 iQ-R，4 字/点）</summary>
        LCN,
    }
}

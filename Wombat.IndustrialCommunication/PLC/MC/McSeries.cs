using System;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 三菱 PLC 系列。组合值用于描述软元件的可用系列。
    /// </summary>
    [Flags]
    public enum McSeries
    {
        /// <summary>未指定</summary>
        None = 0,
        /// <summary>FX 系列（X/Y 按 8 进制编号）</summary>
        Fx = 1,
        /// <summary>A 系列（使用 A-1E 帧）</summary>
        A = 2,
        /// <summary>Q 系列</summary>
        Q = 4,
        /// <summary>L 系列</summary>
        L = 8,
        /// <summary>iQ-R 系列（子指令 0002/0003、2 字节软元件码、4 字节设备号）</summary>
        IqR = 16,
    }
}

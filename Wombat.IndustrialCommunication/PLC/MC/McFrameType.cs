namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 三菱 MC 协议帧类型。帧类型互斥，不是位标志。
    /// </summary>
    public enum McFrameType
    {
        /// <summary>A 兼容 1E 帧（二进制）</summary>
        A1E = 0,
        /// <summary>Qna-3E 帧（二进制）</summary>
        Q3E = 1,
        /// <summary>Qna-4E 帧（二进制）</summary>
        Q4E = 2,
    }
}

using System;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 帧解析结果：帧头字段 + 结束码 + 数据区。请求与响应共用。
    /// </summary>
    public sealed class McPacket
    {
        /// <summary>帧类型</summary>
        public McFrameType FrameType { get; set; }

        /// <summary>副头部（1E 的 0x80~0x83、3E 的 0xD0、4E 的 0xD4；请求侧为 0x00~0x03 / 0x50 / 0x54）</summary>
        public byte SubHeader { get; set; }

        /// <summary>指令（批量读 0x0401、批量写 0x1401）</summary>
        public ushort Command { get; set; }

        /// <summary>子指令（3E/4E：0000 字 / 0001 位 / 0002 字 iQ-R / 0003 位 iQ-R；1E 无此字段）</summary>
        public ushort SubCommand { get; set; }

        /// <summary>请求中的软元件描述符（解析请求时填充）</summary>
        public McDeviceDescriptor Device { get; set; }

        /// <summary>请求的起始软元件编号（十进制，别名展开后）</summary>
        public int StartAddress { get; set; }

        /// <summary>请求的协议点数（长定时器为字号数）</summary>
        public int Points { get; set; }

        /// <summary>请求是否为位单位访问</summary>
        public bool IsBit { get; set; }

        /// <summary>请求的子指令是否指示 iQ-R 系（2 字节软元件码、4 字节设备号）</summary>
        public bool IsIqR { get; set; }

        /// <summary>请求是否为写指令</summary>
        public bool IsWrite
        {
            get { return Command == McFrameCodec.CommandBatchWrite; }
        }

        /// <summary>序列号（仅 4E）</summary>
        public ushort SerialNumber { get; set; }

        /// <summary>网络号</summary>
        public byte NetworkNumber { get; set; }

        /// <summary>PC 号</summary>
        public byte PcNumber { get; set; }

        /// <summary>目标模块 IO 号</summary>
        public ushort ModuleIoNumber { get; set; }

        /// <summary>目标站号</summary>
        public byte StationNumber { get; set; }

        /// <summary>结束码（1E 为 1 字节扩展值，其余 2 字节）</summary>
        public ushort EndCode { get; set; }

        /// <summary>1E 异常码（结束码为 0x5B 时有效，否则为 0）</summary>
        public byte AbnormalCode { get; set; }

        /// <summary>数据区（结束码非正常时为空）</summary>
        public byte[] Data { get; set; }

        /// <summary>结束码描述（原始码 + 含义）</summary>
        public string ErrorMessage { get; set; }

        /// <summary>是否正常结束</summary>
        public bool IsSuccess
        {
            get { return EndCode == 0 && AbnormalCode == 0; }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return IsSuccess
                ? $"{FrameType} 正常，数据 {Data?.Length ?? 0} 字节"
                : $"{FrameType} 失败：{ErrorMessage}";
        }
    }
}

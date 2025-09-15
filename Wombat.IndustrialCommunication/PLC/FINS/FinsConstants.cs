using System;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS协议常量定义
    /// </summary>
    public static class FinsConstants
    {
        /// <summary>
        /// FINS协议魔数
        /// </summary>
        public const uint FINS_MAGIC = 0x46494E53;

        /// <summary>
        /// 默认FINS端口
        /// </summary>
        public const int DEFAULT_PORT = 9600;

        /// <summary>
        /// FINS头部长度
        /// </summary>
        public const int FINS_HEADER_LENGTH = 10;

        /// <summary>
        /// 命令码定义
        /// </summary>
        public static class CommandCodes
        {
            /// <summary>
            /// 内存区域读取
            /// </summary>
            public const byte MEMORY_AREA_READ = 0x01;

            /// <summary>
            /// 内存区域写入
            /// </summary>
            public const byte MEMORY_AREA_WRITE = 0x02;

            /// <summary>
            /// 内存区域填充
            /// </summary>
            public const byte MEMORY_AREA_FILL = 0x03;

            /// <summary>
            /// 多个内存区域读取
            /// </summary>
            public const byte MULTIPLE_MEMORY_AREA_READ = 0x04;

            /// <summary>
            /// 内存区域传输
            /// </summary>
            public const byte MEMORY_AREA_TRANSFER = 0x05;
        }

        /// <summary>
        /// ICF (Information Control Field) 标志
        /// </summary>
        public static class ICF
        {
            /// <summary>
            /// 响应标志位
            /// </summary>
            public const byte RESPONSE_FLAG = 0x40;

            /// <summary>
            /// 网关使用标志
            /// </summary>
            public const byte GATEWAY_FLAG = 0x02;

            /// <summary>
            /// 数据类型标志
            /// </summary>
            public const byte DATA_TYPE_FLAG = 0x01;
        }

        /// <summary>
        /// 错误码定义
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>
            /// 正常完成
            /// </summary>
            public const ushort NORMAL_COMPLETION = 0x0000;

            /// <summary>
            /// 服务被取消
            /// </summary>
            public const ushort SERVICE_CANCELED = 0x0001;

            /// <summary>
            /// 本地节点错误
            /// </summary>
            public const ushort LOCAL_NODE_ERROR = 0x0101;

            /// <summary>
            /// 目标节点错误
            /// </summary>
            public const ushort DESTINATION_NODE_ERROR = 0x0102;

            /// <summary>
            /// 通信控制器错误
            /// </summary>
            public const ushort CONTROLLER_ERROR = 0x0103;

            /// <summary>
            /// 服务不支持
            /// </summary>
            public const ushort SERVICE_UNSUPPORTED = 0x0104;

            /// <summary>
            /// 路由表错误
            /// </summary>
            public const ushort ROUTING_TABLE_ERROR = 0x0105;

            /// <summary>
            /// 命令格式错误
            /// </summary>
            public const ushort COMMAND_FORMAT_ERROR = 0x1001;

            /// <summary>
            /// 参数错误
            /// </summary>
            public const ushort PARAMETER_ERROR = 0x1002;

            /// <summary>
            /// 读取长度过长
            /// </summary>
            public const ushort READ_LENGTH_TOO_LONG = 0x1003;

            /// <summary>
            /// 命令长度过长
            /// </summary>
            public const ushort COMMAND_LENGTH_TOO_LONG = 0x1004;

            /// <summary>
            /// 命令长度过短
            /// </summary>
            public const ushort COMMAND_LENGTH_TOO_SHORT = 0x1005;

            /// <summary>
            /// 内存区域错误
            /// </summary>
            public const ushort MEMORY_AREA_ERROR = 0x1101;

            /// <summary>
            /// 地址范围错误
            /// </summary>
            public const ushort ADDRESS_RANGE_ERROR = 0x1102;

            /// <summary>
            /// 地址范围超出
            /// </summary>
            public const ushort ADDRESS_RANGE_EXCEEDED = 0x1103;
        }
    }

    /// <summary>
    /// FINS内存区域类型
    /// </summary>
    public enum FinsMemoryArea : byte
    {
        /// <summary>
        /// CIO区域
        /// </summary>
        CIO = 0x30,

        /// <summary>
        /// 工作区域
        /// </summary>
        WR = 0x31,

        /// <summary>
        /// 保持区域
        /// </summary>
        HR = 0x32,

        /// <summary>
        /// 辅助区域
        /// </summary>
        AR = 0x33,

        /// <summary>
        /// 数据内存区域
        /// </summary>
        DM = 0x82,

        /// <summary>
        /// 扩展数据内存区域
        /// </summary>
        EM = 0x90,

        /// <summary>
        /// 定时器标志
        /// </summary>
        TIM_FLAG = 0x09,

        /// <summary>
        /// 定时器当前值
        /// </summary>
        TIM_PV = 0x89,

        /// <summary>
        /// 计数器标志
        /// </summary>
        CNT_FLAG = 0x07,

        /// <summary>
        /// 计数器当前值
        /// </summary>
        CNT_PV = 0x87
    }

    /// <summary>
    /// FINS数据类型
    /// </summary>
    public enum FinsDataType
    {
        /// <summary>
        /// 位数据
        /// </summary>
        Bit,

        /// <summary>
        /// 字节数据
        /// </summary>
        Byte,

        /// <summary>
        /// 字数据
        /// </summary>
        Word,

        /// <summary>
        /// 双字数据
        /// </summary>
        DWord,

        /// <summary>
        /// 字符串数据
        /// </summary>
        String
    }

    /// <summary>
    /// FINS协议版本
    /// </summary>
    public enum FinsVersion
    {
        /// <summary>
        /// FINS/TCP版本
        /// </summary>
        TCP = 1,

        /// <summary>
        /// FINS/UDP版本
        /// </summary>
        UDP = 2
    }
}
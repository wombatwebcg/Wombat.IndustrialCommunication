namespace Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge
{
    /// <summary>健康区寄存器偏移，下游据此判断数据是否可信。</summary>
    public static class BridgeHealthRegister
    {
        /// <summary>104 链路状态：0 断开、1 已连接未激活、2 激活。</summary>
        public const int LinkState = 0;

        /// <summary>遥测最后刷新距今秒数。</summary>
        public const int TelemetryAge = 1;

        /// <summary>遥信最后刷新距今秒数。</summary>
        public const int DiscreteAge = 2;

        /// <summary>已映射遥测点数。</summary>
        public const int TelemetryCount = 3;

        /// <summary>已映射遥信点数。</summary>
        public const int DiscreteCount = 4;

        /// <summary>点表版本（映射表 CRC16）。</summary>
        public const int MapVersion = 5;

        /// <summary>桥接运行状态：0 停止、1 运行。</summary>
        public const int Running = 6;

        /// <summary>保留。</summary>
        public const int Reserved = 7;
    }
}

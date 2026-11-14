using System;
using System.Collections.Generic;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge
{
    /// <summary>遥测 float 拆成两个 16 位寄存器时的字序。</summary>
    public enum ModbusFloatWordOrder
    {
        /// <summary>高 16 位在前，多数国产 SCADA 使用。</summary>
        Abcd = 0,

        /// <summary>低 16 位在前，部分 PLC 与海外组态使用。</summary>
        Cdab = 1,
    }

    /// <summary>断链超过 StaleTimeout 后，读请求的返回方式。</summary>
    public enum ModbusStaleReadMode
    {
        /// <summary>继续返回最后值，靠健康区告警。</summary>
        LastValue = 0,

        /// <summary>读数据区返回 Modbus 异常码 0x04。</summary>
        Exception = 1,
    }

    /// <summary>桥接层参数。</summary>
    public sealed class ModbusBridgeOptions
    {
        /// <summary>Modbus TCP 监听地址。</summary>
        public string BindAddress { get; set; } = "0.0.0.0";

        /// <summary>Modbus TCP 监听端口。</summary>
        public int Port { get; set; } = 502;

        /// <summary>Modbus 从站号，必须与下游配置一致，否则请求被静默丢弃。</summary>
        public byte UnitId { get; set; } = 1;

        /// <summary>1x 离散输入基准：地址 = 基准 + 转发号。</summary>
        public int DiscreteBase { get; set; }

        /// <summary>3x 输入寄存器基准：地址 = 基准 + 转发号 × 2。</summary>
        public int TelemetryBase { get; set; }

        /// <summary>4x 保持寄存器命令区基准：地址 = 基准 + 转发号 × 3。</summary>
        public int CommandBase { get; set; }

        /// <summary>4x 保持寄存器健康区基准，占 8 个寄存器。</summary>
        public int HealthBase { get; set; } = 1000;

        /// <summary>遥测字序。</summary>
        public ModbusFloatWordOrder WordOrder { get; set; } = ModbusFloatWordOrder.Abcd;

        /// <summary>脏点刷写周期。</summary>
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(50);

        /// <summary>是否启用命令通道。默认关闭，关闭时写命令寄存器只回“未启用”。</summary>
        public bool EnableCommands { get; set; }

        /// <summary>命令方式：SelectAndExecute 先发 104 选择帧，Direct 只发执行帧。</summary>
        public Iec104CommandMode CommandMode { get; set; } = Iec104CommandMode.SelectAndExecute;

        /// <summary>允许遥控的 IOA 白名单；为空表示不限制。</summary>
        public IReadOnlyCollection<int> AllowedCommandIoa { get; set; }

        /// <summary>进入待确认后等待确认码的窗口，超时置状态 7。</summary>
        public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>选择帧等待确认的超时，作为 104 侧 t1 之外的兜底。</summary>
        public TimeSpan SelectTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>断链后读请求的返回方式。</summary>
        public ModbusStaleReadMode StaleReadMode { get; set; } = ModbusStaleReadMode.LastValue;

        /// <summary>断链超过该时长视为陈旧数据。</summary>
        public TimeSpan StaleTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>校验参数，越界时直接抛异常，避免带病启动。</summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(BindAddress))
                throw new ArgumentException("BindAddress 不能为空。", nameof(BindAddress));

            if (Port < 1 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port 必须在 1..65535 之间。");

            if (UnitId < 1 || UnitId > 247)
                throw new ArgumentOutOfRangeException(nameof(UnitId), "UnitId 必须在 1..247 之间。");

            if (FlushInterval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(FlushInterval), "FlushInterval 必须大于 0。");

            if (ConfirmTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(ConfirmTimeout), "ConfirmTimeout 必须大于 0。");

            if (SelectTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(SelectTimeout), "SelectTimeout 必须大于 0。");

            if (StaleTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(StaleTimeout), "StaleTimeout 必须大于 0。");

            if (DiscreteBase < 0)
                throw new ArgumentOutOfRangeException(nameof(DiscreteBase), "DiscreteBase 不能为负。");

            if (TelemetryBase < 0)
                throw new ArgumentOutOfRangeException(nameof(TelemetryBase), "TelemetryBase 不能为负。");

            if (CommandBase < 0)
                throw new ArgumentOutOfRangeException(nameof(CommandBase), "CommandBase 不能为负。");

            if (HealthBase < 0)
                throw new ArgumentOutOfRangeException(nameof(HealthBase), "HealthBase 不能为负。");
        }
    }
}
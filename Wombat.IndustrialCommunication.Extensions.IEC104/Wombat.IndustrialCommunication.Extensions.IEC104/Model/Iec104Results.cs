using System;
using System.Collections.Generic;
using Wombat.IndustrialCommunication.Extensions.IEC104.Protocol;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Model
{
    /// <summary>遥控执行方式。</summary>
    public enum Iec104CommandMode
    {
        /// <summary>直接执行。</summary>
        Direct,

        /// <summary>先选择，确认后再执行。</summary>
        SelectAndExecute,
    }

    /// <summary>遥控所处阶段。</summary>
    public enum Iec104CommandStage
    {
        None,
        Select,
        Execute,
        Termination,
    }

    /// <summary>遥控结果。</summary>
    public sealed class Iec104CommandResult
    {
        public int Ioa { get; set; }

        /// <summary>是否被接受（收到肯定确认并完成）。</summary>
        public bool Accepted { get; set; }

        /// <summary>是否收到否定确认。</summary>
        public bool Negative { get; set; }

        public Iec104Cause Cause { get; set; }

        public Iec104CommandStage Stage { get; set; }

        public bool TimedOut { get; set; }

        public TimeSpan Duration { get; set; }

        public string Message { get; set; }
    }

    /// <summary>总召唤结果。</summary>
    public sealed class Iec104InterrogationResult
    {
        /// <summary>是否收到激活终止。</summary>
        public bool Completed { get; set; }

        /// <summary>是否因超时结束（此时 Completed 为 false）。</summary>
        public bool TimedOut { get; set; }

        /// <summary>是否收到激活确认。</summary>
        public bool Confirmed { get; set; }

        public int ReceivedCount { get; set; }

        /// <summary>点表预期点数；未配置点表时等于已收点数。</summary>
        public int ExpectedCount { get; set; }

        /// <summary>点表中存在但总召未收到的 IOA。</summary>
        public IReadOnlyList<int> MissingIoa { get; set; } = Array.Empty<int>();

        public TimeSpan Duration { get; set; }
    }
}

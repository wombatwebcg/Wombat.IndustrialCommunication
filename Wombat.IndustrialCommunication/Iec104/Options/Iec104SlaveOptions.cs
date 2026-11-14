using System;
using Wombat.IndustrialCommunication.Iec104.Protocol;

namespace Wombat.IndustrialCommunication.Iec104.Options
{
    /// <summary>从站参数。</summary>
    public sealed class Iec104SlaveOptions
    {
        public string BindAddress { get; set; } = "0.0.0.0";

        public int Port { get; set; } = Iec104Constants.DefaultPort;

        public ushort CommonAddress { get; set; } = 1;

        public int K { get; set; } = Iec104Constants.DefaultK;

        public int W { get; set; } = Iec104Constants.DefaultW;

        public TimeSpan T1 { get; set; } = Iec104Constants.DefaultT1;

        public TimeSpan T2 { get; set; } = Iec104Constants.DefaultT2;

        public TimeSpan T3 { get; set; } = Iec104Constants.DefaultT3;

        /// <summary>周期上送间隔，Zero 表示关闭。</summary>
        public TimeSpan CyclicPeriod { get; set; } = TimeSpan.Zero;

        /// <summary>STARTDT 之后是否上送初始化结束帧。</summary>
        public bool SendEndOfInitialization { get; set; } = true;

        /// <summary>总召期间是否暂停突发与周期上送。</summary>
        public bool PauseUnsolicitedDuringInterrogation { get; set; } = true;

        /// <summary>遥控是否强制选择-执行。</summary>
        public bool RequireSelectBeforeOperate { get; set; }

        /// <summary>选择态保持时间。</summary>
        public TimeSpan SelectTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>是否输出收发帧追踪。</summary>
        public bool EnableTrace { get; set; }

        public int MaxConnections { get; set; } = 8;

        /// <summary>点表，决定 IOA、类型与死区。</summary>
        public Model.Iec104PointTable PointTable { get; set; }
    }
}

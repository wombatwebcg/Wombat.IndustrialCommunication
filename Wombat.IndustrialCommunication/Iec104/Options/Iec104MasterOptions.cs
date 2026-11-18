using System;
using Wombat.IndustrialCommunication.Iec104.Protocol;

namespace Wombat.IndustrialCommunication.Iec104.Options
{
    /// <summary>主站参数。</summary>
    public sealed class Iec104MasterOptions
    {
        public string Host { get; set; } = "127.0.0.1";

        public int Port { get; set; } = Iec104Constants.DefaultPort;

        public ushort CommonAddress { get; set; } = 1;

        public int K { get; set; } = Iec104Constants.DefaultK;

        public int W { get; set; } = Iec104Constants.DefaultW;

        public TimeSpan ConnectTimeout { get; set; } = Iec104Constants.DefaultT0;

        public TimeSpan T1 { get; set; } = Iec104Constants.DefaultT1;

        public TimeSpan T2 { get; set; } = Iec104Constants.DefaultT2;

        public TimeSpan T3 { get; set; } = Iec104Constants.DefaultT3;

        /// <summary>一次总召唤的整体超时。</summary>
        public TimeSpan InterrogationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>遥控每一步的超时。</summary>
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>断链后是否自动重连。</summary>
        public bool AutoReconnect { get; set; }

        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>周期总召间隔，Zero 表示不开启。</summary>
        public TimeSpan CyclicInterrogation { get; set; } = TimeSpan.Zero;

        /// <summary>连接成功并激活后是否立即总召一次。</summary>
        public bool InterrogateOnConnect { get; set; }

        /// <summary>是否输出收发帧追踪。</summary>
        public bool EnableTrace { get; set; }

        /// <summary>
        /// 是否严格校验接收序号 N(S) 与确认序号 N(R)。默认 false：序号不符时自动重新对齐并计数，
        /// 避免现场装置计数器跨连接保留或提前确认导致刚连上就断链。
        /// </summary>
        public bool StrictSequence { get; set; }

        /// <summary>可选点表，用于统计缺失点与按转发号查询。</summary>
        public Model.Iec104PointTable PointTable { get; set; }
    }
}

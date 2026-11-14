using System;
using System.Collections.Generic;
using Wombat.IndustrialCommunication.Extensions.IEC104.Diagnostics;
using Wombat.IndustrialCommunication.Extensions.IEC104.Model;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge
{
    /// <summary>
    /// 桥接层的 104 数据来源。生产环境用 <see cref="Iec104MasterValueSource"/> 包装主站，
    /// 测试可注入自己的实现，不必真的建链。
    /// </summary>
    public interface IIec104ValueSource
    {
        /// <summary>点表，决定地址映射。</summary>
        Iec104PointTable PointTable { get; }

        /// <summary>当前链路状态。</summary>
        Iec104LinkState LinkState { get; }

        /// <summary>取某个 IOA 的当前缓存值。</summary>
        bool TryGetValue(int ioa, out Iec104Value value);

        /// <summary>取全部缓存值的快照，用于启动时的初始镜像。</summary>
        IReadOnlyDictionary<int, Iec104Value> Snapshot();

        /// <summary>缓存值变化。可能在 104 接收线程上触发，订阅方只应记录脏点。</summary>
        event EventHandler<Iec104ValueEventArgs> ValueChanged;

        /// <summary>链路状态变化。</summary>
        event EventHandler<Iec104LinkEventArgs> LinkStateChanged;
    }
}

using System;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Model
{
    using Wombat.IndustrialCommunication.Extensions.IEC104.Protocol;

    /// <summary>
    /// 点位定义。Ioa 为协议地址，ForwardNo 为点表中的转发号，两者不一定相等。
    /// </summary>
    public sealed class Iec104Point
    {
        public ushort CommonAddress { get; set; } = 1;

        /// <summary>信息对象地址（1..16777215）。</summary>
        public int Ioa { get; set; }

        /// <summary>点表中的转发号。</summary>
        public int ForwardNo { get; set; }

        public Iec104TypeId TypeId { get; set; }

        public string Channel { get; set; }

        public string Device { get; set; }

        public string Description { get; set; }

        /// <summary>工程量系数。</summary>
        public double Scale { get; set; } = 1d;

        /// <summary>工程量偏移。</summary>
        public double Offset { get; set; }

        /// <summary>变化死区，0 表示不做抑制。</summary>
        public double Deadband { get; set; }

        /// <summary>反极性：上位值与协议状态取反。</summary>
        public bool Invert { get; set; }

        /// <summary>是否为 SOE 点（变位时带时标上送）。</summary>
        public bool IsSoe { get; set; }

        /// <summary>是否为遥控点。</summary>
        public bool IsCommand { get; set; }

        /// <summary>业务侧数据类型，供网关映射使用。</summary>
        public DataTypeEnums DataType { get; set; } = DataTypeEnums.None;

        /// <summary>总召唤分组，1..16。</summary>
        public int InterrogationGroup { get; set; } = 1;

        /// <summary>工程量换算：协议值乘系数加偏移。</summary>
        public double ToEngineering(double raw)
        {
            return raw * Scale + Offset;
        }

        /// <summary>逆换算：工程量还原为协议值。</summary>
        public double ToProtocol(double engineering)
        {
            return Scale == 0 ? engineering : (engineering - Offset) / Scale;
        }
    }
}

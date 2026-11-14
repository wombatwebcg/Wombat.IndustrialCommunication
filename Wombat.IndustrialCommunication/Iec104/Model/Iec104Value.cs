using System;
using Wombat.IndustrialCommunication.Iec104.Protocol;

namespace Wombat.IndustrialCommunication.Iec104.Model
{
    /// <summary>
    /// 主站缓存中的一个点值。数值统一为 double，单点同时给出布尔语义。
    /// </summary>
    public sealed class Iec104Value
    {
        public int Ioa { get; set; }

        public ushort CommonAddress { get; set; }

        public Iec104TypeId TypeId { get; set; }

        public double Numeric { get; set; }

        public bool Boolean { get; set; }

        public Iec104QualityFlags Quality { get; set; }

        /// <summary>是否携带 CP56Time2a 时标。</summary>
        public bool HasTimestamp { get; set; }

        /// <summary>时标；无时标时等于接收时间。</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>本地接收时间。</summary>
        public DateTime ReceivedAt { get; set; }

        /// <summary>由信息对象构造值。</summary>
        public static Iec104Value FromObject(ushort commonAddress, Iec104InformationObject item, DateTime receivedAt)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            var value = new Iec104Value
            {
                Ioa = item.Ioa,
                CommonAddress = commonAddress,
                TypeId = item.TypeId,
                Quality = item.GetQuality(),
                ReceivedAt = receivedAt,
                Timestamp = receivedAt,
            };

            switch (item.TypeId)
            {
                case Iec104TypeId.SinglePoint:
                case Iec104TypeId.SinglePointWithTime:
                    value.Boolean = item.GetSinglePoint();
                    value.Numeric = value.Boolean ? 1d : 0d;
                    break;

                case Iec104TypeId.DoublePoint:
                case Iec104TypeId.DoublePointWithTime:
                    var state = item.GetDoublePoint();
                    value.Numeric = (double)(byte)state;
                    value.Boolean = state == Iec104DoublePointState.On;
                    break;

                case Iec104TypeId.MeasuredFloat:
                case Iec104TypeId.MeasuredFloatWithTime:
                    float single;
                    if (item.TryGetFloat(out single)) value.Numeric = single;
                    break;

                case Iec104TypeId.MeasuredNormalized:
                case Iec104TypeId.MeasuredNormalizedWithTime:
                    value.Numeric = ReadInt16(item.Element, 0) / 32768d;
                    break;

                case Iec104TypeId.MeasuredScaled:
                case Iec104TypeId.MeasuredScaledWithTime:
                    value.Numeric = ReadInt16(item.Element, 0);
                    break;

                case Iec104TypeId.StepPosition:
                case Iec104TypeId.StepPositionWithTime:
                    value.Numeric = item.Element[0] & 0x7F;
                    break;
            }

            Cp56Time2a time;
            if (item.TryGetTime(out time) && !time.Invalid && time.Value != DateTime.MinValue)
            {
                value.HasTimestamp = true;
                value.Timestamp = time.Value;
            }

            return value;
        }

        private static short ReadInt16(byte[] buffer, int offset)
        {
            return (short)(buffer[offset] | (buffer[offset + 1] << 8));
        }
    }
}

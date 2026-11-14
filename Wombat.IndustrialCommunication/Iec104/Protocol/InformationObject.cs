using System;

namespace Wombat.IndustrialCommunication.Iec104.Protocol
{
    /// <summary>
    /// 信息对象：3 字节地址 + 与类型相关的元素。
    /// </summary>
    public sealed class Iec104InformationObject
    {
        public Iec104InformationObject(Iec104TypeId typeId, int ioa, byte[] element)
        {
            if (Iec104TypeInfo.HasInformationObjectAddress(typeId) &&
                (ioa <= 0 || ioa > Iec104Constants.MaxInformationObjectAddress))
                throw new ArgumentOutOfRangeException(nameof(ioa), "信息对象地址必须在 1..16777215 之间。");
            if (element == null) throw new ArgumentNullException(nameof(element));

            if (Iec104TypeInfo.TryGetElementSize(typeId, out var expected) && element.Length != expected)
                throw new ArgumentException($"类型 {typeId} 的元素长度应为 {expected}，实际为 {element.Length}。", nameof(element));

            TypeId = typeId;
            Ioa = ioa;
            Element = element;
        }

        public Iec104TypeId TypeId { get; }

        public int Ioa { get; }

        public byte[] Element { get; }

        /// <summary>构造单点遥信对象。</summary>
        public static Iec104InformationObject SinglePoint(int ioa, bool on, Iec104QualityFlags quality = Iec104QualityFlags.None)
        {
            var element = new byte[1];
            element[0] = (byte)((on ? 0x01 : 0x00) | ((byte)quality & 0xF0));
            return new Iec104InformationObject(Iec104TypeId.SinglePoint, ioa, element);
        }

        /// <summary>构造带时标单点遥信对象（SOE）。</summary>
        public static Iec104InformationObject SinglePointWithTime(int ioa, bool on, DateTime time, Iec104QualityFlags quality = Iec104QualityFlags.None)
        {
            var element = new byte[8];
            element[0] = (byte)((on ? 0x01 : 0x00) | ((byte)quality & 0xF0));
            new Cp56Time2a(time).WriteTo(element, 1);
            return new Iec104InformationObject(Iec104TypeId.SinglePointWithTime, ioa, element);
        }

        /// <summary>构造双点遥信对象。</summary>
        public static Iec104InformationObject DoublePoint(int ioa, Iec104DoublePointState state, Iec104QualityFlags quality = Iec104QualityFlags.None)
        {
            var element = new byte[1];
            element[0] = (byte)(((byte)state & 0x03) | ((byte)quality & 0xF0));
            return new Iec104InformationObject(Iec104TypeId.DoublePoint, ioa, element);
        }

        /// <summary>构造短浮点遥测对象。</summary>
        public static Iec104InformationObject MeasuredFloat(int ioa, float value, Iec104QualityFlags quality = Iec104QualityFlags.None)
        {
            var element = new byte[5];
            ByteOrder.WriteSingle(element, 0, value);
            element[4] = (byte)quality;
            return new Iec104InformationObject(Iec104TypeId.MeasuredFloat, ioa, element);
        }

        /// <summary>构造带时标短浮点遥测对象。</summary>
        public static Iec104InformationObject MeasuredFloatWithTime(int ioa, float value, DateTime time, Iec104QualityFlags quality = Iec104QualityFlags.None)
        {
            var element = new byte[12];
            ByteOrder.WriteSingle(element, 0, value);
            element[4] = (byte)quality;
            new Cp56Time2a(time).WriteTo(element, 5);
            return new Iec104InformationObject(Iec104TypeId.MeasuredFloatWithTime, ioa, element);
        }

        /// <summary>构造单点命令对象。select 为 true 时表示选择。</summary>
        public static Iec104InformationObject SingleCommand(int ioa, bool on, byte qualifier = 0, bool select = false)
        {
            var element = new byte[1];
            element[0] = (byte)((on ? 0x01 : 0x00) | ((qualifier & 0x1F) << 2) | (select ? 0x80 : 0x00));
            return new Iec104InformationObject(Iec104TypeId.SingleCommand, ioa, element);
        }

        /// <summary>构造双点命令对象。</summary>
        public static Iec104InformationObject DoubleCommand(int ioa, Iec104DoublePointState state, byte qualifier = 0, bool select = false)
        {
            var element = new byte[1];
            element[0] = (byte)(((byte)state & 0x03) | ((qualifier & 0x1F) << 2) | (select ? 0x80 : 0x00));
            return new Iec104InformationObject(Iec104TypeId.DoubleCommand, ioa, element);
        }

        /// <summary>构造总召唤命令对象。</summary>
        public static Iec104InformationObject InterrogationCommand(byte qualifier = Iec104Constants.QualifierStationInterrogation)
        {
            return new Iec104InformationObject(Iec104TypeId.InterrogationCommand, 0, new[] { qualifier });
        }

        /// <summary>构造总召唤确认/终止对象（限定词固定为站召唤）。</summary>
        public static Iec104InformationObject InterrogationResponse(byte qualifier = Iec104Constants.QualifierStationInterrogation)
        {
            return new Iec104InformationObject(Iec104TypeId.InterrogationCommand, 0, new[] { qualifier });
        }

        /// <summary>构造时钟同步对象。</summary>
        public static Iec104InformationObject ClockSynchronization(DateTime time)
        {
            var element = new byte[7];
            new Cp56Time2a(time).WriteTo(element, 0);
            return new Iec104InformationObject(Iec104TypeId.ClockSynchronizationCommand, 0, element);
        }

        /// <summary>读取单点状态。</summary>
        public bool GetSinglePoint()
        {
            return (Element[0] & 0x01) != 0;
        }

        /// <summary>读取双点状态。</summary>
        public Iec104DoublePointState GetDoublePoint()
        {
            return (Iec104DoublePointState)(Element[0] & 0x03);
        }

        /// <summary>读取短浮点值；非浮点类型返回 false。</summary>
        public bool TryGetFloat(out float value)
        {
            if (TypeId == Iec104TypeId.MeasuredFloat || TypeId == Iec104TypeId.MeasuredFloatWithTime)
            {
                value = ByteOrder.ReadSingle(Element, 0);
                return true;
            }

            value = 0f;
            return false;
        }

        /// <summary>读取品质位。</summary>
        public Iec104QualityFlags GetQuality()
        {
            switch (TypeId)
            {
                case Iec104TypeId.SinglePoint:
                case Iec104TypeId.DoublePoint:
                case Iec104TypeId.StepPosition:
                case Iec104TypeId.SinglePointWithTime:
                case Iec104TypeId.DoublePointWithTime:
                case Iec104TypeId.StepPositionWithTime:
                    return (Iec104QualityFlags)(Element[0] & 0xF0);

                case Iec104TypeId.MeasuredNormalized:
                case Iec104TypeId.MeasuredScaled:
                case Iec104TypeId.MeasuredNormalizedWithTime:
                case Iec104TypeId.MeasuredScaledWithTime:
                    return (Iec104QualityFlags)(Element[2] & 0xF1);

                case Iec104TypeId.MeasuredFloat:
                case Iec104TypeId.MeasuredFloatWithTime:
                case Iec104TypeId.BitString32:
                case Iec104TypeId.BitString32WithTime:
                    return (Iec104QualityFlags)(Element[4] & 0xF1);

                default:
                    return Iec104QualityFlags.None;
            }
        }

        /// <summary>读取时标；非带时标类型返回 false。</summary>
        public bool TryGetTime(out Cp56Time2a time)
        {
            if (!Iec104TypeInfo.HasTimeTag(TypeId))
            {
                time = default;
                return false;
            }

            time = Cp56Time2a.ReadFrom(Element, Element.Length - 7);
            return true;
        }
    }
}

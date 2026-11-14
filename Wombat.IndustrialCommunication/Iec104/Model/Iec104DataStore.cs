using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Wombat.IndustrialCommunication.Iec104.Protocol;

namespace Wombat.IndustrialCommunication.Iec104.Model
{
    /// <summary>
    /// 从站数据源：每个点位保存当前值、品质与时标。
    /// 变化是否对外通知由死区决定，单点按状态变化判定。
    /// </summary>
    public sealed class Iec104DataStore
    {
        private readonly ConcurrentDictionary<int, Iec104Value> _values =
            new ConcurrentDictionary<int, Iec104Value>();

        private readonly Iec104PointTable _table;

        public Iec104DataStore(Iec104PointTable table)
        {
            _table = table;
            if (table != null)
            {
                for (var i = 0; i < table.Points.Count; i++)
                {
                    var point = table.Points[i];
                    _values[point.Ioa] = new Iec104Value
                    {
                        Ioa = point.Ioa,
                        CommonAddress = point.CommonAddress,
                        TypeId = point.TypeId,
                        Numeric = 0d,
                        Boolean = false,
                        Quality = Iec104QualityFlags.None,
                        Timestamp = DateTime.Now,
                        ReceivedAt = DateTime.Now,
                    };
                }
            }
        }

        /// <summary>值发生变化时触发，参数为变化后的值。</summary>
        public event EventHandler<Iec104Value> ValueChanged;

        public Iec104PointTable Table
        {
            get { return _table; }
        }

        public bool TryGet(int ioa, out Iec104Value value)
        {
            return _values.TryGetValue(ioa, out value);
        }

        public IReadOnlyDictionary<int, Iec104Value> Snapshot()
        {
            return new Dictionary<int, Iec104Value>(_values);
        }

        /// <summary>写入遥测值。超出死区才触发变化事件。</summary>
        public bool SetNumeric(int ioa, double value, DateTime? timestamp = null)
        {
            Iec104Value existing;
            if (!_values.TryGetValue(ioa, out existing)) return false;

            var point = FindPoint(ioa);
            if (point != null && point.Deadband > 0d)
            {
                var delta = Math.Abs(value - existing.Numeric);
                if (delta < point.Deadband) return false;
            }

            var next = Copy(existing);
            next.Numeric = value;
            next.Timestamp = timestamp ?? DateTime.Now;
            next.ReceivedAt = DateTime.Now;
            next.HasTimestamp = point != null && point.IsSoe;
            _values[ioa] = next;

            Raise(next);
            return true;
        }

        /// <summary>写入单点状态。状态变化才触发事件。</summary>
        public bool SetBoolean(int ioa, bool state, DateTime? timestamp = null)
        {
            Iec104Value existing;
            if (!_values.TryGetValue(ioa, out existing)) return false;
            if (existing.Boolean == state) return false;

            var point = FindPoint(ioa);
            var next = Copy(existing);
            next.Boolean = state;
            next.Numeric = state ? 1d : 0d;
            next.Timestamp = timestamp ?? DateTime.Now;
            next.ReceivedAt = DateTime.Now;
            next.HasTimestamp = point != null && point.IsSoe;
            _values[ioa] = next;

            Raise(next);
            return true;
        }

        /// <summary>设置品质位，不触发变化事件（品质随下次上送带走）。</summary>
        public bool SetQuality(int ioa, Iec104QualityFlags quality)
        {
            Iec104Value existing;
            if (!_values.TryGetValue(ioa, out existing)) return false;

            var next = Copy(existing);
            next.Quality = quality;
            _values[ioa] = next;
            return true;
        }

        /// <summary>按 IOA 的当前值构造信息对象。</summary>
        public Iec104InformationObject BuildObject(Iec104Point point, Iec104Value value)
        {
            if (point == null) return null;

            switch (point.TypeId)
            {
                case Iec104TypeId.SinglePoint:
                    return Iec104InformationObject.SinglePoint(point.Ioa, value.Boolean, value.Quality);

                case Iec104TypeId.SinglePointWithTime:
                    return Iec104InformationObject.SinglePointWithTime(
                        point.Ioa, value.Boolean, value.Timestamp, value.Quality);

                case Iec104TypeId.DoublePoint:
                    return Iec104InformationObject.DoublePoint(
                        point.Ioa, ToDoublePointState(value), value.Quality);

                case Iec104TypeId.DoublePointWithTime:
                    return new Iec104InformationObject(
                        Iec104TypeId.DoublePointWithTime,
                        point.Ioa,
                        BuildDoublePointWithTime(point.Ioa, value));

                case Iec104TypeId.MeasuredFloat:
                    return Iec104InformationObject.MeasuredFloat(
                        point.Ioa, (float)value.Numeric, value.Quality);

                case Iec104TypeId.MeasuredFloatWithTime:
                    return Iec104InformationObject.MeasuredFloatWithTime(
                        point.Ioa, (float)value.Numeric, value.Timestamp, value.Quality);

                default:
                    return null;
            }
        }

        private static byte[] BuildDoublePointWithTime(int ioa, Iec104Value value)
        {
            var element = new byte[8];
            element[0] = (byte)(((byte)ToDoublePointState(value) & 0x03) | ((byte)value.Quality & 0xF0));
            new Cp56Time2a(value.Timestamp).WriteTo(element, 1);
            return element;
        }

        private static Iec104DoublePointState ToDoublePointState(Iec104Value value)
        {
            var raw = (byte)value.Numeric;
            if (raw > 3) raw = value.Boolean ? (byte)2 : (byte)1;
            return (Iec104DoublePointState)raw;
        }

        private Iec104Point FindPoint(int ioa)
        {
            Iec104Point point;
            return _table != null && _table.TryGetPoint(ioa, out point) ? point : null;
        }

        private static Iec104Value Copy(Iec104Value source)
        {
            return new Iec104Value
            {
                Ioa = source.Ioa,
                CommonAddress = source.CommonAddress,
                TypeId = source.TypeId,
                Numeric = source.Numeric,
                Boolean = source.Boolean,
                Quality = source.Quality,
                HasTimestamp = source.HasTimestamp,
                Timestamp = source.Timestamp,
                ReceivedAt = source.ReceivedAt,
            };
        }

        private void Raise(Iec104Value value)
        {
            var handler = ValueChanged;
            if (handler == null) return;

            try { handler(this, value); } catch { }
        }
    }
}

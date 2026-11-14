using System;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Protocol
{
    /// <summary>
    /// CP56Time2a 七字节时标。协议中的时间按本地时间理解，不带时区信息。
    /// </summary>
    public readonly struct Cp56Time2a
    {
        private const int Length = 7;

        public Cp56Time2a(DateTime value, bool invalid = false, bool summerTime = false)
        {
            Value = value;
            Invalid = invalid;
            SummerTime = summerTime;
        }

        /// <summary>时标值。非法或超出范围时为 <see cref="DateTime.MinValue"/>。</summary>
        public DateTime Value { get; }

        /// <summary>IV 位：时标无效。</summary>
        public bool Invalid { get; }

        /// <summary>SU 位：夏令时。</summary>
        public bool SummerTime { get; }

        /// <summary>用当前时间构造。</summary>
        public static Cp56Time2a FromDateTime(DateTime value)
        {
            return new Cp56Time2a(DateTime.SpecifyKind(value, DateTimeKind.Unspecified));
        }

        /// <summary>写入七字节。</summary>
        public void WriteTo(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset + Length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            var value = Value == DateTime.MinValue ? DateTime.Now : Value;
            var milliseconds = value.Second * 1000 + value.Millisecond;
            if (milliseconds > 59999) milliseconds = 59999;

            buffer[offset] = (byte)(milliseconds & 0xFF);
            buffer[offset + 1] = (byte)((milliseconds >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value.Minute & 0x3F) | (Invalid ? 0x80 : 0x00));
            buffer[offset + 3] = (byte)((value.Hour & 0x1F) | (SummerTime ? 0x80 : 0x00));

            var dayOfWeek = (int)value.DayOfWeek;
            if (dayOfWeek == 0) dayOfWeek = 7;
            buffer[offset + 4] = (byte)((value.Day & 0x1F) | ((dayOfWeek & 0x07) << 5));
            buffer[offset + 5] = (byte)(value.Month & 0x0F);
            buffer[offset + 6] = (byte)(value.Year % 100);
        }

        /// <summary>从七字节读取。字段越界时返回 <see cref="Invalid"/> 为 true 的空值，不抛异常。</summary>
        public static Cp56Time2a ReadFrom(byte[] buffer, int offset)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset + Length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            var milliseconds = buffer[offset] | (buffer[offset + 1] << 8);
            var minuteByte = buffer[offset + 2];
            var hourByte = buffer[offset + 3];
            var dayByte = buffer[offset + 4];
            var monthByte = buffer[offset + 5];
            var yearByte = buffer[offset + 6];

            var minute = minuteByte & 0x3F;
            var hour = hourByte & 0x1F;
            var day = dayByte & 0x1F;
            var month = monthByte & 0x0F;
            var year = 2000 + (yearByte & 0x7F);
            var invalid = (minuteByte & 0x80) != 0;
            var summerTime = (hourByte & 0x80) != 0;

            if (milliseconds > 59999 || minute > 59 || hour > 23 || day < 1 || day > 31 || month < 1 || month > 12)
            {
                return new Cp56Time2a(DateTime.MinValue, true, summerTime);
            }

            try
            {
                var value = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified)
                    .AddMilliseconds(milliseconds);
                return new Cp56Time2a(value, invalid, summerTime);
            }
            catch (ArgumentOutOfRangeException)
            {
                return new Cp56Time2a(DateTime.MinValue, true, summerTime);
            }
        }
    }
}

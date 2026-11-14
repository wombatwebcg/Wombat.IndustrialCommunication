using System;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Xunit;

namespace Wombat.IndustrialCommunication.Iec104.Tests
{
    public class Cp56Time2aTests
    {
        [Fact]
        public void RoundTrips_ToMillisecondPrecision()
        {
            var time = new DateTime(2026, 11, 13, 10, 20, 30, 123);
            var buffer = new byte[7];
            new Cp56Time2a(time).WriteTo(buffer, 0);

            var parsed = Cp56Time2a.ReadFrom(buffer, 0);
            Assert.False(parsed.Invalid);
            Assert.Equal(time, parsed.Value);
            Assert.Equal(123, parsed.Value.Millisecond);
        }

        [Fact]
        public void WritesMillisecondsSinceMinuteStart()
        {
            var time = new DateTime(2026, 1, 2, 3, 4, 59, 999);
            var buffer = new byte[7];
            new Cp56Time2a(time).WriteTo(buffer, 0);

            var milliseconds = buffer[0] | (buffer[1] << 8);
            Assert.Equal(59999, milliseconds);
            Assert.Equal(4, buffer[2] & 0x3F);
            Assert.Equal(3, buffer[3] & 0x1F);
            Assert.Equal(2, buffer[4] & 0x1F);
            Assert.Equal(1, buffer[5] & 0x0F);
            Assert.Equal(26, buffer[6] & 0x7F);
        }

        [Fact]
        public void InvalidBit_IsPreserved()
        {
            var time = new DateTime(2026, 5, 6, 7, 8, 9);
            var buffer = new byte[7];
            new Cp56Time2a(time, invalid: true).WriteTo(buffer, 0);

            var parsed = Cp56Time2a.ReadFrom(buffer, 0);
            Assert.True(parsed.Invalid);
            Assert.Equal(time, parsed.Value);
        }

        [Fact]
        public void OutOfRangeFields_YieldInvalidValueWithoutThrowing()
        {
            var buffer = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0F, 0x1A };
            var parsed = Cp56Time2a.ReadFrom(buffer, 0);

            Assert.True(parsed.Invalid);
            Assert.Equal(DateTime.MinValue, parsed.Value);
        }
    }
}

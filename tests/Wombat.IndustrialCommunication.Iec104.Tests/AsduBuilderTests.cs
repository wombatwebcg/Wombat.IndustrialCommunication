using System.Collections.Generic;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Xunit;

namespace Wombat.IndustrialCommunication.Iec104.Tests
{
    public class AsduBuilderTests
    {
        [Theory]
        [InlineData(Iec104TypeId.SinglePoint, 60)]
        [InlineData(Iec104TypeId.MeasuredFloat, 30)]
        [InlineData(Iec104TypeId.SinglePointWithTime, 22)]
        [InlineData(Iec104TypeId.MeasuredFloatWithTime, 16)]
        public void Capacity_MatchesProtocolLimit(Iec104TypeId typeId, int expected)
        {
            Assert.Equal(expected, AsduBuilder.GetCapacity(typeId));
        }

        [Fact]
        public void EightHundredEighteenFloats_SplitIntoTwentyEightFrames()
        {
            var objects = new List<Iec104InformationObject>();
            for (var i = 1; i <= 818; i++)
            {
                objects.Add(Iec104InformationObject.MeasuredFloat(i, i * 0.1f));
            }

            var frames = AsduBuilder.BuildFrames(Iec104TypeId.MeasuredFloat, 1, Iec104Cause.InterrogatedByStation, objects);
            Assert.Equal(28, frames.Count);

            var total = 0;
            foreach (var frame in frames)
            {
                Assert.True(frame.Length + Iec104Constants.ApciLength <= Iec104Constants.MaxApduLength);
                total += frame[1] & 0x7F;
            }

            Assert.Equal(818, total);
        }

        [Fact]
        public void TwoHundredNinetyEightSinglePoints_SplitIntoFiveFrames()
        {
            var objects = new List<Iec104InformationObject>();
            for (var i = 1; i <= 298; i++)
            {
                objects.Add(Iec104InformationObject.SinglePoint(i, i % 2 == 0));
            }

            var frames = AsduBuilder.BuildFrames(Iec104TypeId.SinglePoint, 1, Iec104Cause.InterrogatedByStation, objects);
            Assert.Equal(5, frames.Count);
        }

        [Fact]
        public void TwoHundredNinetyEightSoePoints_SplitIntoFourteenFrames()
        {
            var objects = new List<Iec104InformationObject>();
            for (var i = 1; i <= 298; i++)
            {
                objects.Add(Iec104InformationObject.SinglePointWithTime(i, true, System.DateTime.Now));
            }

            var frames = AsduBuilder.BuildFrames(Iec104TypeId.SinglePointWithTime, 1, Iec104Cause.InterrogatedByStation, objects);
            Assert.Equal(14, frames.Count);
        }

        [Fact]
        public void RejectsObjectTypeMismatch()
        {
            var objects = new[] { Iec104InformationObject.SinglePoint(1, true) };
            Assert.Throws<System.ArgumentException>(
                () => AsduBuilder.Build(Iec104TypeId.MeasuredFloat, 1, Iec104Cause.Spontaneous, objects));
        }
    }
}

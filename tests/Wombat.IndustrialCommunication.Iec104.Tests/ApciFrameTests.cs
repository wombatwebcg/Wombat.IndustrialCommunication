using System;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Xunit;

namespace Wombat.IndustrialCommunication.Iec104.Tests
{
    public class ApciFrameTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 2)]
        [InlineData(32767, 32766)]
        public void InformationFrame_RoundTrips(ushort send, ushort receive)
        {
            var asdu = new byte[] { 0x64, 0x01, 0x06, 0x00, 0x01, 0x00 };
            var frame = ApciFrame.CreateInformation(send, receive, asdu);
            var bytes = frame.ToBytes();

            Assert.Equal(0x68, bytes[0]);
            Assert.Equal(bytes.Length - 2, bytes[1]);

            ApciFrame parsed;
            string error;
            Assert.True(ApciFrame.TryParse(bytes, 0, bytes.Length, out parsed, out error), error);
            Assert.Equal(ApciFrameType.Information, parsed.FrameType);
            Assert.Equal(send, parsed.SendSequence);
            Assert.Equal(receive, parsed.ReceiveSequence);
            Assert.Equal(asdu, parsed.Asdu);
        }

        [Fact]
        public void SupervisoryFrame_UsesTwoByteReceiveSequence()
        {
            var bytes = ApciFrame.CreateSupervisory(1234).ToBytes();
            Assert.Equal(new byte[] { 0x68, 0x04, 0x01, 0x00, 0xA4, 0x09 }, bytes);

            ApciFrame parsed;
            string error;
            Assert.True(ApciFrame.TryParse(bytes, 0, bytes.Length, out parsed, out error), error);
            Assert.Equal(ApciFrameType.Supervisory, parsed.FrameType);
            Assert.Equal(1234, parsed.ReceiveSequence);
        }

        [Theory]
        [InlineData(ApciFunction.StartDataTransferActivation, 0x07)]
        [InlineData(ApciFunction.StartDataTransferConfirmation, 0x0B)]
        [InlineData(ApciFunction.StopDataTransferActivation, 0x13)]
        [InlineData(ApciFunction.StopDataTransferConfirmation, 0x23)]
        [InlineData(ApciFunction.TestFrameActivation, 0x43)]
        [InlineData(ApciFunction.TestFrameConfirmation, 0x83)]
        public void UnnumberedFrame_UsesExpectedControlByte(ApciFunction function, byte expected)
        {
            var bytes = ApciFrame.CreateUnnumbered(function).ToBytes();
            Assert.Equal(new byte[] { 0x68, 0x04, expected, 0x00, 0x00, 0x00 }, bytes);

            ApciFrame parsed;
            string error;
            Assert.True(ApciFrame.TryParse(bytes, 0, bytes.Length, out parsed, out error), error);
            Assert.Equal(ApciFrameType.Unnumbered, parsed.FrameType);
            Assert.Equal(function, parsed.Function);
        }

        [Fact]
        public void Parse_RejectsWrongStartByte()
        {
            var bytes = new byte[] { 0x69, 0x04, 0x01, 0x00, 0x00, 0x00 };
            ApciFrame parsed;
            string error;
            Assert.False(ApciFrame.TryParse(bytes, 0, bytes.Length, out parsed, out error));
            Assert.NotNull(error);
        }

        [Fact]
        public void CreateInformation_RejectsAsduOverLimit()
        {
            var tooLarge = new byte[Iec104Constants.MaxAsduLength + 1];
            var ex = Assert.Throws<Iec104ProtocolException>(() => ApciFrame.CreateInformation(0, 0, tooLarge));
            Assert.Equal(Iec104ErrorKind.CapacityExceeded, ex.Kind);
        }
    }
}

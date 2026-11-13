using System;
using System.Linq;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.PLCTests
{
    /// <summary>
    /// 三菱 MC 帧编解码黄金帧测试：1E / 3E / 4E 逐字节比对。
    /// </summary>
    public class McFrameCodecTests
    {
        private static readonly McProfile A1EProfile = new McProfile(McFrameType.A1E, McSeries.A);
        private static readonly McProfile Q3EProfile = new McProfile(McFrameType.Q3E, McSeries.Q);
        private static readonly McProfile Q4EProfile = new McProfile(McFrameType.Q4E, McSeries.Q);
        private static readonly McProfile IqR3EProfile = new McProfile(McFrameType.Q3E, McSeries.IqR);

        private static string Hex(byte[] data)
        {
            return string.Join(" ", data.Select(b => b.ToString("X2")));
        }

        [Fact]
        public void A1E_ReadD100_OnePoint_ShouldMatchGoldenFrame()
        {
            var address = McAddress.Parse("D100", A1EProfile);

            var frame = McFrameCodec.BuildReadRequest(A1EProfile, address, 1);

            Assert.Equal(12, frame.Length);
            Assert.Equal("01 FF 0A 00 64 00 00 00 20 44 01 00", Hex(frame));
        }

        [Fact]
        public void A1E_Read256Points_ShouldEncodeZeroPointsAndFixedZero()
        {
            var address = McAddress.Parse("M100", A1EProfile);

            var frame = McFrameCodec.BuildReadRequest(A1EProfile, address, 256);

            Assert.Equal(12, frame.Length);
            Assert.Equal(0x00, frame[0]);
            Assert.Equal(0x00, frame[10]);
            Assert.Equal(0x00, frame[11]);
        }

        [Fact]
        public void A1E_ReadSM1200_ShouldUseMAliasCode()
        {
            var address = McAddress.Parse("SM1200", A1EProfile);

            var frame = McFrameCodec.BuildReadRequest(A1EProfile, address, 1);

            Assert.Equal("00 FF 0A 00 F0 23 00 00 20 4D 01 00", Hex(frame));
        }

        [Fact]
        public void A1E_WriteM100_ThreeBits_ShouldPackNibbles()
        {
            var address = McAddress.Parse("M100", A1EProfile);
            byte[] data = McCommonMethods.PackBitData(new[] { true, false, true });

            var frame = McFrameCodec.BuildWriteRequest(A1EProfile, address, 3, data);

            Assert.Equal(14, frame.Length);
            Assert.Equal(0x02, frame[0]);
            Assert.Equal(0x03, frame[10]);
            Assert.Equal(new byte[] { 0x10, 0x10 }, new[] { frame[12], frame[13] });
        }

        [Fact]
        public void Q3E_ReadD100_TwentyWords_ShouldMatchGoldenFrame()
        {
            var address = McAddress.Parse("D100", Q3EProfile);

            var frame = McFrameCodec.BuildReadRequest(Q3EProfile, address, 20);

            Assert.Equal(21, frame.Length);
            Assert.Equal("50 00 00 FF FF 03 00 0C 00 0A 00 01 04 00 00 64 00 00 A8 14 00", Hex(frame));
            Assert.Equal((ushort)0x000C, McCommonMethods.ReadUInt16(frame, 7));
        }

        [Fact]
        public void IqR_ReadLTN0_TwoElements_ShouldUseTwoByteCodeAndFourByteAddress()
        {
            var address = McAddress.Parse("LTN0", IqR3EProfile);
            int points = address.GetPointCount(2, false);

            var frame = McFrameCodec.BuildReadRequest(IqR3EProfile, address, points);

            Assert.Equal(8, points);
            Assert.Equal(23, frame.Length);
            Assert.Equal(
                "50 00 00 FF FF 03 00 0E 00 0A 00 01 04 02 00 00 00 00 00 52 00 08 00",
                Hex(frame));
        }

        [Fact]
        public void Q3E_WriteM100_ThreePoints_ShouldEncodePointCountAndPadding()
        {
            var address = McAddress.Parse("M100", Q3EProfile);
            byte[] data = McCommonMethods.PackBitData(new[] { true, false, true });

            var frame = McFrameCodec.BuildWriteRequest(Q3EProfile, address, 3, data);

            Assert.Equal(23, frame.Length);
            Assert.Equal(0x14, frame[12]);
            Assert.Equal((ushort)0x0001, McCommonMethods.ReadUInt16(frame, 13));
            Assert.Equal((ushort)3, McCommonMethods.ReadUInt16(frame, 19));
            Assert.Equal(0x10, frame[21]);
            Assert.Equal(0x10, frame[22]);
            Assert.Equal(0x00, frame[22] & 0x0F);
        }

        [Fact]
        public void Q4E_ReadD100_SerialNumberAndReserved_ShouldMatchGoldenFrame()
        {
            var address = McAddress.Parse("D100", Q4EProfile);

            var frame = McFrameCodec.BuildReadRequest(Q4EProfile, address, 1, 0x1234);

            Assert.Equal(25, frame.Length);
            Assert.Equal(0x54, frame[0]);
            Assert.Equal(0x00, frame[1]);
            Assert.Equal(0x34, frame[2]);
            Assert.Equal(0x12, frame[3]);
            Assert.Equal(0x00, frame[4]);
            Assert.Equal(0x00, frame[5]);
            Assert.Equal((ushort)0x000C, McCommonMethods.ReadUInt16(frame, 11));
            Assert.Equal(
                "54 00 34 12 00 00 00 FF FF 03 00 0C 00 0A 00 01 04 00 00 64 00 00 A8 01 00",
                Hex(frame));
        }

        [Fact]
        public void Q3E_ErrorResponse_ShouldExposeOriginalCode()
        {
            byte[] response = { 0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x51, 0xC0 };

            var packet = McFrameCodec.ParseResponse(McFrameType.Q3E, response);

            Assert.False(packet.IsSuccess);
            Assert.Equal(McCommonMethods.EndCodePointCountOutOfRange, packet.EndCode);
            Assert.Equal(0, packet.Data.Length);
            Assert.Contains("C051", packet.ErrorMessage);
            Assert.Contains("点数", packet.ErrorMessage);
        }

        [Fact]
        public void Q3E_SuccessResponse_ShouldExposeData()
        {
            byte[] response = { 0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x06, 0x00, 0x00, 0x00, 0x34, 0x12, 0x78, 0x56 };

            var packet = McFrameCodec.ParseResponse(McFrameType.Q3E, response);

            Assert.True(packet.IsSuccess);
            Assert.Equal(new byte[] { 0x34, 0x12, 0x78, 0x56 }, packet.Data);
            Assert.Equal(0xFF, packet.PcNumber);
            Assert.Equal((ushort)0x03FF, packet.ModuleIoNumber);
        }

        [Fact]
        public void Q3E_Response_LengthFieldMismatch_ShouldThrow()
        {
            byte[] response = { 0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x08, 0x00, 0x00, 0x00, 0x34, 0x12 };

            Assert.Throws<ArgumentException>(() => McFrameCodec.ParseResponse(McFrameType.Q3E, response));
        }

        [Fact]
        public void Q3E_Response_InvalidSubHeader_ShouldThrow()
        {
            byte[] response = { 0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x00, 0x00 };

            Assert.Throws<ArgumentException>(() => McFrameCodec.ParseResponse(McFrameType.Q3E, response));
        }

        [Fact]
        public void A1E_ErrorResponse_ShouldCarryAbnormalCode()
        {
            byte[] response = { 0x80, 0x5B, 0x10 };

            var packet = McFrameCodec.ParseResponse(McFrameType.A1E, response);

            Assert.False(packet.IsSuccess);
            Assert.Equal(0x5B, packet.EndCode);
            Assert.Equal(0x10, packet.AbnormalCode);
            Assert.Equal(0, packet.Data.Length);
            Assert.Contains("5B", packet.ErrorMessage);
            Assert.Contains("10", packet.ErrorMessage);
        }

        [Fact]
        public void A1E_ErrorResponse_WithoutAbnormalCode_ShouldNotThrow()
        {
            byte[] response = { 0x83, 0x5B };

            var packet = McFrameCodec.ParseResponse(McFrameType.A1E, response);

            Assert.False(packet.IsSuccess);
            Assert.Equal(0x5B, packet.EndCode);
            Assert.Equal(0, packet.AbnormalCode);
        }

        [Fact]
        public void A1E_SuccessResponse_ShouldExposeBitData()
        {
            byte[] response = { 0x80, 0x00 };

            var packet = McFrameCodec.ParseResponse(McFrameType.A1E, response);

            Assert.True(packet.IsSuccess);
            Assert.Equal(0, packet.Data.Length);
        }

        [Fact]
        public void Q4E_Response_SerialMismatch_ShouldThrow()
        {
            byte[] response =
            {
                0xD4, 0x00, 0x34, 0x12, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12
            };

            Assert.Throws<InvalidOperationException>(
                () => McFrameCodec.ParseResponse(McFrameType.Q4E, response, 0x9999));
        }

        [Fact]
        public void Q4E_Response_ShouldEchoSerialAndData()
        {
            byte[] response =
            {
                0xD4, 0x00, 0x34, 0x12, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x04, 0x00, 0x00, 0x00, 0x34, 0x12
            };

            var packet = McFrameCodec.ParseResponse(McFrameType.Q4E, response, 0x1234);

            Assert.True(packet.IsSuccess);
            Assert.Equal((ushort)0x1234, packet.SerialNumber);
            Assert.Equal(new byte[] { 0x34, 0x12 }, packet.Data);
        }

        [Theory]
        [InlineData(McFrameType.A1E, 2)]
        [InlineData(McFrameType.Q3E, 11)]
        [InlineData(McFrameType.Q4E, 15)]
        public void ResponseHeaderLength_ShouldFollowFrameType(McFrameType frameType, int expected)
        {
            Assert.Equal(expected, McFrameCodec.GetResponseHeaderLength(frameType));
        }

        [Fact]
        public void BitPack_ShouldUseHighNibbleFirst()
        {
            byte[] packed = McCommonMethods.PackBitData(new[] { true, false, true, true, false });

            Assert.Equal(new byte[] { 0x10, 0x11, 0x00 }, packed);
            Assert.Equal(new[] { true, false, true, true, false }, McCommonMethods.UnpackBitData(packed, 5));
        }

        [Fact]
        public void BitPack_OddPoints_ShouldPadLowNibbleWithZero()
        {
            byte[] packed = McCommonMethods.PackBitData(new[] { true, true, true });

            Assert.Equal(new byte[] { 0x11, 0x10 }, packed);
            Assert.Equal(0x00, packed[1] & 0x0F);
        }

        [Fact]
        public void WriteDataLengthMismatch_ShouldThrow()
        {
            var address = McAddress.Parse("D100", Q3EProfile);

            Assert.Throws<ArgumentException>(
                () => McFrameCodec.BuildWriteRequest(Q3EProfile, address, 4, new byte[] { 0x00, 0x01 }));
        }

        [Fact]
        public void A1E_ReadBeyond128Words_ShouldThrow()
        {
            var address = McAddress.Parse("D100", A1EProfile);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => McFrameCodec.BuildReadRequest(A1EProfile, address, 129));
        }

        [Fact]
        public void Q3E_ReadBeyond960Words_ShouldThrow()
        {
            var address = McAddress.Parse("D0", Q3EProfile);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => McFrameCodec.BuildReadRequest(Q3EProfile, address, 961));
        }

        [Fact]
        public void UnknownEndCode_ShouldStillReportRawValue()
        {
            byte[] response = { 0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00, 0x02, 0x00, 0x01, 0x12 };

            var packet = McFrameCodec.ParseResponse(McFrameType.Q3E, response);

            Assert.False(packet.IsSuccess);
            Assert.Equal((ushort)0x1201, packet.EndCode);
            Assert.Contains("1201", packet.ErrorMessage);
        }
    }
}

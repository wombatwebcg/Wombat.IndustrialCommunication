using System;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.PLCTests
{
    /// <summary>
    /// 三菱 MC 地址解析测试：最长前缀匹配、1E 别名展开、进制、点数换算。
    /// </summary>
    public class McAddressTests
    {
        private static readonly McProfile A1EProfile = new McProfile(McFrameType.A1E, McSeries.A);
        private static readonly McProfile Q3EProfile = new McProfile(McFrameType.Q3E, McSeries.Q);
        private static readonly McProfile IqRProfile = new McProfile(McFrameType.Q3E, McSeries.IqR);
        private static readonly McProfile FxProfile = new McProfile(McFrameType.Q3E, McSeries.Fx);

        [Fact]
        public void A1E_D100_ShouldParseDeviceAndNumber()
        {
            var address = McAddress.Parse("d100", A1EProfile);

            Assert.Equal(McDeviceKind.D, address.DeviceKind);
            Assert.Equal(100, address.Address);
            Assert.False(address.IsBitDevice);
            Assert.False(address.IsAliased);
            Assert.Equal(1, address.WordsPerPoint);
        }

        [Theory]
        [InlineData("SD100", McDeviceKind.SD, 100)]
        [InlineData("SW100", McDeviceKind.SW, 256)]
        [InlineData("SB100", McDeviceKind.SB, 256)]
        [InlineData("SN100", McDeviceKind.SN, 100)]
        [InlineData("SC100", McDeviceKind.SC, 100)]
        [InlineData("SS100", McDeviceKind.SS, 100)]
        [InlineData("S100", McDeviceKind.S, 100)]
        [InlineData("ZR100", McDeviceKind.ZR, 256)]
        [InlineData("Z100", McDeviceKind.Z, 100)]
        [InlineData("DX100", McDeviceKind.DX, 256)]
        [InlineData("DY100", McDeviceKind.DY, 256)]
        [InlineData("R100", McDeviceKind.R, 100)]
        public void Q3E_LongestPrefixMatch(string text, McDeviceKind expected, int expectedNumber)
        {
            var address = McAddress.Parse(text, Q3EProfile);

            Assert.Equal(expected, address.DeviceKind);
            Assert.Equal(expectedNumber, address.Address);
        }

        [Theory]
        [InlineData("LSTN100", McDeviceKind.LSTN)]
        [InlineData("LSTS100", McDeviceKind.LSTS)]
        [InlineData("LSTC100", McDeviceKind.LSTC)]
        [InlineData("LTN100", McDeviceKind.LTN)]
        [InlineData("LTS100", McDeviceKind.LTS)]
        [InlineData("LTC100", McDeviceKind.LTC)]
        [InlineData("LCN100", McDeviceKind.LCN)]
        [InlineData("LCS100", McDeviceKind.LCS)]
        [InlineData("LCC100", McDeviceKind.LCC)]
        [InlineData("L100", McDeviceKind.L)]
        [InlineData("LZ100", McDeviceKind.LZ)]
        public void IqR_LongestPrefixMatch(string text, McDeviceKind expected)
        {
            var address = McAddress.Parse(text, IqRProfile);

            Assert.Equal(expected, address.DeviceKind);
            Assert.Equal(100, address.Address);
        }

        [Fact]
        public void A1E_SM1200_ShouldExpandToM9200()
        {
            var address = McAddress.Parse("SM1200", A1EProfile);

            Assert.True(address.IsAliased);
            Assert.Equal("SM", address.Prefix);
            Assert.Equal(McDeviceKind.M, address.DeviceKind);
            Assert.Equal(9200, address.Address);
            Assert.True(address.IsBitDevice);
        }

        [Fact]
        public void A1E_SD1255_ShouldExpandToD9255()
        {
            var address = McAddress.Parse("SD1255", A1EProfile);

            Assert.True(address.IsAliased);
            Assert.Equal(McDeviceKind.D, address.DeviceKind);
            Assert.Equal(9255, address.Address);
        }

        [Fact]
        public void A1E_L100_ShouldAliasToM100()
        {
            var address = McAddress.Parse("L100", A1EProfile);

            Assert.True(address.IsAliased);
            Assert.Equal(McDeviceKind.M, address.DeviceKind);
            Assert.Equal(100, address.Address);
        }

        [Fact]
        public void A1E_SB_ShouldBeRejectedLocally()
        {
            var ex = Assert.Throws<NotSupportedException>(() => McAddress.Parse("SB100", A1EProfile));

            Assert.Contains("SB", ex.Message);
        }

        [Fact]
        public void A1E_SB_ShouldAlsoFailTryParse()
        {
            McAddress address;
            string error;

            Assert.False(McAddress.TryParse("SB100", A1EProfile, out address, out error));
            Assert.Null(address);
            Assert.Contains("SB", error);
        }

        [Theory]
        [InlineData("X10", 16)]
        [InlineData("Y17", 23)]
        [InlineData("B1A", 26)]
        public void Q3E_XyB_ShouldUseHex(string text, int expected)
        {
            Assert.Equal(expected, McAddress.Parse(text, Q3EProfile).Address);
        }

        [Theory]
        [InlineData("X10", 8)]
        [InlineData("Y17", 15)]
        public void Fx_Xy_ShouldUseOctal(string text, int expected)
        {
            Assert.Equal(expected, McAddress.Parse(text, FxProfile).Address);
        }

        [Fact]
        public void Q3E_X_WithOctalDigit_ShouldFail()
        {
            Assert.Throws<ArgumentException>(() => McAddress.Parse("X19", FxProfile));
        }

        [Fact]
        public void IqR_LTN_TwoElements_ShouldBeEightPoints()
        {
            var address = McAddress.Parse("LTN0", IqRProfile);

            Assert.Equal(4, address.WordsPerPoint);
            Assert.Equal(8, address.GetPointCount(2, false));
        }

        [Fact]
        public void IqR_LTN_BitAccess_ShouldFail()
        {
            var address = McAddress.Parse("LTN0", IqRProfile);

            Assert.Throws<ArgumentException>(() => address.GetPointCount(1, true));
        }

        [Fact]
        public void IqR_LTS_WordAccess_ShouldFail()
        {
            var address = McAddress.Parse("LTS0", IqRProfile);

            Assert.Throws<ArgumentException>(() => address.GetPointCount(1, false));
        }

        [Fact]
        public void D_WordAccess_ShouldReturnElementCount()
        {
            var address = McAddress.Parse("D100", Q3EProfile);

            Assert.Equal(10, address.GetPointCount(10, false));
            Assert.Throws<ArgumentException>(() => address.GetPointCount(10, true));
        }

        [Fact]
        public void M_BitAccess_ShouldReturnElementCount()
        {
            var address = McAddress.Parse("M100", Q3EProfile);

            Assert.Equal(10, address.GetPointCount(10, true));
        }

        [Theory]
        [InlineData("D")]
        [InlineData("")]
        [InlineData("QQ100")]
        [InlineData("D1A")]
        public void InvalidAddress_ShouldThrow(string text)
        {
            Assert.Throws<ArgumentException>(() => McAddress.Parse(text, Q3EProfile));
        }

        [Fact]
        public void A1E_WithQSeries_ShouldBeRejected()
        {
            Assert.Throws<ArgumentException>(() => new McProfile(McFrameType.A1E, McSeries.Q));
        }

        [Fact]
        public void A_Series_With3E_ShouldBeRejected()
        {
            Assert.Throws<ArgumentException>(() => new McProfile(McFrameType.Q3E, McSeries.A));
        }

        [Fact]
        public void Q3E_X_ShouldEncodeOneByteCode()
        {
            Assert.Equal(new byte[] { 0x9C }, Q3EProfile.GetDeviceCode(McAddress.Parse("X10", Q3EProfile).Device));
        }

        [Fact]
        public void IqR_LTN_ShouldEncodeTwoByteCode()
        {
            var address = McAddress.Parse("LTN0", IqRProfile);

            Assert.Equal(new byte[] { 0x52, 0x00 }, IqRProfile.GetDeviceCode(address.Device));
            Assert.Equal((ushort)0x0002, IqRProfile.GetSubCommand(false));
            Assert.Equal(2, IqRProfile.DeviceCodeWidth);
            Assert.Equal(4, IqRProfile.AddressWidth);
        }
    }
}

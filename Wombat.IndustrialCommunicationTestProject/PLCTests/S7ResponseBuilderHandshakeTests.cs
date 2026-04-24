using System;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    public class S7ResponseBuilderHandshakeTests
    {
        [Fact]
        public void CreateConnectionResponse_ShouldReturnStandardCotpConnectionConfirm()
        {
            var request = (byte[])SiemensConstant.Command1.Clone();
            request[21] = 0x01;

            var response = S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion.S7_1200, 0, 1);

            Assert.NotNull(response);
            Assert.Equal(
                new byte[]
                {
                    0x03, 0x00, 0x00, 0x16,
                    0x11, 0xD0, 0x00, 0x01, 0x00, 0x00, 0x00,
                    0xC0, 0x01, 0x0A,
                    0xC1, 0x02, 0x01, 0x02,
                    0xC2, 0x02, 0x01, 0x01
                },
                response);
        }

        [Fact]
        public void CreateConnectionResponse_ShouldReturnStandardSetupCommunicationAckData()
        {
            var request = (byte[])SiemensConstant.Command2.Clone();

            var response = S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion.S7_1200, 0, 1);

            Assert.NotNull(response);
            Assert.Equal(
                new byte[]
                {
                    0x03, 0x00, 0x00, 0x1B,
                    0x02, 0xF0, 0x80,
                    0x32, 0x03, 0x00, 0x00, 0x04, 0x00,
                    0x00, 0x08, 0x00, 0x00, 0x00, 0x00,
                    0xF0, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0xE0
                },
                response);
        }

        [Fact]
        public void CreateConnectionResponse_ShouldCapSetupCommunicationNegotiatedPduLength()
        {
            var request = (byte[])SiemensConstant.Command2.Clone();
            request[23] = 0x08;
            request[24] = 0x00;

            var response = S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion.S7_1200, 0, 1);

            Assert.NotNull(response);
            Assert.Equal((byte)0x01, response[25]);
            Assert.Equal((byte)0xE0, response[26]);
        }
    }
}

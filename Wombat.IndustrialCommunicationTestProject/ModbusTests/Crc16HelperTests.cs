using System.Linq;
using Wombat.IndustrialCommunication.Helpers;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.ModbusTests
{
    public class Crc16HelperTests
    {
        [Fact]
        public void CalculatesAndValidatesStandardModbusFrame()
        {
            byte[] payload = { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
            byte[] crc = CRC16Helper.GetCRC16(payload);

            Assert.Equal(new byte[] { 0xC5, 0xCD }, crc);
            Assert.True(CRC16Helper.ValidateCRC(payload.Concat(crc).ToArray()));
        }
    }
}

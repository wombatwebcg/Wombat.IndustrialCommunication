using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusTcpPacket
    {
        public ushort TransactionId { get; private set; }
        public ushort ProtocolId { get; private set; }
        public ushort Length { get; private set; }
        public byte Station { get; private set; }
        public byte FunctionCode { get; private set; }
        public ushort StartingAddress { get; private set; }
        public ushort QuantityOfRegisters { get; private set; }

        // Constructor to parse the received data
        public ModbusTcpPacket(byte[] data)
        {
            if (data.Length < 12)
            {
                throw new ArgumentException("Invalid data length. Expected at least 12 bytes.");
            }

            TransactionId = BitConverter.ToUInt16(new byte[] { data[1], data[0] }, 0); // Transaction ID
            ProtocolId = BitConverter.ToUInt16(new byte[] { data[3], data[2] }, 0);    // Protocol ID
            Length = BitConverter.ToUInt16(new byte[] { data[5], data[4] }, 0);        // Length
            Station = data[6];                                                         // Unit ID
            FunctionCode = data[7];                                                    // Function Code
            StartingAddress = BitConverter.ToUInt16(new byte[] { data[9], data[8] }, 0);  // Starting Address
            QuantityOfRegisters = BitConverter.ToUInt16(new byte[] { data[11], data[10] }, 0);  // Quantity of Registers
        }

    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{

    public class ModbusRtuPacket
    {
        public byte SlaveAddress { get; private set; }
        public byte FunctionCode { get; private set; }
        public byte[] Data { get; private set; }

        // Constructor to parse the received data
        public ModbusRtuPacket(byte[] data)
        {
            if (data.Length < 3)
            {
                throw new ArgumentException("Invalid data length. Modbus RTU packet must have at least 3 bytes.");
            }

            // Slave Address
            SlaveAddress = data[0];

            // Function Code
            FunctionCode = data[1];

            // Data (excluding Slave Address and Function Code)
            int dataLength = data.Length - 2 - 2; // Subtract the 2 CRC bytes
            Data = new byte[dataLength];
            Array.Copy(data, 2, Data, 0, dataLength);

            // CRC (last 2 bytes)
            var crc = new byte[] { data[data.Length - 1], data[data.Length - 2] };

            if (!CRC16Helper.ValidateCRC(crc))
            {
                throw new ArgumentException("crc validate error.");

            }
        }


    }
}

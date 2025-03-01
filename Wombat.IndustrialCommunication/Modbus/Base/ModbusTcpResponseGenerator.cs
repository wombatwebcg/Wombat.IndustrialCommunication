using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusTcpResponseGenerator
    {
        public ushort TransactionId { get; private set; }
        public ushort ProtocolId { get; private set; }
        public ushort Length { get; private set; }
        public byte Station { get; private set; }
        public byte FunctionCode { get; private set; }
        public byte[] Data { get; private set; }

        public ModbusTcpResponseGenerator(ushort transactionId, ushort protocolId, byte station, byte functionCode, byte[] data)
        {
            TransactionId = transactionId;
            ProtocolId = protocolId;
            Station = station;
            FunctionCode = functionCode;
            Data = data;

            // Length = Unit ID (1 byte) + Function Code (1 byte) + Data length
            Length = (ushort)(2 + data.Length);
        }

        public byte[] ToByteArray()
        {
            byte[] response = new byte[6 + Length];

            // Transaction ID (2 bytes)
            response[0] = (byte)(TransactionId >> 8);
            response[1] = (byte)(TransactionId & 0xFF);

            // Protocol ID (2 bytes)
            response[2] = (byte)(ProtocolId >> 8);
            response[3] = (byte)(ProtocolId & 0xFF);

            // Length (2 bytes)
            response[4] = (byte)(Length >> 8);
            response[5] = (byte)(Length & 0xFF);

            // Unit ID (1 byte)
            response[6] = Station;

            // Function Code (1 byte)
            response[7] = FunctionCode;

            // Data
            Array.Copy(Data, 0, response, 8, Data.Length);

            return response;
        }
    }
}

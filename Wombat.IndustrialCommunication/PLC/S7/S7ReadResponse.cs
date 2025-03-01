using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.PLC
{
    public class S7ReadResponse : IDeviceReadWriteMessage
    {
        public S7ReadResponse(byte[] data)
        {
            ProtocolMessageFrame = data;
        }
        public byte[] ProtocolMessageFrame { get; set; }

        public int ProtocolDataNumber { get; set; }

        public int RegisterCount { get; set; }

        public string RegisterAddress { get; set; }

        public int ProtocolResponseLength { get; set; } = SiemensConstant.InitHeadLength;

        public void Initialize(byte[] frame)
        {
            
        }

    }
}

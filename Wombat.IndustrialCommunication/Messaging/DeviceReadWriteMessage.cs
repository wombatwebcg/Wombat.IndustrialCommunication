using System.Diagnostics.CodeAnalysis;
using Wombat.IndustrialCommunication.Abstractions;
using Wombat.IndustrialCommunication.Enums;

namespace Wombat.IndustrialCommunication.Messaging
{


    public class DeviceReadWriteMessage : IDeviceReadWriteMessage
    {

        public int RegisterCount { get; set; }
        public string RegisterAddress { get; set; }
        public int ProtocolResponseLength { get; set; }
        public byte[] ProtocolMessageFrame { get; set; }
        public int ProtocolDataNumber { get; set; }
        public void Initialize(byte[] frame)
        {
            ProtocolMessageFrame = frame;
        }
    }

}

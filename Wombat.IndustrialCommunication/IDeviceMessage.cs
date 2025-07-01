using System.Diagnostics.CodeAnalysis;
using Wombat.IndustrialCommunication.Enums;

namespace Wombat.IndustrialCommunication
{

    public interface IDeviceMessage
    {

        byte[] ProtocolMessageFrame { get; }
        int ProtocolDataNumber { get; }
        void Initialize(byte[] frame);
    }
}

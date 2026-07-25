using System.Diagnostics.CodeAnalysis;
using Wombat.IndustrialCommunication.Enums;

namespace Wombat.IndustrialCommunication
{


    public interface IDeviceReadWriteMessage : IDeviceMessage
    {

        int RegisterCount { get; set; }

        string RegisterAddress { get; set; }

        int ProtocolResponseLength { get; set; }

    }

}

using System.Diagnostics.CodeAnalysis;
using Wombat.IndustrialCommunication.Enums;

namespace Wombat.IndustrialCommunication.Abstractions
{


    public interface IDeviceReadWriteMessage : IDeviceMessage
    {

        int RegisterCount { get; set; }

        string RegisterAddress { get; set; }

        int ProtocolResponseLength { get; set; }

    }

}

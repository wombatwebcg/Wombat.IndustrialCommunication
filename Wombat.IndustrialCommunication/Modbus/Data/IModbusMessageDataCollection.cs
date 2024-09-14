using System.Diagnostics.CodeAnalysis;

namespace Wombat.IndustrialCommunication.Modbus.Data
{

    public interface IModbusMessageDataCollection
    {
        byte[] NetworkBytes { get; }

        byte ByteCount { get; }
    }
}

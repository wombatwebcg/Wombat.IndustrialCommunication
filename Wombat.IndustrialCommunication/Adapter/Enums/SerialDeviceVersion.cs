using System.ComponentModel;

namespace Wombat.IndustrialCommunication.Adapter
{
    public enum SerialDeviceVersion
    {
        [Description("ModbusRtu")]
        ModbusRtu,
        [Description("ModBusAscii")]
        ModBusAscii,
        [Description("MitsubishiFxSerial")]
        MitsubishiFxSerial
    }
}

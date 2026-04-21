namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 设备连接类型。
    /// </summary>
    public enum DeviceConnectionType
    {
        Unknown = 0,
        ModbusTcp = 1,
        ModbusRtu = 2,
        SiemensS7 = 3,
        Fins = 4
    }
}

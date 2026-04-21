using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.Modbus;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public sealed class ModbusTcpPooledConnection : BasePooledDeviceConnection
    {
        public ModbusTcpPooledConnection(ConnectionIdentity identity, ModbusTcpClient client)
            : base(identity, client)
        {
        }
    }
}

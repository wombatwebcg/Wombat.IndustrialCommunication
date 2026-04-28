using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.Modbus;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public sealed class ModbusTcpServerPooledConnection : BasePooledDeviceServerConnection
    {
        public ModbusTcpServerPooledConnection(ConnectionIdentity identity, ModbusTcpServer server)
            : base(identity, server)
        {
        }
    }
}

using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.Modbus;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public sealed class ModbusRtuServerPooledConnection : BasePooledDeviceServerConnection
    {
        public ModbusRtuServerPooledConnection(ConnectionIdentity identity, ModbusRtuServer server)
            : base(identity, server)
        {
        }
    }
}

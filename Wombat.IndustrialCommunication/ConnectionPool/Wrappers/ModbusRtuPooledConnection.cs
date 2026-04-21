using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.Modbus;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public sealed class ModbusRtuPooledConnection : BasePooledDeviceConnection
    {
        public ModbusRtuPooledConnection(ConnectionIdentity identity, ModbusRtuClient client)
            : base(identity, client)
        {
        }
    }
}

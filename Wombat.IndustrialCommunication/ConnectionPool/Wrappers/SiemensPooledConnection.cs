using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public sealed class SiemensPooledConnection : BasePooledDeviceConnection
    {
        public SiemensPooledConnection(ConnectionIdentity identity, SiemensClient client)
            : base(identity, client)
        {
        }
    }
}

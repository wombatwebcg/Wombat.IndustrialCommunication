using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public sealed class FinsPooledConnection : BasePooledDeviceConnection
    {
        public FinsPooledConnection(ConnectionIdentity identity, FinsClient client)
            : base(identity, client)
        {
        }
    }
}

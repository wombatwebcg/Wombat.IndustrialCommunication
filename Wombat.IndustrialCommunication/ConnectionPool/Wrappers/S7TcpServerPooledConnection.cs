using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public sealed class S7TcpServerPooledConnection : BasePooledDeviceServerConnection
    {
        public S7TcpServerPooledConnection(ConnectionIdentity identity, S7TcpServer server)
            : base(identity, server)
        {
        }
    }
}

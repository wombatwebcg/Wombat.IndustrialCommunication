using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public abstract class BasePooledDeviceServerConnection : BasePooledResourceConnection<IDeviceServer>
    {
        protected BasePooledDeviceServerConnection(ConnectionIdentity identity, IDeviceServer server)
            : base(identity, server)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }
        }

        protected IDeviceServer Server => Resource;

        protected override bool IsAvailableCore()
        {
            return Server != null && Server.IsListening;
        }

        protected override Task<OperationResult> EnsureAvailableCoreAsync()
        {
            return Task.FromResult(Server.Listen());
        }

        protected override OperationResult DisconnectOrShutdownCore()
        {
            return Server.Shutdown();
        }
    }
}

using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Wrappers
{
    public abstract class BasePooledDeviceClientConnection : BasePooledResourceConnection<IDeviceClient>
    {
        protected BasePooledDeviceClientConnection(ConnectionIdentity identity, IDeviceClient client)
            : base(identity, client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            client.IsLongConnection = true;
        }

        protected IDeviceClient Client => Resource;

        protected override bool IsAvailableCore()
        {
            return Client != null && Client.Connected;
        }

        protected override Task<OperationResult> EnsureAvailableCoreAsync()
        {
            return Client.ConnectAsync();
        }

        protected override OperationResult DisconnectOrShutdownCore()
        {
            return Client.Disconnect();
        }
    }
}

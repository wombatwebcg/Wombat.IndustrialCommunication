using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
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
            if (Client == null)
            {
                return false;
            }

            if (Client.Connected)
            {
                return true;
            }

            if (TryGetPingHost(out var host) && CanPingHost(host))
            {
                return false;
            }


            return false;
        }

        protected override Task<OperationResult> EnsureAvailableCoreAsync()
        {
            return Client.ConnectAsync();
        }

        protected override OperationResult DisconnectOrShutdownCore()
        {
            return Client.Disconnect();
        }

        private bool TryGetPingHost(out string host)
        {
            host = null;
            var clientType = Client.GetType();
            var ipEndPointProperty = clientType.GetProperty("IPEndPoint", BindingFlags.Instance | BindingFlags.Public);
            if (ipEndPointProperty != null)
            {
                var endPoint = ipEndPointProperty.GetValue(Client, null) as IPEndPoint;
                host = endPoint?.Address?.ToString();
                if (!string.IsNullOrWhiteSpace(host))
                {
                    return true;
                }
            }

            var ipAddressProperty = clientType.GetProperty("IpAddress", BindingFlags.Instance | BindingFlags.Public);
            host = ipAddressProperty?.GetValue(Client, null) as string;
            return !string.IsNullOrWhiteSpace(host);
        }

        private static bool CanPingHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(host, 1000);
                    return reply != null && reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

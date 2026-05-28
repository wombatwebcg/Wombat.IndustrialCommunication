using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunicationTest.ConnectionPoolTests
{
    internal static class ConnectionPoolTestDescriptors
    {
        public static ResourceDescriptor CreateModbusTcpClientDescriptor(ConnectionIdentity identity)
        {
            return new ResourceDescriptor
            {
                Identity = identity,
                ResourceRole = ResourceRole.Client,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp,
                ConnectionParameters = new ModbusTcpClientConnectionParameters
                {
                    Ip = "127.0.0.1",
                    Port = 502
                }
            };
        }

        public static ResourceDescriptor CreateModbusTcpServerDescriptor(ConnectionIdentity identity)
        {
            return new ResourceDescriptor
            {
                Identity = identity,
                ResourceRole = ResourceRole.Server,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp,
                ConnectionParameters = new ModbusTcpServerConnectionParameters
                {
                    Ip = "127.0.0.1",
                    Port = 1502
                }
            };
        }
    }
}

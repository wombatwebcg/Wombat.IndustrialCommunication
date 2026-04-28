using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.ConnectionPool.Wrappers;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ConnectionPoolTests
{
    public class ConnectionPoolFactoryIntegrationTests
    {
        [Fact]
        public void Should_Create_ModbusTcp_Wrapper()
        {
            var factory = new DefaultPooledDeviceClientConnectionFactory();
            var descriptor = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "modbus", ProtocolType = "ModbusTcp", Endpoint = "127.0.0.1:502" },
                DeviceConnectionType = DeviceConnectionType.ModbusTcp
            };
            descriptor.Parameters["ip"] = "127.0.0.1";
            descriptor.Parameters["port"] = 502;

            var result = factory.Create(descriptor);

            Assert.True(result.IsSuccess);
            Assert.IsType<ModbusTcpPooledConnection>(result.ResultValue);
            Assert.True(result.ResultValue.Resource.IsLongConnection);
        }

        [Fact]
        public void Should_Create_S7_Wrapper()
        {
            var factory = new DefaultPooledDeviceClientConnectionFactory();
            var descriptor = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "s7", ProtocolType = "SiemensS7", Endpoint = "127.0.0.1:102" },
                DeviceConnectionType = DeviceConnectionType.SiemensS7
            };
            descriptor.Parameters["ip"] = "127.0.0.1";
            descriptor.Parameters["port"] = 102;
            descriptor.Parameters["siemensVersion"] = SiemensVersion.S7_1200;
            descriptor.Parameters["slot"] = 0;
            descriptor.Parameters["rack"] = 0;

            var result = factory.Create(descriptor);

            Assert.True(result.IsSuccess);
            Assert.IsType<SiemensPooledConnection>(result.ResultValue);
            Assert.True(result.ResultValue.Resource.IsLongConnection);
        }

        [Fact]
        public void Should_Create_Rtu_And_Fins_Wrappers()
        {
            var factory = new DefaultPooledDeviceClientConnectionFactory();

            var rtu = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "rtu", ProtocolType = "ModbusRtu", Endpoint = "COM1" },
                DeviceConnectionType = DeviceConnectionType.ModbusRtu
            };
            rtu.Parameters["portName"] = "COM1";

            var fins = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "fins", ProtocolType = "Fins", Endpoint = "127.0.0.1:9600" },
                DeviceConnectionType = DeviceConnectionType.Fins
            };
            fins.Parameters["ip"] = "127.0.0.1";
            fins.Parameters["port"] = 9600;

            var rtuResult = factory.Create(rtu);
            var finsResult = factory.Create(fins);

            Assert.True(rtuResult.IsSuccess);
            Assert.True(finsResult.IsSuccess);
            Assert.IsType<ModbusRtuPooledConnection>(rtuResult.ResultValue);
            Assert.IsType<FinsPooledConnection>(finsResult.ResultValue);
        }

        [Fact]
        public void Should_Create_ModbusTcp_Server_Wrapper()
        {
            var factory = new DefaultPooledDeviceServerConnectionFactory();
            var descriptor = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "modbus-srv", ProtocolType = "ModbusTcp", Endpoint = "127.0.0.1:1502" },
                ResourceRole = ResourceRole.Server,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp
            };
            descriptor.Parameters["ip"] = "127.0.0.1";
            descriptor.Parameters["port"] = 1502;

            var result = factory.Create(descriptor);

            Assert.True(result.IsSuccess);
            Assert.IsType<ModbusTcpServerPooledConnection>(result.ResultValue);
        }

        [Fact]
        public void Should_Create_Rtu_And_S7_Server_Wrappers()
        {
            var factory = new DefaultPooledDeviceServerConnectionFactory();

            var rtu = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "rtu-srv", ProtocolType = "ModbusRtu", Endpoint = "COM2" },
                ResourceRole = ResourceRole.Server,
                DeviceConnectionType = DeviceConnectionType.ModbusRtu
            };
            rtu.Parameters["portName"] = "COM2";

            var s7 = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "s7-srv", ProtocolType = "SiemensS7", Endpoint = "127.0.0.1:1102" },
                ResourceRole = ResourceRole.Server,
                DeviceConnectionType = DeviceConnectionType.SiemensS7
            };
            s7.Parameters["ip"] = "127.0.0.1";
            s7.Parameters["port"] = 1102;

            var rtuResult = factory.Create(rtu);
            var s7Result = factory.Create(s7);

            Assert.True(rtuResult.IsSuccess);
            Assert.True(s7Result.IsSuccess);
            Assert.IsType<ModbusRtuServerPooledConnection>(rtuResult.ResultValue);
            Assert.IsType<S7TcpServerPooledConnection>(s7Result.ResultValue);
        }
    }
}



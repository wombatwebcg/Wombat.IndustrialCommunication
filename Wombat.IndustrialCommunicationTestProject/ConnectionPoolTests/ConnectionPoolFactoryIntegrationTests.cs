using System;
using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.ConnectionPool.Wrappers;
using Wombat.IndustrialCommunication.Modbus;
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
                ResourceRole = ResourceRole.Client,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp,
                ConnectionParameters = new ModbusTcpClientConnectionParameters
                {
                    Ip = "127.0.0.1",
                    Port = 502,
                    BatchReadStationIntervalMilliseconds = 100
                }
            };

            var result = factory.Create(descriptor);

            Assert.True(result.IsSuccess);
            Assert.IsType<ModbusTcpPooledConnection>(result.ResultValue);
            Assert.True(result.ResultValue.Resource.IsLongConnection);
            var client = Assert.IsType<ModbusTcpClient>(result.ResultValue.Resource);
            Assert.Equal(TimeSpan.FromMilliseconds(100), client.BatchReadStationInterval);
        }

        [Fact]
        public void Should_Create_S7_Wrapper()
        {
            var factory = new DefaultPooledDeviceClientConnectionFactory();
            var descriptor = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "s7", ProtocolType = "SiemensS7", Endpoint = "127.0.0.1:102" },
                ResourceRole = ResourceRole.Client,
                DeviceConnectionType = DeviceConnectionType.SiemensS7,
                ConnectionParameters = new SiemensS7ClientConnectionParameters
                {
                    Ip = "127.0.0.1",
                    Port = 102,
                    SiemensVersion = SiemensVersion.S7_1200,
                    Slot = 0,
                    Rack = 0
                }
            };

            var result = factory.Create(descriptor);

            Assert.True(result.IsSuccess);
            Assert.IsType<SiemensPooledConnection>(result.ResultValue);
            Assert.True(result.ResultValue.Resource.IsLongConnection);
            var client = Assert.IsType<SiemensClient>(result.ResultValue.Resource);
            Assert.Equal(SiemensVersion.S7_1200, client.SiemensVersion);
        }

        [Fact]
        public void Should_Create_Rtu_And_Fins_Wrappers()
        {
            var factory = new DefaultPooledDeviceClientConnectionFactory();

            var rtu = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "rtu", ProtocolType = "ModbusRtu", Endpoint = "COM1" },
                ResourceRole = ResourceRole.Client,
                DeviceConnectionType = DeviceConnectionType.ModbusRtu,
                ConnectionParameters = new ModbusRtuClientConnectionParameters
                {
                    PortName = "COM1",
                    BatchReadStationIntervalMilliseconds = 120
                }
            };

            var fins = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "fins", ProtocolType = "Fins", Endpoint = "127.0.0.1:9600" },
                ResourceRole = ResourceRole.Client,
                DeviceConnectionType = DeviceConnectionType.Fins,
                ConnectionParameters = new FinsClientConnectionParameters
                {
                    Ip = "127.0.0.1",
                    Port = 9600
                }
            };

            var rtuResult = factory.Create(rtu);
            var finsResult = factory.Create(fins);

            Assert.True(rtuResult.IsSuccess);
            Assert.True(finsResult.IsSuccess);
            Assert.IsType<ModbusRtuPooledConnection>(rtuResult.ResultValue);
            Assert.IsType<FinsPooledConnection>(finsResult.ResultValue);
            var rtuClient = Assert.IsType<ModbusRtuClient>(rtuResult.ResultValue.Resource);
            Assert.Equal(TimeSpan.FromMilliseconds(120), rtuClient.BatchReadStationInterval);
        }

        [Fact]
        public void Should_Create_ModbusTcp_Server_Wrapper()
        {
            var factory = new DefaultPooledDeviceServerConnectionFactory();
            var descriptor = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "modbus-srv", ProtocolType = "ModbusTcp", Endpoint = "127.0.0.1:1502" },
                ResourceRole = ResourceRole.Server,
                DeviceConnectionType = DeviceConnectionType.ModbusTcp,
                ConnectionParameters = new ModbusTcpServerConnectionParameters
                {
                    Ip = "127.0.0.1",
                    Port = 1502
                }
            };

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
                DeviceConnectionType = DeviceConnectionType.ModbusRtu,
                ConnectionParameters = new ModbusRtuServerConnectionParameters
                {
                    PortName = "COM2"
                }
            };

            var s7 = new ResourceDescriptor
            {
                Identity = new ConnectionIdentity { DeviceId = "s7-srv", ProtocolType = "SiemensS7", Endpoint = "127.0.0.1:1102" },
                ResourceRole = ResourceRole.Server,
                DeviceConnectionType = DeviceConnectionType.SiemensS7,
                ConnectionParameters = new SiemensS7ServerConnectionParameters
                {
                    Ip = "127.0.0.1",
                    Port = 1102
                }
            };

            var rtuResult = factory.Create(rtu);
            var s7Result = factory.Create(s7);

            Assert.True(rtuResult.IsSuccess);
            Assert.True(s7Result.IsSuccess);
            Assert.IsType<ModbusRtuServerPooledConnection>(rtuResult.ResultValue);
            Assert.IsType<S7TcpServerPooledConnection>(s7Result.ResultValue);
        }
    }
}

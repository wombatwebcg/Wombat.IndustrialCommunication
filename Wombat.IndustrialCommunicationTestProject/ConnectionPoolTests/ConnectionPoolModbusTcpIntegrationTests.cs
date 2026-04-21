using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ConnectionPoolTests
{
    [CollectionDefinition("ConnectionPool ModbusTcp Integration", DisableParallelization = true)]
    public sealed class ConnectionPoolModbusTcpIntegrationCollection
    {
    }

    [Collection("ConnectionPool ModbusTcp Integration")]
    public class ConnectionPoolModbusTcpIntegrationTests : IClassFixture<ModbusTcpServerClusterFixture>
    {
        private const byte Station = 1;
        private readonly ModbusTcpServerClusterFixture _fixture;

        public ConnectionPoolModbusTcpIntegrationTests(ModbusTcpServerClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Should_Execute_All_Documented_HoldingRegister_DataTypes_Against_ModbusTcp_Servers()
        {
            if (!_fixture.IsAvailable)
            {
                return;
            }

            _fixture.ResetHoldingRegisters();

            using (var pool = CreatePool())
            {
                RegisterDefaultDescriptors(pool);

                await AssertRoundTripAsync(pool, 502, DataTypeEnums.Bool, "1;40801", true).ConfigureAwait(false);
                await AssertRoundTripAsync(pool, 502, DataTypeEnums.Int16, "1;40802", (short)-12345).ConfigureAwait(false);
                await AssertRoundTripAsync(pool, 502, DataTypeEnums.UInt16, "1;40803", (ushort)54321).ConfigureAwait(false);

                await AssertRoundTripAsync(pool, 503, DataTypeEnums.Int32, "1;40820", -123456789).ConfigureAwait(false);
                await AssertRoundTripAsync(pool, 503, DataTypeEnums.UInt32, "1;40830", 3234567890U).ConfigureAwait(false);
                await AssertRoundTripAsync(pool, 503, DataTypeEnums.Float, "1;40840", 123.456f).ConfigureAwait(false);

                await AssertRoundTripAsync(pool, 504, DataTypeEnums.Int64, "1;40860", -1234567890123456789L).ConfigureAwait(false);
                await AssertRoundTripAsync(pool, 504, DataTypeEnums.UInt64, "1;40880", 12345678901234567890UL).ConfigureAwait(false);
                await AssertRoundTripAsync(pool, 504, DataTypeEnums.Double, "1;40910", 98765.4321d).ConfigureAwait(false);
                await AssertStringRoundTripAsync(pool, 502, "1;16;40940", "1;3;40940", "WOMBAT").ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task Should_Read_And_Write_PointLists_Against_ModbusTcp_Servers_On_First_Thousand_HoldingRegisters()
        {
            if (!_fixture.IsAvailable)
            {
                return;
            }

            _fixture.ResetHoldingRegisters();

            using (var pool = CreatePool())
            {
                RegisterDefaultDescriptors(pool);

                await AssertPointListRoundTripAsync(
                    pool,
                    502,
                    new DevicePointWriteRequest
                    {
                        Name = "bool-holding",
                        Address = "1;40961",
                        DataType = DataTypeEnums.Bool,
                        EnableBatch = true,
                        Value = true
                    },
                    new DevicePointWriteRequest
                    {
                        Name = "int16-holding",
                        Address = "1;40962",
                        DataType = DataTypeEnums.Int16,
                        EnableBatch = true,
                        Value = (short)-2222
                    },
                    new DevicePointWriteRequest
                    {
                        Name = "uint16-holding",
                        Address = "1;40963",
                        DataType = DataTypeEnums.UInt16,
                        EnableBatch = true,
                        Value = (ushort)2222
                    }).ConfigureAwait(false);

                await AssertPointListRoundTripAsync(
                    pool,
                    503,
                    new DevicePointWriteRequest
                    {
                        Name = "int32-holding",
                        Address = "1;40970",
                        DataType = DataTypeEnums.Int32,
                        EnableBatch = true,
                        Value = -3333333
                    },
                    new DevicePointWriteRequest
                    {
                        Name = "uint32-holding",
                        Address = "1;40980",
                        DataType = DataTypeEnums.UInt32,
                        EnableBatch = true,
                        Value = 3333333U
                    },
                    new DevicePointWriteRequest
                    {
                        Name = "float-holding",
                        Address = "1;40990",
                        DataType = DataTypeEnums.Float,
                        EnableBatch = true,
                        Value = 33.75f
                    }).ConfigureAwait(false);

                await AssertPointListRoundTripAsync(
                    pool,
                    504,
                    new DevicePointWriteRequest
                    {
                        Name = "int64-holding",
                        Address = "1;40920",
                        DataType = DataTypeEnums.Int64,
                        EnableBatch = true,
                        Value = -444444444444L
                    },
                    new DevicePointWriteRequest
                    {
                        Name = "uint64-holding",
                        Address = "1;40930",
                        DataType = DataTypeEnums.UInt64,
                        EnableBatch = true,
                        Value = 444444444444UL
                    },
                    new DevicePointWriteRequest
                    {
                        Name = "double-holding",
                        Address = "1;40950",
                        DataType = DataTypeEnums.Double,
                        EnableBatch = true,
                        Value = 4444.8888d
                    }).ConfigureAwait(false);
            }
        }

        private static DeviceConnectionPool CreatePool()
        {
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = false,
                MaxRetryCount = 1,
                RetryBackoff = TimeSpan.FromMilliseconds(50),
                LeaseTimeout = TimeSpan.FromSeconds(10),
                IdleTimeout = TimeSpan.FromMinutes(5)
            };

            return new DeviceConnectionPool(options, new DefaultPooledDeviceConnectionFactory());
        }

        private static void RegisterDefaultDescriptors(DeviceConnectionPool pool)
        {
            foreach (var descriptor in CreateDescriptors())
            {
                var register = pool.Register(descriptor);
                Assert.True(register.IsSuccess, "注册 ModbusTcp 连接失败: " + register.Message);
            }
        }

        private static IEnumerable<DeviceConnectionDescriptor> CreateDescriptors()
        {
            yield return CreateDescriptor(502);
            yield return CreateDescriptor(503);
            yield return CreateDescriptor(504);
        }

        private static DeviceConnectionDescriptor CreateDescriptor(int port)
        {
            var descriptor = new DeviceConnectionDescriptor
            {
                Identity = new ConnectionIdentity
                {
                    DeviceId = "modbus-tcp-" + port,
                    ProtocolType = "ModbusTcp",
                    Endpoint = "127.0.0.1:" + port
                },
                DeviceConnectionType = DeviceConnectionType.ModbusTcp
            };

            descriptor.Parameters["ip"] = "127.0.0.1";
            descriptor.Parameters["port"] = port;
            descriptor.Parameters["connectTimeoutMilliseconds"] = 2000;
            descriptor.Parameters["receiveTimeoutMilliseconds"] = 2000;
            descriptor.Parameters["sendTimeoutMilliseconds"] = 2000;
            return descriptor;
        }

        private static async Task AssertRoundTripAsync(DeviceConnectionPool pool, int port, DataTypeEnums dataType, string address, object expectedValue)
        {
            var identity = CreateIdentity(port);
            var writeResult = await pool.ExecuteAsync(identity, async client =>
                await client.WriteAsync(dataType, address, expectedValue).ConfigureAwait(false)).ConfigureAwait(false);
            Assert.True(writeResult.IsSuccess, "写入失败, port=" + port + ", dataType=" + dataType + ", address=" + address + ", message=" + writeResult.Message);

            var readResult = await pool.ExecuteAsync<object>(identity, async client =>
                await client.ReadAsync(dataType, address).ConfigureAwait(false)).ConfigureAwait(false);
            Assert.True(readResult.IsSuccess, "读取失败, port=" + port + ", dataType=" + dataType + ", address=" + address + ", message=" + readResult.Message);

            AssertValue(dataType, expectedValue, readResult.ResultValue);
        }

        private static async Task AssertStringRoundTripAsync(DeviceConnectionPool pool, int port, string writeAddress, string readAddress, string expectedValue)
        {
            var identity = CreateIdentity(port);
            var result = await pool.ExecuteAsync<string>(identity, async client =>
            {
                var write = await client.WriteAsync(writeAddress, expectedValue).ConfigureAwait(false);
                if (!write.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<string>(write);
                }

                return await client.ReadStringAsync(readAddress, expectedValue.Length).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Assert.True(result.IsSuccess, "字符串读写失败, port=" + port + ", message=" + result.Message);
            Assert.Equal(expectedValue, result.ResultValue);
        }

        private static async Task AssertPointListRoundTripAsync(DeviceConnectionPool pool, int port, params DevicePointWriteRequest[] writes)
        {
            var identity = CreateIdentity(port);
            var writeResult = await pool.WritePointsAsync(identity, writes).ConfigureAwait(false);
            Assert.True(writeResult.IsSuccess, "点位写入失败, port=" + port + ", message=" + writeResult.Message);
            Assert.All(writeResult.ResultValue, item => Assert.True(item.IsSuccess, "点位写入项失败: " + item.Name + ", message=" + item.Message));

            var reads = writes.Select(t => new DevicePointReadRequest
            {
                Name = t.Name,
                Address = t.Address,
                DataType = t.DataType,
                Length = t.Length <= 0 ? 1 : t.Length,
                EnableBatch = t.EnableBatch
            }).ToList();

            var readResult = await pool.ReadPointsAsync(identity, reads).ConfigureAwait(false);
            Assert.True(readResult.IsSuccess, "点位读取失败, port=" + port + ", message=" + readResult.Message);
            Assert.Equal(writes.Length, readResult.ResultValue.Count);

            foreach (var write in writes)
            {
                var read = readResult.ResultValue.Single(t => t.Name == write.Name);
                Assert.True(read.IsSuccess, "点位读取项失败: " + read.Name + ", message=" + read.Message);
                AssertValue(write.DataType, write.Value, read.Value);
            }
        }

        private static ConnectionIdentity CreateIdentity(int port)
        {
            return new ConnectionIdentity
            {
                DeviceId = "modbus-tcp-" + port,
                ProtocolType = "ModbusTcp",
                Endpoint = "127.0.0.1:" + port
            };
        }

        private static void AssertValue(DataTypeEnums dataType, object expected, object actual)
        {
            switch (dataType)
            {
                case DataTypeEnums.Bool:
                    Assert.Equal(Convert.ToBoolean(expected), Convert.ToBoolean(actual));
                    break;
                case DataTypeEnums.Int16:
                    Assert.Equal(Convert.ToInt16(expected), Convert.ToInt16(actual));
                    break;
                case DataTypeEnums.UInt16:
                    Assert.Equal(Convert.ToUInt16(expected), Convert.ToUInt16(actual));
                    break;
                case DataTypeEnums.Int32:
                    Assert.Equal(Convert.ToInt32(expected), Convert.ToInt32(actual));
                    break;
                case DataTypeEnums.UInt32:
                    Assert.Equal(Convert.ToUInt32(expected), Convert.ToUInt32(actual));
                    break;
                case DataTypeEnums.Int64:
                    Assert.Equal(Convert.ToInt64(expected), Convert.ToInt64(actual));
                    break;
                case DataTypeEnums.UInt64:
                    Assert.Equal(Convert.ToUInt64(expected), Convert.ToUInt64(actual));
                    break;
                case DataTypeEnums.Float:
                    Assert.True(Math.Abs(Convert.ToSingle(expected) - Convert.ToSingle(actual)) < 0.0001f, "Float 值不匹配");
                    break;
                case DataTypeEnums.Double:
                    Assert.True(Math.Abs(Convert.ToDouble(expected) - Convert.ToDouble(actual)) < 0.0000001d, "Double 值不匹配");
                    break;
                default:
                    throw new NotSupportedException("未处理的数据类型: " + dataType);
            }
        }
    }

    public sealed class ModbusTcpServerClusterFixture : IAsyncLifetime, IDisposable
    {
        private const byte DefaultStation = 1;
        private readonly List<ModbusTcpServer> _servers = new List<ModbusTcpServer>();
        public string StartupErrorMessage { get; private set; }
        public bool IsAvailable => string.IsNullOrWhiteSpace(StartupErrorMessage);

        public async Task InitializeAsync()
        {
            try
            {
                foreach (var port in new[] { 502, 503, 504 })
                {
                    var server = new ModbusTcpServer("127.0.0.1", port)
                    {
                        SlaveId = DefaultStation
                    };

                    var start = await server.StartAsync().ConfigureAwait(false);
                    if (!start.IsSuccess)
                    {
                        throw new InvalidOperationException("启动 ModbusTcpServer 失败, port=" + port + ", message=" + start.Message);
                    }

                    _servers.Add(server);
                }

                ResetHoldingRegisters();
            }
            catch (Exception ex)
            {
                StartupErrorMessage = "无法启动固定端口 ModbusTcpServer(127.0.0.1:502/503/504)，当前环境可能缺少端口权限或端口已被占用。原始错误: " + ex.Message;
                await DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task DisposeAsync()
        {
            foreach (var server in _servers)
            {
                try
                {
                    await server.StopAsync().ConfigureAwait(false);
                }
                finally
                {
                    server.Dispose();
                }
            }

            _servers.Clear();
        }

        public void ResetHoldingRegisters()
        {
            foreach (var server in _servers)
            {
                for (var index = 0; index < 1000; index++)
                {
                    server.DataStore.HoldingRegisters[index] = 0;
                }
            }
        }

        public void Dispose()
        {
        }
    }
}

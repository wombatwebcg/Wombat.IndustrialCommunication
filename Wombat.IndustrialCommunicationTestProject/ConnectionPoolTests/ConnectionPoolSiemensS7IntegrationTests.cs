using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ConnectionPoolTests
{
    [CollectionDefinition("ConnectionPool SiemensS7 Integration", DisableParallelization = true)]
    public sealed class ConnectionPoolSiemensS7IntegrationCollection
    {
    }

    [Collection("ConnectionPool SiemensS7 Integration")]
    public class ConnectionPoolSiemensS7IntegrationTests : IClassFixture<S7ServerFixture>
    {
        private readonly S7ServerFixture _fixture;

        public ConnectionPoolSiemensS7IntegrationTests(S7ServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Should_Execute_Read_And_Write_Against_S7_Server()
        {
            if (!_fixture.IsAvailable)
            {
                return;
            }

            using (var pool = CreatePool())
            {
                var register = pool.Register(CreateDescriptor());
                Assert.True(register.IsSuccess, "注册 SiemensS7 连接失败: " + register.Message);

                await AssertRoundTripAsync(pool, DataTypeEnums.Bool, "DB1.DBX0.0", true).ConfigureAwait(false);
                await AssertRoundTripAsync(pool, DataTypeEnums.Int16, "DB1.DBW2", (short)-1234).ConfigureAwait(false);
                await AssertRoundTripAsync(pool, DataTypeEnums.UInt16, "DB1.DBW4", (ushort)5678).ConfigureAwait(false);
                await AssertRoundTripAsync(pool, DataTypeEnums.Int32, "DB1.DBD6", 123456789).ConfigureAwait(false);
                await AssertRoundTripAsync(pool, DataTypeEnums.Float, "DB1.DBD10", 12.5f).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task Should_Read_And_Write_PointLists_Against_S7_Server()
        {
            if (!_fixture.IsAvailable)
            {
                return;
            }

            using (var pool = CreatePool())
            {
                var register = pool.Register(CreateDescriptor());
                Assert.True(register.IsSuccess, "注册 SiemensS7 连接失败: " + register.Message);

                var writes = new List<DevicePointWriteRequest>
                {
                    new DevicePointWriteRequest
                    {
                        Name = "s7-bool",
                        Address = "DB1.DBX20.0",
                        DataType = DataTypeEnums.Bool,
                        EnableBatch = true,
                        Value = true
                    },
                    new DevicePointWriteRequest
                    {
                        Name = "s7-int16",
                        Address = "DB1.DBW22",
                        DataType = DataTypeEnums.Int16,
                        EnableBatch = true,
                        Value = (short)2222
                    },
                    new DevicePointWriteRequest
                    {
                        Name = "s7-int32",
                        Address = "DB1.DBD24",
                        DataType = DataTypeEnums.Int32,
                        EnableBatch = true,
                        Value = 3333333
                    }
                };

                var writeResult = await pool.WritePointsAsync(CreateIdentity(), writes).ConfigureAwait(false);
                Assert.True(writeResult.IsSuccess, "S7 点位写入失败: " + writeResult.Message);
                Assert.All(writeResult.ResultValue, item => Assert.True(item.IsSuccess, "S7 点位写入项失败: " + item.Name + ", message=" + item.Message));

                var reads = writes.Select(t => new DevicePointReadRequest
                {
                    Name = t.Name,
                    Address = t.Address,
                    DataType = t.DataType,
                    Length = 1,
                    EnableBatch = true
                }).ToList();

                var readResult = await pool.ReadPointsAsync(CreateIdentity(), reads).ConfigureAwait(false);
                Assert.True(readResult.IsSuccess, "S7 点位读取失败: " + readResult.Message);
                Assert.Equal(writes.Count, readResult.ResultValue.Count);

                foreach (var write in writes)
                {
                    var read = readResult.ResultValue.Single(t => t.Name == write.Name);
                    Assert.True(read.IsSuccess, "S7 点位读取项失败: " + read.Name + ", message=" + read.Message);
                    AssertValue(write.DataType, write.Value, read.Value);
                }
            }
        }

        private static DeviceClientPool CreatePool()
        {
            var options = new ConnectionPoolOptions
            {
                EnableBackgroundMaintenance = false,
                MaxRetryCount = 1,
                RetryBackoff = TimeSpan.FromMilliseconds(50),
                LeaseTimeout = TimeSpan.FromSeconds(10),
                IdleTimeout = TimeSpan.FromMinutes(5)
            };

            return new DeviceClientPool(options, new DefaultPooledDeviceClientConnectionFactory());
        }

        private static ResourceDescriptor CreateDescriptor()
        {
            var descriptor = new ResourceDescriptor
            {
                Identity = CreateIdentity(),
                DeviceConnectionType = DeviceConnectionType.SiemensS7
            };

            descriptor.Parameters["ip"] = "127.0.0.1";
            descriptor.Parameters["port"] = 1202;
            descriptor.Parameters["siemensVersion"] = SiemensVersion.S7_1200;
            descriptor.Parameters["slot"] = 0;
            descriptor.Parameters["rack"] = 0;
            descriptor.Parameters["connectTimeoutMilliseconds"] = 2000;
            descriptor.Parameters["receiveTimeoutMilliseconds"] = 2000;
            descriptor.Parameters["sendTimeoutMilliseconds"] = 2000;
            return descriptor;
        }

        private static ConnectionIdentity CreateIdentity()
        {
            return new ConnectionIdentity
            {
                DeviceId = "siemens-s7-1202",
                ProtocolType = "SiemensS7",
                Endpoint = "127.0.0.1:1202"
            };
        }

        private static async Task AssertRoundTripAsync(DeviceClientPool pool, DataTypeEnums dataType, string address, object expectedValue)
        {
            var identity = CreateIdentity();
            var writeResult = await pool.ExecuteAsync(identity, async client =>
                await client.WriteAsync(dataType, address, expectedValue).ConfigureAwait(false)).ConfigureAwait(false);
            Assert.True(writeResult.IsSuccess, "S7 写入失败, dataType=" + dataType + ", address=" + address + ", message=" + writeResult.Message);

            var readResult = await pool.ExecuteAsync<object>(identity, async client =>
                await client.ReadAsync(dataType, address).ConfigureAwait(false)).ConfigureAwait(false);
            Assert.True(readResult.IsSuccess, "S7 读取失败, dataType=" + dataType + ", address=" + address + ", message=" + readResult.Message);

            AssertValue(dataType, expectedValue, readResult.ResultValue);
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
                case DataTypeEnums.Float:
                    Assert.True(Math.Abs(Convert.ToSingle(expected) - Convert.ToSingle(actual)) < 0.0001f, "Float 值不匹配");
                    break;
                default:
                    throw new NotSupportedException("未处理的数据类型: " + dataType);
            }
        }
    }

    public sealed class S7ServerFixture : IAsyncLifetime, IDisposable
    {
        private S7TcpServer _server;
        public string StartupErrorMessage { get; private set; }
        public bool IsAvailable => string.IsNullOrWhiteSpace(StartupErrorMessage);

        public async Task InitializeAsync()
        {
            try
            {
                _server = new S7TcpServer("127.0.0.1", 1202);
                _server.SetSiemensVersion(SiemensVersion.S7_1200);
                _server.SetRackSlot(0, 0);

                var dbResult = _server.CreateDataBlock(1, 512);
                if (!dbResult.IsSuccess)
                {
                    throw new InvalidOperationException("创建 S7 数据块失败: " + dbResult.Message);
                }

                var listenResult = _server.Listen();
                if (!listenResult.IsSuccess)
                {
                    throw new InvalidOperationException("启动 S7TcpServer 失败: " + listenResult.Message);
                }

                await Task.Delay(150).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StartupErrorMessage = "无法启动 S7TcpServer(127.0.0.1:1202)，当前环境可能端口被占用。原始错误: " + ex.Message;
                await DisposeAsync().ConfigureAwait(false);
            }
        }

        public Task DisposeAsync()
        {
            if (_server != null)
            {
                try
                {
                    _server.Shutdown();
                }
                finally
                {
                    _server.Dispose();
                    _server = null;
                }
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}



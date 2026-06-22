using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    public class S7RandomBatchWriteTests
    {
        private sealed class TestableS7Communication : S7Communication
        {
            private readonly int _nativeRandomWriteMaxItems;
            private readonly int _nativeRandomWriteMaxPayloadBytes;

            public TestableS7Communication() : base(null)
            {
                _nativeRandomWriteMaxItems = 10;
                _nativeRandomWriteMaxPayloadBytes = 180;
            }

            public TestableS7Communication(int nativeRandomWriteMaxItems = 10, int nativeRandomWriteMaxPayloadBytes = 180) : base(null)
            {
                _nativeRandomWriteMaxItems = nativeRandomWriteMaxItems;
                _nativeRandomWriteMaxPayloadBytes = nativeRandomWriteMaxPayloadBytes;
            }

            public List<S7WriteBatch> Split(IReadOnlyList<SiemensAddress> items) => SplitWriteBatches(items);

            internal override int GetNativeRandomWriteMaxItems() => _nativeRandomWriteMaxItems;
            internal override int GetNativeRandomWriteMaxPayloadBytes() => _nativeRandomWriteMaxPayloadBytes;
        }

        [Fact]
        public void WriteRequest_ShouldBuildMultiItemWriteVarFrame()
        {
            var items = new List<SiemensAddress>
            {
                new SiemensAddress
                {
                    OriginalAddress = "DB1.DBX0.0",
                    TypeCode = 0x84,
                    DbBlock = 1,
                    BeginAddress = 0,
                    IsBit = true,
                    WriteData = new byte[] { 0x01 }
                },
                new SiemensAddress
                {
                    OriginalAddress = "DB1.DBB2",
                    TypeCode = 0x84,
                    DbBlock = 1,
                    BeginAddress = 16,
                    IsBit = false,
                    WriteData = new byte[] { 0x5A }
                }
            };

            var request = new S7WriteRequest(items, 0x3456);

            Assert.Equal(54, request.ProtocolMessageFrame.Length);
            Assert.Equal(0x34, request.ProtocolMessageFrame[11]);
            Assert.Equal(0x56, request.ProtocolMessageFrame[12]);
            Assert.Equal(0x05, request.ProtocolMessageFrame[17]);
            Assert.Equal(0x02, request.ProtocolMessageFrame[18]);
            Assert.Equal(0x01, request.ProtocolMessageFrame[22]);
            Assert.Equal(0x02, request.ProtocolMessageFrame[34]);
            Assert.Equal(0x03, request.ProtocolMessageFrame[44]);
            Assert.Equal(0x01, request.ProtocolMessageFrame[47]);
            Assert.Equal(0x04, request.ProtocolMessageFrame[50]);
            Assert.Equal(0x5A, request.ProtocolMessageFrame[53]);
        }

        [Fact]
        public void WriteResponse_ShouldParsePartialSuccess()
        {
            var items = new List<SiemensAddress>
            {
                new SiemensAddress { OriginalAddress = "DB1.DBX0.0", WriteData = new byte[] { 0x01 } },
                new SiemensAddress { OriginalAddress = "DB1.DBB2", WriteData = new byte[] { 0x11 } }
            };

            var response = new byte[]
            {
                0x03,0x00,0x00,0x18,
                0x02,0xF0,0x80,
                0x32,0x03,0x00,0x00,0x12,0x34,0x00,0x02,0x00,0x02,0x00,0x00,
                0x05,0x02,
                0xFF,0x0A
            };

            var parsed = S7WriteResponse.Parse(response, items);

            Assert.True(parsed.IsSuccess);
            Assert.Equal(2, parsed.ResultValue.Items.Count);
            Assert.True(parsed.ResultValue.Items[0].IsSuccess);
            Assert.False(parsed.ResultValue.Items[1].IsSuccess);
            Assert.Equal(1, parsed.ResultValue.SuccessCount);
        }

        [Fact]
        public void WriteRequest_ShouldPadSingleByteNonLastItem_AndKeepLastItemUnpadded()
        {
            var items = new List<SiemensAddress>
            {
                CreateWriteItem("DB1.DBB0", 0, new byte[] { 0x11 }),
                CreateWriteItem("DB1.DBW2", 16, new byte[] { 0x22, 0x33 }),
                CreateWriteItem("DB1.DBB4", 32, new byte[] { 0x44 })
            };

            var request = new S7WriteRequest(items, 0x1111);

            Assert.Equal(17, request.ProtocolMessageFrame[16]);
            Assert.Equal(0x11, request.ProtocolMessageFrame[59]);
            Assert.Equal(0x00, request.ProtocolMessageFrame[60]);
            Assert.Equal(0x22, request.ProtocolMessageFrame[65]);
            Assert.Equal(0x33, request.ProtocolMessageFrame[66]);
            Assert.Equal(0x44, request.ProtocolMessageFrame[71]);
            Assert.Equal(72, request.ProtocolMessageFrame.Length);
        }

        [Fact]
        public void WriteRequest_ShouldDowngradeInvalidBitPayload_ToByteTransport()
        {
            var items = new List<SiemensAddress>
            {
                new SiemensAddress
                {
                    OriginalAddress = "DB1.DBX0.0",
                    TypeCode = 0x84,
                    DbBlock = 1,
                    BeginAddress = 0,
                    IsBit = true,
                    WriteData = new byte[] { 0x02 }
                }
            };

            var request = new S7WriteRequest(items, 0x2222);

            Assert.Equal(0x02, request.ProtocolMessageFrame[22]);
            Assert.Equal(0x04, request.ProtocolMessageFrame[32]);
            Assert.Equal(0x08, request.ProtocolMessageFrame[34]);
            Assert.Equal(0x02, request.ProtocolMessageFrame[35]);
        }

        [Fact]
        public void SplitWriteBatches_ShouldSplit_WhenItemCountExceedsLimit()
        {
            var communication = new TestableS7Communication(nativeRandomWriteMaxItems: 2, nativeRandomWriteMaxPayloadBytes: 32);
            var items = new List<SiemensAddress>
            {
                CreateWriteItem("DB1.DBB0", 0, new byte[] { 0x01 }),
                CreateWriteItem("DB1.DBB1", 8, new byte[] { 0x02 }),
                CreateWriteItem("DB1.DBB2", 16, new byte[] { 0x03 })
            };

            var batches = communication.Split(items);

            Assert.Equal(2, batches.Count);
            Assert.Equal(2, batches[0].Items.Count);
            Assert.Single(batches[1].Items);
            Assert.Equal("DB1.DBB2", batches[1].Items[0].OriginalAddress);
        }

        [Fact]
        public void WriteRequest_EstimateDataLength_ShouldMatchMixedPayloads()
        {
            var items = new List<SiemensAddress>
            {
                CreateWriteItem("DB1.DBB0", 0, new byte[] { 0x01 }),
                CreateWriteItem("DB1.DBW2", 16, new byte[] { 0x02, 0x03 }),
                CreateWriteItem("DB1.DBB4", 32, new byte[] { 0x04 })
            };

            var dataLength = S7WriteRequest.EstimateDataLength(items);
            Assert.Equal(17, dataLength);
        }

        [Fact]
        public void WriteResponse_ShouldSurfaceCustomFailureCode()
        {
            var items = new List<SiemensAddress>
            {
                new SiemensAddress { OriginalAddress = "DB1.DBB0", WriteData = new byte[] { 0x11 } }
            };

            var response = new byte[]
            {
                0x03,0x00,0x00,0x17,
                0x02,0xF0,0x80,
                0x32,0x03,0x00,0x00,0x12,0x34,0x00,0x02,0x00,0x01,0x00,0x01,
                0x05,0x01,
                0x0C,0x00
            };

            var parsed = S7WriteResponse.Parse(response, items);

            Assert.True(parsed.IsSuccess);
            Assert.False(parsed.ResultValue.Items[0].IsSuccess);
            Assert.Contains("异常状态:12", parsed.ResultValue.Items[0].Message);
        }

        [Fact]
        public void SplitWriteBatches_ShouldThrow_WhenSingleItemExceedsPayloadLimit()
        {
            var communication = new TestableS7Communication(nativeRandomWriteMaxItems: 10, nativeRandomWriteMaxPayloadBytes: 4);
            var items = new List<SiemensAddress>
            {
                new SiemensAddress
                {
                    OriginalAddress = "DB1.DBD0",
                    TypeCode = 0x84,
                    DbBlock = 1,
                    BeginAddress = 0,
                    IsBit = false,
                    WriteData = new byte[] { 0x01, 0x02, 0x03, 0x04 }
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() => communication.Split(items));
            Assert.Contains("超出随机批量写单项上限", ex.Message);
        }

        [Fact]
        public async Task SiemensClient_BatchWriteAsync_OnProtocolSynchronizationFailure_ShouldReconnectAndRetryWholeBatch()
        {
            using var server = new RetryOnceS7WriteTestServer();
            await server.StartAsync().ConfigureAwait(false);

            try
            {
                using var client = new SiemensClient(IPAddress.Loopback.ToString(), server.Port, SiemensVersion.S7_1200, slot: 0, rack: 0)
                {
                    ConnectTimeout = TimeSpan.FromSeconds(2),
                    ReceiveTimeout = TimeSpan.FromSeconds(2),
                    SendTimeout = TimeSpan.FromSeconds(2),
                    DirtyResponseRetryAttempts = 1,
                    IsLongConnection = true,
                    EnableAutoReconnect = true
                };

                var connectResult = await client.ConnectAsync().ConfigureAwait(false);
                Assert.True(connectResult.IsSuccess, connectResult.Message);

                var result = await client.BatchWriteAsync(new Dictionary<string, (DataTypeEnums, object)>
                {
                    ["VB2000"] = (DataTypeEnums.Byte, (object)(byte)0x5A),
                    ["VB2001"] = (DataTypeEnums.Byte, (object)(byte)0x6B)
                }).ConfigureAwait(false);

                Assert.True(result.IsSuccess, result.Message);
                Assert.Equal(2, server.WriteRequestCount);
                Assert.True(server.ConnectionCount >= 2, $"Expected reconnect, actual connections: {server.ConnectionCount}");
            }
            finally
            {
                await server.StopAsync().ConfigureAwait(false);
            }
        }

        private static byte[] CorruptPduReference(byte[] response)
        {
            var corrupted = response.ToArray();
            var s7Offset = 4 + 1 + corrupted[4];
            corrupted[s7Offset + 5] = (byte)(corrupted[s7Offset + 5] == byte.MaxValue ? 0 : corrupted[s7Offset + 5] + 1);
            return corrupted;
        }

        private static SiemensAddress CreateWriteItem(string address, int beginAddress, byte[] payload)
        {
            return new SiemensAddress
            {
                OriginalAddress = address,
                TypeCode = 0x84,
                DbBlock = 1,
                BeginAddress = beginAddress,
                IsBit = false,
                WriteData = payload,
                Length = payload?.Length ?? 0,
                RequestedLength = payload?.Length ?? 0
            };
        }

        private sealed class RetryOnceS7WriteTestServer : IDisposable
        {
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly TcpListener _listener;
            private Task _acceptLoopTask;
            private int _writeRequestCount;
            private int _connectionCount;

            public RetryOnceS7WriteTestServer()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
            }

            public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
            public int WriteRequestCount => Volatile.Read(ref _writeRequestCount);
            public int ConnectionCount => Volatile.Read(ref _connectionCount);

            public Task StartAsync()
            {
                _listener.Start();
                _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
                return Task.CompletedTask;
            }

            public async Task StopAsync()
            {
                _cts.Cancel();
                try { _listener.Stop(); } catch { }

                if (_acceptLoopTask != null)
                {
                    try { await _acceptLoopTask.ConfigureAwait(false); } catch { }
                }
            }

            private async Task AcceptLoopAsync(CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client = null;
                    try
                    {
                        client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        break;
                    }

                    Interlocked.Increment(ref _connectionCount);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
            }

            private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            var request = await ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
                            if (request == null)
                            {
                                break;
                            }

                            byte[] response;
                            if (request.Length >= 7 && request[5] == 0xE0)
                            {
                                response = S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion.S7_1200, rack: 0, slot: 0);
                            }
                            else if (request.Length >= 7 && request[5] == 0xF0 && request[7] == 0x32 && request[17] == 0xF0)
                            {
                                response = S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion.S7_1200, rack: 0, slot: 0);
                            }
                            else if (request.Length >= 19 && request[17] == 0x05)
                            {
                                var currentWrite = Interlocked.Increment(ref _writeRequestCount);
                                response = S7ResponseBuilder.CreateWriteResponse(request, new List<bool> { true, true });
                                if (currentWrite == 1)
                                {
                                    response = CorruptPduReference(response);
                                }
                            }
                            else
                            {
                                response = S7ResponseBuilder.CreateErrorResponse(request, 0x01);
                            }

                            await stream.WriteAsync(response, 0, response.Length, cancellationToken).ConfigureAwait(false);
                            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                    }
                }
            }

            private static async Task<byte[]> ReadFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
            {
                var header = await ReadExactAsync(stream, 4, cancellationToken).ConfigureAwait(false);
                if (header == null)
                {
                    return null;
                }

                int totalLength = (header[2] << 8) | header[3];
                if (totalLength < 4)
                {
                    return null;
                }

                var body = await ReadExactAsync(stream, totalLength - 4, cancellationToken).ConfigureAwait(false);
                if (body == null)
                {
                    return null;
                }

                var frame = new byte[totalLength];
                Buffer.BlockCopy(header, 0, frame, 0, 4);
                Buffer.BlockCopy(body, 0, frame, 4, body.Length);
                return frame;
            }

            private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
            {
                var buffer = new byte[length];
                int offset = 0;

                while (offset < length)
                {
                    var read = await stream.ReadAsync(buffer, offset, length - offset, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return null;
                    }

                    offset += read;
                }

                return buffer;
            }

            public void Dispose()
            {
                StopAsync().GetAwaiter().GetResult();
                _cts.Dispose();
            }
        }
    }
}

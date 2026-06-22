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
public class S7ProtocolSynchronizationTests
{
        private sealed class ForceBlockReadCommunication : S7Communication
        {
            public ForceBlockReadCommunication(S7EthernetTransport transport) : base(transport)
            {
            }

            internal override bool ShouldUseNativeRandomRead(S7BatchReadDispatchAnalysis decision)
            {
                decision.Mode = S7BatchReadPathKind.BlockRead;
                decision.DecisionReason = "测试强制走块读";
                return false;
            }
        }

        [Fact]
        public void S7ReadRequest_ShouldWriteSpecifiedPduReference()
        {
            var request = new S7ReadRequest("VB2000", 0, 1, false, 0x1234);

            Assert.Equal((byte)0x12, request.ProtocolMessageFrame[11]);
            Assert.Equal((byte)0x34, request.ProtocolMessageFrame[12]);
            Assert.Equal((ushort)0x1234, request.PduReference);
        }

        [Fact]
        public void S7WriteRequest_ShouldWriteSpecifiedPduReference()
        {
            var request = new S7WriteRequest("VB2000", 0, new byte[] { 0x5A }, false, 0x5678);

            Assert.Equal((byte)0x56, request.ProtocolMessageFrame[11]);
            Assert.Equal((byte)0x78, request.ProtocolMessageFrame[12]);
            Assert.Equal((ushort)0x5678, request.PduReference);
        }

        [Fact]
        public async Task UnicastReadMessageAsync_OnPduMismatch_ShouldFail()
        {
            var stream = new ResponseFactoryStreamResource(requestBytes =>
            {
                var response = S7ResponseBuilder.CreateReadResponse(requestBytes, new List<byte[]> { new byte[] { 0x11 } });
                return CorruptPduReference(response);
            });
            var transport = new S7EthernetTransport(stream);
            var request = new S7ReadRequest("VB2000", 0, 1, false, 0x0102);

            var result = await transport.UnicastReadMessageAsync(request).ConfigureAwait(false);

            Assert.False(result.IsSuccess);
            Assert.Contains("S7响应PDU Reference不匹配", result.Message);
            Assert.Equal(1, stream.SendCount);
        }

        [Fact]
        public async Task UnicastReadMessageAsync_OnMatchingPdu_ShouldSucceed()
        {
            var stream = new ResponseFactoryStreamResource(requestBytes =>
                S7ResponseBuilder.CreateReadResponse(requestBytes, new List<byte[]> { new byte[] { 0x22 } }));
            var transport = new S7EthernetTransport(stream);
            var request = new S7ReadRequest("VB2000", 0, 1, false, 0x0102);

            var result = await transport.UnicastReadMessageAsync(request).ConfigureAwait(false);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(1, stream.SendCount);
        }

        [Fact]
        public async Task UnicastReadMessageAsync_WhenStrictPduValidationDisabled_ShouldAllowMismatchedPdu()
        {
            var stream = new ResponseFactoryStreamResource(requestBytes =>
            {
                var response = S7ResponseBuilder.CreateReadResponse(requestBytes, new List<byte[]> { new byte[] { 0x33 } });
                return CorruptPduReference(response);
            });
            var transport = new S7EthernetTransport(stream)
            {
                StrictPduReferenceValidation = false
            };
            var request = new S7ReadRequest("VB2000", 0, 1, false, 0x0102);

            var result = await transport.UnicastReadMessageAsync(request).ConfigureAwait(false);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(1, stream.SendCount);
        }

        [Fact]
        public async Task BatchReadAsync_OnProtocolSynchronizationFailure_ShouldNotRetryBoundaryFallback()
        {
            var stream = new ResponseFactoryStreamResource(requestBytes =>
            {
                var response = S7ResponseBuilder.CreateReadResponse(requestBytes, new List<byte[]> { new byte[] { 0x12, 0x34 } });
                return CorruptPduReference(response);
            });
            var communication = new S7Communication(new S7EthernetTransport(stream));

            var result = await communication.BatchReadAsync(new Dictionary<string, DataTypeEnums>
            {
                ["VW2000"] = DataTypeEnums.UInt16
            }).ConfigureAwait(false);

            Assert.False(result.IsSuccess);
            Assert.Contains("S7响应PDU Reference不匹配", result.Message);
            Assert.Equal(1, stream.SendCount);
        }

        [Fact]
        public async Task BatchReadAsync_OnProtocolSynchronizationFailure_ShouldStopReadingFollowingBlocks()
        {
            var stream = new ResponseFactoryStreamResource(requestBytes =>
            {
                var response = S7ResponseBuilder.CreateReadResponse(requestBytes, new List<byte[]> { new byte[] { 0x11 } });
                return CorruptPduReference(response);
            });
            var communication = new ForceBlockReadCommunication(new S7EthernetTransport(stream));

            var result = await communication.BatchReadAsync(new Dictionary<string, DataTypeEnums>
            {
                ["VB2000"] = DataTypeEnums.Byte,
                ["VB2100"] = DataTypeEnums.Byte
            }).ConfigureAwait(false);

            Assert.False(result.IsSuccess);
            Assert.Contains("S7响应PDU Reference不匹配", result.Message);
            Assert.Equal(1, stream.SendCount);
            Assert.All(result.ResultValue.Values, item => Assert.Null(item.Item2));
        }

        [Fact]
        public async Task SiemensClient_BatchReadAsync_OnProtocolSynchronizationFailure_ShouldReconnectAndRetryWholeBatch()
        {
            using var server = new RetryOnceS7TestServer(new byte[] { 0x5A });
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

                var result = await client.BatchReadAsync(new Dictionary<string, DataTypeEnums>
                {
                    ["VB2000"] = DataTypeEnums.Byte
                }).ConfigureAwait(false);

                Assert.True(result.IsSuccess, result.Message);
                Assert.Equal((byte)0x5A, Assert.IsType<byte>(result.ResultValue["VB2000"].Item2));
                Assert.Equal(2, server.ReadRequestCount);
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

        private sealed class ResponseFactoryStreamResource : IStreamResource
        {
            private readonly Func<byte[], byte[]> _responseFactory;
            private byte[] _pendingResponse = Array.Empty<byte>();
            private int _pendingOffset;

            public ResponseFactoryStreamResource(Func<byte[], byte[]> responseFactory)
            {
                _responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
            }

            public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public bool Connected => true;

            public int SendCount { get; private set; }

            public Task<OperationResult<int>> Receive(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                if (_pendingResponse == null || _pendingOffset + length > _pendingResponse.Length)
                {
                    return Task.FromResult(OperationResult.CreateFailedResult<int>("没有可读取的响应数据"));
                }

                Buffer.BlockCopy(_pendingResponse, _pendingOffset, buffer, offset, length);
                _pendingOffset += length;
                return Task.FromResult(OperationResult.CreateSuccessResult(length));
            }

            public Task<OperationResult> Send(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                var request = new byte[length];
                Buffer.BlockCopy(buffer, offset, request, 0, length);
                _pendingResponse = _responseFactory(request);
                _pendingOffset = 0;
                SendCount++;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public void StreamClose()
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class RetryOnceS7TestServer : IDisposable
        {
            private readonly byte[] _successfulReadPayload;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly TcpListener _listener;
            private Task _acceptLoopTask;
            private int _readRequestCount;
            private int _connectionCount;

            public RetryOnceS7TestServer(byte[] successfulReadPayload)
            {
                _successfulReadPayload = successfulReadPayload ?? throw new ArgumentNullException(nameof(successfulReadPayload));
                _listener = new TcpListener(IPAddress.Loopback, 0);
            }

            public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

            public int ReadRequestCount => Volatile.Read(ref _readRequestCount);

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

                try
                {
                    _listener.Stop();
                }
                catch
                {
                }

                if (_acceptLoopTask != null)
                {
                    try
                    {
                        await _acceptLoopTask.ConfigureAwait(false);
                    }
                    catch
                    {
                    }
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
                            else if (request.Length >= 19 && request[17] == 0x04)
                            {
                                var currentRead = Interlocked.Increment(ref _readRequestCount);
                                response = S7ResponseBuilder.CreateReadResponse(request, new List<byte[]> { _successfulReadPayload });
                                if (currentRead == 1)
                                {
                                    response = CorruptPduReference(response);
                                }
                            }
                            else if (request.Length >= 19 && request[17] == 0x05)
                            {
                                response = S7ResponseBuilder.CreateWriteResponse(request, new List<bool> { true });
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

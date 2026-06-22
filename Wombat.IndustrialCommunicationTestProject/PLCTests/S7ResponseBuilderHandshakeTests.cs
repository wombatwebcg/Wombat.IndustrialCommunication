using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    public class S7ResponseBuilderHandshakeTests
    {
        [Fact]
        public void CreateConnectionResponse_ShouldReturnStandardCotpConnectionConfirm()
        {
            var request = (byte[])SiemensConstant.Command1.Clone();
            request[21] = 0x01;

            var response = S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion.S7_1200, 0, 1);

            Assert.NotNull(response);
            Assert.Equal(
                new byte[]
                {
                    0x03, 0x00, 0x00, 0x16,
                    0x11, 0xD0, 0x00, 0x01, 0x00, 0x00, 0x00,
                    0xC0, 0x01, 0x0A,
                    0xC1, 0x02, 0x01, 0x02,
                    0xC2, 0x02, 0x01, 0x01
                },
                response);
        }

        [Fact]
        public void CreateConnectionResponse_ShouldReturnStandardSetupCommunicationAckData()
        {
            var request = (byte[])SiemensConstant.Command2.Clone();

            var response = S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion.S7_1200, 0, 1);

            Assert.NotNull(response);
            Assert.Equal(
                new byte[]
                {
                    0x03, 0x00, 0x00, 0x1B,
                    0x02, 0xF0, 0x80,
                    0x32, 0x03, 0x00, 0x00, 0x04, 0x00,
                    0x00, 0x08, 0x00, 0x00, 0x00, 0x00,
                    0xF0, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0xE0
                },
                response);
        }

        [Fact]
        public void CreateConnectionResponse_ShouldCapSetupCommunicationNegotiatedPduLength()
        {
            var request = (byte[])SiemensConstant.Command2.Clone();
            request[23] = 0x08;
            request[24] = 0x00;

            var response = S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion.S7_1200, 0, 1);

            Assert.NotNull(response);
            Assert.Equal((byte)0x01, response[25]);
            Assert.Equal((byte)0xE0, response[26]);
        }

        [Fact]
        public async Task InitAsync_ShouldCaptureNegotiatedPduLength()
        {
            var handshake1 = S7ResponseBuilder.CreateConnectionResponse((byte[])SiemensConstant.Command1.Clone(), SiemensVersion.S7_1200, 0, 1);
            var handshake2Request = (byte[])SiemensConstant.Command2.Clone();
            handshake2Request[23] = 0x01;
            handshake2Request[24] = 0x00;
            var handshake2 = S7ResponseBuilder.CreateConnectionResponse(handshake2Request, SiemensVersion.S7_1200, 0, 1);

            using var stream = new SequencedResponseStreamResource(handshake1, handshake2);
            var communication = new S7Communication(new S7EthernetTransport(stream))
            {
                SiemensVersion = SiemensVersion.S7_1200
            };

            var result = await communication.InitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(0x0100, communication.NegotiatedPduLimit);
        }

        private sealed class SequencedResponseStreamResource : IStreamResource
        {
            private readonly byte[][] _responses;
            private int _sendIndex;
            private byte[] _pendingResponse = Array.Empty<byte>();
            private int _pendingOffset;

            public SequencedResponseStreamResource(params byte[][] responses)
            {
                _responses = responses ?? Array.Empty<byte[]>();
            }

            public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public bool Connected => true;

            public Task<OperationResult<int>> Receive(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                if (_pendingOffset + length > _pendingResponse.Length)
                {
                    return Task.FromResult(OperationResult.CreateFailedResult<int>("没有可读取的响应数据"));
                }

                Buffer.BlockCopy(_pendingResponse, _pendingOffset, buffer, offset, length);
                _pendingOffset += length;
                return Task.FromResult(OperationResult.CreateSuccessResult(length));
            }

            public Task<OperationResult> Send(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                if (_sendIndex >= _responses.Length)
                {
                    return Task.FromResult(OperationResult.CreateFailedResult("没有可发送的响应序列"));
                }

                _pendingResponse = _responses[_sendIndex++] ?? Array.Empty<byte>();
                _pendingOffset = 0;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public void StreamClose()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}

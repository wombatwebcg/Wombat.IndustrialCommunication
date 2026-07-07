using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.ConnectionPoolTests
{
    public class DeviceClientCancellationTokenTests
    {
        [Fact]
        public async Task ReadAsync_WithCancellationToken_ForwardsTokenToProtocolRead()
        {
            var client = new TokenProbeDeviceClient();
            using var cts = new CancellationTokenSource();

            var result = await client.ReadAsync(DataTypeEnums.Byte, "D0", cts.Token);

            Assert.True(result.IsSuccess);
            Assert.True(client.LastReadToken == cts.Token);
        }

        [Fact]
        public async Task WriteAsync_WithCancellationToken_ForwardsTokenToProtocolWrite()
        {
            var client = new TokenProbeDeviceClient();
            using var cts = new CancellationTokenSource();

            var result = await client.WriteAsync(DataTypeEnums.Byte, "D0", new object[] { (byte)1 }, cts.Token);

            Assert.True(result.IsSuccess);
            Assert.True(client.LastWriteToken == cts.Token);
        }

        private sealed class TokenProbeDeviceClient : DeviceDataReaderWriterBase, IDeviceClient
        {
            public TokenProbeDeviceClient()
                : base(new DeviceMessageTransport(new NoopStreamResource()))
            {
            }

            public CancellationToken LastReadToken { get; private set; }

            public CancellationToken LastWriteToken { get; private set; }

            public override string Version => nameof(TokenProbeDeviceClient);

            public ILogger Logger { get; set; }

            public bool IsLongConnection { get; set; }

            public bool Connected { get; private set; } = true;

            public int Retries { get; set; }

            public TimeSpan WaitToRetryMilliseconds { get; set; }

            public TimeSpan ConnectTimeout { get; set; }

            public TimeSpan ReceiveTimeout { get; set; }

            public TimeSpan SendTimeout { get; set; }

            public TimeSpan ResponseInterval { get; set; }

            public OperationResult Connect() => OperationResult.CreateSuccessResult();

            public OperationResult Disconnect() => OperationResult.CreateSuccessResult();

            public Task<OperationResult> ConnectAsync() => Task.FromResult(OperationResult.CreateSuccessResult());

            public Task<OperationResult> DisconnectAsync() => Task.FromResult(OperationResult.CreateSuccessResult());

            protected internal override ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, DataTypeEnums dataType, bool isBit = false)
            {
                return ReadAsync(address, length, dataType, isBit, CancellationToken.None);
            }

            protected internal override ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, DataTypeEnums dataType, bool isBit, CancellationToken cancellationToken)
            {
                LastReadToken = cancellationToken;
                return new ValueTask<OperationResult<byte[]>>(OperationResult.CreateSuccessResult(new byte[length]));
            }

            protected internal override Task<OperationResult> WriteAsync(string address, byte[] data, DataTypeEnums dataType, bool isBit = false)
            {
                return WriteAsync(address, data, dataType, isBit, CancellationToken.None);
            }

            protected internal override Task<OperationResult> WriteAsync(string address, byte[] data, DataTypeEnums dataType, bool isBit, CancellationToken cancellationToken)
            {
                LastWriteToken = cancellationToken;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }
        }

        private sealed class NoopStreamResource : IStreamResource
        {
            public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public bool Connected => true;

            public Task<OperationResult> ConnectAsync() => Task.FromResult(OperationResult.CreateSuccessResult());

            public Task<OperationResult> DisconnectAsync() => Task.FromResult(OperationResult.CreateSuccessResult());

            public void Dispose()
            {
            }

            public Task<OperationResult<int>> Receive(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult.CreateSuccessResult(0));
            }

            public Task<OperationResult> Send(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public void StreamClose()
            {
            }
        }
    }
}

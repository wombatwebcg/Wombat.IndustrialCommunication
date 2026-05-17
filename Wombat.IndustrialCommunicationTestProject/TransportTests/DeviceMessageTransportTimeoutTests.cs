using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.TransportTests
{
    public class DeviceMessageTransportTimeoutTests
    {
        [Fact]
        public async Task ReceiveResponseAsync_OnTimeout_ShouldNotCloseStream_AndNextReceiveCanSucceed()
        {
            var stream = new SequencedStreamResource();
            stream.EnqueueReceive(async cancellationToken =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                    return OperationResult.CreateSuccessResult(0);
                }
                catch (OperationCanceledException)
                {
                    return OperationResult.CreateFailedResult<int>("模拟超时");
                }
            });
            stream.EnqueueReceive(cancellationToken => Task.FromResult(OperationResult.CreateSuccessResult(2)));

            var transport = new DeviceMessageTransport(stream)
            {
                WaitToRetryMilliseconds = TimeSpan.Zero
            };
            stream.ReceiveTimeout = TimeSpan.FromMilliseconds(50);

            var timeoutResult = await transport.ReceiveResponseAsync(0, 2).ConfigureAwait(false);
            var successResult = await transport.ReceiveResponseAsync(0, 2).ConfigureAwait(false);

            Assert.False(timeoutResult.IsSuccess);
            Assert.Equal(0, stream.StreamCloseCallCount);
            Assert.True(successResult.IsSuccess, successResult.Message);
        }

        [Fact]
        public async Task SendRequestAsync_OnTimeout_ShouldNotCloseStream_AndNextSendCanSucceed()
        {
            var stream = new SequencedStreamResource();
            stream.EnqueueSend(async cancellationToken =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                    return OperationResult.CreateSuccessResult();
                }
                catch (OperationCanceledException)
                {
                    return OperationResult.CreateFailedResult("模拟超时");
                }
            });
            stream.EnqueueSend(cancellationToken => Task.FromResult(OperationResult.CreateSuccessResult()));

            var transport = new DeviceMessageTransport(stream)
            {
                WaitToRetryMilliseconds = TimeSpan.Zero
            };
            stream.SendTimeout = TimeSpan.FromMilliseconds(50);

            var timeoutResult = await transport.SendRequestAsync(new byte[] { 0x01, 0x02 }).ConfigureAwait(false);
            var successResult = await transport.SendRequestAsync(new byte[] { 0x03, 0x04 }).ConfigureAwait(false);

            Assert.False(timeoutResult.IsSuccess);
            Assert.Equal(0, stream.StreamCloseCallCount);
            Assert.True(successResult.IsSuccess, successResult.Message);
        }

        private sealed class SequencedStreamResource : IStreamResource
        {
            private readonly Queue<Func<CancellationToken, Task<OperationResult<int>>>> _receiveSteps = new Queue<Func<CancellationToken, Task<OperationResult<int>>>>();
            private readonly Queue<Func<CancellationToken, Task<OperationResult>>> _sendSteps = new Queue<Func<CancellationToken, Task<OperationResult>>>();

            public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public bool Connected
            {
                get { return true; }
            }

            public int StreamCloseCallCount { get; private set; }

            public void EnqueueReceive(Func<CancellationToken, Task<OperationResult<int>>> step)
            {
                _receiveSteps.Enqueue(step);
            }

            public void EnqueueSend(Func<CancellationToken, Task<OperationResult>> step)
            {
                _sendSteps.Enqueue(step);
            }

            public Task<OperationResult<int>> Receive(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                if (_receiveSteps.Count == 0)
                {
                    return Task.FromResult(OperationResult.CreateFailedResult<int>("没有配置接收步骤"));
                }

                return _receiveSteps.Dequeue()(cancellationToken);
            }

            public Task<OperationResult> Send(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                if (_sendSteps.Count == 0)
                {
                    return Task.FromResult(OperationResult.CreateFailedResult("没有配置发送步骤"));
                }

                return _sendSteps.Dequeue()(cancellationToken);
            }

            public void StreamClose()
            {
                StreamCloseCallCount++;
            }

            public void Dispose()
            {
            }
        }
    }
}

using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.TransportTests
{
    public class SerialPortAdapterLifecycleTests
    {
        [Fact]
        public async Task ConnectAsyncWithCancelledTokenDoesNotOpenPort()
        {
            using (var adapter = new SerialPortAdapter("PORT_THAT_MUST_NOT_BE_OPENED"))
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                var result = await adapter.ConnectAsync(cancellation.Token).ConfigureAwait(false);

                Assert.False(result.IsSuccess);
                Assert.True(result.IsCancelled);
                Assert.Equal(OperationFailureKind.Cancelled, result.FailureKind);
                Assert.False(adapter.Connected);
            }
        }

        [Fact]
        public async Task ConcurrentDisconnectIsIdempotent()
        {
            using (var adapter = new SerialPortAdapter("PORT_THAT_MUST_NOT_BE_OPENED"))
            {
                var results = await Task.WhenAll(adapter.DisconnectAsync(), adapter.DisconnectAsync(), adapter.DisconnectAsync()).ConfigureAwait(false);

                Assert.All(results, result => Assert.True(result.IsSuccess, result.Message));
                Assert.False(adapter.Connected);
            }
        }
    }
}

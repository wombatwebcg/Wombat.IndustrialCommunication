using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.TransportTests
{
    public class TcpClientAdapterTimeoutTests
    {
        [Fact]
        public async Task ReceiveTimeout_ShouldNotCloseConnection_AndNextRequestCanStillSucceed()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = RunServerAsync(listener);

            using (var adapter = new TcpClientAdapter("127.0.0.1", port))
            {
                adapter.ConnectTimeout = TimeSpan.FromSeconds(2);
                adapter.SendTimeout = TimeSpan.FromSeconds(2);
                adapter.ReceiveTimeout = TimeSpan.FromMilliseconds(150);

                var connectResult = await adapter.ConnectAsync().ConfigureAwait(false);
                Assert.True(connectResult.IsSuccess, connectResult.Message);

                var firstSend = await adapter.Send(CreateReadRequest(1, 0x15), 0, 12, CancellationToken.None).ConfigureAwait(false);
                Assert.True(firstSend.IsSuccess, firstSend.Message);

                var timeoutBuffer = new byte[11];
                var firstReceive = await adapter.Receive(timeoutBuffer, 0, timeoutBuffer.Length, CancellationToken.None).ConfigureAwait(false);
                Assert.False(firstReceive.IsSuccess);
                Assert.Contains("timed out", firstReceive.Message);
                Assert.True(adapter.Connected);

                var secondSend = await adapter.Send(CreateReadRequest(2, 0x16), 0, 12, CancellationToken.None).ConfigureAwait(false);
                Assert.True(secondSend.IsSuccess, secondSend.Message);

                var successBuffer = new byte[11];
                var secondReceive = await adapter.Receive(successBuffer, 0, successBuffer.Length, CancellationToken.None).ConfigureAwait(false);
                Assert.True(secondReceive.IsSuccess, secondReceive.Message);
                Assert.Equal("00 02 00 00 00 05 16 03 02 12 34", ToHex(successBuffer));
            }

            await serverTask.ConfigureAwait(false);
            listener.Stop();
        }

        private static async Task RunServerAsync(TcpListener listener)
        {
            using (var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false))
            using (var stream = client.GetStream())
            {
                var firstRequest = new byte[12];
                await ReadExactAsync(stream, firstRequest, CancellationToken.None).ConfigureAwait(false);

                var secondRequest = new byte[12];
                await ReadExactAsync(stream, secondRequest, CancellationToken.None).ConfigureAwait(false);

                var response = new byte[] { 0x00, 0x02, 0x00, 0x00, 0x00, 0x05, 0x16, 0x03, 0x02, 0x12, 0x34 };
                await stream.WriteAsync(response, 0, response.Length, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int currentRead = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, cancellationToken).ConfigureAwait(false);
                if (currentRead == 0)
                {
                    throw new InvalidOperationException("服务端读取请求时连接被关闭");
                }

                totalRead += currentRead;
            }
        }

        private static byte[] CreateReadRequest(byte transactionId, byte station)
        {
            return new byte[] { 0x00, transactionId, 0x00, 0x00, 0x00, 0x06, station, 0x03, 0x00, 0x00, 0x00, 0x01 };
        }

        private static string ToHex(byte[] data)
        {
            return string.Join(" ", Array.ConvertAll(data, b => b.ToString("X2")));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.TransportTests
{
    public class TcpServerAdapterFramingTests
    {
        [Fact]
        public async Task TcpServerAdapter_Should_Reassemble_S7_Tpkt_Split_Frame()
        {
            int port = GetFreePort();
            using var adapter = new TcpServerAdapter(IPAddress.Loopback.ToString(), port);
            var frames = new List<byte[]>();

            adapter.DataReceived += (sender, args) => frames.Add(args.Data);

            var listen = await adapter.ListenAsync().ConfigureAwait(false);
            Assert.True(listen.IsSuccess, listen.Message);

            byte[] frame = BuildS7Frame();
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            using var stream = client.GetStream();

            await stream.WriteAsync(frame, 0, 3).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            await Task.Delay(50).ConfigureAwait(false);
            await stream.WriteAsync(frame, 3, frame.Length - 3).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            await WaitUntilAsync(() => frames.Count == 1).ConfigureAwait(false);

            Assert.Equal(frame, frames[0]);
        }

        [Fact]
        public async Task TcpServerAdapter_Should_Split_Modbus_Sticky_Frames()
        {
            int port = GetFreePort();
            using var adapter = new TcpServerAdapter(IPAddress.Loopback.ToString(), port);
            var frames = new List<byte[]>();

            adapter.DataReceived += (sender, args) => frames.Add(args.Data);

            var listen = await adapter.ListenAsync().ConfigureAwait(false);
            Assert.True(listen.IsSuccess, listen.Message);

            byte[] frame1 = BuildModbusFrame(1);
            byte[] frame2 = BuildModbusFrame(2);
            byte[] merged = frame1.Concat(frame2).ToArray();

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            using var stream = client.GetStream();
            await stream.WriteAsync(merged, 0, merged.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            await WaitUntilAsync(() => frames.Count == 2).ConfigureAwait(false);

            Assert.Equal(frame1, frames[0]);
            Assert.Equal(frame2, frames[1]);
        }

        [Fact]
        public async Task TcpServerAdapter_Should_Keep_Same_Session_Instance_For_Events()
        {
            int port = GetFreePort();
            using var adapter = new TcpServerAdapter(IPAddress.Loopback.ToString(), port);
            INetworkSession connected = null;
            INetworkSession received = null;
            INetworkSession disconnected = null;

            adapter.ClientConnected += (sender, args) => connected = args.Session;
            adapter.DataReceived += (sender, args) => received = args.Session;
            adapter.ClientDisconnected += (sender, args) => disconnected = args.Session;

            var listen = await adapter.ListenAsync().ConfigureAwait(false);
            Assert.True(listen.IsSuccess, listen.Message);

            using (var client = new TcpClient())
            {
                await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                using var stream = client.GetStream();
                byte[] frame = BuildModbusFrame(3);
                await stream.WriteAsync(frame, 0, frame.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                await WaitUntilAsync(() => connected != null && received != null).ConfigureAwait(false);
            }

            await WaitUntilAsync(() => disconnected != null).ConfigureAwait(false);

            Assert.NotNull(connected);
            Assert.Same(connected, received);
            Assert.Same(connected, disconnected);
        }

        private static byte[] BuildS7Frame()
        {
            return new byte[]
            {
                0x03, 0x00, 0x00, 0x16,
                0x11, 0xE0, 0x00, 0x00, 0x00, 0x01,
                0x00, 0xC1, 0x02, 0x01, 0x00,
                0xC2, 0x02, 0x01, 0x02,
                0xC0, 0x01, 0x0A
            };
        }

        private static byte[] BuildModbusFrame(byte transactionId)
        {
            return new byte[]
            {
                0x00, transactionId,
                0x00, 0x00,
                0x00, 0x06,
                0x01,
                0x03,
                0x00, 0x00,
                0x00, 0x01
            };
        }

        private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.True(condition(), "Condition was not met before timeout.");
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}

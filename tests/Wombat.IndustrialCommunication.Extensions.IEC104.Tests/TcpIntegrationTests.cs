using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Extensions.IEC104;
using Wombat.IndustrialCommunication.Extensions.IEC104.Options;
using Wombat.IndustrialCommunication.Extensions.IEC104.Transport;
using Wombat.Network.Transports.Tcp;
using Xunit;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Tests
{
    /// <summary>服务端方向的链路，包一层 TcpTransportConnection，供测试用。</summary>
    internal sealed class TcpServerLink : IIec104Link
    {
        private readonly TcpTransportConnection _connection;
        private readonly PipeReader _reader;
        private readonly PipeWriter _writer;
        private int _closed;

        public TcpServerLink(TcpTransportConnection connection)
        {
            _connection = connection;
            _reader = connection.Transport.Input;
            _writer = connection.Transport.Output;
        }

        public bool IsConnected
        {
            get { return Volatile.Read(ref _closed) == 0; }
        }

        public EndPoint RemoteEndPoint
        {
            get { return _connection.RemoteEndPoint; }
        }

        public async Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var sequence = result.Buffer;

                if (sequence.Length == 0)
                {
                    _reader.AdvanceTo(sequence.Start);
                    if (result.IsCompleted) return 0;
                    continue;
                }

                var count = (int)Math.Min(buffer.Length, sequence.Length);
                sequence.Slice(0, count).CopyTo(new Span<byte>(buffer, 0, count));
                _reader.AdvanceTo(sequence.GetPosition(count));
                return count;
            }
        }

        public async Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            await _writer.WriteAsync(new ReadOnlyMemory<byte>(buffer), cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            try { _reader.Complete(); } catch { }
            try { _writer.Complete(); } catch { }
            try { _connection.CloseAsync().GetAwaiter().GetResult(); } catch { }
        }
    }

    /// <summary>TCP 链路之上的从站通道。</summary>
    internal sealed class TcpSlaveChannel : ISlaveChannel
    {
        private readonly TcpServerLink _link;

        public TcpSlaveChannel(TcpServerLink link)
        {
            _link = link;
        }

        public Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return _link.ReadAsync(buffer, cancellationToken);
        }

        public void Send(byte[] frame)
        {
            _link.WriteAsync(frame, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    public class TcpIntegrationTests
    {
        [Fact]
        public async Task RealSocket_InterrogationSucceeds()
        {
            var listener = new TcpTransportListener(new IPEndPoint(IPAddress.Loopback, 0), 4);
            await listener.StartAsync(CancellationToken.None);

            var endpoint = (IPEndPoint)listener.LocalEndPoint;
            var master = new Iec104Master(new Iec104MasterOptions
            {
                Host = "127.0.0.1",
                Port = endpoint.Port,
                ConnectTimeout = TimeSpan.FromSeconds(5),
                T1 = TimeSpan.FromSeconds(2),
                T2 = TimeSpan.FromSeconds(1),
                T3 = TimeSpan.FromSeconds(10),
                InterrogationTimeout = TimeSpan.FromSeconds(3),
            });

            try
            {
                var connecting = master.ConnectAsync();
                var connection = (TcpTransportConnection)await listener.AcceptAsync(CancellationToken.None);
                await connection.StartAsync(CancellationToken.None);

                var link = new TcpServerLink(connection);
                using (var slave = new FakeSlave(new TcpSlaveChannel(link), 1))
                {
                    slave.Start();
                    await connecting;

                    var result = await master.InterrogateAsync();

                    Assert.True(result.Completed);
                    Assert.Equal(3, result.ReceivedCount);
                }
            }
            finally
            {
                await master.DisposeAsync();
                try { await listener.CloseAsync(); } catch { }
                listener.Dispose();
            }
        }
    }
}

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Network.Transports.Tcp;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Transport
{
    /// <summary>
    /// 基于 Wombat.Network 的 TCP 链路。直接使用 IDuplexPipe，
    /// 不走按长度读满的适配器，因此允许一帧跨多次读、多帧粘在一次读。
    /// </summary>
    public sealed class Iec104TcpLink : IIec104Link
    {
        private readonly TcpTransportConnection _connection;
        private readonly PipeReader _reader;
        private readonly PipeWriter _writer;
        private int _closed;

        private Iec104TcpLink(TcpTransportConnection connection)
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

        /// <summary>建立 TCP 连接。</summary>
        public static async Task<Iec104TcpLink> ConnectAsync(
            IPEndPoint endPoint,
            TimeSpan connectTimeout,
            CancellationToken cancellationToken)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));

            using (var timeout = new CancellationTokenSource(connectTimeout))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
            {
                var connection = await TcpTransportConnection
                    .ConnectAsync(endPoint, cancellationToken: linked.Token)
                    .ConfigureAwait(false);

                try
                {
                    await connection.StartAsync(linked.Token).ConfigureAwait(false);
                }
                catch
                {
                    try { await connection.CloseAsync().ConfigureAwait(false); } catch { }
                    throw;
                }

                return new Iec104TcpLink(connection);
            }
        }

        /// <summary>从已接受的连接创建链路（从站使用）。</summary>
        public static Iec104TcpLink Accept(TcpTransportConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            return new Iec104TcpLink(connection);
        }

        public async Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

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
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

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
}

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Wombat.Network;
using Wombat.Network.Sockets;

namespace Wombat.IndustrialCommunication
{
    public class DeviceTcpClient:IDisposable
    {
        public TcpSocketClient _tcpSocketClient;
        TcpSocketClientConfiguration _tcpSocketClientConfiguration;
        ILogger _logger;
        private AsyncLock _asyncLock = new AsyncLock();
        public Channel<byte[]> _readChannel = Channel.CreateUnbounded<byte[]>();
        private bool disposedValue;


        public DeviceTcpClient(IPAddress address, int port = 9600):this(new IPEndPoint(address, port))
        {


        }

        public DeviceTcpClient(IPEndPoint _ipEndPoint) 
        {
            IpEndPoint = _ipEndPoint;
            _tcpSocketClientConfiguration = new TcpSocketClientConfiguration();
            _tcpSocketClientConfiguration.FrameBuilder = new RawBufferFrameBuilder();
            _tcpSocketClient = new TcpSocketClient(IpEndPoint, new TcpSocketClientEventDispatcher(_readChannel), _tcpSocketClientConfiguration);
        }

        #region Logger
        public void UseLogger(ILogger logger)
        {
            _logger = logger;
        }

        #endregion

        public IPEndPoint IpEndPoint { get; set; }
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(1);


        public async Task Connect()
        {
            using (var cancellationToken = new CancellationTokenSource(ConnectTimeout))
            using (await _asyncLock.LockAsync(cancellationToken.Token))
            {
                _tcpSocketClientConfiguration.ConnectTimeout = ConnectTimeout;
                _tcpSocketClientConfiguration.ReceiveTimeout = ReceiveTimeout;
                _tcpSocketClientConfiguration.SendTimeout = SendTimeout;
                await _tcpSocketClient.Connect();
            }
        }

        public async Task Close()
        {
            await _tcpSocketClient.Close();
        }

        public async Task Send(byte[] data, int offset, int count)
        {
            try
            {
                using (var cancellationToken = new CancellationTokenSource(SendTimeout))
                using (await _asyncLock.LockAsync(cancellationToken.Token))
                {

                        await _tcpSocketClient.SendAsync(data, offset, count, cancellationToken.Token);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex.Message);
            }
        }

        public async Task<byte[]> Read()
        {
            try
            {
                using (var cancellationToken = new CancellationTokenSource(ReceiveTimeout))
                using (await _asyncLock.LockAsync(cancellationToken.Token))
                {
                    await _readChannel.Reader.ReadAsync(cancellationToken.Token);

                }
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex.Message);
                return null;
            }
        }


        internal class TcpSocketClientEventDispatcher : ITcpSocketClientEventDispatcher
        {
            private Channel<byte[]> _readChannel;

            public TcpSocketClientEventDispatcher(Channel<byte[]> readChannel)
            {
                _readChannel = readChannel;
            }
            public async Task OnServerConnected(TcpSocketClient client)
            {
                await Task.CompletedTask;
            }

            public async Task OnServerDataReceived(TcpSocketClient client, byte[] data, int offset, int count)
            {
                await _readChannel.Writer.WriteAsync(data);
                await Task.CompletedTask;
            }

            public async Task OnServerDisconnected(TcpSocketClient client)
            {
                await Task.CompletedTask;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~DeviceTcpClient()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }


}

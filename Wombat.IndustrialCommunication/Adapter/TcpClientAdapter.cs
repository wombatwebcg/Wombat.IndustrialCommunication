using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.Network.Sockets;

namespace Wombat.IndustrialCommunication
{
    public class TcpClientAdapter : IStreamResource, IDisposable
    {
        private TcpSocketClientBase _tcpSocketClientBase;
        private readonly object _lockObject = new object();
        private bool _disposed;
        private const int DEFAULT_TIMEOUT_MS = 100;
        private const int MIN_PORT = 1;
        private const int MAX_PORT = 65535;

        public TcpClientAdapter(string ip, int port)
        {
            if (string.IsNullOrEmpty(ip))
                throw new ArgumentNullException(nameof(ip));
            
            if (port < MIN_PORT || port > MAX_PORT)
                throw new ArgumentOutOfRangeException(nameof(port), $"Port must be between {MIN_PORT} and {MAX_PORT}");

            if (!IPAddress.TryParse(ip, out IPAddress address))
            {
                try
                {
                    address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
                    if (address == null)
                        throw new ArgumentException($"Could not resolve host name: {ip}", nameof(ip));
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Invalid IP address or host name: {ip}", nameof(ip), ex);
                }
            }

            var ipEndPoint = new IPEndPoint(address, port);
            _tcpSocketClientBase = new TcpSocketClientBase(ipEndPoint);
        }

        public string Version => nameof(TcpClientAdapter);
        public ILogger Logger { get; private set; }
        public TimeSpan WaiteInterval { get; set; }
        public bool Connected => _tcpSocketClientBase?.Connected ?? false;

        public TimeSpan ConnectTimeout
        {
            get => _tcpSocketClientBase?.TcpSocketClientConfiguration.ConnectTimeout ?? TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
            set
            {
                if (_tcpSocketClientBase != null)
                {
                    _tcpSocketClientBase.TcpSocketClientConfiguration.ConnectTimeout = value;
                }
            }
        }

        public TimeSpan ReceiveTimeout
        {
            get => _tcpSocketClientBase?.TcpSocketClientConfiguration.ReceiveTimeout ?? TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
            set
            {
                if (_tcpSocketClientBase != null)
                {
                    _tcpSocketClientBase.TcpSocketClientConfiguration.ReceiveTimeout = value;
                }
            }
        }

        public TimeSpan SendTimeout
        {
            get => _tcpSocketClientBase?.TcpSocketClientConfiguration.SendTimeout ?? TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
            set
            {
                if (_tcpSocketClientBase != null)
                {
                    _tcpSocketClientBase.TcpSocketClientConfiguration.SendTimeout = value;
                }
            }
        }

        public async Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));

            if (!Connected)
                return OperationResult.CreateFailedResult(new InvalidOperationException("Not connected"));

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");

            OperationResult operation = new OperationResult();
            try
            {
                Logger?.LogDebug("Sending {Size} bytes to {EndPoint}", size, _tcpSocketClientBase.RemoteEndPoint);
                await _tcpSocketClientBase.SendAsync(buffer, offset, size, cancellationToken);
                Logger?.LogDebug("Successfully sent {Size} bytes", size);
                return operation.Complete();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error sending data");
                return OperationResult.CreateFailedResult(ex);
            }
        }

        public async Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));

            if (!Connected)
                return new OperationResult<int> { IsSuccess = false, Message = "Not connected" };

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");

            OperationResult<int> operation = new OperationResult<int>();
            try
            {
                Logger?.LogDebug("Receiving up to {Size} bytes from {EndPoint}", size, _tcpSocketClientBase.RemoteEndPoint);
                var count = await _tcpSocketClientBase.ReceiveAsync(buffer, offset, size, cancellationToken);
                operation.ResultValue = count;
                Logger?.LogDebug("Successfully received {Count} bytes", count);
                return operation.Complete();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error receiving data");
                return new OperationResult<int> { IsSuccess = false, Message = ex.Message, Exception = ex };
            }
        }

        public void UseLogger(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Logger.LogInformation("Logger configured for TcpClientAdapter");
        }

        public OperationResult Connect()
        {
            return ConnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public OperationResult Disconnect()
        {
            return DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> ConnectAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));

            try
            {
                Logger?.LogInformation("Connecting to {EndPoint}", _tcpSocketClientBase.RemoteEndPoint);
                OperationResult connect = new OperationResult();
                await _tcpSocketClientBase.ConnectAsync();
                connect.IsSuccess = _tcpSocketClientBase.Connected;
                
                if (connect.IsSuccess)
                    Logger?.LogInformation("Successfully connected to {EndPoint}", _tcpSocketClientBase.RemoteEndPoint);
                else
                    Logger?.LogWarning("Failed to connect to {EndPoint}", _tcpSocketClientBase.RemoteEndPoint);
                
                return connect.Complete();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error connecting to {EndPoint}", _tcpSocketClientBase.RemoteEndPoint);
                return OperationResult.CreateFailedResult(ex);
            }
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));

            try
            {
                Logger?.LogInformation("Disconnecting from {EndPoint}", _tcpSocketClientBase.RemoteEndPoint);
                OperationResult disConnect = new OperationResult();
                await _tcpSocketClientBase.Close();
                StreamClose();
                disConnect.IsSuccess = !_tcpSocketClientBase.Connected;
                
                if (disConnect.IsSuccess)
                    Logger?.LogInformation("Successfully disconnected from {EndPoint}", _tcpSocketClientBase.RemoteEndPoint);
                else
                    Logger?.LogWarning("Failed to disconnect from {EndPoint}", _tcpSocketClientBase.RemoteEndPoint);
                
                return disConnect.Complete();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error disconnecting from {EndPoint}", _tcpSocketClientBase.RemoteEndPoint);
                return OperationResult.CreateFailedResult(ex);
            }
        }

        public void StreamClose()
        {
            if (_disposed)
                return;

            try
            {
                Logger?.LogDebug("Closing stream");
                _tcpSocketClientBase.Shutdown();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error closing stream");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    Logger?.LogDebug("Disposing TcpClientAdapter");
                    DisposableUtility.Dispose(ref _tcpSocketClientBase);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Error during disposal");
                }
            }

            _disposed = true;
        }

        ~TcpClientAdapter()
        {
            Dispose(false);
        }
    }
}

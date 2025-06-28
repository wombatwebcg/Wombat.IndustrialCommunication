using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication
{

    public class TcpClientAdapter : IStreamResource, IDisposable
    {
        private TcpClient _tcpClient;
        private IPEndPoint _remoteEndPoint;
        private TimeSpan _connectTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        private TimeSpan _receiveTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        private TimeSpan _sendTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        private bool _disposed;
        private const int DEFAULT_TIMEOUT_MS = 2000;
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

            _remoteEndPoint = new IPEndPoint(address, port);
        }


      
        public bool Connected => _tcpClient?.Connected ?? false;

        public TimeSpan ConnectTimeout
        {
            get => _connectTimeout;
            set => _connectTimeout = value;
        }

        public TimeSpan ReceiveTimeout
        {
            get => _receiveTimeout;
            set => _receiveTimeout = value;
        }

        public TimeSpan SendTimeout
        {
            get => _sendTimeout;
            set => _sendTimeout = value;
        }



        /// <summary>
        /// 验证对象是否已释放
        /// </summary>
        private void ValidateNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpClientAdapter));
        }

        /// <summary>
        /// 验证是否已连接
        /// </summary>
        private void ValidateConnected()
        {
            if (!Connected)
                throw new InvalidOperationException("Not connected");
        }



        /// <summary>
        /// 设置NetworkStream的超时
        /// </summary>
        private void SetStreamTimeouts()
        {
            if (_tcpClient?.Connected == true)
            {
                var stream = _tcpClient.GetStream();
                stream.ReadTimeout = (int)_receiveTimeout.TotalMilliseconds;
                stream.WriteTimeout = (int)_sendTimeout.TotalMilliseconds;
            }
        }

        public async Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            ValidateNotDisposed();
            ValidateConnected();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");

            try
            {
                var stream = _tcpClient.GetStream();
                
                await stream.WriteAsync(buffer, offset, size, cancellationToken);
                
                return OperationResult.CreateSuccessResult();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return OperationResult.CreateFailedResult("Send operation was cancelled");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Send failed: {ex.Message}");
            }
        }

        public async Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            ValidateNotDisposed();
            ValidateConnected();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");

            try
            {
                var stream = _tcpClient.GetStream();
                
                int count = await stream.ReadAsync(buffer, offset, size, cancellationToken);
                
                return new OperationResult<int> { IsSuccess = true, ResultValue = count };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new OperationResult<int> { IsSuccess = false, Message = "Receive operation was cancelled" };
            }
            catch (Exception ex)
            {
                return new OperationResult<int> { IsSuccess = false, Message = $"Receive failed: {ex.Message}" };
            }
        }

        public OperationResult Connect()
        {
            ValidateNotDisposed();

            if (Connected)
                return OperationResult.CreateSuccessResult();

            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = (int)_receiveTimeout.TotalMilliseconds;
                _tcpClient.SendTimeout = (int)_sendTimeout.TotalMilliseconds;

                _tcpClient.Connect(_remoteEndPoint.Address, _remoteEndPoint.Port);

                // 连接成功后设置NetworkStream的超时
                SetStreamTimeouts();

                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Connection failed: {ex.Message}");
            }
        }

        public OperationResult Disconnect()
        {
            ValidateNotDisposed();

            try
            {
                _tcpClient?.Close();
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Disconnect failed: {ex.Message}");
            }
        }

        public async Task<OperationResult> ConnectAsync()
        {
            ValidateNotDisposed();

            if (Connected)
                return OperationResult.CreateSuccessResult();

            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = (int)_receiveTimeout.TotalMilliseconds;
                _tcpClient.SendTimeout = (int)_sendTimeout.TotalMilliseconds;

                await _tcpClient.ConnectAsync(_remoteEndPoint.Address, _remoteEndPoint.Port);
                
                // 连接成功后设置NetworkStream的超时
                SetStreamTimeouts();
                
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Connection failed: {ex.Message}");
            }
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            ValidateNotDisposed();

            try
            {
                _tcpClient?.Close();
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Disconnect failed: {ex.Message}");
            }
        }

        public void StreamClose()
        {
            if (_disposed)
                return;

            try
            {
                _tcpClient?.Close();
            }
            catch { }
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
                    _tcpClient?.Dispose();
                }
                catch { }
                finally
                {
                    _tcpClient = null;
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

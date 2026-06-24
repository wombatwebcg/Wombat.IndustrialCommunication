using Microsoft.Extensions.Logging;
using Pipelines.Sockets.Unofficial;
using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Network.Transports.Tcp;

namespace Wombat.IndustrialCommunication
{

    public class TcpClientAdapter : IStreamResource, IDisposable
    {
        private TcpTransportConnection _connection;
        private Stream _stream;
        private readonly IPEndPoint _remoteEndPoint;
        private TimeSpan _connectTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        private TimeSpan _receiveTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        private TimeSpan _sendTimeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        private int _connected;
        private bool _disposed;
        private const int DEFAULT_TIMEOUT_MS = 2000;
        private const int MIN_PORT = 1;
        private const int MAX_PORT = 65535;
        private ILogger _logger;

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


      
        public bool Connected => Volatile.Read(ref _connected) == 1;

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

        public ILogger Logger
        {
            get => _logger;
            set => _logger = value;
        }

        public bool EnableDebugLog
        {
            get;
            set;
        }

        /// <summary>
        /// IP地址
        /// </summary>
        public string IpAddress => _remoteEndPoint?.Address?.ToString();

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port => _remoteEndPoint?.Port ?? 0;



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



        public async Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            ValidateNotDisposed();
            ValidateConnected();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");

            CancellationTokenSource timeoutCts = null;
            CancellationTokenSource linkedCts = null;
            try
            {
                if (_sendTimeout > TimeSpan.Zero)
                {
                    timeoutCts = new CancellationTokenSource(_sendTimeout);
                    linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                }

                var effectiveToken = linkedCts?.Token ?? cancellationToken;
                await _stream.WriteAsync(buffer, offset, size, effectiveToken).ConfigureAwait(false);
                await _stream.FlushAsync(effectiveToken).ConfigureAwait(false);
                return OperationResult.CreateSuccessResult();
            }
            catch (OperationCanceledException) when (timeoutCts != null && timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return OperationResult.CreateFailedResult($"Send operation timed out after {_sendTimeout.TotalMilliseconds}ms");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return OperationResult.CreateFailedResult("Send operation was cancelled");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Send failed: {ex.Message}");
            }
            finally
            {
                linkedCts?.Dispose();
                timeoutCts?.Dispose();
            }
        }

        public async Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            DebugLog("[TcpClientAdapter调试] 开始接收数据: offset={Offset}, size={Size}", offset, size);
            DebugLog("[TcpClientAdapter调试] 连接状态: Connected={Connected}", Connected);
            DebugLog("[TcpClientAdapter调试] 取消令牌状态: IsCancellationRequested={IsCancellationRequested}", cancellationToken.IsCancellationRequested);
            DebugLog("[TcpClientAdapter调试] 接收超时设置: {ReceiveTimeout}ms", _receiveTimeout.TotalMilliseconds);
            
            ValidateNotDisposed();
            ValidateConnected();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");

            CancellationTokenSource timeoutCts = null;
            CancellationTokenSource linkedCts = null;
            try
            {
                DebugLog("[TcpClientAdapter调试] 获取传输流成功，流状态: CanRead={CanRead}, CanWrite={CanWrite}", _stream.CanRead, _stream.CanWrite);
                if (_receiveTimeout > TimeSpan.Zero)
                {
                    timeoutCts = new CancellationTokenSource(_receiveTimeout);
                    linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                }

                var effectiveToken = linkedCts?.Token ?? cancellationToken;
                int totalRead = 0;
                while (totalRead < size)
                {
                    int currentRead = await _stream.ReadAsync(buffer, offset + totalRead, size - totalRead, effectiveToken).ConfigureAwait(false);
                    DebugLog("[TcpClientAdapter调试] stream.ReadAsync 返回，读取字节数: {CurrentRead}", currentRead);

                    if (currentRead == 0)
                    {
                        DebugLog("[TcpClientAdapter调试] 没有读取到数据，可能连接已关闭");
                        return new OperationResult<int> { IsSuccess = false, Message = "Connection closed by remote host during receive" };
                    }

                    totalRead += currentRead;
                }

                DebugLog("[TcpClientAdapter调试] 读取完成，总字节数: {TotalRead}", totalRead);
                DebugLog("[TcpClientAdapter调试] 读取的数据: {Data}", string.Join(" ", buffer.Take(totalRead).Select(b => b.ToString("X2"))));

                return new OperationResult<int> { IsSuccess = true, ResultValue = totalRead };
            }
            catch (OperationCanceledException) when (timeoutCts != null && timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                DebugLog("[TcpClientAdapter调试] 接收操作超时，超时时间: {ReceiveTimeout}ms", _receiveTimeout.TotalMilliseconds);
                return new OperationResult<int> { IsSuccess = false, Message = $"Receive operation timed out after {_receiveTimeout.TotalMilliseconds}ms" };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                DebugLog("[TcpClientAdapter调试] 接收操作被外部取消");
                return new OperationResult<int> { IsSuccess = false, Message = "Receive operation was cancelled" };
            }
            catch (Exception ex)
            {
                DebugLog("[TcpClientAdapter调试] 接收数据时发生异常: {Message}", ex.Message);
                DebugLog("[TcpClientAdapter调试] 异常类型: {ExceptionType}", ex.GetType().Name);
                DebugLog("[TcpClientAdapter调试] 异常堆栈: {StackTrace}", ex.StackTrace);
                return new OperationResult<int> { IsSuccess = false, Message = $"Receive failed: {ex.Message}" };
            }
            finally
            {
                linkedCts?.Dispose();
                timeoutCts?.Dispose();
            }
        }

        private void DebugLog(string message, params object[] args)
        {
            if (!EnableDebugLog)
            {
                return;
            }

            Logger?.LogDebug(message, args);
        }

        public OperationResult Connect()
        {
            ValidateNotDisposed();

            if (Connected)
                return OperationResult.CreateSuccessResult();

            return ConnectAsync().GetAwaiter().GetResult();
        }

        public OperationResult Disconnect()
        {
            ValidateNotDisposed();

            try
            {
                CloseConnection();
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Disconnect failed: {ex.Message}");
            }
        }

        public async Task<OperationResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();

            if (Connected)
                return OperationResult.CreateSuccessResult();

            CancellationTokenSource timeoutCts = null;
            CancellationTokenSource combinedCts = null;
            try
            {
                timeoutCts = new CancellationTokenSource(_connectTimeout);
                combinedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                var connection = await TcpTransportConnection.ConnectAsync(
                    _remoteEndPoint,
                    cancellationToken: combinedCts.Token).ConfigureAwait(false);
                await connection.StartAsync(combinedCts.Token).ConfigureAwait(false);

                _connection = connection;
                _stream = StreamConnection.GetDuplex(connection.Transport, nameof(TcpClientAdapter));
                Volatile.Write(ref _connected, 1);
                return OperationResult.CreateSuccessResult();
            }
            catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
            {
                CloseConnection();
                return OperationResult.CreateFailedResult($"Connection timeout after {_connectTimeout.TotalMilliseconds}ms");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CloseConnection();
                return OperationResult.CreateFailedResult("Connection was cancelled by user");
            }
            catch (Exception ex)
            {
                CloseConnection();
                return OperationResult.CreateFailedResult($"Connection failed: {ex.Message}");
            }
            finally
            {
                combinedCts?.Dispose();
                timeoutCts?.Dispose();
            }
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            ValidateNotDisposed();

            try
            {
                CloseConnection();
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
                CloseConnection();
            }
            catch { }
        }

        private void CloseConnection()
        {
            Volatile.Write(ref _connected, 0);

            try
            {
                _stream?.Dispose();
            }
            catch { }
            finally
            {
                _stream = null;
            }

            try
            {
                _connection?.CloseAsync().GetAwaiter().GetResult();
            }
            catch { }
            finally
            {
                _connection = null;
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
                CloseConnection();
            }

            _disposed = true;
        }

        public void UseLogger(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        ~TcpClientAdapter()
        {
            Dispose(false);
        }
    }
}

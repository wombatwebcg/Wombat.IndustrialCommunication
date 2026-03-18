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
            Console.WriteLine($"[TcpClientAdapter调试] 开始接收数据: offset={offset}, size={size}");
            Console.WriteLine($"[TcpClientAdapter调试] 连接状态: Connected={Connected}");
            Console.WriteLine($"[TcpClientAdapter调试] 取消令牌状态: IsCancellationRequested={cancellationToken.IsCancellationRequested}");
            Console.WriteLine($"[TcpClientAdapter调试] 接收超时设置: {_receiveTimeout.TotalMilliseconds}ms");
            
            ValidateNotDisposed();
            ValidateConnected();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || size < 0 || offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Invalid offset or size");

            try
            {
                var stream = _tcpClient.GetStream();
                Console.WriteLine($"[TcpClientAdapter调试] 获取网络流成功，流状态: CanRead={stream.CanRead}, CanWrite={stream.CanWrite}");
                Console.WriteLine($"[TcpClientAdapter调试] 流超时设置: ReadTimeout={stream.ReadTimeout}ms, WriteTimeout={stream.WriteTimeout}ms");
                
                // 创建一个组合的取消令牌，包含外部取消令牌和超时
                using (var timeoutCts = new CancellationTokenSource(_receiveTimeout))
                using (var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
                {
                    Console.WriteLine($"[TcpClientAdapter调试] 开始调用 stream.ReadAsync，超时时间: {_receiveTimeout.TotalMilliseconds}ms");

                    int totalRead = 0;
                    while (totalRead < size)
                    {
                        int currentRead = await stream.ReadAsync(buffer, offset + totalRead, size - totalRead, combinedCts.Token);
                        Console.WriteLine($"[TcpClientAdapter调试] stream.ReadAsync 返回，读取字节数: {currentRead}");

                        if (currentRead == 0)
                        {
                            Console.WriteLine($"[TcpClientAdapter调试] 没有读取到数据，可能连接已关闭");
                            return new OperationResult<int> { IsSuccess = false, Message = "Connection closed by remote host during receive" };
                        }

                        totalRead += currentRead;
                    }

                    Console.WriteLine($"[TcpClientAdapter调试] 读取完成，总字节数: {totalRead}");
                    Console.WriteLine($"[TcpClientAdapter调试] 读取的数据: {string.Join(" ", buffer.Take(totalRead).Select(b => b.ToString("X2")))}");

                    return new OperationResult<int> { IsSuccess = true, ResultValue = totalRead };
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"[TcpClientAdapter调试] 接收操作被外部取消");
                return new OperationResult<int> { IsSuccess = false, Message = "Receive operation was cancelled" };
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[TcpClientAdapter调试] 接收操作超时，超时时间: {_receiveTimeout.TotalMilliseconds}ms");
                return new OperationResult<int> { IsSuccess = false, Message = $"Receive operation timed out after {_receiveTimeout.TotalMilliseconds}ms" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpClientAdapter调试] 接收数据时发生异常: {ex.Message}");
                Console.WriteLine($"[TcpClientAdapter调试] 异常类型: {ex.GetType().Name}");
                Console.WriteLine($"[TcpClientAdapter调试] 异常堆栈: {ex.StackTrace}");
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

        public async Task<OperationResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            ValidateNotDisposed();

            if (Connected)
                return OperationResult.CreateSuccessResult();

            CancellationTokenSource timeoutCts = null;
            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = (int)_receiveTimeout.TotalMilliseconds;
                _tcpClient.SendTimeout = (int)_sendTimeout.TotalMilliseconds;

                // 创建超时 CancellationTokenSource
                timeoutCts = new CancellationTokenSource(_connectTimeout);
                
                // 合并超时 token 和传入的 cancellationToken
                var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                // 使用 Task.WhenAny 实现超时控制
                var connectTask = _tcpClient.ConnectAsync(_remoteEndPoint.Address, _remoteEndPoint.Port);
                var timeoutTask = Task.Delay(Timeout.Infinite, combinedCts.Token);
                
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    // 超时发生
                    throw new OperationCanceledException("Connection timeout");
                }
                
                // 等待连接任务完成（应该已经完成）
                await connectTask;
                
                // 连接成功后设置NetworkStream的超时
                SetStreamTimeouts();
                
                return OperationResult.CreateSuccessResult();
            }
            catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
            {
                return OperationResult.CreateFailedResult($"Connection timeout after {_connectTimeout.TotalMilliseconds}ms");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return OperationResult.CreateFailedResult("Connection was cancelled by user");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Connection failed: {ex.Message}");
            }
            finally
            {
                timeoutCts?.Dispose();
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

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS以太网传输层
    /// </summary>
    public class FinsEthernetTransport : DeviceMessageTransport
    {
        private Socket _socket;
        private IPEndPoint _endPoint;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private bool _isConnected = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ipAddress">IP地址</param>
        /// <param name="port">端口号</param>
        /// <param name="timeout">超时时间</param>
        public FinsEthernetTransport(string ipAddress, int port = 9600, TimeSpan? timeout = null)
            : base(new TcpClientAdapter(ipAddress, port))
        {
            IpAddress = ipAddress;
            Port = port;
            Timeout = timeout ?? TimeSpan.FromSeconds(5);
            ConnectTimeout = timeout ?? TimeSpan.FromSeconds(5);
            ReceiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
            SendTimeout = timeout ?? TimeSpan.FromSeconds(5);
            _endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
        }

        /// <summary>
        /// IP地址
        /// </summary>
        public string IpAddress { get; }

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// 超时时间
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; }

        /// <summary>
        /// 接收超时时间
        /// </summary>
        public TimeSpan ReceiveTimeout { get; set; }

        /// <summary>
        /// 发送超时时间
        /// </summary>
        public TimeSpan SendTimeout { get; set; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public new bool IsConnected => _isConnected && _socket?.Connected == true;

        /// <summary>
        /// 连接到设备
        /// </summary>
        /// <returns>连接结果</returns>
        public  async Task<OperationResult> ConnectAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (IsConnected)
                {
                    return OperationResult.CreateSuccessResult("已连接");
                }

                try
                {
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        ReceiveTimeout = (int)Timeout.TotalMilliseconds,
                        SendTimeout = (int)Timeout.TotalMilliseconds
                    };

                    // 使用超时控制连接
                    using (var cts = new CancellationTokenSource(ConnectTimeout))
                    {
                        await _socket.ConnectAsync(_endPoint).ConfigureAwait(false);
                    }

                    _isConnected = true;
                    return OperationResult.CreateSuccessResult("连接成功");
                }
                catch (SocketException ex)
                {
                    _isConnected = false;
                    return OperationResult.CreateFailedResult($"Socket连接失败: {ex.Message}");
                }
                catch (TimeoutException ex)
                {
                    _isConnected = false;
                    return OperationResult.CreateFailedResult($"连接超时: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _isConnected = false;
                    return OperationResult.CreateFailedResult($"连接异常: {ex.Message}");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns>断开连接结果</returns>
        public  async Task<OperationResult> DisconnectAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_socket != null)
                {
                    try
                    {
                        if (_socket.Connected)
                        {
                            _socket.Shutdown(SocketShutdown.Both);
                        }
                    }
                    catch
                    {
                        // 忽略关闭异常
                    }
                    finally
                    {
                        _socket.Close();
                        _socket.Dispose();
                        _socket = null;
                        _isConnected = false;
                    }
                }

                return OperationResult.CreateSuccessResult("断开连接成功");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 发送请求数据
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <returns>发送结果</returns>
        public new async Task<OperationResult> SendRequestAsync(byte[] data)
        {
            if (!IsConnected)
            {
                return OperationResult.CreateFailedResult("未连接到设备");
            }

            try
            {
                using (var cts = new CancellationTokenSource(Timeout))
                {
                    int totalSent = 0;
                    while (totalSent < data.Length)
                    {
                        int sent = await _socket.SendAsync(
                            new ArraySegment<byte>(data, totalSent, data.Length - totalSent),
                            SocketFlags.None).ConfigureAwait(false);
                        
                        if (sent == 0)
                        {
                            return OperationResult.CreateFailedResult("发送数据失败，连接已断开");
                        }
                        
                        totalSent += sent;
                    }
                }

                return OperationResult.CreateSuccessResult("数据发送成功");
            }
            catch (SocketException ex)
            {
                _isConnected = false;
                return OperationResult.CreateFailedResult($"Socket发送异常: {ex.Message}");
            }
            catch (TimeoutException ex)
            {
                return OperationResult.CreateFailedResult($"发送超时: {ex.Message}");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"发送异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 接收响应数据
        /// </summary>
        /// <param name="offset">偏移量</param>
        /// <param name="length">数据长度</param>
        /// <returns>接收到的数据</returns>
        public new async Task<OperationResult<byte[]>> ReceiveResponseAsync(int offset, int length)
        {
            if (!IsConnected)
            {
                return OperationResult.CreateFailedResult<byte[]>("未连接到设备");
            }

            try
            {
                var buffer = new byte[length];
                using (var cts = new CancellationTokenSource(Timeout))
                {
                    int totalReceived = 0;
                    while (totalReceived < length)
                    {
                        int received = await _socket.ReceiveAsync(
                            new ArraySegment<byte>(buffer, totalReceived, length - totalReceived),
                            SocketFlags.None).ConfigureAwait(false);
                        
                        if (received == 0)
                        {
                            return OperationResult.CreateFailedResult<byte[]>("接收数据失败，连接已断开");
                        }
                        
                        totalReceived += received;
                    }
                }

                return OperationResult.CreateSuccessResult(buffer);
            }
            catch (SocketException ex)
            {
                _isConnected = false;
                return OperationResult.CreateFailedResult<byte[]>($"Socket接收异常: {ex.Message}");
            }
            catch (TimeoutException ex)
            {
                return OperationResult.CreateFailedResult<byte[]>($"接收超时: {ex.Message}");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<byte[]>($"接收异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送数据并接收响应
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <param name="responseLength">期望的响应长度</param>
        /// <returns>响应数据</returns>
        public async Task<OperationResult<byte[]>> SendAndReceiveAsync(byte[] data, int responseLength)
        {
            var sendResult = await SendRequestAsync(data);
            if (!sendResult.IsSuccess)
            {
                return OperationResult.CreateFailedResult<byte[]>(sendResult.Message);
            }

            return await ReceiveResponseAsync(0, responseLength);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisconnectAsync().Wait(1000); // 等待最多1秒
                _semaphore?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
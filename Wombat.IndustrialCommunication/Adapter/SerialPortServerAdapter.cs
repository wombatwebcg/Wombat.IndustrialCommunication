using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 串口服务器适配器，提供串口通信的服务器端功能
    /// </summary>
    public class SerialPortServerAdapter : IStreamResource, IDisposable
    {
        #region 字段和属性

        private SerialPort _serialPort;
        private CancellationTokenSource _cts;
        private Task _listeningTask;
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly byte[] _receiveBuffer = new byte[4096];
        private const int DEFAULT_TIMEOUT_MS = 3000;
        private bool _disposed;
        private bool _isListening;

        /// <summary>
        /// 串口名称
        /// </summary>
        public string PortName { get; set; }

        /// <summary>
        /// 波特率
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// 数据位
        /// </summary>
        public int DataBits { get; set; } = 8;

        /// <summary>
        /// 停止位
        /// </summary>
        public StopBits StopBits { get; set; } = StopBits.One;

        /// <summary>
        /// 校验位
        /// </summary>
        public Parity Parity { get; set; } = Parity.None;

        /// <summary>
        /// 握手协议
        /// </summary>
        public Handshake Handshake { get; set; } = Handshake.None;

        /// <summary>
        /// 版本信息
        /// </summary>
        public string Version => nameof(SerialPortServerAdapter);

        /// <summary>
        /// 日志记录器
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// 等待间隔
        /// </summary>
        public TimeSpan WaitInterval { get; set; } = TimeSpan.FromMilliseconds(20);

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool Connected => _serialPort?.IsOpen ?? false;

        /// <summary>
        /// 是否正在监听
        /// </summary>
        public bool IsListening => _isListening;

        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);

        /// <summary>
        /// 接收超时时间
        /// </summary>
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);

        /// <summary>
        /// 发送超时时间
        /// </summary>
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);

        /// <summary>
        /// 接收到的数据处理事件
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <summary>
        /// 客户端连接事件（在串口中，表示检测到客户端请求）
        /// </summary>
        public event EventHandler<SessionEventArgs> ClientConnected;

        /// <summary>
        /// 客户端断开事件（在串口中，表示客户端无响应或断开）
        /// </summary>
        public event EventHandler<SessionEventArgs> ClientDisconnected;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="portName">串口名称</param>
        public SerialPortServerAdapter(string portName)
        {
            PortName = portName;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <param name="handshake">握手协议</param>
        public SerialPortServerAdapter(string portName, int baudRate, int dataBits, StopBits stopBits, Parity parity, Handshake handshake)
        {
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            StopBits = stopBits;
            Parity = parity;
            Handshake = handshake;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 启用日志记录器
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public void UseLogger(ILogger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// 启动监听
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> ListenAsync()
        {
            if (_isListening)
                return new OperationResult { IsSuccess = true, Message = "已在监听" };

            try
            {
                // 初始化串口
                _serialPort = new SerialPort
                {
                    PortName = PortName,
                    BaudRate = BaudRate,
                    DataBits = DataBits,
                    StopBits = StopBits,
                    Parity = Parity,
                    Handshake = Handshake,
                    ReadTimeout = (int)ReceiveTimeout.TotalMilliseconds,
                    WriteTimeout = (int)SendTimeout.TotalMilliseconds
                };

                // 打开串口
                _serialPort.Open();
                _isListening = true;

                // 启动监听任务
                _cts = new CancellationTokenSource();
                _listeningTask = Task.Run(() => ListeningLoopAsync(_cts.Token));

                Logger?.LogInformation($"SerialPortServerAdapter started listening on {PortName}");
                return new OperationResult { IsSuccess = true };
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"SerialPortServerAdapter failed to start listening on {PortName}");
                _isListening = false;

                try
                {
                    _serialPort?.Close();
                    _serialPort?.Dispose();
                    _serialPort = null;
                }
                catch { }

                return new OperationResult { IsSuccess = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> ShutdownAsync()
        {
            if (!_isListening)
                return new OperationResult { IsSuccess = true, Message = "未在监听" };

            try
            {
                // 取消监听任务
                _cts?.Cancel();
                if (_listeningTask != null)
                {
                    try
                    {
                        await _listeningTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // 预期的取消异常
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Error during listening task shutdown");
                    }
                    _listeningTask = null;
                }

                // 关闭串口
                try
                {
                    if (_serialPort?.IsOpen == true)
                    {
                        _serialPort.Close();
                    }
                    _serialPort?.Dispose();
                    _serialPort = null;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Error closing serial port");
                }

                _isListening = false;
                Logger?.LogInformation($"SerialPortServerAdapter stopped listening on {PortName}");
                return new OperationResult { IsSuccess = true };
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"SerialPortServerAdapter failed to stop listening on {PortName}");
                return new OperationResult { IsSuccess = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 向客户端发送数据
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> SendAsync(byte[] data)
        {
            if (!_isListening || _serialPort == null || !_serialPort.IsOpen)
                return new OperationResult { IsSuccess = false, Message = "串口未打开" };

            try
            {
                using (await _lock.LockAsync())
                {
                    await Task.Run(() => _serialPort.Write(data, 0, data.Length));
                    Logger?.LogDebug($"Sent {data.Length} bytes via {PortName}");
                    return new OperationResult { IsSuccess = true };
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"Error sending data via {PortName}");
                return new OperationResult { IsSuccess = false, Message = ex.Message };
            }
        }

        #endregion

        #region IStreamResource接口实现

        /// <summary>
        /// 从串口接收数据
        /// </summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="length">长度</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>接收到的数据长度的操作结果</returns>
        public async Task<OperationResult<int>> Receive(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            if (!_isListening || _serialPort == null || !_serialPort.IsOpen)
                return OperationResult.CreateFailedResult<int>("串口未打开");

            try
            {
                using (await _lock.LockAsync())
                {
                    int bytesRead = await Task.Run(() =>
                    {
                        try
                        {
                            return _serialPort.Read(buffer, offset, Math.Min(_serialPort.BytesToRead, length));
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, $"从{PortName}读取数据时发生错误");
                            throw;
                        }
                    }, cancellationToken);

                    Logger?.LogDebug($"从{PortName}接收到{bytesRead}字节");
                    return OperationResult.CreateSuccessResult(bytesRead);
                }
            }
            catch (OperationCanceledException)
            {
                return OperationResult.CreateFailedResult<int>("操作已取消");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"从{PortName}接收数据时发生错误");
                return OperationResult.CreateFailedResult<int>(ex.Message);
            }
        }

        /// <summary>
        /// 向串口发送数据
        /// </summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="length">长度</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> Send(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            if (!_isListening || _serialPort == null || !_serialPort.IsOpen)
                return OperationResult.CreateFailedResult("串口未打开");

            try
            {
                using (await _lock.LockAsync())
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            _serialPort.Write(buffer, offset, length);
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogError(ex, $"向{PortName}发送数据时发生错误");
                            throw;
                        }
                    }, cancellationToken);

                    Logger?.LogDebug($"向{PortName}发送了{length}字节");
                    return OperationResult.CreateSuccessResult();
                }
            }
            catch (OperationCanceledException)
            {
                return OperationResult.CreateFailedResult("操作已取消");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"向{PortName}发送数据时发生错误");
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        /// <summary>
        /// 关闭流
        /// </summary>
        public void StreamClose()
        {
            if (_disposed)
                return;

            try
            {
                Logger?.LogDebug("关闭串口流");
                ShutdownAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "关闭串口流时发生错误");
            }
        }

        /// <summary>
        /// 检测连接的健康状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>连接是否健康的操作结果</returns>
        public async Task<OperationResult<bool>> IsConnectionHealthyAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return OperationResult.CreateFailedResult<bool>("适配器已释放");

            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    return OperationResult.CreateSuccessResult(true);
                }
                else
                {
                    return OperationResult.CreateSuccessResult(false);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "检测串口连接健康状态时发生错误");
                return OperationResult.CreateFailedResult<bool>(ex.Message);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 监听循环
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ListeningLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 检查是否有数据可读
                    if (_serialPort.BytesToRead > 0)
                    {
                        int bytesRead = await Task.Run(() =>
                        {
                            // 模拟会话对象
                            var sessionId = Guid.NewGuid();
                            var session = new SerialPortSession(sessionId, this);
                            
                            // 触发客户端连接事件
                            ClientConnected?.Invoke(this, new SessionEventArgs(session));
                            
                            try
                            {
                                int count = _serialPort.Read(_receiveBuffer, 0, Math.Min(_serialPort.BytesToRead, _receiveBuffer.Length));
                                if (count > 0)
                                {
                                    byte[] data = new byte[count];
                                    Array.Copy(_receiveBuffer, data, count);
                                    
                                    // 触发数据接收事件
                                    DataReceived?.Invoke(this, new DataReceivedEventArgs(session, data));
                                }
                                return count;
                            }
                            catch
                            {
                                // 触发客户端断开事件
                                ClientDisconnected?.Invoke(this, new SessionEventArgs(session));
                                throw;
                            }
                        }, cancellationToken);

                        Logger?.LogDebug($"Received {bytesRead} bytes via {PortName}");
                    }

                    // 等待一小段时间再检查
                    await Task.Delay(WaitInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // 取消操作，跳出循环
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Logger?.LogError(ex, $"Error in listening loop for {PortName}");
                        
                        // 尝试恢复串口
                        try
                        {
                            if (_serialPort != null)
                            {
                                if (_serialPort.IsOpen)
                                {
                                    _serialPort.Close();
                                }
                                _serialPort.Open();
                            }
                        }
                        catch (Exception reopenEx)
                        {
                            Logger?.LogError(reopenEx, $"Failed to reopen {PortName}");
                            break; // 无法恢复，退出循环
                        }
                    }
                }
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    ShutdownAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    _cts?.Dispose();
                    _cts = null;
                }

                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// 串口会话，模拟网络会话
    /// </summary>
    public class SerialPortSession : INetworkSession
    {
        private readonly SerialPortServerAdapter _adapter;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="id">会话ID</param>
        /// <param name="adapter">适配器</param>
        public SerialPortSession(Guid id, SerialPortServerAdapter adapter)
        {
            Id = id;
            _adapter = adapter;
        }

        /// <summary>
        /// 会话ID
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// 关闭会话
        /// </summary>
        public void Close()
        {
            // 串口会话不需要特别关闭，保持接口一致性
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> SendAsync(byte[] data)
        {
            return await _adapter.SendAsync(data);
        }
    }
} 
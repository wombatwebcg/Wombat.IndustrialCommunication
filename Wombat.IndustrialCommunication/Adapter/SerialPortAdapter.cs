using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;


namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 串口通信适配器，提供串口通信的基本功能
    /// </summary>
    public class SerialPortAdapter : IStreamResource, IDisposable
    {
        #region Properties

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
        public string Version => nameof(SerialPortAdapter);

        /// <summary>
        /// 日志记录器
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// 等待间隔
        /// </summary>
        public TimeSpan WaiteInterval { get; set; }

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool Connected => _serialPort?.IsOpen ?? false;

        /// <summary>
        /// 可读取的字节数
        /// </summary>
        public int BytesToRead => _serialPort?.BytesToRead ?? 0;

        /// <summary>
        /// 待写入的字节数
        /// </summary>
        public int BytesToWrite => _serialPort?.BytesToWrite ?? 0;

        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; }

        /// <summary>
        /// 接收超时时间
        /// </summary>
        public TimeSpan ReceiveTimeout
        {
            get => _serialPort != null ? TimeSpan.FromMilliseconds(_serialPort.ReadTimeout) : TimeSpan.FromMilliseconds(3000);
            set
            {
                if (_serialPort != null)
                {
                    _serialPort.ReadTimeout = (int)value.TotalMilliseconds;
                }
            }
        }

        /// <summary>
        /// 发送超时时间
        /// </summary>
        public TimeSpan SendTimeout
        {
            get => _serialPort != null ? TimeSpan.FromMilliseconds(_serialPort.WriteTimeout) : TimeSpan.FromMilliseconds(3000);
            set
            {
                if (_serialPort != null)
                {
                    _serialPort.WriteTimeout = (int)value.TotalMilliseconds;
                }
            }
        }

        #endregion

        #region Private Fields

        private readonly AsyncLock _lock = new AsyncLock();
        private SerialPort _serialPort;
        private bool _disposed;

        #endregion

        #region Constructor

        /// <summary>
        /// 初始化串口适配器
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <param name="handshake">握手协议</param>
        public SerialPortAdapter(
            string portName,
            int baudRate = 9600,
            int dataBits = 8,
            StopBits stopBits = StopBits.One,
            Parity parity = Parity.None,
            Handshake handshake = Handshake.None)
        {
            PortName = portName ?? throw new ArgumentNullException(nameof(portName));
            BaudRate = baudRate;
            DataBits = dataBits;
            Handshake = handshake;
            Parity = parity;
            StopBits = stopBits;

            _serialPort = new SerialPort
            {
                WriteTimeout = 3000,
                ReadTimeout = 3000
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 发送数据
        /// </summary>
        public async Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SerialPortAdapter));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (offset + size > buffer.Length) throw new ArgumentException("Invalid offset or size");

            using (await _lock.LockAsync())
            {
                try
                {
                    Logger?.LogDebug("正在向串口 {PortName} 发送 {Size} 字节数据", PortName, size);
                    await _serialPort.BaseStream.WriteAsync(buffer, offset, size, cancellationToken);
                    Logger?.LogDebug("成功向串口 {PortName} 发送 {Size} 字节数据", PortName, size);
                    return new OperationResult().Complete();
                }
                catch (TimeoutException te)
                {
                    string errorMessage = $"向串口 {PortName} 发送数据超时，超时设置: {SendTimeout.TotalMilliseconds}ms";
                    Logger?.LogError(te, errorMessage);
                    return OperationResult.CreateFailedResult(errorMessage);
                }
                catch (InvalidOperationException ioe)
                {
                    string errorMessage = $"向串口 {PortName} 发送数据时操作无效: {ioe.Message}";
                    Logger?.LogError(ioe, errorMessage);
                    return OperationResult.CreateFailedResult(errorMessage);
                }
                catch (Exception ex)
                {
                    string errorMessage = $"向串口 {PortName} 发送数据时发生错误: {ex.Message}";
                    Logger?.LogError(ex, errorMessage);
                    return OperationResult.CreateFailedResult(errorMessage);
                }
            }
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        public async Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SerialPortAdapter));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (offset + size > buffer.Length) throw new ArgumentException("Invalid offset or size");

            using (await _lock.LockAsync())
            {
                try
                {
                    Logger?.LogDebug("正在从串口 {PortName} 接收最多 {Size} 字节数据", PortName, size);
                    var count = await _serialPort.BaseStream.ReadAsync(buffer, offset, size, cancellationToken);
                    Logger?.LogDebug("成功从串口 {PortName} 接收 {Count} 字节数据", PortName, count);
                    return new OperationResult<int> { ResultValue = count }.Complete();
                }
                catch (TimeoutException te)
                {
                    string errorMessage = $"从串口 {PortName} 接收数据超时，超时设置: {ReceiveTimeout.TotalMilliseconds}ms";
                    Logger?.LogError(te, errorMessage);
                    return new OperationResult<int> { IsSuccess = false, Message = errorMessage, Exception = te };
                }
                catch (InvalidOperationException ioe)
                {
                    string errorMessage = $"从串口 {PortName} 接收数据时操作无效: {ioe.Message}";
                    Logger?.LogError(ioe, errorMessage);
                    return new OperationResult<int> { IsSuccess = false, Message = errorMessage, Exception = ioe };
                }
                catch (Exception ex)
                {
                    string errorMessage = $"从串口 {PortName} 接收数据时发生错误: {ex.Message}";
                    Logger?.LogError(ex, errorMessage);
                    return new OperationResult<int> { IsSuccess = false, Message = errorMessage, Exception = ex };
                }
            }
        }

        /// <summary>
        /// 连接串口
        /// </summary>
        public OperationResult Connect() => ConnectAsync().GetAwaiter().GetResult();

        /// <summary>
        /// 断开串口连接
        /// </summary>
        public OperationResult Disconnect() => DisconnectAsync().GetAwaiter().GetResult();

        /// <summary>
        /// 异步连接串口
        /// </summary>
        public async Task<OperationResult> ConnectAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SerialPortAdapter));

            var result = new OperationResult();
            try
            {
                _serialPort.PortName = PortName;
                _serialPort.BaudRate = BaudRate;
                _serialPort.Parity = Parity;
                _serialPort.DataBits = DataBits;
                _serialPort.StopBits = StopBits;
                _serialPort.Handshake = Handshake;

                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                _serialPort.Open();
                result.IsSuccess = _serialPort.IsOpen;
                
                if (result.IsSuccess)
                {
                    Logger?.LogInformation("Successfully connected to serial port {PortName}", PortName);
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Logger?.LogError(ex, "Failed to connect to serial port {PortName}", PortName);
            }
            return result;
        }

        /// <summary>
        /// 异步断开串口连接
        /// </summary>
        public Task<OperationResult> DisconnectAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SerialPortAdapter));

            var result = new OperationResult();
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                    Logger?.LogInformation("Successfully disconnected from serial port {PortName}", PortName);
                }
                result.IsSuccess = !_serialPort.IsOpen;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                Logger?.LogError(ex, "Failed to disconnect from serial port {PortName}", PortName);
            }
            return Task.FromResult(result);
        }

        /// <summary>
        /// 关闭流
        /// </summary>
        public void StreamClose()
        {
            if (_serialPort?.IsOpen ?? false)
            {
                _serialPort.BaseStream.Close();
                Logger?.LogInformation("Stream closed for serial port {PortName}", PortName);
            }
        }


        /// <summary>
        /// 设置日志记录器
        /// </summary>
        public void UseLogger(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                if (_serialPort?.IsOpen ?? false)
                {
                    _serialPort.Close();
                }
                DisposableUtility.Dispose(ref _serialPort);
            }

            _disposed = true;
        }

        #endregion
    }
}

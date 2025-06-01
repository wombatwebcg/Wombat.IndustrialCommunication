using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusRTUClient : ModbusRTUClientBase, IDeviceClient, IAutoReconnectClient
    {
        private readonly SerialPortAdapter _serialPortAdapter;
        private readonly AsyncLock _lock = new AsyncLock();
        
        // 是否启用自动重连
        public bool EnableAutoReconnect { get; set; } = true;
        
        // 最大自动重连次数
        public int MaxReconnectAttempts { get; set; } = 5;
        
        // 重连等待时间
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(2);
        
        // 连接检查间隔
        public TimeSpan ConnectionCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
        
        // 短连接模式下的最大重连次数
        public int ShortConnectionReconnectAttempts { get; set; } = 1;
        
        // 上次重连尝试时间
        private DateTime _lastReconnectAttempt = DateTime.MinValue;
        
        // 串口名称
        public string PortName => _serialPortAdapter?.PortName;

        public ModbusRTUClient(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None
            ) :base(new DeviceMessageTransport(new SerialPortAdapter(portName, baudRate,dataBits,stopBits,parity,handshake)))
        {
            _serialPortAdapter = (SerialPortAdapter)this.Transport.StreamResource;
        }

        public ILogger Logger { get; set; }
        public TimeSpan ConnectTimeout 
        {
            get 
            {
                if (_serialPortAdapter != null)
                {
                   return _serialPortAdapter.ConnectTimeout;
                }
                else
                {
                    return default;
                }
            }
            set 
            { 
              if(_serialPortAdapter!=null)
                {
                    _serialPortAdapter.ConnectTimeout = value;
                }
            } 
        }
        public TimeSpan ReceiveTimeout
        {
            get
            {
                if (_serialPortAdapter != null)
                {
                    return _serialPortAdapter.ReceiveTimeout;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (_serialPortAdapter != null)
                {
                    _serialPortAdapter.ReceiveTimeout = value;
                }
            }
        }
        public TimeSpan SendTimeout
        {
            get
            {
                if (_serialPortAdapter != null)
                {
                    return _serialPortAdapter.SendTimeout;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (_serialPortAdapter != null)
                {
                    _serialPortAdapter.SendTimeout = value;
                }

            }
        }
        public bool Connected
        {
            get
            {
                if (_serialPortAdapter != null)
                {
                    return _serialPortAdapter.Connected;
                }
                else
                {
                    return false;
                }
            }
        }
        public int Retries
        {
            get
            {
                if (Transport != null)
                {
                    return Transport.Retries;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (Transport != null)
                {
                    Transport.Retries = value;
                }

            }
        }
        public TimeSpan WaitToRetryMilliseconds
        {
            get
            {
                if (Transport != null)
                {
                    return Transport.WaitToRetryMilliseconds;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (Transport != null)
                {
                    Transport.WaitToRetryMilliseconds = value;
                }

            }
        }
        public bool IsLongConnection { get; set; } = true;
        public TimeSpan ResponseInterval { get; set; }

        public OperationResult Connect()
        {
            try
            {
                return ConnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Modbus Rtu客户端连接失败: {ex.Message}");
            }
        }

        public async Task<OperationResult> ConnectAsync()
        {
            using (await _lock.LockAsync())
            {
                // 已经连接，直接返回成功
                if (Connected)
                {
                    Logger?.LogDebug("Modbus RTU已连接");
                    return OperationResult.CreateSuccessResult("已连接");
                }

                try
                {
                    Logger?.LogDebug("正在连接Modbus RTU，串口：{PortName}", PortName);
                    
                    // 记录开始时间
                    var startTime = DateTime.Now;
                    
                    // 执行底层传输连接操作
                    var result = await _serialPortAdapter.ConnectAsync();
                    
                    if (result.IsSuccess)
                    {
                        // 记录连接成功日志
                        var timeConsuming = (DateTime.Now - startTime).TotalMilliseconds;
                        Logger?.LogInformation("成功连接Modbus RTU，串口：{PortName}，耗时：{TimeConsuming}ms", 
                            PortName, timeConsuming);
                    }
                    else
                    {
                        // 记录连接失败日志
                        Logger?.LogWarning("连接Modbus RTU失败，串口：{PortName}，错误：{Error}", 
                            PortName, result.Message);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "连接Modbus RTU时发生异常，串口：{PortName}", PortName);
                    return OperationResult.CreateFailedResult($"连接异常: {ex.Message}");
                }
            }
        }

        public OperationResult Disconnect()
        {
            try
            {
                return DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Modbus Rtu客户端断开连接失败: {ex.Message}");
            }
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            using (await _lock.LockAsync())
            {
                // 已经断开连接，直接返回成功
                if (!Connected)
                {
                    Logger?.LogDebug("Modbus RTU已断开连接");
                    return OperationResult.CreateSuccessResult("已断开连接");
                }

                try
                {
                    Logger?.LogDebug("正在断开Modbus RTU连接，串口：{PortName}", PortName);
                    
                    // 记录开始时间
                    var startTime = DateTime.Now;
                    
                    // 执行底层传输断开连接操作
                    var result = await _serialPortAdapter.DisconnectAsync();
                    
                    if (result.IsSuccess)
                    {
                        // 记录断开连接成功日志
                        var timeConsuming = (DateTime.Now - startTime).TotalMilliseconds;
                        Logger?.LogInformation("成功断开Modbus RTU连接，串口：{PortName}，耗时：{TimeConsuming}ms", 
                            PortName, timeConsuming);
                    }
                    else
                    {
                        // 记录断开连接失败日志
                        Logger?.LogWarning("断开Modbus RTU连接失败，串口：{PortName}，错误：{Error}", 
                            PortName, result.Message);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "断开Modbus RTU连接时发生异常，串口：{PortName}", PortName);
                    return OperationResult.CreateFailedResult($"断开连接异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查连接状态并在必要时自动重连
        /// </summary>
        /// <returns>连接操作结果</returns>
        public async Task<OperationResult> CheckAndReconnectAsync()
        {
            // 如果已连接，直接返回成功
            if (Connected)
            {
                return OperationResult.CreateSuccessResult("连接正常");
            }
            
            // 如果未启用自动重连，返回失败
            if (!EnableAutoReconnect)
            {
                return OperationResult.CreateFailedResult("未启用自动重连");
            }
            
            // 检查重连间隔
            var now = DateTime.Now;
            if ((now - _lastReconnectAttempt) < ReconnectDelay)
            {
                return OperationResult.CreateFailedResult("重连间隔未到");
            }
            
            // 记录重连尝试时间
            _lastReconnectAttempt = now;
            
            Logger?.LogInformation("尝试重连Modbus RTU，串口：{PortName}", PortName);
            
            // 执行重连
            return await ConnectAsync();
        }

        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            // 获取操作名称，用于日志记录
            string operationName = $"Read_{(isBit ? "Bit" : "Byte")}";
            
            if (IsLongConnection)
            {
                // 长连接模式 - 检查连接状态并在必要时自动重连
                if (!Connected)
                {
                    if (EnableAutoReconnect)
                    {
                        var reconnectResult = await CheckAndReconnectAsync();
                        if (!reconnectResult.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult<byte[]>($"Modbus RTU自动重连失败，无法读取数据");
                        }
                    }
                    else
                    {
                        return OperationResult.CreateFailedResult<byte[]>($"Modbus RTU客户端没有连接");
                    }
                }
                
                try
                {
                    // 记录请求数据
                    Logger?.LogDebug("开始读取Modbus RTU数据，地址：{Address}，长度：{Length}", address, length);
                    
                    // 执行读取操作
                    var result = await base.ReadAsync(address, length, isBit);
                    
                    // 记录成功的读取操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("成功读取Modbus RTU数据，地址：{Address}，长度：{Length}", 
                            address, length);
                    }
                    else
                    {
                        Logger?.LogWarning("读取Modbus RTU数据失败，地址：{Address}，长度：{Length}，错误：{Error}", 
                            address, length, result.Message);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Logger?.LogError(ex, "读取Modbus RTU数据时发生异常，地址：{Address}，长度：{Length}", address, length);
                    
                    // 返回失败结果
                    return OperationResult.CreateFailedResult<byte[]>($"读取数据失败：{ex.Message}");
                }
            }
            else
            {
                // 短连接模式 - 每次操作都建立新连接
                bool connected = false;
                try
                {
                    // 确保先断开可能存在的连接
                    await DisconnectAsync();
                    
                    // 建立新连接
                    var connectResult = await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        // 短连接模式下连接失败直接返回错误
                        return OperationResult.CreateFailedResult<byte[]>($"短连接模式连接失败：{connectResult.Message}");
                    }
                    
                    connected = true;
                    
                    // 执行读取操作
                    var result = await base.ReadAsync(address, length, isBit);
                    
                    // 记录成功的读取操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("短连接模式成功读取Modbus RTU数据，地址：{Address}，长度：{Length}", 
                            address, length);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "短连接模式读取Modbus RTU数据时发生异常，地址：{Address}，长度：{Length}", address, length);
                    return OperationResult.CreateFailedResult<byte[]>($"短连接读取失败：{ex.Message}");
                }
                finally
                {
                    // 如果成功连接，执行完操作后断开
                    if (connected)
                    {
                        try
                        {
                            await DisconnectAsync();
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogWarning(ex, "短连接模式操作后断开连接时发生异常");
                        }
                    }
                }
            }
        }

        internal override async Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            return await HandleWriteAsync(() => base.WriteAsync(address, data, isBit), address);
        }

        public override async Task<OperationResult> WriteAsync(string address, bool[] data)
        {
            return await HandleWriteAsync(() => base.WriteAsync(address, data), address);
        }

        public override async Task<OperationResult> WriteAsync(string address, bool data)
        {
            return await HandleWriteAsync(() => base.WriteAsync(address, data), address);
        }

        private async Task<OperationResult> HandleWriteAsync(Func<Task<OperationResult>> writeAction, string address)
        {
            if (IsLongConnection)
            {
                // 长连接模式 - 检查连接状态并在必要时自动重连
                if (!Connected)
                {
                    if (EnableAutoReconnect)
                    {
                        var reconnectResult = await CheckAndReconnectAsync();
                        if (!reconnectResult.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult($"Modbus RTU自动重连失败，无法写入数据");
                        }
                    }
                    else
                    {
                        return OperationResult.CreateFailedResult($"Modbus RTU客户端没有连接");
                    }
                }
                
                try
                {
                    // 记录请求信息
                    Logger?.LogDebug("开始写入Modbus RTU数据，地址：{Address}", address);
                    
                    // 执行写入操作
                    var result = await writeAction();
                    
                    // 记录成功的写入操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("成功写入Modbus RTU数据，地址：{Address}", address);
                    }
                    else
                    {
                        Logger?.LogWarning("写入Modbus RTU数据失败，地址：{Address}，错误：{Error}", 
                            address, result.Message);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Logger?.LogError(ex, "写入Modbus RTU数据时发生异常，地址：{Address}", address);
                    
                    // 返回失败结果
                    return OperationResult.CreateFailedResult($"写入数据失败：{ex.Message}");
                }
            }
            else
            {
                // 短连接模式 - 每次操作都建立新连接
                bool connected = false;
                try
                {
                    // 确保先断开可能存在的连接
                    await DisconnectAsync();
                    
                    // 建立新连接
                    var connectResult = await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        // 短连接模式下连接失败直接返回错误
                        return OperationResult.CreateFailedResult($"短连接模式连接失败：{connectResult.Message}");
                    }
                    
                    connected = true;
                    
                    // 执行写入操作
                    var result = await writeAction();
                    
                    // 记录成功的写入操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("短连接模式成功写入Modbus RTU数据，地址：{Address}", address);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "短连接模式写入Modbus RTU数据时发生异常，地址：{Address}", address);
                    return OperationResult.CreateFailedResult($"短连接写入失败：{ex.Message}");
                }
                finally
                {
                    // 如果成功连接，执行完操作后断开
                    if (connected)
                    {
                        try
                        {
                            await DisconnectAsync();
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogWarning(ex, "短连接模式操作后断开连接时发生异常");
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected new virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 确保断开连接
                Disconnect();
            }
            
            base.Dispose(disposing);
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}


using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusRTUClient : ModbusRTUClientBase, IDeviceClient, IModbusClient
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

        #region IModbusClient 接口实现 - 同步方法

        public OperationResult<bool> ReadCoil(byte stationNumber, ushort address)
        {
            try
            {
                string modbusAddress = $"{stationNumber};1;{address}";
                var result = ReadBoolean(modbusAddress);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool>($"读取线圈失败: {ex.Message}");
            }
        }

        public OperationResult<bool[]> ReadCoils(byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                string modbusAddress = $"{stationNumber};1;{startAddress}";
                var result = ReadBoolean(modbusAddress, count);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool[]>($"读取多个线圈失败: {ex.Message}");
            }
        }

        public OperationResult<bool> ReadDiscreteInput(byte stationNumber, ushort address)
        {
            try
            {
                string modbusAddress = $"{stationNumber};2;{address}";
                var result = ReadBoolean(modbusAddress);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool>($"读取离散输入失败: {ex.Message}");
            }
        }

        public OperationResult<bool[]> ReadDiscreteInputs(byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                string modbusAddress = $"{stationNumber};2;{startAddress}";
                var result = ReadBoolean(modbusAddress, count);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool[]>($"读取多个离散输入失败: {ex.Message}");
            }
        }

        public OperationResult<ushort> ReadHoldingRegister(byte stationNumber, ushort address)
        {
            try
            {
                string modbusAddress = $"{stationNumber};3;{address}";
                var result = ReadUInt16(modbusAddress);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort>($"读取保持寄存器失败: {ex.Message}");
            }
        }

        public OperationResult<ushort[]> ReadHoldingRegisters(byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                string modbusAddress = $"{stationNumber};3;{startAddress}";
                var result = ReadUInt16(modbusAddress, count);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort[]>($"读取多个保持寄存器失败: {ex.Message}");
            }
        }

        public OperationResult<ushort> ReadInputRegister(byte stationNumber, ushort address)
        {
            try
            {
                string modbusAddress = $"{stationNumber};4;{address}";
                var result = ReadUInt16(modbusAddress);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort>($"读取输入寄存器失败: {ex.Message}");
            }
        }

        public OperationResult<ushort[]> ReadInputRegisters(byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                string modbusAddress = $"{stationNumber};4;{startAddress}";
                var result = ReadUInt16(modbusAddress, count);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort[]>($"读取多个输入寄存器失败: {ex.Message}");
            }
        }

        public OperationResult WriteCoil(byte stationNumber, ushort address, bool value)
        {
            try
            {
                string modbusAddress = $"{stationNumber};5;{address}";
                var result = Write(modbusAddress, value);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"写入线圈失败: {ex.Message}");
            }
        }

        public OperationResult WriteCoils(byte stationNumber, ushort startAddress, bool[] values)
        {
            try
            {
                string modbusAddress = $"{stationNumber};15;{startAddress}";
                var result = Write(modbusAddress, values);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"写入多个线圈失败: {ex.Message}");
            }
        }

        public OperationResult WriteHoldingRegister(byte stationNumber, ushort address, ushort value)
        {
            try
            {
                string modbusAddress = $"{stationNumber};6;{address}";
                var result = Write(modbusAddress, value);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"写入保持寄存器失败: {ex.Message}");
            }
        }

        public OperationResult WriteHoldingRegisters(byte stationNumber, ushort startAddress, ushort[] values)
        {
            try
            {
                string modbusAddress = $"{stationNumber};16;{startAddress}";
                var result = Write(modbusAddress, values);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"写入多个保持寄存器失败: {ex.Message}");
            }
        }

        #endregion

        #region IModbusClient 接口实现 - 异步方法

        public async Task<OperationResult<bool>> ReadCoilAsync(byte stationNumber, ushort address)
        {
            try
            {
                string modbusAddress = $"{stationNumber};1;{address}";
                var result = await ReadBooleanAsync(modbusAddress);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool>($"异步读取线圈失败: {ex.Message}");
            }
        }

        public async Task<OperationResult<bool[]>> ReadCoilsAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                string modbusAddress = $"{stationNumber};1;{startAddress}";
                var result = await ReadBooleanAsync(modbusAddress, count);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool[]>($"异步读取多个线圈失败: {ex.Message}");
            }
        }

        public async Task<OperationResult<bool>> ReadDiscreteInputAsync(byte stationNumber, ushort address)
        {
            try
            {
                string modbusAddress = $"{stationNumber};2;{address}";
                var result = await ReadBooleanAsync(modbusAddress);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool>($"异步读取离散输入失败: {ex.Message}");
            }
        }

        public async Task<OperationResult<bool[]>> ReadDiscreteInputsAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                string modbusAddress = $"{stationNumber};2;{startAddress}";
                var result = await ReadBooleanAsync(modbusAddress, count);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<bool[]>($"异步读取多个离散输入失败: {ex.Message}");
            }
        }

        public async Task<OperationResult<ushort>> ReadHoldingRegisterAsync(byte stationNumber, ushort address)
        {
            try
            {
                string modbusAddress = $"{stationNumber};3;{address}";
                var result = await ReadUInt16Async(modbusAddress);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort>($"异步读取保持寄存器失败: {ex.Message}");
            }
        }

        public async Task<OperationResult<ushort[]>> ReadHoldingRegistersAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                string modbusAddress = $"{stationNumber};3;{startAddress}";
                var result = await ReadUInt16Async(modbusAddress, count);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort[]>($"异步读取多个保持寄存器失败: {ex.Message}");
            }
        }

        public async Task<OperationResult<ushort>> ReadInputRegisterAsync(byte stationNumber, ushort address)
        {
            try
            {
                string modbusAddress = $"{stationNumber};4;{address}";
                var result = await ReadUInt16Async(modbusAddress);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort>($"异步读取输入寄存器失败: {ex.Message}");
            }
        }

        public async Task<OperationResult<ushort[]>> ReadInputRegistersAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            try
            {
                string modbusAddress = $"{stationNumber};4;{startAddress}";
                var result = await ReadUInt16Async(modbusAddress, count);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<ushort[]>($"异步读取多个输入寄存器失败: {ex.Message}");
            }
        }

        public async Task<OperationResult> WriteCoilAsync(byte stationNumber, ushort address, bool value)
        {
            try
            {
                string modbusAddress = $"{stationNumber};5;{address}";
                var result = await WriteAsync(modbusAddress, value);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"异步写入线圈失败: {ex.Message}");
            }
        }

        public async Task<OperationResult> WriteCoilsAsync(byte stationNumber, ushort startAddress, bool[] values)
        {
            try
            {
                string modbusAddress = $"{stationNumber};15;{startAddress}";
                var result = await WriteAsync(modbusAddress, values);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"异步写入多个线圈失败: {ex.Message}");
            }
        }

        public async Task<OperationResult> WriteHoldingRegisterAsync(byte stationNumber, ushort address, ushort value)
        {
            try
            {
                string modbusAddress = $"{stationNumber};6;{address}";
                var result = await WriteAsync(modbusAddress, value);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"异步写入保持寄存器失败: {ex.Message}");
            }
        }

        public async Task<OperationResult> WriteHoldingRegistersAsync(byte stationNumber, ushort startAddress, ushort[] values)
        {
            try
            {
                string modbusAddress = $"{stationNumber};16;{startAddress}";
                var result = await WriteAsync(modbusAddress, values);
                return result;
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"异步写入多个保持寄存器失败: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// 批量读取方法
        /// </summary>
        public override async ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult<Dictionary<string, (DataTypeEnums, object)>>();
                try
                {
                    if (addresses == null || addresses.Count == 0)
                    {
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }
                    var internalAddresses = new Dictionary<string, (DataTypeEnums, object)>();
                    foreach (var kvp in addresses)
                        internalAddresses[kvp.Key] = (kvp.Value, null);
                    var addressInfos = ModbusBatchHelper.ParseModbusAddresses(internalAddresses);
                    if (addressInfos.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "没有有效的地址可以读取";
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }
                    var optimizedBlocks = ModbusBatchHelper.OptimizeModbusAddressBlocks(addressInfos);
                    if (optimizedBlocks.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "地址优化失败";
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }
                    // 执行批量读取
                    var blockDataDict = new Dictionary<string, byte[]>();
                    var errors = new List<string>();
                    foreach (var block in optimizedBlocks)
                    {
                        try
                        {
                            string blockAddress = $"{block.StationNumber};{block.FunctionCode};{block.StartAddress}";
                            string blockKey = $"{block.StationNumber}_{block.FunctionCode}_{block.StartAddress}_{block.TotalLength}";
                            var readResult = await InternalModbusReadAsync(blockAddress, block.TotalLength, block.FunctionCode == 0x01 || block.FunctionCode == 0x02);
                            if (readResult.IsSuccess)
                            {
                                blockDataDict[blockKey] = readResult.ResultValue;
                                result.Requsts.AddRange(readResult.Requsts);
                                result.Responses.AddRange(readResult.Responses);
                            }
                            else
                            {
                                errors.Add($"读取块 {blockAddress} 失败: {readResult.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"读取块 {block.StationNumber};{block.FunctionCode};{block.StartAddress} 异常: {ex.Message}");
                        }
                    }
                    if (errors.Count > 0)
                    {
                        result.IsSuccess = blockDataDict.Count > 0;
                        result.Message = string.Join("; ", errors);
                    }
                    else
                    {
                        result.IsSuccess = true;
                    }
                    var extractedData = ModbusBatchHelper.ExtractDataFromModbusBlocks(blockDataDict, optimizedBlocks, addressInfos);
                    var finalResult = new Dictionary<string, (DataTypeEnums, object)>();
                    foreach (var kvp in addresses)
                    {
                        var address = kvp.Key;
                        var dataType = kvp.Value;
                        if (extractedData.TryGetValue(address, out var value))
                            finalResult[address] = (dataType, value);
                        else
                            finalResult[address] = (dataType, null);
                    }
                    result.ResultValue = finalResult;
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = $"批量读取异常: {ex.Message}";
                    result.Exception = ex;
                    result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                }
                return result.Complete();
            }
        }

        /// <summary>
        /// 批量写入方法
        /// </summary>
        public override async ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                try
                {
                    if (addresses == null || addresses.Count == 0)
                        return result.Complete();
                    var internalAddresses = new Dictionary<string, (DataTypeEnums, object)>();
                    foreach (var kvp in addresses)
                        internalAddresses[kvp.Key] = (kvp.Value.Item1, kvp.Value.Item2);
                    var addressInfos = ModbusBatchHelper.ParseModbusAddresses(internalAddresses);
                    if (addressInfos.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "没有有效的地址可以写入";
                        return result.Complete();
                    }
                    var writeErrors = new List<string>();
                    var successCount = 0;
                    foreach (var addressInfo in addressInfos)
                    {
                        try
                        {
                            if (!internalAddresses.TryGetValue(addressInfo.OriginalAddress, out var value))
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 没有对应的值");
                                continue;
                            }
                            byte[] data = ModbusBatchHelper.ConvertValueToModbusBytes(value, addressInfo, IsReverse);
                            if (data == null)
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 的值转换失败");
                                continue;
                            }
                            string writeAddress = ModbusBatchHelper.ConstructModbusWriteAddress(addressInfo);
                            if (string.IsNullOrEmpty(writeAddress))
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 构造写入地址失败");
                                continue;
                            }
                            var writeResult = await WriteAsync(writeAddress, data, addressInfo.FunctionCode == 0x05 || addressInfo.FunctionCode == 0x0F);
                            if (writeResult.IsSuccess)
                            {
                                successCount++;
                                result.Requsts.AddRange(writeResult.Requsts);
                                result.Responses.AddRange(writeResult.Responses);
                            }
                            else
                            {
                                writeErrors.Add($"写入地址 {addressInfo.OriginalAddress} 失败: {writeResult.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            writeErrors.Add($"写入地址 {addressInfo.OriginalAddress} 异常: {ex.Message}");
                        }
                    }
                    if (successCount == addressInfos.Count)
                    {
                        result.IsSuccess = true;
                        result.Message = $"成功写入 {successCount} 个地址";
                    }
                    else if (successCount > 0)
                    {
                        result.IsSuccess = false;
                        result.Message = $"部分写入成功 ({successCount}/{addressInfos.Count}): {string.Join("; ", writeErrors)}";
                    }
                    else
                    {
                        result.IsSuccess = false;
                        result.Message = $"批量写入失败: {string.Join("; ", writeErrors)}";
                    }
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = $"批量写入异常: {ex.Message}";
                    result.Exception = ex;
                }
                return result.Complete();
            }
        }

        /// <summary>
        /// 内部Modbus读取方法，不加锁，供批量读取使用
        /// </summary>
        private async ValueTask<OperationResult<byte[]>> InternalModbusReadAsync(string address, int length, bool isBit = false)
        {
            if (ModbusAddressParser.TryParseModbusAddress(address, out var modbusAddress))
            {
                var request = new ModbusRTURequest(modbusAddress.StationNumber, modbusAddress.FunctionCode, modbusAddress.Address, (ushort)length);
                var response = await Transport.UnicastReadMessageAsync(request);
                if (response.IsSuccess)
                {
                    var dataPackage = response.ResultValue.ProtocolMessageFrame;
                    var modbusRtuResponse = new ModbusRTUResponse(dataPackage);
                    var result = new OperationResult<byte[]>(response, modbusRtuResponse.Data);
                    result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                    result.Responses.Add(string.Join(" ", response.ResultValue.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                    return result.Complete();
                }
                else
                {
                    return OperationResult.CreateFailedResult<byte[]>(response);
                }
            }
            return OperationResult.CreateFailedResult<byte[]>();
        }
    }
}


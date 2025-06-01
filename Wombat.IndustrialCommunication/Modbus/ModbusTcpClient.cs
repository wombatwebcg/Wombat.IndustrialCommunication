using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusTcpClient : ModbusTcpClientBase, IDeviceClient, IAutoReconnectClient, IModbusClient
    {
        private readonly TcpClientAdapter _tcpClientAdapter;
        private readonly AsyncLock _lock = new AsyncLock();
        
        public IPEndPoint IPEndPoint { get; private set; }
        
        // 是否启用自动重连
        public bool EnableAutoReconnect { get; set; } = true;
        
        // 最大自动重连次数
        public int MaxReconnectAttempts { get; set; } = 5;
        
        // 重连等待时间
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(2);
        
        // 上次重连尝试时间
        private DateTime _lastReconnectAttempt = DateTime.MinValue;
        
        // 连接检查间隔
        public TimeSpan ConnectionCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
        
        // 短连接模式下的最大重连次数
        public int ShortConnectionReconnectAttempts { get; set; } = 1;
        
        public ModbusTcpClient(string ip, int port = 502 )
            : base(new DeviceMessageTransport(new TcpClientAdapter(ip,  port)))
        {
            _tcpClientAdapter = (TcpClientAdapter)this.Transport.StreamResource;
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            IPEndPoint = new IPEndPoint(address, port);
        }

        public ILogger Logger { get; set; }
        public TimeSpan ConnectTimeout
        {
            get
            {
                if (_tcpClientAdapter != null)
                {
                    return _tcpClientAdapter.ConnectTimeout;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (_tcpClientAdapter != null)
                {
                    _tcpClientAdapter.ConnectTimeout = value;
                }
            }
        }
        public TimeSpan ReceiveTimeout
        {
            get
            {
                if (_tcpClientAdapter != null)
                {
                    return _tcpClientAdapter.ReceiveTimeout;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (_tcpClientAdapter != null)
                {
                    _tcpClientAdapter.ReceiveTimeout = value;
                }
            }
        }
        public TimeSpan SendTimeout
        {
            get
            {
                if (_tcpClientAdapter != null)
                {
                    return _tcpClientAdapter.SendTimeout;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (_tcpClientAdapter != null)
                {
                    _tcpClientAdapter.SendTimeout = value;
                }

            }
        }
        public bool Connected
        {
            get
            {
                if (_tcpClientAdapter != null)
                {
                    return _tcpClientAdapter.Connected;
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

        public override string Version => nameof(ModbusTcpClient);

        public OperationResult Connect()
        {
            return ConnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> ConnectAsync()
        {
            using (await _lock.LockAsync())
            {
                // 已经连接，直接返回成功
                if (Connected)
                {
                    Logger?.LogDebug("Modbus TCP已连接");
                    return OperationResult.CreateSuccessResult("已连接");
                }

                try
                {
                    Logger?.LogDebug("正在连接Modbus TCP，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
                    
                    // 记录开始时间
                    var startTime = DateTime.Now;
                    
                    // 执行底层传输连接操作
                    var result = await _tcpClientAdapter.ConnectAsync();
                    
                    if (result.IsSuccess)
                    {
                        // 记录连接成功日志
                        var timeConsuming = (DateTime.Now - startTime).TotalMilliseconds;
                        Logger?.LogInformation("成功连接Modbus TCP，地址：{Address}:{Port}，耗时：{TimeConsuming}ms", 
                            IPEndPoint.Address, IPEndPoint.Port, timeConsuming);
                    }
                    else
                    {
                        // 记录连接失败日志
                        Logger?.LogWarning("连接Modbus TCP失败，地址：{Address}:{Port}，错误：{Error}", 
                            IPEndPoint.Address, IPEndPoint.Port, result.Message);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "连接Modbus TCP时发生异常，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
                    return OperationResult.CreateFailedResult($"连接异常: {ex.Message}");
                }
            }
        }

        public OperationResult Disconnect()
        {
            return DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            using (await _lock.LockAsync())
            {
                // 已经断开连接，直接返回成功
                if (!Connected)
                {
                    Logger?.LogDebug("Modbus TCP已断开连接");
                    return OperationResult.CreateSuccessResult("已断开连接");
                }

                try
                {
                    Logger?.LogDebug("正在断开Modbus TCP连接，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
                    
                    // 记录开始时间
                    var startTime = DateTime.Now;
                    
                    // 执行底层传输断开连接操作
                    var result = await _tcpClientAdapter.DisconnectAsync();
                    
                    if (result.IsSuccess)
                    {
                        // 记录断开连接成功日志
                        var timeConsuming = (DateTime.Now - startTime).TotalMilliseconds;
                        Logger?.LogInformation("成功断开Modbus TCP连接，地址：{Address}:{Port}，耗时：{TimeConsuming}ms", 
                            IPEndPoint.Address, IPEndPoint.Port, timeConsuming);
                    }
                    else
                    {
                        // 记录断开连接失败日志
                        Logger?.LogWarning("断开Modbus TCP连接失败，地址：{Address}:{Port}，错误：{Error}", 
                            IPEndPoint.Address, IPEndPoint.Port, result.Message);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "断开Modbus TCP连接时发生异常，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
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
            
            Logger?.LogInformation("尝试重连Modbus TCP，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
            
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
                            return OperationResult.CreateFailedResult<byte[]>($"Modbus TCP自动重连失败，无法读取数据");
                        }
                    }
                    else
                    {
                        return OperationResult.CreateFailedResult<byte[]>($"Modbus TCP客户端没有连接");
                    }
                }
                
                try
                {
                    // 记录请求数据
                    Logger?.LogDebug("开始读取Modbus TCP数据，地址：{Address}，长度：{Length}", address, length);
                    
                    // 执行读取操作
                    var result = await base.ReadAsync(address, length, isBit);
                    
                    // 记录成功的读取操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("成功读取Modbus TCP数据，地址：{Address}，长度：{Length}", 
                            address, length);
                    }
                    else
                    {
                        Logger?.LogWarning("读取Modbus TCP数据失败，地址：{Address}，长度：{Length}，错误：{Error}", 
                            address, length, result.Message);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Logger?.LogError(ex, "读取Modbus TCP数据时发生异常，地址：{Address}，长度：{Length}", address, length);
                    
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
                        Logger?.LogDebug("短连接模式成功读取Modbus TCP数据，地址：{Address}，长度：{Length}", 
                            address, length);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "短连接模式读取Modbus TCP数据时发生异常，地址：{Address}，长度：{Length}", address, length);
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
                            return OperationResult.CreateFailedResult($"Modbus TCP自动重连失败，无法写入数据");
                        }
                    }
                    else
                    {
                        return OperationResult.CreateFailedResult($"Modbus TCP客户端没有连接");
                    }
                }
                
                try
                {
                    // 记录请求信息
                    Logger?.LogDebug("开始写入Modbus TCP数据，地址：{Address}", address);
                    
                    // 执行写入操作
                    var result = await writeAction();
                    
                    // 记录成功的写入操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("成功写入Modbus TCP数据，地址：{Address}", address);
                    }
                    else
                    {
                        Logger?.LogWarning("写入Modbus TCP数据失败，地址：{Address}，错误：{Error}", 
                            address, result.Message);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Logger?.LogError(ex, "写入Modbus TCP数据时发生异常，地址：{Address}", address);
                    
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
                        Logger?.LogDebug("短连接模式成功写入Modbus TCP数据，地址：{Address}", address);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "短连接模式写入Modbus TCP数据时发生异常，地址：{Address}", address);
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
            return ModbusClientExtensions.ReadCoil(this, stationNumber, address);
        }

        public OperationResult<bool[]> ReadCoils(byte stationNumber, ushort startAddress, ushort count)
        {
            return ModbusClientExtensions.ReadCoils(this, stationNumber, startAddress, count);
        }

        public OperationResult<bool> ReadDiscreteInput(byte stationNumber, ushort address)
        {
            return ModbusClientExtensions.ReadDiscreteInput(this, stationNumber, address);
        }

        public OperationResult<bool[]> ReadDiscreteInputs(byte stationNumber, ushort startAddress, ushort count)
        {
            return ModbusClientExtensions.ReadDiscreteInputs(this, stationNumber, startAddress, count);
        }

        public OperationResult<ushort> ReadHoldingRegister(byte stationNumber, ushort address)
        {
            return ModbusClientExtensions.ReadHoldingRegister(this, stationNumber, address);
        }

        public OperationResult<ushort[]> ReadHoldingRegisters(byte stationNumber, ushort startAddress, ushort count)
        {
            return ModbusClientExtensions.ReadHoldingRegisters(this, stationNumber, startAddress, count);
        }

        public OperationResult<ushort> ReadInputRegister(byte stationNumber, ushort address)
        {
            return ModbusClientExtensions.ReadInputRegister(this, stationNumber, address);
        }

        public OperationResult<ushort[]> ReadInputRegisters(byte stationNumber, ushort startAddress, ushort count)
        {
            return ModbusClientExtensions.ReadInputRegisters(this, stationNumber, startAddress, count);
        }

        public OperationResult WriteCoil(byte stationNumber, ushort address, bool value)
        {
            return ModbusClientExtensions.WriteCoil(this, stationNumber, address, value);
        }

        public OperationResult WriteCoils(byte stationNumber, ushort startAddress, bool[] values)
        {
            return ModbusClientExtensions.WriteCoils(this, stationNumber, startAddress, values);
        }

        public OperationResult WriteHoldingRegister(byte stationNumber, ushort address, ushort value)
        {
            return ModbusClientExtensions.WriteHoldingRegister(this, stationNumber, address, value);
        }

        public OperationResult WriteHoldingRegisters(byte stationNumber, ushort startAddress, ushort[] values)
        {
            return ModbusClientExtensions.WriteHoldingRegisters(this, stationNumber, startAddress, values);
        }

        #endregion

        #region IModbusClient 接口实现 - 异步方法

        public Task<OperationResult<bool>> ReadCoilAsync(byte stationNumber, ushort address)
        {
            return ModbusClientExtensions.ReadCoilAsync(this, stationNumber, address);
        }

        public Task<OperationResult<bool[]>> ReadCoilsAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            return ModbusClientExtensions.ReadCoilsAsync(this, stationNumber, startAddress, count);
        }

        public Task<OperationResult<bool>> ReadDiscreteInputAsync(byte stationNumber, ushort address)
        {
            return ModbusClientExtensions.ReadDiscreteInputAsync(this, stationNumber, address);
        }

        public Task<OperationResult<bool[]>> ReadDiscreteInputsAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            return ModbusClientExtensions.ReadDiscreteInputsAsync(this, stationNumber, startAddress, count);
        }

        public Task<OperationResult<ushort>> ReadHoldingRegisterAsync(byte stationNumber, ushort address)
        {
            return ModbusClientExtensions.ReadHoldingRegisterAsync(this, stationNumber, address);
        }

        public Task<OperationResult<ushort[]>> ReadHoldingRegistersAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            return ModbusClientExtensions.ReadHoldingRegistersAsync(this, stationNumber, startAddress, count);
        }

        public Task<OperationResult<ushort>> ReadInputRegisterAsync(byte stationNumber, ushort address)
        {
            return ModbusClientExtensions.ReadInputRegisterAsync(this, stationNumber, address);
        }

        public Task<OperationResult<ushort[]>> ReadInputRegistersAsync(byte stationNumber, ushort startAddress, ushort count)
        {
            return ModbusClientExtensions.ReadInputRegistersAsync(this, stationNumber, startAddress, count);
        }

        public Task<OperationResult> WriteCoilAsync(byte stationNumber, ushort address, bool value)
        {
            return ModbusClientExtensions.WriteCoilAsync(this, stationNumber, address, value);
        }

        public Task<OperationResult> WriteCoilsAsync(byte stationNumber, ushort startAddress, bool[] values)
        {
            return ModbusClientExtensions.WriteCoilsAsync(this, stationNumber, startAddress, values);
        }

        public Task<OperationResult> WriteHoldingRegisterAsync(byte stationNumber, ushort address, ushort value)
        {
            return ModbusClientExtensions.WriteHoldingRegisterAsync(this, stationNumber, address, value);
        }

        public Task<OperationResult> WriteHoldingRegistersAsync(byte stationNumber, ushort startAddress, ushort[] values)
        {
            return ModbusClientExtensions.WriteHoldingRegistersAsync(this, stationNumber, startAddress, values);
        }

        #endregion
    }
}

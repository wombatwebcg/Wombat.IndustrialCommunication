using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.PLC
{
    public class SiemensClient : S7Communication, IDeviceClient
    {
        TcpClientAdapter _tcpClientAdapter;
        
        public IPEndPoint IPEndPoint { get; private set; }
        
        // 是否启用自动重连
        public bool EnableAutoReconnect { get; set; } = true;
        
        // 最大自动重连次数
        public int MaxReconnectAttempts { get; set; } = 3;
        
        // 重连等待时间
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
        
        // 连接检查间隔
        public TimeSpan ConnectionCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
        
        // 短连接模式下的最大重连次数
        public int ShortConnectionReconnectAttempts { get; set; } = 1;
        
        // 上次重连尝试时间
        private DateTime _lastReconnectAttempt = DateTime.MinValue;
        
        public SiemensClient(string ip, int port, SiemensVersion siemensVersion, byte slot = 0, byte rack = 0)
            :base(new S7EthernetTransport(new TcpClientAdapter(ip, port)))
        {
            _tcpClientAdapter = (TcpClientAdapter)this.Transport.StreamResource;
            Slot = slot;
            Rack = rack;
            SiemensVersion = siemensVersion;
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
              if(_tcpClientAdapter!=null)
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
                    Logger?.LogDebug("西门子PLC已连接");
                    return OperationResult.CreateSuccessResult("已连接");
                }

                try
                {
                    Logger?.LogDebug("正在连接西门子PLC，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
                    
                    // 记录开始时间
                    var startTime = DateTime.Now;
                    var tcpConnectStartTime = DateTime.Now;
                    
                    // 执行底层传输连接操作
                    var result = await _tcpClientAdapter.ConnectAsync().ConfigureAwait(false);
                    
                    var tcpConnectTime = (DateTime.Now - tcpConnectStartTime).TotalMilliseconds;
                    Logger?.LogDebug("TCP连接耗时：{TcpConnectTime}ms", tcpConnectTime);
                    
                    if (result.IsSuccess)
                    {
                        // 连接成功后初始化S7协议
                        // 使用更短的超时时间进行协议初始化，避免超时累加
                        var initTimeout = TimeSpan.FromMilliseconds(Math.Max(500, ConnectTimeout.TotalMilliseconds * 0.3)); // 使用连接超时的30%或至少500ms
                        
                        var initStartTime = DateTime.Now;
                        var initResult = await InitAsync(initTimeout).ConfigureAwait(false);
                        var initTime = (DateTime.Now - initStartTime).TotalMilliseconds;
                        Logger?.LogDebug("S7协议初始化耗时：{InitTime}ms，超时设置：{InitTimeout}ms", initTime, initTimeout.TotalMilliseconds);
                        
                        if (!initResult.IsSuccess)
                        {
                            // 初始化失败，断开连接
                            await _tcpClientAdapter.DisconnectAsync().ConfigureAwait(false);
                            Logger?.LogWarning("西门子PLC协议初始化失败，地址：{Address}:{Port}，错误：{Error}", 
                                IPEndPoint.Address, IPEndPoint.Port, initResult.Message);
                            return OperationResult.CreateFailedResult($"协议初始化失败: {initResult.Message}");
                        }
                        
                        // 记录连接成功日志
                        var timeConsuming = (DateTime.Now - startTime).TotalMilliseconds;
                        Logger?.LogInformation("成功连接西门子PLC，地址：{Address}:{Port}，总耗时：{TimeConsuming}ms (TCP连接:{TcpConnectTime}ms + 协议初始化:{InitTime}ms)", 
                            IPEndPoint.Address, IPEndPoint.Port, timeConsuming, tcpConnectTime, initTime);
                    }
                    else
                    {
                        // 记录连接失败日志
                        var timeConsuming = (DateTime.Now - startTime).TotalMilliseconds;
                        Logger?.LogWarning("连接西门子PLC失败，地址：{Address}:{Port}，耗时：{TimeConsuming}ms，错误：{Error}", 
                            IPEndPoint.Address, IPEndPoint.Port, timeConsuming, result.Message);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "连接西门子PLC时发生异常，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
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
                    Logger?.LogDebug("西门子PLC已断开连接");
                    return OperationResult.CreateSuccessResult("已断开连接");
                }

                try
                {
                    Logger?.LogDebug("正在断开西门子PLC连接，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
                    
                    // 记录开始时间
                    var startTime = DateTime.Now;
                    
                    // 执行底层传输断开连接操作
                    var result = await _tcpClientAdapter.DisconnectAsync().ConfigureAwait(false);
                    
                    if (result.IsSuccess)
                    {
                        // 记录断开连接成功日志
                        var timeConsuming = (DateTime.Now - startTime).TotalMilliseconds;
                        Logger?.LogInformation("成功断开西门子PLC连接，地址：{Address}:{Port}，耗时：{TimeConsuming}ms", 
                            IPEndPoint.Address, IPEndPoint.Port, timeConsuming);
                    }
                    else
                    {
                        // 记录断开连接失败日志
                        Logger?.LogWarning("断开西门子PLC连接失败，地址：{Address}:{Port}，错误：{Error}", 
                            IPEndPoint.Address, IPEndPoint.Port, result.Message);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "断开西门子PLC连接时发生异常，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
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
            
            Logger?.LogInformation("尝试重连西门子PLC，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
            
            // 执行重连
            return await ConnectAsync();
        }

        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            if (IsLongConnection)
            {
                // 长连接模式 - 检查连接状态并在必要时自动重连
                if (!Connected)
                {
                    if (EnableAutoReconnect)
                    {
                        var reconnectResult = await CheckAndReconnectAsync().ConfigureAwait(false);
                        if (!reconnectResult.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult<byte[]>($"S7客户端自动重连失败，无法读取数据");
                        }
                    }
                    else
                    {
                        return OperationResult.CreateFailedResult<byte[]>($"S7客户端没有连接 ip:{IPEndPoint.Address}");
                    }
                }
                
                try
                {
                    // 执行读取操作
                    var result = await base.ReadAsync(address, length, isBit).ConfigureAwait(false);
                    
                    // 记录成功的读取操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("成功读取S7数据，地址：{Address}", address);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Logger?.LogError(ex, "读取S7数据时发生异常，地址：{Address}", address);
                    
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
                    await DisconnectAsync().ConfigureAwait(false);
                    
                    // 建立新连接
                    var connectResult = await ConnectAsync().ConfigureAwait(false);
                    if (!connectResult.IsSuccess)
                    {
                        // 短连接模式下连接失败直接返回错误
                        return OperationResult.CreateFailedResult<byte[]>($"短连接模式连接失败：{connectResult.Message}");
                    }
                    
                    connected = true;
                    
                    // 执行读取操作
                    var result = await base.ReadAsync(address, length, isBit).ConfigureAwait(false);
                    
                    // 记录成功的读取操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("短连接模式成功读取S7数据，地址：{Address}", address);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Logger?.LogError(ex, "短连接模式读取S7数据时发生异常，地址：{Address}", address);
                    return OperationResult.CreateFailedResult<byte[]>($"短连接读取失败：{ex.Message}");
                }
                finally
                {
                    // 如果成功连接，执行完操作后断开连接
                    if (connected)
                    {
                        try
                        {
                            await DisconnectAsync().ConfigureAwait(false);
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
            if (IsLongConnection)
            {
                // 长连接模式 - 检查连接状态并在必要时自动重连
                if (!Connected)
                {
                    if (EnableAutoReconnect)
                    {
                        var reconnectResult = await CheckAndReconnectAsync().ConfigureAwait(false);
                        if (!reconnectResult.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult($"S7客户端自动重连失败，无法写入数据");
                        }
                    }
                    else
                    {
                        return OperationResult.CreateFailedResult($"客户端没有连接");
                    }
                }
                
                try
                {
                    // 执行写入操作
                    var result = await base.WriteAsync(address, data, isBit).ConfigureAwait(false);
                    
                    // 记录成功的写入操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("成功写入S7数据，地址：{Address}", address);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Logger?.LogError(ex, "写入S7数据时发生异常，地址：{Address}", address);
                    
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
                    await DisconnectAsync().ConfigureAwait(false);
                    
                    // 建立新连接
                    var connectResult = await ConnectAsync().ConfigureAwait(false);
                    if (!connectResult.IsSuccess)
                    {
                        // 短连接模式下连接失败直接返回错误
                        return OperationResult.CreateFailedResult($"短连接模式连接失败：{connectResult.Message}");
                    }
                    
                    connected = true;
                    
                    // 执行写入操作
                    var result = await base.WriteAsync(address, data, isBit).ConfigureAwait(false);
                    
                    // 记录成功的写入操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("短连接模式成功写入S7数据，地址：{Address}", address);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Logger?.LogError(ex, "短连接模式写入S7数据时发生异常，地址：{Address}", address);
                    return OperationResult.CreateFailedResult($"短连接写入失败：{ex.Message}");
                }
                finally
                {
                    // 如果成功连接，执行完操作后断开连接
                    if (connected)
                    {
                        try
                        {
                            await DisconnectAsync().ConfigureAwait(false);
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
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 确保断开连接
                Disconnect();
            }
            
            base.Dispose(disposing);
        }
    }
}

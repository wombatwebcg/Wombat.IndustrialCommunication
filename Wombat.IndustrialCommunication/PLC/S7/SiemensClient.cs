using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Abstractions;
using Wombat.IndustrialCommunication.Adapters;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.PLC
{
    public class SiemensClient : S7Communication, IDeviceClient
    {
        TcpClientAdapter _tcpClientAdapter;
        
        public IPEndPoint IPEndPoint { get; private set; }
        
        // 连接检查间隔
        public TimeSpan ConnectionCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
        
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

        public async Task<OperationResult> ConnectAsync(CancellationToken cancellationToken = default)
        {
            using (await _lock.LockAsync(cancellationToken))
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
                    var result =await  _tcpClientAdapter.ConnectAsync(cancellationToken);
                    
                    var tcpConnectTime = (DateTime.Now - tcpConnectStartTime).TotalMilliseconds;
                    Logger?.LogDebug("TCP连接耗时：{TcpConnectTime}ms", tcpConnectTime);
                    
                    if (result.IsSuccess)
                    {
                        // 连接成功后初始化S7协议
                        
                        var initStartTime = DateTime.Now;
                        var initResult = await InitWithoutLockAsync(ConnectTimeout, cancellationToken);
                        var initTime = (DateTime.Now - initStartTime).TotalMilliseconds;
                        Logger?.LogDebug("S7协议初始化耗时：{InitTime}ms，超时设置：{InitTimeout}ms", initTime, ConnectTimeout);
                        
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
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "连接西门子PLC时发生异常，地址：{Address}:{Port}", IPEndPoint.Address, IPEndPoint.Port);
                    return OperationResult.CreateFailedResult($"连接异常: {ex.Message}");
                }
            }
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
                    var result = await _tcpClientAdapter.DisconnectAsync();
                    
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

        private Task HandleProtocolSynchronizationFailureAsync(string operation, string address, string reason)
        {
            Logger?.LogWarning(
                "S7{Operation}检测到协议同步异常，地址：{Address}，原因：{Reason}，准备废弃当前连接",
                operation,
                address,
                reason);

            try
            {
                _tcpClientAdapter?.StreamClose();
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "S7{Operation}协议同步异常后关闭短连接失败，地址：{Address}", operation, address);
            }
            return Task.CompletedTask;
        }

        protected internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, DataTypeEnums dataType, bool isBit = false)
        {
            return await ReadAsync(address, length, dataType, isBit, CancellationToken.None);
        }

        protected internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, DataTypeEnums dataType, bool isBit, CancellationToken cancellationToken)
        {
            if (IsLongConnection)
            {
                if (!Connected)
                {
                    return OperationResult.CreateFailedResult<byte[]>($"S7客户端没有连接 ip:{IPEndPoint.Address}");
                }
                
                try
                {
                    var result = await base.ReadAsync(address, length, dataType, isBit, cancellationToken).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("成功读取S7数据，地址：{Address}", address);
                    }
                    else if (S7Communication.IsProtocolSynchronizationFailure(result))
                    {
                        await HandleProtocolSynchronizationFailureAsync("读取", address, result.Message).ConfigureAwait(false);
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
                    
                    var result = await base.ReadAsync(address, length, dataType, isBit, cancellationToken).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("短连接模式成功读取S7数据，地址：{Address}", address);
                    }
                    else if (S7Communication.IsProtocolSynchronizationFailure(result))
                    {
                        await HandleProtocolSynchronizationFailureAsync("读取", address, result.Message).ConfigureAwait(false);
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

        protected internal override async Task<OperationResult> WriteAsync(string address, byte[] data, DataTypeEnums dataType, bool isBit = false)
        {
            return await WriteAsync(address, data, dataType, isBit, CancellationToken.None);
        }

        protected internal override async Task<OperationResult> WriteAsync(string address, byte[] data, DataTypeEnums dataType, bool isBit, CancellationToken cancellationToken)
        {
            if (IsLongConnection)
            {
                if (!Connected)
                {
                    return OperationResult.CreateFailedResult(WriteErrorCodes.ConnectionNotEstablished, "客户端没有连接");
                }
                
                try
                {
                    // 执行写入操作
                    var result = await base.WriteAsync(address, data, dataType, isBit, cancellationToken).ConfigureAwait(false);
                    
                    // 记录成功的写入操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("成功写入S7数据，地址：{Address}", address);
                    }
                    else if (S7Communication.IsProtocolSynchronizationFailure(result))
                    {
                        await HandleProtocolSynchronizationFailureAsync("写入", address, result.Message).ConfigureAwait(false);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Logger?.LogError(ex, "写入S7数据时发生异常，地址：{Address}", address);
                    
                    // 返回失败结果
                    return OperationResult.CreateFailedResult(ex, WriteErrorCodes.ProtocolException);
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
                        return OperationResult.CreateFailedResult(WriteErrorCodes.ConnectionNotEstablished, $"短连接模式连接失败：{connectResult.Message}");
                    }
                    
                    connected = true;
                    
                    // 执行写入操作
                    var result = await base.WriteAsync(address, data, dataType, isBit, cancellationToken).ConfigureAwait(false);
                    
                    // 记录成功的写入操作
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug("短连接模式成功写入S7数据，地址：{Address}", address);
                    }
                    else if (S7Communication.IsProtocolSynchronizationFailure(result))
                    {
                        await HandleProtocolSynchronizationFailureAsync("写入", address, result.Message).ConfigureAwait(false);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Logger?.LogError(ex, "短连接模式写入S7数据时发生异常，地址：{Address}", address);
                    return OperationResult.CreateFailedResult(ex, WriteErrorCodes.ProtocolException);
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

        public override async ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses, CancellationToken cancellationToken = default)
        {
            if (IsLongConnection)
            {
                if (!Connected)
                {
                    return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>($"S7客户端没有连接 ip:{IPEndPoint.Address}");
                }

                var result = await base.BatchReadAsync(addresses, cancellationToken).ConfigureAwait(false);
                LogBatchReadDispatch(result);
                return result;
            }

            try
            {
                await DisconnectAsync().ConfigureAwait(false);
                var connectResult = await ConnectAsync(cancellationToken).ConfigureAwait(false);
                if (!connectResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>($"短连接模式连接失败：{connectResult.Message}");
                }
                var result = await base.BatchReadAsync(addresses, cancellationToken).ConfigureAwait(false);
                LogBatchReadDispatch(result);
                return result;
            }
            catch (Exception ex) { return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>(ex); }
            finally { await DisconnectAsync().ConfigureAwait(false); }
        }

        public override async ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses, CancellationToken cancellationToken = default)
        {
            if (IsLongConnection)
            {
                if (!Connected)
                {
                    return OperationResult.CreateFailedResult(WriteErrorCodes.ConnectionNotEstablished, $"S7客户端没有连接 ip:{IPEndPoint.Address}");
                }

                var result = await base.BatchWriteAsync(addresses, cancellationToken).ConfigureAwait(false);
                LogBatchWriteDispatch(result);
                return result;
            }

            try
            {
                await DisconnectAsync().ConfigureAwait(false);
                var connectResult = await ConnectAsync(cancellationToken).ConfigureAwait(false);
                if (!connectResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult(WriteErrorCodes.ConnectionNotEstablished, $"短连接模式连接失败：{connectResult.Message}");
                }
                var result = await base.BatchWriteAsync(addresses, cancellationToken).ConfigureAwait(false);
                LogBatchWriteDispatch(result);
                return result;
            }
            catch (Exception ex) { return OperationResult.CreateFailedResult(ex, WriteErrorCodes.ProtocolException); }
            finally { await DisconnectAsync().ConfigureAwait(false); }
        }

        private void LogBatchReadDispatch(OperationResult<Dictionary<string, (DataTypeEnums, object)>> result)
        {
            if (result == null || string.IsNullOrEmpty(result.Message))
            {
                return;
            }

            const string pathPrefix = "BatchReadPath=";
            int pathIndex = result.Message.IndexOf(pathPrefix, StringComparison.Ordinal);
            if (pathIndex < 0)
            {
                return;
            }

            Logger?.LogInformation("S7批量读取完成，调度信息：{DispatchMessage}", result.Message);
        }

        private void LogBatchWriteDispatch(OperationResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.Message))
            {
                return;
            }

            const string pathPrefix = "BatchWritePath=";
            int pathIndex = result.Message.IndexOf(pathPrefix, StringComparison.Ordinal);
            if (pathIndex < 0)
            {
                return;
            }

            Logger?.LogInformation("S7批量写入完成，调度信息：{DispatchMessage}", result.Message);
        }
        
    }
}

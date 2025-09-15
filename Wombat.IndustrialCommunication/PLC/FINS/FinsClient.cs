using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Models;


namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS协议客户端
    /// </summary>
    public class FinsClient : FinsCommunication, IDeviceClient, IClient
    {
        private TcpClientAdapter _tcpClientAdapter;
        private FinsEthernetTransport _transport;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ipAddress">IP地址</param>
        /// <param name="port">端口号，默认9600</param>
        /// <param name="timeout">超时时间</param>
        public FinsClient(string ipAddress, int port = 9600, TimeSpan? timeout = null) 
            : base(new FinsEthernetTransport(new TcpClientAdapter(ipAddress, port)))
        {
            _tcpClientAdapter = (TcpClientAdapter)this.Transport.StreamResource;
            _transport = (FinsEthernetTransport)Transport;
            
            if (timeout.HasValue)
            {
                _tcpClientAdapter.ConnectTimeout = timeout.Value;
                _tcpClientAdapter.ReceiveTimeout = timeout.Value;
                _tcpClientAdapter.SendTimeout = timeout.Value;
            }
        }

        /// <summary>
        /// IP地址
        /// </summary>
        public string IpAddress => _tcpClientAdapter.IpAddress;

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port => _tcpClientAdapter.Port;

        /// <summary>
        /// 超时时间
        /// </summary>
        public TimeSpan Timeout 
        { 
            get => _tcpClientAdapter.ConnectTimeout; 
            set 
            {
                _tcpClientAdapter.ConnectTimeout = value;
                _tcpClientAdapter.ReceiveTimeout = value;
                _tcpClientAdapter.SendTimeout = value;
            }
        }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _tcpClientAdapter?.Connected ?? false;

        /// <summary>
        /// 是否已连接（IClient接口实现）
        /// </summary>
        public bool Connected => IsConnected;



        /// <summary>
        /// 日志记录器
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// 是否长连接
        /// </summary>
        public bool IsLongConnection { get; set; } = true;

        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        public bool EnableAutoReconnect { get; set; } = true;

        /// <summary>
        /// 重连延迟时间
        /// </summary>
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 上次重连尝试时间
        /// </summary>
        private DateTime _lastReconnectAttempt = DateTime.MinValue;

        /// <summary>
        /// 重试次数
        /// </summary>
        public int Retries
        {
            get => _transport?.Retries ?? 2;
            set { if (_transport != null) _transport.Retries = value; }
        }

        /// <summary>
        /// 重试等待时间
        /// </summary>
        public TimeSpan WaitToRetryMilliseconds
        {
            get => _transport?.WaitToRetryMilliseconds ?? TimeSpan.FromMilliseconds(100);
            set { if (_transport != null) _transport.WaitToRetryMilliseconds = value; }
        }

        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout
        {
            get => _tcpClientAdapter?.ConnectTimeout ?? TimeSpan.FromSeconds(3);
            set { if (_tcpClientAdapter != null) _tcpClientAdapter.ConnectTimeout = value; }
        }

        /// <summary>
        /// 接收超时时间
        /// </summary>
        public TimeSpan ReceiveTimeout
        {
            get => _tcpClientAdapter?.ReceiveTimeout ?? TimeSpan.FromSeconds(3);
            set { if (_tcpClientAdapter != null) _tcpClientAdapter.ReceiveTimeout = value; }
        }

        /// <summary>
        /// 发送超时时间
        /// </summary>
        public TimeSpan SendTimeout
        {
            get => _tcpClientAdapter?.SendTimeout ?? TimeSpan.FromSeconds(3);
            set { if (_tcpClientAdapter != null) _tcpClientAdapter.SendTimeout = value; }
        }

        /// <summary>
        /// 响应间隔时间
        /// </summary>
        public TimeSpan ResponseInterval
        {
            get => _transport?.ResponseInterval ?? TimeSpan.FromMilliseconds(50);
            set { if (_transport != null) _transport.ResponseInterval = value; }
        }

        /// <summary>
        /// 连接到PLC
        /// </summary>
        /// <returns>连接结果</returns>
        public async Task<OperationResult> ConnectAsync()
        {
            // 首先建立TCP连接
            var connectResult = await _tcpClientAdapter.ConnectAsync();
            if (!connectResult.IsSuccess)
            {
                return connectResult;
            }

            // 然后进行FINS协议初始化
            var initResult = await InitAsync(Timeout);
            if (!initResult.IsSuccess)
            {
                await _tcpClientAdapter.DisconnectAsync(); // 如果初始化失败，断开TCP连接
                return initResult;
            }

            return OperationResult.CreateSuccessResult("FINS客户端连接成功");
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns>断开连接结果</returns>
        public async Task<OperationResult> DisconnectAsync()
        {
            return await _tcpClientAdapter.DisconnectAsync();
        }

        /// <summary>
        /// 检查连接状态并在必要时执行自动重连
        /// </summary>
        /// <returns>重连结果</returns>
        public async Task<OperationResult> CheckAndReconnectAsync()
        {
            try
            {
                // 检查当前连接状态
                if (IsConnected)
                {
                    return OperationResult.CreateSuccessResult("连接正常");
                }

                // 检查是否启用自动重连
                if (!EnableAutoReconnect)
                {
                    return OperationResult.CreateFailedResult("自动重连已禁用");
                }

                // 检查重连间隔时间
                var timeSinceLastAttempt = DateTime.Now - _lastReconnectAttempt;
                if (timeSinceLastAttempt < ReconnectDelay)
                {
                    var remainingTime = ReconnectDelay - timeSinceLastAttempt;
                    return OperationResult.CreateFailedResult($"重连间隔未到，还需等待 {remainingTime.TotalSeconds:F1} 秒");
                }

                // 更新重连尝试时间
                _lastReconnectAttempt = DateTime.Now;

                // 记录重连尝试
                Logger?.LogInformation("开始尝试自动重连到FINS设备");

                // 执行重连
                var reconnectResult = await ConnectAsync().ConfigureAwait(false);
                if (reconnectResult.IsSuccess)
                {
                    Logger?.LogInformation("FINS设备自动重连成功");
                }
                else
                {
                    Logger?.LogError($"FINS设备自动重连失败: {reconnectResult.Message}");
                }

                return reconnectResult;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "FINS设备自动重连过程中发生异常");
                return OperationResult.CreateFailedResult($"自动重连异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 同步连接到PLC
        /// </summary>
        /// <returns>连接结果</returns>
        public OperationResult Connect()
        {
            return Task.Run(async () => await ConnectAsync()).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 同步断开连接
        /// </summary>
        /// <returns>断开连接结果</returns>
        public OperationResult Disconnect()
        {
            return Task.Run(async () => await DisconnectAsync()).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 内部读取方法，实现连接管理和自动重连
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length">长度</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isBit">是否为位操作</param>
        /// <returns>读取结果</returns>
        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, DataTypeEnums dataType, bool isBit = false)
        {
            if (IsLongConnection)
            {
                // 长连接模式：检查连接状态并自动重连
                try
                {
                    if (!IsConnected && EnableAutoReconnect)
                    {
                        var reconnectResult = await CheckAndReconnectAsync().ConfigureAwait(false);
                        if (!reconnectResult.IsSuccess)
                        {
                            Logger?.LogError($"FINS读取操作失败，自动重连失败: {reconnectResult.Message}");
                            return OperationResult.CreateFailedResult<byte[]>($"连接失败: {reconnectResult.Message}");
                        }
                    }

                    // 执行读取操作
                    Logger?.LogDebug($"FINS长连接模式读取: 地址={address}, 长度={length}, 数据类型={dataType}");
                    var result = await base.ReadAsync(address, length, dataType, isBit).ConfigureAwait(false);
                    
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug($"FINS读取成功: 地址={address}, 数据长度={result.ResultValue?.Length ?? 0}");
                    }
                    else
                    {
                        Logger?.LogError($"FINS读取失败: 地址={address}, 错误={result.Message}");
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"FINS长连接读取异常: 地址={address}");
                    return OperationResult.CreateFailedResult<byte[]>($"读取异常: {ex.Message}");
                }
            }
            else
            {
                // 短连接模式：每次操作建立新连接
                try
                {
                    // 断开现有连接
                    if (IsConnected)
                    {
                        await DisconnectAsync().ConfigureAwait(false);
                    }

                    // 建立新连接
                    Logger?.LogDebug($"FINS短连接模式读取: 建立连接, 地址={address}");
                    var connectResult = await ConnectAsync().ConfigureAwait(false);
                    if (!connectResult.IsSuccess)
                    {
                        Logger?.LogError($"FINS短连接读取失败，连接建立失败: {connectResult.Message}");
                        return OperationResult.CreateFailedResult<byte[]>($"连接失败: {connectResult.Message}");
                    }

                    try
                    {
                        // 执行读取操作
                        Logger?.LogDebug($"FINS短连接模式读取: 地址={address}, 长度={length}, 数据类型={dataType}");
                        var result = await base.ReadAsync(address, length, dataType, isBit).ConfigureAwait(false);
                        
                        if (result.IsSuccess)
                        {
                            Logger?.LogDebug($"FINS短连接读取成功: 地址={address}, 数据长度={result.ResultValue?.Length ?? 0}");
                        }
                        else
                        {
                            Logger?.LogError($"FINS短连接读取失败: 地址={address}, 错误={result.Message}");
                        }
                        
                        return result;
                    }
                    finally
                    {
                        // 操作完成后断开连接
                        try
                        {
                            await DisconnectAsync().ConfigureAwait(false);
                            Logger?.LogDebug("FINS短连接模式读取: 连接已断开");
                        }
                        catch (Exception disconnectEx)
                        {
                            Logger?.LogWarning(disconnectEx, "FINS短连接读取: 断开连接时发生异常");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"FINS短连接读取异常: 地址={address}");
                    return OperationResult.CreateFailedResult<byte[]>($"读取异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length">长度</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, DataTypeEnums dataType = DataTypeEnums.UInt16)
        {
            return await base.ReadAsync(address, length, dataType);
        }





        /// <summary>
        /// 读取字符串
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length">长度</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<string>> ReadStringAsync(string address, ushort length)
        {
            return await base.ReadStringAsync(address, length);
        }

        /// <summary>
        /// 内部写入方法，实现连接管理和自动重连
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="data">数据</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isBit">是否为位操作</param>
        /// <returns>写入结果</returns>
        internal override async Task<OperationResult> WriteAsync(string address, byte[] data, DataTypeEnums dataType, bool isBit = false)
        {
            if (IsLongConnection)
            {
                // 长连接模式：检查连接状态并自动重连
                try
                {
                    if (!IsConnected && EnableAutoReconnect)
                    {
                        var reconnectResult = await CheckAndReconnectAsync().ConfigureAwait(false);
                        if (!reconnectResult.IsSuccess)
                        {
                            Logger?.LogError($"FINS写入操作失败，自动重连失败: {reconnectResult.Message}");
                            return OperationResult.CreateFailedResult($"连接失败: {reconnectResult.Message}");
                        }
                    }

                    // 执行写入操作
                    Logger?.LogDebug($"FINS长连接模式写入: 地址={address}, 数据长度={data?.Length ?? 0}, 数据类型={dataType}");
                    var result = await base.WriteAsync(address, data, dataType, isBit).ConfigureAwait(false);
                    
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug($"FINS写入成功: 地址={address}, 数据长度={data?.Length ?? 0}");
                    }
                    else
                    {
                        Logger?.LogError($"FINS写入失败: 地址={address}, 错误={result.Message}");
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"FINS长连接写入异常: 地址={address}");
                    return OperationResult.CreateFailedResult($"写入异常: {ex.Message}");
                }
            }
            else
            {
                // 短连接模式：每次操作建立新连接
                try
                {
                    // 断开现有连接
                    if (IsConnected)
                    {
                        await DisconnectAsync().ConfigureAwait(false);
                    }

                    // 建立新连接
                    Logger?.LogDebug($"FINS短连接模式写入: 建立连接, 地址={address}");
                    var connectResult = await ConnectAsync().ConfigureAwait(false);
                    if (!connectResult.IsSuccess)
                    {
                        Logger?.LogError($"FINS短连接写入失败，连接建立失败: {connectResult.Message}");
                        return OperationResult.CreateFailedResult($"连接失败: {connectResult.Message}");
                    }

                    try
                    {
                        // 执行写入操作
                        Logger?.LogDebug($"FINS短连接模式写入: 地址={address}, 数据长度={data?.Length ?? 0}, 数据类型={dataType}");
                        var result = await base.WriteAsync(address, data, dataType, isBit).ConfigureAwait(false);
                        
                        if (result.IsSuccess)
                        {
                            Logger?.LogDebug($"FINS短连接写入成功: 地址={address}, 数据长度={data?.Length ?? 0}");
                        }
                        else
                        {
                            Logger?.LogError($"FINS短连接写入失败: 地址={address}, 错误={result.Message}");
                        }
                        
                        return result;
                    }
                    finally
                    {
                        // 操作完成后断开连接
                        try
                        {
                            await DisconnectAsync().ConfigureAwait(false);
                            Logger?.LogDebug("FINS短连接模式写入: 连接已断开");
                        }
                        catch (Exception disconnectEx)
                        {
                            Logger?.LogWarning(disconnectEx, "FINS短连接写入: 断开连接时发生异常");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, $"FINS短连接写入异常: 地址={address}");
                    return OperationResult.CreateFailedResult($"写入异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteAsync(string address, object value, DataTypeEnums dataType = DataTypeEnums.UInt16)
        {
            // 将object转换为byte[]数组
            byte[] data;
            try
            {
                data = ConvertValueToBytes(value, dataType);
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"数据转换失败: {ex.Message}");
            }

            return await base.WriteAsync(address, data, dataType);
        }

        /// <summary>
        /// 将值转换为字节数组
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>字节数组</returns>
        private byte[] ConvertValueToBytes(object value, DataTypeEnums dataType)
        {
            switch (dataType)
            {
                case DataTypeEnums.Bool:
                    return new byte[] { (bool)value ? (byte)1 : (byte)0 };
                case DataTypeEnums.Byte:
                    return new byte[] { (byte)value };
                case DataTypeEnums.UInt16:
                    return BitConverter.GetBytes((ushort)value);
                case DataTypeEnums.Int16:
                    return BitConverter.GetBytes((short)value);
                case DataTypeEnums.UInt32:
                    return BitConverter.GetBytes((uint)value);
                case DataTypeEnums.Int32:
                    return BitConverter.GetBytes((int)value);
                case DataTypeEnums.Float:
                    return BitConverter.GetBytes((float)value);
                case DataTypeEnums.Double:
                    return BitConverter.GetBytes((double)value);
                case DataTypeEnums.String:
                    return System.Text.Encoding.UTF8.GetBytes(value.ToString());
                default:
                    throw new ArgumentException($"不支持的数据类型: {dataType}");
            }
        }





        /// <summary>
        /// 写入字符串
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="length">长度</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteStringAsync(string address, string value, int length = -1)
        {
            return await base.WriteStringAsync(address, value);
        }

        /// <summary>
        /// 优化批量读取（使用FinsBatchHelper）
        /// </summary>
        /// <param name="addresses">地址数据列表</param>
        /// <param name="minEfficiencyRatio">最小效率比（默认0.8）</param>
        /// <param name="maxBlockSize">最大块大小（默认180字）</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> OptimizedBatchReadAsync(
            Dictionary<string, (DataTypeEnums, object)> addresses, 
            double minEfficiencyRatio = 0.8, 
            int maxBlockSize = 180)
        {
            try
            {
                // 解析地址
                var addressInfos = FinsBatchHelper.ParseFinsAddresses(addresses);
                if (addressInfos.Count == 0)
                {
                    return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object) >> ("没有有效的地址需要读取");
                }

                // 优化地址块
                var optimizedBlocks = FinsBatchHelper.OptimizeFinsAddressBlocks(addressInfos, minEfficiencyRatio, maxBlockSize);
                
                // 执行批量读取
                var blockData = new Dictionary<string, byte[]>();
                var readTasks = new List<Task>();
                
                foreach (var block in optimizedBlocks)
                {
                    readTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // 构造读取地址
                            var readAddress = $"{FinsBatchHelper.GetFinsAreaPrefix(block.MemoryArea)}{block.StartAddress}";
                            
                            // 执行读取
                            var readResult = await ReadAsync(readAddress, (ushort)block.TotalLength);
                            if (readResult.IsSuccess)
                            {
                                string blockKey = $"{block.MemoryArea}_{block.StartAddress}_{block.TotalLength}";
                                lock (blockData)
                                {
                                    blockData[blockKey] = readResult.ResultValue;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 记录读取失败的块
                            Logger?.LogError($"读取块失败: {block.MemoryArea}_{block.StartAddress}_{block.TotalLength}, 错误: {ex.Message}");
                        }
                    }));
                }
                
                // 等待所有读取任务完成
                await Task.WhenAll(readTasks);
                
                // 从块数据中提取各个地址的值
                var extractedData = FinsBatchHelper.ExtractDataFromFinsBlocks(blockData, optimizedBlocks, addressInfos);
                
                // 转换为带数据类型的结果
                var result = new Dictionary<string, (DataTypeEnums, object)>();
                foreach (var kvp in extractedData)
                {
                    var addressInfo = addressInfos.FirstOrDefault(a => a.OriginalAddress == kvp.Key);
                    if (addressInfo.OriginalAddress != null) // 结构体不能与null比较，检查其字段
                    {
                        result[kvp.Key] = (addressInfo.TargetDataType, kvp.Value);
                    }
                }
                
                return OperationResult.CreateSuccessResult(result);
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>($"优化批量读取失败: {ex.Message}");

            }
        }

        /// <summary>
        /// 优化批量写入（使用FinsBatchHelper）
        /// </summary>
        /// <param name="addresses">地址数据列表</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> OptimizedBatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            try
            {
                // 解析地址
                var addressInfos = FinsBatchHelper.ParseFinsAddresses(addresses);
                if (addressInfos.Count == 0)
                {
                    return OperationResult.CreateFailedResult("没有有效的地址需要写入");
                }

                var writeTasks = new List<Task<OperationResult>>();
                
                // 对每个地址执行写入
                foreach (var addressInfo in addressInfos)
                {
                    writeTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // 获取要写入的值
                            var originalAddress = addressInfo.OriginalAddress;
                            if (!addresses.TryGetValue(originalAddress, out var addressData))
                            {
                                return OperationResult.CreateFailedResult($"未找到地址 {originalAddress} 的数据");
                            }

                            var value = addressData.Item2;
                            
                            // 转换值为字节数组
                            var bytes = FinsBatchHelper.ConvertValueToFinsBytes(value, addressInfo, this.DataFormat);
                            if (bytes == null)
                            {
                                return OperationResult.CreateFailedResult($"地址 {originalAddress} 的值转换失败");
                            }

                            // 构造写入地址
                            var writeAddress = FinsBatchHelper.ConstructFinsWriteAddress(addressInfo);
                            if (string.IsNullOrEmpty(writeAddress))
                            {
                                return OperationResult.CreateFailedResult($"地址 {originalAddress} 的写入地址构造失败");
                            }

                            // 执行写入
                            return await WriteAsync(writeAddress, bytes);
                        }
                        catch (Exception ex)
                        {
                            return OperationResult.CreateFailedResult($"写入地址 {addressInfo.OriginalAddress} 失败: {ex.Message}");
                        }
                    }));
                }
                
                // 等待所有写入任务完成
                var results = await Task.WhenAll(writeTasks);
                
                // 检查是否有失败的写入
                var failedResults = results.Where(r => !r.IsSuccess).ToList();
                if (failedResults.Count > 0)
                {
                    var errorMessages = failedResults.Select(r => r.Message).ToArray();
                    return OperationResult.CreateFailedResult($"部分写入失败: {string.Join("; ", errorMessages)}");
                }
                
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"优化批量写入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量读取
        /// </summary>
        /// <param name="addresses">地址列表</param>
        /// <returns>读取结果</returns>
        public override async ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            if (addresses == null || addresses.Count == 0)
            {

                return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>("地址列表不能为空");
            }

            Logger?.LogDebug($"FINS批量读取开始，地址数量: {addresses.Count}");

            try
            {
                if (IsLongConnection)
                {
                    // 长连接模式：检查连接状态并自动重连
                    if (!IsConnected && EnableAutoReconnect)
                    {
                        Logger?.LogDebug("FINS批量读取：检测到连接断开，尝试自动重连");
                        var reconnectResult = await CheckAndReconnectAsync().ConfigureAwait(false);
                        if (!reconnectResult.IsSuccess)
                        {
                            Logger?.LogError($"FINS批量读取失败，自动重连失败: {reconnectResult.Message}");
                            return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>($"连接失败: {reconnectResult.Message}");
                        }
                        Logger?.LogDebug("FINS批量读取：自动重连成功");
                    }
                    
                    // 执行批量读取
                    var result = await base.BatchReadAsync(addresses).ConfigureAwait(false);
                    
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug($"FINS批量读取成功，读取了 {result.ResultValue?.Count ?? 0} 个地址");
                    }
                    else
                    {
                        Logger?.LogError($"FINS批量读取失败: {result.Message}");
                    }
                    
                    return result;
                }
                else
                {
                    // 短连接模式：每次操作建立新连接
                    Logger?.LogDebug("FINS批量读取：使用短连接模式");
                    
                    // 断开现有连接
                    if (IsConnected)
                    {
                        await DisconnectAsync().ConfigureAwait(false);
                    }
                    
                    try
                    {
                        // 建立新连接
                        var connectResult = await ConnectAsync().ConfigureAwait(false);
                        if (!connectResult.IsSuccess)
                        {
                            Logger?.LogError($"FINS批量读取失败，连接建立失败: {connectResult.Message}");
                            return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>($"连接失败: {connectResult.Message}");
                        }
                        
                        // 执行批量读取
                        var result = await base.BatchReadAsync(addresses).ConfigureAwait(false);
                        
                        if (result.IsSuccess)
                        {
                            Logger?.LogDebug($"FINS批量读取成功，读取了 {result.ResultValue?.Count ?? 0} 个地址");
                        }
                        else
                        {
                            Logger?.LogError($"FINS批量读取失败: {result.Message}");
                        }
                        
                        return result;
                    }
                    finally
                    {
                        // 断开连接
                        if (IsConnected)
                        {
                            await DisconnectAsync().ConfigureAwait(false);
                            Logger?.LogDebug("FINS批量读取：短连接模式，连接已断开");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"FINS批量读取异常: {ex.Message}");
                return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>($"批量读取失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量写入
        /// </summary>
        /// <param name="addresses">地址数据列表</param>
        /// <returns>写入结果</returns>
        public override async ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            if (addresses == null || addresses.Count == 0)
            {
                return OperationResult.CreateFailedResult("地址列表不能为空");
            }

            Logger?.LogDebug($"FINS批量写入开始，地址数量: {addresses.Count}");

            try
            {
                if (IsLongConnection)
                {
                    // 长连接模式：检查连接状态并自动重连
                    if (!IsConnected && EnableAutoReconnect)
                    {
                        Logger?.LogDebug("FINS批量写入：检测到连接断开，尝试自动重连");
                        var reconnectResult = await CheckAndReconnectAsync().ConfigureAwait(false);
                        if (!reconnectResult.IsSuccess)
                        {
                            Logger?.LogError($"FINS批量写入失败，自动重连失败: {reconnectResult.Message}");
                            return OperationResult.CreateFailedResult($"连接失败: {reconnectResult.Message}");
                        }
                        Logger?.LogDebug("FINS批量写入：自动重连成功");
                    }
                    
                    // 执行批量写入
                    var result = await base.BatchWriteAsync(addresses).ConfigureAwait(false);
                    
                    if (result.IsSuccess)
                    {
                        Logger?.LogDebug($"FINS批量写入成功，写入了 {addresses.Count} 个地址");
                    }
                    else
                    {
                        Logger?.LogError($"FINS批量写入失败: {result.Message}");
                    }
                    
                    return result;
                }
                else
                {
                    // 短连接模式：每次操作建立新连接
                    Logger?.LogDebug("FINS批量写入：使用短连接模式");
                    
                    // 断开现有连接
                    if (IsConnected)
                    {
                        await DisconnectAsync().ConfigureAwait(false);
                    }
                    
                    try
                    {
                        // 建立新连接
                        var connectResult = await ConnectAsync().ConfigureAwait(false);
                        if (!connectResult.IsSuccess)
                        {
                            Logger?.LogError($"FINS批量写入失败，连接建立失败: {connectResult.Message}");
                            return OperationResult.CreateFailedResult($"连接失败: {connectResult.Message}");
                        }
                        
                        // 执行批量写入
                        var result = await base.BatchWriteAsync(addresses).ConfigureAwait(false);
                        
                        if (result.IsSuccess)
                        {
                            Logger?.LogDebug($"FINS批量写入成功，写入了 {addresses.Count} 个地址");
                        }
                        else
                        {
                            Logger?.LogError($"FINS批量写入失败: {result.Message}");
                        }
                        
                        return result;
                    }
                    finally
                    {
                        // 断开连接
                        if (IsConnected)
                        {
                            await DisconnectAsync().ConfigureAwait(false);
                            Logger?.LogDebug("FINS批量写入：短连接模式，连接已断开");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"FINS批量写入异常: {ex.Message}");
                return OperationResult.CreateFailedResult($"批量写入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 批量写入（向后兼容重载）
        /// </summary>
        /// <param name="addresses">地址数据列表</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> BatchWriteAsync(Dictionary<string, object> addresses)
        {
            if (addresses == null || addresses.Count == 0)
            {
                return OperationResult.CreateFailedResult("地址列表不能为空");
            }

            // 转换参数格式
            var convertedAddresses = new Dictionary<string, (DataTypeEnums, object)>();
            foreach (var kvp in addresses)
            {
                // 根据值的类型推断DataTypeEnums
                var dataType = InferDataType(kvp.Value);
                convertedAddresses[kvp.Key] = (dataType, kvp.Value);
            }
            
            // 调用主BatchWriteAsync方法
            var result = await BatchWriteAsync(convertedAddresses).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// 根据值推断数据类型
        /// </summary>
        /// <param name="value">值</param>
        /// <returns>数据类型</returns>
        private DataTypeEnums InferDataType(object value)
        {
            if (value == null) return DataTypeEnums.None;
            
            switch (value)
            {
                case bool _:
                    return DataTypeEnums.Bool;
                case byte _:
                    return DataTypeEnums.Byte;
                case ushort _:
                    return DataTypeEnums.UInt16;
                case short _:
                    return DataTypeEnums.Int16;
                case uint _:
                    return DataTypeEnums.UInt32;
                case int _:
                    return DataTypeEnums.Int32;
                case float _:
                    return DataTypeEnums.Float;
                case double _:
                    return DataTypeEnums.Double;
                case string _:
                    return DataTypeEnums.String;
                default:
                    return DataTypeEnums.None;
            }
        }


    }
}
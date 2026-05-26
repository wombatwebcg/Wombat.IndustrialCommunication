using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.PLC;
using Wombat.Extensions.DataTypeExtensions;
using System.Threading;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// S7 地址数据类型枚举
    /// </summary>
    public enum S7DataType
    {
        DBX,  
        DBB, 
        DBW,  
        DBD,  
        
        I,    
        IB,   
        IW,   
        ID,   
        
        Q,    
        QB,  
        QW,  
        QD,  
        
        M,
        MB,  
        MW,   
        MD,  
        
        V,    
        VB,   
        VW,  
        VD 
    }

    /// <summary>
    /// S7 地址信息结构
    /// </summary>
    public struct S7AddressInfo
    {
        public string OriginalAddress { get; set; }
        public int DbNumber { get; set; }
        public int StartByte { get; set; }
        public int Length { get; set; }
        public S7DataType DataType { get; set; }
        public int BitOffset { get; set; }  // 位偏移，仅对位地址有效
    }

    /// <summary>
    /// S7 优化地址块
    /// </summary>
    public class S7AddressBlock
    {
        public int DbNumber { get; set; }
        public int StartByte { get; set; }
        public int TotalLength { get; set; }
        public List<S7AddressInfo> Addresses { get; set; } = new List<S7AddressInfo>();
        public double EfficiencyRatio { get; set; }
    }

    public class S7Communication : DeviceDataReaderWriterBase
    {
        internal AsyncLock _lock = new AsyncLock();
        private static ArrayPool<byte> _byteArrayPool = ArrayPool<byte>.Shared;

        public S7Communication(S7EthernetTransport s7EthernetTransport) :base(s7EthernetTransport)
        {
            DataFormat = Extensions.DataTypeExtensions.EndianFormat.ABCD;
            IsReverse = true;
        }

        public override string Version => SiemensVersion.ToString();

        /// <summary>
        /// 插槽号
        /// </summary>
        public byte Slot { get; set; }

        /// <summary>
        /// 机架号
        /// </summary>
        public byte Rack { get;set; }

        /// <summary>
        /// 批量读取地址块的最小效率比，小于等于0时使用helper默认值
        /// </summary>
        public double EfficiencyRatio { get; set; }

        /// <summary>
        /// 批量读取时，不同地址块之间的等待间隔。默认0表示不等待。
        /// </summary>
        public TimeSpan BatchReadStationInterval { get; set; } = TimeSpan.Zero;

        public SiemensVersion SiemensVersion{ get; set; }

        public async Task<OperationResult> InitAsync(TimeSpan connectTimeout)
        {
            using (var cts = new CancellationTokenSource(connectTimeout))
            {
                try
                {
                    using (await _lock.LockAsync(cts.Token))
                    {
                        return await InitCoreAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    return OperationResult.CreateFailedResult("S7 协议初始化超时");
                }
                catch (Exception ex)
                {
                    return OperationResult.CreateFailedResult($"S7 协议初始化异常: {ex.Message}");
                }
            }
        }

        internal async Task<OperationResult> InitWithoutLockAsync(TimeSpan connectTimeout)
        {
            using (var cts = new CancellationTokenSource(connectTimeout))
            {
                try
                {
                    return await InitCoreAsync();
                }
                catch (OperationCanceledException)
                {
                    return OperationResult.CreateFailedResult("S7 协议初始化超时");
                }
                catch (Exception ex)
                {
                    return OperationResult.CreateFailedResult($"S7 协议初始化异常: {ex.Message}");
                }
            }
        }

        private async Task<OperationResult> InitCoreAsync()
        {
            var result = new OperationResult();
            try
            {
                var command1 = BuildConnectCommand();
                var command2 = BuildSetupCommunicationCommand();

                var handshake1Result = await SendAndReceiveInitFrameAsync(command1, "首次握手");
                result.Requsts.Add(string.Join(" ", command1.Select(t => t.ToString("X2"))));
                if (!handshake1Result.IsSuccess)
                {
                    return OperationResult.CreateFailedResult(result, $"S7 初始化失败(首次握手): {handshake1Result.Message}");
                }
                result.Responses.Add(string.Join(" ", handshake1Result.ResultValue.Select(t => t.ToString("X2"))));

                var handshake1Validation = ValidateFirstHandshakeResponse(handshake1Result.ResultValue);
                if (!handshake1Validation.IsSuccess)
                {
                    return OperationResult.CreateFailedResult(result, handshake1Validation.Message);
                }

                var handshake2Result = await SendAndReceiveInitFrameAsync(command2, "二次握手");
                result.Requsts.Add(string.Join(" ", command2.Select(t => t.ToString("X2"))));
                if (!handshake2Result.IsSuccess)
                {
                    return OperationResult.CreateFailedResult(result, $"S7 初始化失败(二次握手): {handshake2Result.Message}");
                }
                result.Responses.Add(string.Join(" ", handshake2Result.ResultValue.Select(t => t.ToString("X2"))));

                var handshake2Validation = ValidateSecondHandshakeResponse(handshake2Result.ResultValue);
                if (!handshake2Validation.IsSuccess)
                {
                    return OperationResult.CreateFailedResult(result, handshake2Validation.Message);
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.ErrorCode = 408;
                result.Exception = ex;
            }
            return result.Complete();
        }

        private byte[] BuildConnectCommand()
        {
            byte[] command1;
            switch (SiemensVersion)
            {
                case SiemensVersion.S7_200:
                    command1 = SiemensConstant.Command1_200.ToArray();
                    break;
                case SiemensVersion.S7_200Smart:
                    command1 = SiemensConstant.Command1_200Smart.ToArray();
                    break;
                default:
                    command1 = SiemensConstant.Command1.ToArray();
                    break;
            }

            switch (SiemensVersion)
            {
                case SiemensVersion.S7_300:
                case SiemensVersion.S7_1200:
                case SiemensVersion.S7_1500:
                    command1[21] = (byte)((Rack * 0x20) + Slot);
                    break;
                case SiemensVersion.S7_400:
                    command1[21] = (byte)((Rack * 0x20) + Slot);
                    command1[17] = 0x00;
                    break;
                default:
                    break;
            }

            return command1;
        }

        private byte[] BuildSetupCommunicationCommand()
        {
            switch (SiemensVersion)
            {
                case SiemensVersion.S7_200:
                    return SiemensConstant.Command2_200.ToArray();
                case SiemensVersion.S7_200Smart:
                    return SiemensConstant.Command2_200Smart.ToArray();
                default:
                    return SiemensConstant.Command2.ToArray();
            }
        }

        private async Task<OperationResult<byte[]>> SendAndReceiveInitFrameAsync(byte[] command, string stageName)
        {
            var sendResult = await Transport.SendRequestAsync(command);
            if (!sendResult.IsSuccess)
            {
                return OperationResult.CreateFailedResult<byte[]>($"{stageName}发送失败: {sendResult.Message}");
            }

            var headerResult = await Transport.ReceiveResponseAsync(0, SiemensConstant.InitHeadLength);
            if (!headerResult.IsSuccess)
            {
                return OperationResult.CreateFailedResult<byte[]>($"{stageName}读取头失败: {headerResult.Message}");
            }

            var header = headerResult.ResultValue;
            if (header == null || header.Length < SiemensConstant.InitHeadLength)
            {
                return OperationResult.CreateFailedResult<byte[]>($"{stageName}头长度异常");
            }

            if (header[0] != 0x03 || header[1] != 0x00)
            {
                return OperationResult.CreateFailedResult<byte[]>($"{stageName}TPKT头无效: {header[0]:X2} {header[1]:X2}");
            }

            int totalLength = (header[2] << 8) | header[3];
            if (totalLength < 4 || totalLength > 4096)
            {
                return OperationResult.CreateFailedResult<byte[]>($"{stageName}TPKT长度无效: {totalLength}");
            }

            int contentLength = totalLength - 4;
            var payloadResult = contentLength > 0
                ? await Transport.ReceiveResponseAsync(0, contentLength)
                : OperationResult.CreateSuccessResult(Array.Empty<byte>());

            if (!payloadResult.IsSuccess)
            {
                return OperationResult.CreateFailedResult<byte[]>($"{stageName}读取内容失败: {payloadResult.Message}");
            }

            var fullFrame = new byte[4 + payloadResult.ResultValue.Length];
            Buffer.BlockCopy(header, 0, fullFrame, 0, 4);
            if (payloadResult.ResultValue.Length > 0)
            {
                Buffer.BlockCopy(payloadResult.ResultValue, 0, fullFrame, 4, payloadResult.ResultValue.Length);
            }

            return OperationResult.CreateSuccessResult(fullFrame);
        }

        private OperationResult ValidateFirstHandshakeResponse(byte[] response)
        {
            if (response == null || response.Length < 7)
            {
                return OperationResult.CreateFailedResult("S7 初始化失败: 首次握手响应长度不足");
            }

            if (response[5] != 0xD0 && response[5] != 0xE0)
            {
                return OperationResult.CreateFailedResult($"S7 初始化失败: 首次握手COTP类型异常 {response[5]:X2}");
            }

            return OperationResult.CreateSuccessResult();
        }

        private OperationResult ValidateSecondHandshakeResponse(byte[] response)
        {
            if (response == null || response.Length < 21)
            {
                return OperationResult.CreateFailedResult("S7 初始化失败: 二次握手响应长度不足");
            }

            if (response[5] != 0xF0)
            {
                return OperationResult.CreateFailedResult($"S7 初始化失败: 二次握手COTP类型异常 {response[5]:X2}");
            }

            if (response[7] != 0x32)
            {
                return OperationResult.CreateFailedResult($"S7 初始化失败: 二次握手协议ID异常 {response[7]:X2}");
            }

            if (response[8] != 0x03)
            {
                return OperationResult.CreateFailedResult($"S7 初始化失败: 二次握手ROSCTR异常 {response[8]:X2}");
            }

            if (response[17] != 0x00 || response[18] != 0x00)
            {
                return OperationResult.CreateFailedResult($"S7 初始化失败: 二次握手错误码 {response[17]:X2} {response[18]:X2}");
            }

            return OperationResult.CreateSuccessResult();
        }

        protected internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, DataTypeEnums dataType, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (Transport is S7EthernetTransport s7Transport)
                {
                    int maxCount = 180;
                    if (length > maxCount)
                    {
                        int alreadyFinished = 0;
                        List<byte> bytesContent = new List<byte>();
                        while (alreadyFinished < length)
                        {
                            ushort readLength = (ushort)Math.Min(length - alreadyFinished, maxCount);

                            var tempResult = await internalReadAsync(s7Transport, address, alreadyFinished, readLength, isBit);
                            if (tempResult.IsSuccess)
                            {
                                result.Requsts.Add(tempResult.Requsts[0]);
                                result.Responses.Add(tempResult.Responses[0]);
                                bytesContent.AddRange(tempResult.ResultValue);
                                alreadyFinished += readLength;
                            }
                            else
                            {
                                // 读取失败时直接返回，避免继续累加错误数据
                                return tempResult;
                            }
                        }

                        result.ResultValue = bytesContent.ToArray();
                        return result.Complete();
                    }
                    else
                    {
                        return await internalReadAsync(s7Transport, address, 0, length, isBit);
                    }
                }
                return OperationResult.CreateFailedResult<byte[]>();
            }

            async ValueTask<OperationResult<byte[]>> internalReadAsync(S7EthernetTransport transport, string internalAddress, int internalOffest, int internalLength, bool internalIsBit = false)
            {
                var tempResult = new OperationResult<byte>();
                var readRequest = new S7ReadRequest(internalAddress, internalOffest, internalLength, isBit);
                var response = await transport.UnicastReadMessageAsync(readRequest);
                if (response.IsSuccess)
                {
                    int realLength = internalLength;
                    var dataPackage = response.ResultValue.ProtocolMessageFrame;
                    byte[] responseData = new byte[realLength];
                    try
                    {
                        // 0x04 0x01 表示读取响应；21 位非 0xFF 时表示异常
                        if (dataPackage[19] == 0x04 && dataPackage[20] == 0x01)
                        {
                            if (dataPackage[21] == 0x0A && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取 {internalAddress} 失败，请确认地址是否存在";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] == 0x05 && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取 {internalAddress} 失败，请确认地址是否存在";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] != 0xFF)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取 {internalAddress} 失败，异常状态[{21}]:{dataPackage[21]}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                        }
                        if (internalIsBit) { realLength = (int)(Math.Ceiling(realLength / 8.0)); }
                        Array.Copy(dataPackage, dataPackage.Length - realLength, responseData, 0, realLength);
                    }
                    catch (Exception ex)
                    {
                        tempResult.Exception = ex;
                        tempResult.Message = $"{internalAddress} {internalOffest} {internalLength} 读取预期长度与返回数据长度不一致";
                        return OperationResult.CreateFailedResult<byte[]>(tempResult);
                    }
                    return new OperationResult<byte[]>(response, responseData).Complete();
                }
                else
                {
                    return OperationResult.CreateFailedResult<byte[]>(response);
                }
            }
        }

      protected internal override async Task<OperationResult> WriteAsync(string address, byte[] data,DataTypeEnums dataType, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte> result = new OperationResult<byte>();
                if (Transport is S7EthernetTransport s7Transport)
                {
                    var writeRequest = new S7WriteRequest(address, 0, data, isBit);
                    var response = await s7Transport.UnicastWriteMessageAsync(writeRequest);
                    if (response.IsSuccess)
                    {
                        var dataPackage = response.ResultValue.ProtocolMessageFrame;
                        var offset = dataPackage.Length - 1;
                        if (dataPackage[offset] == 0x0A)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入 {address} 失败，请确认地址是否存在，异常码[{offset}]:{dataPackage[offset]}";
                        }
                        else if (dataPackage[offset] == 0x05)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入 {address} 失败，请确认地址是否存在，异常码[{offset}]:{dataPackage[offset]}";
                        }
                        else if (dataPackage[offset] != 0xFF)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入 {address} 失败，异常状态[{offset}]:{dataPackage[offset]}";
                        }
                        return OperationResult.CreateSuccessResult(response);
                    }
                    else
                    {
                        return OperationResult.CreateFailedResult(response);
                    }
                }
                return OperationResult.CreateFailedResult();
            }
        }

        /// <summary>
        /// 批量读取数据
        /// </summary>
        /// <param name="addresses">地址字典，键为地址，值为数据类型</param>
        /// <returns>读取结果</returns>
        public override async ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult<Dictionary<string, (DataTypeEnums, object)>>();
                
                try
                {
                    // 参数校验
                    if (addresses == null || addresses.Count == 0)
                    {
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }

                    // 地址字典转换为内部格式
                    var internalAddresses = new Dictionary<string, (DataTypeEnums, object)>();
                    foreach (var kvp in addresses)
                    {
                        internalAddresses[kvp.Key] = (kvp.Value, null); // 读取时值为 null
                    }

                    // 解析地址信息
                    var addressInfos = S7BatchHelper.ParseS7Addresses(internalAddresses);
                    if (addressInfos.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "没有有效地址可读取";
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }

                    // 优先使用实例配置的效率比，未设置时回退到helper默认值
                    var minEfficiencyRatio = EfficiencyRatio > 0 ? EfficiencyRatio : 0.8;
                    var optimizedBlocks = S7BatchHelper.OptimizeS7AddressBlocks(addressInfos, minEfficiencyRatio);
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
                    var warnings = new List<string>();
                    var hasPreviousBlock = false;

                    foreach (var block in optimizedBlocks)
                    {
                        try
                        {
                            await DelayBeforeNextBatchReadAsync(hasPreviousBlock).ConfigureAwait(false);

                            if (block.Addresses.Count == 0)
                            {
                                errors.Add("地址块中没有地址信息");
                                continue;
                            }

                            var readResult = await ReadBlockWithBoundaryFallbackAsync(block);
                            
                            if (readResult.IsSuccess)
                            {
                                blockDataDict[BuildBatchReadBlockKey(block)] = readResult.ResultValue;
                                
                                // 合并请求和响应记录
                                if (readResult.Requsts != null && readResult.Requsts.Count > 0)
                                {
                                    result.Requsts.AddRange(readResult.Requsts);
                                }
                                if (readResult.Responses != null && readResult.Responses.Count > 0)
                                {
                                    result.Responses.AddRange(readResult.Responses);
                                }
                                if (!string.IsNullOrEmpty(readResult.Message))
                                {
                                    warnings.Add(readResult.Message);
                                }
                            }
                            else
                            {
                                errors.Add($"读取块 {DescribeBatchReadBlock(block)} 失败: {readResult.Message}");
                            }

                            hasPreviousBlock = true;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"读取块 {DescribeBatchReadBlock(block)} 异常: {ex.Message}");
                            hasPreviousBlock = true;
                        }
                    }

                    if (errors.Count > 0)
                    {
                        result.IsSuccess = blockDataDict.Count > 0; // 允许部分成功
                        result.Message = string.Join("; ", errors.Concat(warnings));
                    }
                    else
                    {
                        result.IsSuccess = true;
                        if (warnings.Count > 0)
                        {
                            result.Message = string.Join("; ", warnings);
                        }
                    }

                    // 从块数据中提取各地址对应值
                    var extractedData = S7BatchHelper.ExtractDataFromS7Blocks(blockDataDict, optimizedBlocks, addressInfos);

                    // 转换为返回格式
                    var finalResult = new Dictionary<string, (DataTypeEnums, object)>();
                    foreach (var kvp in addresses)
                    {
                        var address = kvp.Key;
                        var dataType = kvp.Value;
                        
                        if (extractedData.TryGetValue(address, out var value))
                        {
                            finalResult[address] = (dataType, value);
                        }
                        else
                        {
                            finalResult[address] = (dataType, null);
                        }
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

        private async Task DelayBeforeNextBatchReadAsync(bool hasPreviousBlock)
        {
            if (!hasPreviousBlock || BatchReadStationInterval <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(BatchReadStationInterval).ConfigureAwait(false);
        }

        private async ValueTask<OperationResult<byte[]>> ReadBlockWithBoundaryFallbackAsync(S7BatchHelper.S7AddressBlock block)
        {
            var initialResult = await ReadBatchBlockAsync(block);
            if (initialResult.IsSuccess || !ShouldShrinkBlockBoundary(block, initialResult))
            {
                return initialResult;
            }

            var originalLength = block.TotalLength;
            var bestLength = 0;
            OperationResult<byte[]> bestResult = null;
            OperationResult<byte[]> lastFailure = initialResult;
            var mergedRequests = new List<string>();
            var mergedResponses = new List<string>();
            MergeBatchReadLogs(initialResult, mergedRequests, mergedResponses);

            int low = 1;
            int high = originalLength - 1;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                var retryResult = await ReadBatchBlockAsync(block, mid);
                MergeBatchReadLogs(retryResult, mergedRequests, mergedResponses);

                if (retryResult.IsSuccess)
                {
                    bestLength = mid;
                    bestResult = retryResult;
                    low = mid + 1;
                }
                else
                {
                    lastFailure = retryResult;
                    high = mid - 1;
                }
            }

            if (bestResult != null)
            {
                var originalEndByte = block.StartByte + originalLength - 1;
                var actualEndByte = block.StartByte + bestLength - 1;
                block.TotalLength = bestLength;

                var successResult = new OperationResult<byte[]>
                {
                    IsSuccess = true,
                    ResultValue = bestResult.ResultValue,
                    Message = $"读取块 {DescribeBatchReadBlock(block.DbNumber, block.StartByte, originalLength, block)} 超出实际边界，已回退到 {DescribeBatchReadBlock(block)}"
                };
                successResult.Requsts.AddRange(mergedRequests);
                successResult.Responses.AddRange(mergedResponses);
                return successResult.Complete();
            }

            var failedResult = new OperationResult<byte[]>
            {
                IsSuccess = false,
                Message = lastFailure != null && !string.IsNullOrEmpty(lastFailure.Message)
                    ? lastFailure.Message
                    : $"读取块 {DescribeBatchReadBlock(block.DbNumber, block.StartByte, originalLength, block)} 失败，缩小边界后仍未成功"
            };
            failedResult.Requsts.AddRange(mergedRequests);
            failedResult.Responses.AddRange(mergedResponses);
            return failedResult;
        }

        private async ValueTask<OperationResult<byte[]>> ReadBatchBlockAsync(S7BatchHelper.S7AddressBlock block, int? overrideLength = null)
        {
            var readLength = overrideLength ?? block.TotalLength;
            var blockAddress = BuildBatchReadBlockAddress(block);
            if (string.IsNullOrEmpty(blockAddress))
            {
                return OperationResult.CreateFailedResult<byte[]>($"不支持的地址区域类型: {GetBatchReadAreaType(block)}");
            }

            return await ReadAsync(blockAddress, readLength, DataTypeEnums.Byte, false);
        }

        private static void MergeBatchReadLogs(OperationResult<byte[]> source, List<string> requests, List<string> responses)
        {
            if (source == null)
            {
                return;
            }

            if (source.Requsts != null && source.Requsts.Count > 0)
            {
                requests.AddRange(source.Requsts);
            }

            if (source.Responses != null && source.Responses.Count > 0)
            {
                responses.AddRange(source.Responses);
            }
        }

        private static bool ShouldShrinkBlockBoundary(S7BatchHelper.S7AddressBlock block, OperationResult<byte[]> readResult)
        {
            if (block == null || block.TotalLength <= 1 || block.Addresses.Count == 0 || readResult == null || readResult.IsSuccess)
            {
                return false;
            }

            var areaType = GetBatchReadAreaType(block);
            if (areaType != "DB" && areaType != "V")
            {
                return false;
            }

            if (string.IsNullOrEmpty(readResult.Message))
            {
                return true;
            }

            return readResult.Message.Contains("地址是否存在")
                || readResult.Message.Contains("长度不一致")
                || readResult.Message.Contains("超出")
                || readResult.Message.Contains("异常状态");
        }

        private static string BuildBatchReadBlockAddress(S7BatchHelper.S7AddressBlock block)
        {
            switch (GetBatchReadAreaType(block))
            {
                case "DB":
                    return $"DB{block.DbNumber}.DBB{block.StartByte}";
                case "I":
                    return $"IB{block.StartByte}";
                case "Q":
                    return $"QB{block.StartByte}";
                case "M":
                    return $"MB{block.StartByte}";
                case "V":
                    return $"VB{block.StartByte}";
                default:
                    return null;
            }
        }

        private static string BuildBatchReadBlockKey(S7BatchHelper.S7AddressBlock block)
        {
            switch (GetBatchReadAreaType(block))
            {
                case "DB":
                    return $"DB{block.DbNumber}_{block.StartByte}_{block.TotalLength}";
                case "I":
                    return $"I_{block.StartByte}_{block.TotalLength}";
                case "Q":
                    return $"Q_{block.StartByte}_{block.TotalLength}";
                case "M":
                    return $"M_{block.StartByte}_{block.TotalLength}";
                case "V":
                    return $"V_{block.StartByte}_{block.TotalLength}";
                default:
                    return $"{block.DbNumber}_{block.StartByte}_{block.TotalLength}";
            }
        }

        private static string DescribeBatchReadBlock(S7BatchHelper.S7AddressBlock block)
        {
            return DescribeBatchReadBlock(block.DbNumber, block.StartByte, block.TotalLength, block);
        }

        private static string DescribeBatchReadBlock(int dbNumber, int startByte, int totalLength, S7BatchHelper.S7AddressBlock block)
        {
            var areaType = GetBatchReadAreaType(block);
            var endByte = startByte + totalLength - 1;

            switch (areaType)
            {
                case "DB":
                    return $"DB{dbNumber}:{startByte}-{endByte}";
                case "V":
                    return $"V:{startByte}-{endByte}";
                case "I":
                case "Q":
                case "M":
                    return $"{areaType}:{startByte}-{endByte}";
                default:
                    return $"UNKNOWN:{startByte}-{endByte}";
            }
        }

        private static string GetBatchReadAreaType(S7BatchHelper.S7AddressBlock block)
        {
            if (block == null || block.Addresses.Count == 0)
            {
                return "UNKNOWN";
            }

            return S7BatchHelper.GetS7AreaType(block.Addresses[0].DataType);
        }

        /// <summary>
        /// 批量写入数据
        /// </summary>
        /// <param name="addresses">地址字典，键为地址，值为(数据类型, 值)</param>
        /// <returns>写入结果</returns>
        public override async ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                
                try
                {
                    // 参数校验
                    if (addresses == null || addresses.Count == 0)
                    {
                        return result.Complete();
                    }

                    // 解析地址信息
                    var addressInfos = S7BatchHelper.ParseS7Addresses(addresses);
                    if (addressInfos.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "没有有效地址可写入";
                        return result.Complete();
                    }

                    // 执行批量写入
                    var writeErrors = new List<string>();
                    var successCount = 0;

                    foreach (var addressInfo in addressInfos)
                    {
                        try
                        {
                            // 获取对应值
                            if (!addresses.TryGetValue(addressInfo.OriginalAddress, out var valueTuple))
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 没有对应的值");
                                continue;
                            }

                            var value = valueTuple.Item2;

                            // 值转换为字节数组
                            byte[] data = S7BatchHelper.ConvertValueToS7Bytes(value, addressInfo, IsReverse, DataFormat);
                            if (data == null)
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 数值转换失败");
                                continue;
                            }

                            // 构造写入地址
                            string writeAddress = S7BatchHelper.ConstructS7WriteAddress(addressInfo);
                            if (string.IsNullOrEmpty(writeAddress))
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 构造写入地址失败");
                                continue;
                            }

                            // 执行单点写入
                            var writeResult = await WriteAsync(writeAddress, data, DataTypeEnums.Byte, S7BatchHelper.IsBitType(addressInfo.DataType));
                            if (writeResult.IsSuccess)
                            {
                                successCount++;
                                // 合并请求和响应记录
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

                    // 设置结果
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
    }
}


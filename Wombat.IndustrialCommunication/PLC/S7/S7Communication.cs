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
    /// S7数据类型枚举
    /// </summary>
    public enum S7DataType
    {
        // DB区（数据块）
        DBX,  // DB位
        DBB,  // DB字节
        DBW,  // DB字
        DBD,  // DB双字
        
        // I区（输入区）- 位地址直接在解析时处理，不需要单独的IX类型
        IB,   // I区字节
        IW,   // I区字
        ID,   // I区双字
        
        // Q区（输出区）- 位地址直接在解析时处理，不需要单独的QX类型  
        QB,   // Q区字节
        QW,   // Q区字
        QD,   // Q区双字
        
        // M区（Merker内部存储区）
        MX,   // M区位
        MB,   // M区字节
        MW,   // M区字
        MD,   // M区双字
        
        // V区（Smart200专用，映射到DB1）
        VB,   // V区字节（Smart200用）
        VW,   // V区字（Smart200用）
        VD    // V区双字（Smart200用）
    }

    /// <summary>
    /// S7地址信息结构体
    /// </summary>
    public struct S7AddressInfo
    {
        public string OriginalAddress { get; set; }
        public int DbNumber { get; set; }
        public int StartByte { get; set; }
        public int Length { get; set; }
        public S7DataType DataType { get; set; }
        public int BitOffset { get; set; }  // 位偏移，仅对DBX类型有效
    }

    /// <summary>
    /// S7优化地址块
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

        public SiemensVersion SiemensVersion{ get; set; }

        public async Task<OperationResult> InitAsync(TimeSpan connectTimeout)
        {
            // 添加超时控制，防止死锁
            using (var cts = new CancellationTokenSource(connectTimeout))
            {
                try
                {
                    using (await _lock.LockAsync(cts.Token))
                    {
                        var result = new OperationResult();
                        try
                        {
                            var command1 = SiemensConstant.Command1;
                            var command2 = SiemensConstant.Command2;

                            switch (SiemensVersion)
                            {
                                case SiemensVersion.S7_200:
                                    command1 = SiemensConstant.Command1_200;
                                    command2 = SiemensConstant.Command2_200;
                                    break;
                                case SiemensVersion.S7_200Smart:
                                    command1 = SiemensConstant.Command1_200Smart;
                                    command2 = SiemensConstant.Command2_200Smart;
                                    break;
                                case SiemensVersion.S7_300:
                                    command1[21] = (byte)((Rack * 0x20) + Slot); //0x02;
                                    break;
                                case SiemensVersion.S7_400:
                                    command1[21] = (byte)((Rack * 0x20) + Slot); //0x03;
                                    command1[17] = 0x00;
                                    break;
                                case SiemensVersion.S7_1200:
                                    command1[21] = (byte)((Rack * 0x20) + Slot); //0x00;
                                    break;
                                case SiemensVersion.S7_1500:
                                    command1[21] = (byte)((Rack * 0x20) + Slot); //0x00;
                                    break;
                                default:
                                    command1[18] = 0x00;
                                    break;
                            }

                            result.Requsts.Add(string.Join(" ", command1.Select(t => t.ToString("X2"))));
                            var command1RequestResult = await Transport.SendRequestAsync(command1);
                            if (command1RequestResult.IsSuccess)
                            {
                                var response1Result = await Transport.ReceiveResponseAsync(0, SiemensConstant.InitHeadLength);
                                if (response1Result.IsSuccess)
                                {
                                    var response1 = response1Result.ResultValue;
                                    var response2Result = await Transport.ReceiveResponseAsync(0, S7CommonMethods.GetContentLength(response1));
                                    if (!response2Result.IsSuccess)
                                    {
                                        return response2Result;
                                    }
                                    var response2 = response1Result.ResultValue;
                                    result.Responses.Add(string.Join(" ", response1.Concat(response2).Select(t => t.ToString("X2"))));
                                }
                            }

                            result.Requsts.Add(string.Join(" ", command2.Select(t => t.ToString("X2"))));
                            //第二次初始化指令交换
                            var command2RequestResult = await Transport.SendRequestAsync(command2);
                            if (command2RequestResult.IsSuccess)
                            {
                                var response3Result = await Transport.ReceiveResponseAsync(0, SiemensConstant.InitHeadLength);
                                if (!response3Result.IsSuccess)
                                {
                                    return response3Result;
                                }
                                var response3 = response3Result.ResultValue;

                                var response4Result = await Transport.ReceiveResponseAsync(0, S7CommonMethods.GetContentLength(response3));
                                if (!response4Result.IsSuccess)
                                {
                                    return response4Result;
                                }
                                var response4 = response4Result.ResultValue;
                                result.Responses.Add(string.Join(" ", response3.Concat(response4).Select(t => t.ToString("X2"))));
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
                }
                catch (OperationCanceledException)
                {
                    return OperationResult.CreateFailedResult("S7协议初始化超时");
                }
                catch (Exception ex)
                {
                    return OperationResult.CreateFailedResult($"S7协议初始化异常: {ex.Message}");
                }
            }
        }

        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
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
                                // 读取失败，直接返回失败结果，避免无限循环
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
                        //0x04 读 0x01 读取一个长度 //如果是批量读取，批量读取方法里面有验证
                        if (dataPackage[19] == 0x04 && dataPackage[20] == 0x01)
                        {
                            if (dataPackage[21] == 0x0A && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取{internalAddress}失败，请确认是否存在地址{internalAddress}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] == 0x05 && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取{internalAddress}失败，请确认是否存在地址{internalAddress}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] != 0xFF)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取{internalAddress}失败，异常代码[{21}]:{dataPackage[21]}";
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

        internal override async Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
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
                            result.Message = $"写入{address}失败，请确认是否存在地址{address}，异常代码[{offset}]:{dataPackage[offset]}";
                        }
                        else if (dataPackage[offset] == 0x05)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入{address}失败，请确认是否存在地址{address}，异常代码[{offset}]:{dataPackage[offset]}";
                        }
                        else if (dataPackage[offset] != 0xFF)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入{address}失败，异常代码[{offset}]:{dataPackage[offset]}";
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
                    // 参数验证
                    if (addresses == null || addresses.Count == 0)
                    {
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }

                    // 将地址字典转换为内部格式
                    var internalAddresses = new Dictionary<string, (DataTypeEnums, object)>();
                    foreach (var kvp in addresses)
                    {
                        internalAddresses[kvp.Key] = (kvp.Value, null); // 读取时值为null
                    }

                    // 解析地址信息
                    var addressInfos = S7BatchHelper.ParseS7Addresses(internalAddresses);
                    if (addressInfos.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "没有有效的地址可以读取";
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }

                    // 优化地址块
                    var optimizedBlocks = S7BatchHelper.OptimizeS7AddressBlocks(addressInfos);
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
                            // 根据地址类型构造正确的块地址
                            string blockAddress = "";
                            string blockKey = "";
                            
                            if (block.Addresses.Count > 0)
                            {
                                var firstAddress = block.Addresses[0];
                                var areaType = S7BatchHelper.GetS7AreaType(firstAddress.DataType);
                                
                                switch (areaType)
                                {
                                    case "DB":
                                        blockAddress = $"DB{block.DbNumber}.DBB{block.StartByte}";
                                        blockKey = $"DB{block.DbNumber}_{block.StartByte}_{block.TotalLength}";
                                        break;
                                    case "I":
                                        blockAddress = $"IB{block.StartByte}";
                                        blockKey = $"I_{block.StartByte}_{block.TotalLength}";
                                        break;
                                    case "Q":
                                        blockAddress = $"QB{block.StartByte}";
                                        blockKey = $"Q_{block.StartByte}_{block.TotalLength}";
                                        break;
                                    case "M":
                                        blockAddress = $"MB{block.StartByte}";
                                        blockKey = $"M_{block.StartByte}_{block.TotalLength}";
                                        break;
                                    case "V":
                                        blockAddress = $"VB{block.StartByte}";
                                        blockKey = $"V_{block.StartByte}_{block.TotalLength}";
                                        break;
                                    default:
                                        errors.Add($"不支持的区域类型: {areaType}");
                                        continue;
                                }
                            }
                            else
                            {
                                errors.Add($"块中没有地址信息");
                                continue;
                            }
                            
                            // 直接调用底层读取方法，避免重复逻辑
                            var readResult = await ReadAsync(blockAddress, block.TotalLength, false);
                            
                            if (readResult.IsSuccess)
                            {
                                blockDataDict[blockKey] = readResult.ResultValue;
                                
                                // 合并请求和响应日志
                                result.Requsts.AddRange(readResult.Requsts);
                                result.Responses.AddRange(readResult.Responses);
                            }
                            else
                            {
                                var areaType = S7BatchHelper.GetS7AreaType(block.Addresses[0].DataType);
                                errors.Add($"读取块 {areaType}{(areaType == "DB" ? block.DbNumber.ToString() : "")}:{block.StartByte}-{block.StartByte + block.TotalLength - 1} 失败: {readResult.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            var areaType = block.Addresses.Count > 0 ? S7BatchHelper.GetS7AreaType(block.Addresses[0].DataType) : "UNKNOWN";
                            errors.Add($"读取块 {areaType}{(areaType == "DB" ? block.DbNumber.ToString() : "")}:{block.StartByte}-{block.StartByte + block.TotalLength - 1} 异常: {ex.Message}");
                        }
                    }

                    if (errors.Count > 0)
                    {
                        result.IsSuccess = blockDataDict.Count > 0; // 部分成功
                        result.Message = string.Join("; ", errors);
                    }
                    else
                    {
                        result.IsSuccess = true;
                    }

                    // 从块数据中提取各个地址的值
                    var extractedData = S7BatchHelper.ExtractDataFromS7Blocks(blockDataDict, optimizedBlocks, addressInfos);

                    // 转换为新的返回格式
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

        /// <summary>
        /// 批量写入数据
        /// </summary>
        /// <param name="addresses">地址字典，键为地址，值为(数据类型, 值)元组</param>
        /// <returns>写入结果</returns>
        public override async ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                
                try
                {
                    // 参数验证
                    if (addresses == null || addresses.Count == 0)
                    {
                        return result.Complete();
                    }

                    // 解析地址信息
                    var addressInfos = S7BatchHelper.ParseS7Addresses(addresses);
                    if (addressInfos.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "没有有效的地址可以写入";
                        return result.Complete();
                    }

                    // 执行批量写入
                    var writeErrors = new List<string>();
                    var successCount = 0;

                    foreach (var addressInfo in addressInfos)
                    {
                        try
                        {
                            // 获取对应的值
                            if (!addresses.TryGetValue(addressInfo.OriginalAddress, out var valueTuple))
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 没有对应的值");
                                continue;
                            }

                            var value = valueTuple.Item2;

                            // 将值转换为字节数组
                            byte[] data = S7BatchHelper.ConvertValueToS7Bytes(value, addressInfo, IsReverse, DataFormat);
                            if (data == null)
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 的值转换失败");
                                continue;
                            }

                            // 构造写入地址
                            string writeAddress = S7BatchHelper.ConstructS7WriteAddress(addressInfo);
                            if (string.IsNullOrEmpty(writeAddress))
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 构造写入地址失败");
                                continue;
                            }

                            // 执行单个写入
                            var writeResult = await WriteAsync(writeAddress, data, addressInfo.DataType == S7DataType.DBX);
                            if (writeResult.IsSuccess)
                            {
                                successCount++;
                                // 合并请求和响应日志
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


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
        private const int DefaultNativeRandomReadMaxItems = 19;
        private const int DefaultNativeRandomWriteMaxItems = 10;
        private const int DefaultNativeRandomReadMaxPayloadBytes = 180;
        private const int DefaultNativeRandomWriteMaxPayloadBytes = 180;
        private const double DefaultBlockReadMinEfficiency = 0.8d;
        private const int DefaultRandomReadPreferSingleLengthThreshold = 4;
        private const double DefaultBatchReadDispatchRequestWeight = 1.0d;
        private ushort _pduReference;

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

        public int NegotiatedPduLimit { get; set; } = 480;

        /// <summary>
        /// 批量读取时，不同地址块之间的等待间隔。默认0表示不等待。
        /// </summary>
        public TimeSpan BatchReadStationInterval { get; set; } = TimeSpan.Zero;

        public SiemensVersion SiemensVersion{ get; set; }

        public bool StrictPduReferenceValidation
        {
            get => Transport is S7EthernetTransport s7Transport
                ? s7Transport.StrictPduReferenceValidation
                : true;
            set
            {
                if (Transport is S7EthernetTransport s7Transport)
                {
                    s7Transport.StrictPduReferenceValidation = value;
                }
            }
        }

        protected ushort GetNextPduReference()
        {
            _pduReference++;
            if (_pduReference == 0)
            {
                _pduReference = 1;
            }

            return _pduReference;
        }

        internal static bool IsProtocolSynchronizationFailure(OperationResult result)
        {
            return result != null
                && !result.IsSuccess
                && IsProtocolSynchronizationFailureMessage(result.Message);
        }

        internal static bool IsProtocolSynchronizationFailureMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            return message.Contains("读取预期长度与返回数据长度不一致")
                || message.Contains("响应长度不足")
                || message.Contains("S7响应头长度不足")
                || message.Contains("响应参数或数据长度无效")
                || message.Contains("响应功能码异常")
                || message.Contains("S7响应PDU Reference不匹配");
        }

        internal virtual int GetNativeRandomReadMaxItems() => DefaultNativeRandomReadMaxItems;

        internal virtual int GetNativeRandomWriteMaxItems() => DefaultNativeRandomWriteMaxItems;

        internal virtual int GetNativeRandomReadMaxPayloadBytes() => DefaultNativeRandomReadMaxPayloadBytes;

        internal virtual int GetNativeRandomWriteMaxPayloadBytes() => DefaultNativeRandomWriteMaxPayloadBytes;

        internal virtual double GetBlockReadMinEfficiency() => DefaultBlockReadMinEfficiency;

        internal virtual int GetRandomReadPreferSingleLengthThreshold() => DefaultRandomReadPreferSingleLengthThreshold;

        internal virtual double GetBatchReadDispatchRequestWeight() => DefaultBatchReadDispatchRequestWeight;

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

                NegotiatedPduLimit = ExtractNegotiatedPduLength(handshake2Result.ResultValue);
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

        private static int ExtractNegotiatedPduLength(byte[] response)
        {
            if (response == null || response.Length < 27)
            {
                return 480;
            }

            int negotiated = (response[25] << 8) | response[26];
            return negotiated > 0 ? negotiated : 480;
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
                var readRequest = new S7ReadRequest(internalAddress, internalOffest, internalLength, internalIsBit, GetNextPduReference());
                var response = await transport.UnicastReadMessageAsync(readRequest);
                if (response.IsSuccess)
                {
                    int realLength = internalLength;
                    var dataPackage = response.ResultValue.ProtocolMessageFrame;
                    byte[] responseData = new byte[realLength];
                    try
                    {
                        if (dataPackage == null || dataPackage.Length < 25)
                        {
                            tempResult.IsSuccess = false;
                            tempResult.Message = $"{internalAddress} 响应长度不足";
                            return OperationResult.CreateFailedResult<byte[]>(tempResult);
                        }

                        var cotpTotalLength = 1 + dataPackage[4];
                        var s7Offset = 4 + cotpTotalLength;
                        if (s7Offset + 12 > dataPackage.Length)
                        {
                            tempResult.IsSuccess = false;
                            tempResult.Message = $"{internalAddress} S7响应头长度不足";
                            return OperationResult.CreateFailedResult<byte[]>(tempResult);
                        }

                        var parameterLength = (dataPackage[s7Offset + 6] << 8) | dataPackage[s7Offset + 7];
                        var dataLength = (dataPackage[s7Offset + 8] << 8) | dataPackage[s7Offset + 9];
                        var parameterOffset = s7Offset + 12;
                        var dataOffset = parameterOffset + parameterLength;

                        if (parameterOffset + parameterLength > dataPackage.Length || dataOffset + dataLength > dataPackage.Length)
                        {
                            tempResult.IsSuccess = false;
                            tempResult.Message = $"{internalAddress} 响应参数或数据长度无效";
                            return OperationResult.CreateFailedResult<byte[]>(tempResult);
                        }

                        // 读取响应参数区: 0x04 + itemCount；单项读取的数据区从 dataOffset 开始
                        if (parameterLength < 2 || dataPackage[parameterOffset] != 0x04)
                        {
                            tempResult.IsSuccess = false;
                            tempResult.Message = $"读取 {internalAddress} 失败，响应功能码异常";
                            return OperationResult.CreateFailedResult<byte[]>(tempResult);
                        }

                        byte returnCode = dataPackage[dataOffset];
                        byte transportSize = dataPackage[dataOffset + 1];
                        int payloadBitLength = (dataPackage[dataOffset + 2] << 8) | dataPackage[dataOffset + 3];
                        int payloadOffset = dataOffset + 4;

                        if (returnCode == 0x0A || returnCode == 0x05)
                        {
                            tempResult.IsSuccess = false;
                            tempResult.Message = $"读取 {internalAddress} 失败，请确认地址是否存在";
                            return OperationResult.CreateFailedResult<byte[]>(tempResult);
                        }

                        if (returnCode != 0xFF)
                        {
                            tempResult.IsSuccess = false;
                            tempResult.Message = $"读取 {internalAddress} 失败，异常状态[{dataOffset}]:{returnCode}";
                            return OperationResult.CreateFailedResult<byte[]>(tempResult);
                        }

                        if (internalIsBit)
                        {
                            realLength = (int)Math.Ceiling(realLength / 8.0);
                        }

                        int payloadByteLength = transportSize == 0x03
                            ? (int)Math.Ceiling(payloadBitLength / 8.0)
                            : (int)Math.Ceiling(payloadBitLength / 8.0);

                        if (payloadOffset + payloadByteLength > dataPackage.Length || payloadByteLength < realLength)
                        {
                            tempResult.IsSuccess = false;
                            tempResult.Message = $"{internalAddress} {internalOffest} {internalLength} 读取预期长度与返回数据长度不一致";
                            return OperationResult.CreateFailedResult<byte[]>(tempResult);
                        }

                        Array.Copy(dataPackage, payloadOffset, responseData, 0, realLength);
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
                    var writeRequest = new S7WriteRequest(address, 0, data, isBit, GetNextPduReference());
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
                        if (!result.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult(response);
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
                    if (addresses == null || addresses.Count == 0)
                    {
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }

                    var internalAddresses = new Dictionary<string, (DataTypeEnums, object)>();
                    foreach (var kvp in addresses)
                    {
                        internalAddresses[kvp.Key] = (kvp.Value, null);
                    }

                    var addressInfos = S7BatchHelper.ParseS7Addresses(internalAddresses);
                    if (addressInfos.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "没有有效地址可读取";
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }

                    var dispatchDecision = AnalyzeBatchReadDispatch(addressInfos);
                    OperationResult<Dictionary<string, (DataTypeEnums, object)>> pathResult;

                    if (dispatchDecision.Mode == S7BatchReadPathKind.NativeRandomRead)
                    {
                        pathResult = await BatchReadByNativeRandomAsync(addresses, addressInfos, dispatchDecision).ConfigureAwait(false);
                    }
                    else
                    {
                        pathResult = await BatchReadByBlockAsync(addresses, addressInfos, dispatchDecision).ConfigureAwait(false);
                    }

                    result.SetInfo(pathResult);
                    result.ResultValue = pathResult.ResultValue;
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

        internal virtual S7BatchReadDispatchAnalysis AnalyzeBatchReadDispatch(IReadOnlyList<S7BatchHelper.S7AddressInfo> addressInfos)
        {
            var decision = new S7BatchReadDispatchAnalysis
            {
                Mode = S7BatchReadPathKind.BlockRead,
                DecisionReason = "默认块读",
                AddressCount = addressInfos?.Count ?? 0
            };

            if (addressInfos == null || addressInfos.Count == 0)
            {
                decision.DecisionReason = "没有可用地址";
                return decision;
            }

            decision.RequestedBytes = addressInfos.Sum(t => t.Length);
            decision.MaxAddressLength = addressInfos.Max(t => t.Length);
            EstimateBlockReadCost(addressInfos, decision);
            EstimateNativeRandomReadCost(addressInfos, decision);

            if (addressInfos.Count > 1 && !IsContinuousAddressBatch(addressInfos))
            {
                decision.Mode = S7BatchReadPathKind.NativeRandomRead;
                decision.DecisionReason = $"检测到不连续地址，按固定{GetNativeRandomReadMaxItems()}项/包执行原生随机批量读";
                return decision;
            }

            if (decision.NativeBatchCount <= 0 || decision.NativeTotalBytes <= 0)
            {
                decision.DecisionReason = "随机批量读地址映射失败，回退块读";
                return decision;
            }

            ShouldUseNativeRandomRead(decision);
            return decision;
        }

        internal virtual bool IsContinuousAddressBatch(IReadOnlyList<S7BatchHelper.S7AddressInfo> addressInfos)
        {
            if (addressInfos == null || addressInfos.Count <= 1)
            {
                return true;
            }

            var ordered = addressInfos
                .OrderBy(t => S7BatchHelper.GetS7AreaType(t.DataType))
                .ThenBy(t => t.DbNumber)
                .ThenBy(t => t.StartByte)
                .ThenBy(t => t.BitOffset)
                .ToList();

            var first = ordered[0];
            string areaType = S7BatchHelper.GetS7AreaType(first.DataType);
            int dbNumber = first.DbNumber;
            int expectedNextByte = first.StartByte + Math.Max(first.Length, 1);

            for (int i = 1; i < ordered.Count; i++)
            {
                var current = ordered[i];
                if (!string.Equals(S7BatchHelper.GetS7AreaType(current.DataType), areaType, StringComparison.Ordinal)
                    || current.DbNumber != dbNumber
                    || current.StartByte != expectedNextByte)
                {
                    return false;
                }

                expectedNextByte = current.StartByte + Math.Max(current.Length, 1);
            }

            return true;
        }

        internal virtual void EstimateBlockReadCost(
            IReadOnlyList<S7BatchHelper.S7AddressInfo> addressInfos,
            S7BatchReadDispatchAnalysis decision)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            if (addressInfos == null || addressInfos.Count == 0)
            {
                decision.BlockCount = 0;
                decision.BlockReadBytes = 0;
                decision.BlockByteEfficiency = 0;
                return;
            }

            double minEfficiencyRatio = EfficiencyRatio > 0 ? EfficiencyRatio : GetBlockReadMinEfficiency();
            var optimizedBlocks = S7BatchHelper.OptimizeS7AddressBlocks(addressInfos.ToList(), minEfficiencyRatio);
            decision.BlockCount = optimizedBlocks.Count;
            decision.BlockReadBytes = optimizedBlocks.Sum(t => t.TotalLength);
            decision.BlockByteEfficiency = decision.BlockReadBytes <= 0
                ? 0
                : (double)decision.RequestedBytes / decision.BlockReadBytes;
        }

        internal virtual void EstimateNativeRandomReadCost(
            IReadOnlyList<S7BatchHelper.S7AddressInfo> addressInfos,
            S7BatchReadDispatchAnalysis decision)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            if (addressInfos == null || addressInfos.Count == 0)
            {
                decision.NativeBatchCount = 0;
                decision.NativeTotalBytes = 0;
                return;
            }

            var nativeItems = BuildReadItems(addressInfos);
            if (nativeItems.Count == 0)
            {
                decision.NativeBatchCount = 0;
                decision.NativeTotalBytes = 0;
                return;
            }

            var nativeBatches = SplitReadBatches(nativeItems);
            decision.NativeBatchCount = nativeBatches.Count;
            decision.NativeTotalBytes = nativeBatches.Sum(t => t.ResponseFrameLength);
        }

        internal virtual bool ShouldUseNativeRandomRead(S7BatchReadDispatchAnalysis decision)
        {
            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            if (decision.AddressCount == 1)
            {
                decision.Mode = S7BatchReadPathKind.BlockRead;
                decision.DecisionReason = "单地址读取，保留块读";
                return false;
            }

            if (decision.BlockCount >= decision.AddressCount
                && decision.AddressCount > 1
                && decision.MaxAddressLength <= GetRandomReadPreferSingleLengthThreshold())
            {
                decision.Mode = S7BatchReadPathKind.NativeRandomRead;
                decision.DecisionReason = "多离散短地址，优先原生随机批量读";
                return true;
            }

            if (decision.BlockByteEfficiency >= GetBlockReadMinEfficiency() && decision.BlockCount <= decision.NativeBatchCount)
            {
                decision.Mode = S7BatchReadPathKind.BlockRead;
                decision.DecisionReason = $"块读效率高({decision.BlockByteEfficiency:P2})，优先块读";
                return false;
            }

            if (decision.MaxAddressLength <= GetRandomReadPreferSingleLengthThreshold() && decision.BlockByteEfficiency < GetBlockReadMinEfficiency())
            {
                decision.Mode = S7BatchReadPathKind.NativeRandomRead;
                decision.DecisionReason = $"地址离散且单项长度较小，块读效率{decision.BlockByteEfficiency:P2}低于阈值{GetBlockReadMinEfficiency():P2}";
                return true;
            }

            double blockScore = decision.BlockReadBytes * Math.Max(GetBatchReadDispatchRequestWeight(), 0.1d);
            double nativeScore = decision.NativeTotalBytes;
            if (nativeScore < blockScore)
            {
                decision.Mode = S7BatchReadPathKind.NativeRandomRead;
                decision.DecisionReason = $"成本比较选择随机读，请求成本{nativeScore} < 块读成本{blockScore:F0}";
                return true;
            }

            decision.Mode = S7BatchReadPathKind.BlockRead;
            decision.DecisionReason = $"成本比较选择块读，请求成本{nativeScore} >= 块读成本{blockScore:F0}";
            return false;
        }

        private async ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadByBlockAsync(
            Dictionary<string, DataTypeEnums> addresses,
            List<S7BatchHelper.S7AddressInfo> addressInfos,
            S7BatchReadDispatchAnalysis dispatchDecision)
        {
            var result = new OperationResult<Dictionary<string, (DataTypeEnums, object)>>();
            double minEfficiencyRatio = EfficiencyRatio > 0 ? EfficiencyRatio : GetBlockReadMinEfficiency();
            var optimizedBlocks = S7BatchHelper.OptimizeS7AddressBlocks(addressInfos, minEfficiencyRatio);
            if (optimizedBlocks.Count == 0)
            {
                return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>("地址优化失败");
            }

            var blockDataDict = new Dictionary<string, byte[]>();
            var errors = new List<string>();
            var warnings = new List<string>();
            bool hasPreviousBlock = false;
            bool protocolSynchronizationFailure = false;

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

                    var readResult = await ReadBlockWithBoundaryFallbackAsync(block).ConfigureAwait(false);
                    MergeBatchReadLogs(readResult, result.Requsts, result.Responses);

                    if (readResult.IsSuccess)
                    {
                        blockDataDict[BuildBatchReadBlockKey(block)] = readResult.ResultValue;
                        if (!string.IsNullOrEmpty(readResult.Message))
                        {
                            warnings.Add(readResult.Message);
                        }
                    }
                    else
                    {
                        errors.Add($"读取块 {DescribeBatchReadBlock(block)} 失败: {readResult.Message}");
                        if (IsProtocolSynchronizationFailure(readResult))
                        {
                            protocolSynchronizationFailure = true;
                            break;
                        }
                    }

                    hasPreviousBlock = true;
                }
                catch (Exception ex)
                {
                    errors.Add($"读取块 {DescribeBatchReadBlock(block)} 异常: {ex.Message}");
                    if (IsProtocolSynchronizationFailureMessage(ex.Message))
                    {
                        protocolSynchronizationFailure = true;
                        break;
                    }

                    hasPreviousBlock = true;
                }
            }

            if (protocolSynchronizationFailure)
            {
                result.IsSuccess = false;
                    result.Message = BuildDispatchMessage(dispatchDecision, string.Join("; ", errors.Concat(warnings)));
                result.ResultValue = BuildFailedBatchReadResult(addresses);
                return result.Complete();
            }

            var extractedData = S7BatchHelper.ExtractDataFromS7Blocks(blockDataDict, optimizedBlocks, addressInfos);
            result.ResultValue = BuildBatchReadResult(addresses, extractedData);
            result.IsSuccess = errors.Count == 0 || blockDataDict.Count > 0;
            result.Message = BuildDispatchMessage(dispatchDecision, string.Join("; ", errors.Concat(warnings)).Trim().Trim(';'));
            return result.Complete();
        }

        private async ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadByNativeRandomAsync(
            Dictionary<string, DataTypeEnums> addresses,
            List<S7BatchHelper.S7AddressInfo> addressInfos,
            S7BatchReadDispatchAnalysis dispatchDecision)
        {
            var result = new OperationResult<Dictionary<string, (DataTypeEnums, object)>>();
            var items = BuildReadItems(addressInfos);
            var batches = SplitReadBatches(items);
            var values = new Dictionary<string, object>();
            var errors = new List<string>();

            foreach (var batch in batches)
            {
                var batchResult = await ExecuteReadBatchAsync(batch).ConfigureAwait(false);
                MergeBatchReadLogs(batchResult, result.Requsts, result.Responses);

                if (!batchResult.IsSuccess)
                {
                    result.IsSuccess = false;
                    result.Message = BuildDispatchMessage(dispatchDecision, batchResult.Message);
                    result.ResultValue = BuildFailedBatchReadResult(addresses);
                    return result.Complete();
                }

                foreach (var itemResult in batchResult.ResultValue.Items)
                {
                    if (itemResult.IsSuccess)
                    {
                        values[itemResult.Item.OriginalAddress] = ConvertReadBytesToValue(itemResult.Item, itemResult.Data);
                    }
                    else
                    {
                        errors.Add(itemResult.Message ?? $"读取 {itemResult.Item.OriginalAddress} 失败");
                    }
                }
            }

            result.IsSuccess = errors.Count == 0 || values.Count > 0;
            result.Message = BuildDispatchMessage(dispatchDecision, string.Join("; ", errors));
            result.ResultValue = BuildBatchReadResult(addresses, values);
            return result.Complete();
        }

        protected internal virtual List<SiemensAddress> BuildReadItems(IReadOnlyList<S7BatchHelper.S7AddressInfo> addressInfos)
        {
            var result = new List<SiemensAddress>();
            if (addressInfos == null)
            {
                return result;
            }

            for (int i = 0; i < addressInfos.Count; i++)
            {
                var addressInfo = addressInfos[i];
                result.Add(S7CommonMethods.BuildReadAddress(
                    addressInfo.OriginalAddress,
                    addressInfo.TargetDataType,
                    addressInfo.StartByte,
                    addressInfo.BitOffset,
                    addressInfo.Length,
                    i));
            }

            return result;
        }

        protected internal virtual List<S7ReadBatch> SplitReadBatches(IReadOnlyList<SiemensAddress> items)
        {
            var batches = new List<S7ReadBatch>();
            if (items == null || items.Count == 0)
            {
                return batches;
            }

            var limits = S7BatchLimits.CreateReadLimits(NegotiatedPduLimit, GetNativeRandomReadMaxItems(), GetNativeRandomReadMaxPayloadBytes());
            var orderedItems = items.OrderBy(t => t.OriginalIndex).ToList();
            foreach (var item in orderedItems)
            {
                ValidateReadItem(item, limits);
            }

            for (int i = 0; i < orderedItems.Count; i += limits.MaxItems)
            {
                var batch = new S7ReadBatch();
                foreach (var item in orderedItems.Skip(i).Take(limits.MaxItems))
                {
                    batch.Items.Add(item);
                }

                if (batch.Items.Count > 0)
                {
                    batches.Add(batch);
                }
            }

            return batches;
        }

        protected internal virtual List<SiemensAddress> BuildWriteItems(
            IReadOnlyList<S7BatchHelper.S7AddressInfo> addressInfos,
            Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            var result = new List<SiemensAddress>();
            if (addressInfos == null)
            {
                return result;
            }

            for (int i = 0; i < addressInfos.Count; i++)
            {
                var addressInfo = addressInfos[i];
                if (!addresses.TryGetValue(addressInfo.OriginalAddress, out var valueTuple))
                {
                    throw new InvalidOperationException($"地址 {addressInfo.OriginalAddress} 没有对应的值");
                }

                byte[] writeData = S7BatchHelper.ConvertValueToS7Bytes(valueTuple.Item2, addressInfo, IsReverse, DataFormat);
                if (writeData == null || writeData.Length == 0)
                {
                    throw new InvalidOperationException($"地址 {addressInfo.OriginalAddress} 数值转换失败");
                }

                result.Add(S7CommonMethods.BuildWriteAddress(
                    addressInfo.OriginalAddress,
                    addressInfo.TargetDataType,
                    addressInfo.StartByte,
                    addressInfo.BitOffset,
                    writeData,
                    i));
            }

            return result;
        }

        protected internal virtual List<S7WriteBatch> SplitWriteBatches(IReadOnlyList<SiemensAddress> items)
        {
            var batches = new List<S7WriteBatch>();
            if (items == null || items.Count == 0)
            {
                return batches;
            }

            var limits = S7BatchLimits.CreateWriteLimits(NegotiatedPduLimit, GetNativeRandomWriteMaxItems(), GetNativeRandomWriteMaxPayloadBytes());
            var orderedItems = items.OrderBy(t => t.OriginalIndex).ToList();
            foreach (var item in orderedItems)
            {
                ValidateWriteItem(item, limits);
            }

            for (int i = 0; i < orderedItems.Count; i += limits.MaxItems)
            {
                var batch = new S7WriteBatch();
                foreach (var item in orderedItems.Skip(i).Take(limits.MaxItems))
                {
                    batch.Items.Add(item);
                }

                if (batch.Items.Count > 0)
                {
                    batches.Add(batch);
                }
            }

            return batches;
        }

        private static void ValidateWriteItem(SiemensAddress item, S7BatchLimits limits)
        {
            var singleBatch = new S7WriteBatch();
            singleBatch.Items.Add(item);

            if (1 > limits.MaxItems
                || singleBatch.RequestLength > limits.RequestLimit
                || singleBatch.DataLength > limits.PayloadLimit
                || singleBatch.ResponseFrameLength > limits.ResponseLimit)
            {
                throw new InvalidOperationException(
                    $"地址 {item.OriginalAddress} 超出随机批量写单项上限: RequestLength={singleBatch.RequestLength}, DataLength={singleBatch.DataLength}, ResponseLength={singleBatch.ResponseFrameLength}");
            }
        }

        private async ValueTask<OperationResult<S7WriteResponse>> ExecuteWriteBatchAsync(S7WriteBatch batch)
        {
            if (!(Transport is S7EthernetTransport s7Transport))
            {
                return OperationResult.CreateFailedResult<S7WriteResponse>("S7传输层不可用");
            }

            var request = new S7WriteRequest(batch.Items, GetNextPduReference());
            var response = await s7Transport.UnicastWriteMessageAsync(request).ConfigureAwait(false);
            if (!response.IsSuccess)
            {
                var failureMessage = !string.IsNullOrEmpty(response.Message)
                    ? response.Message
                    : string.Join("; ", response.OperationInfo ?? new List<string>());
                if (string.IsNullOrEmpty(failureMessage))
                {
                    failureMessage = "批量随机写请求失败";
                }

                var failed = OperationResult.CreateFailedResult<S7WriteResponse>(failureMessage);
                failed.Requsts.AddRange(response.Requsts);
                failed.Responses.AddRange(response.Responses);
                return failed;
            }

            var parsed = S7WriteResponse.Parse(response.ResultValue.ProtocolMessageFrame, batch.Items);
            parsed.Requsts.AddRange(response.Requsts);
            parsed.Responses.AddRange(response.Responses);
            return parsed;
        }

        private static void ValidateReadItem(SiemensAddress item, S7BatchLimits limits)
        {
            var singleBatch = new S7ReadBatch();
            singleBatch.Items.Add(item);

            if (1 > limits.MaxItems
                || singleBatch.RequestLength > limits.RequestLimit
                || S7ReadRequest.EstimateResponsePayloadLength(singleBatch.Items) > limits.PayloadLimit
                || singleBatch.ResponseFrameLength > limits.ResponseLimit)
            {
                throw new InvalidOperationException(
                    $"地址 {item.OriginalAddress} 超出随机批量读单项上限: RequestLength={singleBatch.RequestLength}, ResponseLength={singleBatch.ResponseFrameLength}, Payload={S7ReadRequest.EstimateResponsePayloadLength(singleBatch.Items)}");
            }
        }

        private async ValueTask<OperationResult<S7ReadResponse>> ExecuteReadBatchAsync(S7ReadBatch batch)
        {
            if (!(Transport is S7EthernetTransport s7Transport))
            {
                return OperationResult.CreateFailedResult<S7ReadResponse>("S7传输层不可用");
            }

            var request = new S7ReadRequest(batch.Items, GetNextPduReference());
            var response = await s7Transport.UnicastReadMessageAsync(request).ConfigureAwait(false);
            if (!response.IsSuccess)
            {
                var failureMessage = !string.IsNullOrEmpty(response.Message)
                    ? response.Message
                    : string.Join("; ", response.OperationInfo ?? new List<string>());
                if (string.IsNullOrEmpty(failureMessage))
                {
                    failureMessage = "批量随机读请求失败";
                }

                var failed = OperationResult.CreateFailedResult<S7ReadResponse>(failureMessage);
                failed.Requsts.AddRange(response.Requsts);
                failed.Responses.AddRange(response.Responses);
                return failed;
            }

            var parsed = S7ReadResponse.Parse(response.ResultValue.ProtocolMessageFrame, batch.Items);
            parsed.Requsts.AddRange(response.Requsts);
            parsed.Responses.AddRange(response.Responses);
            return parsed;
        }

        private static object ConvertReadBytesToValue(SiemensAddress item, byte[] data)
        {
            if (data == null)
            {
                return null;
            }

            var normalized = NormalizeS7BytesForRead(data, item.DataType);
            switch (item.DataType)
            {
                case DataTypeEnums.Bool:
                    return normalized[0] != 0;
                case DataTypeEnums.Byte:
                    return normalized[0];
                case DataTypeEnums.Int16:
                    return BitConverter.ToInt16(normalized, 0);
                case DataTypeEnums.UInt16:
                    return BitConverter.ToUInt16(normalized, 0);
                case DataTypeEnums.Int32:
                    return BitConverter.ToInt32(normalized, 0);
                case DataTypeEnums.UInt32:
                    return BitConverter.ToUInt32(normalized, 0);
                case DataTypeEnums.Int64:
                    return BitConverter.ToInt64(normalized, 0);
                case DataTypeEnums.UInt64:
                    return BitConverter.ToUInt64(normalized, 0);
                case DataTypeEnums.Float:
                    return BitConverter.ToSingle(normalized, 0);
                case DataTypeEnums.Double:
                    return BitConverter.ToDouble(normalized, 0);
                case DataTypeEnums.String:
                    return Encoding.ASCII.GetString(normalized);
                default:
                    return normalized;
            }
        }

        private static byte[] NormalizeS7BytesForRead(byte[] data, DataTypeEnums dataType)
        {
            if (data == null)
            {
                return null;
            }

            var result = (byte[])data.Clone();
            if (!BitConverter.IsLittleEndian)
            {
                return result;
            }

            switch (dataType)
            {
                case DataTypeEnums.Int16:
                case DataTypeEnums.UInt16:
                case DataTypeEnums.Int32:
                case DataTypeEnums.UInt32:
                case DataTypeEnums.Int64:
                case DataTypeEnums.UInt64:
                case DataTypeEnums.Float:
                case DataTypeEnums.Double:
                    Array.Reverse(result);
                    break;
            }

            return result;
        }

        private static Dictionary<string, (DataTypeEnums, object)> BuildBatchReadResult(
            Dictionary<string, DataTypeEnums> addresses,
            Dictionary<string, object> values)
        {
            var finalResult = new Dictionary<string, (DataTypeEnums, object)>();
            foreach (var kvp in addresses)
            {
                object value;
                values.TryGetValue(kvp.Key, out value);
                finalResult[kvp.Key] = (kvp.Value, value);
            }

            return finalResult;
        }

        private static Dictionary<string, (DataTypeEnums, object)> BuildFailedBatchReadResult(Dictionary<string, DataTypeEnums> addresses)
        {
            return addresses.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value, (object)null));
        }

        private static string BuildDispatchMessage(S7BatchReadDispatchAnalysis decision, string detail)
        {
            return S7BatchMessageFormatter.BuildReadDispatchMessage(decision, detail);
        }

        protected internal virtual string BuildBatchWriteMessage(string detail)
        {
            return S7BatchMessageFormatter.BuildWriteDispatchMessage("NativeRandomWrite", detail);
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
            if (initialResult.IsSuccess
                || IsProtocolSynchronizationFailure(initialResult)
                || !ShouldShrinkBlockBoundary(block, initialResult))
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

        private static void MergeBatchReadLogs(OperationResult source, List<string> requests, List<string> responses)
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

            if (IsProtocolSynchronizationFailure(readResult))
            {
                return false;
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
                try
                {
                    if (addresses == null || addresses.Count == 0)
                    {
                        return new OperationResult().Complete();
                    }

                    var addressInfos = S7BatchHelper.ParseS7Addresses(addresses);
                    if (addressInfos.Count == 0)
                    {
                        return OperationResult.CreateFailedResult("没有有效地址可写入");
                    }

                    return await BatchWriteByNativeRandomAsync(addresses, addressInfos).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return OperationResult.CreateFailedResult($"批量写入异常: {ex.Message}");
                }
            }
        }

        protected internal virtual async ValueTask<OperationResult> BatchWriteByNativeRandomAsync(
            Dictionary<string, (DataTypeEnums, object)> addresses,
            List<S7BatchHelper.S7AddressInfo> addressInfos)
        {
            var result = new OperationResult();
            var items = BuildWriteItems(addressInfos, addresses);
            var batches = SplitWriteBatches(items);
            var errors = new List<string>();
            int successCount = 0;

            foreach (var batch in batches)
            {
                var batchResult = await ExecuteWriteBatchAsync(batch).ConfigureAwait(false);
                MergeBatchReadLogs(batchResult, result.Requsts, result.Responses);
                if (!batchResult.IsSuccess)
                {
                    result.IsSuccess = false;
                    result.Message = BuildBatchWriteMessage(batchResult.Message);
                    return result.Complete();
                }

                foreach (var itemResult in batchResult.ResultValue.Items)
                {
                    if (itemResult.IsSuccess)
                    {
                        successCount++;
                    }
                    else
                    {
                        errors.Add(itemResult.Message ?? $"写入 {itemResult.Item.OriginalAddress} 失败");
                    }
                }
            }

            result.IsSuccess = errors.Count == 0;
            result.Message = BuildBatchWriteMessage(
                errors.Count == 0
                    ? $"成功写入 {successCount} 个地址; Batches={batches.Count}"
                    : $"部分写入成功 ({successCount}/{items.Count}); Batches={batches.Count}; {string.Join("; ", errors)}");
            return result.Complete();
        }

        protected internal virtual async ValueTask<OperationResult> BatchWriteBySingleAsync(
            Dictionary<string, (DataTypeEnums, object)> addresses,
            List<S7BatchHelper.S7AddressInfo> addressInfos)
        {
            var result = new OperationResult();
            var writeErrors = new List<string>();
            var successCount = 0;

            foreach (var addressInfo in addressInfos)
            {
                try
                {
                    if (!addresses.TryGetValue(addressInfo.OriginalAddress, out var valueTuple))
                    {
                        writeErrors.Add($"地址 {addressInfo.OriginalAddress} 没有对应的值");
                        continue;
                    }

                    byte[] data = S7BatchHelper.ConvertValueToS7Bytes(valueTuple.Item2, addressInfo, IsReverse, DataFormat);
                    if (data == null)
                    {
                        writeErrors.Add($"地址 {addressInfo.OriginalAddress} 数值转换失败");
                        continue;
                    }

                    string writeAddress = S7BatchHelper.ConstructS7WriteAddress(addressInfo);
                    if (string.IsNullOrEmpty(writeAddress))
                    {
                        writeErrors.Add($"地址 {addressInfo.OriginalAddress} 构造写入地址失败");
                        continue;
                    }

                    var writeResult = await WriteAsync(writeAddress, data, DataTypeEnums.Byte, S7BatchHelper.IsBitType(addressInfo.DataType)).ConfigureAwait(false);
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

            result.IsSuccess = successCount == addressInfos.Count;
            result.Message = successCount == addressInfos.Count
                ? $"成功写入 {successCount} 个地址"
                : successCount > 0
                    ? $"部分写入成功 ({successCount}/{addressInfos.Count}): {string.Join("; ", writeErrors)}"
                    : $"批量写入失败: {string.Join("; ", writeErrors)}";
            return result.Complete();
        }
    }
}


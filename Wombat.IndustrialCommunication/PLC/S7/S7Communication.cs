using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.PLC;
using Wombat.Extensions.DataTypeExtensions;

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

        public async Task<OperationResult> InitAsync()
        {
            using (await _lock.LockAsync())
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
                    //第二次初始化指令交互
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


        internal override  async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
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

                            var tempResult = await internalReadAsync(s7Transport, address,alreadyFinished, readLength, isBit);
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
                        return await internalReadAsync(s7Transport, address,0, length, isBit);
                    }

                }
                return OperationResult.CreateFailedResult<byte[]>();
            }

            async ValueTask<OperationResult<byte[]>> internalReadAsync(S7EthernetTransport transport,string internalAddress,int internalOffest, int internalLength, bool internalIsBit = false)
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
                    var writeRequest = new S7WriteRequest(address,0,data, isBit);
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
        /// 解析地址字符串为结构化信息
        /// </summary>
        /// <param name="addresses">地址字典</param>
        /// <returns>解析后的地址信息列表</returns>
        private List<S7AddressInfo> ParseAddresses(Dictionary<string, object> addresses)
        {
            var addressInfos = new List<S7AddressInfo>();
            
            foreach (var kvp in addresses)
            {
                try
                {
                    var addressInfo = ParseSingleAddress(kvp.Key);
                    
                    // 对于V区和Q区地址，尝试根据上下文推断数据类型和长度
                    if (addressInfo.DbNumber == -1 || addressInfo.DbNumber == -2)
                    {
                        addressInfo = InferDataTypeAndLength(addressInfo, kvp.Key);
                    }
                    
                    addressInfos.Add(addressInfo);
                }
                catch (Exception ex)
                {
                    // 地址解析失败，跳过该地址，但记录日志
                    // 这里可以添加日志记录
                    continue;
                }
            }
            
            return addressInfos;
        }

        /// <summary>
        /// 推断V区和Q区地址的数据类型和长度
        /// </summary>
        /// <param name="addressInfo">原始地址信息</param>
        /// <param name="originalAddress">原始地址字符串</param>
        /// <returns>推断后的地址信息</returns>
        private S7AddressInfo InferDataTypeAndLength(S7AddressInfo addressInfo, string originalAddress)
        {
            // 对于V区地址，默认推断为32位数据类型（能兼容大多数情况）
            if (addressInfo.DbNumber == -1) // V区
            {
                addressInfo.DataType = S7DataType.DBD; // 使用DB区的双字类型
                addressInfo.Length = 4;
            }
            // 对于Q区地址，如果没有位标识，默认推断为字数据
            else if (addressInfo.DbNumber == -2 && !originalAddress.Contains(".")) // Q区且非位地址
            {
                addressInfo.DataType = S7DataType.DBW; // 使用DB区的字类型
                addressInfo.Length = 2;
            }
            // 对于I区地址，如果没有位标识，默认推断为字数据
            else if (addressInfo.DbNumber == -3 && !originalAddress.Contains(".")) // I区且非位地址
            {
                addressInfo.DataType = S7DataType.IW; // 使用I区的字类型
                addressInfo.Length = 2;
            }

            return addressInfo;
        }

        /// <summary>
        /// 解析单个地址字符串
        /// </summary>
        /// <param name="address">地址字符串，如 "DB1.DBW10", "DB2.DBX5.3", "V700", "Q1.3"</param>
        /// <returns>地址信息</returns>
        private S7AddressInfo ParseSingleAddress(string address)
        {
            var addressInfo = new S7AddressInfo
            {
                OriginalAddress = address
            };

            // 统一转换为大写并去除空格
            address = address.ToUpper().Replace(" ", "");

            // 检查是否是DB地址格式
            if (address.StartsWith("DB"))
            {
                return ParseDBAddress(address, addressInfo);
            }
            // 检查是否是V区地址格式
            else if (address.StartsWith("V"))
            {
                return ParseVAddress(address, addressInfo);
            }
            // 检查是否是Q区地址格式
            else if (address.StartsWith("Q"))
            {
                return ParseQAddress(address, addressInfo);
            }
            // 检查是否是I区地址格式
            else if (address.StartsWith("I"))
            {
                return ParseIAddress(address, addressInfo);
            }
            // 检查是否是M区地址格式
            else if (address.StartsWith("M"))
            {
                return ParseMAddress(address, addressInfo);
            }
            else
            {
                throw new ArgumentException($"不支持的地址格式: {address}");
            }
        }

        /// <summary>
        /// 解析DB地址格式
        /// </summary>
        private S7AddressInfo ParseDBAddress(string address, S7AddressInfo addressInfo)
        {
            var dbEndIndex = address.IndexOf('.');
            if (dbEndIndex == -1)
                throw new ArgumentException($"DB地址格式错误: {address}");

            var dbNumberStr = address.Substring(2, dbEndIndex - 2);
            if (!int.TryParse(dbNumberStr, out int dbNumber))
                throw new ArgumentException($"DB号解析失败: {address}");

            addressInfo.DbNumber = dbNumber;

            // 解析数据类型和偏移
            var typeAndOffset = address.Substring(dbEndIndex + 1);
            
            if (typeAndOffset.StartsWith("DBX"))
            {
                // 位地址，如 DBX5.3
                addressInfo.DataType = S7DataType.DBX;
                addressInfo.Length = 1; // 位长度为1

                var parts = typeAndOffset.Substring(3).Split('.');
                if (parts.Length != 2)
                    throw new ArgumentException($"位地址格式错误: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"位地址偏移解析失败: {address}");

                addressInfo.StartByte = byteOffset;
                addressInfo.BitOffset = bitOffset;
            }
            else if (typeAndOffset.StartsWith("DBB"))
            {
                // 字节地址
                addressInfo.DataType = S7DataType.DBB;
                addressInfo.Length = 1;
                addressInfo.BitOffset = 0;

                var offsetStr = typeAndOffset.Substring(3);
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"字节地址偏移解析失败: {address}");

                addressInfo.StartByte = offset;
            }
            else if (typeAndOffset.StartsWith("DBW"))
            {
                // 字地址
                addressInfo.DataType = S7DataType.DBW;
                addressInfo.Length = 2;
                addressInfo.BitOffset = 0;

                var offsetStr = typeAndOffset.Substring(3);
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"字地址偏移解析失败: {address}");

                addressInfo.StartByte = offset;
            }
            else if (typeAndOffset.StartsWith("DBD"))
            {
                // 双字地址
                addressInfo.DataType = S7DataType.DBD;
                addressInfo.Length = 4;
                addressInfo.BitOffset = 0;

                var offsetStr = typeAndOffset.Substring(3);
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"双字地址偏移解析失败: {address}");

                addressInfo.StartByte = offset;
            }
            else
            {
                throw new ArgumentException($"不支持的DB数据类型: {address}");
            }

            return addressInfo;
        }

        /// <summary>
        /// 解析V区地址格式
        /// </summary>
        private S7AddressInfo ParseVAddress(string address, S7AddressInfo addressInfo)
        {
            // 检查是否是复合地址格式（VW、VD等）
            if (address.Length > 2 && (address[1] == 'W' || address[1] == 'D' || address[1] == 'B'))
            {
                var dataType = address[1];
                var offsetStr = address.Substring(2); // 去掉VW、VD、VB前缀
                
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"V区地址偏移解析失败: {address}");

                // 对于Smart200，V区地址映射到DB1
                if (SiemensVersion == SiemensVersion.S7_200Smart)
                {
                    addressInfo.DbNumber = 1; // Smart200的V区对应DB1
                    addressInfo.StartByte = offset;
                    addressInfo.BitOffset = 0;

                    // 根据数据类型设置相应的DB类型
                    switch (dataType)
                    {
                        case 'B':
                            addressInfo.DataType = S7DataType.DBB;
                            addressInfo.Length = 1;
                            break;
                        case 'W':
                            addressInfo.DataType = S7DataType.DBW;
                            addressInfo.Length = 2;
                            break;
                        case 'D':
                            addressInfo.DataType = S7DataType.DBD;
                            addressInfo.Length = 4;
                            break;
                        default:
                            throw new ArgumentException($"不支持的V区数据类型: {dataType}");
                    }
                }
                else
                {
                    // 其他版本的V区地址使用特殊的DB号标识
                    addressInfo.DbNumber = -1; // 使用-1表示V区
                    addressInfo.StartByte = offset;
                    addressInfo.BitOffset = 0;

                    // 根据数据类型设置相应的V类型
                    switch (dataType)
                    {
                        case 'B':
                            addressInfo.DataType = S7DataType.VB;
                            addressInfo.Length = 1;
                            break;
                        case 'W':
                            addressInfo.DataType = S7DataType.VW;
                            addressInfo.Length = 2;
                            break;
                        case 'D':
                            addressInfo.DataType = S7DataType.VD;
                            addressInfo.Length = 4;
                            break;
                        default:
                            throw new ArgumentException($"不支持的V区数据类型: {dataType}");
                    }
                }
            }
            else
            {
                // 简单V区地址格式（如V700）
                var offsetStr = address.Substring(1); // 去掉V前缀
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"V区地址偏移解析失败: {address}");

                // 对于Smart200，V区地址映射到DB1
                if (SiemensVersion == SiemensVersion.S7_200Smart)
                {
                    addressInfo.DbNumber = 1; // Smart200的V区对应DB1
                    addressInfo.DataType = S7DataType.DBB; // 使用DB区数据类型
                    addressInfo.StartByte = offset;
                    addressInfo.Length = 1; // 默认长度，实际使用时会根据需要调整
                    addressInfo.BitOffset = 0;
                }
                else
                {
                    // 其他版本的V区地址使用特殊的DB号标识
                    addressInfo.DbNumber = -1; // 使用-1表示V区
                    addressInfo.DataType = S7DataType.VB; // 使用V区字节类型
                    addressInfo.StartByte = offset;
                    addressInfo.Length = 1; // 默认长度，实际使用时会根据需要调整
                    addressInfo.BitOffset = 0;
                }
            }

            return addressInfo;
        }

        /// <summary>
        /// 解析Q区地址格式
        /// </summary>
        private S7AddressInfo ParseQAddress(string address, S7AddressInfo addressInfo)
        {
            // Q区地址的DbNumber始终为0
            addressInfo.DbNumber = 0;

            if (address.Contains("."))
            {
                // Q区位地址，如 Q1.3
                var parts = address.Substring(1).Split('.'); // 去掉Q前缀并分割
                if (parts.Length != 2)
                    throw new ArgumentException($"Q区位地址格式错误: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"Q区位地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.DBX; // Q区位地址使用DB位类型
                addressInfo.StartByte = byteOffset;
                addressInfo.BitOffset = bitOffset;
                addressInfo.Length = 1; // 位长度为1
            }
            else
            {
                // Q区字节地址，如 Q10
                var offsetStr = address.Substring(1); // 去掉Q前缀
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"Q区地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.DBW; // Q区字地址使用DB字类型
                addressInfo.StartByte = offset;
                addressInfo.Length = 2;
                addressInfo.BitOffset = 0;
            }

            return addressInfo;
        }

        /// <summary>
        /// 解析I区地址格式
        /// </summary>
        private S7AddressInfo ParseIAddress(string address, S7AddressInfo addressInfo)
        {
            // I区地址的DbNumber始终为0
            addressInfo.DbNumber = 0;

            if (address.Contains("."))
            {
                // I区位地址，如 I1.3
                var parts = address.Substring(1).Split('.'); // 去掉I前缀并分割
                if (parts.Length != 2)
                    throw new ArgumentException($"I区位地址格式错误: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"I区位地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.DBX; // I区位地址使用DBX类型表示
                addressInfo.StartByte = byteOffset;
                addressInfo.BitOffset = bitOffset;
                addressInfo.Length = 1; // 位长度为1
            }
            else
            {
                // I区字节地址，如 I10
                var offsetStr = address.Substring(1); // 去掉I前缀
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"I区地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.IW; // 默认按字处理
                addressInfo.StartByte = offset;
                addressInfo.Length = 2;
                addressInfo.BitOffset = 0;
            }

            return addressInfo;
        }

        /// <summary>
        /// 解析M区地址格式
        /// </summary>
        private S7AddressInfo ParseMAddress(string address, S7AddressInfo addressInfo)
        {
            // M区地址属于内部存储区，使用特殊的DB号标识
            addressInfo.DbNumber = -4; // 使用-4表示M区

            if (address.Contains("."))
            {
                // M区位地址，如 M1.3
                var parts = address.Substring(1).Split('.'); // 去掉M前缀并分割
                if (parts.Length != 2)
                    throw new ArgumentException($"M区位地址格式错误: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"M区位地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.MX;
                addressInfo.StartByte = byteOffset;
                addressInfo.BitOffset = bitOffset;
                addressInfo.Length = 1; // 位长度为1
            }
            else
            {
                // M区字节地址，如 M10
                var offsetStr = address.Substring(1); // 去掉M前缀
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"M区地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.MB; // 默认按字节处理
                addressInfo.StartByte = offset;
                addressInfo.Length = 1;
                addressInfo.BitOffset = 0;
            }

            return addressInfo;
        }

        /// <summary>
        /// 获取地址的区域类型
        /// </summary>
        /// <param name="dataType">数据类型</param>
        /// <returns>区域类型字符串</returns>
        private string GetAreaType(S7DataType dataType)
        {
            switch (dataType)
            {
                case S7DataType.DBX:
                case S7DataType.DBB:
                case S7DataType.DBW:
                case S7DataType.DBD:
                    return "DB";
                case S7DataType.IB:
                case S7DataType.IW:
                case S7DataType.ID:
                    return "I";
                case S7DataType.QB:
                case S7DataType.QW:
                case S7DataType.QD:
                    return "Q";
                case S7DataType.MX:
                case S7DataType.MB:
                case S7DataType.MW:
                case S7DataType.MD:
                    return "M";
                case S7DataType.VB:
                case S7DataType.VW:
                case S7DataType.VD:
                    return "V";
                default:
                    return "UNKNOWN";
            }
        }

        /// <summary>
        /// 动态窗口优化算法：将地址合并为高效的读取块
        /// </summary>
        /// <param name="addressInfos">地址信息列表</param>
        /// <param name="minEfficiencyRatio">最小效率比（有效数据/总读取数据）</param>
        /// <param name="maxBlockSize">最大块大小（字节）</param>
        /// <returns>优化后的地址块列表</returns>
        private List<S7AddressBlock> OptimizeAddressBlocks(List<S7AddressInfo> addressInfos, double minEfficiencyRatio = 0.7, int maxBlockSize = 180)
        {
            var optimizedBlocks = new List<S7AddressBlock>();
            
            // 按区域类型和DB号分组（DB地址按DB号分组，V区和Q区分别独立分组）
            var dbGroups = addressInfos.GroupBy(a => new { a.DbNumber, AreaType = GetAreaType(a.DataType) }).ToList();
            
            foreach (var dbGroup in dbGroups)
            {
                var areaType = dbGroup.Key.AreaType;
                
                // 特殊处理Q区和I区的位地址
                if ((areaType == "Q" || areaType == "I") && dbGroup.Any(a => a.DataType == S7DataType.DBX))
                {
                    // 对于Q区和I区的位地址，按字节边界进行优化
                    var bitAddresses = dbGroup.Where(a => a.DataType == S7DataType.DBX).ToList();
                    var nonBitAddresses = dbGroup.Where(a => a.DataType != S7DataType.DBX).ToList();
                    
                    // 处理位地址
                    if (bitAddresses.Count > 0)
                    {
                        var bitBlocks = OptimizeBitAddresses(bitAddresses, areaType, maxBlockSize);
                        optimizedBlocks.AddRange(bitBlocks);
                    }
                    
                    // 处理非位地址
                    if (nonBitAddresses.Count > 0)
                    {
                        var nonBitBlocks = OptimizeNonBitAddresses(nonBitAddresses, minEfficiencyRatio, maxBlockSize);
                        optimizedBlocks.AddRange(nonBitBlocks);
                    }
                }
                else
                {
                    // 其他区域的地址使用原有逻辑
                    var sortedAddresses = dbGroup.OrderBy(a => a.StartByte).ToList();
                    var blocks = OptimizeNonBitAddresses(sortedAddresses, minEfficiencyRatio, maxBlockSize);
                    optimizedBlocks.AddRange(blocks);
                }
            }
            
            return optimizedBlocks;
        }

        /// <summary>
        /// 优化位地址（Q区和I区）
        /// </summary>
        /// <param name="bitAddresses">位地址列表</param>
        /// <param name="areaType">区域类型</param>
        /// <param name="maxBlockSize">最大块大小</param>
        /// <returns>优化后的地址块列表</returns>
        private List<S7AddressBlock> OptimizeBitAddresses(List<S7AddressInfo> bitAddresses, string areaType, int maxBlockSize)
        {
            var optimizedBlocks = new List<S7AddressBlock>();
            
            // 按字节地址分组
            var byteGroups = bitAddresses.GroupBy(a => a.StartByte).ToList();
            
            foreach (var byteGroup in byteGroups)
            {
                var byteOffset = byteGroup.Key;
                var addresses = byteGroup.ToList();
                
                // 每个字节作为一个块
                var block = new S7AddressBlock
                {
                    DbNumber = addresses[0].DbNumber,
                    StartByte = byteOffset,
                    TotalLength = 1, // 按字节读取
                    Addresses = addresses,
                    EfficiencyRatio = 1.0 // 位地址的效率比总是1.0
                };
                
                optimizedBlocks.Add(block);
            }
            
            return optimizedBlocks;
        }

        /// <summary>
        /// 优化非位地址
        /// </summary>
        /// <param name="addresses">地址列表</param>
        /// <param name="minEfficiencyRatio">最小效率比</param>
        /// <param name="maxBlockSize">最大块大小</param>
        /// <returns>优化后的地址块列表</returns>
        private List<S7AddressBlock> OptimizeNonBitAddresses(List<S7AddressInfo> addresses, double minEfficiencyRatio, int maxBlockSize)
        {
            var optimizedBlocks = new List<S7AddressBlock>();
            
            // 按起始地址排序
            var sortedAddresses = addresses.OrderBy(a => a.StartByte).ToList();
            
            var currentBlock = new S7AddressBlock
            {
                DbNumber = addresses[0].DbNumber,
                Addresses = new List<S7AddressInfo>()
            };
            
            foreach (var address in sortedAddresses)
            {
                // 如果是第一个地址，直接加入当前块
                if (currentBlock.Addresses.Count == 0)
                {
                    currentBlock.StartByte = address.StartByte;
                    currentBlock.TotalLength = address.Length;
                    currentBlock.Addresses.Add(address);
                    continue;
                }
                
                // 计算如果加入此地址后的新块参数
                var newStartByte = Math.Min(currentBlock.StartByte, address.StartByte);
                var currentEndByte = currentBlock.StartByte + currentBlock.TotalLength;
                var addressEndByte = address.StartByte + address.Length;
                var newEndByte = Math.Max(currentEndByte, addressEndByte);
                var newTotalLength = newEndByte - newStartByte;
                
                // 检查块大小限制
                if (newTotalLength > maxBlockSize)
                {
                    // 超过最大块大小，完成当前块并开始新块
                    currentBlock.EfficiencyRatio = CalculateEfficiencyRatio(currentBlock);
                    optimizedBlocks.Add(currentBlock);
                    
                    currentBlock = new S7AddressBlock
                    {
                        DbNumber = address.DbNumber,
                        StartByte = address.StartByte,
                        TotalLength = address.Length,
                        Addresses = new List<S7AddressInfo> { address }
                    };
                    continue;
                }
                
                // 计算加入后的效率比
                var testBlock = new S7AddressBlock
                {
                    DbNumber = address.DbNumber,
                    StartByte = newStartByte,
                    TotalLength = newTotalLength,
                    Addresses = new List<S7AddressInfo>(currentBlock.Addresses) { address }
                };
                
                var newEfficiencyRatio = CalculateEfficiencyRatio(testBlock);
                
                // 检查效率比是否满足要求
                if (newEfficiencyRatio >= minEfficiencyRatio)
                {
                    // 效率比满足要求，合并地址
                    currentBlock.StartByte = newStartByte;
                    currentBlock.TotalLength = newTotalLength;
                    currentBlock.Addresses.Add(address);
                }
                else
                {
                    // 效率比不满足要求，完成当前块并开始新块
                    currentBlock.EfficiencyRatio = CalculateEfficiencyRatio(currentBlock);
                    optimizedBlocks.Add(currentBlock);
                    
                    currentBlock = new S7AddressBlock
                    {
                        DbNumber = address.DbNumber,
                        StartByte = address.StartByte,
                        TotalLength = address.Length,
                        Addresses = new List<S7AddressInfo> { address }
                    };
                }
            }
            
            // 添加最后一个块
            if (currentBlock.Addresses.Count > 0)
            {
                currentBlock.EfficiencyRatio = CalculateEfficiencyRatio(currentBlock);
                optimizedBlocks.Add(currentBlock);
            }
            
            return optimizedBlocks;
        }

        /// <summary>
        /// 计算地址块的效率比
        /// </summary>
        /// <param name="block">地址块</param>
        /// <returns>效率比（0-1之间）</returns>
        private double CalculateEfficiencyRatio(S7AddressBlock block)
        {
            if (block.TotalLength == 0) return 0;
            
            var effectiveDataLength = block.Addresses.Sum(a => a.Length);
            return (double)effectiveDataLength / block.TotalLength;
        }

        /// <summary>
        /// 执行批量读取操作
        /// </summary>
        /// <param name="blocks">优化后的地址块列表</param>
        /// <returns>读取结果，键为块标识，值为读取的字节数据</returns>
        private async Task<OperationResult<Dictionary<string, byte[]>>> ExecuteBatchRead(List<S7AddressBlock> blocks)
        {
            var result = new OperationResult<Dictionary<string, byte[]>>();
            var blockDataDict = new Dictionary<string, byte[]>();
            var errors = new List<string>();

            foreach (var block in blocks)
            {
                try
                {
                    // 构造块的地址字符串，根据地址类型选择合适的格式
                    string blockAddress = "";
                    string blockKey = "";
                    
                    if (block.Addresses.Count > 0)
                    {
                        var firstAddress = block.Addresses[0];
                        var areaType = GetAreaType(firstAddress.DataType);
                        
                        // 根据DbNumber的特殊值来确定区域类型
                        if (block.DbNumber == -2) // Q区
                        {
                            areaType = "Q";
                        }
                        else if (block.DbNumber == -3) // I区
                        {
                            areaType = "I";
                        }
                        else if (block.DbNumber == -4) // M区
                        {
                            areaType = "M";
                        }
                        else if (block.DbNumber == -1) // V区
                        {
                            areaType = "V";
                        }
                        
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
                                errors.Add($"不支持的地址类型: {areaType}");
                                continue;
                        }
                    }
                    else
                    {
                        errors.Add($"块中没有地址信息");
                        continue;
                    }
                    
                    // 直接调用底层读取方法，避免重复加锁
                    var readResult = await InternalReadAsync(blockAddress, block.TotalLength, false);
                    
                    if (readResult.IsSuccess)
                    {
                        blockDataDict[blockKey] = readResult.ResultValue;
                        
                        // 合并请求和响应日志
                        result.Requsts.AddRange(readResult.Requsts);
                        result.Responses.AddRange(readResult.Responses);
                    }
                    else
                    {
                        var areaType = GetAreaType(block.Addresses[0].DataType);
                        errors.Add($"读取块 {areaType}{(areaType == "DB" ? block.DbNumber.ToString() : "")}:{block.StartByte}-{block.StartByte + block.TotalLength - 1} 失败: {readResult.Message}");
                    }
                }
                catch (Exception ex)
                {
                    var areaType = block.Addresses.Count > 0 ? GetAreaType(block.Addresses[0].DataType) : "UNKNOWN";
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

            result.ResultValue = blockDataDict;
            return result.Complete();
        }

        /// <summary>
        /// 内部读取方法，不加锁，供批量读取使用
        /// </summary>
        private async ValueTask<OperationResult<byte[]>> InternalReadAsync(string address, int length, bool isBit = false)
        {
            if (Transport is S7EthernetTransport s7Transport)
            {
                var tempResult = new OperationResult<byte>();
                var readRequest = new S7ReadRequest(address, 0, length, isBit);
                var response = await s7Transport.UnicastReadMessageAsync(readRequest);
                if (response.IsSuccess)
                {
                    int realLength = length;
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
                                tempResult.Message = $"读取{address}失败，请确认是否存在地址{address}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] == 0x05 && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取{address}失败，请确认是否存在地址{address}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] != 0xFF)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取{address}失败，异常代码[{21}]:{dataPackage[21]}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                        }
                        if (isBit) { realLength = (int)(Math.Ceiling(realLength / 8.0)); }
                        Array.Copy(dataPackage, dataPackage.Length - realLength, responseData, 0, realLength);
                    }
                    catch (Exception ex)
                    {
                        tempResult.Exception = ex;
                        tempResult.Message = $"{address} 0 {length} 读取预期长度与返回数据长度不一致";
                        return OperationResult.CreateFailedResult<byte[]>(tempResult);
                    }
                    
                    var result = new OperationResult<byte[]>(response, responseData);
                    result.Requsts.Add(string.Join(" ", readRequest.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
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

        /// <summary>
        /// 从读取的块数据中提取各个地址对应的数据
        /// </summary>
        /// <param name="blockData">块数据字典</param>
        /// <param name="blocks">地址块信息</param>
        /// <param name="originalAddresses">原始地址信息</param>
        /// <returns>地址到数据值的映射</returns>
        private Dictionary<string, object> ExtractDataFromBlocks(Dictionary<string, byte[]> blockData, List<S7AddressBlock> blocks, List<S7AddressInfo> originalAddresses)
        {
            var result = new Dictionary<string, object>();

            foreach (var block in blocks)
            {
                // 根据地址类型生成正确的块键
                string blockKey = "";
                if (block.Addresses.Count > 0)
                {
                    var firstAddress = block.Addresses[0];
                    var areaType = GetAreaType(firstAddress.DataType);
                    
                    // 根据DbNumber的特殊值来确定区域类型
                    if (block.DbNumber == -2) // Q区
                    {
                        areaType = "Q";
                    }
                    else if (block.DbNumber == -3) // I区
                    {
                        areaType = "I";
                    }
                    else if (block.DbNumber == -4) // M区
                    {
                        areaType = "M";
                    }
                    else if (block.DbNumber == -1) // V区
                    {
                        areaType = "V";
                    }
                    
                    switch (areaType)
                    {
                        case "DB":
                            blockKey = $"DB{block.DbNumber}_{block.StartByte}_{block.TotalLength}";
                            break;
                        case "I":
                            blockKey = $"I_{block.StartByte}_{block.TotalLength}";
                            break;
                        case "Q":
                            blockKey = $"Q_{block.StartByte}_{block.TotalLength}";
                            break;
                        case "M":
                            blockKey = $"M_{block.StartByte}_{block.TotalLength}";
                            break;
                        case "V":
                            blockKey = $"V_{block.StartByte}_{block.TotalLength}";
                            break;
                        default:
                            // 无法识别的地址类型，跳过
                            foreach (var address in block.Addresses)
                            {
                                result[address.OriginalAddress] = null;
                            }
                            continue;
                    }
                }
                else
                {
                    // 块中没有地址，跳过
                    continue;
                }
                
                if (!blockData.TryGetValue(blockKey, out byte[] data))
                {
                    // 该块读取失败，将其包含的所有地址标记为null
                    foreach (var address in block.Addresses)
                    {
                        result[address.OriginalAddress] = null;
                    }
                    continue;
                }

                // 从块数据中提取各个地址的值
                foreach (var address in block.Addresses)
                {
                    try
                    {
                        var relativeOffset = address.StartByte - block.StartByte;
                        
                        if (relativeOffset < 0 || relativeOffset + address.Length > data.Length)
                        {
                            result[address.OriginalAddress] = null;
                            continue;
                        }

                        object value = ExtractValueFromBytes(data, relativeOffset, address);
                        result[address.OriginalAddress] = value;
                    }
                    catch (Exception)
                    {
                        result[address.OriginalAddress] = null;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 从字节数组中提取指定类型的值
        /// </summary>
        /// <param name="data">字节数组</param>
        /// <param name="offset">偏移量</param>
        /// <param name="addressInfo">地址信息</param>
        /// <returns>提取的值</returns>
        private object ExtractValueFromBytes(byte[] data, int offset, S7AddressInfo addressInfo)
        {
            switch (addressInfo.DataType)
            {
                case S7DataType.DBX:
                    // 位数据（包括I区和Q区的位地址）
                    if (offset < data.Length)
                    {
                        var byteValue = data[offset];
                        return (byteValue & (1 << addressInfo.BitOffset)) != 0;
                    }
                    return false;

                case S7DataType.DBB:
                case S7DataType.IB:
                case S7DataType.VB:
                    // 字节数据
                    if (offset < data.Length)
                    {
                        return data[offset];
                    }
                    return (byte)0;

                case S7DataType.DBW:
                case S7DataType.IW:
                case S7DataType.VW:
                    // 字数据 (2字节)
                    if (offset + 1 < data.Length)
                    {
                        if (IsReverse)
                        {
                            return (ushort)(data[offset] << 8 | data[offset + 1]);
                        }
                        else
                        {
                            return (ushort)(data[offset + 1] << 8 | data[offset]);
                        }
                    }
                    return (ushort)0;

                case S7DataType.DBD:
                case S7DataType.ID:
                case S7DataType.VD:
                    // 双字数据 (4字节)
                    if (offset + 3 < data.Length)
                    {
                        if (IsReverse)
                        {
                            return (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
                        }
                        else
                        {
                            return (uint)(data[offset + 3] << 24 | data[offset + 2] << 16 | data[offset + 1] << 8 | data[offset]);
                        }
                    }
                    return (uint)0;

                default:
                    return null;
            }
        }

        /// <summary>
        /// 将值转换为字节数组
        /// </summary>
        /// <param name="value">要转换的值</param>
        /// <param name="addressInfo">地址信息</param>
        /// <returns>字节数组</returns>
        private byte[] ConvertValueToBytes(object value, S7AddressInfo addressInfo)
        {
            try
            {
                switch (addressInfo.DataType)
                {
                    case S7DataType.DBX:
                        // 位数据
                        if (value is bool boolValue)
                        {
                            var byteArray = new byte[1];
                            if (boolValue)
                            {
                                byteArray[0] = (byte)(1 << addressInfo.BitOffset);
                            }
                            return byteArray;
                        }
                        return null;

                    case S7DataType.DBB:
                    case S7DataType.IB:
                    case S7DataType.VB:
                        // 字节数据
                        if (value is byte byteValue)
                        {
                            return new byte[] { byteValue };
                        }
                        else if (value is int intValue)
                        {
                            return new byte[] { (byte)intValue };
                        }
                        return null;

                    case S7DataType.DBW:
                    case S7DataType.IW:
                    case S7DataType.VW:
                        // 字数据 (2字节)
                        if (value is short shortValue)
                        {
                            var bytes = new byte[2];
                            if (IsReverse)
                            {
                                bytes[0] = (byte)(shortValue >> 8);
                                bytes[1] = (byte)(shortValue & 0xFF);
                            }
                            else
                            {
                                bytes[0] = (byte)(shortValue & 0xFF);
                                bytes[1] = (byte)(shortValue >> 8);
                            }
                            return bytes;
                        }
                        else if (value is ushort ushortValue)
                        {
                            var bytes = new byte[2];
                            if (IsReverse)
                            {
                                bytes[0] = (byte)(ushortValue >> 8);
                                bytes[1] = (byte)(ushortValue & 0xFF);
                            }
                            else
                            {
                                bytes[0] = (byte)(ushortValue & 0xFF);
                                bytes[1] = (byte)(ushortValue >> 8);
                            }
                            return bytes;
                        }
                        return null;

                    case S7DataType.DBD:
                    case S7DataType.ID:
                        // 双字数据 (4字节)
                        if (value is int int32Value)
                        {
                            var bytes = new byte[4];
                            if (IsReverse)
                            {
                                bytes[0] = (byte)(int32Value >> 24);
                                bytes[1] = (byte)(int32Value >> 16);
                                bytes[2] = (byte)(int32Value >> 8);
                                bytes[3] = (byte)(int32Value & 0xFF);
                            }
                            else
                            {
                                bytes[0] = (byte)(int32Value & 0xFF);
                                bytes[1] = (byte)(int32Value >> 8);
                                bytes[2] = (byte)(int32Value >> 16);
                                bytes[3] = (byte)(int32Value >> 24);
                            }
                            return bytes;
                        }
                        else if (value is uint uint32Value)
                        {
                            var bytes = new byte[4];
                            if (IsReverse)
                            {
                                bytes[0] = (byte)(uint32Value >> 24);
                                bytes[1] = (byte)(uint32Value >> 16);
                                bytes[2] = (byte)(uint32Value >> 8);
                                bytes[3] = (byte)(uint32Value & 0xFF);
                            }
                            else
                            {
                                bytes[0] = (byte)(uint32Value & 0xFF);
                                bytes[1] = (byte)(uint32Value >> 8);
                                bytes[2] = (byte)(uint32Value >> 16);
                                bytes[3] = (byte)(uint32Value >> 24);
                            }
                            return bytes;
                        }
                        else if (value is float floatValue)
                        {
                            var intBytes = BitConverter.GetBytes(floatValue);
                            if (IsReverse)
                            {
                                Array.Reverse(intBytes);
                            }
                            return intBytes;
                        }
                        return null;

                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 构造写入地址
        /// </summary>
        /// <param name="addressInfo">地址信息</param>
        /// <returns>写入地址字符串</returns>
        private string ConstructWriteAddress(S7AddressInfo addressInfo)
        {
            try
            {
                var areaType = GetAreaType(addressInfo.DataType);
                
                switch (areaType)
                {
                    case "DB":
                        if (addressInfo.DataType == S7DataType.DBX)
                        {
                            return $"DB{addressInfo.DbNumber}.DBX{addressInfo.StartByte}.{addressInfo.BitOffset}";
                        }
                        else if (addressInfo.DataType == S7DataType.DBB)
                        {
                            return $"DB{addressInfo.DbNumber}.DBB{addressInfo.StartByte}";
                        }
                        else if (addressInfo.DataType == S7DataType.DBW)
                        {
                            return $"DB{addressInfo.DbNumber}.DBW{addressInfo.StartByte}";
                        }
                        else if (addressInfo.DataType == S7DataType.DBD)
                        {
                            return $"DB{addressInfo.DbNumber}.DBD{addressInfo.StartByte}";
                        }
                        break;

                    case "I":
                        if (addressInfo.DataType == S7DataType.DBX)
                        {
                            return $"I{addressInfo.StartByte}.{addressInfo.BitOffset}";
                        }
                        else if (addressInfo.DataType == S7DataType.IW)
                        {
                            return $"IW{addressInfo.StartByte}";
                        }
                        break;

                    case "Q":
                        if (addressInfo.DataType == S7DataType.DBX)
                        {
                            return $"Q{addressInfo.StartByte}.{addressInfo.BitOffset}";
                        }
                        else if (addressInfo.DataType == S7DataType.DBW)
                        {
                            return $"QW{addressInfo.StartByte}";
                        }
                        break;

                    case "M":
                        if (addressInfo.DataType == S7DataType.MX)
                        {
                            return $"M{addressInfo.StartByte}.{addressInfo.BitOffset}";
                        }
                        else if (addressInfo.DataType == S7DataType.MB)
                        {
                            return $"MB{addressInfo.StartByte}";
                        }
                        break;

                    case "V":
                        // V区地址（Smart200）
                        if (SiemensVersion == SiemensVersion.S7_200Smart)
                        {
                            if (addressInfo.DataType == S7DataType.DBX)
                            {
                                return $"DB1.DBX{addressInfo.StartByte}.{addressInfo.BitOffset}";
                            }
                            else if (addressInfo.DataType == S7DataType.DBB)
                            {
                                return $"DB1.DBB{addressInfo.StartByte}";
                            }
                            else if (addressInfo.DataType == S7DataType.DBW)
                            {
                                return $"DB1.DBW{addressInfo.StartByte}";
                            }
                            else if (addressInfo.DataType == S7DataType.DBD)
                            {
                                return $"DB1.DBD{addressInfo.StartByte}";
                            }
                        }
                        else
                        {
                            // 非Smart200的V区地址
                            if (addressInfo.DataType == S7DataType.VB)
                            {
                                return $"VB{addressInfo.StartByte}";
                            }
                            else if (addressInfo.DataType == S7DataType.VW)
                            {
                                return $"VW{addressInfo.StartByte}";
                            }
                            else if (addressInfo.DataType == S7DataType.VD)
                            {
                                return $"VD{addressInfo.StartByte}";
                            }
                        }
                        break;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

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
                    var internalAddresses = new Dictionary<string, object>();
                    foreach (var kvp in addresses)
                    {
                        internalAddresses[kvp.Key] = null; // 读取时值为null
                    }

                    // 解析地址信息
                    var addressInfos = ParseAddresses(internalAddresses);
                    if (addressInfos.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "没有有效的地址可以读取";
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }

                    // 优化地址块
                    var optimizedBlocks = OptimizeAddressBlocks(addressInfos);
                    if (optimizedBlocks.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = "地址优化失败";
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }

                    // 执行批量读取
                    var blockReadResult = await ExecuteBatchRead(optimizedBlocks);
                    if (!blockReadResult.IsSuccess && blockReadResult.ResultValue.Count == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = blockReadResult.Message;
                        result.ResultValue = new Dictionary<string, (DataTypeEnums, object)>();
                        return result.Complete();
                    }

                    // 从块数据中提取各个地址的值
                    var extractedData = ExtractDataFromBlocks(blockReadResult.ResultValue, optimizedBlocks, addressInfos);

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
                    result.IsSuccess = true;
                    
                    // 合并请求和响应日志
                    result.Requsts.AddRange(blockReadResult.Requsts);
                    result.Responses.AddRange(blockReadResult.Responses);
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

                    // 将地址字典转换为内部格式
                    var internalAddresses = new Dictionary<string, object>();
                    foreach (var kvp in addresses)
                    {
                        internalAddresses[kvp.Key] = kvp.Value.Item2; // 写入时使用实际值
                    }

                    // 解析地址信息
                    var addressInfos = ParseAddresses(internalAddresses);
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
                            if (!internalAddresses.TryGetValue(addressInfo.OriginalAddress, out var value))
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 没有对应的值");
                                continue;
                            }

                            // 将值转换为字节数组
                            byte[] data = ConvertValueToBytes(value, addressInfo);
                            if (data == null)
                            {
                                writeErrors.Add($"地址 {addressInfo.OriginalAddress} 的值转换失败");
                                continue;
                            }

                            // 构造写入地址
                            string writeAddress = ConstructWriteAddress(addressInfo);
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

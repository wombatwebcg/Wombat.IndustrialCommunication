using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// S7批量读写通用工具类
    /// </summary>
    public static class S7BatchHelper
    {
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
            public DataTypeEnums TargetDataType { get; set; } // 目标数据类型
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

        /// <summary>
        /// 解析S7地址字典为地址信息列表
        /// </summary>
        /// <param name="addresses">地址字典，键为地址，值为(数据类型, 值)元组</param>
        /// <returns>解析后的地址信息列表</returns>
        public static List<S7AddressInfo> ParseS7Addresses(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            var addressInfos = new List<S7AddressInfo>();
            
            foreach (var kvp in addresses)
            {
                try
                {
                    var addressInfo = ParseSingleS7Address(kvp.Key, kvp.Value.Item1);
                    addressInfos.Add(addressInfo);
                }
                catch (Exception ex)
                {
                    // 地址解析失败，跳过该地址并记录日志
                    // 这里可以添加日志记录
                    continue;
                }
            }
            
            return addressInfos;
        }

        /// <summary>
        /// 解析单个S7地址字符串
        /// </summary>
        /// <param name="address">地址字符串，如 "DB1.DBW10", "DB2.DBX5.3", "V700", "Q1.3"</param>
        /// <param name="dataType">目标数据类型</param>
        /// <returns>地址信息</returns>
        public static S7AddressInfo ParseSingleS7Address(string address, DataTypeEnums dataType = DataTypeEnums.None)
        {
            var addressInfo = new S7AddressInfo
            {
                OriginalAddress = address,
                TargetDataType = dataType
            };

            // 统一转换为大写，去除空格
            address = address.ToUpper().Replace(" ", "");

            // 判断是否为DB地址格式
            if (address.StartsWith("DB"))
            {
                return ParseDBAddress(address, addressInfo);
            }
            // 判断是否为V区地址格式
            else if (address.StartsWith("V"))
            {
                return ParseVAddress(address, addressInfo);
            }
            // 判断是否为Q区地址格式
            else if (address.StartsWith("Q"))
            {
                return ParseQAddress(address, addressInfo);
            }
            // 判断是否为I区地址格式
            else if (address.StartsWith("I"))
            {
                return ParseIAddress(address, addressInfo);
            }
            // 判断是否为M区地址格式
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
        private static S7AddressInfo ParseDBAddress(string address, S7AddressInfo addressInfo)
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
        private static S7AddressInfo ParseVAddress(string address, S7AddressInfo addressInfo)
        {
            // Smart200的V区地址映射到DB1
            addressInfo.DbNumber = 1; // Smart200的V区对应DB1

            // 判断是否是位地址格式（如V0.0）
            if (address.Contains("."))
            {
                // V区位地址，如 V1.3
                var parts = address.Substring(1).Split('.'); // 去掉V前缀并分割
                if (parts.Length != 2)
                    throw new ArgumentException($"V区位地址格式错误: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"V区位地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.V; // V区位地址使用V类型
                addressInfo.StartByte = byteOffset;
                addressInfo.BitOffset = bitOffset;
                addressInfo.Length = 1; // 位长度为1
                return addressInfo;
            }
            // 判断是否是复合地址格式（VW、VD等）
            else if (address.Length > 2 && (address[1] == 'W' || address[1] == 'D' || address[1] == 'B'))
            {
                var dataType = address[1];
                var offsetStr = address.Substring(2); // 去掉VW、VD、VB前缀
                
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"V区地址偏移解析失败: {address}");

                addressInfo.StartByte = offset;
                addressInfo.BitOffset = 0;

                // 根据数据类型设置对应的DB类型
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
            else
            {
                // 简单V区地址格式，如V700
                var offsetStr = address.Substring(1); // 去掉V前缀
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"V区地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.VB; // 使用V区的字节类型
                addressInfo.StartByte = offset;
                addressInfo.Length = 1; // 默认长度，实际使用时根据需要进行调整
                addressInfo.BitOffset = 0;
            }

            return addressInfo;
        }

        /// <summary>
        /// 解析Q区地址格式
        /// </summary>
        private static S7AddressInfo ParseQAddress(string address, S7AddressInfo addressInfo)
        {
            // Q区地址的DbNumber初始化为0
            addressInfo.DbNumber = 0;

            if (address.Contains("."))
            {
                // Q区位地址，如 Q1.3
                var parts = address.Substring(1).Split('.'); // 去掉Q前缀并分割
                if (parts.Length != 2)
                    throw new ArgumentException($"Q区位地址格式错误: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"Q区位地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.Q; // Q区位地址使用Q类型
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
        private static S7AddressInfo ParseIAddress(string address, S7AddressInfo addressInfo)
        {
            // I区地址的DbNumber初始化为0
            addressInfo.DbNumber = 0;

            if (address.Contains("."))
            {
                // I区位地址，如 I1.3
                var parts = address.Substring(1).Split('.'); // 去掉I前缀并分割
                if (parts.Length != 2)
                    throw new ArgumentException($"I区位地址格式错误: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"I区位地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.I; // I区位地址使用I类型表示
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
        private static S7AddressInfo ParseMAddress(string address, S7AddressInfo addressInfo)
        {
            // M区地址（内部存储区）使用特殊的DB号标识
            addressInfo.DbNumber = 0; 

            if (address.Contains("."))
            {
                // M区位地址，如 M1.3
                var parts = address.Substring(1).Split('.'); // 去掉M前缀并分割
                if (parts.Length != 2)
                    throw new ArgumentException($"M区位地址格式错误: {address}");

                if (!int.TryParse(parts[0], out int byteOffset) || !int.TryParse(parts[1], out int bitOffset))
                    throw new ArgumentException($"M区位地址偏移解析失败: {address}");

                addressInfo.DataType = S7DataType.M;
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
        /// 优化S7地址块，将地址合并为高效的读取块
        /// </summary>
        /// <param name="addressInfos">地址信息列表</param>
        /// <param name="minEfficiencyRatio">最小效率比（有效数据/总读取数据）</param>
        /// <param name="maxBlockSize">最大块大小（字节）</param>
        /// <returns>优化后的地址块列表</returns>
        public static List<S7AddressBlock> OptimizeS7AddressBlocks(List<S7AddressInfo> addressInfos, double minEfficiencyRatio = 0.8, int maxBlockSize = 180)
        {
            var optimizedBlocks = new List<S7AddressBlock>();
            
            // 按数据类型和DB号分组（DB地址按DB号分组，V区、Q区、I区分别分组）
            var dbGroups = addressInfos.GroupBy(a => new { a.DbNumber, AreaType = GetS7AreaType(a.DataType) }).ToList();
            
            foreach (var dbGroup in dbGroups)
            {
                var areaType = dbGroup.Key.AreaType;
                
                // 特殊处理位地址
                if (dbGroup.Any(a => IsBitType(a.DataType)))
                {
                    // 对于位地址，按字节边界进行优化
                    var bitAddresses = dbGroup.Where(a => IsBitType(a.DataType)).ToList();
                    var nonBitAddresses = dbGroup.Where(a => !IsBitType(a.DataType)).ToList();
                    
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
                    var sortedAddresses = dbGroup.OrderBy(a => a.BitOffset)
                                                .ThenBy(a => a.StartByte)
                                                .ToList();
                    var blocks = OptimizeNonBitAddresses(sortedAddresses, minEfficiencyRatio, maxBlockSize);
                    optimizedBlocks.AddRange(blocks);
                }
            }
            
            return optimizedBlocks;
        }

        /// <summary>
        /// 优化位地址
        /// </summary>
        private static List<S7AddressBlock> OptimizeBitAddresses(List<S7AddressInfo> bitAddresses, string areaType, int maxBlockSize)
        {
            var optimizedBlocks = new List<S7AddressBlock>();
            
            // 先按BitOffset、StartByte、DbNumber排序
            var sortedBitAddresses = bitAddresses.OrderBy(a => a.BitOffset)
                                               .ThenBy(a => a.StartByte)
                                               .ThenBy(a => a.DbNumber)
                                               .ToList();
            
            // 按字节地址分组
            var byteGroups = sortedBitAddresses.GroupBy(a => a.StartByte).ToList();
            
            foreach (var byteGroup in byteGroups)
            {
                var byteOffset = byteGroup.Key;
                // 按BitOffset排序位地址
                var addresses = byteGroup.OrderBy(a => a.BitOffset).ToList();
                
                // 每个字节作为一个块
                var block = new S7AddressBlock
                {
                    DbNumber = addresses[0].DbNumber,
                    StartByte = byteOffset,
                    TotalLength = 1, // 读取一个字节
                    Addresses = addresses,
                    EfficiencyRatio = 1.0 // 位地址效率比总是1.0
                };
                
                optimizedBlocks.Add(block);
            }
            
            return optimizedBlocks;
        }

        /// <summary>
        /// 优化非位地址
        /// </summary>
        private static List<S7AddressBlock> OptimizeNonBitAddresses(List<S7AddressInfo> addresses, double minEfficiencyRatio, int maxBlockSize)
        {
            var optimizedBlocks = new List<S7AddressBlock>();
            
            // 按BitOffset、StartByte和DbNumber排序（根据用户需求）
            var sortedAddresses = addresses.OrderBy(a => a.DbNumber)
                                          .ThenBy(a => a.StartByte)
                                          .ToList();
            
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
            
            // 处理最后一个块
            if (currentBlock.Addresses.Count > 0)
            {
                currentBlock.EfficiencyRatio = CalculateEfficiencyRatio(currentBlock);
                optimizedBlocks.Add(currentBlock);
            }
            
            return optimizedBlocks;
        }

        /// <summary>
        /// 计算地址块效率比
        /// </summary>
        /// <param name="block">地址块</param>
        /// <returns>效率比（0-1之间）</returns>
        private static double CalculateEfficiencyRatio(S7AddressBlock block)
        {
            if (block.TotalLength == 0) return 0;
            
            var effectiveDataLength = block.Addresses.Sum(a => a.Length);
            return (double)effectiveDataLength / block.TotalLength;
        }

        /// <summary>
        /// 从读取的块数据中提取各个地址对应的值
        /// </summary>
        /// <param name="blockData">块数据字典</param>
        /// <param name="blocks">地址块信息</param>
        /// <param name="originalAddresses">原始地址信息</param>
        /// <returns>地址到值的映射</returns>
        public static Dictionary<string, object> ExtractDataFromS7Blocks(Dictionary<string, byte[]> blockData, List<S7AddressBlock> blocks, List<S7AddressInfo> originalAddresses)
        {
            var result = new Dictionary<string, object>();

            foreach (var block in blocks)
            {
                // 根据地址类型构造正确的块键
                string blockKey = "";
                if (block.Addresses.Count > 0)
                {
                    var firstAddress = block.Addresses[0];
                    var areaType = GetS7AreaType(firstAddress.DataType);
                                   
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
                    // 该块读取失败，将块中所有地址设为null
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

                        object value = ExtractValueFromBytes(data, relativeOffset, address, true, EndianFormat.ABCD);
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
        /// <param name="isReverse">是否反转字节序</param>
        /// <param name="dataFormat">数据格式</param>
        /// <returns>提取的值</returns>
        private static object ExtractValueFromBytes(byte[] data, int offset, S7AddressInfo addressInfo, bool isReverse = true, EndianFormat dataFormat = EndianFormat.ABCD)
        {
            try
            {
                switch (addressInfo.DataType)
                {
                    case S7DataType.DBX:
                    case S7DataType.I:
                    case S7DataType.Q:
                    case S7DataType.V:
                        // 位数据
                        if (offset < data.Length)
                        {
                            var byteValue = data[offset];
                            return (byteValue & (1 << addressInfo.BitOffset)) != 0;
                        }
                        return false;

                    case S7DataType.DBB:
                    case S7DataType.IB:
                    case S7DataType.VB:
                    case S7DataType.MB:
                    case S7DataType.QB:
                        // 字节数据
                        if (offset < data.Length)
                        {
                            return data[offset];
                        }
                        return (byte)0;

                    case S7DataType.DBW:
                    case S7DataType.IW:
                    case S7DataType.VW:
                    case S7DataType.MW:
                    case S7DataType.QW:
                        // 字数据 (2字节)
                        if (offset + 1 < data.Length)
                        {
                            return data.ToUInt16(offset, isReverse);
                        }
                        return (ushort)0;

                    case S7DataType.DBD:
                    case S7DataType.ID:
                    case S7DataType.VD:
                    case S7DataType.MD:
                    case S7DataType.QD:
                        // 双字数据 (4字节)
                        // 根据TargetDataType确定返回类型
                        if (offset + 3 < data.Length)
                        {
                            if (addressInfo.TargetDataType == DataTypeEnums.Float)
                            {
                                return data.ToFloat(offset, dataFormat);
                            }
                            else if (addressInfo.TargetDataType == DataTypeEnums.Int32)
                            {
                                return data.ToInt32(offset, dataFormat);
                            }
                            else
                            {
                                return data.ToUInt32(offset, dataFormat);
                            }
                        }
                        return (uint)0;

                    default:
                        // 根据TargetDataType尝试进行转换
                        switch (addressInfo.TargetDataType)
                        {
                            case DataTypeEnums.Int64:
                                if (offset + 7 < data.Length)
                                {
                                    return data.ToInt64(offset, dataFormat);
                                }
                                return (long)0;
                                
                            case DataTypeEnums.UInt64:
                                if (offset + 7 < data.Length)
                                {
                                    return data.ToUInt64(offset, dataFormat);
                                }
                                return (ulong)0;
                                
                            case DataTypeEnums.Double:
                                if (offset + 7 < data.Length)
                                {
                                    return data.ToDouble(offset, dataFormat);
                                }
                                return 0.0;
                                
                            default:
                                return null;
                        }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 将值转换为S7协议所需的字节数组
        /// </summary>
        /// <param name="value">要转换的值</param>
        /// <param name="addressInfo">地址信息</param>
        /// <param name="isReverse">是否反转字节序</param>
        /// <param name="dataFormat">数据格式</param>
        /// <returns>字节数组，转换失败返回null</returns>
        public static byte[] ConvertValueToS7Bytes(object value, S7AddressInfo addressInfo, bool isReverse = false, EndianFormat dataFormat = EndianFormat.ABCD)
        {
            try
            {
                switch (addressInfo.DataType)
                {
                    case S7DataType.DBX:
                    case S7DataType.I:
                    case S7DataType.Q:
                    case S7DataType.V:
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
                    case S7DataType.MB:
                    case S7DataType.QB:
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
                    case S7DataType.MW:
                    case S7DataType.QW:
                        // 字数据 (2字节)
                        if (value is short shortValue)
                        {
                            return shortValue.ToByte(isReverse);
                        }
                        else if (value is ushort ushortValue)
                        {
                            return ushortValue.ToByte(isReverse);
                        }
                        return null;

                    case S7DataType.DBD:
                    case S7DataType.ID:
                    case S7DataType.VD:
                    case S7DataType.MD:
                    case S7DataType.QD:
                        // 双字数据 (4字节)
                        // 根据值类型确定转换方法
                        if (value is int int32Value)
                        {
                            return int32Value.ToByte(dataFormat);
                        }
                        else if (value is uint uint32Value)
                        {
                            return uint32Value.ToByte(dataFormat);
                        }
                        else if (value is float floatValue)
                        {
                            return floatValue.ToByte(dataFormat);
                        }
                        return null;

                    default:
                        // 根据值类型尝试进行转换
                        if (value is long longValue)
                        {
                            return longValue.ToByte(dataFormat);
                        }
                        else if (value is ulong ulongValue)
                        {
                            return ulongValue.ToByte(dataFormat);
                        }
                        else if (value is double doubleValue)
                        {
                            return doubleValue.ToByte(dataFormat);
                        }
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 构造S7写入地址字符串
        /// </summary>
        /// <param name="addressInfo">地址信息</param>
        /// <returns>写入地址字符串，构造失败返回null</returns>
        public static string ConstructS7WriteAddress(S7AddressInfo addressInfo)
        {
            try
            {
                var areaType = GetS7AreaType(addressInfo.DataType);
                
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
                        if (addressInfo.DataType == S7DataType.I)
                        {
                            return $"I{addressInfo.StartByte}.{addressInfo.BitOffset}";
                        }
                        else if (addressInfo.DataType == S7DataType.IB)
                        {
                            return $"IB{addressInfo.StartByte}";
                        }
                        else if (addressInfo.DataType == S7DataType.IW)
                        {
                            return $"IW{addressInfo.StartByte}";
                        }
                        else if (addressInfo.DataType == S7DataType.ID)
                        {
                            return $"ID{addressInfo.StartByte}";
                        }
                        break;

                    case "Q":
                        if (addressInfo.DataType == S7DataType.Q)
                        {
                            return $"Q{addressInfo.StartByte}.{addressInfo.BitOffset}";
                        }
                        else if (addressInfo.DataType == S7DataType.QB)
                        {
                            return $"QB{addressInfo.StartByte}";
                        }
                        else if (addressInfo.DataType == S7DataType.QW)
                        {
                            return $"QW{addressInfo.StartByte}";
                        }
                        else if (addressInfo.DataType == S7DataType.QD)
                        {
                            return $"QD{addressInfo.StartByte}";
                        }
                        break;

                    case "M":
                        if (addressInfo.DataType == S7DataType.M)
                        {
                            return $"M{addressInfo.StartByte}.{addressInfo.BitOffset}";
                        }
                        else if (addressInfo.DataType == S7DataType.MB)
                        {
                            return $"MB{addressInfo.StartByte}";
                        }
                        else if (addressInfo.DataType == S7DataType.MW)
                        {
                            return $"MW{addressInfo.StartByte}";
                        }
                        else if (addressInfo.DataType == S7DataType.MD)
                        {
                            return $"MD{addressInfo.StartByte}";
                        }
                        break;

                    case "V":
                        // V区地址（Smart200专用）
                        if (addressInfo.DataType == S7DataType.V)
                        {
                            return $"V{addressInfo.StartByte}.{addressInfo.BitOffset}";
                        }
                        else if (addressInfo.DataType == S7DataType.VB)
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
                        break;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 根据S7数据类型获取区域类型
        /// </summary>
        /// <param name="dataType">S7数据类型</param>
        /// <returns>区域类型字符串</returns>
        public static string GetS7AreaType(S7DataType dataType)
        {
            switch (dataType)
            {
                case S7DataType.DBX:
                case S7DataType.DBB:
                case S7DataType.DBW:
                case S7DataType.DBD:
                    return "DB";
                case S7DataType.I:
                case S7DataType.IB:
                case S7DataType.IW:
                case S7DataType.ID:
                    return "I";
                case S7DataType.Q:
                case S7DataType.QB:
                case S7DataType.QW:
                case S7DataType.QD:
                    return "Q";
                case S7DataType.M:
                case S7DataType.MB:
                case S7DataType.MW:
                case S7DataType.MD:
                    return "M";
                case S7DataType.V:
                case S7DataType.VB:
                case S7DataType.VW:
                case S7DataType.VD:
                    return "V";
                default:
                    return "UNKNOWN";
            }
        }
        
        /// <summary>
        /// 判断S7数据类型是否为位类型
        /// </summary>
        /// <param name="dataType">S7数据类型</param>
        /// <returns>是否为位类型</returns>
        public static bool IsBitType(S7DataType dataType)
        {
            return dataType == S7DataType.DBX || 
                   dataType == S7DataType.I || 
                   dataType == S7DataType.Q || 
                   dataType == S7DataType.V || 
                   dataType == S7DataType.M;
        }
    }
} 
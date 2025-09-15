using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS批量读写通用工具类
    /// </summary>
    public static class FinsBatchHelper
    {
        /// <summary>
        /// FINS地址信息结构体
        /// </summary>
        public struct FinsAddressInfo
        {
            public string OriginalAddress { get; set; }
            public FinsMemoryArea MemoryArea { get; set; }
            public int Address { get; set; }
            public int Length { get; set; }
            public int BitAddress { get; set; }  // 位偏移，仅对位类型有效
            public bool IsBit { get; set; }
            public DataTypeEnums TargetDataType { get; set; } // 目标数据类型
        }

        /// <summary>
        /// FINS优化地址块
        /// </summary>
        public class FinsAddressBlock
        {
            public FinsMemoryArea MemoryArea { get; set; }
            public int StartAddress { get; set; }
            public int TotalLength { get; set; }
            public List<FinsAddressInfo> Addresses { get; set; } = new List<FinsAddressInfo>();
            public double EfficiencyRatio { get; set; }
        }

        /// <summary>
        /// 解析FINS地址字典为地址信息列表
        /// </summary>
        /// <param name="addresses">地址字典，键为地址，值为(数据类型, 值)元组</param>
        /// <returns>解析后的地址信息列表</returns>
        public static List<FinsAddressInfo> ParseFinsAddresses(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            var addressInfos = new List<FinsAddressInfo>();
            
            foreach (var kvp in addresses)
            {
                try
                {
                    var addressInfo = ParseSingleFinsAddress(kvp.Key, kvp.Value.Item1);
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
        /// 解析单个FINS地址字符串
        /// </summary>
        /// <param name="address">地址字符串，如 "D100", "CIO100.01", "W200", "H300"</param>
        /// <param name="dataType">目标数据类型</param>
        /// <returns>地址信息</returns>
        public static FinsAddressInfo ParseSingleFinsAddress(string address, DataTypeEnums dataType = DataTypeEnums.None)
        {
            var addressInfo = new FinsAddressInfo
            {
                OriginalAddress = address,
                TargetDataType = dataType
            };

            // 统一转换为大写，去除空格
            address = address.ToUpper().Replace(" ", "");

            // 判断是否为位地址格式（包含.）
            if (address.Contains("."))
            {
                return ParseBitAddress(address, addressInfo);
            }
            // 判断是否为DM区地址格式
            else if (address.StartsWith("D"))
            {
                return ParseDMAddress(address, addressInfo);
            }
            // 判断是否为CIO区地址格式
            else if (address.StartsWith("CIO"))
            {
                return ParseCIOAddress(address, addressInfo);
            }
            // 判断是否为工作区地址格式
            else if (address.StartsWith("W"))
            {
                return ParseWRAddress(address, addressInfo);
            }
            // 判断是否为保持区地址格式
            else if (address.StartsWith("H"))
            {
                return ParseHRAddress(address, addressInfo);
            }
            // 判断是否为辅助区地址格式
            else if (address.StartsWith("A"))
            {
                return ParseARAddress(address, addressInfo);
            }
            // 判断是否为扩展数据内存区地址格式
            else if (address.StartsWith("E"))
            {
                return ParseEMAddress(address, addressInfo);
            }
            // 判断是否为定时器地址格式
            else if (address.StartsWith("T") || address.StartsWith("TIM"))
            {
                return ParseTimerAddress(address, addressInfo);
            }
            // 判断是否为计数器地址格式
            else if (address.StartsWith("C") || address.StartsWith("CNT"))
            {
                return ParseCounterAddress(address, addressInfo);
            }
            else
            {
                throw new ArgumentException($"不支持的地址格式: {address}");
            }
        }

        /// <summary>
        /// 解析位地址格式
        /// </summary>
        private static FinsAddressInfo ParseBitAddress(string address, FinsAddressInfo addressInfo)
        {
            var parts = address.Split('.');
            if (parts.Length != 2)
                throw new ArgumentException($"位地址格式错误: {address}");

            var baseAddress = parts[0];
            if (!int.TryParse(parts[1], out int bitOffset) || bitOffset < 0 || bitOffset > 15)
                throw new ArgumentException($"位偏移解析失败或超出范围: {address}");

            addressInfo.BitAddress = bitOffset;
            addressInfo.IsBit = true;
            addressInfo.Length = 1; // 位长度为1

            // 解析基础地址部分
            if (baseAddress.StartsWith("CIO"))
            {
                addressInfo.MemoryArea = FinsMemoryArea.CIO;
                var offsetStr = baseAddress.Substring(3);
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"CIO位地址偏移解析失败: {address}");
                addressInfo.Address = offset;
            }
            else if (baseAddress.StartsWith("W"))
            {
                addressInfo.MemoryArea = FinsMemoryArea.WR;
                var offsetStr = baseAddress.Substring(1);
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"W位地址偏移解析失败: {address}");
                addressInfo.Address = offset;
            }
            else if (baseAddress.StartsWith("H"))
            {
                addressInfo.MemoryArea = FinsMemoryArea.HR;
                var offsetStr = baseAddress.Substring(1);
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"H位地址偏移解析失败: {address}");
                addressInfo.Address = offset;
            }
            else if (baseAddress.StartsWith("A"))
            {
                addressInfo.MemoryArea = FinsMemoryArea.AR;
                var offsetStr = baseAddress.Substring(1);
                if (!int.TryParse(offsetStr, out int offset))
                    throw new ArgumentException($"A位地址偏移解析失败: {address}");
                addressInfo.Address = offset;
            }
            else
            {
                throw new ArgumentException($"不支持的位地址格式: {address}");
            }

            return addressInfo;
        }

        /// <summary>
        /// 解析DM区地址格式
        /// </summary>
        private static FinsAddressInfo ParseDMAddress(string address, FinsAddressInfo addressInfo)
        {
            addressInfo.MemoryArea = FinsMemoryArea.DM;
            addressInfo.IsBit = false;

            var offsetStr = address.Substring(1); // 去掉D前缀
            if (!int.TryParse(offsetStr, out int offset))
                throw new ArgumentException($"DM地址偏移解析失败: {address}");

            addressInfo.Address = offset;
            addressInfo.Length = GetDataTypeLength(addressInfo.TargetDataType);
            addressInfo.BitAddress = 0;

            return addressInfo;
        }

        /// <summary>
        /// 解析CIO区地址格式
        /// </summary>
        private static FinsAddressInfo ParseCIOAddress(string address, FinsAddressInfo addressInfo)
        {
            addressInfo.MemoryArea = FinsMemoryArea.CIO;
            addressInfo.IsBit = false;

            var offsetStr = address.Substring(3); // 去掉CIO前缀
            if (!int.TryParse(offsetStr, out int offset))
                throw new ArgumentException($"CIO地址偏移解析失败: {address}");

            addressInfo.Address = offset;
            addressInfo.Length = GetDataTypeLength(addressInfo.TargetDataType);
            addressInfo.BitAddress = 0;

            return addressInfo;
        }

        /// <summary>
        /// 解析工作区地址格式
        /// </summary>
        private static FinsAddressInfo ParseWRAddress(string address, FinsAddressInfo addressInfo)
        {
            addressInfo.MemoryArea = FinsMemoryArea.WR;
            addressInfo.IsBit = false;

            var offsetStr = address.Substring(1); // 去掉W前缀
            if (!int.TryParse(offsetStr, out int offset))
                throw new ArgumentException($"W地址偏移解析失败: {address}");

            addressInfo.Address = offset;
            addressInfo.Length = GetDataTypeLength(addressInfo.TargetDataType);
            addressInfo.BitAddress = 0;

            return addressInfo;
        }

        /// <summary>
        /// 解析保持区地址格式
        /// </summary>
        private static FinsAddressInfo ParseHRAddress(string address, FinsAddressInfo addressInfo)
        {
            addressInfo.MemoryArea = FinsMemoryArea.HR;
            addressInfo.IsBit = false;

            var offsetStr = address.Substring(1); // 去掉H前缀
            if (!int.TryParse(offsetStr, out int offset))
                throw new ArgumentException($"H地址偏移解析失败: {address}");

            addressInfo.Address = offset;
            addressInfo.Length = GetDataTypeLength(addressInfo.TargetDataType);
            addressInfo.BitAddress = 0;

            return addressInfo;
        }

        /// <summary>
        /// 解析辅助区地址格式
        /// </summary>
        private static FinsAddressInfo ParseARAddress(string address, FinsAddressInfo addressInfo)
        {
            addressInfo.MemoryArea = FinsMemoryArea.AR;
            addressInfo.IsBit = false;

            var offsetStr = address.Substring(1); // 去掉A前缀
            if (!int.TryParse(offsetStr, out int offset))
                throw new ArgumentException($"A地址偏移解析失败: {address}");

            addressInfo.Address = offset;
            addressInfo.Length = GetDataTypeLength(addressInfo.TargetDataType);
            addressInfo.BitAddress = 0;

            return addressInfo;
        }

        /// <summary>
        /// 解析扩展数据内存区地址格式
        /// </summary>
        private static FinsAddressInfo ParseEMAddress(string address, FinsAddressInfo addressInfo)
        {
            addressInfo.MemoryArea = FinsMemoryArea.EM;
            addressInfo.IsBit = false;

            var offsetStr = address.Substring(1); // 去掉E前缀
            if (!int.TryParse(offsetStr, out int offset))
                throw new ArgumentException($"E地址偏移解析失败: {address}");

            addressInfo.Address = offset;
            addressInfo.Length = GetDataTypeLength(addressInfo.TargetDataType);
            addressInfo.BitAddress = 0;

            return addressInfo;
        }

        /// <summary>
        /// 解析定时器地址格式
        /// </summary>
        private static FinsAddressInfo ParseTimerAddress(string address, FinsAddressInfo addressInfo)
        {
            addressInfo.IsBit = false;
            addressInfo.BitAddress = 0;

            string offsetStr;
            if (address.StartsWith("TIM"))
            {
                offsetStr = address.Substring(3);
            }
            else
            {
                offsetStr = address.Substring(1); // 去掉T前缀
            }

            if (!int.TryParse(offsetStr, out int offset))
                throw new ArgumentException($"定时器地址偏移解析失败: {address}");

            // 根据数据类型判断是标志还是当前值
            if (addressInfo.TargetDataType == DataTypeEnums.Bool)
            {
                addressInfo.MemoryArea = FinsMemoryArea.TIM_FLAG;
                addressInfo.Length = 1;
            }
            else
            {
                addressInfo.MemoryArea = FinsMemoryArea.TIM_PV;
                addressInfo.Length = GetDataTypeLength(addressInfo.TargetDataType);
            }

            addressInfo.Address = offset;
            return addressInfo;
        }

        /// <summary>
        /// 解析计数器地址格式
        /// </summary>
        private static FinsAddressInfo ParseCounterAddress(string address, FinsAddressInfo addressInfo)
        {
            addressInfo.IsBit = false;
            addressInfo.BitAddress = 0;

            string offsetStr;
            if (address.StartsWith("CNT"))
            {
                offsetStr = address.Substring(3);
            }
            else
            {
                offsetStr = address.Substring(1); // 去掉C前缀
            }

            if (!int.TryParse(offsetStr, out int offset))
                throw new ArgumentException($"计数器地址偏移解析失败: {address}");

            // 根据数据类型判断是标志还是当前值
            if (addressInfo.TargetDataType == DataTypeEnums.Bool)
            {
                addressInfo.MemoryArea = FinsMemoryArea.CNT_FLAG;
                addressInfo.Length = 1;
            }
            else
            {
                addressInfo.MemoryArea = FinsMemoryArea.CNT_PV;
                addressInfo.Length = GetDataTypeLength(addressInfo.TargetDataType);
            }

            addressInfo.Address = offset;
            return addressInfo;
        }

        /// <summary>
        /// 获取数据类型对应的字节长度
        /// </summary>
        /// <param name="dataType">数据类型</param>
        /// <returns>字节长度</returns>
        private static int GetDataTypeLength(DataTypeEnums dataType)
        {
            switch (dataType)
            {
                case DataTypeEnums.Bool:
                    return 1;
                case DataTypeEnums.Byte:
                    return 1;
                case DataTypeEnums.UInt16:
                case DataTypeEnums.Int16:
                    return 1; // FINS中字长度为1个字（2字节）
                case DataTypeEnums.UInt32:
                case DataTypeEnums.Int32:
                case DataTypeEnums.Float:
                    return 2; // FINS中双字长度为2个字（4字节）
                case DataTypeEnums.UInt64:
                case DataTypeEnums.Int64:
                case DataTypeEnums.Double:
                    return 4; // FINS中四字长度为4个字（8字节）
                default:
                    return 1; // 默认长度
            }
        }

        /// <summary>
        /// 优化FINS地址块，将地址合并为高效的读取块
        /// </summary>
        /// <param name="addressInfos">地址信息列表</param>
        /// <param name="minEfficiencyRatio">最小效率比（有效数据/总读取数据）</param>
        /// <param name="maxBlockSize">最大块大小（字）</param>
        /// <returns>优化后的地址块列表</returns>
        public static List<FinsAddressBlock> OptimizeFinsAddressBlocks(List<FinsAddressInfo> addressInfos, double minEfficiencyRatio = 0.8, int maxBlockSize = 180)
        {
            var optimizedBlocks = new List<FinsAddressBlock>();
            
            // 按内存区域分组
            var areaGroups = addressInfos.GroupBy(a => a.MemoryArea).ToList();
            
            foreach (var areaGroup in areaGroups)
            {
                var memoryArea = areaGroup.Key;
                
                // 特殊处理位地址
                if (areaGroup.Any(a => a.IsBit))
                {
                    // 对于位地址，按字边界进行优化
                    var bitAddresses = areaGroup.Where(a => a.IsBit).ToList();
                    var nonBitAddresses = areaGroup.Where(a => !a.IsBit).ToList();
                    
                    // 处理位地址
                    if (bitAddresses.Count > 0)
                    {
                        var bitBlocks = OptimizeBitAddresses(bitAddresses, memoryArea, maxBlockSize);
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
                    var sortedAddresses = areaGroup.OrderBy(a => a.Address).ToList();
                    var blocks = OptimizeNonBitAddresses(sortedAddresses, minEfficiencyRatio, maxBlockSize);
                    optimizedBlocks.AddRange(blocks);
                }
            }
            
            return optimizedBlocks;
        }

        /// <summary>
        /// 优化位地址
        /// </summary>
        private static List<FinsAddressBlock> OptimizeBitAddresses(List<FinsAddressInfo> bitAddresses, FinsMemoryArea memoryArea, int maxBlockSize)
        {
            var optimizedBlocks = new List<FinsAddressBlock>();
            
            // 先按Address、BitAddress排序
            var sortedBitAddresses = bitAddresses.OrderBy(a => a.Address)
                                               .ThenBy(a => a.BitAddress)
                                               .ToList();
            
            // 按字地址分组
            var wordGroups = sortedBitAddresses.GroupBy(a => a.Address).ToList();
            
            foreach (var wordGroup in wordGroups)
            {
                var wordAddress = wordGroup.Key;
                // 按BitAddress排序位地址
                var addresses = wordGroup.OrderBy(a => a.BitAddress).ToList();
                
                // 每个字作为一个块
                var block = new FinsAddressBlock
                {
                    MemoryArea = memoryArea,
                    StartAddress = wordAddress,
                    TotalLength = 1, // 读取一个字
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
        private static List<FinsAddressBlock> OptimizeNonBitAddresses(List<FinsAddressInfo> addresses, double minEfficiencyRatio, int maxBlockSize)
        {
            var optimizedBlocks = new List<FinsAddressBlock>();
            
            // 按Address排序
            var sortedAddresses = addresses.OrderBy(a => a.Address).ToList();
            
            var currentBlock = new FinsAddressBlock
            {
                MemoryArea = addresses[0].MemoryArea,
                Addresses = new List<FinsAddressInfo>()
            };
            
            foreach (var address in sortedAddresses)
            {
                // 如果是第一个地址，直接加入当前块
                if (currentBlock.Addresses.Count == 0)
                {
                    currentBlock.StartAddress = address.Address;
                    currentBlock.TotalLength = address.Length;
                    currentBlock.Addresses.Add(address);
                    continue;
                }
                
                // 计算如果加入此地址后的新块参数
                var newStartAddress = Math.Min(currentBlock.StartAddress, address.Address);
                var currentEndAddress = currentBlock.StartAddress + currentBlock.TotalLength;
                var addressEndAddress = address.Address + address.Length;
                var newEndAddress = Math.Max(currentEndAddress, addressEndAddress);
                var newTotalLength = newEndAddress - newStartAddress;
                
                // 检查块大小限制
                if (newTotalLength > maxBlockSize)
                {
                    // 超过最大块大小，完成当前块并开始新块
                    currentBlock.EfficiencyRatio = CalculateEfficiencyRatio(currentBlock);
                    optimizedBlocks.Add(currentBlock);
                    
                    currentBlock = new FinsAddressBlock
                    {
                        MemoryArea = address.MemoryArea,
                        StartAddress = address.Address,
                        TotalLength = address.Length,
                        Addresses = new List<FinsAddressInfo> { address }
                    };
                    continue;
                }
                
                // 计算加入后的效率比
                var testBlock = new FinsAddressBlock
                {
                    MemoryArea = address.MemoryArea,
                    StartAddress = newStartAddress,
                    TotalLength = newTotalLength,
                    Addresses = new List<FinsAddressInfo>(currentBlock.Addresses) { address }
                };
                
                var newEfficiencyRatio = CalculateEfficiencyRatio(testBlock);
                
                // 检查效率比是否满足要求
                if (newEfficiencyRatio >= minEfficiencyRatio)
                {
                    // 效率比满足要求，合并地址
                    currentBlock.StartAddress = newStartAddress;
                    currentBlock.TotalLength = newTotalLength;
                    currentBlock.Addresses.Add(address);
                }
                else
                {
                    // 效率比不满足要求，完成当前块并开始新块
                    currentBlock.EfficiencyRatio = CalculateEfficiencyRatio(currentBlock);
                    optimizedBlocks.Add(currentBlock);
                    
                    currentBlock = new FinsAddressBlock
                    {
                        MemoryArea = address.MemoryArea,
                        StartAddress = address.Address,
                        TotalLength = address.Length,
                        Addresses = new List<FinsAddressInfo> { address }
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
        private static double CalculateEfficiencyRatio(FinsAddressBlock block)
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
        public static Dictionary<string, object> ExtractDataFromFinsBlocks(Dictionary<string, byte[]> blockData, List<FinsAddressBlock> blocks, List<FinsAddressInfo> originalAddresses)
        {
            var result = new Dictionary<string, object>();

            foreach (var block in blocks)
            {
                // 构造块键
                string blockKey = $"{block.MemoryArea}_{block.StartAddress}_{block.TotalLength}";
                
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
                        var relativeOffset = (address.Address - block.StartAddress) * 2; // FINS中每个字为2字节
                        
                        if (relativeOffset < 0 || relativeOffset >= data.Length)
                        {
                            result[address.OriginalAddress] = null;
                            continue;
                        }

                        object value = ExtractValueFromBytes(data, relativeOffset, address, EndianFormat.ABCD);
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
        /// <param name="dataFormat">数据格式</param>
        /// <returns>提取的值</returns>
        private static object ExtractValueFromBytes(byte[] data, int offset, FinsAddressInfo addressInfo, EndianFormat dataFormat = EndianFormat.ABCD)
        {
            try
            {
                if (addressInfo.IsBit)
                {
                    // 位数据
                    if (offset + 1 < data.Length)
                    {
                        var wordValue = data.ToUInt16(offset, true); // FINS使用大端序
                        return (wordValue & (1 << addressInfo.BitAddress)) != 0;
                    }
                    return false;
                }

                switch (addressInfo.TargetDataType)
                {
                    case DataTypeEnums.Bool:
                        if (offset + 1 < data.Length)
                        {
                            return data.ToUInt16(offset, true) != 0;
                        }
                        return false;

                    case DataTypeEnums.Byte:
                        if (offset < data.Length)
                        {
                            return data[offset];
                        }
                        return (byte)0;

                    case DataTypeEnums.UInt16:
                        if (offset + 1 < data.Length)
                        {
                            return data.ToUInt16(offset, true);
                        }
                        return (ushort)0;

                    case DataTypeEnums.Int16:
                        if (offset + 1 < data.Length)
                        {
                            return data.ToInt16(offset, true);
                        }
                        return (short)0;

                    case DataTypeEnums.UInt32:
                        if (offset + 3 < data.Length)
                        {
                            return data.ToUInt32(offset, dataFormat);
                        }
                        return (uint)0;

                    case DataTypeEnums.Int32:
                        if (offset + 3 < data.Length)
                        {
                            return data.ToInt32(offset, dataFormat);
                        }
                        return 0;

                    case DataTypeEnums.Float:
                        if (offset + 3 < data.Length)
                        {
                            return data.ToFloat(offset, dataFormat);
                        }
                        return 0.0f;

                    case DataTypeEnums.UInt64:
                        if (offset + 7 < data.Length)
                        {
                            return data.ToUInt64(offset, dataFormat);
                        }
                        return (ulong)0;

                    case DataTypeEnums.Int64:
                        if (offset + 7 < data.Length)
                        {
                            return data.ToInt64(offset, dataFormat);
                        }
                        return (long)0;

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
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 将值转换为FINS协议所需的字节数组
        /// </summary>
        /// <param name="value">要转换的值</param>
        /// <param name="addressInfo">地址信息</param>
        /// <param name="dataFormat">数据格式</param>
        /// <returns>字节数组，转换失败返回null</returns>
        public static byte[] ConvertValueToFinsBytes(object value, FinsAddressInfo addressInfo, EndianFormat dataFormat = EndianFormat.ABCD)
        {
            try
            {
                if (addressInfo.IsBit)
                {
                    // 位数据
                    if (value is bool boolValue)
                    {
                        var wordArray = new byte[2];
                        if (boolValue)
                        {
                            var wordValue = (ushort)(1 << addressInfo.BitAddress);
                            return wordValue.ToByte(true); // FINS使用大端序
                        }
                        return wordArray;
                    }
                    return null;
                }

                switch (addressInfo.TargetDataType)
                {
                    case DataTypeEnums.Bool:
                        if (value is bool boolValue)
                        {
                            return ((ushort)(boolValue ? 1 : 0)).ToByte(true);
                        }
                        return null;

                    case DataTypeEnums.Byte:
                        if (value is byte byteValue)
                        {
                            return new byte[] { byteValue, 0x00 }; // FINS中字节数据也占用一个字
                        }
                        else if (value is int intValue)
                        {
                            return new byte[] { (byte)intValue, 0x00 };
                        }
                        return null;

                    case DataTypeEnums.UInt16:
                        if (value is ushort ushortValue)
                        {
                            return ushortValue.ToByte(true);
                        }
                        else if (value is int intValue)
                        {
                            return ((ushort)intValue).ToByte(true);
                        }
                        return null;

                    case DataTypeEnums.Int16:
                        if (value is short shortValue)
                        {
                            return shortValue.ToByte(true);
                        }
                        else if (value is int intValue)
                        {
                            return ((short)intValue).ToByte(true);
                        }
                        return null;

                    case DataTypeEnums.UInt32:
                        if (value is uint uint32Value)
                        {
                            return uint32Value.ToByte(dataFormat);
                        }
                        else if (value is int intValue)
                        {
                            return ((uint)intValue).ToByte(dataFormat);
                        }
                        return null;

                    case DataTypeEnums.Int32:
                        if (value is int int32Value)
                        {
                            return int32Value.ToByte(dataFormat);
                        }
                        return null;

                    case DataTypeEnums.Float:
                        if (value is float floatValue)
                        {
                            return floatValue.ToByte(dataFormat);
                        }
                        return null;

                    case DataTypeEnums.UInt64:
                        if (value is ulong ulongValue)
                        {
                            return ulongValue.ToByte(dataFormat);
                        }
                        return null;

                    case DataTypeEnums.Int64:
                        if (value is long longValue)
                        {
                            return longValue.ToByte(dataFormat);
                        }
                        return null;

                    case DataTypeEnums.Double:
                        if (value is double doubleValue)
                        {
                            return doubleValue.ToByte(dataFormat);
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
        /// 构造FINS写入地址字符串
        /// </summary>
        /// <param name="addressInfo">地址信息</param>
        /// <returns>写入地址字符串，构造失败返回null</returns>
        public static string ConstructFinsWriteAddress(FinsAddressInfo addressInfo)
        {
            try
            {
                var areaPrefix = GetFinsAreaPrefix(addressInfo.MemoryArea);
                
                if (addressInfo.IsBit)
                {
                    return $"{areaPrefix}{addressInfo.Address}.{addressInfo.BitAddress:D2}";
                }
                else
                {
                    return $"{areaPrefix}{addressInfo.Address}";
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 根据FINS内存区域获取地址前缀
        /// </summary>
        /// <param name="memoryArea">内存区域</param>
        /// <returns>地址前缀字符串</returns>
        public static string GetFinsAreaPrefix(FinsMemoryArea memoryArea)
        {
            switch (memoryArea)
            {
                case FinsMemoryArea.CIO:
                    return "CIO";
                case FinsMemoryArea.WR:
                    return "W";
                case FinsMemoryArea.HR:
                    return "H";
                case FinsMemoryArea.AR:
                    return "A";
                case FinsMemoryArea.DM:
                    return "D";
                case FinsMemoryArea.EM:
                    return "E";
                case FinsMemoryArea.TIM_FLAG:
                case FinsMemoryArea.TIM_PV:
                    return "T";
                case FinsMemoryArea.CNT_FLAG:
                case FinsMemoryArea.CNT_PV:
                    return "C";
                default:
                    return "UNKNOWN";
            }
        }
        
        /// <summary>
        /// 判断FINS内存区域是否支持位操作
        /// </summary>
        /// <param name="memoryArea">内存区域</param>
        /// <returns>是否支持位操作</returns>
        public static bool IsBitOperationSupported(FinsMemoryArea memoryArea)
        {
            return memoryArea == FinsMemoryArea.CIO || 
                   memoryArea == FinsMemoryArea.WR || 
                   memoryArea == FinsMemoryArea.HR || 
                   memoryArea == FinsMemoryArea.AR;
        }
    }
}
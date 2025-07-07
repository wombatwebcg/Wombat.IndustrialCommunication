using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Modbus批量读写通用工具类
    /// </summary>
    public static class ModbusBatchHelper
    {
        public struct ModbusAddressInfo
        {
            public string OriginalAddress { get; set; }
            public byte StationNumber { get; set; }
            public byte FunctionCode { get; set; }
            public ushort Address { get; set; }
            public int Length { get; set; }
            public DataTypeEnums DataType { get; set; }
        }

        public class ModbusAddressBlock
        {
            public byte StationNumber { get; set; }
            public byte FunctionCode { get; set; }
            public ushort StartAddress { get; set; }
            public ushort TotalLength { get; set; }
            public List<ModbusAddressInfo> Addresses { get; set; } = new List<ModbusAddressInfo>();
            public double EfficiencyRatio { get; set; }
        }

        /// <summary>
        /// 预处理地址字符串，提取纯地址部分
        /// </summary>
        /// <param name="address">原始地址字符串</param>
        /// <returns>处理后的纯地址字符串</returns>
        private static string PreprocessAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return address;

            // 移除首尾的空白字符
            address = address.Trim();

            // 处理 {地址, 值} 格式
            if (address.StartsWith("{") && address.Contains(","))
            {
                var endBraceIndex = address.LastIndexOf("}");
                if (endBraceIndex > 0)
                {
                    var content = address.Substring(1, endBraceIndex - 1);
                    var commaIndex = content.IndexOf(",");
                    if (commaIndex > 0)
                    {
                        return content.Substring(0, commaIndex).Trim();
                    }
                }
            }

            // 处理 [地址, 值] 格式
            if (address.StartsWith("[") && address.Contains(","))
            {
                var endBracketIndex = address.LastIndexOf("]");
                if (endBracketIndex > 0)
                {
                    var content = address.Substring(1, endBracketIndex - 1);
                    var commaIndex = content.IndexOf(",");
                    if (commaIndex > 0)
                    {
                        return content.Substring(0, commaIndex).Trim();
                    }
                }
            }

            // 处理 (地址, 值) 格式
            if (address.StartsWith("(") && address.Contains(","))
            {
                var endParenIndex = address.LastIndexOf(")");
                if (endParenIndex > 0)
                {
                    var content = address.Substring(1, endParenIndex - 1);
                    var commaIndex = content.IndexOf(",");
                    if (commaIndex > 0)
                    {
                        return content.Substring(0, commaIndex).Trim();
                    }
                }
            }

            // 如果没有特殊格式，直接返回原地址
            return address;
        }

        public static List<ModbusAddressInfo> ParseModbusAddresses(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            return ParseModbusAddresses(addresses, true); // 默认为写操作
        }

        public static List<ModbusAddressInfo> ParseModbusAddresses(Dictionary<string, (DataTypeEnums, object)> addresses, bool isWrite)
        {
            var addressInfos = new List<ModbusAddressInfo>();
            foreach (var kvp in addresses)
            {
                try
                {
                    var addressInfo = ParseSingleModbusAddress(kvp.Key, kvp.Value.Item1, isWrite);
                    addressInfos.Add(addressInfo);
                }
                catch
                {
                    continue;
                }
            }
            return addressInfos;
        }

        public static List<ModbusAddressInfo> ParseModbusAddresses(Dictionary<string, DataTypeEnums> addresses)
        {
            return ParseModbusAddresses(addresses, false); // 默认为读操作
        }

        public static List<ModbusAddressInfo> ParseModbusAddresses(Dictionary<string, DataTypeEnums> addresses, bool isWrite)
        {
            var addressInfos = new List<ModbusAddressInfo>();
            foreach (var kvp in addresses)
            {
                try
                {
                    var addressInfo = ParseSingleModbusAddress(kvp.Key, kvp.Value, isWrite);
                    addressInfos.Add(addressInfo);
                }
                catch
                {
                    continue;
                }
            }
            return addressInfos;
        }

        public static ModbusAddressInfo ParseSingleModbusAddress(string address, DataTypeEnums dataType = DataTypeEnums.None)
        {
            return ParseSingleModbusAddress(address, dataType, false);
        }

        public static ModbusAddressInfo ParseSingleModbusAddress(string address, DataTypeEnums dataType, bool isWrite)
        {
            var addressInfo = new ModbusAddressInfo { OriginalAddress = address };
            
            // 预处理地址，提取纯地址部分
            var processedAddress = PreprocessAddress(address);
            
            // 使用增强解析器（统一使用增强方式）
            if (ModbusAddressParser.TryParseModbusAddress(processedAddress, dataType, isWrite, out var modbusHeader))
            {
                addressInfo.StationNumber = modbusHeader.StationNumber;
                addressInfo.FunctionCode = modbusHeader.FunctionCode;
                addressInfo.Address = modbusHeader.Address;
                
                // 优先使用传入的DataTypeEnums，如果没有指定则根据功能码推断
                if (dataType != DataTypeEnums.None)
                {
                    addressInfo.DataType = dataType;
                }
                else
                {
                    // 根据功能码推断数据类型（保持向后兼容）
                    switch (modbusHeader.FunctionCode)
                    {
                        case 0x01:  // 读线圈
                        case 0x02:  // 读离散输入
                            addressInfo.DataType = DataTypeEnums.Bool;
                            break;
                        case 0x03:  // 读保持寄存器
                        case 0x04:  // 读输入寄存器
                            addressInfo.DataType = DataTypeEnums.UInt16;
                            break;
                        case 0x05:  // 写单个线圈
                            addressInfo.DataType = DataTypeEnums.Bool;
                            break;
                        case 0x06:  // 写单个寄存器
                        case 0x10:  // 写多个寄存器
                            addressInfo.DataType = DataTypeEnums.UInt16;
                            break;
                        case 0x0F:  // 写多个线圈
                            addressInfo.DataType = DataTypeEnums.Bool;
                            break;
                        default:
                            throw new ArgumentException($"不支持的功能码: {modbusHeader.FunctionCode}");
                    }
                }
                
                // 根据DataTypeEnums确定长度
                switch (addressInfo.DataType)
                {
                    case DataTypeEnums.Bool:
                        addressInfo.Length = 1;
                        break;
                    case DataTypeEnums.Byte:
                        addressInfo.Length = 1;
                        break;
                    case DataTypeEnums.Int16:
                    case DataTypeEnums.UInt16:
                        addressInfo.Length = 2;
                        break;
                    case DataTypeEnums.Int32:
                    case DataTypeEnums.UInt32:
                    case DataTypeEnums.Float:
                        addressInfo.Length = 4;
                        break;
                    case DataTypeEnums.Int64:
                    case DataTypeEnums.UInt64:
                    case DataTypeEnums.Double:
                        addressInfo.Length = 8;
                        break;
                    default:
                        addressInfo.Length = 2; // 默认长度
                        break;
                }
            }
            else
            {
                throw new ArgumentException($"无效的Modbus地址格式: {address} (处理后: {processedAddress})");
            }
            return addressInfo;
        }

        public static List<ModbusAddressBlock> OptimizeModbusAddressBlocks(List<ModbusAddressInfo> addressInfos, double minEfficiencyRatio = 0.7, ushort maxBlockSize = 125)
        {
            var optimizedBlocks = new List<ModbusAddressBlock>();
            var groups = addressInfos.GroupBy(a => new { a.StationNumber, a.FunctionCode }).ToList();
            foreach (var group in groups)
            {
                var sortedAddresses = group.OrderBy(a => a.Address).ToList();
                var blocks = OptimizeAddressesInGroup(sortedAddresses, minEfficiencyRatio, maxBlockSize);
                optimizedBlocks.AddRange(blocks);
            }
            return optimizedBlocks;
        }

        private static List<ModbusAddressBlock> OptimizeAddressesInGroup(List<ModbusAddressInfo> addresses, double minEfficiencyRatio, ushort maxBlockSize)
        {
            // 移除强制的块大小限制，使用传入的maxBlockSize参数
            // maxBlockSize = Math.Min(maxBlockSize, (ushort)120); // 移除这个限制
            
            var optimizedBlocks = new List<ModbusAddressBlock>();
            var currentBlock = new ModbusAddressBlock
            {
                StationNumber = addresses[0].StationNumber,
                FunctionCode = addresses[0].FunctionCode,
                Addresses = new List<ModbusAddressInfo>()
            };
            
            // 创建地址依赖关系表，标记哪些地址不能被拆分（例如一个double值可能跨多个寄存器）
            var addressDependencies = new Dictionary<ushort, List<ushort>>();
            foreach (var address in addresses)
            {
                // 对于长度超过2的数据类型（如float、double、int32等），记录所有相关地址
                if (address.Length > 2)
                {
                    var relatedAddresses = new List<ushort>();
                    for (ushort i = 0; i < address.Length / 2; i++)
                    {
                        relatedAddresses.Add((ushort)(address.Address + i));
                    }
                    
                    foreach (var addr in relatedAddresses)
                    {
                        if (!addressDependencies.ContainsKey(addr))
                        {
                            addressDependencies[addr] = new List<ushort>();
                        }
                        
                        // 将所有相关地址添加到依赖列表
                        foreach (var relAddr in relatedAddresses)
                        {
                            if (addr != relAddr && !addressDependencies[addr].Contains(relAddr))
                            {
                                addressDependencies[addr].Add(relAddr);
                            }
                        }
                    }
                }
            }
            
            foreach (var address in addresses)
            {
                if (currentBlock.Addresses.Count == 0)
                {
                    currentBlock.StartAddress = address.Address;
                    currentBlock.TotalLength = (ushort)address.Length;
                    currentBlock.Addresses.Add(address);
                    continue;
                }
                
                var newStartAddress = Math.Min(currentBlock.StartAddress, address.Address);
                var currentEndAddress = currentBlock.StartAddress + currentBlock.TotalLength;
                var addressEndAddress = address.Address + (ushort)address.Length;
                var newEndAddress = Math.Max(currentEndAddress, addressEndAddress);
                var newTotalLength = (ushort)(newEndAddress - newStartAddress);
                
                // 检查是否超过最大块大小，但仍允许必要的地址合并
                // 修改策略：如果当前块已经有地址且新块长度超限，则完成当前块并创建新块
                if (newTotalLength > maxBlockSize && currentBlock.Addresses.Count > 0)
                {
                    // 在添加新块之前，确保当前块不会截断任何依赖关系
                    // 不再将maxBlockSize作为限制，而是确保数据完整性
                    CheckAndEnsureDataIntegrity(currentBlock, addressDependencies);
                    
                    currentBlock.EfficiencyRatio = CalculateModbusEfficiencyRatio(currentBlock);
                    optimizedBlocks.Add(currentBlock);
                    
                    currentBlock = new ModbusAddressBlock
                    {
                        StationNumber = address.StationNumber,
                        FunctionCode = address.FunctionCode,
                        StartAddress = address.Address,
                        TotalLength = (ushort)address.Length,
                        Addresses = new List<ModbusAddressInfo> { address }
                    };
                    continue;
                }
                
                // 如果是单个地址超限，也必须接受它，确保所有地址都能被处理
                if (address.Length > maxBlockSize)
                {
                    // 如果当前块已有其他地址，先完成当前块
                    if (currentBlock.Addresses.Count > 0)
                    {
                        CheckAndEnsureDataIntegrity(currentBlock, addressDependencies);
                        currentBlock.EfficiencyRatio = CalculateModbusEfficiencyRatio(currentBlock);
                        optimizedBlocks.Add(currentBlock);
                    }
                    
                    // 创建一个专门处理此超大地址的块
                    var largeAddressBlock = new ModbusAddressBlock
                    {
                        StationNumber = address.StationNumber,
                        FunctionCode = address.FunctionCode,
                        StartAddress = address.Address,
                        TotalLength = (ushort)address.Length,
                        Addresses = new List<ModbusAddressInfo> { address }
                    };
                    
                    largeAddressBlock.EfficiencyRatio = 1.0; // 单个地址，效率比为1
                    optimizedBlocks.Add(largeAddressBlock);
                    
                    // 重置当前块为空
                    currentBlock = new ModbusAddressBlock
                    {
                        StationNumber = address.StationNumber,
                        FunctionCode = address.FunctionCode,
                        Addresses = new List<ModbusAddressInfo>()
                    };
                    continue;
                }
                
                var testBlock = new ModbusAddressBlock
                {
                    StationNumber = address.StationNumber,
                    FunctionCode = address.FunctionCode,
                    StartAddress = newStartAddress,
                    TotalLength = newTotalLength,
                    Addresses = new List<ModbusAddressInfo>(currentBlock.Addresses) { address }
                };
                
                var newEfficiencyRatio = CalculateModbusEfficiencyRatio(testBlock);
                
                if (newEfficiencyRatio >= minEfficiencyRatio || HasDependency(addressDependencies, currentBlock.Addresses, address))
                {
                    // 当效率比满足要求，或者存在依赖关系时，合并地址
                    currentBlock.StartAddress = newStartAddress;
                    currentBlock.TotalLength = newTotalLength;
                    currentBlock.Addresses.Add(address);
                }
                else
                {
                    // 在添加新块之前，确保当前块不会截断任何依赖关系
                    CheckAndEnsureDataIntegrity(currentBlock, addressDependencies);
                    
                    currentBlock.EfficiencyRatio = CalculateModbusEfficiencyRatio(currentBlock);
                    optimizedBlocks.Add(currentBlock);
                    
                    currentBlock = new ModbusAddressBlock
                    {
                        StationNumber = address.StationNumber,
                        FunctionCode = address.FunctionCode,
                        StartAddress = address.Address,
                        TotalLength = (ushort)address.Length,
                        Addresses = new List<ModbusAddressInfo> { address }
                    };
                }
            }
            
            if (currentBlock.Addresses.Count > 0)
            {
                // 最后一个块也需要检查数据完整性
                CheckAndEnsureDataIntegrity(currentBlock, addressDependencies);
                
                currentBlock.EfficiencyRatio = CalculateModbusEfficiencyRatio(currentBlock);
                optimizedBlocks.Add(currentBlock);
            }
            
            // 合并有依赖关系的块，不再限制最大块大小
            return MergeRelatedBlocks(optimizedBlocks);
        }

        // 检查地址是否与已有地址存在依赖关系
        private static bool HasDependency(Dictionary<ushort, List<ushort>> addressDependencies, List<ModbusAddressInfo> existingAddresses, ModbusAddressInfo newAddress)
        {
            // 如果新地址在依赖关系表中
            if (addressDependencies.TryGetValue(newAddress.Address, out var newAddressDependencies))
            {
                // 检查新地址的依赖是否在现有地址中
                foreach (var existingAddress in existingAddresses)
                {
                    if (newAddressDependencies.Contains(existingAddress.Address))
                    {
                        return true;
                    }
                }
            }
            
            // 检查现有地址是否依赖新地址
            foreach (var existingAddress in existingAddresses)
            {
                if (addressDependencies.TryGetValue(existingAddress.Address, out var existingDependencies) &&
                    existingDependencies.Contains(newAddress.Address))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 检查并确保数据完整性，防止数据类型被截断
        /// </summary>
        private static void CheckAndEnsureDataIntegrity(ModbusAddressBlock block, Dictionary<ushort, List<ushort>> addressDependencies)
        {
            // 对于每个地址，检查其相关依赖是否都已包含在当前块中
            var currentAddresses = block.Addresses.Select(a => a.Address).ToList();
            var addressesToCheck = new List<ushort>(currentAddresses);
            
            foreach (var address in addressesToCheck)
            {
                if (addressDependencies.ContainsKey(address))
                {
                    // 查找此地址的所有依赖地址
                    foreach (var relatedAddr in addressDependencies[address])
                    {
                        // 如果依赖地址不在当前块中，调整块的边界以包含它
                        if (!currentAddresses.Contains(relatedAddr))
                        {
                            // 更新块边界以包含所有依赖地址，不再考虑maxBlockSize限制
                            if (relatedAddr < block.StartAddress)
                            {
                                block.StartAddress = relatedAddr;
                            }
                            
                            var endAddr = (ushort)(relatedAddr + 2); // 每个寄存器占2字节
                            var currentEnd = (ushort)(block.StartAddress + block.TotalLength);
                            if (endAddr > currentEnd)
                            {
                                block.TotalLength = (ushort)(endAddr - block.StartAddress);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 合并有依赖关系的块，确保数据完整性
        /// </summary>
        private static List<ModbusAddressBlock> MergeRelatedBlocks(List<ModbusAddressBlock> blocks)
        {
            if (blocks.Count <= 1)
                return blocks;

            var result = new List<ModbusAddressBlock>();
            var i = 0;

            while (i < blocks.Count)
            {
                var currentBlock = blocks[i];
                var merged = false;

                // 检查当前块和后续块是否需要合并
                for (int j = i + 1; j < blocks.Count; j++)
                {
                    var nextBlock = blocks[j];
                    
                    // 检查两个块是否有重叠或紧邻
                    if (currentBlock.StationNumber == nextBlock.StationNumber && 
                        currentBlock.FunctionCode == nextBlock.FunctionCode)
                    {
                        var currentEnd = currentBlock.StartAddress + currentBlock.TotalLength;
                        var nextEnd = nextBlock.StartAddress + nextBlock.TotalLength;
                        
                        // 判断是否应该合并块
                        bool shouldMerge = false;
                        
                        // 条件1：有重叠（数据完整性要求）
                        if (currentEnd >= nextBlock.StartAddress)
                        {
                            shouldMerge = true;
                        }
                        // 条件2：块之间间距很小（可能有跨块数据）
                        else if (nextBlock.StartAddress - currentEnd <= 4)  // 允许最多4个字节的间隔，容纳double类型
                        {
                            bool hasLargeDataType = false;
                            
                            // 检查是否有跨越块边界的大型数据类型
                            foreach (var addr in currentBlock.Addresses)
                            {
                                if (addr.Length > 2 && addr.Address + addr.Length / 2 > currentEnd - 2)
                                {
                                    hasLargeDataType = true;
                                    break;
                                }
                            }
                            
                            foreach (var addr in nextBlock.Addresses)
                            {
                                if (addr.Length > 2 && addr.Address < nextBlock.StartAddress + 2)
                                {
                                    hasLargeDataType = true;
                                    break;
                                }
                            }
                            
                            shouldMerge = hasLargeDataType;
                        }
                        
                        if (shouldMerge)
                        {
                            // 合并两个块，不再考虑大小限制
                            var newStartAddress = Math.Min(currentBlock.StartAddress, nextBlock.StartAddress);
                            var newEndAddress = Math.Max(currentEnd, nextEnd);
                            var newTotalLength = (ushort)(newEndAddress - newStartAddress);
                            
                            // 创建合并后的块
                            currentBlock = new ModbusAddressBlock
                            {
                                StationNumber = currentBlock.StationNumber,
                                FunctionCode = currentBlock.FunctionCode,
                                StartAddress = newStartAddress,
                                TotalLength = newTotalLength,
                                Addresses = new List<ModbusAddressInfo>(currentBlock.Addresses)
                            };
                            
                            // 添加下一个块的地址
                            foreach (var addr in nextBlock.Addresses)
                            {
                                if (!currentBlock.Addresses.Any(a => a.OriginalAddress == addr.OriginalAddress))
                                {
                                    currentBlock.Addresses.Add(addr);
                                }
                            }
                            
                            // 更新效率比
                            currentBlock.EfficiencyRatio = CalculateModbusEfficiencyRatio(currentBlock);
                            
                            blocks.RemoveAt(j);
                            merged = true;
                            j--; // 回退索引以处理可能的连续合并
                        }
                    }
                }
                
                // 如果没有合并，将当前块添加到结果中
                if (!merged)
                {
                    result.Add(currentBlock);
                    i++;
                }
                else
                {
                    // 如果有合并，重新检查当前块
                    blocks[i] = currentBlock;
                }
            }
            
            return result;
        }

        private static double CalculateModbusEfficiencyRatio(ModbusAddressBlock block)
        {
            if (block.TotalLength == 0) return 0;
            var effectiveDataLength = block.Addresses.Sum(a => a.Length);
            return (double)effectiveDataLength / block.TotalLength;
        }

        public static Dictionary<string, object> ExtractDataFromModbusBlocks(Dictionary<string, byte[]> blockData, List<ModbusAddressBlock> blocks, List<ModbusAddressInfo> originalAddresses)
        {
            var result = new Dictionary<string, object>();
            
            // 创建地址映射，以便快速查找地址信息
            var addressInfoMap = originalAddresses.ToDictionary(a => a.OriginalAddress, a => a);
            
            // 处理每个块中的地址数据
            foreach (var block in blocks)
            {
                string blockKey = $"{block.StationNumber}_{block.FunctionCode}_{block.StartAddress}_{block.TotalLength}";
                if (!blockData.TryGetValue(blockKey, out byte[] data))
                {
                    foreach (var address in block.Addresses)
                    {
                        result[address.OriginalAddress] = null;
                    }
                    continue;
                }
                
                // 添加调试信息 - 输出块的详细信息和数据
                //Console.WriteLine($"Block: {blockKey}, Data Length: {data.Length}");
                //Console.WriteLine($"Data: {string.Join(" ", data.Select(b => b.ToString("X2")))}");
                
                // 尝试处理此块中的每个地址
                foreach (var address in block.Addresses)
                {
                    // 如果此地址已经处理过并有值，则跳过
                    if (result.ContainsKey(address.OriginalAddress) && result[address.OriginalAddress] != null)
                    {
                        continue;
                    }
                    
                    try
                    {
                        if (address.FunctionCode == 0x01 || address.FunctionCode == 0x02)
                        {
                            // 线圈和离散输入类型：按位打包，每8位一个字节
                            int bitOffset = address.Address - block.StartAddress;
                            int byteOffset = bitOffset / 8;
                            int bitPosition = bitOffset % 8;
                            
                            if (byteOffset >= data.Length)
                            {
                                result[address.OriginalAddress] = null;
                                continue;
                            }
                            
                            // 提取位值
                            bool bitValue = (data[byteOffset] & (1 << bitPosition)) != 0;
                            result[address.OriginalAddress] = bitValue;
                        }
                        else
                        {
                            // 寄存器类型和其他类型
                            int relativeOffset;
                            if (address.FunctionCode == 0x03 || address.FunctionCode == 0x04)
                            {
                                // 寄存器类型：地址差值转换为字节偏移量
                                // 这里需要确保使用有符号整数计算，防止地址相减导致溢出
                                int addressOffset = (int)address.Address - (int)block.StartAddress;
                                relativeOffset = addressOffset * 2;  // 每个寄存器2字节
                                
                                // 添加调试信息 - 输出地址计算详情
                                //Console.WriteLine($"Address: {address.OriginalAddress}, Type: {address.DataType}, Value Address: {address.Address}, Block Start: {block.StartAddress}, Offset: {relativeOffset}");
                            }
                            else
                            {
                                // 其他功能码：直接使用地址差值作为字节偏移量
                                relativeOffset = (int)address.Address - (int)block.StartAddress;
                            }
                            
                            // 检查数据是否在当前块的范围内
                            if (relativeOffset < 0 || relativeOffset + address.Length > data.Length)
                            {
                                // 处理跨块数据的情况
                                if (TryMergeDataFromMultipleBlocks(blockData, blocks, address, out byte[] mergedData))
                                {
                                    object value = ExtractValueFromModbusBytes(mergedData, 0, address);
                                    result[address.OriginalAddress] = value;
                                    
                                    // 添加调试信息 - 输出跨块合并结果
                                    //Console.WriteLine($"Merged data for {address.OriginalAddress}: {string.Join(" ", mergedData.Select(b => b.ToString("X2")))}");
                                }
                                else
                                {
                                    result[address.OriginalAddress] = null;
                                    // 添加调试信息 - 输出合并失败原因
                                    //Console.WriteLine($"Failed to merge data for {address.OriginalAddress}, RelOffset: {relativeOffset}, DataLen: {data.Length}, AddrLen: {address.Length}");
                                }
                            }
                            else
                            {
                                // 数据在当前块内部，正常提取
                                object value = ExtractValueFromModbusBytes(data, relativeOffset, address);
                                result[address.OriginalAddress] = value;
                                
                                // 添加调试信息 - 输出提取的值
                                //if (address.DataType == DataTypeEnums.Double)
                                //{
                                //    Console.WriteLine($"Extracted Double: {address.OriginalAddress} = {value}, bytes: {string.Join(" ", data.Skip(relativeOffset).Take(8).Select(b => b.ToString("X2")))}");
                                //}
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result[address.OriginalAddress] = null;
                        // 添加调试信息 - 输出异常详情
                        //Console.WriteLine($"Exception extracting {address.OriginalAddress}: {ex.Message}");
                    }
                }
            }
            
            return result;
        }

        /// <summary>
        /// 尝试从多个块中合并数据
        /// </summary>
        private static bool TryMergeDataFromMultipleBlocks(Dictionary<string, byte[]> blockData, List<ModbusAddressBlock> blocks, ModbusAddressInfo address, out byte[] mergedData)
        {
            mergedData = null;
            
            try
            {
                // 仅对寄存器类型处理跨块情况
                if (address.FunctionCode != 0x03 && address.FunctionCode != 0x04)
                {
                    return false;
                }
                
                // 创建足够大的数组来存储所有数据
                mergedData = new byte[address.Length];
                
                // 标记哪些部分的数据已填充
                bool[] dataFilled = new bool[address.Length];
                
                // 找到所有可能包含此地址数据的块，按地址顺序排序
                var relevantBlocks = new List<(ModbusAddressBlock block, byte[] data)>();
                
                foreach (var block in blocks)
                {
                    // 只考虑相同站号和功能码的块
                    if (block.StationNumber != address.StationNumber || block.FunctionCode != address.FunctionCode)
                    {
                        continue;
                    }
                    
                    // 计算此地址与块的关系
                    int addressEnd = address.Address + address.Length / 2; // 地址以字（2字节）为单位的结束位置
                    int blockEnd = block.StartAddress + block.TotalLength; // 块的结束地址
                    
                    // 如果地址完全不在块范围内，跳过
                    if (address.Address >= blockEnd || addressEnd <= block.StartAddress)
                    {
                        continue;
                    }
                    
                    // 获取块数据
                    string blockKey = $"{block.StationNumber}_{block.FunctionCode}_{block.StartAddress}_{block.TotalLength}";
                    if (!blockData.TryGetValue(blockKey, out byte[] data) || data == null)
                    {
                        continue;
                    }
                    
                    relevantBlocks.Add((block, data));
                }
                
                // 按起始地址排序
                relevantBlocks = relevantBlocks.OrderBy(rb => rb.block.StartAddress).ToList();
                
                // 如果没有找到相关块，返回失败
                if (relevantBlocks.Count == 0)
                {
                    mergedData = null;
                    return false;
                }
                
                // 从每个相关块中填充数据
                foreach (var (block, data) in relevantBlocks)
                {
                    // 计算此地址在当前块中的字节偏移量
                    int addressStartOffset = Math.Max(0, (address.Address - block.StartAddress) * 2);
                    int addressEndOffset = Math.Min(address.Length, ((Math.Min(address.Address + address.Length / 2, block.StartAddress + block.TotalLength) - block.StartAddress) * 2));
                    int blockStartOffset = Math.Max(0, (block.StartAddress - address.Address) * 2);
                    
                    // 验证偏移量是否有效
                    if (addressStartOffset >= data.Length || blockStartOffset >= mergedData.Length)
                    {
                        continue;
                    }
                    
                    // 计算要复制的字节数
                    int copyLength = Math.Min(
                        data.Length - addressStartOffset,  // 块中可用数据
                        mergedData.Length - blockStartOffset   // 合并数组中的可用空间
                    );
                    copyLength = Math.Min(copyLength, addressEndOffset - addressStartOffset);
                    
                    if (copyLength <= 0)
                    {
                        continue;
                    }
                    
                    // 复制数据到合并数组中
                    try
                    {
                        Array.Copy(data, addressStartOffset, mergedData, blockStartOffset, copyLength);
                        
                        // 标记已填充的部分
                        for (int i = 0; i < copyLength; i++)
                        {
                            dataFilled[blockStartOffset + i] = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 调试信息
                        //Console.WriteLine($"Error in data merging: {ex.Message}, BlockStart: {block.StartAddress}, AddrStart: {address.Address}, " +
                        //                  $"AddrStartOffset: {addressStartOffset}, BlockStartOffset: {blockStartOffset}, CopyLength: {copyLength}, " +
                        //                  $"DataLen: {data.Length}, MergedLen: {mergedData.Length}");
                    }
                }
                
                // 检查数据是否完整填充
                if (dataFilled.All(filled => filled))
                {
                    return true;
                }
                
                // 输出调试信息，显示哪些部分没有填充
                //int firstUnfilled = Array.FindIndex(dataFilled, f => !f);
                //Console.WriteLine($"Incomplete data for address {address.OriginalAddress}. First unfilled index: {firstUnfilled}, Address: {address.Address}, Length: {address.Length}");
                
                // 数据不完整，返回失败
                mergedData = null;
                return false;
            }
            catch (Exception ex)
            {
                // 调试信息
                //Console.WriteLine($"Exception in TryMergeDataFromMultipleBlocks: {ex.Message}, Address: {address.OriginalAddress}");
                mergedData = null;
                return false;
            }
        }

        public static object ExtractValueFromModbusBytes(byte[] data, int offset, ModbusAddressInfo addressInfo, bool isReverse = true, EndianFormat dataFormat = EndianFormat.ABCD)
        {
            try
            {
                // 添加安全检查，确保数据范围有效
                if (offset < 0 || offset >= data.Length)
                {
                    return null;
                }
            
                // 基于DataTypeEnums进行类型转换
                switch (addressInfo.DataType)
                {
                    case DataTypeEnums.Bool:
                        if (offset < data.Length)
                        {
                            var byteValue = data[offset];
                            return (byteValue & 0x01) != 0;
                        }
                        return false;
                        
                    case DataTypeEnums.Byte:
                        if (offset < data.Length)
                        {
                            return data[offset];
                        }
                        return (byte)0;
                        
                    case DataTypeEnums.Int16:
                        if (offset + 1 < data.Length)
                        {
                            return data.ToInt16(offset, isReverse);
                        }
                        return (short)0;
                        
                    case DataTypeEnums.UInt16:
                        if (offset + 1 < data.Length)
                        {
                            return data.ToUInt16(offset, isReverse);
                        }
                        return (ushort)0;
                        
                    case DataTypeEnums.Int32:
                        if (offset + 3 < data.Length)
                        {
                            return data.ToInt32(offset, dataFormat);
                        }
                        return (int)0;
                        
                    case DataTypeEnums.UInt32:
                        if (offset + 3 < data.Length)
                        {
                            return data.ToUInt32(offset, dataFormat);
                        }
                        return (uint)0;
                        
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
                        
                    case DataTypeEnums.Float:
                        if (offset + 3 < data.Length)
                        {
                            return data.ToFloat(offset, dataFormat);
                        }
                        return 0.0f;
                        
                    case DataTypeEnums.Double:
                        if (offset + 7 < data.Length)
                        {
                            return data.ToDouble(offset, dataFormat);
                        }
                        return 0.0;
                        
                    default:
                        // 对于不支持的类型，尝试根据功能码进行传统转换（向后兼容）
                        switch (addressInfo.FunctionCode)
                        {
                            case 0x01:
                            case 0x02:
                                if (offset < data.Length)
                                {
                                    var byteValue = data[offset];
                                    return (byteValue & 0x01) != 0;
                                }
                                return false;
                            case 0x03:
                            case 0x04:
                                if (offset + 1 < data.Length)
                                {
                                    return data.ToUInt16(offset, isReverse);
                                }
                                return (ushort)0;
                            default:
                                return null;
                        }
                }
            }
            catch (Exception ex)
            {
                // 添加异常处理以便于调试
                //Console.WriteLine($"Error extracting value: {ex.Message}, Type: {addressInfo.DataType}, Offset: {offset}, DataLen: {data?.Length}");
                return null;
            }
        }

        public static byte[] ConvertValueToModbusBytes((DataTypeEnums,object) value, ModbusAddressInfo addressInfo, bool isReverse = true, EndianFormat dataFormat = EndianFormat.ABCD)
        {
            try
            {
                // 基于DataTypeEnums进行类型转换
                switch (addressInfo.DataType)
                {
                    case DataTypeEnums.Bool:
                        if (value.Item2 is bool boolValue)
                        {
                            // Modbus协议要求写单个线圈时为2字节：0xFF00/0x0000
                            return boolValue ? new byte[] { 0xFF, 0x00 } : new byte[] { 0x00, 0x00 };
                        }
                        return null;
                        
                    case DataTypeEnums.Byte:
                        if (value.Item2 is byte byteValue)
                        {
                            return new byte[] { byteValue };
                        }
                        return null;
                        
                    case DataTypeEnums.Int16:
                        if (value.Item2 is short shortValue)
                        {
                            return shortValue.ToByte(isReverse);
                        }
                        return null;
                        
                    case DataTypeEnums.UInt16:
                        if (value.Item2 is ushort ushortValue)
                        {
                            return ushortValue.ToByte(isReverse);
                        }
                        return null;
                        
                    case DataTypeEnums.Int32:
                        if (value.Item2 is int intValue)
                        {
                            return intValue.ToByte(dataFormat);
                        }
                        return null;
                        
                    case DataTypeEnums.UInt32:
                        if (value.Item2 is uint uintValue)
                        {
                            return uintValue.ToByte(dataFormat);
                        }
                        return null;
                        
                    case DataTypeEnums.Int64:
                        if (value.Item2 is long longValue)
                        {
                            return longValue.ToByte(dataFormat);
                        }
                        return null;
                        
                    case DataTypeEnums.UInt64:
                        if (value.Item2 is ulong ulongValue)
                        {
                            return ulongValue.ToByte(dataFormat);
                        }
                        return null;
                        
                    case DataTypeEnums.Float:
                        if (value.Item2 is float floatValue)
                        {
                            return floatValue.ToByte(dataFormat);
                        }
                        return null;
                        
                    case DataTypeEnums.Double:
                        if (value.Item2 is double doubleValue)
                        {
                            return doubleValue.ToByte(dataFormat);
                        }
                        return null;
                        
                    default:
                        // 对于不支持的类型，尝试根据功能码进行传统转换（向后兼容）
                        switch (addressInfo.FunctionCode)
                        {
                            case 0x05:
                                if (value.Item2 is bool boolVal)
                                {
                                    return boolVal ? new byte[] { 0xFF, 0x00 } : new byte[] { 0x00, 0x00 };
                                }
                                return null;
                            case 0x06:
                            case 0x10:
                                if (value.Item2 is short shortVal)
                                {
                                    return shortVal.ToByte(isReverse);
                                }
                                else if (value.Item2 is ushort ushortVal)
                                {
                                    return ushortVal.ToByte(isReverse);
                                }
                                return null;
                            case 0x0F:
                                if (value.Item2 is bool[] boolArray)
                                {
                                    var byteCount = (boolArray.Length + 7) / 8;
                                    var bytes = new byte[byteCount];
                                    for (int i = 0; i < boolArray.Length; i++)
                                    {
                                        if (boolArray[i])
                                        {
                                            bytes[i / 8] |= (byte)(1 << (i % 8));
                                        }
                                    }
                                    return bytes;
                                }
                                return null;
                            default:
                                return null;
                        }
                }
            }
            catch
            {
                return null;
            }
        }

        public static string ConstructModbusWriteAddress(ModbusAddressInfo addressInfo)
        {
            return $"{addressInfo.StationNumber};{addressInfo.FunctionCode};{addressInfo.Address}";
        }
    }
} 
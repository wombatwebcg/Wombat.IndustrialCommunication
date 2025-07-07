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
            var optimizedBlocks = new List<ModbusAddressBlock>();
            var currentBlock = new ModbusAddressBlock
            {
                StationNumber = addresses[0].StationNumber,
                FunctionCode = addresses[0].FunctionCode,
                Addresses = new List<ModbusAddressInfo>()
            };
            foreach (var address in addresses)
            {
                if (currentBlock.Addresses.Count == 0)
                {
                    currentBlock.StartAddress = (ushort)address.Address;
                    currentBlock.TotalLength = (ushort)address.Length;
                    currentBlock.Addresses.Add(address);
                    continue;
                }
                var newStartAddress = Math.Min(currentBlock.StartAddress, (ushort)address.Address);
                
                // 根据功能码计算当前块的结束地址
                ushort currentEndAddress;
                if (address.FunctionCode == 0x03 || address.FunctionCode == 0x04)
                {
                    // 寄存器类型：TotalLength 是字节数，需要转换为寄存器数量
                    var registerCount = (ushort)(currentBlock.TotalLength / 2);
                    currentEndAddress = (ushort)(currentBlock.StartAddress + registerCount);
                }
                else
                {
                    // 线圈类型：TotalLength 是字节数
                    currentEndAddress = (ushort)(currentBlock.StartAddress + currentBlock.TotalLength);
                }
                
                // 根据功能码计算地址结束位置
                ushort addressEndAddress;
                if (address.FunctionCode == 0x03 || address.FunctionCode == 0x04)
                {
                    // 寄存器类型：address.Length 是字节数，需要转换为寄存器数量
                    var registerCount = (ushort)(address.Length / 2);
                    addressEndAddress = (ushort)((ushort)address.Address + registerCount);
                }
                else
                {
                    // 线圈类型：address.Length 是字节数
                    addressEndAddress = (ushort)((ushort)address.Address + address.Length);
                }
                
                var newEndAddress = Math.Max(currentEndAddress, addressEndAddress);
                
                // 根据功能码计算总长度
                ushort newTotalLength;
                if (address.FunctionCode == 0x03 || address.FunctionCode == 0x04)
                {
                    // 寄存器类型：按寄存器数量计算，每个寄存器2字节
                    var registerCount = (ushort)(newEndAddress - newStartAddress);
                    newTotalLength = (ushort)(registerCount * 2);
                }
                else
                {
                    // 线圈类型：按字节数计算
                    newTotalLength = (ushort)(newEndAddress - newStartAddress);
                }
                if (newTotalLength > maxBlockSize)
                {
                    currentBlock.EfficiencyRatio = CalculateModbusEfficiencyRatio(currentBlock);
                    optimizedBlocks.Add(currentBlock);
                    currentBlock = new ModbusAddressBlock
                    {
                        StationNumber = address.StationNumber,
                        FunctionCode = address.FunctionCode,
                        StartAddress = (ushort)address.Address,
                        TotalLength = (ushort)address.Length,
                        Addresses = new List<ModbusAddressInfo> { address }
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
                if (newEfficiencyRatio >= minEfficiencyRatio)
                {
                    currentBlock.StartAddress = newStartAddress;
                    currentBlock.TotalLength = newTotalLength;
                    currentBlock.Addresses.Add(address);
                }
                else
                {
                    currentBlock.EfficiencyRatio = CalculateModbusEfficiencyRatio(currentBlock);
                    optimizedBlocks.Add(currentBlock);
                    currentBlock = new ModbusAddressBlock
                    {
                        StationNumber = address.StationNumber,
                        FunctionCode = address.FunctionCode,
                        StartAddress = (ushort)address.Address,
                        TotalLength = (ushort)address.Length,
                        Addresses = new List<ModbusAddressInfo> { address }
                    };
                }
            }
            if (currentBlock.Addresses.Count > 0)
            {
                currentBlock.EfficiencyRatio = CalculateModbusEfficiencyRatio(currentBlock);
                optimizedBlocks.Add(currentBlock);
            }
            return optimizedBlocks;
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
                foreach (var address in block.Addresses)
                {
                    try
                    {
                        int relativeOffset;
                        if (address.FunctionCode == 0x03 || address.FunctionCode == 0x04)
                        {
                            // 寄存器类型：地址差值转换为字节偏移量
                            var addressOffset = (ushort)address.Address - block.StartAddress;
                            relativeOffset = addressOffset * 2;
                        }
                        else
                        {
                            // 线圈类型：直接使用地址差值作为字节偏移量
                            relativeOffset = (ushort)address.Address - block.StartAddress;
                        }
                        
                        if (relativeOffset < 0 || relativeOffset + address.Length > data.Length)
                        {
                            result[address.OriginalAddress] = null;
                            continue;
                        }
                        object value = ExtractValueFromModbusBytes(data, relativeOffset, address);
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

        public static object ExtractValueFromModbusBytes(byte[] data, int offset, ModbusAddressInfo addressInfo, bool isReverse = true, EndianFormat dataFormat = EndianFormat.ABCD)
        {
            try
            {
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
                                    return data.ToUInt16(offset, isReverse  );
                                }
                                return (ushort)0;
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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Modbus地址格式枚举
    /// </summary>
    public enum ModbusAddressFormat
    {
        /// <summary>
        /// 标准格式：站号;功能码;地址
        /// </summary>
        Standard,
        
        /// <summary>
        /// 增强格式：站号;地址（自动判断功能码）
        /// </summary>
        Enhanced
    }

    public static class ModbusAddressParser
    {
        /// <summary>
        /// 检测地址格式
        /// </summary>
        /// <param name="header">地址字符串</param>
        /// <returns>地址格式</returns>
        private static ModbusAddressFormat DetectAddressFormat(string header)
        {
            if (string.IsNullOrEmpty(header))
                throw new ArgumentException("地址字符串不能为空");
            
            var parts = header.Split(';');
            switch (parts.Length)
            {
                case 3:
                    return ModbusAddressFormat.Standard;
                case 2:
                    return ModbusAddressFormat.Enhanced;
                default:
                    throw new ArgumentException($"无效的地址格式: {header}");
            }
        }

        /// <summary>
        /// 解析逻辑地址格式（如40001、30001等）
        /// </summary>
        /// <param name="addressStr">地址字符串</param>
        /// <param name="registerType">寄存器类型</param>
        /// <param name="actualAddress">实际地址</param>
        /// <returns>是否解析成功</returns>
        private static bool TryParseLogicalAddress(string addressStr, out byte registerType, out ushort actualAddress)
        {
            registerType = 0;
            actualAddress = 0;

            if (string.IsNullOrEmpty(addressStr) || addressStr.Length < 2)
                return false;

            // 检查是否以寄存器类型前缀开头
            char firstChar = addressStr[0];
            if (firstChar != '1' && firstChar != '2' && firstChar != '3' && firstChar != '4')
                return false;

            // 提取寄存器类型
            switch (firstChar)
            {
                case '1':
                    registerType = 0x01;  // 线圈
                    break;
                case '2':
                    registerType = 0x02;  // 离散输入
                    break;
                case '3':
                    registerType = 0x04;  // 输入寄存器
                    break;
                case '4':
                    registerType = 0x03;  // 保持寄存器
                    break;
                default:
                    return false;
            }

            // 提取实际地址部分
            string addressPart = addressStr.Substring(1);
            if (!ushort.TryParse(addressPart, out ushort logicalAddress))
                return false;

            // 验证地址范围（1-65535）
            if (logicalAddress < 1 || logicalAddress > 65535)
                return false;

            // 转换为实际地址（Modbus协议中，40001对应实际地址0）
            actualAddress = (ushort)(logicalAddress - 1);

            return true;
        }

        /// <summary>
        /// 根据寄存器类型和操作类型获取功能码
        /// </summary>
        /// <param name="registerType">寄存器类型</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isWrite">是否为写操作</param>
        /// <returns>功能码</returns>
        private static byte GetFunctionCodeByRegisterType(byte registerType, DataTypeEnums dataType, bool isWrite)
        {
            if (isWrite)
            {
                switch (registerType)
                {
                    case 0x01:  // 线圈
                        return 0x05;  // 写单个线圈
                    case 0x02:  // 离散输入 - 不支持写操作
                        throw new ArgumentException("离散输入寄存器不支持写操作");
                    case 0x03:  // 保持寄存器
                        switch (dataType)
                        {
                            case DataTypeEnums.Int16:
                            case DataTypeEnums.UInt16:
                                return 0x06;  // 写单个寄存器
                            case DataTypeEnums.Int32:
                            case DataTypeEnums.UInt32:
                            case DataTypeEnums.Float:
                            case DataTypeEnums.Int64:
                            case DataTypeEnums.UInt64:
                            case DataTypeEnums.Double:
                                return 0x10;  // 写多个寄存器
                            default:
                                return 0x06;  // 默认写单个寄存器
                        }
                    case 0x04:  // 输入寄存器 - 不支持写操作
                        throw new ArgumentException("输入寄存器不支持写操作");
                    default:
                        return 0x06;  // 默认写单个寄存器
                }
            }
            else
            {
                return registerType;  // 读操作直接使用寄存器类型作为功能码
            }
        }

        /// <summary>
        /// 根据地址获取寄存器类型
        /// </summary>
        /// <param name="address">地址字符串</param>
        /// <returns>功能码</returns>
        private static byte GetRegisterTypeFromAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("地址不能为空");
            
            char firstChar = address[0];
            switch (firstChar)
            {
                case '4':
                    return 0x03;  // 保持寄存器
                case '3':
                    return 0x04;  // 输入寄存器
                case '1':
                    return 0x01;  // 线圈
                case '2':
                    return 0x02;  // 离散输入
                default:
                    throw new ArgumentException($"不支持的寄存器类型: {firstChar}");
            }
        }

        /// <summary>
        /// 根据数据类型和操作类型自动判断功能码
        /// </summary>
        /// <param name="dataType">数据类型</param>
        /// <param name="isWrite">是否为写操作</param>
        /// <param name="registerType">寄存器类型（可选，用于逻辑地址格式）</param>
        /// <returns>功能码</returns>
        private static byte GetAutoFunctionCode(DataTypeEnums dataType, bool isWrite, byte registerType = 0x03)
        {
            if (isWrite)
            {
                switch (registerType)
                {
                    case 0x01:  // 线圈
                        return 0x05;  // 写单个线圈
                    case 0x02:  // 离散输入 - 不支持写操作
                        throw new ArgumentException("离散输入寄存器不支持写操作");
                    case 0x03:  // 保持寄存器
                        switch (dataType)
                        {
                            case DataTypeEnums.Int16:
                            case DataTypeEnums.UInt16:
                                return 0x06;  // 写单个寄存器
                            case DataTypeEnums.Int32:
                            case DataTypeEnums.UInt32:
                            case DataTypeEnums.Float:
                            case DataTypeEnums.Int64:
                            case DataTypeEnums.UInt64:
                            case DataTypeEnums.Double:
                                return 0x10;  // 写多个寄存器
                            default:
                                return 0x06;  // 默认写单个寄存器
                        }
                    case 0x04:  // 输入寄存器 - 不支持写操作
                        throw new ArgumentException("输入寄存器不支持写操作");
                    default:
                        return 0x06;  // 默认写单个寄存器
                }
            }
            else
            {
                switch (registerType)
                {
                    case 0x01:  // 线圈
                        return 0x01;  // 读线圈
                    case 0x02:  // 离散输入
                        return 0x02;  // 读离散输入
                    case 0x03:  // 保持寄存器
                        return 0x03;  // 读保持寄存器
                    case 0x04:  // 输入寄存器
                        return 0x04;  // 读输入寄存器
                    default:
                        // 如果没有指定寄存器类型，根据数据类型默认判断
                        switch (dataType)
                        {
                            case DataTypeEnums.Bool:
                                return 0x01;  // 读线圈
                            default:
                                return 0x03;  // 默认读保持寄存器
                        }
                }
            }
        }

        /// <summary>
        /// 尝试解析增强的Modbus地址（支持新格式）
        /// </summary>
        /// <param name="header">地址字符串</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isWrite">是否为写操作</param>
        /// <param name="modbusHeader">解析结果</param>
        /// <returns>是否解析成功</returns>
        public static bool TryParseEnhancedModbusAddress(string header, DataTypeEnums dataType, bool isWrite, out ModbusHeader modbusHeader)
        {
            modbusHeader = new ModbusHeader();
            
            try
            {
                var format = DetectAddressFormat(header);
                var parts = header.Split(';');
                
                // 解析站号
                if (!TryParseNumber(parts[0], out byte stationNumber))
                {
                    return false;
                }
                modbusHeader.StationNumber = stationNumber;
                
                if (format == ModbusAddressFormat.Standard)
                {
                    // 原有格式：站号;功能码;地址
                    if (!TryParseNumber(parts[1], out byte functionCode))
                    {
                        return false;
                    }
                    modbusHeader.FunctionCode = functionCode;
                    
                    // 尝试解析逻辑地址格式
                    if (TryParseLogicalAddress(parts[2], out byte registerType, out ushort actualAddress))
                    {
                        modbusHeader.Address = actualAddress;
                    }
                    else
                    {
                        // 尝试解析普通数字地址
                        if (!ushort.TryParse(parts[2], out ushort address))
                        {
                            return false;
                        }
                        modbusHeader.Address = address;
                    }
                }
                else
                {
                    // 新格式：站号;地址
                    // 尝试解析逻辑地址格式
                    if (TryParseLogicalAddress(parts[1], out byte registerType, out ushort actualAddress))
                    {
                        modbusHeader.Address = actualAddress;
                        modbusHeader.FunctionCode = GetFunctionCodeByRegisterType(registerType, dataType, isWrite);
                    }
                    else
                    {
                        // 尝试解析普通数字地址
                        if (!ushort.TryParse(parts[1], out ushort address))
                        {
                            return false;
                        }
                        modbusHeader.Address = address;
                        // 自动判断功能码（默认使用保持寄存器类型）
                        modbusHeader.FunctionCode = GetAutoFunctionCode(dataType, isWrite, 0x03);
                    }
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 解析增强的Modbus地址（支持新格式）
        /// </summary>
        /// <param name="header">地址字符串</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isWrite">是否为写操作</param>
        /// <returns>解析结果</returns>
        public static ModbusHeader ParseEnhancedModbusAddress(string header, DataTypeEnums dataType, bool isWrite)
        {
            if (TryParseEnhancedModbusAddress(header, dataType, isWrite, out ModbusHeader modbusHeader))
            {
                return modbusHeader;
            }
            return null;
        }

        public static bool TryParseModbusAddress(string header, out ModbusHeader modbusHeader)
        {
            modbusHeader = new ModbusHeader();

            // 移除空格并按分号分割
            header = header.ToLower().Replace(" ", "").Trim();
            var parts = header.Split(';');
            bool isIsucess = true;
            if (parts.Length == 3)
            {
                // 检查是否以 s: 开头
                if (parts[0].StartsWith("s:") && TryParseNumber(parts[0].Substring(2), out byte s1))
                {
                    modbusHeader.StationNumber = s1;
                }
                else
                {
                    if (TryParseNumber(parts[0], out byte s2))
                    {
                        modbusHeader.StationNumber = s2;
                    }
                    else
                    {
                        isIsucess &= false;

                    }
                }
                // 检查是否以 f: 开头
                if (parts[1].StartsWith("f:") && TryParseNumber(parts[1].Substring(2), out byte f1))
                {
                    modbusHeader.FunctionCode = f1;
                }
                else
                {
                    if (TryParseNumber(parts[1], out byte f2))
                    {
                        modbusHeader.FunctionCode = f2;
                    }
                    else
                    {
                        isIsucess &= false;

                    }

                }
                // 检查是否以 a: 开头
                if (parts[2].StartsWith("a:"))
                {
                    string addressStr = parts[2].Substring(2);
                    // 尝试解析逻辑地址格式
                    if (TryParseLogicalAddress(addressStr, out byte registerType, out ushort actualAddress))
                {
                        modbusHeader.Address = actualAddress;
                    }
                    else
                    {
                        // 尝试解析普通数字地址
                        if (!ushort.TryParse(addressStr, out ushort address))
                        {
                            isIsucess &= false;
                        }
                        else
                        {
                            modbusHeader.Address = address;
                        }
                    }
                }
                else
                {
                    // 尝试解析逻辑地址格式
                    if (TryParseLogicalAddress(parts[2], out byte registerType, out ushort actualAddress))
                    {
                        modbusHeader.Address = actualAddress;
                    }
                    else
                    {
                        // 尝试解析普通数字地址
                        if (!ushort.TryParse(parts[2], out ushort address))
                    {
                        isIsucess &= false;
                        }
                        else
                        {
                            modbusHeader.Address = address;
                    }
                    }
                }

            }
            else
            {
                isIsucess &= false;
            }
            return isIsucess;
        }

        public static ModbusHeader ParseModbusAddress(string header)
        {
            if (TryParseModbusAddress(header, out ModbusHeader modbusHeader))
            {
                return modbusHeader;
            }
            return null;
        }

        public static bool TryParseModbusAddress(ModbusHeader modbusHeader, out string header)
        {
            header = string.Empty;

            if (modbusHeader != null)
            {
                header = $"{modbusHeader.StationNumber};{modbusHeader.FunctionCode};{modbusHeader.Address}";
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string ParseModbusAddress(ModbusHeader modbusHeader)
        {
            if (TryParseModbusAddress(modbusHeader, out string header))
            {
                return header;
            }
            return string.Empty;
        }

        private static bool TryParseNumber(string input, out byte number)
        {
            bool result = false;
            number = 0;

            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                result = byte.TryParse(input.Substring(2), NumberStyles.HexNumber, null, out number);
            }
            else
            {
                result = byte.TryParse(input, out number);
            }

            return result;
        }
    }
}

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

            // 转换为实际地址（Modbus协议中，40001对应实际地址0，需要偏移-1）
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
        /// 尝试解析增强的Modbus地址（支持新格式）
        /// </summary>
        /// <param name="header">地址字符串</param>
        /// <param name="dataBytes">要写入的数据字节数组</param>
        /// <param name="isWrite">是否为写操作</param>
        /// <param name="modbusHeader">解析结果</param>
        /// <returns>是否解析成功</returns>
        public static bool TryParseModbusAddress(string header, DataTypeEnums dataType, bool isWrite, out ModbusHeader modbusHeader)
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
                        modbusHeader.FunctionCode = GetFunctionCodeByRegisterType(0x03, dataType, isWrite);
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
        public static ModbusHeader ParseModbusAddress(string header, DataTypeEnums dataType, bool isWrite)
        {
            if (TryParseModbusAddress(header, dataType, isWrite, out ModbusHeader modbusHeader))
            {
                return modbusHeader;
            }
            return null;
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

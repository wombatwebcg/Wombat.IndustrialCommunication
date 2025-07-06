using System;
using System.Collections.Generic;
using System.Text;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusHeader
    {

        /// <summary>
        /// 地址
        /// </summary>
        public ushort Address { get; set; }
        /// <summary>
        /// 站号
        /// </summary>
        public byte StationNumber { get; set; }
        /// <summary>
        /// 功能码
        /// </summary>
        public byte FunctionCode { get; set; }
    }

    public class ModbusInput : ModbusHeader
    {
        /// <summary>
        /// 数据类型
        /// </summary>
        public DataTypeEnums DataType { get; set; }

        /// <summary>
        /// 地址格式
        /// </summary>
        public ModbusAddressFormat AddressFormat { get; set; }

        /// <summary>
        /// 是否自动判断功能码
        /// </summary>
        public bool IsAutoFunctionCode { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ModbusInput()
        {
            AddressFormat = ModbusAddressFormat.Standard;
            IsAutoFunctionCode = false;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="address">地址</param>
        /// <param name="dataType">数据类型</param>
        public ModbusInput(byte stationNumber, byte functionCode, ushort address, DataTypeEnums dataType)
        {
            StationNumber = stationNumber;
            FunctionCode = functionCode;
            Address = address;
            DataType = dataType;
            AddressFormat = ModbusAddressFormat.Standard;
            IsAutoFunctionCode = false;
        }

        /// <summary>
        /// 构造函数（增强格式）
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">地址</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isWrite">是否为写操作</param>
        public ModbusInput(byte stationNumber, ushort address, DataTypeEnums dataType, bool isWrite)
        {
            StationNumber = stationNumber;
            Address = address;
            DataType = dataType;
            AddressFormat = ModbusAddressFormat.Enhanced;
            IsAutoFunctionCode = true;
            
            // 自动判断功能码
            FunctionCode = GetAutoFunctionCode(dataType, isWrite);
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
                    case 0x04:  // 输入寄存器
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
    }
}

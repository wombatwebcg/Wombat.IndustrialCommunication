
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
        public string RegisterAddress { get; set; }
        /// <summary>
        /// 站号
        /// </summary>
        public byte StationNumber { get; set; }
        /// <summary>
        /// 功能码
        /// </summary>
        public byte FunctionCode { get; set; }
    }

    public class ModbusInput:ModbusHeader
    {
        /// <summary>
        /// 数据类型
        /// </summary>
        public DataTypeEnum DataType { get; set; }
    }

    public enum ModbusStandardFunctionCode
    {
        ReadCoils = 1,
        ReadDiscreteInputs = 2,
        ReadHoldingRegisters = 3,
        ReadInputRegisters = 4,
        WriteSingleCoil = 5,
        WriteSingleRegister = 6,
        WriteMultipleCoils = 0xF,
        WriteMultipleRegister = 0x10

    }

}

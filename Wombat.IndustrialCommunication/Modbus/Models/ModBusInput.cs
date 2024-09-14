
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
    }


}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public static class ModbusAddressParser
    {
        public static ModbusHeader Parse(string message)
        {
            var modbusMessage = new ModbusHeader();

            // 移除空格并按分号分割
            message = message.ToLower().Replace(" ", "");
            var parts = message.Split(';');
            try
            {
                if (parts.Length == 3)
                {
                    // 检查是否以 s: 开头
                    if (parts[0].StartsWith("s:"))
                    {
                        modbusMessage.StationNumber = (byte)ParseNumber(parts[0].Substring(2));
                    }
                    else
                    {
                        modbusMessage.StationNumber = (byte)ParseNumber(parts[0]);
                    }
                    // 检查是否以 s: 开头
                    if (parts[1].StartsWith("f:"))
                    {
                        modbusMessage.StationNumber = (byte)ParseNumber(parts[1].Substring(2));
                    }
                    else
                    {
                        modbusMessage.FunctionCode = (byte)ParseNumber(parts[1]);
                    }

                    modbusMessage.RegisterAddress = ParseNumber(parts[2]).ToString();
                }
                else 
                {
                    throw new ArgumentException($"modbus地址参数");
                }
            }
            catch(Exception ex)
            {
                throw new ArgumentException($"modbus地址参数有误:{ex.Message}");
            }
            return modbusMessage;
        }

        public static string Parse(ModbusHeader modbusHeader)
        {
            var header = string.Empty;

            try
            {
                if(modbusHeader!=null)
                {
                    header = $"{modbusHeader.StationNumber};{modbusHeader.FunctionCode};{modbusHeader.RegisterAddress}";
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"modbus地址参数有误:{ex.Message}");
            }
            return header;
        }

        private static int ParseNumber(string number)
        {
            if (number.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.Parse(number.Substring(2), NumberStyles.HexNumber);
            }
            return int.Parse(number);
        }
    }

}

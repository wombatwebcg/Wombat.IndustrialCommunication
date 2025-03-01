using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public static class ModbusAddressParser
    {
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
                // 检查是否以 s: 开头
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
                if (parts[2].StartsWith("a:") && TryParseNumber(parts[1].Substring(2), out ushort a1))
                {
                    modbusHeader.Address = a1;
                }
                else
                {
                    if (TryParseNumber(parts[2], out ushort a2))
                    {
                        modbusHeader.Address = a2;
                    }
                    else
                    {
                        isIsucess &= false;

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
        private static bool TryParseNumber(string input, out ushort number)
        {
            bool result = false;
            number = 0;

            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                result = ushort.TryParse(input.Substring(2), NumberStyles.HexNumber, null, out number);
            }
            else
            {
                result = ushort.TryParse(input, out number);
            }

            return result;
        }

    }

}

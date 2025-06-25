using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    public static class S7CommonMethods
    {
        private static readonly Dictionary<char, byte> TypeCodeMap = new Dictionary<char, byte>
        {
            ['I'] = 0x81,
            ['Q'] = 0x82,
            ['M'] = 0x83,
            ['D'] = 0x84,
            ['T'] = 0x1D,
            ['C'] = 0x1C,
            ['V'] = 0x84
        };

        private static readonly Dictionary<DataTypeEnums, (int Length, bool IsBit)> DataTypeInfo = new Dictionary<DataTypeEnums, (int Length, bool IsBit)>
        {
            [DataTypeEnums.Bool] = (1, true),
            [DataTypeEnums.Byte] = (1, false),
            [DataTypeEnums.Int16] = (2, false),
            [DataTypeEnums.UInt16] = (2, false),
            [DataTypeEnums.Int32] = (4, false),
            [DataTypeEnums.UInt32] = (4, false),
            [DataTypeEnums.Int64] = (8, false),
            [DataTypeEnums.UInt64] = (8, false),
            [DataTypeEnums.Float] = (4, false),
            [DataTypeEnums.Double] = (8, false)
        };

        /// <summary>
        /// 获取需要读取的长度
        /// </summary>
        /// <param name="head"></param>
        /// <returns></returns>
        public static int GetContentLength(byte[] head)
        {
            if (head?.Length < 4)
                throw new ArgumentException("请传入正确的参数");
            
            return head[2] * 256 + head[3] - 4;
        }

        /// <summary>
        /// 获取读取PLC地址的开始位置
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static int GetBeingAddress(string address, int offest)
        {
            int dotIndex = address.IndexOf('.');
            if (dotIndex < 0)
                return (int.Parse(address) + offest) * 8;

            return (Convert.ToInt32(address.Substring(0, dotIndex)) + offest) * 8 + 
                   Convert.ToInt32(address.Substring(dotIndex + 1));
        }

        /// <summary>
        /// 获取区域类型代码
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static SiemensAddress ConvertArg(string address, int offest = 0)
        {
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("地址不能为空");

            address = address.ToUpperInvariant();
            var addressInfo = new SiemensAddress
            {
                Address = address,
                DbBlock = 0
            };

            char firstChar = address[0];
            if (!TypeCodeMap.TryGetValue(firstChar, out byte typeCode))
                throw new ArgumentException($"不支持的地址类型: {firstChar}");

            addressInfo.TypeCode = typeCode;

            if (firstChar == 'D' && address.Length > 1 && address[1] == 'B')
            {
                int dotIndex = address.IndexOf('.');
                if (dotIndex > 0)
                {
                    addressInfo.DbBlock = Convert.ToUInt16(address.Substring(2, dotIndex - 2));
                    int nextDotIndex = address.IndexOf('.', dotIndex + 1);
                    
                    if (nextDotIndex > 0 && address[dotIndex + 1] >= '0' && address[dotIndex + 1] <= '9')
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(dotIndex + 1), offest);
                    else
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(dotIndex + 4), offest);
                }
            }
            else if (firstChar == 'V')
            {
                addressInfo.DbBlock = 1;
                
                // V地址格式解析：V1.0（位）、V1（字节）、VW1（字）、VD1（双字）
                if (address.Length >= 3 && address[1] == 'W')
                {
                    // VW格式：字地址
                    addressInfo.DataType = DataTypeEnums.Int16;
                    addressInfo.ReadWriteLength = 2;
                    addressInfo.IsBit = false;
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(2), offest);
                }
                else if (address.Length >= 3 && address[1] == 'D')
                {
                    // VD格式：双字地址
                    addressInfo.DataType = DataTypeEnums.Int32;
                    addressInfo.ReadWriteLength = 4;
                    addressInfo.IsBit = false;
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(2), offest);
                }
                else if (address.Contains('.'))
                {
                    // V1.0格式：位地址
                    addressInfo.DataType = DataTypeEnums.Bool;
                    addressInfo.ReadWriteLength = 1;
                    addressInfo.IsBit = true;
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                }
                else
                {
                    // V1格式：字节地址
                    addressInfo.DataType = DataTypeEnums.Byte;
                    addressInfo.ReadWriteLength = 1;
                    addressInfo.IsBit = false;
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                }
            }
            else if (firstChar == 'Q' || firstChar == 'I' || firstChar == 'M')
            {
                // Q区、I区、M区地址格式解析
                if (address.Contains('.'))
                {
                    // Q2.0、I1.3、M5.7格式：位地址
                    addressInfo.DataType = DataTypeEnums.Bool;
                    addressInfo.ReadWriteLength = 1;
                    addressInfo.IsBit = true;
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                }
                else
                {
                    // Q2、I1、M5格式：字节地址
                    addressInfo.DataType = DataTypeEnums.Byte;
                    addressInfo.ReadWriteLength = 1;
                    addressInfo.IsBit = false;
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                }
            }
            else
            {
                if (address.Length > 1 && address[1] >= '0' && address[1] <= '9')
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                else
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(2), offest);
            }

            return addressInfo;
        }

        public static SiemensAddress[] ConvertArg(Dictionary<string, DataTypeEnums> addresses, int offest = 0)
        {
            if (addresses == null || addresses.Count == 0)
                return Array.Empty<SiemensAddress>();

            return addresses.Select(t =>
            {
                var item = ConvertArg(t.Key, offest);
                item.DataType = t.Value;

                if (DataTypeInfo.TryGetValue(t.Value, out var info))
                {
                    item.ReadWriteLength = info.Length;
                    item.IsBit = info.IsBit;
                }
                else
                {
                    throw new ArgumentException($"未定义数据类型：{t.Value}");
                }

                return item;
            }).ToArray();
        }

        public static SiemensWriteAddress ConvertWriteArg(string address, int offest, byte[] writeData, bool bit)
        {
            return new SiemensWriteAddress(ConvertArg(address, offest))
            {
                WriteData = writeData,
                IsBit = bit
            };
        }
    }
}

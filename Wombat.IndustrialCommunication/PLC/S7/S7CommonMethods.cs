using System;
using System.Collections.Generic;
using System.Linq;
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

        public static int GetContentLength(byte[] head)
        {
            if (head?.Length < 4)
            {
                throw new ArgumentException("请传入正确的参数");
            }

            return head[2] * 256 + head[3] - 4;
        }

        public static int GetBeingAddress(string address, int offest)
        {
            int dotIndex = address.IndexOf('.');
            if (dotIndex < 0)
            {
                return (int.Parse(address) + offest) * 8;
            }

            return (Convert.ToInt32(address.Substring(0, dotIndex)) + offest) * 8 +
                   Convert.ToInt32(address.Substring(dotIndex + 1));
        }

        private static void ApplyTypedAddressMetadata(SiemensAddress addressInfo, string typeSegment)
        {
            if (addressInfo == null || string.IsNullOrEmpty(typeSegment))
            {
                return;
            }

            switch (typeSegment)
            {
                case "DBX":
                    addressInfo.DataType = DataTypeEnums.Bool;
                    addressInfo.ReadWriteLength = 1;
                    addressInfo.IsBit = true;
                    break;
                case "DBB":
                case "B":
                case "MB":
                case "IB":
                case "QB":
                case "VB":
                    addressInfo.DataType = DataTypeEnums.Byte;
                    addressInfo.ReadWriteLength = 1;
                    addressInfo.IsBit = false;
                    break;
                case "DBW":
                case "W":
                case "MW":
                case "IW":
                case "QW":
                case "VW":
                    addressInfo.DataType = DataTypeEnums.Int16;
                    addressInfo.ReadWriteLength = 2;
                    addressInfo.IsBit = false;
                    break;
                case "DBD":
                case "D":
                case "MD":
                case "ID":
                case "QD":
                case "VD":
                    addressInfo.DataType = DataTypeEnums.Int32;
                    addressInfo.ReadWriteLength = 4;
                    addressInfo.IsBit = false;
                    break;
            }
        }

        public static SiemensAddress ConvertArg(string address, int offest = 0)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException("地址不能为空");
            }

            address = address.ToUpperInvariant();
            var addressInfo = new SiemensAddress
            {
                Address = address,
                OriginalAddress = address,
                DbBlock = 0
            };

            char firstChar = address[0];
            if (!TypeCodeMap.TryGetValue(firstChar, out byte typeCode))
            {
                throw new ArgumentException($"不支持的地址类型: {firstChar}");
            }

            addressInfo.TypeCode = typeCode;

            if (firstChar == 'D' && address.Length > 1 && address[1] == 'B')
            {
                int dotIndex = address.IndexOf('.');
                if (dotIndex > 0)
                {
                    addressInfo.DbBlock = Convert.ToUInt16(address.Substring(2, dotIndex - 2));
                    int nextDotIndex = address.IndexOf('.', dotIndex + 1);
                    string dbTypeSegment = nextDotIndex > 0 && address[dotIndex + 1] >= '0' && address[dotIndex + 1] <= '9'
                        ? null
                        : address.Substring(dotIndex + 1, Math.Min(3, address.Length - (dotIndex + 1)));
                    ApplyTypedAddressMetadata(addressInfo, dbTypeSegment);

                    if (nextDotIndex > 0 && address[dotIndex + 1] >= '0' && address[dotIndex + 1] <= '9')
                    {
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(dotIndex + 1), offest);
                    }
                    else
                    {
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(dotIndex + 4), offest);
                    }
                }
            }
            else if (firstChar == 'V')
            {
                addressInfo.DbBlock = 1;
                if (address.Length >= 3 && (address[1] == 'B' || address[1] == 'M'))
                {
                    ApplyTypedAddressMetadata(addressInfo, "VB");
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(2), offest);
                }
                else if (address.Length >= 3 && address[1] == 'W')
                {
                    ApplyTypedAddressMetadata(addressInfo, "VW");
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(2), offest);
                }
                else if (address.Length >= 3 && address[1] == 'D')
                {
                    ApplyTypedAddressMetadata(addressInfo, "VD");
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(2), offest);
                }
                else if (address.Contains('.'))
                {
                    addressInfo.DataType = DataTypeEnums.Bool;
                    addressInfo.ReadWriteLength = 1;
                    addressInfo.IsBit = true;
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                }
                else
                {
                    addressInfo.DataType = DataTypeEnums.Byte;
                    addressInfo.ReadWriteLength = 1;
                    addressInfo.IsBit = false;
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                }
            }
            else if (firstChar == 'Q' || firstChar == 'I' || firstChar == 'M')
            {
                if (address.Contains('.') && address.Length > 1 && address[1] >= '0' && address[1] <= '9')
                {
                    addressInfo.DataType = DataTypeEnums.Bool;
                    addressInfo.ReadWriteLength = 1;
                    addressInfo.IsBit = true;
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                }
                else if (address.Length >= 3 && (address[1] == 'B' || address[1] == 'W' || address[1] == 'D'))
                {
                    ApplyTypedAddressMetadata(addressInfo, address.Substring(1, 2));
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(2), offest);
                }
                else
                {
                    addressInfo.DataType = DataTypeEnums.Byte;
                    addressInfo.ReadWriteLength = 1;
                    addressInfo.IsBit = false;
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                }
            }
            else
            {
                if (address.Length > 1 && address[1] >= '0' && address[1] <= '9')
                {
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                }
                else
                {
                    addressInfo.BeginAddress = GetBeingAddress(address.Substring(2), offest);
                }
            }

            return addressInfo;
        }

        public static SiemensAddress[] ConvertArg(Dictionary<string, DataTypeEnums> addresses, int offest = 0)
        {
            if (addresses == null || addresses.Count == 0)
            {
                return Array.Empty<SiemensAddress>();
            }

            return addresses.Select(t =>
            {
                var item = ConvertArg(t.Key, offest);
                item.DataType = t.Value;

                if (!DataTypeInfo.TryGetValue(t.Value, out var info))
                {
                    throw new ArgumentException($"未定义数据类型：{t.Value}");
                }

                item.ReadWriteLength = info.Length;
                item.IsBit = info.IsBit;
                item.RequestedLength = info.Length;
                item.Length = info.Length;
                return item;
            }).ToArray();
        }

        public static SiemensWriteAddress ConvertWriteArg(string address, int offest, byte[] writeData, bool bit)
        {
            return new SiemensWriteAddress(ConvertArg(address, offest))
            {
                WriteData = writeData,
                IsBit = bit,
                Length = writeData?.Length ?? 0,
                RequestedLength = writeData?.Length ?? 0
            };
        }

        public static SiemensAddress BuildReadAddress(
            string originalAddress,
            DataTypeEnums dataType,
            int byteOffset,
            int bitOffset,
            int length,
            int originalIndex,
            int offest = 0)
        {
            var item = ConvertArg(originalAddress, offest);
            item.OriginalAddress = originalAddress;
            item.DataType = dataType;
            item.ByteOffset = byteOffset;
            item.BitOffset = bitOffset;
            item.Length = length;
            item.RequestedLength = item.ReadWriteLength > 0 ? item.ReadWriteLength : length;
            item.OriginalIndex = originalIndex;
            return item;
        }

        public static SiemensAddress BuildWriteAddress(
            string originalAddress,
            DataTypeEnums dataType,
            int byteOffset,
            int bitOffset,
            byte[] writeData,
            int originalIndex,
            int offest = 0)
        {
            var item = ConvertArg(originalAddress, offest);
            item.OriginalAddress = originalAddress;
            item.DataType = dataType;
            item.ByteOffset = byteOffset;
            item.BitOffset = bitOffset;
            item.WriteData = writeData;
            item.Length = writeData?.Length ?? 0;
            item.RequestedLength = item.Length;
            item.OriginalIndex = originalIndex;
            item.IsBit = item.IsBit && writeData != null && writeData.Length == 1 && writeData[0] < 2;
            return item;
        }
    }
}

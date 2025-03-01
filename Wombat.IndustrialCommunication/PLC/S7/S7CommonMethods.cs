using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
   public static class S7CommonMethods
    {
        /// <summary>
        /// 获取需要读取的长度
        /// </summary>
        /// <param name="head"></param>
        /// <returns></returns>
        public static int GetContentLength(byte[] head)
        {
            if (head?.Length >= 4)
                return head[2] * 256 + head[3] - 4;
            else
                throw new ArgumentException("请传入正确的参数");
        }

        /// <summary>
        /// 获取读取PLC地址的开始位置
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static int GetBeingAddress(string address, int offest)
        {
            //去掉V1025 前面的V
            //address = address.Substring(1);
            //I1.3地址的情况
            if (address.IndexOf('.') < 0)
                return (int.Parse(address) + offest) * 8;
            else
            {
                string[] temp = address.Split('.');
                return (Convert.ToInt32(temp[0]) + offest) * 8 + Convert.ToInt32(temp[1]);
            }
        }

        /// <summary>
        /// 获取区域类型代码
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static SiemensAddress ConvertArg(string address, int offest = 0)
        {
            try
            {
                //转换成大写
                address = address.ToUpper();
                var addressInfo = new SiemensAddress()
                {
                    Address = address,
                    DbBlock = 0,
                };
                switch (address[0])
                {
                    case 'I':
                        addressInfo.TypeCode = 0x81;
                        break;
                    case 'Q':
                        addressInfo.TypeCode = 0x82;
                        break;
                    case 'M':
                        addressInfo.TypeCode = 0x83;
                        break;
                    case 'D':
                        addressInfo.TypeCode = 0x84;
                        string[] adds = address.Split('.');
                        if (address[1] == 'B')
                            addressInfo.DbBlock = Convert.ToUInt16(adds[0].Substring(2));
                        else
                            addressInfo.DbBlock = Convert.ToUInt16(adds[0].Substring(1));
                        //TODO 
                        //addressInfo.BeginAddress = GetBeingAddress(address.Substring(address.IndexOf('.') + 1));
                        break;
                    case 'T':
                        addressInfo.TypeCode = 0x1D;
                        break;
                    case 'C':
                        addressInfo.TypeCode = 0x1C;
                        break;
                    case 'V':
                        addressInfo.TypeCode = 0x84;
                        addressInfo.DbBlock = 1;
                        break;
                }

                //if (address[0] != 'D' && address[1] != 'B')
                //    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1));

                //DB块
                if (address[0] == 'D' && address[1] == 'B')
                {
                    //DB1.0.0、DB1.4（非PLC地址）
                    var indexOfpoint = address.IndexOf('.') + 1;
                    if (address[indexOfpoint] >= '0' && address[indexOfpoint] <= '9')
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(indexOfpoint), offest);
                    //DB1.DBX0.0、DB1.DBD4（标准PLC地址）
                    else
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(address.IndexOf('.') + 4), offest);
                }
                //非DB块
                else
                {
                    //I0.0、V1004的情况（非PLC地址）
                    if (address[1] >= '0' && address[1] <= '9')
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(1), offest);
                    //VB1004的情况（标准PLC地址）
                    else
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(2), offest);
                }
                return addressInfo;
            }
            catch (Exception ex)
            {
                throw new Exception($"地址[{address}]解析异常，ConvertArg Message:{ex.Message}");
            }
        }


        public static SiemensAddress[] ConvertArg(Dictionary<string, DataTypeEnums> addresses, int offest = 0)
        {
            return addresses.Select(t =>
            {
                var item = ConvertArg(t.Key, offest);
                item.DataType = t.Value;
                switch (t.Value)
                {
                    case DataTypeEnums.Bool:
                        item.ReadWriteLength = 1;
                        item.IsBit = true;
                        break;
                    case DataTypeEnums.Byte:
                        item.ReadWriteLength = 1;
                        break;
                    case DataTypeEnums.Int16:
                        item.ReadWriteLength = 2;
                        break;
                    case DataTypeEnums.UInt16:
                        item.ReadWriteLength = 2;
                        break;
                    case DataTypeEnums.Int32:
                        item.ReadWriteLength = 4;
                        break;
                    case DataTypeEnums.UInt32:
                        item.ReadWriteLength = 4;
                        break;
                    case DataTypeEnums.Int64:
                        item.ReadWriteLength = 8;
                        break;
                    case DataTypeEnums.UInt64:
                        item.ReadWriteLength = 8;
                        break;
                    case DataTypeEnums.Float:
                        item.ReadWriteLength = 4;
                        break;
                    case DataTypeEnums.Double:
                        item.ReadWriteLength = 8;
                        break;
                    default:
                        throw new Exception($"未定义数据类型：{t.Value}");
                }
                return item;
            }).ToArray();
        }

        public static SiemensWriteAddress ConvertWriteArg(string address, int offest, byte[] writeData, bool bit)
        {
            SiemensWriteAddress arg = new SiemensWriteAddress(ConvertArg(address, offest));
            arg.WriteData = writeData;
            arg.IsBit = bit;
            return arg;
        }

    }
}

using System;
using System.Text.RegularExpressions;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS协议地址解析类
    /// </summary>
    public class FinsAddress
    {
        /// <summary>
        /// 内存区域类型
        /// </summary>
        public FinsMemoryArea MemoryArea { get; set; }

        /// <summary>
        /// 起始地址
        /// </summary>
        public int Address { get; set; }

        /// <summary>
        /// 位地址（仅用于位操作）
        /// </summary>
        public int BitAddress { get; set; }

        /// <summary>
        /// 是否为位操作
        /// </summary>
        public bool IsBit { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public DataTypeEnums DataType { get; set; }

        /// <summary>
        /// 原始地址字符串
        /// </summary>
        public string OriginalAddress { get; set; }

        /// <summary>
        /// 地址是否有效
        /// </summary>
        public bool IsValid
        {
            get
            {
                try
                {
                    // 检查基本属性是否有效
                    if (string.IsNullOrEmpty(OriginalAddress))
                        return false;

                    // 检查地址范围
                    if (Address < 0)
                        return false;

                    // 检查位地址范围
                    if (IsBit && (BitAddress < 0 || BitAddress > 15))
                        return false;

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public FinsAddress()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="address">地址字符串</param>
        /// <param name="dataType">数据类型</param>
        public FinsAddress(string address, DataTypeEnums dataType = DataTypeEnums.None)
        {
            OriginalAddress = address;
            DataType = dataType;
            ParseAddress(address);
        }

        /// <summary>
        /// 解析地址字符串
        /// </summary>
        /// <param name="address">地址字符串</param>
        private void ParseAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("地址不能为空", nameof(address));

            address = address.ToUpper().Trim();

            // 解析位地址格式：如 CIO100.01, DM100.15
            var bitMatch = Regex.Match(address, @"^([A-Z]+)(\d+)\.(\d{1,2})$");
            if (bitMatch.Success)
            {
                IsBit = true;
                string areaCode = bitMatch.Groups[1].Value;
                Address = int.Parse(bitMatch.Groups[2].Value);
                BitAddress = int.Parse(bitMatch.Groups[3].Value);
                
                if (BitAddress > 15)
                    throw new ArgumentException($"位地址超出范围(0-15): {BitAddress}");

                MemoryArea = ParseMemoryArea(areaCode);
                return;
            }

            // 解析字地址格式：如 CIO100, DM200, WR300
            var wordMatch = Regex.Match(address, @"^([A-Z]+)(\d+)$");
            if (wordMatch.Success)
            {
                IsBit = false;
                string areaCode = wordMatch.Groups[1].Value;
                Address = int.Parse(wordMatch.Groups[2].Value);
                BitAddress = 0;
                MemoryArea = ParseMemoryArea(areaCode);
                return;
            }

            throw new ArgumentException($"无效的地址格式: {address}");
        }

        /// <summary>
        /// 解析内存区域代码
        /// </summary>
        /// <param name="areaCode">区域代码</param>
        /// <returns>内存区域枚举</returns>
        private FinsMemoryArea ParseMemoryArea(string areaCode)
        {
            switch (areaCode)
            {
                case "CIO":
                    return FinsMemoryArea.CIO;
                case "WR":
                case "W":
                    return FinsMemoryArea.WR;
                case "HR":
                case "H":
                    return FinsMemoryArea.HR;
                case "AR":
                case "A":
                    return FinsMemoryArea.AR;
                case "DM":
                case "D":
                    return FinsMemoryArea.DM;
                case "EM":
                case "E":
                    return FinsMemoryArea.EM;
                case "TIM":
                case "T":
                    return IsBit ? FinsMemoryArea.TIM_FLAG : FinsMemoryArea.TIM_PV;
                case "CNT":
                case "C":
                    return IsBit ? FinsMemoryArea.CNT_FLAG : FinsMemoryArea.CNT_PV;
                default:
                    throw new ArgumentException($"不支持的内存区域: {areaCode}");
            }
        }

        /// <summary>
        /// 获取内存区域代码字节
        /// </summary>
        /// <returns>内存区域代码</returns>
        public byte GetMemoryAreaCode()
        {
            return (byte)MemoryArea;
        }

        /// <summary>
        /// 获取地址字节数组（用于FINS命令）
        /// </summary>
        /// <returns>地址字节数组</returns>
        public byte[] GetAddressBytes()
        {
            if (IsBit)
            {
                // 位地址格式：[高字节][低字节][位号]
                return new byte[]
                {
                    (byte)(Address >> 8),
                    (byte)(Address & 0xFF),
                    (byte)BitAddress
                };
            }
            else
            {
                // 字地址格式：[高字节][低字节][00]
                return new byte[]
                {
                    (byte)(Address >> 8),
                    (byte)(Address & 0xFF),
                    0x00
                };
            }
        }

        /// <summary>
        /// 创建地址实例
        /// </summary>
        /// <param name="address">地址字符串</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>地址实例</returns>
        public static FinsAddress Create(string address, DataTypeEnums dataType = DataTypeEnums.None)
        {
            return new FinsAddress(address, dataType);
        }

        /// <summary>
        /// 验证地址是否有效
        /// </summary>
        /// <param name="address">地址字符串</param>
        /// <returns>是否有效</returns>
        public static bool IsValidAddress(string address)
        {
            try
            {
                var finsAddress = new FinsAddress(address);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 计算下一个地址
        /// </summary>
        /// <param name="offset">偏移量</param>
        /// <returns>新的地址实例</returns>
        public FinsAddress GetNextAddress(int offset)
        {
            var newAddress = new FinsAddress
            {
                MemoryArea = this.MemoryArea,
                Address = this.Address + offset,
                BitAddress = this.BitAddress,
                IsBit = this.IsBit,
                DataType = this.DataType
            };

            // 重新构造原始地址字符串
            if (newAddress.IsBit)
            {
                newAddress.OriginalAddress = $"{GetAreaString()}{newAddress.Address}.{newAddress.BitAddress:D2}";
            }
            else
            {
                newAddress.OriginalAddress = $"{GetAreaString()}{newAddress.Address}";
            }

            return newAddress;
        }

        /// <summary>
        /// 获取区域字符串表示
        /// </summary>
        /// <returns>区域字符串</returns>
        private string GetAreaString()
        {
            switch (MemoryArea)
            {
                case FinsMemoryArea.CIO:
                    return "CIO";
                case FinsMemoryArea.WR:
                    return "WR";
                case FinsMemoryArea.HR:
                    return "HR";
                case FinsMemoryArea.AR:
                    return "AR";
                case FinsMemoryArea.DM:
                    return "DM";
                case FinsMemoryArea.EM:
                    return "EM";
                case FinsMemoryArea.TIM_FLAG:
                case FinsMemoryArea.TIM_PV:
                    return "TIM";
                case FinsMemoryArea.CNT_FLAG:
                case FinsMemoryArea.CNT_PV:
                    return "CNT";
                default:
                    return "UNKNOWN";
            }
        }

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        /// <returns>地址字符串表示</returns>
        public override string ToString()
        {
            return OriginalAddress ?? $"{GetAreaString()}{Address}{(IsBit ? $".{BitAddress:D2}" : "")}";
        }
    }
}
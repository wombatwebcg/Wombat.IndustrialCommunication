using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS协议通用方法类
    /// </summary>
    public static class FinsCommonMethods
    {
        /// <summary>
        /// FINS头部长度常量
        /// </summary>
        public const int FINS_HEADER_LENGTH = 10;
        /// <summary>
        /// 计算校验和
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>校验和</returns>
        public static byte CalculateChecksum(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            int sum = 0;
            foreach (byte b in data)
            {
                sum += b;
            }
            return (byte)(sum & 0xFF);
        }

        /// <summary>
        /// 验证校验和
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="checksum">校验和</param>
        /// <returns>是否有效</returns>
        public static bool ValidateChecksum(byte[] data, byte checksum)
        {
            return CalculateChecksum(data) == checksum;
        }

        /// <summary>
        /// 构建FINS握手命令
        /// </summary>
        /// <returns>握手命令字节数组</returns>
        public static byte[] BuildHandshakeCommand()
        {
            var command = new List<byte>();
            
            // FINS握手命令格式：长度(4字节) + 命令类型(4字节) + 客户端节点地址(4字节) + 服务器节点地址(4字节) + 其他参数(8字节)
            // 总长度：24字节
            var handshakeData = new byte[20]; // 除去长度字段的数据
            
            // 命令长度 (4字节，大端序)
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x14); // 20字节数据长度
            
            // 命令类型 (4字节) - FINS握手命令
            command.Add(0x46); // 'F'
            command.Add(0x49); // 'I'
            command.Add(0x4E); // 'N'
            command.Add(0x53); // 'S'
            
            // 客户端节点地址 (4字节)
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            
            // 服务器节点地址 (4字节)
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            
            // 其他参数 (8字节)
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            
            return command.ToArray();
        }

        /// <summary>
        /// 构建FINS命令头
        /// </summary>
        /// <param name="icf">信息控制字段</param>
        /// <param name="rsv">保留字段</param>
        /// <param name="gct">网关计数</param>
        /// <param name="dna">目标网络地址</param>
        /// <param name="da1">目标节点号</param>
        /// <param name="da2">目标单元地址</param>
        /// <param name="sna">源网络地址</param>
        /// <param name="sa1">源节点号</param>
        /// <param name="sa2">源单元地址</param>
        /// <param name="sid">服务ID</param>
        /// <returns>命令头字节数组</returns>
        public static byte[] BuildFinsHeader(byte icf = 0x80, byte rsv = 0x00, byte gct = 0x02,
            byte dna = 0x00, byte da1 = 0x00, byte da2 = 0x00,
            byte sna = 0x00, byte sa1 = 0x00, byte sa2 = 0x00, byte sid = 0x00)
        {
            return new byte[]
            {
                icf,  // ICF: 信息控制字段
                rsv,  // RSV: 保留
                gct,  // GCT: 网关计数
                dna,  // DNA: 目标网络地址
                da1,  // DA1: 目标节点号
                da2,  // DA2: 目标单元地址
                sna,  // SNA: 源网络地址
                sa1,  // SA1: 源节点号
                sa2,  // SA2: 源单元地址
                sid   // SID: 服务ID
            };
        }

        /// <summary>
        /// 解析FINS响应头
        /// </summary>
        /// <param name="data">响应数据</param>
        /// <returns>解析结果</returns>
        public static FinsResponseHeader ParseFinsResponseHeader(byte[] data)
        {
            if (data == null || data.Length < 12)
                throw new ArgumentException("响应数据长度不足");

            return new FinsResponseHeader
            {
                ICF = data[0],
                RSV = data[1],
                GCT = data[2],
                DNA = data[3],
                DA1 = data[4],
                DA2 = data[5],
                SNA = data[6],
                SA1 = data[7],
                SA2 = data[8],
                SID = data[9],
                MRC = data[10],  // 主响应码
                SRC = data[11]   // 子响应码
            };
        }

        /// <summary>
        /// 检查FINS响应是否成功
        /// </summary>
        /// <param name="header">响应头</param>
        /// <returns>是否成功</returns>
        public static bool IsResponseSuccess(FinsResponseHeader header)
        {
            return header.MRC == 0x00 && header.SRC == 0x00;
        }

        /// <summary>
        /// 获取错误描述
        /// </summary>
        /// <param name="mrc">主响应码</param>
        /// <param name="src">子响应码</param>
        /// <returns>错误描述</returns>
        public static string GetErrorDescription(byte mrc, byte src)
        {
            ushort errorCode = (ushort)((mrc << 8) | src);
            
            switch (errorCode)
            {
                case FinsConstants.ErrorCodes.NORMAL_COMPLETION:
                    return "正常完成";
                case FinsConstants.ErrorCodes.SERVICE_CANCELED:
                    return "服务被取消";
                case FinsConstants.ErrorCodes.LOCAL_NODE_ERROR:
                    return "本地节点错误";
                case FinsConstants.ErrorCodes.DESTINATION_NODE_ERROR:
                    return "目标节点错误";
                case FinsConstants.ErrorCodes.CONTROLLER_ERROR:
                    return "通信控制器错误";
                case FinsConstants.ErrorCodes.SERVICE_UNSUPPORTED:
                    return "服务不支持";
                case FinsConstants.ErrorCodes.ROUTING_TABLE_ERROR:
                    return "路由表错误";
                case FinsConstants.ErrorCodes.COMMAND_FORMAT_ERROR:
                    return "命令格式错误";
                case FinsConstants.ErrorCodes.PARAMETER_ERROR:
                    return "参数错误";
                case FinsConstants.ErrorCodes.READ_LENGTH_TOO_LONG:
                    return "读取长度过长";
                case FinsConstants.ErrorCodes.COMMAND_LENGTH_TOO_LONG:
                    return "命令长度过长";
                case FinsConstants.ErrorCodes.COMMAND_LENGTH_TOO_SHORT:
                    return "命令长度过短";
                case FinsConstants.ErrorCodes.MEMORY_AREA_ERROR:
                    return "内存区域错误";
                case FinsConstants.ErrorCodes.ADDRESS_RANGE_ERROR:
                    return "地址范围错误";
                case FinsConstants.ErrorCodes.ADDRESS_RANGE_EXCEEDED:
                    return "地址范围超出";
                default:
                    return $"未知错误 (MRC: 0x{mrc:X2}, SRC: 0x{src:X2})";
            }
        }

        /// <summary>
        /// 转换数据到字节数组
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isReverse">是否反转字节序</param>
        /// <returns>字节数组</returns>
        public static byte[] ConvertToBytes(object value, DataTypeEnums dataType, bool isReverse = false)
        {
            if (value == null)
                return new byte[0];

            byte[] result;

            switch (dataType)
            {
                case DataTypeEnums.Bool:
                    result = new byte[] { Convert.ToBoolean(value) ? (byte)1 : (byte)0 };
                    break;
                case DataTypeEnums.Byte:
                    result = new byte[] { Convert.ToByte(value) };
                    break;
                case DataTypeEnums.Int16:
                    result = BitConverter.GetBytes(Convert.ToInt16(value));
                    break;
                case DataTypeEnums.UInt16:
                    result = BitConverter.GetBytes(Convert.ToUInt16(value));
                    break;
                case DataTypeEnums.Int32:
                    result = BitConverter.GetBytes(Convert.ToInt32(value));
                    break;
                case DataTypeEnums.UInt32:
                    result = BitConverter.GetBytes(Convert.ToUInt32(value));
                    break;
                case DataTypeEnums.Float:
                    result = BitConverter.GetBytes(Convert.ToSingle(value));
                    break;
                case DataTypeEnums.Double:
                    result = BitConverter.GetBytes(Convert.ToDouble(value));
                    break;
                case DataTypeEnums.String:
                    result = Encoding.ASCII.GetBytes(value.ToString());
                    break;
                default:
                    throw new NotSupportedException($"不支持的数据类型: {dataType}");
            }

            if (isReverse && result.Length > 1)
            {
                Array.Reverse(result);
            }

            return result;
        }

        /// <summary>
        /// 从字节数组转换数据
        /// </summary>
        /// <param name="data">字节数组</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isReverse">是否反转字节序</param>
        /// <returns>转换后的值</returns>
        public static object ConvertFromBytes(byte[] data, DataTypeEnums dataType, bool isReverse = false)
        {
            if (data == null || data.Length == 0)
                return null;

            byte[] workData = new byte[data.Length];
            Array.Copy(data, workData, data.Length);

            if (isReverse && workData.Length > 1)
            {
                Array.Reverse(workData);
            }

            switch (dataType)
            {
                case DataTypeEnums.Bool:
                    return workData[0] != 0;
                case DataTypeEnums.Byte:
                    return workData[0];
                case DataTypeEnums.Int16:
                    return BitConverter.ToInt16(workData, 0);
                case DataTypeEnums.UInt16:
                    return BitConverter.ToUInt16(workData, 0);
                case DataTypeEnums.Int32:
                    return BitConverter.ToInt32(workData, 0);
                case DataTypeEnums.UInt32:
                    return BitConverter.ToUInt32(workData, 0);
                case DataTypeEnums.Float:
                    return BitConverter.ToSingle(workData, 0);
                case DataTypeEnums.Double:
                    return BitConverter.ToDouble(workData, 0);
                case DataTypeEnums.String:
                    return Encoding.ASCII.GetString(workData).TrimEnd('\0');
                default:
                    throw new NotSupportedException($"不支持的数据类型: {dataType}");
            }
        }

        /// <summary>
        /// 获取数据类型的字节长度
        /// </summary>
        /// <param name="dataType">数据类型</param>
        /// <returns>字节长度</returns>
        public static int GetDataTypeLength(DataTypeEnums dataType)
        {
            switch (dataType)
            {
                case DataTypeEnums.Bool:
                case DataTypeEnums.Byte:
                    return 1;
                case DataTypeEnums.Int16:
                case DataTypeEnums.UInt16:
                    return 2;
                case DataTypeEnums.Int32:
                case DataTypeEnums.UInt32:
                case DataTypeEnums.Float:
                    return 4;
                case DataTypeEnums.Double:
                    return 8;
                case DataTypeEnums.String:
                    return 1; // 字符串长度需要单独指定
                default:
                    return 1;
            }
        }

        /// <summary>
        /// 分割大批量读取请求
        /// </summary>
        /// <param name="addresses">地址列表</param>
        /// <param name="maxLength">最大长度</param>
        /// <returns>分割后的地址组</returns>
        public static List<List<FinsAddress>> SplitAddresses(List<FinsAddress> addresses, int maxLength = 990)
        {
            var result = new List<List<FinsAddress>>();
            var currentGroup = new List<FinsAddress>();
            int currentLength = 0;

            foreach (var address in addresses)
            {
                int addressLength = GetDataTypeLength(address.DataType);
                
                if (currentLength + addressLength > maxLength && currentGroup.Count > 0)
                {
                    result.Add(currentGroup);
                    currentGroup = new List<FinsAddress>();
                    currentLength = 0;
                }

                currentGroup.Add(address);
                currentLength += addressLength;
            }

            if (currentGroup.Count > 0)
            {
                result.Add(currentGroup);
            }

            return result;
        }

        /// <summary>
        /// 获取内容长度
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>内容长度</returns>
        public static int GetContentLength(byte[] data)
        {
            if (data == null || data.Length < 4)
                return 0;
            
            // FINS TCP协议中，前4个字节表示后续数据的长度
            return BitConverter.ToInt32(new byte[] { data[3], data[2], data[1], data[0] }, 0);
        }

        /// <summary>
        /// 验证握手响应
        /// </summary>
        /// <param name="response">响应数据</param>
        /// <returns>是否有效</returns>
        public static bool ValidateHandshakeResponse(byte[] response)
        {
            if (response == null || response.Length < 24)
                return false;

            // 检查FINS握手响应的基本格式
            // 前4个字节是长度，接下来是FINS头部
            return response.Length >= 24 && response[4] == 0x80; // ICF字段应该是0x80
        }
    }

    /// <summary>
    /// FINS响应头结构
    /// </summary>
    public class FinsResponseHeader
    {
        /// <summary>
        /// 信息控制字段
        /// </summary>
        public byte ICF { get; set; }

        /// <summary>
        /// 保留字段
        /// </summary>
        public byte RSV { get; set; }

        /// <summary>
        /// 网关计数
        /// </summary>
        public byte GCT { get; set; }

        /// <summary>
        /// 目标网络地址
        /// </summary>
        public byte DNA { get; set; }

        /// <summary>
        /// 目标节点号
        /// </summary>
        public byte DA1 { get; set; }

        /// <summary>
        /// 目标单元地址
        /// </summary>
        public byte DA2 { get; set; }

        /// <summary>
        /// 源网络地址
        /// </summary>
        public byte SNA { get; set; }

        /// <summary>
        /// 源节点号
        /// </summary>
        public byte SA1 { get; set; }

        /// <summary>
        /// 源单元地址
        /// </summary>
        public byte SA2 { get; set; }

        /// <summary>
        /// 服务ID
        /// </summary>
        public byte SID { get; set; }

        /// <summary>
        /// 主响应码
        /// </summary>
        public byte MRC { get; set; }

        /// <summary>
        /// 子响应码
        /// </summary>
        public byte SRC { get; set; }
    }
}
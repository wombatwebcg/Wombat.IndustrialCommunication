using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
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
        /// <param name="clientNodeAddress">客户端节点地址，默认为1</param>
        /// <returns>握手命令字节数组</returns>
        public static byte[] BuildHandshakeCommand(byte clientNodeAddress = 0x01)
        {
            var command = new List<byte>();
            
            // 标准FINS握手命令格式（总长度：20字节）
            // 参考：https://blog.csdn.net/xiaopig0516/article/details/142895171
            
            // FINS头部标识 (4字节)
            command.Add(0x46); // 'F'
            command.Add(0x49); // 'I'
            command.Add(0x4E); // 'N'
            command.Add(0x53); // 'S'
            
            // 长度字段 (4字节，大端序) - 后续数据长度为12字节
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x0C); // 12字节数据长度
            
            // 命令字段 (4字节) - 握手命令
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            
            // 错误码字段 (4字节) - 初始为0
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            
            // 客户端节点地址 (4字节)
            command.Add(0x00);
            command.Add(0x00);
            command.Add(0x00);
            command.Add(clientNodeAddress); // 客户端节点号
            
            var result = command.ToArray();
            
            // 打印发送的握手命令用于调试
            Console.WriteLine($"[FINS握手调试] 发送握手命令 ({result.Length}字节): {string.Join(" ", result.Select(b => b.ToString("X2")))}");
            
            return result;
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
        /// <param name="data">响应头数据</param>
        /// <returns>内容长度</returns>
        public static int GetContentLength(byte[] data)
        {
            if (data == null || data.Length < FINS_HEADER_LENGTH)
                return 0;
            
            // FINS UDP协议响应格式：
            // 0-9: FINS头部 (ICF, RSV, GCT, DNA, DA1, DA2, SNA, SA1, SA2, SID)
            // 10-11: 响应码 (MRC, SRC) - 这部分需要额外接收
            
            // 对于FINS UDP协议，响应长度固定为：
            // - 响应码：2字节 (MRC, SRC)
            // - 数据部分：根据命令类型确定
            
            // 首先需要接收响应码部分
            return 2; // 先接收MRC和SRC，然后根据命令类型确定数据长度
        }

        /// <summary>
        /// 根据FINS响应确定数据长度
        /// </summary>
        /// <param name="fullResponse">完整响应数据（包含头部和响应码）</param>
        /// <param name="requestedLength">请求的数据长度</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>数据长度</returns>
        public static int GetDataLength(byte[] fullResponse, int requestedLength, DataTypeEnums dataType)
        {
            if (fullResponse == null || fullResponse.Length < FINS_HEADER_LENGTH + 2)
                return 0;

            // 检查响应码
            byte mrc = fullResponse[FINS_HEADER_LENGTH];     // 主响应码
            byte src = fullResponse[FINS_HEADER_LENGTH + 1]; // 子响应码

            // 如果有错误，没有数据部分
            if (mrc != 0x00 || src != 0x00)
                return 0;

            // 根据数据类型计算数据长度
            switch (dataType)
            {
                case DataTypeEnums.Bool:
                case DataTypeEnums.Byte:
                    return requestedLength;
                case DataTypeEnums.UInt16:
                case DataTypeEnums.Int16:
                    return requestedLength * 2;
                case DataTypeEnums.UInt32:
                case DataTypeEnums.Int32:
                case DataTypeEnums.Float:
                    return requestedLength * 4;
                case DataTypeEnums.Double:
                    return requestedLength * 8;
                case DataTypeEnums.String:
                    return requestedLength;
                default:
                    return requestedLength * 2; // 默认按字处理
            }
        }

        /// <summary>
        /// 验证FINS握手响应
        /// </summary>
        /// <param name="response">响应数据</param>
        /// <param name="debugOutput">可选的调试输出委托</param>
        /// <returns>是否有效</returns>
        public static bool ValidateHandshakeResponse(byte[] response, Action<string> debugOutput = null)
        {
            if (response == null || response.Length < 24)
            {
                var message = $"[FINS握手调试] 响应数据无效: 长度={response?.Length ?? 0}, 期望>=24";
                debugOutput?.Invoke(message);
                Console.WriteLine(message);
                return false;
            }

            // 打印完整的响应数据用于调试
            var responseMessage = $"[FINS握手调试] 接收到的响应数据 ({response.Length}字节): {string.Join(" ", response.Select(b => b.ToString("X2")))}";
            debugOutput?.Invoke(responseMessage);
            Console.WriteLine(responseMessage);

            // 检查FINS握手响应的基本格式
            // 前4字节应该是FINS头部标识
            if (response[0] != 0x46 || response[1] != 0x49 || 
                response[2] != 0x4E || response[3] != 0x53)
            {
                var message = $"[FINS握手调试] FINS头部验证失败: {response[0]:X2} {response[1]:X2} {response[2]:X2} {response[3]:X2}, 期望: 46 49 4E 53";
                debugOutput?.Invoke(message);
                Console.WriteLine(message);
                return false;
            }

            // 检查长度字段（第5-8字节）
            // 长度字段表示除前8字节外的数据长度，对于24字节响应应该是16字节（0x00000010）
            if (response[4] != 0x00 || response[5] != 0x00 || 
                response[6] != 0x00 || response[7] != 0x10)
            {
                var message = $"[FINS握手调试] 长度字段验证失败: {response[4]:X2} {response[5]:X2} {response[6]:X2} {response[7]:X2}, 期望: 00 00 00 10";
                debugOutput?.Invoke(message);
                Console.WriteLine(message);
                return false;
            }

            // 检查命令字段（第9-12字节）
            // 握手响应命令应该是1（0x00000001）
            if (response[8] != 0x00 || response[9] != 0x00 || 
                response[10] != 0x00 || response[11] != 0x01)
            {
                var message = $"[FINS握手调试] 命令字段验证失败: {response[8]:X2} {response[9]:X2} {response[10]:X2} {response[11]:X2}, 期望: 00 00 00 01";
                debugOutput?.Invoke(message);
                Console.WriteLine(message);
                return false;
            }

            // 检查错误码字段（第13-16字节）
            // 成功时错误码应该是0（0x00000000）
            if (response[12] != 0x00 || response[13] != 0x00 || 
                response[14] != 0x00 || response[15] != 0x00)
            {
                var message = $"[FINS握手调试] 错误码字段验证失败: {response[12]:X2} {response[13]:X2} {response[14]:X2} {response[15]:X2}, 期望: 00 00 00 00";
                debugOutput?.Invoke(message);
                Console.WriteLine(message);
                return false;
            }

            var successMessage = "[FINS握手调试] 握手响应验证成功";
            debugOutput?.Invoke(successMessage);
            Console.WriteLine(successMessage);
            return true;
        }

        /// <summary>
        /// 从握手响应中提取节点地址信息
        /// </summary>
        /// <param name="response">握手响应数据</param>
        /// <param name="clientNodeAddress">客户端节点地址</param>
        /// <param name="serverNodeAddress">服务器节点地址</param>
        /// <returns>是否成功提取</returns>
        public static bool ExtractNodeAddresses(byte[] response, out byte clientNodeAddress, out byte serverNodeAddress)
        {
            clientNodeAddress = 0;
            serverNodeAddress = 0;

            if (response == null || response.Length < 24)
                return false;

            // 客户端节点地址在第17-20字节
            clientNodeAddress = response[19];
            
            // 服务器节点地址在第21-24字节
            serverNodeAddress = response[23];

            return true;
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
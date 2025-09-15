using System;
using System.Text;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS写入响应消息类
    /// </summary>
    public class FinsWriteResponse : IDeviceReadWriteMessage
    {
        /// <summary>
        /// 协议消息帧
        /// </summary>
        public byte[] ProtocolMessageFrame { get; private set; }

        /// <summary>
        /// 响应头信息
        /// </summary>
        public FinsResponseHeader ResponseHeader { get; private set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// 错误码
        /// </summary>
        public ushort ErrorCode { get; private set; }

        /// <summary>
        /// 错误描述
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// 服务ID
        /// </summary>
        public byte ServiceId { get; private set; }

        /// <summary>
        /// 寄存器数量
        /// </summary>
        public int RegisterCount { get; set; }

        /// <summary>
        /// 寄存器地址
        /// </summary>
        public string RegisterAddress { get; set; }

        /// <summary>
        /// 协议响应长度
        /// </summary>
        public int ProtocolResponseLength { get; set; }

        /// <summary>
        /// 协议数据编号
        /// </summary>
        public int ProtocolDataNumber { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="responseData">响应数据</param>
        public FinsWriteResponse(byte[] responseData)
        {
            ProtocolMessageFrame = responseData ?? throw new ArgumentNullException(nameof(responseData));
            ParseResponse();
        }

        /// <summary>
        /// 解析响应数据
        /// </summary>
        private void ParseResponse()
        {
            try
            {
                if (ProtocolMessageFrame.Length < 12)
                {
                    IsSuccess = false;
                    ErrorCode = 0xFFFF;
                    ErrorMessage = "响应数据长度不足";
                    return;
                }

                // 解析响应头 (12字节)
                ResponseHeader = FinsCommonMethods.ParseFinsResponseHeader(ProtocolMessageFrame);
                ServiceId = ResponseHeader.SID;

                // 检查主响应码和子响应码
                if (ProtocolMessageFrame.Length >= 12)
                {
                    byte mres = ProtocolMessageFrame[10]; // 主响应码
                    byte sres = ProtocolMessageFrame[11]; // 子响应码

                    ErrorCode = (ushort)((mres << 8) | sres);
                    
                    if (mres == 0x00 && sres == 0x00)
                    {
                        IsSuccess = true;
                        ErrorMessage = "写入成功";
                    }
                    else
                    {
                        IsSuccess = false;
                        ErrorMessage = FinsCommonMethods.GetErrorDescription(mres, sres);
                    }
                }
                else
                {
                    IsSuccess = false;
                    ErrorCode = 0xFFFF;
                    ErrorMessage = "响应格式错误";
                }
            }
            catch (Exception ex)
            {
                IsSuccess = false;
                ErrorCode = 0xFFFF;
                ErrorMessage = $"解析响应时发生错误: {ex.Message}";
            }
        }

        /// <summary>
        /// 验证写入操作是否成功
        /// </summary>
        /// <returns>是否成功</returns>
        public bool ValidateWriteSuccess()
        {
            return IsSuccess && ErrorCode == 0x0000;
        }

        /// <summary>
        /// 获取详细的错误信息
        /// </summary>
        /// <returns>错误信息</returns>
        public string GetDetailedErrorInfo()
        {
            if (IsSuccess)
                return "写入操作成功完成";

            var sb = new StringBuilder();
            sb.AppendLine($"写入操作失败");
            sb.AppendLine($"错误码: 0x{ErrorCode:X4}");
            sb.AppendLine($"错误描述: {ErrorMessage}");
            
            if (ResponseHeader != null)
            {
                sb.AppendLine($"服务ID: 0x{ResponseHeader.SID:X2}");
                sb.AppendLine($"ICF: 0x{ResponseHeader.ICF:X2}");
                sb.AppendLine($"RSV: 0x{ResponseHeader.RSV:X2}");
                sb.AppendLine($"GCT: 0x{ResponseHeader.GCT:X2}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 获取响应状态摘要
        /// </summary>
        /// <returns>状态摘要</returns>
        public string GetStatusSummary()
        {
            if (IsSuccess)
                return "写入成功";
            else
                return $"写入失败 - {ErrorMessage} (错误码: 0x{ErrorCode:X4})";
        }

        /// <summary>
        /// 检查是否为网络错误
        /// </summary>
        /// <returns>是否为网络错误</returns>
        public bool IsNetworkError()
        {
            if (!IsSuccess)
            {
                byte mres = (byte)(ErrorCode >> 8);
                // 网络相关错误码
                return mres == 0x01 || mres == 0x02 || mres == 0x03;
            }
            return false;
        }

        /// <summary>
        /// 检查是否为地址错误
        /// </summary>
        /// <returns>是否为地址错误</returns>
        public bool IsAddressError()
        {
            if (!IsSuccess)
            {
                byte mres = (byte)(ErrorCode >> 8);
                byte sres = (byte)(ErrorCode & 0xFF);
                // 地址相关错误码
                return (mres == 0x11 && (sres == 0x01 || sres == 0x02 || sres == 0x03)) ||
                       (mres == 0x20 && (sres >= 0x01 && sres <= 0x0F));
            }
            return false;
        }

        /// <summary>
        /// 检查是否为数据错误
        /// </summary>
        /// <returns>是否为数据错误</returns>
        public bool IsDataError()
        {
            if (!IsSuccess)
            {
                byte mres = (byte)(ErrorCode >> 8);
                byte sres = (byte)(ErrorCode & 0xFF);
                // 数据相关错误码
                return (mres == 0x11 && sres == 0x04) ||
                       (mres == 0x20 && (sres >= 0x10 && sres <= 0x1F));
            }
            return false;
        }

        /// <summary>
        /// 检查是否为权限错误
        /// </summary>
        /// <returns>是否为权限错误</returns>
        public bool IsPermissionError()
        {
            if (!IsSuccess)
            {
                byte mres = (byte)(ErrorCode >> 8);
                // 权限相关错误码
                return mres == 0x22 || mres == 0x23;
            }
            return false;
        }

        /// <summary>
        /// 创建成功响应
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>成功响应</returns>
        public static FinsWriteResponse CreateSuccessResponse(byte serviceId = 0x00)
        {
            var responseData = new byte[12];
            
            // 构建成功响应头
            responseData[0] = 0xC0; // ICF: 响应
            responseData[1] = 0x00; // RSV: 保留
            responseData[2] = 0x02; // GCT: 网关计数
            responseData[3] = 0x00; // DNA: 目标网络地址
            responseData[4] = 0x00; // DA1: 目标节点号
            responseData[5] = 0x00; // DA2: 目标单元地址
            responseData[6] = 0x00; // SNA: 源网络地址
            responseData[7] = 0x00; // SA1: 源节点号
            responseData[8] = 0x00; // SA2: 源单元地址
            responseData[9] = serviceId; // SID: 服务ID
            responseData[10] = 0x00; // MRES: 主响应码 (成功)
            responseData[11] = 0x00; // SRES: 子响应码 (成功)

            return new FinsWriteResponse(responseData);
        }

        /// <summary>
        /// 创建错误响应
        /// </summary>
        /// <param name="errorCode">错误码</param>
        /// <param name="serviceId">服务ID</param>
        /// <returns>错误响应</returns>
        public static FinsWriteResponse CreateErrorResponse(ushort errorCode, byte serviceId = 0x00)
        {
            var responseData = new byte[12];
            
            // 构建错误响应头
            responseData[0] = 0xC0; // ICF: 响应
            responseData[1] = 0x00; // RSV: 保留
            responseData[2] = 0x02; // GCT: 网关计数
            responseData[3] = 0x00; // DNA: 目标网络地址
            responseData[4] = 0x00; // DA1: 目标节点号
            responseData[5] = 0x00; // DA2: 目标单元地址
            responseData[6] = 0x00; // SNA: 源网络地址
            responseData[7] = 0x00; // SA1: 源节点号
            responseData[8] = 0x00; // SA2: 源单元地址
            responseData[9] = serviceId; // SID: 服务ID
            responseData[10] = (byte)(errorCode >> 8); // MRES: 主响应码
            responseData[11] = (byte)(errorCode & 0xFF); // SRES: 子响应码

            return new FinsWriteResponse(responseData);
        }

        /// <summary>
        /// 创建批量写入响应
        /// </summary>
        /// <param name="writeResults">写入结果列表</param>
        /// <param name="serviceId">服务ID</param>
        /// <returns>批量写入响应</returns>
        public static FinsWriteResponse CreateBatchWriteResponse(bool[] writeResults, byte serviceId = 0x00)
        {
            if (writeResults == null || writeResults.Length == 0)
                return CreateErrorResponse(0x1104, serviceId); // 数据错误

            // 检查是否所有写入都成功
            bool allSuccess = true;
            foreach (var result in writeResults)
            {
                if (!result)
                {
                    allSuccess = false;
                    break;
                }
            }

            if (allSuccess)
                return CreateSuccessResponse(serviceId);
            else
                return CreateErrorResponse(0x1104, serviceId); // 部分写入失败
        }

        /// <summary>
        /// 获取原始响应数据的十六进制表示
        /// </summary>
        /// <returns>十六进制字符串</returns>
        public string GetRawDataHex()
        {
            if (ProtocolMessageFrame == null || ProtocolMessageFrame.Length == 0)
                return "无数据";

            var sb = new StringBuilder();
            for (int i = 0; i < ProtocolMessageFrame.Length; i++)
            {
                if (i > 0 && i % 16 == 0)
                    sb.AppendLine();
                else if (i > 0 && i % 8 == 0)
                    sb.Append("  ");
                else if (i > 0)
                    sb.Append(" ");
                
                sb.Append($"{ProtocolMessageFrame[i]:X2}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 初始化消息
        /// </summary>
        /// <param name="data">初始化数据</param>
        public void Initialize(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                ProtocolMessageFrame = data;
                ParseResponse();
            }
        }

        /// <summary>
        /// 获取消息描述
        /// </summary>
        /// <returns>消息描述</returns>
        public override string ToString()
        {
            return $"FINS写入响应 - 状态: {(IsSuccess ? "成功" : "失败")}, 错误码: 0x{ErrorCode:X4}, 消息: {ErrorMessage}";
        }
    }
}
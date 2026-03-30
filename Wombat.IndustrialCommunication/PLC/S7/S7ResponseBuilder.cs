using System;
using System.Collections.Generic;
using System.Linq;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// S7协议响应生成器，用于构造标准的S7协议响应
    /// </summary>
    public static class S7ResponseBuilder
    {
        /// <summary>
        /// 创建S7连接建立响应
        /// </summary>
        /// <param name="request">原始请求</param>
        /// <param name="siemensVersion">西门子版本</param>
        /// <param name="rack">机架号</param>
        /// <param name="slot">槽号</param>
        /// <returns>连接建立响应</returns>
        public static byte[] CreateConnectionResponse(byte[] request, SiemensVersion siemensVersion, byte rack = 0, byte slot = 1)
        {
            // 检查请求类型，S7协议连接建立分为两个阶段
            if (request == null || request.Length < 10)
                return null;

            // 检查是否是COTP连接请求
            if (request[0] == 0x03 && request[1] == 0x00 && request[4] == 0x11 && request[5] == 0xE0)
            {
                // 第一阶段：COTP连接建立响应
                return CreateCOTPConnectionResponse(request, siemensVersion);
            }
            // 检查是否是S7通信建立请求
            else if (request[0] == 0x03 && request[1] == 0x00 && request[4] == 0x02 && request[5] == 0xF0)
            {
                // 第二阶段：S7通信建立响应
                return CreateS7CommunicationResponse(request, siemensVersion, rack, slot);
            }

            // 根据不同的PLC型号返回对应的响应
            switch (siemensVersion)
            {
                case SiemensVersion.S7_200:
                    return CreateS7_200ConnectionResponse(request);
                case SiemensVersion.S7_200Smart:
                    return CreateS7_200SmartConnectionResponse(request);
                case SiemensVersion.S7_300:
                case SiemensVersion.S7_400:
                case SiemensVersion.S7_1200:
                case SiemensVersion.S7_1500:
                default:
                    return CreateStandardConnectionResponse(request, rack, slot);
            }
        }

        /// <summary>
        /// 创建COTP连接建立响应
        /// </summary>
        /// <param name="request">原始请求</param>
        /// <param name="siemensVersion">西门子版本</param>
        /// <returns>COTP连接响应</returns>
        private static byte[] CreateCOTPConnectionResponse(byte[] request, SiemensVersion siemensVersion)
        {
            // COTP连接建立响应格式
            byte[] response = new byte[22];
            
            // TPKT头 (4字节)
            response[0] = 0x03; // 版本
            response[1] = 0x00; // 保留
            response[2] = 0x00; // 长度高字节
            response[3] = 0x16; // 长度低字节 (22)
            
            // COTP头 (18字节)
            response[4] = 0x11; // 长度
            response[5] = 0xE0; // CR = Connection Response
            response[6] = 0x00; // Destination Reference (High Byte)
            response[7] = 0x00; // Destination Reference (Low Byte)
            response[8] = 0x00; // Source Reference (High Byte)
            response[9] = 0x01; // Source Reference (Low Byte)
            response[10] = 0x00; // Flags
            
            // TPDU Number
            response[11] = 0xC1; // TPDU Number
            response[12] = 0x02; // 长度
            response[13] = 0x10; // 目标TSAP
            response[14] = 0x00; // 源TSAP
            
            // 根据PLC型号设置不同的参数
            switch (siemensVersion)
            {
                case SiemensVersion.S7_200:
                    response[15] = 0xC2; // 参数代码
                    response[16] = 0x02; // 长度
                    response[17] = 0x03; // 参数值
                    response[18] = 0x00;
                    response[19] = 0xC0; // 参数代码
                    response[20] = 0x01; // 长度
                    response[21] = 0x0A; // 参数值
                    break;
                    
                case SiemensVersion.S7_200Smart:
                    response[15] = 0xC2; // 参数代码
                    response[16] = 0x02; // 长度
                    response[17] = 0x03; // 参数值
                    response[18] = 0x00;
                    response[19] = 0xC0; // 参数代码
                    response[20] = 0x01; // 长度
                    response[21] = 0x0A; // 参数值
                    break;
                    
                default:
                    response[15] = 0xC2; // 参数代码
                    response[16] = 0x02; // 长度
                    response[17] = 0x01; // 参数值
                    response[18] = 0x02;
                    response[19] = 0xC0; // 参数代码
                    response[20] = 0x01; // 长度
                    response[21] = 0x0A; // 参数值
                    break;
            }
            
            return response;
        }

        /// <summary>
        /// 创建S7通信建立响应
        /// </summary>
        /// <param name="request">原始请求</param>
        /// <param name="siemensVersion">西门子版本</param>
        /// <param name="rack">机架号</param>
        /// <param name="slot">槽号</param>
        /// <returns>S7通信响应</returns>
        private static byte[] CreateS7CommunicationResponse(byte[] request, SiemensVersion siemensVersion, byte rack, byte slot)
        {
            // S7通信建立响应格式
            byte[] response = new byte[25];
            
            // TPKT头 (4字节)
            response[0] = 0x03; // 版本
            response[1] = 0x00; // 保留
            response[2] = 0x00; // 长度高字节
            response[3] = 0x19; // 长度低字节 (25)
            
            // COTP头 (3字节)
            response[4] = 0x02; // 长度
            response[5] = 0xF0; // DT = Data Transfer
            response[6] = 0x80; // TPDU Number
            
            // S7头 (7字节)
            response[7] = 0x32; // 协议ID
            response[8] = 0x03; // 消息类型 (Acknowledge)
            response[9] = 0x00; // 冗余标识
            response[10] = 0x00; // 协议数据单元引用
            response[11] = 0x00; // 参数长度 (高字节)
            response[12] = 0x00; // 参数长度 (低字节)
            response[13] = 0x00; // 数据长度 (高字节)
            response[14] = 0x00; // 数据长度 (低字节)
            
            // 参数 (8字节)
            response[15] = 0x00; // 参数代码
            response[16] = 0x00; // 参数长度 (高字节)
            response[17] = 0x00; // 参数长度 (低字节)
            response[18] = 0x00; // 参数代码
            response[19] = 0x00; // 参数长度 (高字节)
            response[20] = 0x00; // 参数长度 (低字节)
            response[21] = 0x00; // 参数代码
            response[22] = 0x00; // 参数长度 (高字节)
            response[23] = 0x00; // 参数长度 (低字节)
            response[24] = 0x00; // 参数代码
            
            return response;
        }

        /// <summary>
        /// 创建S7_200连接响应
        /// </summary>
        private static byte[] CreateS7_200ConnectionResponse(byte[] request)
        {
            // S7_200的连接响应格式 - 更符合实际协议
            byte[] response = new byte[22];
            
            // TPKT头 (4字节)
            response[0] = 0x03; // 版本
            response[1] = 0x00; // 保留
            response[2] = 0x00; // 长度高字节
            response[3] = 0x16; // 长度低字节 (22)
            
            // COTP头 (5字节)
            response[4] = 0x02; // 长度
            response[5] = 0xF0; // CR = Connection Response
            response[6] = 0x80; // Source Reference
            response[7] = 0x00; // Destination Reference (High Byte)
            response[8] = 0x01; // Destination Reference (Low Byte)
            
            // S7头 (7字节)
            response[9] = 0x32; // 协议ID (S7)
            response[10] = 0x03; // ROSCTR (Acknowledgement)
            response[11] = 0x00; // 冗余ID
            response[12] = 0x00; // 参数长度 (高字节)
            response[13] = 0x00; // 参数长度 (低字节)
            response[14] = 0x00; // 数据长度 (高字节)
            response[15] = 0x00; // 数据长度 (低字节)
            
            // 参数 (6字节) - S7_200特定参数
            response[16] = 0x00; // 错误类
            response[17] = 0x00; // 错误码
            response[18] = 0x00; // 参数代码
            response[19] = 0x00; // 参数长度
            response[20] = 0x00; // 参数值
            response[21] = 0x00; // 保留
            
            return response;
        }

        /// <summary>
        /// 创建S7_200Smart连接响应
        /// </summary>
        private static byte[] CreateS7_200SmartConnectionResponse(byte[] request)
        {
            // S7_200Smart的连接响应格式 - 更符合实际协议
            byte[] response = new byte[22];
            
            // TPKT头 (4字节)
            response[0] = 0x03; // 版本
            response[1] = 0x00; // 保留
            response[2] = 0x00; // 长度高字节
            response[3] = 0x16; // 长度低字节 (22)
            
            // COTP头 (5字节)
            response[4] = 0x02; // 长度
            response[5] = 0xF0; // CR = Connection Response
            response[6] = 0x80; // Source Reference
            response[7] = 0x00; // Destination Reference (High Byte)
            response[8] = 0x01; // Destination Reference (Low Byte)
            
            // S7头 (7字节)
            response[9] = 0x32; // 协议ID (S7)
            response[10] = 0x03; // ROSCTR (Acknowledgement)
            response[11] = 0x00; // 冗余ID
            response[12] = 0x00; // 参数长度 (高字节)
            response[13] = 0x00; // 参数长度 (低字节)
            response[14] = 0x00; // 数据长度 (高字节)
            response[15] = 0x00; // 数据长度 (低字节)
            
            // 参数 (6字节) - S7_200Smart特定参数
            response[16] = 0x00; // 错误类
            response[17] = 0x00; // 错误码
            response[18] = 0x00; // 参数代码
            response[19] = 0x00; // 参数长度
            response[20] = 0x00; // 参数值
            response[21] = 0x00; // 保留
            
            return response;
        }

        /// <summary>
        /// 创建标准连接响应（S7_300/400/1200/1500）
        /// </summary>
        private static byte[] CreateStandardConnectionResponse(byte[] request, byte rack, byte slot)
        {
            // 标准S7连接响应格式 - 更符合实际协议
            byte[] response = new byte[22];
            
            // TPKT头 (4字节)
            response[0] = 0x03; // 版本
            response[1] = 0x00; // 保留
            response[2] = 0x00; // 长度高字节
            response[3] = 0x16; // 长度低字节 (22)
            
            // COTP头 (5字节)
            response[4] = 0x02; // 长度
            response[5] = 0xF0; // CR = Connection Response
            response[6] = 0x80; // Source Reference
            response[7] = 0x00; // Destination Reference (High Byte)
            response[8] = 0x01; // Destination Reference (Low Byte)
            
            // S7头 (7字节)
            response[9] = 0x32; // 协议ID (S7)
            response[10] = 0x03; // ROSCTR (Acknowledgement)
            response[11] = 0x00; // 冗余ID
            response[12] = 0x00; // 参数长度 (高字节)
            response[13] = 0x00; // 参数长度 (低字节)
            response[14] = 0x00; // 数据长度 (高字节)
            response[15] = 0x00; // 数据长度 (低字节)
            
            // 参数 (6字节) - 标准S7参数，包含机架和槽号信息
            response[16] = 0x00; // 错误类
            response[17] = 0x00; // 错误码
            response[18] = 0x00; // 参数代码
            response[19] = 0x00; // 参数长度
            response[20] = (byte)((rack * 0x20) + slot); // 机架槽号组合
            response[21] = 0x00; // 保留
            
            return response;
        }

        /// <summary>
        /// 创建S7读取响应
        /// </summary>
        /// <param name="request">原始请求</param>
        /// <param name="readData">读取的数据列表</param>
        /// <returns>读取响应</returns>
        public static byte[] CreateReadResponse(byte[] request, List<byte[]> readData)
        {
            if (request == null || request.Length < 19)
                return CreateErrorResponse(request, 0x01); // 无效请求

            try
            {
                if (!TryParseRequestFrame(request, out int s7Offset, out int parameterOffset, out int itemCount, out int[] requestLengths, out bool[] requestBitTypes))
                {
                    return CreateErrorResponse(request, 0x01);
                }

                if (itemCount != readData.Count)
                    return CreateErrorResponse(request, 0x02); // 数据项数量不匹配

                List<byte> responseData = new List<byte>();

                for (int i = 0; i < itemCount; i++)
                {
                    var data = readData[i];
                    if (data == null)
                    {
                        responseData.Add(0x0A);
                        responseData.Add(0x00);
                        responseData.Add(0x00);
                        responseData.Add(0x00);
                    }
                    else
                    {
                        int lengthInBits = requestBitTypes[i] ? requestLengths[i] : data.Length * 8;
                        responseData.Add(0xFF);
                        responseData.Add(requestBitTypes[i] ? (byte)0x03 : (byte)0x04);
                        responseData.Add((byte)(lengthInBits >> 8));
                        responseData.Add((byte)(lengthInBits & 0xFF));
                        responseData.AddRange(data);
                    }
                }

                byte[] parameter = { 0x04, (byte)itemCount };
                return BuildAckDataResponse(request, s7Offset, parameter, responseData.ToArray());
            }
            catch (Exception)
            {
                return CreateErrorResponse(request, 0x04); // 服务器故障
            }
        }

        /// <summary>
        /// 创建S7写入响应
        /// </summary>
        /// <param name="request">原始请求</param>
        /// <param name="writeResults">写入结果列表</param>
        /// <returns>写入响应</returns>
        public static byte[] CreateWriteResponse(byte[] request, List<bool> writeResults)
        {
            if (request == null || request.Length < 19)
                return CreateErrorResponse(request, 0x01); // 无效请求

            try
            {
                if (!TryParseRequestFrame(request, out int s7Offset, out int _, out int itemCount, out int[] _, out bool[] _))
                {
                    return CreateErrorResponse(request, 0x01);
                }

                if (itemCount != writeResults.Count)
                    return CreateErrorResponse(request, 0x02); // 数据项数量不匹配

                List<byte> responseData = new List<byte>();

                for (int i = 0; i < itemCount; i++)
                {
                    responseData.Add(writeResults[i] ? (byte)0xFF : (byte)0x0A);
                }

                byte[] parameter = { 0x05, (byte)itemCount };
                return BuildAckDataResponse(request, s7Offset, parameter, responseData.ToArray());
            }
            catch (Exception)
            {
                return CreateErrorResponse(request, 0x04); // 服务器故障
            }
        }

        /// <summary>
        /// 创建S7错误响应
        /// </summary>
        /// <param name="request">原始请求</param>
        /// <param name="errorCode">错误代码</param>
        /// <returns>错误响应</returns>
        public static byte[] CreateErrorResponse(byte[] request, byte errorCode)
        {
            if (request == null || request.Length < 19)
                return null;

            try
            {
                if (!TryParseRequestFrame(request, out int s7Offset, out int _, out int itemCount, out int[] _, out bool[] _))
                {
                    return null;
                }

                byte[] parameter = { request[s7Offset + 10], (byte)itemCount };
                byte[] errorData = { errorCode, 0x00, 0x00, 0x00 };
                return BuildAckDataResponse(request, s7Offset, parameter, errorData);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static byte[] BuildAckDataResponse(byte[] request, int s7Offset, byte[] parameter, byte[] data)
        {
            int cotpTotalLength = 1 + request[4];
            int totalLength = 4 + cotpTotalLength + 12 + parameter.Length + data.Length;
            byte[] response = new byte[totalLength];

            response[0] = 0x03;
            response[1] = 0x00;
            response[2] = (byte)(totalLength >> 8);
            response[3] = (byte)(totalLength & 0xFF);

            Array.Copy(request, 4, response, 4, cotpTotalLength);

            int responseS7Offset = 4 + cotpTotalLength;
            response[responseS7Offset] = 0x32;
            response[responseS7Offset + 1] = 0x03;
            response[responseS7Offset + 2] = request[s7Offset + 2];
            response[responseS7Offset + 3] = request[s7Offset + 3];
            response[responseS7Offset + 4] = request[s7Offset + 4];
            response[responseS7Offset + 5] = request[s7Offset + 5];
            response[responseS7Offset + 6] = (byte)(parameter.Length >> 8);
            response[responseS7Offset + 7] = (byte)(parameter.Length & 0xFF);
            response[responseS7Offset + 8] = (byte)(data.Length >> 8);
            response[responseS7Offset + 9] = (byte)(data.Length & 0xFF);
            response[responseS7Offset + 10] = 0x00;
            response[responseS7Offset + 11] = 0x00;

            int payloadOffset = responseS7Offset + 12;
            Array.Copy(parameter, 0, response, payloadOffset, parameter.Length);
            Array.Copy(data, 0, response, payloadOffset + parameter.Length, data.Length);

            return response;
        }

        private static bool TryParseRequestFrame(byte[] request, out int s7Offset, out int parameterOffset, out int itemCount, out int[] requestLengths, out bool[] requestBitTypes)
        {
            s7Offset = 0;
            parameterOffset = 0;
            itemCount = 0;
            requestLengths = Array.Empty<int>();
            requestBitTypes = Array.Empty<bool>();

            if (request == null || request.Length < 19 || request[0] != 0x03 || request[1] != 0x00)
            {
                return false;
            }

            int cotpTotalLength = 1 + request[4];
            s7Offset = 4 + cotpTotalLength;
            if (s7Offset + 10 > request.Length || request[s7Offset] != 0x32)
            {
                return false;
            }

            int parameterLength = (request[s7Offset + 6] << 8) | request[s7Offset + 7];
            parameterOffset = s7Offset + 10;
            if (parameterLength < 2 || parameterOffset + parameterLength > request.Length)
            {
                return false;
            }

            itemCount = request[parameterOffset + 1];
            if (itemCount < 0)
            {
                return false;
            }

            requestLengths = new int[itemCount];
            requestBitTypes = new bool[itemCount];

            for (int i = 0; i < itemCount; i++)
            {
                int itemOffset = parameterOffset + 2 + i * 12;
                if (itemOffset + 11 >= parameterOffset + parameterLength || itemOffset + 11 >= request.Length)
                {
                    return false;
                }

                bool isBit = request[itemOffset + 3] == 0x01;
                int requestLength = (request[itemOffset + 4] << 8) | request[itemOffset + 5];

                requestBitTypes[i] = isBit;
                requestLengths[i] = requestLength;
            }

            return true;
        }

        /// <summary>
        /// 解析S7读取请求参数
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>读取参数列表</returns>
        public static List<S7ReadParameter> ParseReadRequest(byte[] request)
        {
            var parameters = new List<S7ReadParameter>();
            
            if (request == null || request.Length < 19)
                return parameters;

            try
            {
                int itemCount = request[18];
                
                for (int i = 0; i < itemCount; i++)
                {
                    int paramOffset = 19 + i * 12;
                    if (paramOffset + 11 >= request.Length)
                        break;

                    bool isBit = request[paramOffset + 3] == 0x01;
                    int rawLength = (request[paramOffset + 4] << 8) + request[paramOffset + 5];
                    int byteLength = isBit ? (int)Math.Ceiling(rawLength / 8.0) : rawLength;
                    int startAddressBits = (request[paramOffset + 9] << 16) +
                                           (request[paramOffset + 10] << 8) +
                                           request[paramOffset + 11];

                    var parameter = new S7ReadParameter
                    {
                        AreaType = request[paramOffset + 8],
                        DbNumber = request[paramOffset + 8] == (byte)S7Area.DB
                            ? (request[paramOffset + 6] << 8) + request[paramOffset + 7]
                            : 0,
                        StartAddress = startAddressBits / 8,
                        Length = byteLength,
                        IsBit = isBit
                    };
                    
                    parameters.Add(parameter);
                }
            }
            catch (Exception)
            {
            }

            return parameters;
        }

        /// <summary>
        /// 解析S7写入请求参数
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>写入参数列表</returns>
        public static List<S7WriteParameter> ParseWriteRequest(byte[] request)
        {
            var parameters = new List<S7WriteParameter>();
            
            if (request == null || request.Length < 19)
                return parameters;

            try
            {
                int itemCount = request[18];
                int dataOffset = 19 + itemCount * 12;
                
                for (int i = 0; i < itemCount; i++)
                {
                    int paramOffset = 19 + i * 12;
                    if (paramOffset + 11 >= request.Length || dataOffset + 4 > request.Length)
                        break;

                    bool isBit = request[paramOffset + 3] == 0x01;
                    int startAddressBits = (request[paramOffset + 9] << 16) +
                                           (request[paramOffset + 10] << 8) +
                                           request[paramOffset + 11];
                    int dataBitLength = (request[dataOffset + 2] << 8) + request[dataOffset + 3];
                    int byteLength = (int)Math.Ceiling(dataBitLength / 8.0);
                    
                    var parameter = new S7WriteParameter
                    {
                        AreaType = request[paramOffset + 8],
                        DbNumber = request[paramOffset + 8] == (byte)S7Area.DB
                            ? (request[paramOffset + 6] << 8) + request[paramOffset + 7]
                            : 0,
                        StartAddress = startAddressBits / 8,
                        Length = byteLength,
                        IsBit = isBit,
                        Data = new byte[byteLength]
                    };
                    
                    int payloadOffset = dataOffset + 4;
                    if (payloadOffset + byteLength > request.Length)
                        break;

                    Array.Copy(request, payloadOffset, parameter.Data, 0, byteLength);
                    
                    parameters.Add(parameter);

                    dataOffset = payloadOffset + byteLength;
                    if (i < itemCount - 1 && (byteLength % 2 != 0) && dataOffset < request.Length)
                    {
                        dataOffset += 1;
                    }
                }
            }
            catch (Exception)
            {
            }

            return parameters;
        }
    }

    /// <summary>
    /// S7读取参数
    /// </summary>
    public class S7ReadParameter
    {
        public byte AreaType { get; set; }
        public int DbNumber { get; set; }
        public int StartAddress { get; set; }
        public int Length { get; set; }
        public bool IsBit { get; set; }
    }

    /// <summary>
    /// S7写入参数
    /// </summary>
    public class S7WriteParameter
    {
        public byte AreaType { get; set; }
        public int DbNumber { get; set; }
        public int StartAddress { get; set; }
        public int Length { get; set; }
        public bool IsBit { get; set; }
        public byte[] Data { get; set; }
    }
} 

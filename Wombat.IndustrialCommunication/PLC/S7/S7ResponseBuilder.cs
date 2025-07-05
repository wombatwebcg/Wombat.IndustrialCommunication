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
            response[8] = 0x01; // 消息类型 (Job Request)
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
                // 解析请求参数
                int itemCount = request[18];
                if (itemCount != readData.Count)
                    return CreateErrorResponse(request, 0x02); // 数据项数量不匹配

                // 创建响应头
                byte[] responseHeader = new byte[19];
                Array.Copy(request, responseHeader, 19);
                
                // 设置响应类型为确认
                responseHeader[8] = 0x03; // 服务器响应

                // 创建响应数据
                List<byte> responseData = new List<byte>();
                
                // 为每个读取项创建响应
                for (int i = 0; i < itemCount; i++)
                {
                    var data = readData[i];
                    if (data == null)
                    {
                        // 读取失败
                        responseData.Add(0xFF); // 数据项标记
                        responseData.Add(0x0A); // 返回码 (0x0A = 资源不可用)
                        responseData.Add(0x00); // 数据长度 (高字节)
                        responseData.Add(0x00); // 数据长度 (低字节)
                    }
                    else
                    {
                        // 读取成功
                        responseData.Add(0xFF); // 数据项标记
                        responseData.Add(0xFF); // 返回码 (0xFF = 成功)
                        responseData.Add((byte)(data.Length >> 8)); // 数据长度 (高字节)
                        responseData.Add((byte)(data.Length & 0xFF)); // 数据长度 (低字节)
                        responseData.AddRange(data); // 数据内容
                    }
                }

                // 计算总长度
                int totalLength = responseHeader.Length + responseData.Count;
                
                // 创建完整响应
                byte[] response = new byte[totalLength];
                Array.Copy(responseHeader, response, responseHeader.Length);
                responseData.CopyTo(0, response, responseHeader.Length, responseData.Count);

                // 设置TPKT长度
                response[2] = (byte)(totalLength >> 8);
                response[3] = (byte)(totalLength & 0xFF);

                // 设置参数和数据长度
                response[13] = 0x00; // 参数长度 (高字节)
                response[14] = 0x00; // 参数长度 (低字节)
                response[15] = (byte)(responseData.Count >> 8); // 数据长度 (高字节)
                response[16] = (byte)(responseData.Count & 0xFF); // 数据长度 (低字节)

                return response;
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
                // 解析请求参数
                int itemCount = request[18];
                if (itemCount != writeResults.Count)
                    return CreateErrorResponse(request, 0x02); // 数据项数量不匹配

                // 创建响应头
                byte[] responseHeader = new byte[19];
                Array.Copy(request, responseHeader, 19);
                
                // 设置响应类型为确认
                responseHeader[8] = 0x03; // 服务器响应

                // 创建响应数据
                List<byte> responseData = new List<byte>();
                
                // 为每个写入项创建响应
                for (int i = 0; i < itemCount; i++)
                {
                    if (writeResults[i])
                    {
                        // 写入成功
                        responseData.Add(0xFF); // 数据项标记
                        responseData.Add(0xFF); // 返回码 (0xFF = 成功)
                    }
                    else
                    {
                        // 写入失败
                        responseData.Add(0xFF); // 数据项标记
                        responseData.Add(0x0A); // 返回码 (0x0A = 资源不可用)
                    }
                }

                // 计算总长度
                int totalLength = responseHeader.Length + responseData.Count;
                
                // 创建完整响应
                byte[] response = new byte[totalLength];
                Array.Copy(responseHeader, response, responseHeader.Length);
                responseData.CopyTo(0, response, responseHeader.Length, responseData.Count);

                // 设置TPKT长度
                response[2] = (byte)(totalLength >> 8);
                response[3] = (byte)(totalLength & 0xFF);

                // 设置参数和数据长度
                response[13] = 0x00; // 参数长度 (高字节)
                response[14] = 0x00; // 参数长度 (低字节)
                response[15] = (byte)(responseData.Count >> 8); // 数据长度 (高字节)
                response[16] = (byte)(responseData.Count & 0xFF); // 数据长度 (低字节)

                return response;
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
                // 创建响应头
                byte[] responseHeader = new byte[19];
                Array.Copy(request, responseHeader, 19);
                
                // 设置响应类型为确认
                responseHeader[8] = 0x03; // 服务器响应

                // 创建错误数据
                List<byte> errorData = new List<byte>();
                errorData.Add(0xFF); // 数据项标记
                errorData.Add(errorCode); // 错误码

                // 计算总长度
                int totalLength = responseHeader.Length + errorData.Count;
                
                // 创建完整响应
                byte[] response = new byte[totalLength];
                Array.Copy(responseHeader, response, responseHeader.Length);
                errorData.CopyTo(0, response, responseHeader.Length, errorData.Count);

                // 设置TPKT长度
                response[2] = (byte)(totalLength >> 8);
                response[3] = (byte)(totalLength & 0xFF);

                // 设置参数和数据长度
                response[13] = 0x00; // 参数长度 (高字节)
                response[14] = 0x00; // 参数长度 (低字节)
                response[15] = (byte)(errorData.Count >> 8); // 数据长度 (高字节)
                response[16] = (byte)(errorData.Count & 0xFF); // 数据长度 (低字节)

                return response;
            }
            catch (Exception)
            {
                return null;
            }
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

                    var parameter = new S7ReadParameter
                    {
                        AreaType = request[paramOffset + 3],
                        DbNumber = request[paramOffset + 3] == (byte)S7Area.DB ? 
                                  (request[paramOffset + 4] << 8) + request[paramOffset + 5] : 0,
                        StartAddress = (request[paramOffset + 6] << 16) + 
                                     (request[paramOffset + 7] << 8) + 
                                     request[paramOffset + 8],
                        Length = (request[paramOffset + 10] << 8) + request[paramOffset + 11],
                        IsBit = request[paramOffset + 2] == 0x01
                    };
                    
                    parameters.Add(parameter);
                }
            }
            catch (Exception)
            {
                // 解析失败，返回空列表
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
                    if (paramOffset + 11 >= request.Length)
                        break;

                    int length = (request[paramOffset + 10] << 8) + request[paramOffset + 11];
                    bool isBit = request[paramOffset + 2] == 0x01;
                    
                    // 计算实际数据长度
                    int actualLength = isBit ? (int)Math.Ceiling(length / 8.0) : length;
                    
                    var parameter = new S7WriteParameter
                    {
                        AreaType = request[paramOffset + 3],
                        DbNumber = request[paramOffset + 3] == (byte)S7Area.DB ? 
                                  (request[paramOffset + 4] << 8) + request[paramOffset + 5] : 0,
                        StartAddress = (request[paramOffset + 6] << 16) + 
                                     (request[paramOffset + 7] << 8) + 
                                     request[paramOffset + 8],
                        Length = length,
                        IsBit = isBit,
                        Data = new byte[actualLength]
                    };
                    
                    // 提取数据
                    if (dataOffset + actualLength <= request.Length)
                    {
                        Array.Copy(request, dataOffset, parameter.Data, 0, actualLength);
                        dataOffset += actualLength;
                    }
                    
                    parameters.Add(parameter);
                }
            }
            catch (Exception)
            {
                // 解析失败，返回空列表
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
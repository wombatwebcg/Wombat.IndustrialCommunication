using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.Network.Sockets;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// S7 TCP服务器基类，负责处理S7协议
    /// </summary>
    public class S7TcpServerBase : DeviceServerBase
    {
        private readonly AsyncLock _lock = new AsyncLock();
        protected readonly ServerMessageTransport _transport;
        
        /// <summary>
        /// 数据存储
        /// </summary>
        public S7DataStore DataStore { get; } = S7DataStoreFactory.CreateDefaultDataStore();
        
        /// <summary>
        /// 西门子版本
        /// </summary>
        public SiemensVersion SiemensVersion { get; set; } = SiemensVersion.S7_1200;
        
        /// <summary>
        /// 机架号
        /// </summary>
        public byte Rack { get; set; } = 0;
        
        /// <summary>
        /// 槽号
        /// </summary>
        public byte Slot { get; set; } = 1;
        
        /// <summary>
        /// 是否正在监听
        /// </summary>
        public virtual bool IsListening => _transport?.StreamResource?.Connected ?? false;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="transport">服务器消息传输</param>
        public S7TcpServerBase(ServerMessageTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            DataFormat = EndianFormat.ABCD;
            IsReverse = true;
            
            // 注册消息处理程序
            _transport.RegisterMessageHandler(HandleS7Message);
        }
        
        /// <summary>
        /// 版本
        /// </summary>
        public override string Version => SiemensVersion.ToString();
        
        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <returns>操作结果</returns>
        public virtual async Task<OperationResult> StartAsync()
        {
            return await _transport.StartAsync();
        }
        
        /// <summary>
        /// 停止服务器
        /// </summary>
        /// <returns>操作结果</returns>
        public virtual async Task<OperationResult> StopAsync()
        {
            return await _transport.StopAsync();
        }
        
        /// <summary>
        /// 处理S7消息
        /// </summary>
        /// <param name="message">接收到的消息</param>
        private void HandleS7Message(ReceivedMessage message)
        {
            if (message == null || message.Data == null || message.Data.Length < 10)
            {
                Logger?.LogWarning("收到无效的S7消息");
                return;
            }

            try
            {
                // 解析S7消息
                // S7协议的消息结构与Modbus不同，需要特殊处理
                
                Logger?.LogDebug(
                    "收到S7请求: {RequestHex}",
                    BitConverter.ToString(message.Data).Replace("-", " "));
                
                // 处理S7请求并生成响应
                byte[] response = ProcessS7Request(message.Data);
                
                // 向客户端发送响应
                if (response != null)
                {
                    Task.Run(async () =>
                    {
                        Logger?.LogDebug(
                            "发送S7响应: {ResponseHex}",
                            BitConverter.ToString(response).Replace("-", " "));
                            
                        var result = await _transport.SendToSessionAsync(message.Session, response);
                        if (!result.IsSuccess)
                        {
                            Logger?.LogError("发送S7响应失败: {ErrorMessage}", result.Message);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理S7消息时发生错误");
            }
        }
        
        /// <summary>
        /// 处理S7请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        protected virtual byte[] ProcessS7Request(byte[] request)
        {
            if (request == null || request.Length < 10)
                return null;
            
            // 确定S7消息类型
            byte messageType = DetermineS7MessageType(request);
            
            try
            {
                // 根据消息类型处理请求
                switch (messageType)
                {
                    case 0xE0: // 连接建立请求
                        return HandleConnectionRequest(request);
                        
                    case 0x04: // 读取请求
                        return HandleReadRequest(request);
                        
                    case 0x05: // 写入请求
                        return HandleWriteRequest(request);
                        
                    default:
                        // 不支持的功能码
                        Logger?.LogWarning("不支持的S7消息类型: {MessageType:X2}", messageType);
                        return CreateErrorResponse(request, 0x01);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理S7请求时发生错误: MessageType={MessageType:X2}", messageType);
                return CreateErrorResponse(request, 0x04); // 服务器故障
            }
        }
        
        /// <summary>
        /// 确定S7消息类型
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>消息类型</returns>
        private byte DetermineS7MessageType(byte[] request)
        {
            // 解析S7协议头，确定消息类型
            // TPKT (4字节) + COTP (3-8字节) + S7头 (3-7字节) + 数据
            // 消息类型通常在S7头的第一个字节，位置在COTP头之后
            
            // 简化处理，假设COTP头长度为3字节
            if (request.Length >= 8)
            {
                return request[7]; // S7头的第一个字节包含消息类型
            }
            
            return 0;
        }
        
        /// <summary>
        /// 创建错误响应
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <param name="errorCode">错误代码</param>
        /// <returns>错误响应</returns>
        protected byte[] CreateErrorResponse(byte[] request, byte errorCode)
        {
            // 创建S7错误响应
            // 保持TPKT和COTP头不变，修改S7头的错误码
            
            // 简单实现，后续可根据具体需求完善
            byte[] response = new byte[request.Length];
            Array.Copy(request, response, request.Length);
            
            // 设置错误码
            if (response.Length >= 18)
            {
                response[17] = errorCode;
            }
            
            return response;
        }
        
        /// <summary>
        /// 处理连接建立请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        protected virtual byte[] HandleConnectionRequest(byte[] request)
        {
            // 处理S7连接建立请求
            // 简单实现，直接返回成功响应
            
            // TPKT头 (4字节)
            byte[] response = new byte[22];
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
            
            // S7头
            response[9] = 0xD0; // 协议ID
            response[10] = 0x00; // ROSCTR (Acknowledgement)
            response[11] = 0x00; // 冗余ID
            response[12] = 0x00; // 参数长度 (高字节)
            response[13] = 0x00; // 参数长度 (低字节)
            response[14] = 0x00; // 数据长度 (高字节)
            response[15] = 0x00; // 数据长度 (低字节)
            
            // 参数
            response[16] = 0x00; // 错误类
            response[17] = 0x00; // 错误码
            
            // 其他字段填充0
            for (int i = 18; i < response.Length; i++)
            {
                response[i] = 0x00;
            }
            
            return response;
        }
        
        /// <summary>
        /// 处理读取请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        protected virtual byte[] HandleReadRequest(byte[] request)
        {
            // 处理S7读取请求
            // 简单实现，解析请求并从数据存储中读取数据
            
            try
            {
                // 假设请求格式符合S7协议，解析读取参数
                // 读取区域类型、数据块编号、起始地址和长度
                
                // 提取读取项数（实际实现中需要更精确的解析）
                int itemCount = request[18];
                
                // 创建响应头
                byte[] responseHeader = new byte[12];
                Array.Copy(request, responseHeader, 12);
                
                // 设置响应类型
                responseHeader[10] = 0x00; // ROSCTR (Acknowledgement)
                
                // 创建响应数据
                List<byte> responseData = new List<byte>();
                
                // 为每个读取项创建响应
                for (int i = 0; i < itemCount; i++)
                {
                    // 解析读取项参数（实际实现中需要更精确的解析）
                    int paramOffset = 19 + i * 12; // 每个读取项的参数偏移
                    byte areaType = request[paramOffset + 3];
                    int dbNumber = areaType == (byte)S7Area.DB ? (request[paramOffset + 4] << 8) + request[paramOffset + 5] : 0;
                    int startAddress = (request[paramOffset + 6] << 16) + (request[paramOffset + 7] << 8) + request[paramOffset + 8];
                    int length = (request[paramOffset + 10] << 8) + request[paramOffset + 11];
                    
                    // 从数据存储中读取数据
                    byte[] data = DataStore.ReadArea((S7Area)areaType, dbNumber, startAddress, length);
                    
                    // 添加响应数据项
                    responseData.Add(0xFF); // 数据项标记
                    responseData.Add(0x04); // 返回码 (0x04 = 成功)
                    responseData.Add((byte)(data.Length >> 8)); // 数据长度 (高字节)
                    responseData.Add((byte)(data.Length & 0xFF)); // 数据长度 (低字节)
                    responseData.AddRange(data); // 数据内容
                }
                
                // 创建完整响应
                byte[] response = new byte[responseHeader.Length + responseData.Count];
                Array.Copy(responseHeader, response, responseHeader.Length);
                responseData.CopyTo(0, response, responseHeader.Length, responseData.Count);
                
                // 设置参数和数据长度
                response[12] = 0x00; // 参数长度 (高字节)
                response[13] = 0x00; // 参数长度 (低字节)
                response[14] = (byte)(responseData.Count >> 8); // 数据长度 (高字节)
                response[15] = (byte)(responseData.Count & 0xFF); // 数据长度 (低字节)
                
                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理S7读取请求时发生错误");
                return CreateErrorResponse(request, 0x03); // 资源不可用
            }
        }
        
        /// <summary>
        /// 处理写入请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        protected virtual byte[] HandleWriteRequest(byte[] request)
        {
            // 处理S7写入请求
            // 简单实现，解析请求并向数据存储中写入数据
            
            try
            {
                // 假设请求格式符合S7协议，解析写入参数
                // 写入区域类型、数据块编号、起始地址和数据
                
                // 提取写入项数（实际实现中需要更精确的解析）
                int itemCount = request[18];
                
                // 创建响应头
                byte[] responseHeader = new byte[12];
                Array.Copy(request, responseHeader, 12);
                
                // 设置响应类型
                responseHeader[10] = 0x00; // ROSCTR (Acknowledgement)
                
                // 创建响应数据
                List<byte> responseData = new List<byte>();
                
                // 为每个写入项处理请求
                for (int i = 0; i < itemCount; i++)
                {
                    // 解析写入项参数（实际实现中需要更精确的解析）
                    int paramOffset = 19 + i * 12; // 每个写入项的参数偏移
                    byte areaType = request[paramOffset + 3];
                    int dbNumber = areaType == (byte)S7Area.DB ? (request[paramOffset + 4] << 8) + request[paramOffset + 5] : 0;
                    int startAddress = (request[paramOffset + 6] << 16) + (request[paramOffset + 7] << 8) + request[paramOffset + 8];
                    int length = (request[paramOffset + 10] << 8) + request[paramOffset + 11];
                    
                    // 提取写入数据
                    int dataOffset = paramOffset + 12; // 写入数据的偏移
                    byte[] data = new byte[length];
                    Array.Copy(request, dataOffset, data, 0, length);
                    
                    // 向数据存储中写入数据
                    DataStore.WriteArea((S7Area)areaType, dbNumber, startAddress, data);
                    
                    // 添加响应数据项
                    responseData.Add(0xFF); // 数据项标记
                    responseData.Add(0x04); // 返回码 (0x04 = 成功)
                }
                
                // 创建完整响应
                byte[] response = new byte[responseHeader.Length + responseData.Count];
                Array.Copy(responseHeader, response, responseHeader.Length);
                responseData.CopyTo(0, response, responseHeader.Length, responseData.Count);
                
                // 设置参数和数据长度
                response[12] = 0x00; // 参数长度 (高字节)
                response[13] = 0x00; // 参数长度 (低字节)
                response[14] = (byte)(responseData.Count >> 8); // 数据长度 (高字节)
                response[15] = (byte)(responseData.Count & 0xFF); // 数据长度 (低字节)
                
                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理S7写入请求时发生错误");
                return CreateErrorResponse(request, 0x03); // 资源不可用
            }
        }
    }
} 
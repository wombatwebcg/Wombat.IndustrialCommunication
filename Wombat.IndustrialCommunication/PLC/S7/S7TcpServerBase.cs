using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;


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
                Logger?.LogDebug("处理S7请求，消息类型: {MessageType:X2}", messageType);
                
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
                        return S7ResponseBuilder.CreateErrorResponse(request, 0x01); // 不支持的功能
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理S7请求时发生错误: MessageType={MessageType:X2}", messageType);
                return S7ResponseBuilder.CreateErrorResponse(request, 0x04); // 服务器故障
            }
        }
        
        /// <summary>
        /// 确定S7消息类型
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>消息类型</returns>
        private byte DetermineS7MessageType(byte[] request)
        {
            try
            {
                // S7协议结构：TPKT (4字节) + COTP (3-8字节) + S7头 + 数据
                if (request == null || request.Length < 10)
                {
                    Logger?.LogWarning("S7请求数据长度不足，无法解析消息类型");
                    return 0;
                }

                // 检查TPKT头
                if (request[0] != 0x03 || request[1] != 0x00)
                {
                    Logger?.LogWarning("无效的TPKT头: {TPKTHeader:X2} {TPKTReserved:X2}", request[0], request[1]);
                    return 0;
                }

                // 获取TPKT长度
                int tpktLength = (request[2] << 8) | request[3];
                if (tpktLength != request.Length)
                {
                    Logger?.LogWarning("TPKT长度不匹配: 声明={DeclaredLength}, 实际={ActualLength}", tpktLength, request.Length);
                }

                // 检查COTP头
                byte cotpLength = request[4];
                byte cotpType = request[5];
                
                Logger?.LogDebug("COTP头: 长度={COTPLength}, 类型={COTPType:X2}", cotpLength, cotpType);

                // 处理不同类型的COTP消息
                switch (cotpType)
                {
                    case 0xE0: // CR = Connection Request
                        Logger?.LogDebug("检测到COTP连接请求");
                        return 0xE0;
                        
                    case 0xD0: // CC = Connection Confirm
                        Logger?.LogDebug("检测到COTP连接确认");
                        return 0xD0;
                        
                    case 0xF0: // DT = Data Transfer
                        // 继续解析S7协议
                        break;
                        
                    default:
                        Logger?.LogWarning("不支持的COTP消息类型: {COTPType:X2}", cotpType);
                        return 0;
                }

                // 计算S7头的偏移量
                int s7Offset = 4 + cotpLength;
                if (s7Offset + 7 >= request.Length)
                {
                    Logger?.LogWarning("S7头长度不足");
                    return 0;
                }

                // 检查S7协议ID
                if (request[s7Offset] != 0x32)
                {
                    Logger?.LogWarning("无效的S7协议ID: {ProtocolID:X2}", request[s7Offset]);
                    return 0;
                }

                // 获取S7消息类型
                byte s7MessageType = request[s7Offset + 1];
                Logger?.LogDebug("S7消息类型: {MessageType:X2}", s7MessageType);

                // 根据S7消息类型返回对应的功能码
                switch (s7MessageType)
                {
                    case 0x01: // Job Request
                        // 进一步解析功能码
                        if (s7Offset + 17 < request.Length)
                        {
                            byte functionCode = request[s7Offset + 17];
                            Logger?.LogDebug("S7功能码: {FunctionCode:X2}", functionCode);
                            return functionCode;
                        }
                        break;
                        
                    case 0x03: // Acknowledge
                        Logger?.LogDebug("检测到S7确认消息");
                        return 0x03;
                        
                    case 0x07: // User Data
                        Logger?.LogDebug("检测到S7用户数据消息");
                        return 0x07;
                        
                    default:
                        Logger?.LogWarning("不支持的S7消息类型: {MessageType:X2}", s7MessageType);
                        return 0;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "确定S7消息类型时发生错误");
                return 0;
            }
        }
        

        
        /// <summary>
        /// 处理连接建立请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        protected virtual byte[] HandleConnectionRequest(byte[] request)
        {
            try
            {
                Logger?.LogDebug("处理S7连接建立请求，PLC型号：{SiemensVersion}，机架号：{Rack}，槽号：{Slot}", 
                    SiemensVersion, Rack, Slot);
                
                // 验证请求数据
                if (request == null || request.Length < 10)
                {
                    Logger?.LogWarning("连接建立请求数据无效");
                    return S7ResponseBuilder.CreateErrorResponse(request, 0x01); // 无效请求
                }

                // 记录请求详情
                Logger?.LogDebug("连接建立请求详情: 长度={Length}, TPKT版本={TPKTVersion:X2}, COTP类型={COTPType:X2}", 
                    request.Length, request[0], request[5]);

                // 根据不同的PLC型号和请求类型处理连接
                byte[] response = null;
                
                switch (SiemensVersion)
                {
                    case SiemensVersion.S7_200:
                        Logger?.LogDebug("处理S7_200连接建立请求");
                        response = HandleS7_200ConnectionRequest(request);
                        break;
                        
                    case SiemensVersion.S7_200Smart:
                        Logger?.LogDebug("处理S7_200Smart连接建立请求");
                        response = HandleS7_200SmartConnectionRequest(request);
                        break;
                        
                    case SiemensVersion.S7_300:
                    case SiemensVersion.S7_400:
                        Logger?.LogDebug("处理S7_300/400连接建立请求");
                        response = HandleS7_300_400ConnectionRequest(request);
                        break;
                        
                    case SiemensVersion.S7_1200:
                    case SiemensVersion.S7_1500:
                        Logger?.LogDebug("处理S7_1200/1500连接建立请求");
                        response = HandleS7_1200_1500ConnectionRequest(request);
                        break;
                        
                    default:
                        Logger?.LogWarning("不支持的PLC型号: {SiemensVersion}", SiemensVersion);
                        response = S7ResponseBuilder.CreateErrorResponse(request, 0x01); // 不支持的功能
                        break;
                }

                if (response != null)
                {
                    Logger?.LogDebug("S7连接建立响应已生成，响应长度: {ResponseLength}", response.Length);
                }
                else
                {
                    Logger?.LogWarning("连接建立响应生成失败");
                    response = S7ResponseBuilder.CreateErrorResponse(request, 0x04); // 服务器故障
                }

                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理S7连接建立请求时发生错误");
                return S7ResponseBuilder.CreateErrorResponse(request, 0x04); // 服务器故障
            }
        }

        /// <summary>
        /// 处理S7_200连接建立请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleS7_200ConnectionRequest(byte[] request)
        {
            // S7_200使用简化的连接协议
            return S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion.S7_200, Rack, Slot);
        }

        /// <summary>
        /// 处理S7_200Smart连接建立请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleS7_200SmartConnectionRequest(byte[] request)
        {
            // S7_200Smart使用特定的连接协议
            return S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion.S7_200Smart, Rack, Slot);
        }

        /// <summary>
        /// 处理S7_300/400连接建立请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleS7_300_400ConnectionRequest(byte[] request)
        {
            // S7_300/400使用标准连接协议，需要机架和槽号信息
            return S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion, Rack, Slot);
        }

        /// <summary>
        /// 处理S7_1200/1500连接建立请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        private byte[] HandleS7_1200_1500ConnectionRequest(byte[] request)
        {
            // S7_1200/1500使用标准连接协议，需要机架和槽号信息
            return S7ResponseBuilder.CreateConnectionResponse(request, SiemensVersion, Rack, Slot);
        }
        
        /// <summary>
        /// 处理读取请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        protected virtual byte[] HandleReadRequest(byte[] request)
        {
            try
            {
                Logger?.LogDebug("处理S7读取请求，PLC型号：{SiemensVersion}", SiemensVersion);
                
                // 验证请求数据
                if (request == null || request.Length < 19)
                {
                    Logger?.LogWarning("S7读取请求数据长度不足，实际长度: {Length}", request?.Length ?? 0);
                    return S7ResponseBuilder.CreateErrorResponse(request, 0x01); // 无效请求
                }

                // 记录请求详情
                Logger?.LogDebug("S7读取请求详情: 长度={Length}, TPKT版本={TPKTVersion:X2}, COTP类型={COTPType:X2}", 
                    request.Length, request[0], request[5]);

                // 使用S7ResponseBuilder解析读取请求参数
                var readParameters = S7ResponseBuilder.ParseReadRequest(request);
                if (readParameters.Count == 0)
                {
                    Logger?.LogWarning("S7读取请求参数解析失败");
                    return S7ResponseBuilder.CreateErrorResponse(request, 0x01); // 无效请求
                }

                Logger?.LogDebug("S7读取请求包含 {ItemCount} 个读取项", readParameters.Count);

                // 验证读取参数
                foreach (var parameter in readParameters)
                {
                    if (!ValidateReadParameter(parameter))
                    {
                        Logger?.LogWarning("读取参数验证失败: 区域类型={AreaType}, DB号={DbNumber}, 起始地址={StartAddress}, 长度={Length}",
                            parameter.AreaType, parameter.DbNumber, parameter.StartAddress, parameter.Length);
                        return S7ResponseBuilder.CreateErrorResponse(request, 0x01); // 无效请求
                    }
                }

                // 执行读取操作
                var readData = new List<byte[]>();
                var readResults = new List<bool>();
                var readErrors = new List<string>();
                
                foreach (var parameter in readParameters)
                {
                    try
                    {
                        Logger?.LogDebug("开始读取: 区域类型={AreaType}, DB号={DbNumber}, 起始地址={StartAddress}, 长度={Length}, 位操作={IsBit}",
                            (S7Area)parameter.AreaType, parameter.DbNumber, parameter.StartAddress, parameter.Length, parameter.IsBit);

                        // 确保数据块存在（如果是DB区域）
                        if (parameter.AreaType == (byte)S7Area.DB && parameter.DbNumber > 0)
                        {
                            if (!DataStore.DataBlocks.ContainsKey(parameter.DbNumber))
                            {
                                // 自动创建数据块（默认大小1024字节）
                                DataStore.CreateDataBlock(parameter.DbNumber, 1024);
                                Logger?.LogDebug("自动创建数据块DB{DbNumber}", parameter.DbNumber);
                            }
                        }

                        // 从数据存储中读取数据
                        byte[] data = DataStore.ReadArea((S7Area)parameter.AreaType, parameter.DbNumber, parameter.StartAddress, parameter.Length);
                        
                        // 验证读取的数据
                        if (data == null || data.Length != parameter.Length)
                        {
                            throw new InvalidOperationException($"读取数据长度不匹配，期望: {parameter.Length}，实际: {data?.Length ?? 0}");
                        }
                        
                        readData.Add(data);
                        readResults.Add(true);
                        
                        Logger?.LogDebug("成功读取区域 {AreaType}，DB{DbNumber}，地址 {StartAddress}，长度 {Length}，数据长度 {DataLength}",
                            (S7Area)parameter.AreaType, parameter.DbNumber, parameter.StartAddress, parameter.Length, data.Length);
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = $"读取区域 {(S7Area)parameter.AreaType}，DB{parameter.DbNumber}，地址 {parameter.StartAddress} 失败: {ex.Message}";
                        Logger?.LogError(ex, errorMsg);
                        readData.Add(new byte[parameter.Length]); // 返回零数据而不是null
                        readResults.Add(false);
                        readErrors.Add(errorMsg);
                    }
                }

                // 检查是否有读取失败的项目
                if (readResults.Exists(r => !r))
                {
                    Logger?.LogWarning("部分读取操作失败，成功: {SuccessCount}, 失败: {FailureCount}", 
                        readResults.FindAll(r => r).Count, readResults.FindAll(r => !r).Count);
                    
                    // 记录具体的错误信息
                    foreach (var error in readErrors)
                    {
                        Logger?.LogWarning("读取错误: {Error}", error);
                    }
                }

                // 使用S7ResponseBuilder创建读取响应
                var response = S7ResponseBuilder.CreateReadResponse(request, readData);
                
                if (response != null)
                {
                    Logger?.LogDebug("S7读取响应已生成，响应长度: {ResponseLength}", response.Length);
                }
                else
                {
                    Logger?.LogWarning("读取响应生成失败");
                    response = S7ResponseBuilder.CreateErrorResponse(request, 0x04); // 服务器故障
                }

                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理S7读取请求时发生错误");
                return S7ResponseBuilder.CreateErrorResponse(request, 0x04); // 服务器故障
            }
        }

        /// <summary>
        /// 验证读取参数
        /// </summary>
        /// <param name="parameter">读取参数</param>
        /// <returns>是否有效</returns>
        private bool ValidateReadParameter(S7ReadParameter parameter)
        {
            try
            {
                // 检查区域类型
                if (!Enum.IsDefined(typeof(S7Area), parameter.AreaType))
                {
                    Logger?.LogWarning("无效的区域类型: {AreaType:X2}", parameter.AreaType);
                    return false;
                }

                // 检查地址范围
                if (parameter.StartAddress < 0)
                {
                    Logger?.LogWarning("起始地址不能为负数: {StartAddress}", parameter.StartAddress);
                    return false;
                }

                // 检查长度
                if (parameter.Length <= 0 || parameter.Length > 65535)
                {
                    Logger?.LogWarning("读取长度超出范围: {Length}", parameter.Length);
                    return false;
                }

                // 检查DB号（仅对DB区域）
                if (parameter.AreaType == (byte)S7Area.DB)
                {
                    if (parameter.DbNumber < 0 || parameter.DbNumber > 65535)
                    {
                        Logger?.LogWarning("DB号超出范围: {DbNumber}", parameter.DbNumber);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "验证读取参数时发生错误");
                return false;
            }
        }

        /// <summary>
        /// 验证写入参数
        /// </summary>
        /// <param name="parameter">写入参数</param>
        /// <returns>是否有效</returns>
        private bool ValidateWriteParameter(S7WriteParameter parameter)
        {
            try
            {
                // 检查区域类型
                if (!Enum.IsDefined(typeof(S7Area), parameter.AreaType))
                {
                    Logger?.LogWarning("无效的区域类型: {AreaType:X2}", parameter.AreaType);
                    return false;
                }

                // 检查地址范围
                if (parameter.StartAddress < 0)
                {
                    Logger?.LogWarning("起始地址不能为负数: {StartAddress}", parameter.StartAddress);
                    return false;
                }

                // 检查长度
                if (parameter.Length <= 0 || parameter.Length > 65535)
                {
                    Logger?.LogWarning("写入长度超出范围: {Length}", parameter.Length);
                    return false;
                }

                // 检查数据
                if (parameter.Data == null || parameter.Data.Length != parameter.Length)
                {
                    Logger?.LogWarning("写入数据长度不匹配，期望: {ExpectedLength}，实际: {ActualLength}", 
                        parameter.Length, parameter.Data?.Length ?? 0);
                    return false;
                }

                // 检查DB号（仅对DB区域）
                if (parameter.AreaType == (byte)S7Area.DB)
                {
                    if (parameter.DbNumber < 0 || parameter.DbNumber > 65535)
                    {
                        Logger?.LogWarning("DB号超出范围: {DbNumber}", parameter.DbNumber);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "验证写入参数时发生错误");
                return false;
            }
        }
        
        /// <summary>
        /// 处理写入请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>响应数据</returns>
        protected virtual byte[] HandleWriteRequest(byte[] request)
        {
            try
            {
                Logger?.LogDebug("处理S7写入请求，PLC型号：{SiemensVersion}", SiemensVersion);
                
                // 验证请求数据
                if (request == null || request.Length < 19)
                {
                    Logger?.LogWarning("S7写入请求数据长度不足，实际长度: {Length}", request?.Length ?? 0);
                    return S7ResponseBuilder.CreateErrorResponse(request, 0x01); // 无效请求
                }

                // 记录请求详情
                Logger?.LogDebug("S7写入请求详情: 长度={Length}, TPKT版本={TPKTVersion:X2}, COTP类型={COTPType:X2}", 
                    request.Length, request[0], request[5]);
                
                // 使用S7ResponseBuilder解析写入请求参数
                var writeParameters = S7ResponseBuilder.ParseWriteRequest(request);
                if (writeParameters.Count == 0)
                {
                    Logger?.LogWarning("S7写入请求参数解析失败");
                    return S7ResponseBuilder.CreateErrorResponse(request, 0x01); // 无效请求
                }

                Logger?.LogDebug("S7写入请求包含 {ItemCount} 个写入项", writeParameters.Count);

                // 验证写入参数
                foreach (var parameter in writeParameters)
                {
                    if (!ValidateWriteParameter(parameter))
                    {
                        Logger?.LogWarning("写入参数验证失败: 区域类型={AreaType}, DB号={DbNumber}, 起始地址={StartAddress}, 长度={Length}",
                            parameter.AreaType, parameter.DbNumber, parameter.StartAddress, parameter.Length);
                        return S7ResponseBuilder.CreateErrorResponse(request, 0x01); // 无效请求
                    }
                }

                // 执行写入操作
                var writeResults = new List<bool>();
                var writeErrors = new List<string>();
                
                foreach (var parameter in writeParameters)
                {
                    try
                    {
                        Logger?.LogDebug("开始写入: 区域类型={AreaType}, DB号={DbNumber}, 起始地址={StartAddress}, 长度={Length}, 位操作={IsBit}",
                            (S7Area)parameter.AreaType, parameter.DbNumber, parameter.StartAddress, parameter.Length, parameter.IsBit);

                        // 确保数据块存在（如果是DB区域）
                        if (parameter.AreaType == (byte)S7Area.DB && parameter.DbNumber > 0)
                        {
                            if (!DataStore.DataBlocks.ContainsKey(parameter.DbNumber))
                            {
                                // 自动创建数据块（默认大小1024字节）
                                DataStore.CreateDataBlock(parameter.DbNumber, 1024);
                                Logger?.LogDebug("自动创建数据块DB{DbNumber}", parameter.DbNumber);
                            }
                        }

                        // 验证数据长度
                        if (parameter.Data == null || parameter.Data.Length != parameter.Length)
                        {
                            throw new InvalidOperationException($"写入数据长度不匹配，期望: {parameter.Length}，实际: {parameter.Data?.Length ?? 0}");
                        }

                        // 向数据存储中写入数据
                        DataStore.WriteArea((S7Area)parameter.AreaType, parameter.DbNumber, parameter.StartAddress, parameter.Data);
                        writeResults.Add(true);
                        
                        Logger?.LogDebug("成功写入区域 {AreaType}，DB{DbNumber}，地址 {StartAddress}，长度 {Length}",
                            (S7Area)parameter.AreaType, parameter.DbNumber, parameter.StartAddress, parameter.Data.Length);
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = $"写入区域 {(S7Area)parameter.AreaType}，DB{parameter.DbNumber}，地址 {parameter.StartAddress} 失败: {ex.Message}";
                        Logger?.LogError(ex, errorMsg);
                        writeResults.Add(false); // 标记写入失败
                        writeErrors.Add(errorMsg);
                    }
                }

                // 检查是否有写入失败的项目
                if (writeResults.Exists(r => !r))
                {
                    Logger?.LogWarning("部分写入操作失败，成功: {SuccessCount}, 失败: {FailureCount}", 
                        writeResults.FindAll(r => r).Count, writeResults.FindAll(r => !r).Count);
                    
                    // 记录具体的错误信息
                    foreach (var error in writeErrors)
                    {
                        Logger?.LogWarning("写入错误: {Error}", error);
                    }
                }

                // 使用S7ResponseBuilder创建写入响应
                var response = S7ResponseBuilder.CreateWriteResponse(request, writeResults);
                
                if (response != null)
                {
                    Logger?.LogDebug("S7写入响应已生成，响应长度: {ResponseLength}", response.Length);
                }
                else
                {
                    Logger?.LogWarning("写入响应生成失败");
                    response = S7ResponseBuilder.CreateErrorResponse(request, 0x04); // 服务器故障
                }

                return response;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "处理S7写入请求时发生错误");
                return S7ResponseBuilder.CreateErrorResponse(request, 0x04); // 服务器故障
            }
        }
    }
} 
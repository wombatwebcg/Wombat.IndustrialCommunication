using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS协议通信基类
    /// </summary>
    public class FinsCommunication : DeviceDataReaderWriterBase
    {
        internal AsyncLock _lock = new AsyncLock();
        private static ArrayPool<byte> _byteArrayPool = ArrayPool<byte>.Shared;

        public FinsCommunication(FinsEthernetTransport finsEthernetTransport) : base(finsEthernetTransport)
        {
            DataFormat = EndianFormat.ABCD;
            IsReverse = false;
        }

        public override string Version => "FINS 1.0";

        /// <summary>
        /// 源节点地址
        /// </summary>
        public byte SourceNodeAddress { get; set; } = 0x01;

        /// <summary>
        /// 目标节点地址
        /// </summary>
        public byte DestinationNodeAddress { get; set; } = 0x00;

        /// <summary>
        /// 服务ID
        /// </summary>
        public byte ServiceId { get; set; } = 0x00;

        /// <summary>
        /// 初始化FINS协议连接
        /// </summary>
        /// <param name="connectTimeout">连接超时时间</param>
        /// <returns>初始化结果</returns>
        public async Task<OperationResult> InitAsync(TimeSpan connectTimeout)
        {
            // 连接超时控制，防止死锁
            using (var cts = new CancellationTokenSource(connectTimeout))
            {
                try
                {
                    using (await _lock.LockAsync(cts.Token))
                    {
                        var result = new OperationResult();
                        try
                        {
                            // FINS协议握手命令
                            var handshakeCommand = FinsCommonMethods.BuildHandshakeCommand();
                            
                            result.Requsts.Add(string.Join(" ", handshakeCommand.Select(t => t.ToString("X2"))));
                            
                            // 发送握手命令
                            var handshakeRequestResult = await Transport.SendRequestAsync(handshakeCommand);
                            if (handshakeRequestResult.IsSuccess)
                            {
                                // 接收握手响应头部
                                var responseHeaderResult = await Transport.ReceiveResponseAsync(0, FinsConstants.FINS_HEADER_LENGTH);
                                if (responseHeaderResult.IsSuccess)
                                {
                                    var responseHeader = responseHeaderResult.ResultValue;
                                    
                                    // 获取完整响应长度
                                    var contentLength = FinsCommonMethods.GetContentLength(responseHeader);
                                    if (contentLength > 0)
                                    {
                                        var responseContentResult = await Transport.ReceiveResponseAsync(0, contentLength);
                                        if (!responseContentResult.IsSuccess)
                                        {
                                            return responseContentResult;
                                        }
                                        var responseContent = responseContentResult.ResultValue;
                                        result.Responses.Add(string.Join(" ", responseHeader.Concat(responseContent).Select(t => t.ToString("X2"))));
                                    }
                                    else
                                    {
                                        result.Responses.Add(string.Join(" ", responseHeader.Select(t => t.ToString("X2"))));
                                    }
                                    
                                    // 验证握手响应
                                    if (FinsCommonMethods.ValidateHandshakeResponse(responseHeader))
                                    {
                                        // 提取节点地址信息
                                        if (responseHeader.Length >= 24)
                                        {
                                            SourceNodeAddress = responseHeader[23]; // 客户端节点地址
                                        }
                                        result.IsSuccess = true;
                                        result.Message = "FINS协议初始化成功";
                                    }
                                    else
                                    {
                                        result.IsSuccess = false;
                                        result.Message = "FINS协议握手失败";
                                        result.ErrorCode = 500;
                                    }
                                }
                                else
                                {
                                    return responseHeaderResult;
                                }
                            }
                            else
                            {
                                return handshakeRequestResult;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.IsSuccess = false;
                            result.Message = ex.Message;
                            result.ErrorCode = 408;
                            result.Exception = ex;
                        }
                        return result.Complete();
                    }
                }
                catch (OperationCanceledException)
                {
                    return OperationResult.CreateFailedResult("FINS协议初始化超时");
                }
                catch (Exception ex)
                {
                    return OperationResult.CreateFailedResult($"FINS协议初始化异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 读取数据的内部实现
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length">长度</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isBit">是否为位操作</param>
        /// <returns>读取结果</returns>
        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, DataTypeEnums dataType, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                try
                {
                    // 解析FINS地址
                    var finsAddress = new FinsAddress(address);
                    if (!finsAddress.IsValid)
                    {
                        return OperationResult.CreateFailedResult<byte[]>("无效的FINS地址格式");
                    }

                    // 创建读取请求
                    var readRequest = new FinsReadRequest(address, (ushort)length, dataType, ServiceId);
                    var requestFrame = readRequest.ProtocolMessageFrame;

                    // 发送读取请求
                    var sendResult = await Transport.SendRequestAsync(requestFrame);
                    if (!sendResult.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult<byte[]>($"发送读取请求失败: {sendResult.Message}");
                    }

                    // 接收响应头部
                    var responseHeaderResult = await Transport.ReceiveResponseAsync(0, FinsConstants.FINS_HEADER_LENGTH);
                    if (!responseHeaderResult.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult<byte[]>($"接收响应头部失败: {responseHeaderResult.Message}");
                    }

                    var responseHeader = responseHeaderResult.ResultValue;
                    
                    // 获取响应数据长度
                    var contentLength = FinsCommonMethods.GetContentLength(responseHeader);
                    if (contentLength > 0)
                    {
                        var responseContentResult = await Transport.ReceiveResponseAsync(0, contentLength);
                        if (!responseContentResult.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult<byte[]>($"接收响应内容失败: {responseContentResult.Message}");
                        }
                        
                        var fullResponse = responseHeader.Concat(responseContentResult.ResultValue).ToArray();
                        
                        // 解析响应
                        var readResponse = new FinsReadResponse(fullResponse);
                        if (!readResponse.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult<byte[]>(readResponse.ErrorMessage);
                        }

                        return OperationResult.CreateSuccessResult<byte[]>(readResponse.Data);
                    }
                    else
                    {
                        // 只有头部，检查错误码
                        var readResponse = new FinsReadResponse(responseHeader);
                        if (!readResponse.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult<byte[]>(readResponse.ErrorMessage);
                        }
                        
                        return OperationResult.CreateSuccessResult<byte[]>(new byte[0]);
                    }
                }
                catch (Exception ex)
                {
                    return OperationResult.CreateFailedResult<byte[]>($"读取操作异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 写入数据的内部实现
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="data">数据</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isBit">是否为位操作</param>
        /// <returns>写入结果</returns>
        internal override async Task<OperationResult> WriteAsync(string address, byte[] data, DataTypeEnums dataType, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                try
                {
                    // 解析FINS地址
                    var finsAddress = new FinsAddress(address);
                    if (!finsAddress.IsValid)
                    {
                        return OperationResult.CreateFailedResult("无效的FINS地址格式");
                    }

                    // 创建写入请求
                    var writeRequest = new FinsWriteRequest(address, data, dataType, ServiceId);
                    var requestFrame = writeRequest.ProtocolMessageFrame;

                    // 发送写入请求
                    var sendResult = await Transport.SendRequestAsync(requestFrame);
                    if (!sendResult.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult($"发送写入请求失败: {sendResult.Message}");
                    }

                    // 接收响应头部
                    var responseHeaderResult = await Transport.ReceiveResponseAsync(0, FinsConstants.FINS_HEADER_LENGTH);
                    if (!responseHeaderResult.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult($"接收响应头部失败: {responseHeaderResult.Message}");
                    }

                    var responseHeader = responseHeaderResult.ResultValue;
                    
                    // 获取响应数据长度
                    var contentLength = FinsCommonMethods.GetContentLength(responseHeader);
                    if (contentLength > 0)
                    {
                        var responseContentResult = await Transport.ReceiveResponseAsync(0, contentLength);
                        if (!responseContentResult.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult($"接收响应内容失败: {responseContentResult.Message}");
                        }
                        
                        var fullResponse = responseHeader.Concat(responseContentResult.ResultValue).ToArray();
                        
                        // 解析响应
                        var writeResponse = new FinsWriteResponse(fullResponse);
                        if (!writeResponse.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult(writeResponse.ErrorMessage);
                        }

                        return OperationResult.CreateSuccessResult();
                    }
                    else
                    {
                        // 只有头部，检查错误码
                        var writeResponse = new FinsWriteResponse(responseHeader);
                        if (!writeResponse.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult(writeResponse.ErrorMessage);
                        }
                        
                        return OperationResult.CreateSuccessResult();
                    }
                }
                catch (Exception ex)
                {
                    return OperationResult.CreateFailedResult($"写入操作异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 写入字符串数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">字符串值</param>
        /// <param name="encoding">编码方式</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> WriteStringAsync(string address, string value, System.Text.Encoding encoding = null)
        {
            if (encoding == null)
                encoding = System.Text.Encoding.UTF8;

            var data = encoding.GetBytes(value);
            return await WriteAsync(address, data);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _lock?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
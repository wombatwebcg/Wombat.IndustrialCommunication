using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS以太网通信协议的传输层实现。
    /// 处理客户端与FINS PLC设备之间的通信。
    /// </summary>
    public class FinsEthernetTransport : DeviceMessageTransport
    {
        /// <summary>
        /// 初始化FinsEthernetTransport类的新实例。
        /// </summary>
        /// <param name="streamResource">用于通信的流资源。</param>
        /// <exception cref="ArgumentNullException">当streamResource为null时抛出。</exception>
        public FinsEthernetTransport(IStreamResource streamResource) : base(streamResource)
        {
            if (streamResource == null)
                throw new ArgumentNullException(nameof(streamResource));
        }

        /// <summary>
        /// 异步执行单播读取消息操作。
        /// </summary>
        /// <param name="message">要发送的读取请求消息。</param>
        /// <returns>包含读取响应的任务。</returns>
        /// <exception cref="ArgumentException">当消息不是FinsReadRequest类型时抛出。</exception>
        /// <exception cref="InvalidOperationException">当通信过程中发生错误时抛出。</exception>
        public override async Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage message)
        {
            if (!(message is FinsReadRequest request))
                throw new ArgumentException("消息必须是FinsReadRequest类型", nameof(message));

            try
            {
                Console.WriteLine("[FINS读取调试] 开始执行读取操作");
                Console.WriteLine($"[FINS读取调试] 请求参数: RegisterCount={request.RegisterCount}, DataType={request.DataType}, Length={request.Length}");
                Console.WriteLine($"[FINS读取调试] 请求命令: {string.Join(" ", request.ProtocolMessageFrame.Select(b => b.ToString("X2")))}");

                // 发送请求
                Console.WriteLine("[FINS读取调试] 开始发送读取请求");
                var sendResult = await SendRequestAsync(request.ProtocolMessageFrame);
                if (!sendResult.IsSuccess)
                {
                    Console.WriteLine($"[FINS读取调试] 发送请求失败: {sendResult.Message}");
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"发送FINS读取请求失败: {sendResult.Message}");
                }
                Console.WriteLine("[FINS读取调试] 请求发送成功");

                // 接收FINS响应头部 (10字节)
                Console.WriteLine("[FINS读取调试] 开始接收响应头部 (10字节)");
                var headerResult = await ReceiveResponseAsync(0, FinsConstants.FINS_HEADER_LENGTH);
                if (!headerResult.IsSuccess)
                {
                    Console.WriteLine($"[FINS读取调试] 接收头部失败: {headerResult.Message}");
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"接收FINS读取响应头部失败: {headerResult.Message}");
                }
                var headerBuffer = headerResult.ResultValue;
                Console.WriteLine($"[FINS读取调试] 接收到头部: {string.Join(" ", headerBuffer.Select(b => b.ToString("X2")))}");

                // 接收响应码部分 (MRC, SRC) - 2字节
                var responseCodeLength = FinsCommonMethods.GetContentLength(headerBuffer);
                Console.WriteLine($"[FINS读取调试] 开始接收响应码 ({responseCodeLength}字节)");
                var responseCodeResult = await ReceiveResponseAsync(0, responseCodeLength);
                if (!responseCodeResult.IsSuccess)
                {
                    Console.WriteLine($"[FINS读取调试] 接收响应码失败: {responseCodeResult.Message}");
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"接收FINS读取响应码失败: {responseCodeResult.Message}");
                }
                Console.WriteLine($"[FINS读取调试] 接收到响应码: {string.Join(" ", responseCodeResult.ResultValue.Select(b => b.ToString("X2")))}");

                // 合并头部和响应码
                var headerAndCode = headerBuffer.Concat(responseCodeResult.ResultValue).ToArray();
                Console.WriteLine($"[FINS读取调试] 合并头部和响应码: {string.Join(" ", headerAndCode.Select(b => b.ToString("X2")))}");
                
                // 根据响应码确定数据长度
                var dataLength = FinsCommonMethods.GetDataLength(headerAndCode, request.RegisterCount, request.DataType);
                Console.WriteLine($"[FINS读取调试] 计算数据长度: {dataLength}字节");
                
                byte[] fullResponse;
                if (dataLength > 0)
                {
                    // 接收数据部分
                    Console.WriteLine($"[FINS读取调试] 开始接收数据部分 ({dataLength}字节)");
                    var dataResult = await ReceiveResponseAsync(0, dataLength);
                    if (!dataResult.IsSuccess)
                    {
                        Console.WriteLine($"[FINS读取调试] 接收数据失败: {dataResult.Message}");
                        return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"接收FINS读取响应数据失败: {dataResult.Message}");
                    }
                    Console.WriteLine($"[FINS读取调试] 接收到数据: {string.Join(" ", dataResult.ResultValue.Select(b => b.ToString("X2")))}");
                    
                    fullResponse = headerAndCode.Concat(dataResult.ResultValue).ToArray();
                }
                else
                {
                    // 没有数据部分，只有头部和响应码
                    Console.WriteLine("[FINS读取调试] 无数据部分，只有头部和响应码");
                    fullResponse = headerAndCode;
                }

                Console.WriteLine($"[FINS读取调试] 完整响应: {string.Join(" ", fullResponse.Select(b => b.ToString("X2")))}");
                Console.WriteLine("[FINS读取调试] 读取操作完成");
                return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(new FinsReadResponse(fullResponse));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FINS读取调试] 读取操作异常: {ex.Message}");
                Console.WriteLine($"[FINS读取调试] 异常堆栈: {ex.StackTrace}");
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"FINS读取操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步执行单播写入消息操作。
        /// </summary>
        /// <param name="message">要发送的写入请求消息。</param>
        /// <returns>包含写入响应的任务。</returns>
        /// <exception cref="ArgumentException">当消息不是FinsWriteRequest类型时抛出。</exception>
        /// <exception cref="InvalidOperationException">当通信过程中发生错误时抛出。</exception>
        public override async Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage message)
        {
            if (!(message is FinsWriteRequest request))
                throw new ArgumentException("消息必须是FinsWriteRequest类型", nameof(message));

            try
            {
                // 发送请求
                var sendResult = await SendRequestAsync(request.ProtocolMessageFrame);
                if (!sendResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"发送FINS写入请求失败: {sendResult.Message}");
                }

                // 接收FINS响应头部 (10字节)
                var headerResult = await ReceiveResponseAsync(0, FinsConstants.FINS_HEADER_LENGTH);
                if (!headerResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"接收FINS写入响应头部失败: {headerResult.Message}");
                }
                var headerBuffer = headerResult.ResultValue;

                // 接收响应码部分 (MRC, SRC) - 2字节
                var responseCodeLength = FinsCommonMethods.GetContentLength(headerBuffer);
                var responseCodeResult = await ReceiveResponseAsync(0, responseCodeLength);
                if (!responseCodeResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"接收FINS写入响应码失败: {responseCodeResult.Message}");
                }

                // 合并头部和响应码 (写入响应通常没有数据部分)
                var fullResponse = headerBuffer.Concat(responseCodeResult.ResultValue).ToArray();

                return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(new FinsWriteResponse(fullResponse));
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"FINS写入操作失败: {ex.Message}");
            }
        }


    }
}
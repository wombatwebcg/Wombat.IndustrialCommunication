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
                // 发送请求
                var sendResult = await SendRequestAsync(request.ProtocolMessageFrame);
                if (!sendResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"发送FINS读取请求失败: {sendResult.Message}");
                }

                // 接收响应头部
                var headerResult = await ReceiveResponseAsync(0, 16); // FINS响应头部长度
                if (!headerResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"接收FINS读取响应头部失败: {headerResult.Message}");
                }
                var headerBuffer = headerResult.ResultValue;

                // 解析响应长度
                var responseLength = BitConverter.ToInt32(headerBuffer, 4);
                if (responseLength > headerBuffer.Length)
                {
                    // 接收剩余数据
                    var remainingLength = responseLength - headerBuffer.Length;
                    var dataResult = await ReceiveResponseAsync(0, remainingLength);
                    if (!dataResult.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"接收FINS读取响应数据失败: {dataResult.Message}");
                    }
                    var dataBuffer = dataResult.ResultValue;

                    // 合并头部和数据
                    var fullResponse = new byte[responseLength];
                    Array.Copy(headerBuffer, 0, fullResponse, 0, headerBuffer.Length);
                    Array.Copy(dataBuffer, 0, fullResponse, headerBuffer.Length, dataBuffer.Length);

                    return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(new FinsReadResponse(fullResponse));
                }
                else
                {
                    return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(new FinsReadResponse(headerBuffer.Take(responseLength).ToArray()));
                }
            }
            catch (Exception ex)
            {
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

                // 接收响应头部
                var headerResult = await ReceiveResponseAsync(0, 16); // FINS响应头部长度
                if (!headerResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"接收FINS写入响应头部失败: {headerResult.Message}");
                }
                var headerBuffer = headerResult.ResultValue;

                // 解析响应长度
                var responseLength = BitConverter.ToInt32(headerBuffer, 4);
                if (responseLength > headerBuffer.Length)
                {
                    // 接收剩余数据
                    var remainingLength = responseLength - headerBuffer.Length;
                    var dataResult = await ReceiveResponseAsync(0, remainingLength);
                    if (!dataResult.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"接收FINS写入响应数据失败: {dataResult.Message}");
                    }
                    var dataBuffer = dataResult.ResultValue;

                    // 合并头部和数据
                    var fullResponse = new byte[responseLength];
                    Array.Copy(headerBuffer, 0, fullResponse, 0, headerBuffer.Length);
                    Array.Copy(dataBuffer, 0, fullResponse, headerBuffer.Length, dataBuffer.Length);

                    return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(new FinsWriteResponse(fullResponse));
                }
                else
                {
                    return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(new FinsWriteResponse(headerBuffer.Take(responseLength).ToArray()));
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"FINS写入操作失败: {ex.Message}");
            }
        }


    }
}
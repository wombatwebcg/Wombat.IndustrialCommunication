using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// S7以太网通信协议的传输层实现。
    /// 处理客户端与S7 PLC设备之间的通信。
    /// </summary>
    public class S7EthernetTransport : DeviceMessageTransport
    {
        /// <summary>
        /// 初始化S7EthernetTransport类的新实例。
        /// </summary>
        /// <param name="streamResource">用于通信的流资源。</param>
        /// <exception cref="ArgumentNullException">当streamResource为null时抛出。</exception>
        public S7EthernetTransport(IStreamResource streamResource) : base(streamResource)
        {
            if (streamResource == null)
                throw new ArgumentNullException(nameof(streamResource));
        }

        /// <summary>
        /// 向S7 PLC设备发送读取请求并处理响应。
        /// </summary>
        /// <param name="request">读取请求消息。</param>
        /// <returns>包含读取响应消息的操作结果。</returns>
        public override async Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var result = new OperationResult<S7ReadResponse>();
            
            if (!(request is S7ReadRequest s7ReadRequest))
            {
                result.IsSuccess = false;
                result.Message = "无效的请求类型。期望S7ReadRequest类型。";
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
            }

            try
            {
                // 发送初始请求
                var commandRequest = await SendRequestAsync(s7ReadRequest.ProtocolMessageFrame);
                result.Requsts.Add(string.Join(" ", s7ReadRequest.ProtocolMessageFrame.Select(t => t.ToString("X2"))));

                if (!commandRequest.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
                }

                // 接收并处理头部数据包
                var headerResponse = await ReceiveResponseAsync(0, request.ProtocolResponseLength);
                if (!headerResponse.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
                }
                result.Responses.Add(string.Join(" ", headerResponse.ResultValue.Select(t => t.ToString("X2"))));

                // 接收并处理数据包
                var dataLength = S7CommonMethods.GetContentLength(headerResponse.ResultValue);
                var dataResponse = await ReceiveResponseAsync(0, dataLength);
                if (!dataResponse.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
                }
                result.Responses.Add(string.Join(" ", dataResponse.ResultValue.Select(t => t.ToString("X2"))));

                // 创建并配置响应
                result.ResultValue = new S7ReadResponse(dataResponse.ResultValue)
                {
                    RegisterAddress = request.RegisterAddress,
                    RegisterCount = request.RegisterCount
                };

                return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result, result.ResultValue);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = $"读取操作过程中发生错误: {ex.Message}";
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
            }
        }

        /// <summary>
        /// 向S7 PLC设备发送写入请求并处理响应。
        /// </summary>
        /// <param name="request">写入请求消息。</param>
        /// <returns>包含写入响应消息的操作结果。</returns>
        public override async Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var result = new OperationResult<S7WriteResponse>();
            
            if (!(request is S7WriteRequest s7WriteRequest))
            {
                result.IsSuccess = false;
                result.Message = "无效的请求类型。期望S7WriteRequest类型。";
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
            }

            try
            {
                // 发送初始请求
                var commandRequest = await SendRequestAsync(s7WriteRequest.ProtocolMessageFrame);
                result.Requsts.Add(string.Join(" ", s7WriteRequest.ProtocolMessageFrame.Select(t => t.ToString("X2"))));

                if (!commandRequest.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
                }

                // 接收并处理头部数据包
                var headerResponse = await ReceiveResponseAsync(0, request.ProtocolResponseLength);
                if (!headerResponse.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
                }

                // 接收并处理数据包
                var dataLength = S7CommonMethods.GetContentLength(headerResponse.ResultValue);
                var dataResponse = await ReceiveResponseAsync(0, dataLength);
                if (!dataResponse.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
                }
                result.Responses.Add(string.Join(" ", dataResponse.ResultValue.Select(t => t.ToString("X2"))));

                // 创建并配置响应
                result.ResultValue = new S7WriteResponse(dataResponse.ResultValue)
                {
                    RegisterAddress = request.RegisterAddress,
                    RegisterCount = request.RegisterCount
                };

                return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result, result.ResultValue);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = $"写入操作过程中发生错误: {ex.Message}";
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
            }
        }
    }
}

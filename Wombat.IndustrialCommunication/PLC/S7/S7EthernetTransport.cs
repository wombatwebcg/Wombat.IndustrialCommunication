using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// S7以太网通信协议的传输层实现。
    /// </summary>
    public class S7EthernetTransport : DeviceMessageTransport
    {
        public bool StrictPduReferenceValidation { get; set; } = true;

        public S7EthernetTransport(IStreamResource streamResource) : base(streamResource)
        {
            if (streamResource == null)
            {
                throw new ArgumentNullException(nameof(streamResource));
            }
        }

        public override Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage request)
        {
            return UnicastReadMessageAsync(request, CancellationToken.None);
        }

        public override async Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var result = new OperationResult<S7ReadResponse>();
            if (!(request is S7ReadRequest s7ReadRequest))
            {
                result.IsSuccess = false;
                result.Message = "无效的请求类型。期望S7ReadRequest类型。";
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
            }

            try
            {
                result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                var commandRequest = await SendRequestAsync(request.ProtocolMessageFrame, cancellationToken);
                if (!commandRequest.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(commandRequest);
                }

                var receiveResult = await ReceiveFullResponseAsync(request.ProtocolResponseLength, result, cancellationToken).ConfigureAwait(false);
                if (!receiveResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(receiveResult);
                }

                var fullResponse = receiveResult.ResultValue;
                var pduValidation = ValidateResponsePduReference(fullResponse, s7ReadRequest.PduReference);
                if (StrictPduReferenceValidation && !pduValidation.IsSuccess)
                {
                    result.IsSuccess = false;
                    result.Message = pduValidation.Message;
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(result);
                }

                result.ResultValue = new S7ReadResponse(fullResponse)
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

        public override Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage request)
        {
            return UnicastWriteMessageAsync(request, CancellationToken.None);
        }

        public override async Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var result = new OperationResult<S7WriteResponse>();
            var requestSent = false;
            if (!(request is S7WriteRequest s7WriteRequest))
            {
                result.IsSuccess = false;
                result.Message = "无效的请求类型。期望S7WriteRequest类型。";
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
            }

            try
            {
                result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                var commandRequest = await SendRequestAsync(request.ProtocolMessageFrame, cancellationToken);
                if (!commandRequest.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(commandRequest);
                }
                requestSent = true;

                var receiveResult = await ReceiveFullResponseAsync(request.ProtocolResponseLength, result, cancellationToken).ConfigureAwait(false);
                if (!receiveResult.IsSuccess)
                {
                    var failed = OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(receiveResult);
                    failed.FailureKind = OperationFailureKind.OutcomeUnknown;
                    return failed;
                }

                var fullResponse = receiveResult.ResultValue;
                var pduValidation = ValidateResponsePduReference(fullResponse, s7WriteRequest.PduReference);
                if (StrictPduReferenceValidation && !pduValidation.IsSuccess)
                {
                    result.IsSuccess = false;
                    result.FailureKind = OperationFailureKind.OutcomeUnknown;
                    result.Message = pduValidation.Message;
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(result);
                }

                result.ResultValue = new S7WriteResponse(fullResponse)
                {
                    RegisterAddress = request.RegisterAddress,
                    RegisterCount = request.RegisterCount
                };

                return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result, result.ResultValue);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                if (requestSent) result.FailureKind = OperationFailureKind.OutcomeUnknown;
                result.Message = $"写入操作过程中发生错误: {ex.Message}";
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(result);
            }
        }

        private async Task<OperationResult<byte[]>> ReceiveFullResponseAsync(int headerLength, OperationResult result, CancellationToken cancellationToken)
        {
            var headerResponse = await ReceiveResponseAsync(0, headerLength, cancellationToken).ConfigureAwait(false);
            if (!headerResponse.IsSuccess)
            {
                return OperationResult.CreateFailedResult<byte[]>(headerResponse);
            }

            result.Responses.Add(string.Join(" ", headerResponse.ResultValue.Select(t => t.ToString("X2"))));

            var dataLength = S7CommonMethods.GetContentLength(headerResponse.ResultValue);
            var dataResponse = await ReceiveResponseAsync(0, dataLength, cancellationToken).ConfigureAwait(false);
            if (!dataResponse.IsSuccess)
            {
                return OperationResult.CreateFailedResult<byte[]>(dataResponse);
            }

            result.Responses.Add(string.Join(" ", dataResponse.ResultValue.Select(t => t.ToString("X2"))));

            var fullResponse = new byte[headerResponse.ResultValue.Length + dataResponse.ResultValue.Length];
            Buffer.BlockCopy(headerResponse.ResultValue, 0, fullResponse, 0, headerResponse.ResultValue.Length);
            Buffer.BlockCopy(dataResponse.ResultValue, 0, fullResponse, headerResponse.ResultValue.Length, dataResponse.ResultValue.Length);
            return OperationResult.CreateSuccessResult(fullResponse);
        }

        private static OperationResult ValidateResponsePduReference(byte[] response, ushort requestPduReference)
        {
            if (response == null || response.Length < 5)
            {
                return OperationResult.CreateFailedResult("响应长度不足");
            }

            var cotpTotalLength = 1 + response[4];
            var s7Offset = 4 + cotpTotalLength;
            if (s7Offset + 12 > response.Length)
            {
                return OperationResult.CreateFailedResult("S7响应头长度不足");
            }

            var responsePduReference = (ushort)((response[s7Offset + 4] << 8) | response[s7Offset + 5]);
            if (responsePduReference != requestPduReference)
            {
                return OperationResult.CreateFailedResult($"S7响应PDU Reference不匹配，请求:{requestPduReference} 响应:{responsePduReference}");
            }

            return OperationResult.CreateSuccessResult();
        }
    }
}

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
    public class S7EthernetTransport : DeviceMessageTransport
    {
        public S7EthernetTransport(IStreamResource streamResource):base(streamResource)
        {

        }

        public override async Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage request)
        {
            OperationResult<S7ReadResponse> result = new OperationResult<S7ReadResponse>();
            if (request is S7ReadRequest s7ReadRequest)
            {
                try
                {
                    var commandRequest1 = await SendRequestAsync(s7ReadRequest.ProtocolMessageFrame);
                    result.Requsts.Add(string.Join(" ", s7ReadRequest.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                    if (commandRequest1.IsSuccess)
                    {
                        var response1Result = await ReceiveResponseAsync(0, request.ProtocolResponseLength);
                        if (!response1Result.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
                        }
                        result.Responses.Add(string.Join(" ", response1Result.ResultValue.Select(t => t.ToString("X2"))));
                        var headPackage = response1Result.ResultValue;
                        var response2Result = await ReceiveResponseAsync(0, S7CommonMethods.GetContentLength(headPackage));
                        if (!response2Result.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();

                        }
                        result.Responses.Add(string.Join(" ", response2Result.ResultValue.Select(t => t.ToString("X2"))));
                        var dataPackage = response2Result.ResultValue;
                        result.ResultValue = new S7ReadResponse(dataPackage);
                        result.ResultValue.RegisterAddress = request.RegisterAddress;
                        result.ResultValue.RegisterCount = request.RegisterCount;

                        return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result,result.ResultValue);
                    }
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
            }
            return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
        }


        public override async Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage request)
        {
            OperationResult<S7WriteResponse> result = new OperationResult<S7WriteResponse>();
            if (request is S7WriteRequest s7WriteRequest)
            {
                try
                {
                    var commandRequest1 = await SendRequestAsync(s7WriteRequest.ProtocolMessageFrame);
                    result.Requsts.Add(string.Join(" ", s7WriteRequest.ProtocolMessageFrame.Select(t => t.ToString("X2"))));

                    if (commandRequest1.IsSuccess)
                    {
                        var response1Result = await ReceiveResponseAsync(0, request.ProtocolResponseLength);
                        if (!response1Result.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
                        }
                        var headPackage = response1Result.ResultValue;
                        var response2Result = await ReceiveResponseAsync(0, S7CommonMethods.GetContentLength(headPackage));
                        if (!response2Result.IsSuccess)
                        {
                            return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();

                        }
                        result.Responses.Add(string.Join(" ", response2Result.ResultValue.Select(t => t.ToString("X2"))));
                        var dataPackage = response2Result.ResultValue;
                        result.ResultValue = new S7WriteResponse(dataPackage);
                        result.ResultValue.RegisterAddress = request.RegisterAddress;
                        result.ResultValue.RegisterCount = request.RegisterCount;

                        return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result,result.ResultValue);

                    }
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
            }
            return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
        }



    }
}

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.PLC
{
    public class S7Communication : DeviceDataReaderWriterBase
    {

        private AsyncLock _lock = new AsyncLock();


        private static ArrayPool<byte> _byteArrayPool = ArrayPool<byte>.Shared;

        public S7Communication(S7EthernetTransport s7EthernetTransport) :base(s7EthernetTransport)
        {
            DataFormat = Extensions.DataTypeExtensions.EndianFormat.CDAB;
            IsReverse = true;
        }


        public override string Version => SiemensVersion.ToString();

        /// <summary>
        /// 插槽号 
        /// </summary>
        public byte Slot { get; set; }

        /// <summary>
        /// 机架号
        /// </summary>
        public byte Rack { get;set; }


        public SiemensVersion SiemensVersion{ get; set; }

        public async Task<OperationResult> InitAsync()
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                try
                {
                    var command1 = SiemensConstant.Command1;
                    var command2 = SiemensConstant.Command2;

                    switch (SiemensVersion)
                    {
                        case SiemensVersion.S7_200:
                            command1 = SiemensConstant.Command1_200;
                            command2 = SiemensConstant.Command2_200;
                            break;
                        case SiemensVersion.S7_200Smart:
                            command1 = SiemensConstant.Command1_200Smart;
                            command2 = SiemensConstant.Command2_200Smart;
                            break;
                        case SiemensVersion.S7_300:
                            command1[21] = (byte)((Rack * 0x20) + Slot); //0x02;
                            break;
                        case SiemensVersion.S7_400:
                            command1[21] = (byte)((Rack * 0x20) + Slot); //0x03;
                            command1[17] = 0x00;
                            break;
                        case SiemensVersion.S7_1200:
                            command1[21] = (byte)((Rack * 0x20) + Slot); //0x00;
                            break;
                        case SiemensVersion.S7_1500:
                            command1[21] = (byte)((Rack * 0x20) + Slot); //0x00;
                            break;
                        default:
                            command1[18] = 0x00;
                            break;
                    }

                    result.Requsts.Add(string.Join(" ", command1.Select(t => t.ToString("X2"))));
                    var command1RequestResult = await Transport.SendRequestAsync(command1);
                    if (command1RequestResult.IsSuccess)
                    {
                        var response1Result = await Transport.ReceiveResponseAsync(0, SiemensConstant.InitHeadLength);
                        if (response1Result.IsSuccess)
                        {
                            var response1 = response1Result.ResultValue;
                            var response2Result = await Transport.ReceiveResponseAsync(0, S7CommonMethods.GetContentLength(response1));
                            if (!response2Result.IsSuccess)
                            {
                                return response2Result;

                            }
                            var response2 = response1Result.ResultValue;
                            result.Responses.Add(string.Join(" ", response1.Concat(response2).Select(t => t.ToString("X2"))));

                        }

                    }

                    result.Requsts.Add(string.Join(" ", command2.Select(t => t.ToString("X2"))));
                    //第二次初始化指令交互
                    var command2RequestResult = await Transport.SendRequestAsync(command2);
                    if (command2RequestResult.IsSuccess)
                    {
                        var response3Result = await Transport.ReceiveResponseAsync(0, SiemensConstant.InitHeadLength);
                        if (!response3Result.IsSuccess)
                        {
                            return response3Result;

                        }
                        var response3 = response3Result.ResultValue;

                        var response4Result = await Transport.ReceiveResponseAsync(0, S7CommonMethods.GetContentLength(response3));
                        if (!response4Result.IsSuccess)
                        {
                            return response4Result;

                        }
                        var response4 = response4Result.ResultValue;
                        result.Responses.Add(string.Join(" ", response3.Concat(response4).Select(t => t.ToString("X2"))));
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


        internal override  async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                if (Transport is S7EthernetTransport s7Transport)
                {
                    int maxCount = 200;
                    if (length > maxCount)
                    {
                        int alreadyFinished = 0;
                        List<byte> bytesContent = new List<byte>();
                        while (alreadyFinished < length)
                        {
                            ushort readLength = (ushort)Math.Min(length - alreadyFinished, maxCount);

                            var tempResult = await internalReadAsync(s7Transport, address,alreadyFinished, readLength, isBit);
                            if (tempResult.IsSuccess)
                            {
                                result.Requsts.Add(tempResult.Requsts[0]);
                                result.Responses.Add(tempResult.Responses[0]);
                                bytesContent.AddRange(tempResult.ResultValue);
                                alreadyFinished += readLength;
                            }
                        }

                        result.ResultValue = bytesContent.ToArray();
                        return result.Complete();


                    }
                    else
                    {
                        return await internalReadAsync(s7Transport, address,0, length, isBit);
                    }

                }
                return OperationResult.CreateFailedResult<byte[]>();
            }

            async ValueTask<OperationResult<byte[]>> internalReadAsync(S7EthernetTransport transport,string internalAddress,int internalOffest, int internalLength, bool internalIsBit = false)
            {
                var tempResult = new OperationResult<byte>();
                var readRequest = new S7ReadRequest(internalAddress, internalOffest, internalLength, isBit);
                var response = await transport.UnicastReadMessageAsync(readRequest);
                if (response.IsSuccess)
                {
                    int realLength = internalLength;
                    var dataPackage = response.ResultValue.ProtocolMessageFrame;
                    byte[] responseData = new byte[realLength];
                    try
                    {
                        //0x04 读 0x01 读取一个长度 //如果是批量读取，批量读取方法里面有验证
                        if (dataPackage[19] == 0x04 && dataPackage[20] == 0x01)
                        {
                            if (dataPackage[21] == 0x0A && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取{address}失败，请确认是否存在地址{address}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] == 0x05 && dataPackage[22] == 0x00)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取{address}失败，请确认是否存在地址{address}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                            else if (dataPackage[21] != 0xFF)
                            {
                                tempResult.IsSuccess = false;
                                tempResult.Message = $"读取{address}失败，异常代码[{21}]:{dataPackage[21]}";
                                return OperationResult.CreateFailedResult<byte[]>(tempResult);
                            }
                        }
                        if (internalIsBit) { realLength = (int)(Math.Ceiling(realLength / 8.0)); }
                        Array.Copy(dataPackage, dataPackage.Length - realLength, responseData, 0, realLength);
                    }
                    catch (Exception ex)
                    {
                        tempResult.Exception = ex;
                        tempResult.Message = $"{internalAddress} {internalOffest} {internalLength} 读取预期长度与返回数据长度不一致";
                        return OperationResult.CreateFailedResult<byte[]>(tempResult);
                    }
                    return new OperationResult<byte[]>(response, responseData).Complete();
                }
                else
                {

                    return OperationResult.CreateFailedResult<byte[]>(response);
                }
            }

        }

        internal override async Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                OperationResult<byte> result = new OperationResult<byte>();
                if (Transport is S7EthernetTransport s7Transport)
                {
                    var writeRequest = new S7WriteRequest(address,0,data, isBit);
                    var response = await s7Transport.UnicastWriteMessageAsync(writeRequest);
                    if (response.IsSuccess)
                    {
                        var dataPackage = response.ResultValue.ProtocolMessageFrame;
                        var offset = dataPackage.Length - 1;
                        if (dataPackage[offset] == 0x0A)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入{address}失败，请确认是否存在地址{address}，异常代码[{offset}]:{dataPackage[offset]}";
                        }
                        else if (dataPackage[offset] == 0x05)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入{address}失败，请确认是否存在地址{address}，异常代码[{offset}]:{dataPackage[offset]}";
                        }
                        else if (dataPackage[offset] != 0xFF)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入{address}失败，异常代码[{offset}]:{dataPackage[offset]}";
                        }
                        return OperationResult.CreateSuccessResult(response);
                    }
                    else
                    {

                        return OperationResult.CreateFailedResult(response);
                    }
                }
                return OperationResult.CreateFailedResult();
            }
        }

    }
}

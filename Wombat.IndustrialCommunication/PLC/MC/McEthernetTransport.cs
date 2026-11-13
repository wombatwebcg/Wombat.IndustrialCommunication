using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.Abstractions;
using Wombat.IndustrialCommunication.Messaging;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 以太网传输层：按帧前缀分帧读取、结束码提前返回、4E 序列号自增与回显校验。
    /// 三种帧的分帧策略见实施计划 D1，客户端与服务端共用同一套编解码。
    /// </summary>
    public sealed class McEthernetTransport : DeviceMessageTransport
    {
        private int _serialNumber;

        /// <summary>构造传输层</summary>
        /// <param name="streamResource">流资源（TCP）</param>
        /// <param name="frameType">帧类型，决定前缀长度与序列号行为</param>
        public McEthernetTransport(IStreamResource streamResource, McFrameType frameType) : base(streamResource)
        {
            if (streamResource == null)
                throw new ArgumentNullException(nameof(streamResource));

            FrameType = frameType;
        }

        /// <summary>帧类型</summary>
        public McFrameType FrameType { get; }

        /// <inheritdoc />
        public override Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(
            IDeviceReadWriteMessage request, CancellationToken cancellationToken)
        {
            return ExchangeAsync(request, false, cancellationToken);
        }

        /// <inheritdoc />
        public override Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(
            IDeviceReadWriteMessage request, CancellationToken cancellationToken)
        {
            return ExchangeAsync(request, true, cancellationToken);
        }

        private async Task<OperationResult<IDeviceReadWriteMessage>> ExchangeAsync(
            IDeviceReadWriteMessage request, bool isWrite, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var trace = new OperationResult();
            byte[] frame = (byte[])request.ProtocolMessageFrame.Clone();

            ushort? expectedSerialNumber = null;
            if (FrameType == McFrameType.Q4E)
            {
                expectedSerialNumber = NextSerialNumber();
                McCommonMethods.WriteUInt16(frame, 2, expectedSerialNumber.Value);
            }

            trace.Requsts.Add(ToHex(frame));

            var sendResult = await SendRequestAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!sendResult.IsSuccess)
                return Failure(sendResult, trace, isWrite);

            var prefixResult = await ReceiveResponseAsync(0, McFrameCodec.GetResponseHeaderLength(FrameType), cancellationToken)
                .ConfigureAwait(false);
            if (!prefixResult.IsSuccess)
                return Failure(prefixResult, trace, isWrite);

            byte[] prefix = prefixResult.ResultValue;
            int expectedDataLength = request is McReadRequest readRequest ? readRequest.ExpectedDataLength : 0;

            OperationResult<byte[]> frameResult = await ReceiveFrameBodyAsync(prefix, expectedDataLength, cancellationToken)
                .ConfigureAwait(false);
            if (!frameResult.IsSuccess)
                return Failure(frameResult, trace, isWrite);

            byte[] responseFrame = frameResult.ResultValue;
            trace.Responses.Add(ToHex(responseFrame));

            McPacket packet;
            try
            {
                packet = McFrameCodec.ParseResponse(FrameType, responseFrame, expectedSerialNumber);
            }
            catch (Exception ex)
            {
                DebugLog("[MC] 响应解析失败: {Message}", ex.Message);
                var violation = OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(ex);
                violation.FailureKind = OperationFailureKind.ProtocolViolation;
                return Failure(violation, trace, false);
            }

            if (!packet.IsSuccess)
            {
                var rejected = new OperationResult<IDeviceReadWriteMessage>
                {
                    IsSuccess = false,
                    FailureKind = OperationFailureKind.DeviceRejected,
                    ErrorCode = packet.EndCode,
                    Message = packet.ErrorMessage
                };
                return Failure(rejected, trace, false);
            }

            McReadResponse readResponse = null;
            McWriteResponse writeResponse = null;
            if (isWrite)
            {
                writeResponse = new McWriteResponse(packet);
                writeResponse.ProtocolMessageFrame = responseFrame;
                writeResponse.ProtocolResponseLength = responseFrame.Length;
                writeResponse.RegisterAddress = request.RegisterAddress;
                writeResponse.RegisterCount = request.RegisterCount;
            }
            else
            {
                readResponse = new McReadResponse(packet);
                readResponse.ProtocolMessageFrame = responseFrame;
                readResponse.ProtocolResponseLength = responseFrame.Length;
                readResponse.RegisterAddress = request.RegisterAddress;
                readResponse.RegisterCount = request.RegisterCount;
            }

            IDeviceReadWriteMessage response = isWrite
                ? (IDeviceReadWriteMessage)writeResponse
                : readResponse;

            return new OperationResult<IDeviceReadWriteMessage>
            {
                IsSuccess = true,
                ResultValue = response,
                Requsts = trace.Requsts,
                Responses = trace.Responses
            }.Complete();
        }

        /// <summary>
        /// 读完前缀后按帧规则取数据部分：1E 由点数推算、3E/4E 由长度字段给出，
        /// 任一环节失败都不再继续读，避免把错误码当数据。
        /// </summary>
        private async Task<OperationResult<byte[]>> ReceiveFrameBodyAsync(byte[] prefix, int expectedDataLength,
            CancellationToken cancellationToken)
        {
            if (FrameType == McFrameType.A1E)
            {
                if ((prefix[0] & 0x80) == 0)
                    return ProtocolFailure($"A-1E 响应副头部非法：0x{prefix[0]:X2}");

                byte endCode = prefix[1];
                if (endCode == McCommonMethods.EndCodeA1EAbnormal)
                {
                    var abnormalResult = await ReceiveResponseAsync(0, 1, cancellationToken).ConfigureAwait(false);
                    if (!abnormalResult.IsSuccess)
                        return abnormalResult;
                    return OperationResult.CreateSuccessResult(Concat(prefix, abnormalResult.ResultValue));
                }

                if (endCode != McCommonMethods.EndCodeNormal)
                    return OperationResult.CreateSuccessResult(prefix);

                if (expectedDataLength <= 0)
                    return OperationResult.CreateSuccessResult(prefix);

                var dataResult = await ReceiveResponseAsync(0, expectedDataLength, cancellationToken).ConfigureAwait(false);
                if (!dataResult.IsSuccess)
                    return dataResult;
                return OperationResult.CreateSuccessResult(Concat(prefix, dataResult.ResultValue));
            }

            int lengthFieldOffset = FrameType == McFrameType.Q4E ? 11 : 7;
            if (prefix.Length < lengthFieldOffset + 2)
                return ProtocolFailure($"MC 响应前缀长度不足：实际 {prefix.Length} 字节");

            // 副头部不对说明帧已错位，此时长度字段不可信，直接失败而不去读数据
            ushort expectedSubHeader = FrameType == McFrameType.Q4E
                ? McFrameCodec.SubHeader4EResponse
                : McFrameCodec.SubHeader3EResponse;
            ushort actualSubHeader = McCommonMethods.ReadUInt16(prefix, 0);
            if (actualSubHeader != expectedSubHeader)
                return ProtocolFailure($"MC 响应副头部非法：0x{actualSubHeader:X4}，期望 0x{expectedSubHeader:X4}");

            int dataLength = McCommonMethods.ReadUInt16(prefix, lengthFieldOffset);
            if (dataLength < 2)
                return ProtocolFailure($"MC 响应长度字段 0x{dataLength:X4} 小于结束码长度 2");

            int bodyLength = dataLength - 2;
            if (bodyLength == 0)
                return OperationResult.CreateSuccessResult((byte[])prefix.Clone());

            var bodyResult = await ReceiveResponseAsync(0, bodyLength, cancellationToken).ConfigureAwait(false);
            if (!bodyResult.IsSuccess)
                return bodyResult;
            return OperationResult.CreateSuccessResult(Concat(prefix, bodyResult.ResultValue));
        }

        private ushort NextSerialNumber()
        {
            int value = Interlocked.Increment(ref _serialNumber);
            return (ushort)(value & 0xFFFF);
        }

        private static OperationResult<IDeviceReadWriteMessage> Failure(OperationResult source, OperationResult trace,
            bool outcomeUnknown)
        {
            var failed = OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(source);
            failed.Requsts = trace.Requsts;
            failed.Responses = trace.Responses;
            if (outcomeUnknown && failed.FailureKind != OperationFailureKind.DeviceRejected)
                failed.FailureKind = OperationFailureKind.OutcomeUnknown;
            return failed;
        }

        private static OperationResult<byte[]> ProtocolFailure(string message)
        {
            return new OperationResult<byte[]>
            {
                IsSuccess = false,
                FailureKind = OperationFailureKind.ProtocolViolation,
                Message = message
            };
        }

        private static byte[] Concat(byte[] first, byte[] second)
        {
            var merged = new byte[first.Length + second.Length];
            Array.Copy(first, 0, merged, 0, first.Length);
            Array.Copy(second, 0, merged, first.Length, second.Length);
            return merged;
        }

        private static string ToHex(byte[] data)
        {
            if (data == null)
                return string.Empty;

            var text = new List<string>(data.Length);
            for (int i = 0; i < data.Length; i++)
                text.Add(data[i].ToString("X2"));
            return string.Join(" ", text);
        }

        private void DebugLog(string message, params object[] args)
        {
            if (!EnableDebugLog)
                return;

            Logger?.LogDebug(message, args);
        }
    }
}

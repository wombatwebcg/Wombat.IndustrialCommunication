using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.Abstractions;
using Wombat.IndustrialCommunication.Helpers;
using Wombat.IndustrialCommunication.Models;
using Wombat.IndustrialCommunication.Modbus;

namespace Wombat.IndustrialCommunication.Messaging
{
    /// <summary>
    /// 设备消息传输类，负责处理与设备的通信
    /// </summary>
    public class DeviceMessageTransport : IDeviceMessageTransport, IDisposable
    {
        private readonly AsyncLock _asyncLock = new AsyncLock();
        private IStreamResource _streamResource;

        public ILogger Logger { get; set; }

        public bool EnableDebugLog { get; set; } = false;

        private void DebugLog(string message, params object[] args)
        {
            if (!EnableDebugLog)
            {
                return;
            }

            Logger?.LogDebug(message, args);
        }



        public DeviceMessageTransport(IStreamResource streamResource)
        {
            Debug.Assert(streamResource != null, "Argument streamResource cannot be null.");

            _streamResource = streamResource;
        }


        public IStreamResource StreamResource
        {
            get { return _streamResource; }
        }

        public int Retries
        {
            get { return 0; }
            set { }
        }

        public TimeSpan WaitToRetryMilliseconds
        {
            get { return TimeSpan.Zero; }
            set { }
        }


        public TimeSpan ResponseInterval { get; set; } = TimeSpan.FromMilliseconds(50);

        public Task<OperationResult<byte[]>> ReceiveResponseAsync(int offset, int length)
        {
            return ReceiveResponseAsync(offset, length, CancellationToken.None);
        }

        public async Task<OperationResult<byte[]>> ReceiveResponseAsync(int offset, int length, CancellationToken cancellationToken)
        {
            using (await _asyncLock.LockAsync(cancellationToken))
            using (var timeoutCts = new CancellationTokenSource(_streamResource.ReceiveTimeout))
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
            {
                try
                {
                    var buffer = new byte[length];
                    var read = await _streamResource.Receive(buffer, offset, length, cts.Token);
                    return read?.IsSuccess ?? false
                        ? OperationResult.CreateSuccessResult(buffer)
                        : OperationResult.CreateFailedResult<byte[]>(read);
                }
                catch (Exception exception)
                {
                    return OperationResult.CreateFailedResult<byte[]>(exception);
                }
            }
        }

        protected async Task<OperationResult<byte[]>> ReceiveExactAsync(int length, CancellationToken cancellationToken)
        {
            if (length < 0 || length > 260) return new OperationResult<byte[]> { IsSuccess = false, FailureKind = OperationFailureKind.ProtocolViolation, Message = "Invalid response length" };
            var buffer = new byte[length];
            var read = 0;
            while (read < length)
            {
                var part = await ReceiveCoreAsync(buffer, read, length - read, cancellationToken).ConfigureAwait(false);
                if (!part.IsSuccess) return OperationResult.CreateFailedResult<byte[]>(part);
                if (part.ResultValue <= 0) return new OperationResult<byte[]> { IsSuccess = false, FailureKind = OperationFailureKind.ConnectionClosed, Message = "Connection closed before response was complete" };
                read += part.ResultValue;
            }
            return OperationResult.CreateSuccessResult(buffer);
        }

        private async Task<OperationResult<int>> ReceiveCoreAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            using var timeout = new CancellationTokenSource(_streamResource.ReceiveTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
            try
            {
                var result = await _streamResource.Receive(buffer, offset, length, linked.Token).ConfigureAwait(false);
                if (result?.IsSuccess == true) return result;
                return result ?? new OperationResult<int> { IsSuccess = false, FailureKind = OperationFailureKind.TransportFailure, Message = "Receive failed" };
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            { return new OperationResult<int> { IsSuccess = false, IsCancelled = true, FailureKind = OperationFailureKind.Cancelled, Exception = ex, Message = ex.Message }; }
            catch (OperationCanceledException ex)
            { return new OperationResult<int> { IsSuccess = false, FailureKind = OperationFailureKind.ReceiveTimeout, Exception = ex, Message = ex.Message }; }
            catch (Exception ex)
            { return new OperationResult<int> { IsSuccess = false, FailureKind = OperationFailureKind.TransportFailure, Exception = ex, Message = ex.Message }; }
        }

        public Task<OperationResult> SendRequestAsync(byte[] request)
        {
            return SendRequestAsync(request, CancellationToken.None);
        }

        public async Task<OperationResult> SendRequestAsync(byte[] request, CancellationToken cancellationToken)
        {
            using (await _asyncLock.LockAsync(cancellationToken))
            using (var timeoutCts = new CancellationTokenSource(_streamResource.SendTimeout))
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken))
            {
                try
                {
                    var write = await _streamResource.Send(request, 0, request.Length, cts.Token);
                    return write?.IsSuccess ?? false
                        ? OperationResult.CreateSuccessResult(write)
                        : OperationResult.CreateFailedResult(write);
                }
                catch (Exception exception)
                {
                    return OperationResult.CreateFailedResult(exception);
                }
            }
        }

        public virtual Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage request)
        {
            return UnicastReadMessageAsync(request, CancellationToken.None);
        }

        public virtual async Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage request, CancellationToken cancellationToken)
        {
            if (request is ModbusTcpRequest tcp) return await ExchangeTcpAsync(tcp, false, cancellationToken).ConfigureAwait(false);
            if (request is ModbusRTURequest rtu) return await ExchangeRtuAsync(rtu, false, cancellationToken).ConfigureAwait(false);
            OperationResult<IDeviceReadWriteMessage> result = new OperationResult<IDeviceReadWriteMessage>();
            try
            {
                var commandRequest1 = await SendRequestAsync(request.ProtocolMessageFrame, cancellationToken);
                result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                if (commandRequest1.IsSuccess)
                {
                    await Task.Delay(ResponseInterval, cancellationToken);
                    var response1Result = await ReceiveResponseAsync(0, request.ProtocolResponseLength, cancellationToken);
                    if (!response1Result.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(response1Result);
                    }
                    result.Responses.Add(string.Join(" ", response1Result.ResultValue.Select(t => t.ToString("X2"))));
                    var package = response1Result.ResultValue;
                    result.ResultValue = new DeviceReadWriteMessage();
                    result.ResultValue.Initialize(package);
                    result.ResultValue.ProtocolResponseLength = request.ProtocolResponseLength;
                    result.ResultValue.RegisterAddress = request.RegisterAddress;
                    result.ResultValue.RegisterCount = request.RegisterCount;

                    return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result, result.ResultValue);
                }
                else
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(commandRequest1);

                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();

        }
        public virtual Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage request)
        {
            return UnicastWriteMessageAsync(request, CancellationToken.None);
        }

        public virtual async Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage request, CancellationToken cancellationToken)
        {
            if (request is ModbusTcpRequest tcp) return await ExchangeTcpAsync(tcp, true, cancellationToken).ConfigureAwait(false);
            if (request is ModbusRTURequest rtu) return await ExchangeRtuAsync(rtu, true, cancellationToken).ConfigureAwait(false);
            OperationResult<IDeviceReadWriteMessage> result = new OperationResult<IDeviceReadWriteMessage>();
            bool requestSent = false;
            try
            {
                var commandRequest1 = await SendRequestAsync(request.ProtocolMessageFrame, cancellationToken);
                result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                if (commandRequest1.IsSuccess)
                {
                    requestSent = true;
                    await Task.Delay(ResponseInterval, cancellationToken);
                    var response1Result = await ReceiveResponseAsync(0, request.ProtocolResponseLength, cancellationToken);
                    if (!response1Result.IsSuccess)
                    {
                        var unknown = OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(response1Result);
                        unknown.FailureKind = OperationFailureKind.OutcomeUnknown;
                        return unknown;
                    }
                    result.Responses.Add(string.Join(" ", response1Result.ResultValue.Select(t => t.ToString("X2"))));
                    var package = response1Result.ResultValue;
                    result.ResultValue = new DeviceReadWriteMessage();
                    result.ResultValue.Initialize(package);
                    result.ResultValue.ProtocolResponseLength = request.ProtocolResponseLength;
                    result.ResultValue.RegisterAddress = request.RegisterAddress;
                    result.ResultValue.RegisterCount = request.RegisterCount;
                    return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result, result.ResultValue);
                }
                else
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(commandRequest1);
                }
            }
            catch (Exception ex)
            {
                var failed = OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(ex);
                if (requestSent)
                {
                    failed.FailureKind = OperationFailureKind.OutcomeUnknown;
                }
                return failed;
            }

        }

        private async Task<OperationResult<IDeviceReadWriteMessage>> ExchangeTcpAsync(ModbusTcpRequest request, bool write, CancellationToken token)
        {
            using (await _asyncLock.LockAsync(token).ConfigureAwait(false))
            {
                var result = new OperationResult<IDeviceReadWriteMessage>();
                result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(x => x.ToString("X2"))));
                var sent = await SendCoreAsync(request.ProtocolMessageFrame, token).ConfigureAwait(false);
                if (!sent.IsSuccess) return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(sent);
                var prefix = await ReceiveExactAsync(6, token).ConfigureAwait(false);
                if (!prefix.IsSuccess) return FailureWithTrace(prefix, result, write && sent.IsSuccess);
                var p = prefix.ResultValue;
                var protocol = (ushort)((p[2] << 8) | p[3]);
                var length = (ushort)((p[4] << 8) | p[5]);
                if (protocol != 0 || length < 2 || length > 253) return ProtocolFailure(result, "Invalid Modbus TCP MBAP header");
                var tail = await ReceiveExactAsync(length, token).ConfigureAwait(false);
                if (!tail.IsSuccess) return FailureWithTrace(tail, result, write);
                var frame = new byte[6 + length]; Buffer.BlockCopy(p, 0, frame, 0, 6); Buffer.BlockCopy(tail.ResultValue, 0, frame, 6, length);
                result.Responses.Add(string.Join(" ", frame.Select(x => x.ToString("X2"))));
                var message = new DeviceReadWriteMessage { ProtocolMessageFrame = frame, ProtocolResponseLength = frame.Length, RegisterAddress = request.RegisterAddress, RegisterCount = request.RegisterCount };
                var fc = frame[7];
                if ((fc & 0x80) != 0 && length == 3) return new OperationResult<IDeviceReadWriteMessage> { IsSuccess = false, FailureKind = OperationFailureKind.DeviceRejected, Message = ModbusTcpResponse.ErrMsg(frame[8]), Requsts = result.Requsts, Responses = result.Responses };
                return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result, message);
            }
        }

        private async Task<OperationResult<IDeviceReadWriteMessage>> ExchangeRtuAsync(ModbusRTURequest request, bool write, CancellationToken token)
        {
            using (await _asyncLock.LockAsync(token).ConfigureAwait(false))
            {
                var result = new OperationResult<IDeviceReadWriteMessage>(); result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(x => x.ToString("X2"))));
                var sent = await SendCoreAsync(request.ProtocolMessageFrame, token).ConfigureAwait(false); if (!sent.IsSuccess) return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(sent);
                var head = await ReceiveExactAsync(2, token).ConfigureAwait(false); if (!head.IsSuccess) return FailureWithTrace(head, result, write);
                var fc = head.ResultValue[1]; int remaining;
                if ((fc & 0x80) != 0) remaining = 3;
                else if (fc is 1 or 2 or 3 or 4) { var count = await ReceiveExactAsync(1, token).ConfigureAwait(false); if (!count.IsSuccess) return FailureWithTrace(count, result, write); remaining = count.ResultValue[0] + 2; head.ResultValue = head.ResultValue.Concat(count.ResultValue).ToArray(); }
                else if (fc is 5 or 6 or 15 or 16) remaining = 6;
                else return ProtocolFailure(result, "Unsupported Modbus RTU function code");
                var tail = await ReceiveExactAsync(remaining, token).ConfigureAwait(false); if (!tail.IsSuccess) return FailureWithTrace(tail, result, write);
                var frame = head.ResultValue.Concat(tail.ResultValue).ToArray(); result.Responses.Add(string.Join(" ", frame.Select(x => x.ToString("X2"))));
                if (!Helpers.CRC16Helper.ValidateCRC(frame)) return ProtocolFailure(result, "Modbus RTU CRC mismatch");
                var message = new DeviceReadWriteMessage { ProtocolMessageFrame = frame, ProtocolResponseLength = frame.Length, RegisterAddress = request.RegisterAddress, RegisterCount = request.RegisterCount };
                if ((fc & 0x80) != 0) return new OperationResult<IDeviceReadWriteMessage> { IsSuccess = false, FailureKind = OperationFailureKind.DeviceRejected, Message = "Modbus RTU exception", Requsts = result.Requsts, Responses = result.Responses };
                return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result, message);
            }
        }

        private async Task<OperationResult> SendCoreAsync(byte[] request, CancellationToken token)
        {
            using var timeout = new CancellationTokenSource(_streamResource.SendTimeout); using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, token);
            try { return await _streamResource.Send(request, 0, request.Length, linked.Token).ConfigureAwait(false) ?? OperationResult.CreateFailedResult("Send failed"); }
            catch (OperationCanceledException ex) when (token.IsCancellationRequested) { return new OperationResult { IsSuccess = false, FailureKind = OperationFailureKind.Cancelled, IsCancelled = true, Exception = ex, Message = ex.Message }; }
            catch (OperationCanceledException ex) { return new OperationResult { IsSuccess = false, FailureKind = OperationFailureKind.SendTimeout, Exception = ex, Message = ex.Message }; }
            catch (Exception ex) { return new OperationResult { IsSuccess = false, FailureKind = OperationFailureKind.TransportFailure, Exception = ex, Message = ex.Message }; }
        }

        private static OperationResult<IDeviceReadWriteMessage> FailureWithTrace(OperationResult failure, OperationResult<IDeviceReadWriteMessage> trace, bool unknown) { var r = OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(failure); r.Requsts = trace.Requsts; r.Responses = trace.Responses; if (unknown && r.FailureKind != OperationFailureKind.DeviceRejected) r.FailureKind = OperationFailureKind.OutcomeUnknown; return r; }
        private static OperationResult<IDeviceReadWriteMessage> ProtocolFailure(OperationResult<IDeviceReadWriteMessage> trace, string message) => new OperationResult<IDeviceReadWriteMessage> { IsSuccess = false, FailureKind = OperationFailureKind.ProtocolViolation, Message = message, Requsts = trace.Requsts, Responses = trace.Responses };

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                if (_streamResource == null)
                    return;

            _streamResource?.Dispose();
            _streamResource = default;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

    }
}

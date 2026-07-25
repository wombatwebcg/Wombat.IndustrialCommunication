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

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 设备消息传输类，负责处理与设备的通信
    /// </summary>
    public class DeviceMessageTransport : IDeviceMessageTransport, IDisposable
    {
        private AsyncLock _asyncLock = new AsyncLock();
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

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                if (_streamResource == null)
                    return;

            _streamResource.Dispose();
            _streamResource = default;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

    }
}

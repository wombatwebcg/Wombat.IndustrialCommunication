using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    public class DeviceMessageTransport : IDeviceMessageTransport, IDisposable
    {
        private AsyncLock _asyncLock = new AsyncLock();
        private IStreamResource _streamResource;
        private int _retries = 1;
        private TimeSpan _waitToRetryMilliseconds = TimeSpan.FromMilliseconds(1000);
        private static ArrayPool<byte> _byteArrayPool = ArrayPool<byte>.Shared;



        public DeviceMessageTransport(IStreamResource streamResource)
        {
            Debug.Assert(streamResource != null, "Argument streamResource cannot be null.");

            _streamResource = streamResource;
        }


        CancellationToken _cancellationToken = new CancellationToken();
        public IStreamResource StreamResource
        {
            get { return _streamResource; }
        }

        public int Retries
        {
            get { return _retries; }
            set { _retries = value; }
        }

        public TimeSpan WaitToRetryMilliseconds
        {
            get { return _waitToRetryMilliseconds; }
            set
            {

                _waitToRetryMilliseconds = value;
            }
        }




        public async Task<OperationResult<byte[]>> ReceiveResponseAsync(int offset, int length)
        {
            using (await _asyncLock.LockAsync())
            {
                int attempt = 1;
                bool success = false;
                do
                {
                    try
                    {
                        byte[] buffer = new byte[length];
                        var read = await _streamResource?.Receive(buffer, offset, length,_cancellationToken);
                        bool readAgain = true;
                        do
                        {
                            if (read?.IsSuccess ?? false)
                            {
                                readAgain = false;
                                success = true;
                                return OperationResult.CreateSuccessResult(buffer);
                            }
                            else
                            {
                                read = await _streamResource?.Receive(buffer, offset, length, _cancellationToken);
                                if (read?.IsSuccess ?? false)
                                {
                                    readAgain = false;
                                    success = true;
                                    return OperationResult.CreateSuccessResult(buffer);
                                }
                                else
                                {
                                    if (attempt++ > _retries)
                                    {
                                        return OperationResult.CreateFailedResult(buffer);
                                    }
                                }

                            }
                        } while (readAgain);
                    }
                    catch (DevcieResponseException se)
                    {

                        //if (se.SlaveExceptionCode != Modbus.SlaveDeviceBusy)
                        //    throw;

                        //if (SlaveBusyUsesRetryCount && attempt++ > _retries)
                        //    throw;

                        //Debug.WriteLine(
                        //    "Received SLAVE_DEVICE_BUSY exception response, waiting {0} milliseconds and resubmitting request.",
                        //    _waitToRetryMilliseconds);
                        //Sleep(WaitToRetryMilliseconds);
                    }
                    catch (Exception e)
                    {
                        if (e is FormatException ||
                            e is NotImplementedException ||
                            e is TimeoutException ||
                            e is IOException)
                        {
                            Debug.WriteLine("{0}, {1} retries remaining - {2}", e.GetType().Name, _retries - attempt + 1, e);

                        }

                    }
                } while (!success);
                return OperationResult.CreateFailedResult<byte[]>();

            }
        }
        public async Task<OperationResult> SendRequestAsync(byte[] request)
        {
            using (await _asyncLock.LockAsync())
            {
                int attempt = 1;
                bool success = false;
                do
                {
                    try
                    {
                        var write = await _streamResource?.Send(request, 0, request.Length, _cancellationToken);
                        bool readAgain = true;
                        do
                        {
                            if (write?.IsSuccess ?? false)
                            {
                                readAgain = false;
                                success = true;
                                return OperationResult.CreateSuccessResult(write);
                            }
                            else
                            {
                                write = await _streamResource?.Send(request, 0, request.Length, _cancellationToken);
                                if (write?.IsSuccess ?? false)
                                {
                                    readAgain = false;
                                    success = true;
                                    return OperationResult.CreateSuccessResult(write);
                                }
                                else
                                {
                                    if (attempt++ > _retries)
                                    {
                                        return OperationResult.CreateFailedResult(write);
                                    }
                                }

                            }
                        } while (readAgain);
                    }
                    catch (DevcieResponseException se)
                    {

                        //if (se.SlaveExceptionCode != Modbus.SlaveDeviceBusy)
                        //    throw;

                        //if (SlaveBusyUsesRetryCount && attempt++ > _retries)
                        //    throw;

                        //Debug.WriteLine(
                        //    "Received SLAVE_DEVICE_BUSY exception response, waiting {0} milliseconds and resubmitting request.",
                        //    _waitToRetryMilliseconds);
                        //Sleep(WaitToRetryMilliseconds);
                    }
                    catch (Exception e)
                    {
                        if (e is FormatException ||
                            e is NotImplementedException ||
                            e is TimeoutException ||
                            e is IOException)
                        {
                            Debug.WriteLine("{0}, {1} retries remaining - {2}", e.GetType().Name, _retries - attempt + 1, e);

                        }

                    }
                } while (!success);
                return OperationResult.CreateFailedResult();

            }
        }
        public virtual async Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage request)
        {
            OperationResult<IDeviceReadWriteMessage> result = new OperationResult<IDeviceReadWriteMessage>();
            try
            {
                var commandRequest1 = await SendRequestAsync(request.ProtocolMessageFrame);
                result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                if (commandRequest1.IsSuccess)
                {
                    var response1Result = await ReceiveResponseAsync(0, request.ProtocolResponseLength);
                    if (!response1Result.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
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
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();

        }
        public virtual async Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage request)
        {
            OperationResult<IDeviceReadWriteMessage> result = new OperationResult<IDeviceReadWriteMessage>();
            try
            {
                var commandRequest1 = await SendRequestAsync(request.ProtocolMessageFrame);
                result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                if (commandRequest1.IsSuccess)
                {
                    var response1Result = await ReceiveResponseAsync(0, request.ProtocolResponseLength);
                    if (!response1Result.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();
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
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>();

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

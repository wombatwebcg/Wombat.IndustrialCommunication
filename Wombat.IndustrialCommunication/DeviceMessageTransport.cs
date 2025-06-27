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
    /// <summary>
    /// 设备消息传输类，负责处理与设备的通信
    /// </summary>
    public class DeviceMessageTransport : IDeviceMessageTransport, IDisposable
    {
        private AsyncLock _asyncLock = new AsyncLock();
        private IStreamResource _streamResource;
        private int _retries = 2;
        private TimeSpan _waitToRetryMilliseconds = TimeSpan.FromMilliseconds(100);




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


        public TimeSpan ResponseInterval { get; set; } = TimeSpan.FromMilliseconds(50);

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
                        using (var cts = new CancellationTokenSource(_streamResource.ReceiveTimeout))
                        {
                            try
                            {
                                byte[] buffer = new byte[length];
                                cts.Token.Register(() => _streamResource.StreamClose());
                                var read = await _streamResource.Receive(buffer, offset, length, cts.Token);
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
                                        if (attempt++ > _retries)
                                        {
                                            var retryResult = OperationResult.CreateFailedResult<byte[]>($"读取设备失败,重试次数:{_retries - attempt + 1},超时参数:{_streamResource.ReceiveTimeout.TotalMilliseconds}");
                                        }
                                        // 可选：等待一段时间后重试
                                        await Task.Delay(WaitToRetryMilliseconds, cts.Token);
                                    }
                                } while (readAgain);
                            }
                            catch (OperationCanceledException)
                            {
                                return OperationResult.CreateFailedResult<byte[]>("操作超时或被取消");
                            }
                            catch (ObjectDisposedException)
                            {
                                // 流被关闭引发的异常
                                return OperationResult.CreateFailedResult<byte[]>("连接已关闭");
                            }
                            catch (Exception e)
                            {
                                return OperationResult.CreateFailedResult<byte[]>($"操作失败：{e.Message}");
                            }
                        }
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
                bool readAgain = true;
                do
                {
                    try
                    {
                        using (var cts = new CancellationTokenSource(_streamResource.SendTimeout))
                        {
                            var ss = _streamResource.SendTimeout;
                            cts.Token.Register(() => _streamResource.StreamClose());
                            var write = await _streamResource?.Send(request, 0, request.Length, cts.Token);
                            do
                            {
                                try
                                {

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
                                            var retryResult = OperationResult.CreateFailedResult<byte[]>($"写入设备失败,重试次数:{_retries - attempt + 1},超时参数:{_streamResource.SendTimeout.TotalMilliseconds}");
                                            retryResult.Requsts.Add(string.Join(" ", request.Select(t => t.ToString("X2"))));
                                            return retryResult;
                                        }
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    return OperationResult.CreateFailedResult<byte[]>("操作超时或被取消");
                                }
                                catch (ObjectDisposedException)
                                {
                                    // 流被关闭引发的异常
                                    return OperationResult.CreateFailedResult<byte[]>("连接已关闭");
                                }
                                catch (Exception e)
                                {
                                    return OperationResult.CreateFailedResult<byte[]>($"操作失败：{e.Message}");
                                }

                            } while (readAgain);

                        }
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
                    await Task.Delay(ResponseInterval);
                    var response1Result = await ReceiveResponseAsync(0, request.ProtocolResponseLength);
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
        public virtual async Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage request)
        {
            OperationResult<IDeviceReadWriteMessage> result = new OperationResult<IDeviceReadWriteMessage>();
            try
            {
                var commandRequest1 = await SendRequestAsync(request.ProtocolMessageFrame);
                result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(t => t.ToString("X2"))));
                if (commandRequest1.IsSuccess)
                {
                    await Task.Delay(ResponseInterval);
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

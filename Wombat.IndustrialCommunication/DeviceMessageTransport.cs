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
        // 异步锁，确保并发安全
        private readonly AsyncLock _asyncLock = new AsyncLock();
        // 流资源接口
        private IStreamResource _streamResource;
        // 重试次数
        private int _retries = 2;
        // 重试等待时间
        private TimeSpan _waitToRetryMilliseconds = TimeSpan.FromMilliseconds(100);
        // 字节数组池，用于优化内存使用
        private static readonly ArrayPool<byte> _byteArrayPool = ArrayPool<byte>.Shared;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="streamResource">流资源接口</param>
        public DeviceMessageTransport(IStreamResource streamResource)
        {
            Debug.Assert(streamResource != null, "流资源参数不能为空");
            _streamResource = streamResource;
        }

        /// <summary>
        /// 获取流资源
        /// </summary>
        public IStreamResource StreamResource => _streamResource;

        /// <summary>
        /// 获取或设置重试次数
        /// </summary>
        public int Retries
        {
            get => _retries;
            set => _retries = value;
        }

        /// <summary>
        /// 获取或设置重试等待时间
        /// </summary>
        public TimeSpan WaitToRetryMilliseconds
        {
            get => _waitToRetryMilliseconds;
            set => _waitToRetryMilliseconds = value;
        }

        /// <summary>
        /// 响应间隔时间
        /// </summary>
        public TimeSpan ResponseInterval { get; set; } = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// 异步接收响应
        /// </summary>
        /// <param name="offset">偏移量</param>
        /// <param name="length">长度</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult<byte[]>> ReceiveResponseAsync(int offset, int length)
        {
            using (await _asyncLock.LockAsync())
            {
                if (!_streamResource.Connected)
                {
                    return OperationResult.CreateFailedResult<byte[]>("设备未连接");
                }

                int attempt = 1;
                byte[] buffer = _byteArrayPool.Rent(length);
                try
                {
                    using (var cts = new CancellationTokenSource(_streamResource.ReceiveTimeout))
                    {
                        cts.Token.Register(() => _streamResource.StreamClose());

                        while (attempt <= _retries)
                        {
                            try
                            {
                                var read = await _streamResource.Receive(buffer, offset, length, cts.Token);
                                if (read?.IsSuccess ?? false)
                                {
                                    var result = new byte[length];
                                    Array.Copy(buffer, result, length);
                                    return OperationResult.CreateSuccessResult(result);
                                }

                                if (attempt++ > _retries)
                                {
                                    return OperationResult.CreateFailedResult<byte[]>($"读取设备失败，重试次数：{_retries}，超时参数：{_streamResource.ReceiveTimeout.TotalMilliseconds}ms");
                                }

                                await Task.Delay(WaitToRetryMilliseconds, cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                return OperationResult.CreateFailedResult<byte[]>("操作超时");
                            }
                            catch (ObjectDisposedException)
                            {
                                return OperationResult.CreateFailedResult<byte[]>("连接已关闭");
                            }
                            catch (Exception e)
                            {
                                if (attempt > _retries)
                                {
                                    return OperationResult.CreateFailedResult<byte[]>($"操作失败：{e.Message}");
                                }
                                Debug.WriteLine($"接收异常：{e.GetType().Name}，剩余重试次数：{_retries - attempt + 1}，异常信息：{e}");
                            }
                        }
                    }
                }
                finally
                {
                    _byteArrayPool.Return(buffer);
                }

                return OperationResult.CreateFailedResult<byte[]>("接收失败");
            }
        }

        /// <summary>
        /// 异步发送请求
        /// </summary>
        /// <param name="request">请求数据</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> SendRequestAsync(byte[] request)
        {
            using (await _asyncLock.LockAsync())
            {
                if (!_streamResource.Connected)
                {
                    return OperationResult.CreateFailedResult("设备未连接");
                }

                int attempt = 1;
                var result = new OperationResult();
                result.Requsts.Add(string.Join(" ", request.Select(t => t.ToString("X2"))));

                using (var cts = new CancellationTokenSource(_streamResource.SendTimeout))
                {
                    cts.Token.Register(() => _streamResource.StreamClose());

                    while (attempt <= _retries)
                    {
                        try
                        {
                            var write = await _streamResource.Send(request, 0, request.Length, cts.Token);
                            if (write?.IsSuccess ?? false)
                            {
                                return OperationResult.CreateSuccessResult(write);
                            }

                            if (attempt++ > _retries)
                            {
                                return OperationResult.CreateFailedResult($"写入设备失败，重试次数：{_retries}，超时参数：{_streamResource.SendTimeout.TotalMilliseconds}ms");
                            }

                            await Task.Delay(WaitToRetryMilliseconds, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            return OperationResult.CreateFailedResult("操作超时");
                        }
                        catch (ObjectDisposedException)
                        {
                            return OperationResult.CreateFailedResult("连接已关闭");
                        }
                        catch (Exception e)
                        {
                            if (attempt > _retries)
                            {
                                return OperationResult.CreateFailedResult($"操作失败：{e.Message}");
                            }
                            Debug.WriteLine($"发送异常：{e.GetType().Name}，剩余重试次数：{_retries - attempt + 1}，异常信息：{e}");
                        }
                    }
                }

                return OperationResult.CreateFailedResult("发送失败");
            }
        }

        /// <summary>
        /// 单播读取消息
        /// </summary>
        /// <param name="request">读取请求</param>
        /// <returns>操作结果</returns>
        public virtual async Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage request)
        {
            var result = new OperationResult<IDeviceReadWriteMessage>();
            try
            {
                var commandRequest = await SendRequestAsync(request.ProtocolMessageFrame);
                result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(t => t.ToString("X2"))));

                if (!commandRequest.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(commandRequest);
                }

                await Task.Delay(ResponseInterval);
                var responseResult = await ReceiveResponseAsync(0, request.ProtocolResponseLength);
                responseResult.Requsts.Add(result.Requsts[0]);

                if (!responseResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(responseResult);
                }

                result.Responses.Add(string.Join(" ", responseResult.ResultValue.Select(t => t.ToString("X2"))));
                result.ResultValue = new DeviceReadWriteMessage
                {
                    ProtocolResponseLength = request.ProtocolResponseLength,
                    RegisterAddress = request.RegisterAddress,
                    RegisterCount = request.RegisterCount
                };
                result.ResultValue.Initialize(responseResult.ResultValue);

                return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result, result.ResultValue);
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"读取消息失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 单播写入消息
        /// </summary>
        /// <param name="request">写入请求</param>
        /// <returns>操作结果</returns>
        public virtual async Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage request)
        {
            var result = new OperationResult<IDeviceReadWriteMessage>();
            try
            {
                var commandRequest = await SendRequestAsync(request.ProtocolMessageFrame);
                result.Requsts.Add(string.Join(" ", request.ProtocolMessageFrame.Select(t => t.ToString("X2"))));

                if (!commandRequest.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(commandRequest);
                }

                await Task.Delay(ResponseInterval);
                var responseResult = await ReceiveResponseAsync(0, request.ProtocolResponseLength);

                if (!responseResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>(responseResult);
                }

                result.Responses.Add(string.Join(" ", responseResult.ResultValue.Select(t => t.ToString("X2"))));
                result.ResultValue = new DeviceReadWriteMessage
                {
                    ProtocolResponseLength = request.ProtocolResponseLength,
                    RegisterAddress = request.RegisterAddress,
                    RegisterCount = request.RegisterCount
                };
                result.ResultValue.Initialize(responseResult.ResultValue);

                return OperationResult.CreateSuccessResult<IDeviceReadWriteMessage>(result, result.ResultValue);
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<IDeviceReadWriteMessage>($"写入消息失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _streamResource != null)
            {
                _streamResource.Dispose();
                _streamResource = null;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

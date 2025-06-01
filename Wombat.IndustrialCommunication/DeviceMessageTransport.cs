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
        private int _retries = 5;
        // 重试等待时间
        private TimeSpan _waitToRetryMilliseconds = TimeSpan.FromMilliseconds(500);
        // 字节数组池，用于优化内存使用
        private static readonly ArrayPool<byte> _byteArrayPool = ArrayPool<byte>.Shared;

        #region 性能指标
        
        // 总发送请求数
        private long _totalSendRequests = 0;
        // 总接收响应数
        private long _totalReceiveResponses = 0;
        // 发送成功次数
        private long _successfulSendRequests = 0;
        // 接收成功次数
        private long _successfulReceiveResponses = 0;
        // 总发送字节数
        private long _totalSentBytes = 0;
        // 总接收字节数
        private long _totalReceivedBytes = 0;
        // 发送失败次数
        private long _failedSendRequests = 0;
        // 接收失败次数
        private long _failedReceiveResponses = 0;
        // 重试总次数
        private long _totalRetries = 0;
        // 累计发送耗时（毫秒）
        private long _totalSendTime = 0;
        // 累计接收耗时（毫秒）
        private long _totalReceiveTime = 0;
        // 上次重置指标时间
        private DateTime _lastMetricsReset = DateTime.Now;
        // 性能指标收集开关
        public bool EnableMetricsCollection { get; set; } = true;
        // 指标重置间隔（默认1小时）
        public TimeSpan MetricsResetInterval { get; set; } = TimeSpan.FromHours(1);
        
        /// <summary>
        /// 获取当前性能指标
        /// </summary>
        /// <returns>性能指标的字典</returns>
        public Dictionary<string, object> GetPerformanceMetrics()
        {
            // 检查是否需要重置指标
            if (EnableMetricsCollection && (DateTime.Now - _lastMetricsReset) > MetricsResetInterval)
            {
                ResetMetrics();
            }
            
            return new Dictionary<string, object>
            {
                ["TotalSendRequests"] = _totalSendRequests,
                ["TotalReceiveResponses"] = _totalReceiveResponses,
                ["SuccessfulSendRequests"] = _successfulSendRequests,
                ["SuccessfulReceiveResponses"] = _successfulReceiveResponses,
                ["FailedSendRequests"] = _failedSendRequests,
                ["FailedReceiveResponses"] = _failedReceiveResponses,
                ["TotalSentBytes"] = _totalSentBytes,
                ["TotalReceivedBytes"] = _totalReceivedBytes,
                ["TotalRetries"] = _totalRetries,
                ["SendSuccessRate"] = _totalSendRequests > 0 ? (double)_successfulSendRequests / _totalSendRequests : 0,
                ["ReceiveSuccessRate"] = _totalReceiveResponses > 0 ? (double)_successfulReceiveResponses / _totalReceiveResponses : 0,
                ["AverageSendTime"] = _successfulSendRequests > 0 ? (double)_totalSendTime / _successfulSendRequests : 0,
                ["AverageReceiveTime"] = _successfulReceiveResponses > 0 ? (double)_totalReceiveTime / _successfulReceiveResponses : 0,
                ["LastMetricsReset"] = _lastMetricsReset
            };
        }
        
        /// <summary>
        /// 重置性能指标
        /// </summary>
        public void ResetMetrics()
        {
            _totalSendRequests = 0;
            _totalReceiveResponses = 0;
            _successfulSendRequests = 0;
            _successfulReceiveResponses = 0;
            _totalSentBytes = 0;
            _totalReceivedBytes = 0;
            _failedSendRequests = 0;
            _failedReceiveResponses = 0;
            _totalRetries = 0;
            _totalSendTime = 0;
            _totalReceiveTime = 0;
            _lastMetricsReset = DateTime.Now;
        }
        
        #endregion

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
            if (EnableMetricsCollection)
            {
                Interlocked.Increment(ref _totalReceiveResponses);
            }
            
            var stopwatch = Stopwatch.StartNew();
            
            using (await _asyncLock.LockAsync())
            {
                if (!_streamResource.Connected)
                {
                    if (EnableMetricsCollection)
                    {
                        Interlocked.Increment(ref _failedReceiveResponses);
                    }
                    return OperationResult.CreateFailedResult<byte[]>("设备未连接");
                }

                int attempt = 1;
                int totalRetries = 0;
                byte[] buffer = _byteArrayPool.Rent(length);
                try
                {
                    using (var cts = new CancellationTokenSource(_streamResource.ReceiveTimeout))
                    {
                        // 避免直接调用StreamClose，改为设置一个标志，防止循环调用
                        // 原始代码: cts.Token.Register(() => _streamResource.StreamClose());
                        bool timeoutOccurred = false;
                        cts.Token.Register(() => { timeoutOccurred = true; });

                        while (attempt <= _retries)
                        {
                            try
                            {
                                // 如果已超时，终止循环
                                if (timeoutOccurred)
                                {
                                    if (EnableMetricsCollection)
                                    {
                                        Interlocked.Increment(ref _failedReceiveResponses);
                                        Interlocked.Add(ref _totalRetries, totalRetries);
                                    }
                                    return OperationResult.CreateFailedResult<byte[]>("接收操作超时");
                                }
                                
                                var read = await _streamResource.Receive(buffer, offset, length, cts.Token);
                                if (read?.IsSuccess ?? false)
                                {
                                    var result = new byte[length];
                                    Array.Copy(buffer, result, length);
                                    
                                    if (EnableMetricsCollection)
                                    {
                                        stopwatch.Stop();
                                        Interlocked.Increment(ref _successfulReceiveResponses);
                                        Interlocked.Add(ref _totalReceivedBytes, length);
                                        Interlocked.Add(ref _totalRetries, totalRetries);
                                        Interlocked.Add(ref _totalReceiveTime, stopwatch.ElapsedMilliseconds);
                                    }
                                    
                                    return OperationResult.CreateSuccessResult(result);
                                }

                                if (attempt++ > _retries)
                                {
                                    if (EnableMetricsCollection)
                                    {
                                        Interlocked.Increment(ref _failedReceiveResponses);
                                        Interlocked.Add(ref _totalRetries, totalRetries);
                                    }
                                    return OperationResult.CreateFailedResult<byte[]>($"读取设备失败，重试次数：{_retries}，超时参数：{_streamResource.ReceiveTimeout.TotalMilliseconds}ms");
                                }

                                totalRetries++;
                                
                                // 计算退避等待时间，每次重试增加等待时间
                                var backoffDelay = TimeSpan.FromMilliseconds(WaitToRetryMilliseconds.TotalMilliseconds * attempt);
                                await Task.Delay(backoffDelay, cts.Token);
                                
                                Debug.WriteLine($"接收操作重试 {attempt}/{_retries}，等待时间：{backoffDelay.TotalMilliseconds}ms");
                            }
                            catch (OperationCanceledException)
                            {
                                if (EnableMetricsCollection)
                                {
                                    Interlocked.Increment(ref _failedReceiveResponses);
                                    Interlocked.Add(ref _totalRetries, totalRetries);
                                }
                                return OperationResult.CreateFailedResult<byte[]>("操作超时");
                            }
                            catch (ObjectDisposedException)
                            {
                                if (EnableMetricsCollection)
                                {
                                    Interlocked.Increment(ref _failedReceiveResponses);
                                    Interlocked.Add(ref _totalRetries, totalRetries);
                                }
                                return OperationResult.CreateFailedResult<byte[]>("连接已关闭");
                            }
                            catch (Exception e)
                            {
                                if (attempt > _retries)
                                {
                                    if (EnableMetricsCollection)
                                    {
                                        Interlocked.Increment(ref _failedReceiveResponses);
                                        Interlocked.Add(ref _totalRetries, totalRetries);
                                    }
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

                if (EnableMetricsCollection)
                {
                    Interlocked.Increment(ref _failedReceiveResponses);
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
            if (EnableMetricsCollection)
            {
                Interlocked.Increment(ref _totalSendRequests);
            }
            
            var stopwatch = Stopwatch.StartNew();
            
            using (await _asyncLock.LockAsync())
            {
                if (!_streamResource.Connected)
                {
                    if (EnableMetricsCollection)
                    {
                        Interlocked.Increment(ref _failedSendRequests);
                    }
                    return OperationResult.CreateFailedResult("设备未连接");
                }

                int attempt = 1;
                int totalRetries = 0;
                var result = new OperationResult();
                result.Requsts.Add(string.Join(" ", request.Select(t => t.ToString("X2"))));

                using (var cts = new CancellationTokenSource(_streamResource.SendTimeout))
                {
                    // 避免直接调用StreamClose，改为设置一个标志，防止循环调用
                    // 原始代码: cts.Token.Register(() => _streamResource.StreamClose());
                    bool timeoutOccurred = false;
                    cts.Token.Register(() => { timeoutOccurred = true; });

                    while (attempt <= _retries)
                    {
                        try
                        {
                            // 如果已超时，终止循环
                            if (timeoutOccurred)
                            {
                                if (EnableMetricsCollection)
                                {
                                    Interlocked.Increment(ref _failedSendRequests);
                                    Interlocked.Add(ref _totalRetries, totalRetries);
                                }
                                return OperationResult.CreateFailedResult("发送操作超时");
                            }
                            
                            var write = await _streamResource.Send(request, 0, request.Length, cts.Token);
                            if (write?.IsSuccess ?? false)
                            {
                                if (EnableMetricsCollection)
                                {
                                    stopwatch.Stop();
                                    Interlocked.Increment(ref _successfulSendRequests);
                                    Interlocked.Add(ref _totalSentBytes, request.Length);
                                    Interlocked.Add(ref _totalRetries, totalRetries);
                                    Interlocked.Add(ref _totalSendTime, stopwatch.ElapsedMilliseconds);
                                }
                                
                                return OperationResult.CreateSuccessResult(write);
                            }

                            if (attempt++ > _retries)
                            {
                                if (EnableMetricsCollection)
                                {
                                    Interlocked.Increment(ref _failedSendRequests);
                                    Interlocked.Add(ref _totalRetries, totalRetries);
                                }
                                return OperationResult.CreateFailedResult($"写入设备失败，重试次数：{_retries}，超时参数：{_streamResource.SendTimeout.TotalMilliseconds}ms");
                            }

                            totalRetries++;
                            
                            // 计算退避等待时间，每次重试增加等待时间
                            var backoffDelay = TimeSpan.FromMilliseconds(WaitToRetryMilliseconds.TotalMilliseconds * attempt);
                            await Task.Delay(backoffDelay, cts.Token);
                            
                            Debug.WriteLine($"发送操作重试 {attempt}/{_retries}，等待时间：{backoffDelay.TotalMilliseconds}ms");
                        }
                        catch (OperationCanceledException)
                        {
                            if (EnableMetricsCollection)
                            {
                                Interlocked.Increment(ref _failedSendRequests);
                                Interlocked.Add(ref _totalRetries, totalRetries);
                            }
                            return OperationResult.CreateFailedResult("操作超时");
                        }
                        catch (ObjectDisposedException)
                        {
                            if (EnableMetricsCollection)
                            {
                                Interlocked.Increment(ref _failedSendRequests);
                                Interlocked.Add(ref _totalRetries, totalRetries);
                            }
                            return OperationResult.CreateFailedResult("连接已关闭");
                        }
                        catch (Exception e)
                        {
                            if (attempt > _retries)
                            {
                                if (EnableMetricsCollection)
                                {
                                    Interlocked.Increment(ref _failedSendRequests);
                                    Interlocked.Add(ref _totalRetries, totalRetries);
                                }
                                return OperationResult.CreateFailedResult($"操作失败：{e.Message}");
                            }
                            Debug.WriteLine($"发送异常：{e.GetType().Name}，剩余重试次数：{_retries - attempt + 1}，异常信息：{e}");
                        }
                    }
                }

                if (EnableMetricsCollection)
                {
                    Interlocked.Increment(ref _failedSendRequests);
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

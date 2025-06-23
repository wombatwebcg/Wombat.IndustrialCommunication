using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Network.Pipelines
{
    /// <summary>
    /// 基于System.IO.Pipelines的高性能Socket连接实现
    /// </summary>
    public class PipelineSocketConnection : IDisposable
    {
        private readonly Socket _socket;
        private readonly Pipe _receivePipe;
        private readonly Pipe _sendPipe;
        private readonly ILogger _logger;
        private Task _receiveTask;
        private Task _sendTask;
        private readonly CancellationTokenSource _cts;
        private readonly SemaphoreSlim _sendSemaphore;
        private readonly int _maxConcurrentSends;
        private readonly TimeSpan _receiveTimeout;
        private readonly TimeSpan _sendTimeout;
        private bool _disposed;
        
        // SocketAsyncEventArgs对象池
        private SocketAsyncEventArgs _receiveEventArgs;
        private SocketAsyncEventArgs _sendEventArgs;
        private readonly ManualResetEventSlim _receiveDone = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _sendDone = new ManualResetEventSlim(false);
        private Exception _socketError;

        /// <summary>
        /// 创建新的PipelineSocketConnection
        /// </summary>
        /// <param name="socket">底层Socket</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="receiveOptions">接收管道选项</param>
        /// <param name="sendOptions">发送管道选项</param>
        /// <param name="maxConcurrentSends">最大并发发送数</param>
        public PipelineSocketConnection(Socket socket, ILogger logger = null, PipeOptions receiveOptions = null, PipeOptions sendOptions = null, int maxConcurrentSends = 1)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _logger = logger;
            _receivePipe = new Pipe(receiveOptions ?? PipeOptions.Default);
            _sendPipe = new Pipe(sendOptions ?? PipeOptions.Default);
            _cts = new CancellationTokenSource();
            _sendSemaphore = new SemaphoreSlim(maxConcurrentSends, maxConcurrentSends);
            _maxConcurrentSends = maxConcurrentSends;
            _receiveTimeout = TimeSpan.FromSeconds(30);
            _sendTimeout = TimeSpan.FromSeconds(30);
            _disposed = false;
            
            // 初始化SocketAsyncEventArgs
            InitializeSocketAsyncEventArgs();
        }

        /// <summary>
        /// 初始化SocketAsyncEventArgs
        /// </summary>
        private void InitializeSocketAsyncEventArgs()
        {
            // 初始化接收事件参数
            _receiveEventArgs = new SocketAsyncEventArgs();
            _receiveEventArgs.Completed += OnReceiveCompleted;
            
            // 初始化发送事件参数
            _sendEventArgs = new SocketAsyncEventArgs();
            _sendEventArgs.Completed += OnSendCompleted;
        }
        
        /// <summary>
        /// 接收完成回调
        /// </summary>
        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                _socketError = new SocketException((int)e.SocketError);
            }
            _receiveDone.Set();
        }
        
        /// <summary>
        /// 发送完成回调
        /// </summary>
        private void OnSendCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                _socketError = new SocketException((int)e.SocketError);
            }
            _sendDone.Set();
        }

        /// <summary>
        /// 远程端点
        /// </summary>
        public IPEndPoint RemoteEndPoint => _socket.RemoteEndPoint as IPEndPoint;

        /// <summary>
        /// 本地端点
        /// </summary>
        public IPEndPoint LocalEndPoint => _socket.LocalEndPoint as IPEndPoint;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _socket.Connected;
        
        /// <summary>
        /// 接收管道读取器
        /// </summary>
        public PipeReader Input => _receivePipe.Reader;

        /// <summary>
        /// 发送管道写入器
        /// </summary>
        public PipeWriter Output => _sendPipe.Writer;

        /// <summary>
        /// 启动管道处理
        /// </summary>
        public void Start()
        {
            _receiveTask = ReceiveAsync();
            _sendTask = SendAsync();
        }

        /// <summary>
        /// 停止管道处理
        /// </summary>
        /// <returns>异步任务</returns>
        public async Task StopAsync()
        {
            try
            {
                _cts.Cancel();
                
                // 等待发送和接收任务完成
                if (_receiveTask != null)
                {
                    await _receiveTask;
                }
                
                if (_sendTask != null)
                {
                    // 通知管道所有数据已经写入
                    await _sendPipe.Writer.CompleteAsync();
                    await _sendTask;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping pipeline connection");
            }
        }

        private async Task ReceiveAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    // 获取管道中的可写入内存
                    Memory<byte> memory = _receivePipe.Writer.GetMemory(4096);
                    
                    // 创建一个接收超时的取消令牌
                    using (var timeoutCts = new CancellationTokenSource(_receiveTimeout))
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, _cts.Token))
                    {
                        int bytesRead;
                        try
                        {
                            // 使用SocketAsyncEventArgs进行异步接收
                            bytesRead = await ReceiveAsyncWithTimeout(memory, linkedCts.Token);
                            
                            if (bytesRead == 0)
                            {
                                // 连接已关闭
                                break;
                            }
                        }
                        catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
                        {
                            // 正常取消，退出循环
                            break;
                        }
                        catch (TimeoutException)
                        {
                            if (!_cts.Token.IsCancellationRequested)
                            {
                                // 读取超时，继续尝试
                                _logger?.LogWarning("Socket receive operation timed out after {0}ms", _receiveTimeout.TotalMilliseconds);
                                continue;
                            }
                            else
                            {
                                // 已取消，退出循环
                                break;
                            }
                        }
                        
                        // 告诉管道我们写入了多少字节
                        _receivePipe.Writer.Advance(bytesRead);
                        
                        // 使数据对读取者可用
                        FlushResult result = await _receivePipe.Writer.FlushAsync();
                        
                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in receive pipeline");
            }
            finally
            {
                await _receivePipe.Writer.CompleteAsync();
            }
        }
        
        /// <summary>
        /// 带超时的异步接收数据
        /// </summary>
        /// <param name="memory">目标内存</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>接收的字节数</returns>
        private async Task<int> ReceiveAsyncWithTimeout(Memory<byte> memory, CancellationToken cancellationToken)
        {
            // 在.NET Standard 2.0中，我们需要将Memory<byte>转换为byte[]
            byte[] buffer = new byte[memory.Length];
            var segment = new ArraySegment<byte>(buffer);
            
            try
            {
                return await Task.Run(() => 
                {
                    int bytesRead = 0;
                    
                    // 重置信号和错误
                    _receiveDone.Reset();
                    _socketError = null;
                    
                    // 设置接收缓冲区
                    _receiveEventArgs.SetBuffer(buffer, 0, buffer.Length);
                    
                    // 启动异步接收
                    bool pending;
                    
                    try
                    {
                        pending = _socket.ReceiveAsync(_receiveEventArgs);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error starting receive operation");
                        throw;
                    }
                    
                    if (!pending)
                    {
                        // 操作同步完成
                        if (_receiveEventArgs.SocketError != SocketError.Success)
                        {
                            throw new SocketException((int)_receiveEventArgs.SocketError);
                        }
                        
                        bytesRead = _receiveEventArgs.BytesTransferred;
                    }
                    else
                    {
                        // 操作异步进行，等待完成或超时
                        using (var registration = cancellationToken.Register(() => _receiveDone.Set()))
                        {
                            // 等待操作完成或取消
                            if (!_receiveDone.Wait(_receiveTimeout))
                            {
                                throw new TimeoutException($"Socket receive operation timed out after {_receiveTimeout.TotalMilliseconds}ms");
                            }
                            
                            // 如果已取消，抛出取消异常
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // 检查错误
                            if (_socketError != null)
                            {
                                throw _socketError;
                            }
                            
                            bytesRead = _receiveEventArgs.BytesTransferred;
                        }
                    }
                    
                    // 将数据从缓冲区复制到目标内存
                    if (bytesRead > 0)
                    {
                        new Span<byte>(buffer, 0, bytesRead).CopyTo(memory.Span);
                    }
                    
                    return bytesRead;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in receive operation: {0}", ex.Message);
                throw;
            }
        }

        private async Task SendAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    // 等待有数据可发送
                    ReadResult result = await _sendPipe.Reader.ReadAsync(_cts.Token);
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    
                    if (buffer.IsEmpty && result.IsCompleted)
                    {
                        break;
                    }
                    
                    try
                    {
                        // 发送缓冲区中的数据
                        foreach (ReadOnlyMemory<byte> segment in buffer)
                        {
                            // 创建一个发送超时的取消令牌
                            using (var timeoutCts = new CancellationTokenSource(_sendTimeout))
                            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, _cts.Token))
                            {
                                try
                                {
                                    // 使用SocketAsyncEventArgs进行异步发送
                                    await SendAsyncWithTimeout(segment, linkedCts.Token);
                                }
                                catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
                                {
                                    // 正常取消，退出循环
                                    break;
                                }
                                catch (TimeoutException)
                                {
                                    if (linkedCts.Token.IsCancellationRequested && !_cts.Token.IsCancellationRequested)
                                    {
                                        throw new TimeoutException($"Socket send operation timed out after {_sendTimeout.TotalMilliseconds}ms");
                                    }
                                    
                                    if (_cts.Token.IsCancellationRequested)
                                    {
                                        // 正常取消，退出循环
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error sending data: {0}", ex.Message);
                    }
                    
                    // 标记处理过的数据
                    _sendPipe.Reader.AdvanceTo(buffer.End);
                    
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in send pipeline");
            }
            finally
            {
                await _sendPipe.Reader.CompleteAsync();
            }
        }
        
        /// <summary>
        /// 带超时的异步发送数据
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        private async Task SendAsyncWithTimeout(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            // 在.NET Standard 2.0中，我们需要将ReadOnlyMemory<byte>转换为byte[]
            byte[] buffer = new byte[data.Length];
            data.CopyTo(new Memory<byte>(buffer));
            
            try
            {
                await Task.Run(() => 
                {
                    // 重置信号和错误
                    _sendDone.Reset();
                    _socketError = null;
                    
                    // 设置发送缓冲区
                    _sendEventArgs.SetBuffer(buffer, 0, buffer.Length);
                    
                    // 启动异步发送
                    bool pending;
                    
                    try 
                    {
                        pending = _socket.SendAsync(_sendEventArgs);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error starting send operation");
                        throw;
                    }
                    
                    if (!pending)
                    {
                        // 操作同步完成
                        if (_sendEventArgs.SocketError != SocketError.Success)
                        {
                            throw new SocketException((int)_sendEventArgs.SocketError);
                        }
                    }
                    else
                    {
                        // 操作异步进行，等待完成或超时
                        using (var registration = cancellationToken.Register(() => _sendDone.Set()))
                        {
                            // 等待操作完成或取消
                            if (!_sendDone.Wait(_sendTimeout))
                            {
                                throw new TimeoutException($"Socket send operation timed out after {_sendTimeout.TotalMilliseconds}ms");
                            }
                            
                            // 如果已取消，抛出取消异常
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // 检查错误
                            if (_socketError != null)
                            {
                                throw _socketError;
                            }
                        }
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in send operation: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 直接发送数据，绕过管道
        /// </summary>
        /// <param name="data">要发送的数据</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送的字节数</returns>
        public async Task<int> SendDataAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Socket is not connected");
            }
            
            // 合并取消令牌
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken))
            {
                // 限制并发发送
                await _sendSemaphore.WaitAsync(linkedCts.Token);
                try
                {
                    // 创建一个发送超时的取消令牌
                    using (var timeoutCts = new CancellationTokenSource(_sendTimeout))
                    using (var timeoutLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, linkedCts.Token))
                    {
                        // 在.NET Standard 2.0中，我们需要将ReadOnlyMemory<byte>转换为byte[]
                        byte[] buffer = new byte[data.Length];
                        data.CopyTo(new Memory<byte>(buffer));
                        
                        return await Task.Run(() => 
                        {
                            int bytesSent = 0;
                            
                            // 重置信号和错误
                            _sendDone.Reset();
                            _socketError = null;
                            
                            // 设置发送缓冲区
                            _sendEventArgs.SetBuffer(buffer, 0, buffer.Length);
                            
                            // 启动异步发送
                            bool pending;
                            
                            try 
                            {
                                pending = _socket.SendAsync(_sendEventArgs);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error starting send operation");
                                throw;
                            }
                            
                            if (!pending)
                            {
                                // 操作同步完成
                                if (_sendEventArgs.SocketError != SocketError.Success)
                                {
                                    throw new SocketException((int)_sendEventArgs.SocketError);
                                }
                                
                                bytesSent = _sendEventArgs.BytesTransferred;
                            }
                            else
                            {
                                // 操作异步进行，等待完成或超时
                                using (var registration = timeoutLinkedCts.Token.Register(() => _sendDone.Set()))
                                {
                                    // 等待操作完成或取消
                                    if (!_sendDone.Wait(_sendTimeout))
                                    {
                                        throw new TimeoutException($"Socket send operation timed out after {_sendTimeout.TotalMilliseconds}ms");
                                    }
                                    
                                    // 如果已取消，抛出取消异常
                                    timeoutLinkedCts.Token.ThrowIfCancellationRequested();
                                    
                                    // 检查错误
                                    if (_socketError != null)
                                    {
                                        throw _socketError;
                                    }
                                    
                                    bytesSent = _sendEventArgs.BytesTransferred;
                                }
                            }
                            
                            return bytesSent;
                        }, timeoutLinkedCts.Token);
                    }
                }
                finally
                {
                    _sendSemaphore.Release();
                }
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

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在主动释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _sendSemaphore.Dispose();
                    _receiveDone.Dispose();
                    _sendDone.Dispose();
                    
                    if (_receiveEventArgs != null)
                    {
                        _receiveEventArgs.Completed -= OnReceiveCompleted;
                        _receiveEventArgs.Dispose();
                    }
                    
                    if (_sendEventArgs != null)
                    {
                        _sendEventArgs.Completed -= OnSendCompleted;
                        _sendEventArgs.Dispose();
                    }
                    
                    try
                    {
                        if (_socket.Connected)
                        {
                            _socket.Shutdown(SocketShutdown.Both);
                        }
                    }
                    catch
                    {
                        // 忽略关闭时的错误
                    }
                    finally
                    {
                        _socket.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing pipeline connection");
                }
            }

            _disposed = true;
        }
    }
} 
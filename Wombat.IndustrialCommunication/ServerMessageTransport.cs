using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 服务器消息传输类，负责处理服务器与客户端的通信
    /// </summary>
    public class ServerMessageTransport : IDisposable
    {
        // 流资源接口
        private readonly IStreamResource _streamResource;
        // 异步锁，确保并发安全
        private readonly AsyncLock _asyncLock = new AsyncLock();
        // 是否已释放
        private bool _disposed;
        // 用于响应的等待时间
        private TimeSpan _responseWaitTime = TimeSpan.FromMilliseconds(500);
        // 会话字典，用于跟踪客户端会话
        private readonly Dictionary<INetworkSession, ClientState> _sessionStates = new Dictionary<INetworkSession, ClientState>();
        // 会话字典的锁
        private readonly AsyncLock _sessionLock = new AsyncLock();
        // 消息队列
        private readonly Queue<ReceivedMessage> _messageQueue = new Queue<ReceivedMessage>();
        // 消息队列的锁
        private readonly AsyncLock _messageLock = new AsyncLock();
        // 消息处理程序
        private Action<ReceivedMessage> _messageHandler;
        // 消息处理是否启用
        private bool _messageProcessingEnabled;
        // 消息处理取消令牌
        private CancellationTokenSource _messageCts;
        // 消息处理任务
        private Task _messageProcessingTask;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="streamResource">流资源接口</param>
        public ServerMessageTransport(IStreamResource streamResource)
        {
            _streamResource = streamResource ?? throw new ArgumentNullException(nameof(streamResource));
            
            // 注册事件处理程序
            if (_streamResource is TcpServerAdapter serverAdapter)
            {
                serverAdapter.DataReceived += OnDataReceived;
                serverAdapter.ClientConnected += OnClientConnected;
                serverAdapter.ClientDisconnected += OnClientDisconnected;
            }
        }

        /// <summary>
        /// 获取流资源
        /// </summary>
        public IStreamResource StreamResource => _streamResource;

        /// <summary>
        /// 获取或设置响应等待时间
        /// </summary>
        public TimeSpan ResponseWaitTime
        {
            get => _responseWaitTime;
            set => _responseWaitTime = value;
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> StartAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServerMessageTransport));
            
            // 启动消息处理
            StartMessageProcessing();
            
            // 启动监听
            if (_streamResource is TcpServerAdapter serverAdapter)
            {
                return await serverAdapter.ListenAsync();
            }
            
            return OperationResult.CreateSuccessResult();
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> StopAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServerMessageTransport));
            
            // 停止消息处理
            StopMessageProcessing();
            
            // 停止监听
            if (_streamResource is TcpServerAdapter serverAdapter)
            {
                return await serverAdapter.ShutdownAsync();
            }
            
            return OperationResult.CreateSuccessResult();
        }

        /// <summary>
        /// 向所有客户端发送数据
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> BroadcastAsync(byte[] data)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServerMessageTransport));
            
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            
            return await _streamResource.Send(data, 0, data.Length, CancellationToken.None);
        }

        /// <summary>
        /// 向特定会话发送数据
        /// </summary>
        /// <param name="session">会话</param>
        /// <param name="data">数据</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> SendToSessionAsync(INetworkSession session, byte[] data)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServerMessageTransport));
            
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            
            try
            {
                return await session.SendAsync(data);
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult(ex);
            }
        }

        /// <summary>
        /// 注册消息处理程序
        /// </summary>
        /// <param name="handler">处理程序</param>
        public void RegisterMessageHandler(Action<ReceivedMessage> handler)
        {
            _messageHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// 启动消息处理
        /// </summary>
        private void StartMessageProcessing()
        {
            if (_messageProcessingEnabled)
                return;
            
            _messageProcessingEnabled = true;
            _messageCts = new CancellationTokenSource();
            _messageProcessingTask = Task.Run(MessageProcessingLoop, _messageCts.Token);
        }

        /// <summary>
        /// 停止消息处理
        /// </summary>
        private void StopMessageProcessing()
        {
            if (!_messageProcessingEnabled)
                return;
            
            _messageProcessingEnabled = false;
            _messageCts?.Cancel();
            
            try
            {
                _messageProcessingTask?.Wait(1000);
            }
            catch (Exception)
            {
                // 忽略任务取消异常
            }
            
            _messageCts?.Dispose();
            _messageCts = null;
            _messageProcessingTask = null;
        }

        /// <summary>
        /// 消息处理循环
        /// </summary>
        private async Task MessageProcessingLoop()
        {
            while (_messageProcessingEnabled && !_messageCts.Token.IsCancellationRequested)
            {
                ReceivedMessage message = null;
                
                // 从队列中获取消息
                using (await _messageLock.LockAsync())
                {
                    if (_messageQueue.Count > 0)
                    {
                        message = _messageQueue.Dequeue();
                    }
                }
                
                // 处理消息
                if (message != null)
                {
                    try
                    {
                        _messageHandler?.Invoke(message);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"处理消息时发生错误: {ex.Message}");
                    }
                }
                else
                {
                    // 如果队列为空，等待一段时间
                    await Task.Delay(10);
                }
            }
        }

        /// <summary>
        /// 客户端连接事件处理程序
        /// </summary>
        private async void OnClientConnected(object sender, SessionEventArgs e)
        {
            using (await _sessionLock.LockAsync())
            {
                _sessionStates[e.Session] = new ClientState();
            }
        }

        /// <summary>
        /// 客户端断开连接事件处理程序
        /// </summary>
        private async void OnClientDisconnected(object sender, SessionEventArgs e)
        {
            using (await _sessionLock.LockAsync())
            {
                _sessionStates.Remove(e.Session);
            }
        }

        /// <summary>
        /// 数据接收事件处理程序
        /// </summary>
        private async void OnDataReceived(object sender, DataReceivedEventArgs e)
        {
            // 将接收到的数据添加到消息队列
            var message = new ReceivedMessage
            {
                Session = e.Session,
                Data = e.Data,
                Timestamp = DateTime.Now
            };
            
            using (await _messageLock.LockAsync())
            {
                _messageQueue.Enqueue(message);
            }
        }

        /// <summary>
        /// 处理 Modbus 请求（通过反射处理）
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="session">会话</param>
        /// <returns>处理结果</returns>
        public async Task<OperationResult> ProcessModbusRequestAsync(byte[] request, INetworkSession session)
        {
            if (request == null || request.Length < 7)
            {
                return OperationResult.CreateFailedResult("Invalid Modbus request");
            }
            
            try
            {
                // 解析基本Modbus头信息
                ushort transactionId = (ushort)((request[0] << 8) | request[1]);
                byte unitId = request[6];
                byte functionCode = request[7];
                
                // 这里可以添加更多的请求处理逻辑
                // 例如，根据功能码调用不同的处理方法
                
                // 作为示例，我们返回一个成功的结果
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"Error processing Modbus request: {ex.Message}");
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
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 停止消息处理
                    StopMessageProcessing();
                    
                    // 取消事件注册
                    if (_streamResource is TcpServerAdapter serverAdapter)
                    {
                        serverAdapter.DataReceived -= OnDataReceived;
                        serverAdapter.ClientConnected -= OnClientConnected;
                        serverAdapter.ClientDisconnected -= OnClientDisconnected;
                    }
                    
                    // 释放资源
                    _messageCts?.Dispose();
                }
                
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 客户端状态类
    /// </summary>
    public class ClientState
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public ClientState()
        {
            ConnectedTime = DateTime.Now;
            LastActivityTime = DateTime.Now;
        }

        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime ConnectedTime { get; }

        /// <summary>
        /// 最后活动时间
        /// </summary>
        public DateTime LastActivityTime { get; set; }

        /// <summary>
        /// 发送的字节数
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// 接收的字节数
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// 消息计数
        /// </summary>
        public int MessageCount { get; set; }
    }

    /// <summary>
    /// 接收到的消息
    /// </summary>
    public class ReceivedMessage
    {
        /// <summary>
        /// 会话
        /// </summary>
        public INetworkSession Session { get; set; }

        /// <summary>
        /// 数据
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
} 
# Wombat.Network API参考文档

## 目录

- [核心组件概述](#核心组件概述)
- [TCP Socket客户端/服务器接口](#tcp-socket客户端服务器接口)
- [WebSocket客户端/服务器接口](#websocket客户端服务器接口)
- [Pipelines高性能连接接口](#pipelines高性能连接接口)
- [帧构建器和缓冲区接口](#帧构建器和缓冲区接口)
- [通用结果和状态接口](#通用结果和状态接口)
- [配置选项](#配置选项)
- [使用示例](#使用示例)

## 核心组件概述

Wombat.Network是一个高性能、功能丰富的.NET网络通信库，提供了以下主要组件和接口：

### 主要命名空间

- `Wombat.Network` - 基础命名空间，包含通用接口和组件
- `Wombat.Network.Sockets` - TCP Socket相关组件
- `Wombat.Network.WebSockets` - WebSocket相关组件
- `Wombat.Network.Pipelines` - 基于System.IO.Pipelines的高性能组件

### 核心功能组件

1. **TCP Socket客户端/服务器**
   - `TcpSocketClient` - TCP客户端实现
   - `TcpSocketServer` - TCP服务器实现
   - `ITcpSocketClientEventDispatcher` - TCP客户端事件分发器接口

2. **WebSocket客户端/服务器**
   - `WebSocketClient` - WebSocket客户端实现
   - `WebSocketServer` - WebSocket服务器实现
   - `IWebSocketClientMessageDispatcher` - WebSocket客户端消息分发器接口

3. **高性能Pipelines组件**
   - `PipelineSocketConnection` - 基于System.IO.Pipelines的Socket连接实现

4. **帧构建器**
   - `IFrameBuilder` - 帧构建器接口
   - `LengthPrefixedFrameBuilder` - 长度前缀帧构建器
   - `LineBasedFrameBuilder` - 基于行分隔符的帧构建器
   - `LengthFieldBasedFrameBuilder` - 基于长度字段的帧构建器

5. **缓冲区管理**
   - `ISegmentBufferManager` - 分段缓冲区管理器接口
   - `SegmentBufferManager` - 默认分段缓冲区管理器实现

6. **通用组件**
   - `CommunicationResult<T>` - 通信结果类，支持同步和异步等待
   - `CommunicationResultStatus` - 通信结果状态枚举 

## TCP Socket客户端/服务器接口

### TcpSocketClient

TCP Socket客户端实现，用于创建与服务器的TCP连接。

#### 构造函数

```csharp
// 使用远程地址和端口创建客户端
public TcpSocketClient(IPAddress remoteAddress, int remotePort, 
    ITcpSocketClientEventDispatcher dispatcher, 
    TcpSocketClientConfiguration configuration = null)

// 使用远程和本地地址端口创建客户端
public TcpSocketClient(IPAddress remoteAddress, int remotePort, 
    IPAddress localAddress, int localPort, 
    ITcpSocketClientEventDispatcher dispatcher, 
    TcpSocketClientConfiguration configuration = null)

// 使用远程终结点创建客户端
public TcpSocketClient(IPEndPoint remoteEP, 
    ITcpSocketClientEventDispatcher dispatcher, 
    TcpSocketClientConfiguration configuration = null)

// 使用远程和本地终结点创建客户端
public TcpSocketClient(IPEndPoint remoteEP, IPEndPoint localEP, 
    ITcpSocketClientEventDispatcher dispatcher, 
    TcpSocketClientConfiguration configuration = null)

// 使用委托方式创建客户端
public TcpSocketClient(IPAddress remoteAddress, int remotePort,
    Func<TcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
    Func<TcpSocketClient, Task> onServerConnected = null,
    Func<TcpSocketClient, Task> onServerDisconnected = null,
    TcpSocketClientConfiguration configuration = null)
```

#### 属性

```csharp
// 客户端配置
public TcpSocketClientConfiguration TcpSocketClientConfiguration { get; }

// 是否已连接
public bool Connected { get; }

// 远程终结点
public IPEndPoint RemoteEndPoint { get; }

// 本地终结点
public IPEndPoint LocalEndPoint { get; }

// 连接状态
public TcpSocketConnectionState State { get; }
```

#### 方法

```csharp
// 连接到服务器
public Task Connect(CancellationToken cancellationToken = default)

// 关闭连接
public Task Close()

// 立即关闭连接
public void Shutdown()

// 发送数据
public Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
public Task SendAsync(byte[] data, int offset, int count, CancellationToken cancellationToken = default)

// 使用日志记录器
public void UseLogger(ILogger logger)
```

### ITcpSocketClientEventDispatcher

TCP Socket客户端事件分发器接口，用于处理TCP客户端的各种事件。

```csharp
public interface ITcpSocketClientEventDispatcher
{
    // 当连接到服务器时调用
    Task OnServerConnected(TcpSocketClient client);

    // 当从服务器接收到数据时调用
    Task OnServerDataReceived(TcpSocketClient client, byte[] data, int offset, int count);

    // 当与服务器断开连接时调用
    Task OnServerDisconnected(TcpSocketClient client);
}
```

### TcpSocketServer

TCP Socket服务器实现，用于创建TCP服务器并接受客户端连接。

#### 构造函数

```csharp
// 使用本地地址和端口创建服务器
public TcpSocketServer(IPAddress localAddress, int localPort,
    ITcpSocketServerEventDispatcher dispatcher,
    TcpSocketServerConfiguration configuration = null)

// 使用本地终结点创建服务器
public TcpSocketServer(IPEndPoint localEP,
    ITcpSocketServerEventDispatcher dispatcher,
    TcpSocketServerConfiguration configuration = null)

// 使用委托方式创建服务器
public TcpSocketServer(IPAddress localAddress, int localPort,
    Func<TcpSocketServer, TcpSocketSession, Task> onClientConnected = null,
    Func<TcpSocketServer, TcpSocketSession, byte[], int, int, Task> onClientDataReceived = null,
    Func<TcpSocketServer, TcpSocketSession, Task> onClientDisconnected = null,
    TcpSocketServerConfiguration configuration = null)
```

#### 属性

```csharp
// 服务器配置
public TcpSocketServerConfiguration TcpSocketServerConfiguration { get; }

// 是否正在运行
public bool IsRunning { get; }

// 本地终结点
public IPEndPoint LocalEndPoint { get; }

// 当前会话数量
public int SessionCount { get; }

// 当前活动会话
public IEnumerable<TcpSocketSession> Sessions { get; }
```

#### 方法

```csharp
// 启动服务器
public Task Start(CancellationToken cancellationToken = default)

// 停止服务器
public Task Stop()

// 立即停止服务器
public void Shutdown()

// 使用日志记录器
public void UseLogger(ILogger logger)

// 获取指定ID的会话
public TcpSocketSession GetSessionById(string sessionId)
```

### ITcpSocketServerEventDispatcher

TCP Socket服务器事件分发器接口，用于处理TCP服务器的各种事件。

```csharp
public interface ITcpSocketServerEventDispatcher
{
    // 当客户端连接时调用
    Task OnClientConnected(TcpSocketServer server, TcpSocketSession session);

    // 当从客户端接收到数据时调用
    Task OnClientDataReceived(TcpSocketServer server, TcpSocketSession session, byte[] data, int offset, int count);

    // 当客户端断开连接时调用
    Task OnClientDisconnected(TcpSocketServer server, TcpSocketSession session);
}
```

### TcpSocketSession

表示TCP服务器中的客户端会话。

#### 属性

```csharp
// 会话ID
public string Id { get; }

// 远程终结点
public IPEndPoint RemoteEndPoint { get; }

// 是否已连接
public bool Connected { get; }

// 用户自定义数据
public object UserToken { get; set; }

// 会话开始时间
public DateTime StartTime { get; }

// 最后活动时间
public DateTime LastActiveTime { get; }
```

#### 方法

```csharp
// 发送数据
public Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
public Task SendAsync(byte[] data, int offset, int count, CancellationToken cancellationToken = default)

// 关闭会话
public Task Close()

// 立即关闭会话
public void Shutdown()
```

### TcpSocketConnectionState

表示TCP Socket连接的状态。

```csharp
public enum TcpSocketConnectionState
{
    // 初始状态
    None,
    
    // 正在连接
    Connecting,
    
    // 已连接
    Connected,
    
    // 已关闭
    Closed
}
```

## 通用结果和状态接口

### CommunicationResult&lt;T&gt;

通信结果类，用于表示通信操作的结果。支持同步和异步等待，并支持取消和超时功能。

#### 构造函数

```csharp
// 创建新的通信结果对象
public CommunicationResult()
```

#### 属性

```csharp
// 当前通信的状态
public CommunicationResultStatus Status { get; }

// 通信结果
public T Result { get; }
```

#### 方法

```csharp
// 取消当前的等待操作
public void Cancel()

// 重置状态和结果，使对象可以被重新使用
public void Reset()

// 设置状态为完成，并解除所有等待线程
public bool Set()

// 设置结果并解除所有等待线程
public bool Set(T result)

// 设置取消令牌，使等待可以被取消
public void SetCancellationToken(CancellationToken cancellationToken)

// 同步等待指定时间
public CommunicationResultStatus Wait(TimeSpan timeout)

// 同步等待指定毫秒数
public CommunicationResultStatus Wait(int timeoutMilliseconds)

// 异步等待结果
public Task<T> WaitAsync(CancellationToken cancellationToken = default)

// 释放资源
public void Dispose()
```

#### 使用示例

```csharp
// 创建通信结果对象
var result = new CommunicationResult<byte[]>();

// 设置取消令牌
var cts = new CancellationTokenSource();
result.SetCancellationToken(cts.Token);

// 在另一个线程中等待结果
Task.Run(async () => {
    try {
        byte[] data = await result.WaitAsync();
        Console.WriteLine($"Received {data.Length} bytes");
    }
    catch (OperationCanceledException) {
        Console.WriteLine("Operation was canceled");
    }
});

// 在某个时刻设置结果
byte[] response = GetResponseData();
result.Set(response);

// 或者取消操作
cts.Cancel();
```

### CommunicationResultStatus

通信结果状态枚举，用于表示通信操作的状态。

```csharp
public enum CommunicationResultStatus
{
    // 默认状态，表示尚未开始等待或设置结果
    Default,

    // 等待中，表示当前对象正在等待一个结果
    Waiting,

    // 已完成，表示等待的结果已成功设置
    Completed,

    // 超时，表示等待超过了指定的时间
    Timeout,

    // 被取消，表示等待被取消了
    Canceled,

    // 已被释放，表示对象已被释放，无法继续使用
    Disposed
}
```

## 配置选项

### TcpSocketClientConfiguration

TCP Socket客户端配置选项，用于配置TCP Socket客户端的行为。

#### 构造函数

```csharp
// 创建默认的TCP Socket客户端配置
public TcpSocketClientConfiguration()

// 使用指定的缓冲区管理器创建TCP Socket客户端配置
public TcpSocketClientConfiguration(ISegmentBufferManager bufferManager)
```

#### 属性

```csharp
// 缓冲区管理器
public ISegmentBufferManager BufferManager { get; set; }

// 接收缓冲区大小(字节)
public int ReceiveBufferSize { get; set; }

// 发送缓冲区大小(字节)
public int SendBufferSize { get; set; }

// 是否禁用Nagle算法
public bool NoDelay { get; set; }

// Socket关闭时的行为
public LingerOption LingerState { get; set; }

// 是否启用TCP保活
public bool KeepAlive { get; set; }

// TCP保活间隔
public TimeSpan KeepAliveInterval { get; set; }

// 是否允许地址重用
public bool ReuseAddress { get; set; }

// 是否启用SSL/TLS
public bool SslEnabled { get; set; }

// SSL/TLS目标主机名
public string SslTargetHost { get; set; }

// SSL/TLS客户端证书集合
public X509CertificateCollection SslClientCertificates { get; set; }

// SSL/TLS加密策略
public EncryptionPolicy SslEncryptionPolicy { get; set; }

// SSL/TLS启用的协议
public SslProtocols SslEnabledProtocols { get; set; }

// 是否检查SSL/TLS证书吊销
public bool SslCheckCertificateRevocation { get; set; }

// 是否绕过SSL/TLS策略错误
public bool SslPolicyErrorsBypassed { get; set; }

// 连接超时时间
public TimeSpan ConnectTimeout { get; set; }

// 接收超时时间
public TimeSpan ReceiveTimeout { get; set; }

// 发送超时时间
public TimeSpan SendTimeout { get; set; }

// 帧构建器
public IFrameBuilder FrameBuilder { get; set; }

// 一般操作超时时间
public TimeSpan OperationTimeout { get; set; }

// 是否启用System.IO.Pipelines进行高性能I/O
public bool EnablePipelineIo { get; set; }

// 最大并发连接数
public int MaxConcurrentConnections { get; set; }

// 每个连接的最大并发操作数
public int MaxConcurrentOperations { get; set; }
```

#### 默认值

```csharp
// 缓冲区管理器: 新的SegmentBufferManager实例(100, 8192, 1, true)
// 接收缓冲区大小: 8192字节
// 发送缓冲区大小: 8192字节
// 是否禁用Nagle算法: true
// Socket关闭时的行为: new LingerOption(false, 0)
// 是否启用TCP保活: false
// TCP保活间隔: 5秒
// 是否允许地址重用: false
// 是否启用SSL/TLS: false
// SSL/TLS目标主机名: null
// SSL/TLS客户端证书集合: 空集合
// SSL/TLS加密策略: EncryptionPolicy.RequireEncryption
// SSL/TLS启用的协议: SslProtocols.Ssl3 | SslProtocols.Tls
// 是否检查SSL/TLS证书吊销: false
// 是否绕过SSL/TLS策略错误: false
// 连接超时时间: 30秒
// 接收超时时间: 30秒
// 发送超时时间: 30秒
// 帧构建器: new LengthPrefixedFrameBuilder()
// 一般操作超时时间: 30秒
// 是否启用System.IO.Pipelines: true
// 最大并发连接数: 100
// 每个连接的最大并发操作数: 10
```

### TcpSocketServerConfiguration

TCP Socket服务器配置选项，用于配置TCP Socket服务器的行为。

#### 构造函数

```csharp
// 创建默认的TCP Socket服务器配置
public TcpSocketServerConfiguration()

// 使用指定的缓冲区管理器创建TCP Socket服务器配置
public TcpSocketServerConfiguration(ISegmentBufferManager bufferManager)
```

#### 属性

```csharp
// 缓冲区管理器
public ISegmentBufferManager BufferManager { get; set; }

// 最大连接数
public int MaxConnections { get; set; }

// 最大并行连接处理数
public int MaxPendingConnections { get; set; }

// 接收缓冲区大小(字节)
public int ReceiveBufferSize { get; set; }

// 发送缓冲区大小(字节)
public int SendBufferSize { get; set; }

// 是否禁用Nagle算法
public bool NoDelay { get; set; }

// Socket关闭时的行为
public LingerOption LingerState { get; set; }

// 是否启用TCP保活
public bool KeepAlive { get; set; }

// TCP保活间隔
public TimeSpan KeepAliveInterval { get; set; }

// 是否允许地址重用
public bool ReuseAddress { get; set; }

// 是否启用SSL/TLS
public bool SslEnabled { get; set; }

// SSL/TLS服务器证书
public X509Certificate SslServerCertificate { get; set; }

// SSL/TLS客户端证书验证回调
public RemoteCertificateValidationCallback SslClientCertificateValidationCallback { get; set; }

// SSL/TLS启用的协议
public SslProtocols SslEnabledProtocols { get; set; }

// 是否检查SSL/TLS证书吊销
public bool SslCheckCertificateRevocation { get; set; }

// 是否绕过SSL/TLS策略错误
public bool SslPolicyErrorsBypassed { get; set; }

// 连接超时时间
public TimeSpan ConnectTimeout { get; set; }

// 接收超时时间
public TimeSpan ReceiveTimeout { get; set; }

// 发送超时时间
public TimeSpan SendTimeout { get; set; }

// 帧构建器
public IFrameBuilder FrameBuilder { get; set; }

// 是否启用System.IO.Pipelines进行高性能I/O
public bool EnablePipelineIo { get; set; }

// 最大并发操作数
public int MaxConcurrentOperations { get; set; }
```

### WebSocketClientConfiguration

WebSocket客户端配置选项，用于配置WebSocket客户端的行为。

#### 构造函数

```csharp
// 创建默认的WebSocket客户端配置
public WebSocketClientConfiguration()

// 使用指定的缓冲区管理器创建WebSocket客户端配置
public WebSocketClientConfiguration(ISegmentBufferManager bufferManager)
```

#### 属性

```csharp
// 缓冲区管理器
public ISegmentBufferManager BufferManager { get; set; }

// 接收缓冲区大小(字节)
public int ReceiveBufferSize { get; set; }

// 发送缓冲区大小(字节)
public int SendBufferSize { get; set; }

// 接收超时时间
public TimeSpan ReceiveTimeout { get; set; }

// 发送超时时间
public TimeSpan SendTimeout { get; set; }

// 连接超时时间
public TimeSpan ConnectTimeout { get; set; }

// 关闭超时时间
public TimeSpan CloseTimeout { get; set; }

// 保活间隔时间
public TimeSpan KeepAliveInterval { get; set; }

// 保活超时时间
public TimeSpan KeepAliveTimeout { get; set; }

// 启用的WebSocket扩展
public IDictionary<string, IWebSocketExtensionNegotiator> EnabledExtensions { get; }

// 启用的WebSocket子协议
public IDictionary<string, IWebSocketSubProtocolNegotiator> EnabledSubProtocols { get; }

// 提供的WebSocket扩展
public IList<WebSocketExtensionOfferDescription> OfferedExtensions { get; }

// 请求的WebSocket子协议
public IList<WebSocketSubProtocolRequestDescription> RequestedSubProtocols { get; }
```

### WebSocketServerConfiguration

WebSocket服务器配置选项，用于配置WebSocket服务器的行为。

#### 构造函数

```csharp
// 创建默认的WebSocket服务器配置
public WebSocketServerConfiguration()

// 使用指定的缓冲区管理器创建WebSocket服务器配置
public WebSocketServerConfiguration(ISegmentBufferManager bufferManager)
```

#### 属性

```csharp
// 缓冲区管理器
public ISegmentBufferManager BufferManager { get; set; }

// 最大连接数
public int MaxConnections { get; set; }

// 最大并行连接处理数
public int MaxPendingConnections { get; set; }

// 接收缓冲区大小(字节)
public int ReceiveBufferSize { get; set; }

// 发送缓冲区大小(字节)
public int SendBufferSize { get; set; }

// 接收超时时间
public TimeSpan ReceiveTimeout { get; set; }

// 发送超时时间
public TimeSpan SendTimeout { get; set; }

// 连接超时时间
public TimeSpan ConnectTimeout { get; set; }

// 关闭超时时间
public TimeSpan CloseTimeout { get; set; }

// 是否启用SSL/TLS
public bool SslEnabled { get; set; }

// SSL/TLS服务器证书
public X509Certificate SslServerCertificate { get; set; }

// SSL/TLS客户端证书验证回调
public RemoteCertificateValidationCallback SslClientCertificateValidationCallback { get; set; }

// SSL/TLS启用的协议
public SslProtocols SslEnabledProtocols { get; set; }

// 是否检查SSL/TLS证书吊销
public bool SslCheckCertificateRevocation { get; set; }

// 是否绕过SSL/TLS策略错误
public bool SslPolicyErrorsBypassed { get; set; }

// 启用的WebSocket扩展
public IDictionary<string, IWebSocketExtensionNegotiator> EnabledExtensions { get; }

// 启用的WebSocket子协议
public IDictionary<string, IWebSocketSubProtocolNegotiator> EnabledSubProtocols { get; }
```

## 使用示例

### 示例1：创建TCP Socket客户端

以下示例演示如何创建和使用TCP Socket客户端：

```csharp
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wombat.Network;
using Wombat.Network.Sockets;

namespace WombatNetworkDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 创建TCP Socket客户端配置
            var config = new TcpSocketClientConfiguration
            {
                ReceiveBufferSize = 16384,
                SendBufferSize = 16384,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                FrameBuilder = new LengthPrefixedFrameBuilder()
            };

            // 创建TCP Socket客户端
            var client = new TcpSocketClient(
                IPAddress.Parse("127.0.0.1"), 8080,
                onServerConnected: OnConnected,
                onServerDataReceived: OnDataReceived,
                onServerDisconnected: OnDisconnected,
                configuration: config);

            try
            {
                // 连接到服务器
                Console.WriteLine("正在连接到服务器...");
                await client.Connect();
                Console.WriteLine("已连接到服务器！");

                // 发送数据
                var message = "Hello, Server!";
                var data = Encoding.UTF8.GetBytes(message);
                await client.SendAsync(data);
                Console.WriteLine($"已发送: {message}");

                // 等待用户输入退出
                Console.WriteLine("按Enter键退出...");
                Console.ReadLine();

                // 关闭连接
                await client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }

        static Task OnConnected(TcpSocketClient client)
        {
            Console.WriteLine($"已连接到服务器 {client.RemoteEndPoint}");
            return Task.CompletedTask;
        }

        static Task OnDataReceived(TcpSocketClient client, byte[] data, int offset, int count)
        {
            var message = Encoding.UTF8.GetString(data, offset, count);
            Console.WriteLine($"收到数据: {message}");
            return Task.CompletedTask;
        }

        static Task OnDisconnected(TcpSocketClient client)
        {
            Console.WriteLine("已与服务器断开连接");
            return Task.CompletedTask;
        }
    }
}
```

### 示例2：创建TCP Socket服务器

以下示例演示如何创建和使用TCP Socket服务器：

```csharp
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wombat.Network;
using Wombat.Network.Sockets;

namespace WombatNetworkDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 创建TCP Socket服务器配置
            var config = new TcpSocketServerConfiguration
            {
                MaxConnections = 100,
                ReceiveBufferSize = 16384,
                SendBufferSize = 16384,
                FrameBuilder = new LengthPrefixedFrameBuilder()
            };

            // 创建TCP Socket服务器
            var server = new TcpSocketServer(
                IPAddress.Any, 8080,
                onClientConnected: OnClientConnected,
                onClientDataReceived: OnClientDataReceived,
                onClientDisconnected: OnClientDisconnected,
                configuration: config);

            try
            {
                // 启动服务器
                Console.WriteLine("正在启动服务器...");
                await server.Start();
                Console.WriteLine($"服务器已启动，监听于 {server.LocalEndPoint}");

                // 等待用户输入退出
                Console.WriteLine("按Enter键停止服务器...");
                Console.ReadLine();

                // 停止服务器
                await server.Stop();
                Console.WriteLine("服务器已停止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }

        static Task OnClientConnected(TcpSocketServer server, TcpSocketSession session)
        {
            Console.WriteLine($"客户端 {session.RemoteEndPoint} 已连接，会话ID: {session.Id}");
            
            // 向新连接的客户端发送欢迎消息
            var welcomeMessage = $"欢迎连接到服务器！您的会话ID是 {session.Id}";
            var data = Encoding.UTF8.GetBytes(welcomeMessage);
            return session.SendAsync(data);
        }

        static Task OnClientDataReceived(TcpSocketServer server, TcpSocketSession session, byte[] data, int offset, int count)
        {
            var message = Encoding.UTF8.GetString(data, offset, count);
            Console.WriteLine($"从客户端 {session.RemoteEndPoint} 收到数据: {message}");
            
            // 将消息回显给客户端
            var response = $"服务器收到: {message}";
            var responseData = Encoding.UTF8.GetBytes(response);
            return session.SendAsync(responseData);
        }

        static Task OnClientDisconnected(TcpSocketServer server, TcpSocketSession session)
        {
            Console.WriteLine($"客户端 {session.RemoteEndPoint} 已断开连接，会话ID: {session.Id}");
            return Task.CompletedTask;
        }
    }
}
```

### 示例3：创建WebSocket客户端

以下示例演示如何创建和使用WebSocket客户端：

```csharp
using System;
using System.Threading.Tasks;
using Wombat.Network;
using Wombat.Network.WebSockets;
using Wombat.Network.WebSockets.Extensions;
using Wombat.Network.WebSockets.SubProtocols;

namespace WombatNetworkDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 创建WebSocket客户端配置
            var config = new WebSocketClientConfiguration();
            
            // 启用压缩扩展
            config.EnabledExtensions.Add(
                PerMessageCompressionExtension.RegisteredToken, 
                new PerMessageCompressionExtensionNegotiator()
            );
            config.OfferedExtensions.Add(
                new WebSocketExtensionOfferDescription(PerMessageCompressionExtension.RegisteredToken)
            );
            
            // 请求特定子协议
            config.RequestedSubProtocols.Add(
                new WebSocketSubProtocolRequestDescription("json")
            );

            // 创建WebSocket客户端
            var client = new WebSocketClient(
                new Uri("ws://echo.websocket.org"),
                onServerTextReceived: OnTextReceived,
                onServerBinaryReceived: OnBinaryReceived,
                onServerConnected: OnConnected,
                onServerDisconnected: OnDisconnected,
                configuration: config);

            try
            {
                // 连接到WebSocket服务器
                Console.WriteLine("正在连接到WebSocket服务器...");
                await client.ConnectAsync();
                Console.WriteLine("已连接到WebSocket服务器！");

                // 发送文本消息
                var message = "Hello, WebSocket Server!";
                await client.SendTextAsync(message);
                Console.WriteLine($"已发送文本: {message}");

                // 发送二进制消息
                var binaryData = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
                await client.SendBinaryAsync(binaryData);
                Console.WriteLine($"已发送二进制数据: {BitConverter.ToString(binaryData)}");

                // 等待用户输入退出
                Console.WriteLine("按Enter键关闭连接...");
                Console.ReadLine();

                // 正常关闭WebSocket连接
                await client.Close(WebSocketCloseCode.Normal, "客户端主动关闭");
                Console.WriteLine("WebSocket连接已关闭");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                
                // 尝试中止连接
                try
                {
                    await client.Abort();
                }
                catch { }
            }
        }

        static Task OnConnected(WebSocketClient client)
        {
            Console.WriteLine($"已连接到WebSocket服务器 {client.Uri}");
            return Task.CompletedTask;
        }

        static Task OnTextReceived(WebSocketClient client, string text)
        {
            Console.WriteLine($"收到文本消息: {text}");
            return Task.CompletedTask;
        }

        static Task OnBinaryReceived(WebSocketClient client, byte[] data, int offset, int count)
        {
            var hexString = BitConverter.ToString(data, offset, count);
            Console.WriteLine($"收到二进制消息: {hexString}");
            return Task.CompletedTask;
        }

        static Task OnDisconnected(WebSocketClient client)
        {
            Console.WriteLine("已与WebSocket服务器断开连接");
            return Task.CompletedTask;
        }
    }
}
```

### 示例4：创建WebSocket服务器

以下示例演示如何创建和使用WebSocket服务器：

```csharp
using System;
using System.Net;
using System.Threading.Tasks;
using Wombat.Network;
using Wombat.Network.WebSockets;
using Wombat.Network.WebSockets.Extensions;
using Wombat.Network.WebSockets.SubProtocols;

namespace WombatNetworkDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 创建WebSocket服务器配置
            var config = new WebSocketServerConfiguration
            {
                MaxConnections = 100,
                ReceiveBufferSize = 16384,
                SendBufferSize = 16384
            };
            
            // 启用压缩扩展
            config.EnabledExtensions.Add(
                PerMessageCompressionExtension.RegisteredToken, 
                new PerMessageCompressionExtensionNegotiator()
            );
            
            // 支持特定子协议
            config.EnabledSubProtocols.Add(
                "json", new SimpleSubProtocolNegotiator()
            );

            // 创建WebSocket服务器
            var server = new WebSocketServer(
                IPAddress.Any, 8080,
                onClientTextReceived: OnClientTextReceived,
                onClientBinaryReceived: OnClientBinaryReceived,
                onClientConnected: OnClientConnected,
                onClientDisconnected: OnClientDisconnected,
                configuration: config);

            try
            {
                // 启动服务器
                Console.WriteLine("正在启动WebSocket服务器...");
                await server.Start();
                Console.WriteLine($"WebSocket服务器已启动，监听于 {server.LocalEndPoint}");

                // 等待用户输入退出
                Console.WriteLine("按Enter键停止服务器...");
                Console.ReadLine();

                // 停止服务器
                await server.Stop();
                Console.WriteLine("WebSocket服务器已停止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }

        static Task OnClientConnected(WebSocketServer server, WebSocketSession session)
        {
            Console.WriteLine($"WebSocket客户端已连接: {session.RemoteEndPoint}，路径: {session.Path}");
            
            // 向新连接的客户端发送欢迎消息
            return session.SendTextAsync($"欢迎连接到WebSocket服务器！您的会话ID是 {session.Id}");
        }

        static Task OnClientTextReceived(WebSocketServer server, WebSocketSession session, string text)
        {
            Console.WriteLine($"从客户端 {session.RemoteEndPoint} 收到文本: {text}");
            
            // 将文本消息回显给客户端
            return session.SendTextAsync($"服务器收到文本: {text}");
        }

        static Task OnClientBinaryReceived(WebSocketServer server, WebSocketSession session, byte[] data, int offset, int count)
        {
            var hexString = BitConverter.ToString(data, offset, count);
            Console.WriteLine($"从客户端 {session.RemoteEndPoint} 收到二进制数据: {hexString}");
            
            // 将二进制消息回显给客户端
            return session.SendBinaryAsync(data, offset, count);
        }

        static Task OnClientDisconnected(WebSocketServer server, WebSocketSession session)
        {
            Console.WriteLine($"WebSocket客户端已断开连接: {session.RemoteEndPoint}");
            return Task.CompletedTask;
        }
    }

    // 简单子协议协商器
    class SimpleSubProtocolNegotiator : IWebSocketSubProtocolNegotiator
    {
        public string Negotiate(WebSocketSubProtocolRequestDescription[] requestedSubProtocols)
        {
            foreach (var protocol in requestedSubProtocols)
            {
                if (protocol.Name == "json")
                {
                    return "json";
                }
            }
            return null;
        }
    }
}
```

### 示例5：使用PipelineSocketConnection进行高性能通信

以下示例演示如何使用PipelineSocketConnection进行高性能Socket通信：

```csharp
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Network.Pipelines;

namespace WombatNetworkDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 创建Socket
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            
            try
            {
                // 连接到服务器
                await socket.ConnectAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080));
                Console.WriteLine("已连接到服务器");
                
                // 创建Pipeline连接
                var pipeOptions = new PipeOptions(
                    pool: MemoryPool<byte>.Shared,
                    minimumSegmentSize: 4096,
                    pauseWriterThreshold: 1024 * 1024,
                    resumeWriterThreshold: 1024 * 512
                );
                var connection = new PipelineSocketConnection(
                    socket,
                    receiveOptions: pipeOptions,
                    sendOptions: pipeOptions,
                    maxConcurrentSends: 2
                );
                
                // 启动管道处理
                connection.Start();
                
                // 启动读取任务
                var readTask = ReadFromPipeAsync(connection.Input);
                
                // 发送数据
                var message = "Hello from Pipeline client!";
                var messageBytes = Encoding.UTF8.GetBytes(message);
                await connection.SendDataAsync(messageBytes);
                Console.WriteLine($"已发送: {message}");
                
                // 处理用户输入，发送更多消息
                var cts = new CancellationTokenSource();
                var sendTask = Task.Run(async () => {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            Console.Write("输入要发送的消息 (输入'exit'退出): ");
                            var input = Console.ReadLine();
                            if (input?.ToLower() == "exit")
                            {
                                break;
                            }
                            
                            if (!string.IsNullOrEmpty(input))
                            {
                                var inputBytes = Encoding.UTF8.GetBytes(input);
                                await connection.SendDataAsync(inputBytes, cts.Token);
                                Console.WriteLine($"已发送: {input}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"发送时出错: {ex.Message}");
                    }
                }, cts.Token);
                
                // 等待读取任务完成
                await readTask;
                
                // 取消发送任务并等待完成
                cts.Cancel();
                try { await sendTask; } catch { }
                
                // 停止连接
                await connection.StopAsync();
                Console.WriteLine("连接已关闭");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
            finally
            {
                socket.Dispose();
            }
        }
        
        static async Task ReadFromPipeAsync(PipeReader reader)
        {
            try
            {
                while (true)
                {
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    
                    // 处理接收到的数据
                    if (buffer.Length > 0)
                    {
                        // 示例中简单地将所有数据视为UTF-8文本
                        var data = buffer.ToArray();
                        var message = Encoding.UTF8.GetString(data);
                        Console.WriteLine($"收到: {message}");
                    }
                    
                    // 标记所有数据为已处理
                    reader.AdvanceTo(buffer.End);
                    
                    // 如果管道已完成，退出循环
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                
                // 完成读取
                await reader.CompleteAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取错误: {ex.Message}");
                await reader.CompleteAsync(ex);
            }
        }
    }
}
```

### 示例6：自定义帧构建器

以下示例演示如何创建和使用自定义帧构建器：

```csharp
using System;
using System.Text;
using Wombat.Network;

namespace WombatNetworkDemo
{
    // 自定义帧构建器：使用特定的起始和结束标记包装消息
    public class CustomDelimitedFrameBuilder : IFrameBuilder
    {
        private readonly byte[] _startMarker;
        private readonly byte[] _endMarker;
        
        public IFrameEncoder Encoder { get; }
        public IFrameDecoder Decoder { get; }
        
        public CustomDelimitedFrameBuilder(string startMarker, string endMarker)
        {
            if (string.IsNullOrEmpty(startMarker))
                throw new ArgumentNullException(nameof(startMarker));
            if (string.IsNullOrEmpty(endMarker))
                throw new ArgumentNullException(nameof(endMarker));
                
            _startMarker = Encoding.UTF8.GetBytes(startMarker);
            _endMarker = Encoding.UTF8.GetBytes(endMarker);
            
            Encoder = new CustomDelimitedFrameEncoder(_startMarker, _endMarker);
            Decoder = new CustomDelimitedFrameDecoder(_startMarker, _endMarker);
        }
    }
    
    public class CustomDelimitedFrameEncoder : IFrameEncoder
    {
        private readonly byte[] _startMarker;
        private readonly byte[] _endMarker;
        
        public CustomDelimitedFrameEncoder(byte[] startMarker, byte[] endMarker)
        {
            _startMarker = startMarker;
            _endMarker = endMarker;
        }
        
        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            // 计算帧总长度：起始标记 + 负载 + 结束标记
            int frameLength = _startMarker.Length + count + _endMarker.Length;
            
            // 创建帧缓冲区
            frameBuffer = new byte[frameLength];
            frameBufferOffset = 0;
            frameBufferLength = frameLength;
            
            // 复制起始标记
            Buffer.BlockCopy(_startMarker, 0, frameBuffer, 0, _startMarker.Length);
            
            // 复制负载数据
            Buffer.BlockCopy(payload, offset, frameBuffer, _startMarker.Length, count);
            
            // 复制结束标记
            Buffer.BlockCopy(_endMarker, 0, frameBuffer, _startMarker.Length + count, _endMarker.Length);
        }
    }
    
    public class CustomDelimitedFrameDecoder : IFrameDecoder
    {
        private readonly byte[] _startMarker;
        private readonly byte[] _endMarker;
        
        public CustomDelimitedFrameDecoder(byte[] startMarker, byte[] endMarker)
        {
            _startMarker = startMarker;
            _endMarker = endMarker;
        }
        
        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            // 初始化输出参数
            frameLength = 0;
            payload = buffer;
            payloadOffset = 0;
            payloadCount = 0;
            
            // 检查缓冲区是否足够大
            if (count < _startMarker.Length + _endMarker.Length)
                return false;
                
            // 查找起始标记
            int startIdx = IndexOf(buffer, offset, count, _startMarker);
            if (startIdx == -1)
                return false;
                
            // 查找结束标记
            int endIdx = IndexOf(buffer, startIdx + _startMarker.Length, 
                offset + count - (startIdx + _startMarker.Length), _endMarker);
            if (endIdx == -1)
                return false;
                
            // 计算帧和负载的信息
            frameLength = (endIdx + _endMarker.Length) - startIdx;
            payloadOffset = startIdx + _startMarker.Length;
            payloadCount = endIdx - payloadOffset;
            
            return true;
        }
        
        // 在字节数组中查找模式
        private int IndexOf(byte[] buffer, int offset, int count, byte[] pattern)
        {
            for (int i = offset; i <= offset + count - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                
                if (found)
                    return i;
            }
            
            return -1;
        }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            // 创建自定义帧构建器
            var frameBuilder = new CustomDelimitedFrameBuilder("<MSG>", "</MSG>");
            
            // 模拟编码和解码
            var message = "Hello, Custom Frame!";
            var messageBytes = Encoding.UTF8.GetBytes(message);
            
            // 编码消息
            frameBuilder.Encoder.EncodeFrame(messageBytes, 0, messageBytes.Length, 
                out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength);
            
            Console.WriteLine($"编码后的帧: {Encoding.UTF8.GetString(frameBuffer)}");
            
            // 解码消息
            bool decoded = frameBuilder.Decoder.TryDecodeFrame(frameBuffer, frameBufferOffset, frameBufferLength,
                out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount);
            
            if (decoded)
            {
                string decodedMessage = Encoding.UTF8.GetString(payload, payloadOffset, payloadCount);
                Console.WriteLine($"解码后的消息: {decodedMessage}");
            }
            else
            {
                Console.WriteLine("解码失败");
            }
            
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}
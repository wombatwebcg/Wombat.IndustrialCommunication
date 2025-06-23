# Wombat.Network

一个高性能、功能丰富的.NET网络通信库，支持TCP Socket、WebSocket以及基于System.IO.Pipelines的高性能I/O操作。

## 目录
- [简介](#简介)
- [特性详解](#特性详解)
- [安装](#安装)
- [快速入门](#快速入门)
- [API文档](#api文档)
- [配置选项](#配置选项)
- [进阶主题](#进阶主题)
- [性能优化建议](#性能优化建议)
- [贡献指南](#贡献指南)
- [许可信息](#许可信息)

## 简介

Wombat.Network是一个用于.NET平台的网络通信库，提供了简单易用但功能强大的API，用于构建高性能的网络应用程序。无论是构建实时通信应用、游戏服务器还是物联网设备通信，Wombat.Network都能满足您的需求。

### 主要功能

- **TCP Socket客户端/服务器**：提供可靠的TCP通信功能
- **WebSocket客户端/服务器**：支持标准WebSocket协议
- **高性能I/O处理**：基于System.IO.Pipelines的高效数据处理
- **多种帧格式**：支持多种数据帧格式，包括长度前缀、行分隔等
- **SSL/TLS支持**：内置安全通信支持
- **异步API**：全面支持异步编程模型
- **完整的中文API文档**：所有公共API都提供详细的中文XML注释，支持智能感知
- **可配置性**：提供丰富的配置选项以满足不同场景需求
- **可扩展性**：易于扩展的架构设计

## 特性详解

### 高性能

- **基于System.IO.Pipelines**：利用最新的高性能I/O API
- **异步非阻塞I/O**：全面使用异步操作提高吞吐量
- **智能缓冲区管理**：高效的内存使用和缓冲区重用
- **高并发支持**：设计用于处理大量并发连接
- **超时控制**：精细的超时和取消操作支持

### 可靠性

- **健壮的错误处理**：全面的异常处理机制
- **自动重连**：可配置的连接恢复策略
- **连接状态管理**：详细的连接状态跟踪
- **资源自动释放**：实现IDisposable确保资源正确释放
- **日志支持**：集成Microsoft.Extensions.Logging支持

### 易用性

- **流畅的API设计**：简洁明了的方法命名和参数设计
- **全面的配置选项**：易于自定义的配置对象
- **直观的事件模型**：基于回调的事件处理
- **支持多种数据格式**：文本、二进制、JSON等
- **完整的中文文档**：所有类、方法、属性都提供详细的中文XML注释
- **智能感知友好**：在Visual Studio等IDE中提供完整的中文智能感知支持
- **开发者友好**：降低中文开发者的学习和使用门槛

## 安装

### NuGet安装

```bash
dotnet add package Wombat.Network
```

或在Visual Studio的NuGet包管理器中搜索"Wombat.Network"。

### 手动安装

1. 从GitHub仓库克隆源代码：

```bash
git clone https://github.com/wombatwebcg/Wombat.Network.git
```

2. 构建解决方案：

```bash
cd Wombat.Network
dotnet build
```

3. 在您的项目中引用Wombat.Network.dll。

### 支持的平台

- .NET Standard 2.0+
- .NET Core 2.0+
- .NET 5/6/7/8

## 快速入门

以下是一些基本使用示例，帮助您快速上手：

### TCP Socket客户端

创建并使用TCP Socket客户端进行网络通信：

```csharp
using System;
using System.Text;
using System.Threading.Tasks;
using Wombat.Network.Sockets;

namespace WombatNetworkDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 创建TCP客户端配置
            var config = new TcpSocketClientConfiguration
            {
                ReceiveBufferSize = 8192,           // 接收缓冲区大小
                SendBufferSize = 8192,              // 发送缓冲区大小
                ReceiveTimeout = TimeSpan.FromSeconds(30),
                SendTimeout = TimeSpan.FromSeconds(30),
                ConnectTimeout = TimeSpan.FromSeconds(10),
                NoDelay = true,                     // 禁用Nagle算法
                KeepAlive = true,                   // 启用TCP保活
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            };

            // 创建TCP客户端，使用事件处理
            var client = new TcpSocketClient(
                "127.0.0.1", 8080,                 // 服务器地址和端口
                onServerDataReceived: OnDataReceived,
                onServerConnected: OnConnected,
                onServerDisconnected: OnDisconnected,
                configuration: config);

            try
            {
                // 连接到服务器
                Console.WriteLine("正在连接到TCP服务器...");
                await client.Connect();
                Console.WriteLine("已连接到TCP服务器！");

                // 发送数据
                var message = "Hello, TCP Server!";
                var messageBytes = Encoding.UTF8.GetBytes(message);
                await client.SendAsync(messageBytes, 0, messageBytes.Length);
                Console.WriteLine($"已发送消息: {message}");

                // 等待用户输入退出
                Console.WriteLine("按Enter键断开连接...");
                Console.ReadLine();

                // 关闭连接
                await client.Close();
                Console.WriteLine("TCP连接已关闭");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
        }

        // 数据接收事件处理
        private static async Task OnDataReceived(TcpSocketClient sender, byte[] data, int offset, int count)
        {
            var message = Encoding.UTF8.GetString(data, offset, count);
            Console.WriteLine($"收到服务器消息: {message}");
        }

        // 连接成功事件处理
        private static async Task OnConnected(TcpSocketClient sender)
        {
            Console.WriteLine($"已连接到服务器: {sender.RemoteEndPoint}");
        }

        // 连接断开事件处理
        private static async Task OnDisconnected(TcpSocketClient sender)
        {
            Console.WriteLine($"与服务器断开连接: {sender.RemoteEndPoint}");
        }
    }
}
```

### TCP Socket服务器

创建并使用TCP Socket服务器处理客户端连接：

```csharp
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wombat.Network.Sockets;

namespace WombatNetworkDemo
{
    class TcpServerDemo
    {
        static async Task Main(string[] args)
        {
            // 创建TCP服务器配置
            var config = new TcpSocketServerConfiguration
            {
                ReceiveBufferSize = 8192,
                SendBufferSize = 8192,
                ReceiveTimeout = TimeSpan.Zero,     // 服务器通常不设置接收超时
                SendTimeout = TimeSpan.FromSeconds(30),
                NoDelay = true,
                KeepAlive = true,
                KeepAliveInterval = TimeSpan.FromSeconds(30),
                PendingConnectionBacklog = 100      // 待处理连接队列大小
            };

            // 创建TCP服务器
            var server = new TcpSocketServer(
                new IPEndPoint(IPAddress.Any, 8080), // 监听所有网卡的8080端口
                new TcpServerEventDispatcher(),      // 事件分发器
                config);

            try
            {
                // 启动服务器
                Console.WriteLine("正在启动TCP服务器...");
                await server.Listen();
                Console.WriteLine("TCP服务器已启动，监听端口: 8080");

                // 等待用户输入退出
                Console.WriteLine("按Enter键停止服务器...");
                Console.ReadLine();

                // 停止服务器
                await server.Shutdown();
                Console.WriteLine("TCP服务器已停止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"服务器错误: {ex.Message}");
            }
        }
    }

    // 服务器事件分发器
    public class TcpServerEventDispatcher : ITcpSocketServerEventDispatcher
    {
        // 客户端连接事件
        public async Task OnSessionStarted(TcpSocketSession session)
        {
            Console.WriteLine($"客户端已连接: {session.RemoteEndPoint}");
            
            // 向客户端发送欢迎消息
            var welcomeMessage = "欢迎连接到TCP服务器！";
            var welcomeBytes = Encoding.UTF8.GetBytes(welcomeMessage);
            await session.SendAsync(welcomeBytes, 0, welcomeBytes.Length);
        }

        // 客户端数据接收事件
        public async Task OnSessionDataReceived(TcpSocketSession session, byte[] data, int offset, int count)
        {
            var message = Encoding.UTF8.GetString(data, offset, count);
            Console.WriteLine($"收到客户端 {session.RemoteEndPoint} 消息: {message}");

            // 回显消息给客户端
            var response = $"服务器收到: {message}";
            var responseBytes = Encoding.UTF8.GetBytes(response);
            await session.SendAsync(responseBytes, 0, responseBytes.Length);
        }

        // 客户端断开连接事件
        public async Task OnSessionClosed(TcpSocketSession session)
        {
            Console.WriteLine($"客户端已断开连接: {session.RemoteEndPoint}");
        }
    }
}
```

### WebSocket客户端

创建并使用WebSocket客户端进行实时通信：

```csharp
using System;
using System.Text;
using System.Threading.Tasks;
using Wombat.Network.WebSockets;

namespace WombatNetworkDemo
{
    class WebSocketClientDemo
    {
        static async Task Main(string[] args)
        {
            // 创建WebSocket客户端配置
            var config = new WebSocketClientConfiguration
            {
                ReceiveBufferSize = 8192,
                SendBufferSize = 8192,
                ReceiveTimeout = TimeSpan.FromSeconds(30),
                SendTimeout = TimeSpan.FromSeconds(30),
                ConnectTimeout = TimeSpan.FromSeconds(10),
                CloseTimeout = TimeSpan.FromSeconds(10),
                KeepAliveInterval = TimeSpan.FromSeconds(60),
                KeepAliveTimeout = TimeSpan.FromSeconds(10)
            };

            // 创建WebSocket客户端
            var client = new WebSocketClient(
                new Uri("ws://echo.websocket.org"),  // WebSocket服务器地址
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
                var textMessage = "Hello, WebSocket Server!";
                await client.SendTextAsync(textMessage);
                Console.WriteLine($"已发送文本消息: {textMessage}");

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

        // 文本消息接收事件
        private static async Task OnTextReceived(WebSocketClient sender, string text)
        {
            Console.WriteLine($"收到服务器文本消息: {text}");
        }

        // 二进制消息接收事件
        private static async Task OnBinaryReceived(WebSocketClient sender, byte[] data, int offset, int count)
        {
            var dataStr = BitConverter.ToString(data, offset, count);
            Console.WriteLine($"收到服务器二进制数据: {dataStr}");
        }

        // 连接成功事件
        private static async Task OnConnected(WebSocketClient sender)
        {
            Console.WriteLine("WebSocket连接已建立");
        }

        // 连接断开事件
        private static async Task OnDisconnected(WebSocketClient sender)
        {
            Console.WriteLine("WebSocket连接已断开");
        }
    }
}
```

### WebSocket服务器

创建并使用WebSocket服务器处理客户端连接：

```csharp
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wombat.Network.WebSockets;

namespace WombatNetworkDemo
{
    class WebSocketServerDemo
    {
        // 存储所有连接的客户端会话
        private static readonly ConcurrentDictionary<string, WebSocketSession> _sessions = 
            new ConcurrentDictionary<string, WebSocketSession>();

        static async Task Main(string[] args)
        {
            // 创建WebSocket服务器配置
            var config = new WebSocketServerConfiguration
            {
                Path = "/",                         // WebSocket服务路径
                ReceiveBufferSize = 8192,
                SendBufferSize = 8192,
                ReceiveTimeout = TimeSpan.Zero,
                SendTimeout = TimeSpan.FromSeconds(30),
                KeepAliveInterval = TimeSpan.FromSeconds(60),
                KeepAliveTimeout = TimeSpan.FromSeconds(10)
            };

            // 创建WebSocket服务器
            var server = new WebSocketServer(
                new IPEndPoint(IPAddress.Any, 8082),  // 监听端口8082
                new WebSocketServerEventDispatcher(), // 事件分发器
                config);

            try
            {
                // 启动服务器
                Console.WriteLine("正在启动WebSocket服务器...");
                await server.Listen();
                Console.WriteLine("WebSocket服务器已启动，监听端口: 8082");
                Console.WriteLine("可以通过 ws://localhost:8082 连接");

                // 等待用户输入退出
                Console.WriteLine("按Enter键停止服务器...");
                Console.ReadLine();

                // 停止服务器
                await server.Shutdown();
                Console.WriteLine("WebSocket服务器已停止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"服务器错误: {ex.Message}");
            }
        }

        // WebSocket服务器事件分发器
        public class WebSocketServerEventDispatcher : IWebSocketServerEventDispatcher
        {
            // 客户端连接事件
            public async Task OnSessionStarted(WebSocketSession session)
            {
                var sessionId = session.RemoteEndPoint.ToString();
                _sessions.TryAdd(sessionId, session);
                Console.WriteLine($"WebSocket客户端已连接: {sessionId}");

                // 向客户端发送欢迎消息
                await session.SendTextAsync("欢迎连接到WebSocket服务器！");
                
                // 向所有其他客户端广播新用户加入消息
                await BroadcastToOthers(session, $"用户 {sessionId} 已加入聊天室");
            }

            // 客户端文本消息事件
            public async Task OnSessionTextReceived(WebSocketSession session, string text)
            {
                var sessionId = session.RemoteEndPoint.ToString();
                Console.WriteLine($"收到客户端 {sessionId} 文本消息: {text}");

                // 广播消息给所有客户端
                await BroadcastToAll($"{sessionId}: {text}");
            }

            // 客户端二进制消息事件
            public async Task OnSessionBinaryReceived(WebSocketSession session, byte[] data, int offset, int count)
            {
                var sessionId = session.RemoteEndPoint.ToString();
                var dataStr = BitConverter.ToString(data, offset, count);
                Console.WriteLine($"收到客户端 {sessionId} 二进制数据: {dataStr}");

                // 回显二进制数据
                await session.SendBinaryAsync(data, offset, count);
            }

            // 客户端断开连接事件
            public async Task OnSessionClosed(WebSocketSession session)
            {
                var sessionId = session.RemoteEndPoint.ToString();
                _sessions.TryRemove(sessionId, out _);
                Console.WriteLine($"WebSocket客户端已断开连接: {sessionId}");

                // 向所有其他客户端广播用户离开消息
                await BroadcastToAll($"用户 {sessionId} 已离开聊天室");
            }

            // 向所有客户端广播消息
            private async Task BroadcastToAll(string message)
            {
                var tasks = new List<Task>();
                foreach (var session in _sessions.Values)
                {
                    try
                    {
                        tasks.Add(session.SendTextAsync(message));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"广播消息失败: {ex.Message}");
                    }
                }
                await Task.WhenAll(tasks);
            }

            // 向除指定会话外的所有客户端广播消息
            private async Task BroadcastToOthers(WebSocketSession excludeSession, string message)
            {
                var tasks = new List<Task>();
                var excludeId = excludeSession.RemoteEndPoint.ToString();
                
                foreach (var kvp in _sessions)
                {
                    if (kvp.Key != excludeId)
                    {
                        try
                        {
                            tasks.Add(kvp.Value.SendTextAsync(message));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"广播消息失败: {ex.Message}");
                        }
                    }
                }
                await Task.WhenAll(tasks);
            }
        }
    }
}
```

### 高性能 Pipeline 连接

使用System.IO.Pipelines进行高性能Socket通信：

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
    class PipelineDemo
    {
        static async Task Main(string[] args)
        {
            // 创建Socket连接
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            
            try
            {
                // 连接到服务器
                Console.WriteLine("正在连接到服务器...");
                await socket.ConnectAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080));
                Console.WriteLine("已连接到服务器");
                
                // 创建Pipeline连接配置
                var pipeOptions = new PipeOptions(
                    pool: MemoryPool<byte>.Shared,          // 使用共享内存池
                    readerScheduler: PipeScheduler.ThreadPool, // 读取调度器
                    writerScheduler: PipeScheduler.ThreadPool, // 写入调度器
                    pauseWriterThreshold: 1024 * 1024,     // 写入暂停阈值（1MB）
                    resumeWriterThreshold: 1024 * 512,     // 写入恢复阈值（512KB）
                    minimumSegmentSize: 4096,               // 最小段大小
                    useSynchronizationContext: false        // 不使用同步上下文
                );
                
                // 创建高性能Pipeline连接
                var connection = new PipelineSocketConnection(
                    socket,
                    receiveOptions: pipeOptions,
                    sendOptions: pipeOptions,
                    maxConcurrentSends: 2);                 // 最大并发发送数
                
                // 启动管道处理
                connection.Start();
                
                // 启动读取任务
                var readTask = ReadFromPipelineAsync(connection.Input);
                
                // 启动写入任务
                var writeTask = WriteToPipelineAsync(connection.Output);
                
                // 等待任务完成或用户输入
                Console.WriteLine("按Enter键停止连接...");
                Console.ReadLine();
                
                // 停止连接
                await connection.StopAsync();
                Console.WriteLine("Pipeline连接已停止");
                
                // 等待读写任务完成
                await Task.WhenAll(readTask, writeTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Pipeline连接错误: {ex.Message}");
            }
            finally
            {
                socket?.Dispose();
            }
        }

        // 从Pipeline读取数据
        private static async Task ReadFromPipelineAsync(PipeReader reader)
        {
            try
            {
                while (true)
                {
                    // 读取数据
                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    
                    if (buffer.IsEmpty && result.IsCompleted)
                    {
                        break; // 没有更多数据且管道已完成
                    }
                    
                    // 处理接收到的数据
                    await ProcessReceivedData(buffer);
                    
                    // 告诉管道我们已经处理了所有数据
                    reader.AdvanceTo(buffer.End);
                    
                    if (result.IsCompleted)
                    {
                        break; // 管道已完成
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取数据错误: {ex.Message}");
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }
        
        // 处理接收到的数据
        private static async Task ProcessReceivedData(ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsEmpty)
                return;
                
            // 将数据转换为字符串（假设是UTF-8编码的文本）
            if (buffer.IsSingleSegment)
            {
                // 单个内存段，直接处理
                var span = buffer.FirstSpan;
                var message = Encoding.UTF8.GetString(span);
                Console.WriteLine($"收到服务器消息: {message}");
            }
            else
            {
                // 多个内存段，需要合并
                var totalLength = (int)buffer.Length;
                var data = new byte[totalLength];
                buffer.CopyTo(data);
                var message = Encoding.UTF8.GetString(data);
                Console.WriteLine($"收到服务器消息: {message}");
            }
        }

        // 向Pipeline写入数据
        private static async Task WriteToPipelineAsync(PipeWriter writer)
        {
            try
            {
                // 模拟定期发送数据
                for (int i = 0; i < 10; i++)
                {
                    var message = $"Pipeline消息 #{i + 1}";
                    var messageBytes = Encoding.UTF8.GetBytes(message);
                    
                    // 获取写入内存
                    Memory<byte> memory = writer.GetMemory(messageBytes.Length);
                    
                    // 写入数据
                    messageBytes.CopyTo(memory);
                    writer.Advance(messageBytes.Length);
                    
                    // 刷新数据，发送到网络
                    FlushResult flushResult = await writer.FlushAsync();
                    if (flushResult.IsCompleted)
                    {
                        break; // 管道已完成
                    }
                    
                    Console.WriteLine($"已发送消息: {message}");
                    
                    // 等待1秒再发送下一条消息
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入数据错误: {ex.Message}");
            }
            finally
            {
                await writer.CompleteAsync();
            }
        }
    }
}
```

> **性能优势说明**：
> - **内存效率**：使用`Memory<T>`和`Span<T>`减少内存分配和复制
> - **背压控制**：自动管理读写缓冲区，避免内存过度使用
> - **异步处理**：完全异步的I/O操作，提高并发性能
> - **零拷贝**：在可能的情况下避免数据复制操作
> - **资源管理**：自动管理内存池和缓冲区生命周期

## API文档

Wombat.Network 提供了完整的中文API文档支持，让中文开发者能够更轻松地使用和理解库的功能。

### 完整的中文XML注释

- **全面覆盖**：所有公共类、接口、方法、属性和枚举都提供了详细的中文XML注释
- **详细说明**：包含功能描述、参数说明、返回值说明、异常信息和使用示例
- **使用指导**：提供最佳实践建议和注意事项
- **标准格式**：遵循.NET XML文档注释标准，确保兼容性

### IDE智能感知支持

在Visual Studio、Visual Studio Code、JetBrains Rider等现代IDE中，您可以享受到：

- **中文智能感知提示**：输入代码时自动显示中文方法和属性说明
- **参数提示**：详细的参数类型和用途说明
- **快速信息**：鼠标悬停即可查看详细的中文文档
- **错误提示**：异常情况的中文说明和处理建议

### 文档生成

您可以使用标准的.NET文档生成工具从XML注释生成完整的API文档：

```bash
# 使用DocFX生成文档
docfx init -q
docfx docfx_project\docfx.json --serve

# 使用Sandcastle Help File Builder
# 或其他支持XML注释的文档生成工具
```

### 核心模块文档覆盖

以下模块已完全覆盖中文XML注释：

- **网络连接**：TCP Socket、WebSocket客户端和服务器
- **帧处理**：各种帧构建器和编解码器
- **缓冲区管理**：高性能内存管理组件
- **配置系统**：所有配置类和选项
- **扩展机制**：WebSocket扩展和子协议支持
- **异常处理**：完整的异常类型和错误信息

## 配置选项

Wombat.Network提供了丰富的配置选项，使您能够根据具体需求定制网络组件的行为。

### TCP Socket客户端配置

`TcpSocketClientConfiguration`类提供以下主要配置选项：

| 配置项 | 说明 | 默认值 |
|-------|------|-------|
| `BufferManager` | 缓冲区管理器 | 新的SegmentBufferManager实例 |
| `FrameBuilder` | 帧构建器，决定如何解析数据包 | LengthPrefixedFrameBuilder |
| `ReceiveBufferSize` | 接收缓冲区大小（字节） | 8192 |
| `SendBufferSize` | 发送缓冲区大小（字节） | 8192 |
| `ReceiveTimeout` | 接收超时时间 | 30秒 |
| `SendTimeout` | 发送超时时间 | 30秒 |
| `ConnectTimeout` | 连接超时时间 | 30秒 |
| `NoDelay` | 是否禁用Nagle算法 | true |
| `SslEnabled` | 是否启用SSL/TLS | false |
| `OperationTimeout` | 一般操作超时时间 | 30秒 |
| `EnablePipelineIo` | 是否启用高性能Pipeline | true |
| `MaxConcurrentOperations` | 每个连接的最大并发操作数 | 10 |

### WebSocket客户端配置

`WebSocketClientConfiguration`类提供以下主要配置选项：

| 配置项 | 说明 | 默认值 |
|-------|------|-------|
| `BufferManager` | 缓冲区管理器 | 新的SegmentBufferManager实例 |
| `ReceiveBufferSize` | 接收缓冲区大小（字节） | 8192 |
| `SendBufferSize` | 发送缓冲区大小（字节） | 8192 |
| `ReceiveTimeout` | 接收超时时间 | 30秒 |
| `SendTimeout` | 发送超时时间 | 30秒 |
| `ConnectTimeout` | 连接超时时间 | 30秒 |
| `CloseTimeout` | 关闭超时时间 | 10秒 |
| `KeepAliveInterval` | 保活间隔时间 | 60秒 |
| `KeepAliveTimeout` | 保活超时时间 | 10秒 |
| `EnabledExtensions` | 启用的WebSocket扩展 | 默认包含PerMessageCompressionExtension |

### 帧构建器

Wombat.Network支持多种帧格式，您可以根据需要选择合适的帧构建器：

1. **LengthPrefixedFrameBuilder**：长度前缀帧，消息前加入长度字段
2. **LengthFieldBasedFrameBuilder**：基于长度字段的帧，可以灵活设置长度字段位置和大小
3. **LineBasedFrameBuilder**：基于行分隔符的帧，适合文本协议
4. **RawBufferFrameBuilder**：原始缓冲区帧，无特殊格式

示例：
```csharp
// 使用长度前缀帧构建器
config.FrameBuilder = new LengthPrefixedFrameBuilder();

// 使用基于行的帧构建器
config.FrameBuilder = new LineBasedFrameBuilder();

// 使用自定义长度字段帧构建器
config.FrameBuilder = new LengthFieldBasedFrameBuilder(
    lengthFieldOffset: 0,     // 长度字段起始位置
    lengthFieldLength: 4,     // 长度字段占用字节数
    lengthAdjustment: 0,      // 长度调整值
    initialBytesToStrip: 4    // 解码时跳过的初始字节数
);
```

## 进阶主题

### SSL/TLS配置

在TCP Socket和WebSocket客户端中启用SSL/TLS加密：

```csharp
// TCP Socket客户端SSL配置
var config = new TcpSocketClientConfiguration
{
    SslEnabled = true,
    SslTargetHost = "example.com",
    SslClientCertificates = new X509CertificateCollection
    {
        new X509Certificate2("client.pfx", "password")
    },
    SslEncryptionPolicy = EncryptionPolicy.RequireEncryption,
    SslEnabledProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
    SslCheckCertificateRevocation = true,
    SslPolicyErrorsBypassed = false
};

// WebSocket服务器SSL配置
var config = new WebSocketServerConfiguration
{
    SslEnabled = true,
    SslServerCertificate = new X509Certificate2("server.pfx", "password"),
    SslEnabledProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
    SslCheckCertificateRevocation = true,
    SslPolicyErrorsBypassed = false
};
```

### WebSocket扩展和子协议

WebSocket支持标准扩展和子协议的注册和使用：

```csharp
// 客户端启用压缩扩展
var config = new WebSocketClientConfiguration();
config.EnabledExtensions.Add(
    PerMessageCompressionExtension.RegisteredToken, 
    new PerMessageCompressionExtensionNegotiator()
);
config.OfferedExtensions.Add(
    new WebSocketExtensionOfferDescription(PerMessageCompressionExtension.RegisteredToken)
);

// 服务器端启用自定义子协议
var config = new WebSocketServerConfiguration();
config.EnabledSubProtocols.Add(
    "myprotocol", 
    new MyProtocolNegotiator()
);
```

### 自定义帧构建器

您可以通过实现`IFrameBuilder`接口创建自定义帧构建器：

```csharp
public class MyCustomFrameBuilder : IFrameBuilder
{
    public IFrameDecoder Decoder { get; }
    public IFrameEncoder Encoder { get; }
    
    public MyCustomFrameBuilder()
    {
        Decoder = new MyCustomFrameDecoder();
        Encoder = new MyCustomFrameEncoder();
    }
}

public class MyCustomFrameDecoder : IFrameDecoder
{
    public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
    {
        // 实现您的解码逻辑
    }
}

public class MyCustomFrameEncoder : IFrameEncoder
{
    public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
    {
        // 实现您的编码逻辑
    }
}
```

## 性能优化建议

以下是一些提高Wombat.Network性能的建议：

### 1. 使用System.IO.Pipelines

启用Pipeline I/O以获得更高的吞吐量和更低的内存占用：

```csharp
var config = new TcpSocketClientConfiguration
{
    EnablePipelineIo = true
};
```

或直接使用`PipelineSocketConnection`类处理高性能Socket通信。

### 2. 框架兼容性注意事项

在不同的.NET框架版本中，Socket API的使用方式存在差异：

- **.NET Standard 2.0**: 使用`SocketAsyncEventArgs`进行异步操作，不支持`Socket.ReceiveAsync(Memory<byte>, SocketFlags)`和`Socket.SendAsync(ReadOnlyMemory<byte>, SocketFlags)`。
- **.NET Core 3.0+/.NET 5+**: 支持基于`Memory<T>`的异步Socket API，可以直接使用`await socket.ReceiveAsync(memory, SocketFlags.None)`。

如果您的项目面向.NET Standard 2.0，`PipelineSocketConnection`已经内部处理了这些差异，确保跨平台兼容性。对于自定义Socket操作，请使用兼容的API。

```csharp
// .NET Standard 2.0 兼容写法
var args = new SocketAsyncEventArgs();
args.SetBuffer(buffer, 0, buffer.Length);
args.Completed += OnOperationCompleted;
bool pending = socket.ReceiveAsync(args);
if (!pending) OnOperationCompleted(socket, args);

// .NET Core 3.0+/.NET 5+ 写法 (不兼容 .NET Standard 2.0)
int bytesReceived = await socket.ReceiveAsync(memory, SocketFlags.None);
```

### 3. 选择合适的帧格式

为您的应用场景选择最合适的帧格式：
- 对于二进制协议，使用`LengthFieldBasedFrameBuilder`
- 对于文本协议，使用`LineBasedFrameBuilder`
- 对于大量小消息，使用`RawBufferFrameBuilder`

### 4. 配置适当的缓冲区大小

根据您的消息大小设置合适的缓冲区参数：

```csharp
var config = new TcpSocketClientConfiguration
{
    ReceiveBufferSize = 16384,  // 更大的接收缓冲区
    SendBufferSize = 16384,     // 更大的发送缓冲区
    BufferManager = new SegmentBufferManager(
        maxCapacity: 1000,      // 最大缓冲区数量
        bufferSize: 16384,      // 每个缓冲区大小
        maxSegmentSize: 16,     // 每个缓冲段的最大大小
        isCircular: true        // 循环使用缓冲区
    )
};
```

### 5. 优化连接参数

调整连接和超时参数以获得更好的性能：

```csharp
var config = new TcpSocketClientConfiguration
{
    NoDelay = true,               // 禁用Nagle算法
    MaxConcurrentOperations = 20, // 增加并发操作数
    OperationTimeout = TimeSpan.FromSeconds(5), // 减少操作超时
    KeepAlive = true,             // 启用TCP保活
    KeepAliveInterval = TimeSpan.FromSeconds(30) // 设置保活间隔
};
```

### 6. 减少内存分配

- 重用缓冲区而不是每次分配新内存
- 使用`ArrayPool<byte>`或缓冲区池管理内存
- 避免不必要的数据复制操作

## 贡献指南

我们欢迎社区的贡献！如果您想参与Wombat.Network的开发，请遵循以下步骤：

1. Fork仓库到您的GitHub账号
2. 创建新的特性分支 (`git checkout -b feature/amazing-feature`)
3. 提交您的更改 (`git commit -m 'Add some amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 创建一个Pull Request

### 代码规范

- 请遵循现有的代码风格和命名约定
- **为所有公共API添加完整的中文XML注释**，包括类、方法、属性、参数、返回值和异常说明
- 确保XML注释内容准确、详细，并提供使用指导和最佳实践建议
- 确保所有单元测试通过
- 添加新功能时应包含相应的单元测试
- 保持中文注释的专业性和一致性，遵循项目现有的注释风格

## 许可信息

Wombat.Network使用MIT许可证发布。详细信息请参见[LICENSE](LICENSE)文件。

---

© 2023 wombatwebcg. 保留所有权利。
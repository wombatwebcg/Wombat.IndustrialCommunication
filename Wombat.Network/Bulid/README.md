# Wombat.Network 扩展库

本扩展库为 Wombat.Network 提供了类似 Microsoft.Extensions.Hosting 的服务注册模式，支持依赖注入和托管服务。

## 功能特性

- ✅ 支持TCP、UDP、WebSocket服务器和客户端
- ✅ 集成Microsoft.Extensions.Hosting托管服务
- ✅ 支持依赖注入和配置系统
- ✅ 提供流畅的配置API
- ✅ **支持单协议独立使用**
- ✅ 支持客户端工厂模式
- ✅ 完整的中文文档

## 安装

确保项目已引用以下NuGet包：
```xml
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="6.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="6.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="6.0.0" />
```

## 使用方式

### 方式一：单协议独立使用（推荐）

#### 仅使用TCP
```csharp
// 添加TCP服务器（最简单）
services.AddWombatTcpServer(8080);

// 添加TCP客户端（最简单）
services.AddWombatTcpClient("127.0.0.1", 8080);

// 使用构建器模式配置TCP
services.AddWombatTcp(tcp =>
{
    tcp.AddServer(8080, options =>
    {
        options.AutoStart = true;
        options.ListenAddress = "0.0.0.0";
    })
    .AddClient("127.0.0.1", 8080, options =>
    {
        options.AutoConnect = true;
    });
});
```

#### 仅使用UDP
```csharp
// 添加UDP服务器（最简单）
services.AddWombatUdpServer(8081);

// 添加UDP客户端（最简单）
services.AddWombatUdpClient("127.0.0.1", 8081);

// 使用构建器模式配置UDP
services.AddWombatUdp(udp =>
{
    udp.AddServer(8081, options =>
    {
        options.AutoStart = true;
        options.ListenAddress = "0.0.0.0";
    })
    .AddClient("127.0.0.1", 8081, options =>
    {
        options.AutoConnect = true;
    });
});
```

#### 仅使用WebSocket
```csharp
// 添加WebSocket服务器（最简单）
services.AddWombatWebSocketServer(8082);

// 添加WebSocket客户端（最简单）
services.AddWombatWebSocketClient("ws://127.0.0.1:8082/");

// 使用构建器模式配置WebSocket
services.AddWombatWebSocket(ws =>
{
    ws.AddServer(8082, options =>
    {
        options.AutoStart = true;
        options.ListenAddress = "0.0.0.0";
    })
    .AddClient("ws://127.0.0.1:8082/", options =>
    {
        options.AutoConnect = true;
    });
});
```

### 方式二：多协议整合使用

#### 完整网络套件（快速设置）
```csharp
// 一键添加所有协议（使用默认端口）
services.AddWombatNetworkSuite();

// 自定义端口
services.AddWombatNetworkSuite(tcpPort: 9001, udpPort: 9002, webSocketPort: 9003);
```

#### 使用整合构建器
```csharp
services.AddWombatNetwork(network =>
{
    network.AddTcpServer(8080)
           .AddUdpServer(8081)
           .AddWebSocketServer(8082)
           .AddTcpClient("127.0.0.1", 8080)
           .AddUdpClient("127.0.0.1", 8081)
           .AddWebSocketClient("ws://127.0.0.1:8082/");
});
```

### 方式三：原生扩展方法

#### TCP服务器
```csharp
services.AddTcpServer(builder =>
{
    builder.Listen(8080)
           .AutoStart()
           .UseHandlers(
               onSessionDataReceived: async (session, data, offset, count) =>
               {
                   // 处理接收到的数据
                   var message = Encoding.UTF8.GetString(data, offset, count);
                   Console.WriteLine($"收到数据: {message}");
                   
                   // 回复数据
                   var response = Encoding.UTF8.GetBytes($"服务器回复: {message}");
                   await session.SendAsync(response);
               },
               onSessionStarted: async session =>
               {
                   Console.WriteLine($"客户端连接: {session.RemoteEndPoint}");
               },
               onSessionClosed: async session =>
               {
                   Console.WriteLine($"客户端断开: {session.RemoteEndPoint}");
               });
});
```

#### UDP服务器
```csharp
services.AddUdpServer(8081, options =>
{
    options.AutoStart = true;
    options.ListenAddress = "0.0.0.0";
});
```

#### WebSocket服务器
```csharp
services.AddWebSocketServer(8082, options =>
{
    options.AutoStart = true;
    options.ListenAddress = "0.0.0.0";
});
```

## 客户端工厂使用

### TCP客户端工厂
```csharp
// 注册服务
services.AddTcpClient(options =>
{
    options.RemoteAddress = "127.0.0.1";
    options.RemotePort = 8080;
});

// 使用工厂创建客户端
public class MyService
{
    private readonly ITcpSocketClientFactory _tcpFactory;
    
    public MyService(ITcpSocketClientFactory tcpFactory)
    {
        _tcpFactory = tcpFactory;
    }
    
    public async Task ConnectAsync()
    {
        var client = _tcpFactory.CreateClient(
            onServerDataReceived: async (client, data, offset, count) =>
            {
                var message = Encoding.UTF8.GetString(data, offset, count);
                Console.WriteLine($"收到服务器数据: {message}");
            },
            onServerConnected: async client =>
            {
                Console.WriteLine("已连接到服务器");
            },
            onServerDisconnected: async client =>
            {
                Console.WriteLine("与服务器断开连接");
            });
        
        await client.Connect();
        
        // 发送数据
        var data = Encoding.UTF8.GetBytes("Hello Server!");
        await client.SendAsync(data);
    }
}
```

### UDP客户端工厂
```csharp
// 注册服务
services.AddUdpClient(options =>
{
    options.RemoteAddress = "127.0.0.1";
    options.RemotePort = 8081;
});

// 使用工厂
public class MyUdpService
{
    private readonly IUdpSocketClientFactory _udpFactory;
    
    public MyUdpService(IUdpSocketClientFactory udpFactory)
    {
        _udpFactory = udpFactory;
    }
    
    public async Task SendAsync()
    {
        var client = _udpFactory.CreateClient();
        var data = Encoding.UTF8.GetBytes("Hello UDP Server!");
        await client.SendAsync(data);
    }
}
```

### WebSocket客户端工厂
```csharp
// 注册服务
services.AddWebSocketClient(options =>
{
    options.ServerUri = "ws://127.0.0.1:8082/";
});

// 使用工厂
public class MyWebSocketService
{
    private readonly IWebSocketClientFactory _wsFactory;
    
    public MyWebSocketService(IWebSocketClientFactory wsFactory)
    {
        _wsFactory = wsFactory;
    }
    
    public async Task ConnectAsync()
    {
        var client = _wsFactory.CreateClient(
            onServerTextReceived: async (client, text) =>
            {
                Console.WriteLine($"收到文本消息: {text}");
            },
            onServerConnected: async client =>
            {
                Console.WriteLine("WebSocket已连接");
            },
            onServerDisconnected: async client =>
            {
                Console.WriteLine("WebSocket已断开");
            });
        
        await client.ConnectAsync();
        await client.SendTextAsync("Hello WebSocket Server!");
    }
}
```

## 完整示例

### ASP.NET Core项目中使用
```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // 方式1：仅使用TCP
        builder.Services.AddWombatTcpServer(8080, options =>
        {
            options.AutoStart = true;
        });
        
        // 方式2：使用完整套件
        // builder.Services.AddWombatNetworkSuite();
        
        var app = builder.Build();
        
        app.Run();
    }
}
```

### 控制台应用中使用
```csharp
public class Program
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        
        // 添加日志
        services.AddLogging(builder => builder.AddConsole());
        
        // 仅添加UDP服务
        services.AddWombatUdpServer(8081, options =>
        {
            options.AutoStart = true;
        });
        
        var serviceProvider = services.BuildServiceProvider();
        
        // 启动托管服务
        var hostedServices = serviceProvider.GetServices<IHostedService>();
        foreach (var service in hostedServices)
        {
            await service.StartAsync(CancellationToken.None);
        }
        
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
        
        // 停止托管服务
        foreach (var service in hostedServices)
        {
            await service.StopAsync(CancellationToken.None);
        }
    }
}
```

## 优势对比

| 使用方式 | 适用场景 | 优势 |
|---------|---------|------|
| 单协议扩展 | 只需要某一种协议 | 最简洁，无冗余依赖 |
| 整合构建器 | 需要多种协议 | 统一配置，功能完整 |
| 原生扩展 | 需要高度定制 | 最大灵活性 |

## 配置选项

### TCP服务器选项
```csharp
public class TcpServerOptions
{
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int ListenPort { get; set; }
    public bool AutoStart { get; set; } = true;
    public TcpSocketServerConfiguration Configuration { get; set; }
}
```

### UDP服务器选项
```csharp
public class UdpServerOptions
{
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int ListenPort { get; set; }
    public bool AutoStart { get; set; } = true;
    public UdpSocketServerConfiguration Configuration { get; set; }
}
```

### WebSocket服务器选项
```csharp
public class WebSocketServerOptions
{
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int ListenPort { get; set; }
    public bool AutoStart { get; set; } = true;
    public WebSocketServerConfiguration Configuration { get; set; }
    public AsyncWebSocketServerModuleCatalog ModuleCatalog { get; set; }
}
```

## 注意事项

1. 确保端口未被占用
2. 在生产环境中建议配置适当的日志级别
3. 客户端需要处理连接重试逻辑
4. 服务器需要处理并发连接和异常情况
5. 使用单协议扩展时，只会引入相关的依赖，减少资源占用

## 许可证

本扩展库遵循与 Wombat.Network 相同的许可证。 
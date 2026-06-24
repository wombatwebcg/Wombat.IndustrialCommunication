# Wombat.Network

`Wombat.Network` 是一个偏底层的 .NET 通讯库。现在的公开用法很简单，统一分成三层：

- `Transport`：负责连接或收发字节。
- `Channel`：负责按“消息”收发。
- `Protocol`：负责把字节流切成消息，或做 WebSocket 握手与帧编解码。

如果只想知道怎么用，按下面三种场景选一套就够了：

- TCP：`TcpTransportConnection` + `StreamMessageChannel` + `LengthFieldMessagePipe`
- UDP：`UdpDatagramTransport` + `DatagramMessageChannel`
- WebSocket：`TcpTransportConnection` + `WebSocketHandshakeMiddleware` + `WebSocketMessageChannel`

## 1. 安装

如果你通过项目引用使用：

```xml
<ProjectReference Include="..\Wombat.Network\Wombat.Network.csproj" />
```

如果你通过 NuGet 使用：

```xml
<PackageReference Include="Wombat.Network" Version="1.1.5" />
```

当前项目目标框架是 `netstandard2.0`。

## 2. 核心模型

### Transport

公开入口：

- `TcpTransportConnection`
- `TcpTransportListener`
- `UdpDatagramTransport`
- `TlsTransportConnection`
- `SerialTransportConnection`

规则很直接：

- 使用前先 `StartAsync()`
- 用完后 `CloseAsync()`
- TCP/TLS/串口这类流式连接，通常还要再包一层 `StreamMessageChannel`
- UDP 本身就是报文，通常直接配 `DatagramMessageChannel`

### Channel

公开入口：

- `IMessageChannel`
- `StreamMessageChannel`
- `DatagramMessageChannel`
- `WebSocketMessageChannel`

统一关注这几个动作：

- `SendAsync(...)`
- `ReceiveAsync(...)`
- `CloseAsync(...)`

其中：

- `StreamMessageChannel` 依赖一个 `IMessagePipe` 来定义拆包规则
- `DatagramMessageChannel` 可以直接收发 UDP，也可以额外加 framing
- `WebSocketMessageChannel` 直接处理 text/binary/ping/pong/close

### Protocol

常用入口：

- `LengthFieldMessagePipe`
- `WebSocketHandshakeMiddleware`
- `WebSocketFrameCodec`

最常用的协议是：

- 流式消息：`new LengthFieldMessagePipe(LengthField.FourBytes)`
- WebSocket 握手：`AcceptServerAsync(...)` / `AcceptClientAsync(...)`

## 3. TCP 用法

TCP 是“字节流”，没有天然消息边界，所以必须配一个 `IMessagePipe`。仓库里的默认做法就是长度前缀。

### 服务端

```csharp
using System.Net;
using System.Text;
using Wombat.Network.Channels;
using Wombat.Network.Protocols.Framing;
using Wombat.Network.Transports.Tcp;

var listener = new TcpTransportListener(new IPEndPoint(IPAddress.Any, 5000));
await listener.StartAsync();

var connection = (TcpTransportConnection)await listener.AcceptAsync();
await connection.StartAsync();

var channel = new StreamMessageChannel(
    connection,
    new LengthFieldMessagePipe(LengthField.FourBytes));

var message = await channel.ReceiveAsync();
if (message.HasValue)
{
    var text = Encoding.UTF8.GetString(message.Value.Payload.ToArray());
    Console.WriteLine($"server recv: {text}");
    await channel.SendAsync(Encoding.UTF8.GetBytes("pong"));
}

await channel.CloseAsync();
await listener.CloseAsync();
```

### 客户端

```csharp
using System.Net;
using System.Text;
using Wombat.Network.Channels;
using Wombat.Network.Protocols.Framing;
using Wombat.Network.Transports.Tcp;

var connection = await TcpTransportConnection.ConnectAsync(
    new IPEndPoint(IPAddress.Loopback, 5000));
await connection.StartAsync();

var channel = new StreamMessageChannel(
    connection,
    new LengthFieldMessagePipe(LengthField.FourBytes));

await channel.SendAsync(Encoding.UTF8.GetBytes("ping"));

var reply = await channel.ReceiveAsync();
if (reply.HasValue)
{
    var text = Encoding.UTF8.GetString(reply.Value.Payload.ToArray());
    Console.WriteLine($"client recv: {text}");
}

await channel.CloseAsync();
```

## 4. UDP 用法

UDP 天然按报文收发，所以最简单。不需要拆包时，`messagePipe` 传空就行。

### 直接收发报文

```csharp
using System.Net;
using System.Text;
using Wombat.Network.Channels;
using Wombat.Network.Transports.Udp;

var serverEndPoint = new IPEndPoint(IPAddress.Loopback, 6000);

using var serverTransport = new UdpDatagramTransport(serverEndPoint);
await serverTransport.StartAsync();
var serverChannel = new DatagramMessageChannel(serverTransport);

using var clientTransport = new UdpDatagramTransport(defaultRemoteEndPoint: serverEndPoint);
await clientTransport.StartAsync();
var clientChannel = new DatagramMessageChannel(clientTransport, serverEndPoint);

await clientChannel.SendAsync(Encoding.UTF8.GetBytes("hello udp"));

var received = await serverChannel.ReceiveAsync();
if (received.HasValue)
{
    var text = Encoding.UTF8.GetString(received.Value.Payload.ToArray());
    Console.WriteLine($"server recv: {text}");

    await serverChannel.SendToAsync(
        Encoding.UTF8.GetBytes("world udp"),
        received.Value.RemoteEndPoint);
}
```

### UDP 上加长度前缀 framing

通常没必要，但库支持：

```csharp
using Wombat.Network.Channels;
using Wombat.Network.Protocols.Framing;
using Wombat.Network.Transports.Udp;

var pipe = new LengthFieldMessagePipe(LengthField.FourBytes);
var channel = new DatagramMessageChannel(transport, defaultRemoteEndPoint, pipe);
```

## 5. WebSocket 用法

WebSocket 分两步：

1. 先基于 TCP 建立连接
2. 先做握手，再用 `WebSocketMessageChannel`

### 服务端

```csharp
using System.Net;
using Wombat.Network.Channels;
using Wombat.Network.Protocols.WebSocket;
using Wombat.Network.Transports.Tcp;

var listener = new TcpTransportListener(new IPEndPoint(IPAddress.Any, 7000));
await listener.StartAsync();

var connection = (TcpTransportConnection)await listener.AcceptAsync();
await connection.StartAsync();

var request = await WebSocketHandshakeMiddleware.AcceptServerAsync(connection);
Console.WriteLine(request.RequestTarget);

var channel = new WebSocketMessageChannel(connection, isClient: false);
await channel.SendTextAsync("connected");
```

### 客户端

```csharp
using System.Net;
using Wombat.Network.Channels;
using Wombat.Network.Protocols.WebSocket;
using Wombat.Network.Transports.Tcp;

var endPoint = new IPEndPoint(IPAddress.Loopback, 7000);
var connection = await TcpTransportConnection.ConnectAsync(endPoint);
await connection.StartAsync();

await WebSocketHandshakeMiddleware.AcceptClientAsync(
    connection,
    host: $"127.0.0.1:{endPoint.Port}",
    path: "/chat");

var channel = new WebSocketMessageChannel(connection, isClient: true);
await channel.SendTextAsync("hello");

var message = await channel.ReceiveAsync();
if (message.HasValue && message.Value.MessageType == WebSocketMessageType.Text)
{
    Console.WriteLine("client recv text");
}
```

### WebSocketMessageChannel 常用方法

- `SendTextAsync(string text)`
- `SendBinaryAsync(ReadOnlyMemory<byte> payload)`
- `SendPingAsync(...)`
- `SendPongAsync(...)`
- `SendCloseAsync(...)`
- `ReceiveAsync()`

## 6. 常见坑

### 1) TCP 不能裸用 `ReceiveAsync()` 当消息边界

TCP 是流，不是消息。要么自己实现 `IMessagePipe`，要么直接用：

```csharp
new LengthFieldMessagePipe(LengthField.FourBytes)
```

### 2) `ReceiveAsync()` 返回 `null`

这通常表示：

- 对端已经关闭
- 底层输入完成
- UDP + framing 时，收到的数据包不够组成一条完整消息

### 3) UDP 发送时报 `Remote endpoint is required for UDP send`

说明你既没有：

- 在 `UdpDatagramTransport` 里设置 `defaultRemoteEndPoint`

也没有：

- 调用 `SendToAsync(..., remoteEndPoint)`

### 4) WebSocket 不能直接 new channel 就发消息

必须先握手：

```csharp
await WebSocketHandshakeMiddleware.AcceptServerAsync(connection);
await WebSocketHandshakeMiddleware.AcceptClientAsync(connection, host, path);
```

## 7. 仓库里的现成示例

最直接的例子就在测试项目里：

- `Tests/Wombat.Network.TcpTest`
- `Tests/Wombat.Network.UdpTest`
- `Tests/Wombat.Network.WebSokcetTest`
- `Wombat.Network.UnitTest/NewModel`

可以直接运行：

```powershell
dotnet run --project .\Tests\Wombat.Network.TcpTest\Wombat.Network.TcpTest.csproj
dotnet run --project .\Tests\Wombat.Network.UdpTest\Wombat.Network.UdpTest.csproj
dotnet run --project .\Tests\Wombat.Network.WebSokcetTest\Wombat.Network.WebSokcetTest.csproj
dotnet test .\Wombat.Network.UnitTest\Wombat.Network.UnitTest.csproj
```

## 8. 公开 API 对照

当前公开模型以 [..\Wombat.Network.API.md](..\Wombat.Network.API.md) 为准。

如果你在旧代码里还看到了这些名字，它们已经不是当前推荐入口：

- `TcpSocket*`
- `UdpSocket*`
- `WebSocketClient/Server/Session`
- `PipelineSocketConnection`

## 9. 推荐起步方式

不想想太多，就按这三个模板选：

- 自定义 TCP 协议：`TcpTransportConnection + StreamMessageChannel + LengthFieldMessagePipe`
- 简单局域网报文：`UdpDatagramTransport + DatagramMessageChannel`
- WebSocket：`TcpTransportConnection + WebSocketHandshakeMiddleware + WebSocketMessageChannel`

这就是现在这个库的主路径。先跑测试项目，再照着改成你的业务协议，基本够用。

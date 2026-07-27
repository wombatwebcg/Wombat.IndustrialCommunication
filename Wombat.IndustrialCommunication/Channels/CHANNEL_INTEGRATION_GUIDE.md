# ChannelManager 第三方对接指南

本文档说明 `Wombat.IndustrialCommunication.Channels` 的公开使用方式。它负责管理一个物理通信端点的连接生命周期、并发访问、超时、断线重连和运行状态；具体的寄存器/变量读写仍由对应协议客户端完成。

## 1. 依赖与命名空间

引用 `Wombat.IndustrialCommunication` 程序集，并按协议引入命名空间：

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Channels;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.Models;
using Wombat.IndustrialCommunication.PLC;
```

`ChannelManager` 默认通过内置工厂创建客户端。当前支持：

| 通道配置 | 客户端 | 默认端口/参数 |
| --- | --- | --- |
| `ModbusTcpChannelOptions` | `ModbusTcpClient` | TCP `502` |
| `ModbusRtuChannelOptions` | `ModbusRtuClient` | `9600, 8N1, Handshake.None` |
| `SiemensS7ChannelOptions` | `SiemensClient` | TCP `102`，必须指定 `SiemensVersion` |
| `FinsTcpChannelOptions` | `FinsClient` | TCP `9600` |

## 2. 最小接入流程

一个物理端点对应一个唯一 `Id`。创建管理器、添加通道、通过 `ExecuteAsync` 执行协议操作，最后异步释放管理器：

```csharp
await using var channels = new ChannelManager();

await channels.AddAsync(new ModbusTcpChannelOptions(
    id: "line-1-modbus",
    host: "192.168.0.100",
    port: 502));

var result = await channels.ExecuteAsync(
    "line-1-modbus",
    (client, cancellationToken) =>
        new ValueTask<OperationResult<ushort>>(
            ((ModbusTcpClient)client).ReadHoldingRegisterAsync(
                stationNumber: 1,
                address: 0)));

if (!result.IsSuccess)
    throw new InvalidOperationException(result.Message);

ushort value = result.ResultValue;
```

`AddAsync` 注册通道后立即尝试首次连接。连接失败时运行时保留为 `Faulted` 并抛出 `ChannelException`，后续 `ExecuteAsync` 会按重连策略再次尝试，连接成功后才执行操作。

## 3. 通道配置

### 3.1 公共配置

所有 `ChannelOptions` 都包含：

| 属性 | 默认值 | 说明 |
| --- | --- | --- |
| `Id` | 必填 | 管理器内唯一，区分大小写；不可为空白 |
| `MaxConcurrency` | `1` | 同一通道允许同时进入协议操作的最大数量 |
| `ConnectTimeout` | `3s` | 每次连接尝试的超时 |
| `OperationTimeout` | `3s` | 操作排队等待和实际执行共用的总超时 |
| `Reconnect` | 新建默认对象 | 断线后的重连策略 |

有效性要求：`MaxConcurrency >= 1`，两个超时必须大于零，`Reconnect` 不能为空且 `MaxAttempts >= 1`。

### 3.2 Modbus TCP

```csharp
var options = new ModbusTcpChannelOptions("modbus-tcp", "192.168.0.100", 502)
{
    MaxConcurrency = 1,
    ConnectTimeout = TimeSpan.FromSeconds(5),
    OperationTimeout = TimeSpan.FromSeconds(3),
    Reconnect = new ReconnectOptions
    {
        InitialDelay = TimeSpan.FromMilliseconds(100),
        MaxDelay = TimeSpan.FromSeconds(2),
        MaxAttempts = 3
    }
};
```

### 3.3 Modbus RTU

```csharp
var options = new ModbusRtuChannelOptions("modbus-rtu", "COM3")
{
    BaudRate = 9600,
    DataBits = 8,
    Parity = System.IO.Ports.Parity.None,
    StopBits = System.IO.Ports.StopBits.One,
    Handshake = System.IO.Ports.Handshake.None
};
```

`PortName` 是操作系统串口名，例如 Windows 的 `COM3`。RTU 设备的站号和地址在读写方法中传入。

### 3.4 Siemens S7

```csharp
var options = new SiemensS7ChannelOptions(
    "s7-1200",
    "192.168.0.101",
    SiemensVersion.S7_1200,
    102)
{
    Rack = 0,
    Slot = 0
};
```

`Version` 不能为 `SiemensVersion.None`；`Rack` 和 `Slot` 按 PLC 实际配置填写。

### 3.5 Omron FINS TCP

```csharp
var options = new FinsTcpChannelOptions("fins-1", "192.168.0.102", 9600);
```

## 4. 执行协议操作

`ExecuteAsync` 的回调参数是 `IProtocolClient`，因此需要转换为与通道配置匹配的具体客户端。回调收到的 `CancellationToken` 必须继续传递给支持取消的协议方法。

常用入口如下：

| 客户端 | 典型入口 |
| --- | --- |
| `ModbusTcpClient` / `ModbusRtuClient` | `ReadCoilAsync`、`ReadHoldingRegisterAsync`、`ReadInputRegisterAsync`、`WriteCoilAsync`、`WriteHoldingRegisterAsync`、`BatchReadAsync`、`BatchWriteAsync` |
| `SiemensClient` | `BatchReadAsync`、`BatchWriteAsync`；单变量读写使用 `ReadAsync` / `WriteAsync` |
| `FinsClient` | `ReadAsync`、`ReadStringAsync`、`WriteAsync`、`WriteStringAsync`、`BatchReadAsync`、`BatchWriteAsync` |

Modbus 示例：

```csharp
var read = await channels.ExecuteAsync(
    "line-1-modbus",
    (client, cancellationToken) =>
        ((ModbusTcpClient)client).ReadHoldingRegistersAsync(
            stationNumber: 1,
            startAddress: 0,
            count: 10));

if (read.IsSuccess)
{
    ushort[] registers = read.ResultValue;
}
```

S7 示例：

```csharp
var read = await channels.ExecuteAsync(
    "s7-1200",
    (client, cancellationToken) =>
        ((SiemensClient)client).ReadAsync(
            "DB1.DBW0", 1, DataTypeEnums.UInt16));
```

批量读取地址和数据类型由具体协议客户端定义，第三方应按对应 PLC/Modbus 地址规则配置，不要把不同协议的地址格式混用。

## 5. 生命周期与状态

状态流转：

```text
Created -> Connecting -> Online
                         |
                         v
                      Faulted -> Reconnecting -> Online
                         |
                         v
                      Stopping -> Stopped
```

状态含义：

| 状态 | 含义 |
| --- | --- |
| `Created` | 已创建运行时，尚未开始连接 |
| `Connecting` | 首次连接中 |
| `Online` | 已连接，可执行操作 |
| `Faulted` | 连接或传输故障，等待下一次操作触发重连 |
| `Reconnecting` | 正在按策略重连 |
| `Stopping` | 正在取消操作并断开连接 |
| `Stopped` | 已停止，不可继续使用 |

监听状态变化：

```csharp
channels.StateChanged += (_, e) =>
{
    Console.WriteLine($"{e.ChannelId}: {e.Previous} -> {e.Current}");
};
```

状态事件订阅者抛出的异常会被吞掉，不会中断通道运行。应用层仍应在事件中做好日志记录，且不要在事件回调里执行长时间阻塞操作。

查询快照：

```csharp
if (channels.TryGetSnapshot("line-1-modbus", out var snapshot))
{
    Console.WriteLine($"{snapshot.State}, active={snapshot.ActiveOperations}, waiting={snapshot.WaitingOperations}");
    Console.WriteLine($"last error={snapshot.LastError}, failures={snapshot.ConsecutiveFailures}");
}
```

快照是副本，可安全在管理器外读取。时间字段均为 `DateTimeOffset?`，连接时间及操作时间使用 UTC。

## 6. 并发、超时和重连规则

- 同一通道的并发数由 `MaxConcurrency` 限制，默认值为 `1`，即严格串行。
- 超过并发数的请求进入等待队列；`OperationTimeout` 同时覆盖排队和执行时间。
- 调用方取消令牌只取消当前请求，不会取消共享的重连任务。
- 通道停止或移除时，会取消该通道上的活动及排队操作，然后断开客户端。
- `AddAsync` 的首次连接只尝试一次；后续操作发现未连接时，最多尝试 `Reconnect.MaxAttempts` 次。
- 重连间隔使用指数退避：第 `n` 次重试等待 `min(MaxDelay, InitialDelay * 2^(n-1))`，例如默认间隔为 `100ms、200ms、400ms`。
- `ChannelManager` 创建的底层客户端将 `ConnectTimeout`、`OperationTimeout` 同时设置为客户端的连接/发送/接收超时，并将客户端内部 `Retries` 设置为 `0`；重试由通道统一管理。

## 7. 结果与异常处理

协议方法通常返回 `OperationResult` 或 `OperationResult<T>`：

```csharp
try
{
    var result = await channels.ExecuteAsync(
        "line-1-modbus",
        (client, token) => ((ModbusTcpClient)client).ReadHoldingRegisterAsync(1, 0));

    if (!result.IsSuccess)
    {
        Console.Error.WriteLine($"失败: kind={result.FailureKind}, code={result.ErrorCode}, message={result.Message}");
        return;
    }

    Console.WriteLine(result.ResultValue);
}
catch (ChannelException ex)
{
    Console.Error.WriteLine($"通道失败: kind={ex.FailureKind}, message={ex.Message}");
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("调用方取消了操作。");
}
```

重点判断：

| 情况 | 处理方式 |
| --- | --- |
| `IsSuccess == false` | 读取 `FailureKind`、`ErrorCode`、`Message`；这是协议层返回的业务/协议失败 |
| `ChannelException` | 连接、重连或通道总超时失败；读取 `FailureKind` |
| `QueueTimeout` | 等待并发槽位超时，通常降低调用频率或提高 `MaxConcurrency` |
| `ConnectTimeout` | 连接尝试超时，检查网络、端口和设备状态 |
| `ReceiveTimeout` / `SendTimeout` / `TransportFailure` | 传输层问题；通道会进入故障状态，后续操作触发重连 |
| `OperationCanceledException` | 调用方取消、通道被移除或管理器释放；不要当作设备拒绝 |

对于写操作遇到 `OutcomeUnknown`，不要在未确认设备状态时盲目重试，避免重复写入。

## 8. 添加、移除和重启

```csharp
await channels.RemoveAsync("line-1-modbus");

await channels.RestartAsync(
    "line-1-modbus",
    new ModbusTcpChannelOptions("line-1-modbus", "192.168.0.110", 502));
```

- `RemoveAsync` 找不到通道时不报错。
- `RestartAsync` 要求参数中的 `options.Id` 与 `channelId` 完全一致；它会先停止并移除旧运行时，再创建并连接新运行时。
- 同一 `Id` 重复 `AddAsync` 会抛出 `InvalidOperationException`。
- 应用退出时使用 `await using` 或显式 `await channels.DisposeAsync()`，不要直接丢弃管理器。

## 9. 对接检查清单

1. 确认设备 IP/串口、端口、站号、S7 版本、Rack/Slot。
2. 为每个物理端点分配唯一 `Id`，不要让多个管理器同时管理同一端点。
3. 调用 `AddAsync` 注册并首次连接通道；连接失败时通道仍会保留，后续操作可再次连接。
4. 在所有协议调用中传递回调提供的 `CancellationToken`。
5. 同时处理 `OperationResult.IsSuccess`、`ChannelException` 和 `OperationCanceledException`。
6. 通过 `StateChanged` 和 `TryGetSnapshot` 输出连接状态与失败计数。
7. 写操作发生 `OutcomeUnknown` 时先核实设备状态，再决定是否重试。

## 10. 当前边界

- `Channels` 当前只支持上述四种内置配置类型；其他 `ChannelOptions` 会抛出 `NotSupportedException`。
- `ChannelManager` 面向主动连接设备，不是服务端监听器；服务端会话不应注册为通道。
- 本文档不定义各 PLC 的地址语法、数据字节序或业务数据模型，这些由具体客户端和设备协议决定。

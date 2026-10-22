# Wombat.IndustrialCommunication

开源的 .NET 工业通讯库，面向 PLC、仪表、采集网关等场景，提供 **Modbus TCP / Modbus RTU / 西门子 S7 / 欧姆龙 FINS** 等协议的客户端与服务端实现，并附带蓝牙透传（Modbus RTU）、采集网关（Gateway）等扩展。

> 本 README 面向**第三方接入方**，依据当前代码库（`Version 2.1.56-preview`）的真实 API 编写。文中示例可直接映射到源码中的公开类型。

---

## 目录

- [1. 项目简介](#1-项目简介)
- [2. 协议支持状态](#2-协议支持状态)
- [3. 目标框架与依赖](#3-目标框架与依赖)
- [4. 安装](#4-安装)
- [5. 核心概念](#5-核心概念)
- [6. 快速开始](#6-快速开始)
- [7. Modbus 客户端（TCP / RTU）](#7-modbus-客户端tcp--rtu)
- [8. 西门子 S7 客户端](#8-西门子-s7-客户端)
- [9. 欧姆龙 FINS 客户端](#9-欧姆龙-fins-客户端)
- [10. 通道管理 ChannelManager（推荐的生产接入方式）](#10-通道管理-channelmanager推荐的生产接入方式)
- [11. 服务端（Modbus / S7 模拟）](#11-服务端modbus--s7-模拟)
- [12. 蓝牙扩展（Bluetooth Modbus RTU）](#12-蓝牙扩展bluetooth-modbus-rtu)
- [13. 采集网关 Gateway](#13-采集网关-gateway)
- [14. 项目结构](#14-项目结构)
- [15. 构建与测试](#15-构建与测试)
- [16. 常见问题 / 注意事项](#16-常见问题--注意事项)
- [17. 文档索引](#17-文档索引)
- [18. 贡献指南](#18-贡献指南)
- [19. 许可证](#19-许可证)

---

## 1. 项目简介

Wombat.IndustrialCommunication 是一个基于 **.NET Standard 2.0 / .NET 8 / .NET 10** 的工业通讯库，封装了主流工业协议的报文细节，向调用方暴露**统一、类型安全**的读写接口。库内同时提供**客户端**（主动采集）与**服务端**（从站 / PLC 模拟）实现，便于构建采集程序、协议网关与自动化测试环境。

典型使用场景：

- 上位机 / SCADA / MES 采集 PLC 与仪表数据
- 数据采集网关（可参考内置 `Wombat.IndustrialCommunication.Gateway`）
- 协议转换与转发（Modbus ↔ S7 ↔ FINS）
- 无真实设备时的 PLC / Modbus 从站模拟（服务端）
- 蓝牙透传设备（BLE 转 Modbus RTU）接入

---

## 2. 协议支持状态

代码库当前**实际实现**的协议如下。Package 描述中提及但尚未落地的协议，均明确标注为**规划中**，第三方请勿据此对接。

| 协议 / 设备 | 状态 | 客户端 | 服务端 |
|---|---|---|---|
| Modbus TCP | ✅ 已实现 | `ModbusTcpClient` | `ModbusTcpServer` |
| Modbus RTU | ✅ 已实现 | `ModbusRtuClient` | `ModbusRtuServer` |
| 西门子 S7（S7-200 / 200Smart / 300 / 400 / 1200 / 1500） | ✅ 已实现 | `SiemensClient` | `S7TcpServer` |
| 欧姆龙 FINS（Ethernet） | ✅ 已实现 | `FinsClient` | — |
| 蓝牙透传 Modbus RTU | ✅ 已实现（扩展包） | `ModbusRtuBluetoothClient` | `ModbusRtuBluetoothServer` |
| 三菱 Qna-3E / A-1E、FX 编程口 | 🚧 规划中 | — | — |
| 汇川 H3U | 🚧 规划中 | — | — |
| Allen-Bradley (CIP) | 🚧 规划中 | — | — |

> 说明：仓库中存在 `MitsubishiVersion` 枚举、`MitsubishiMCAddress` / `AllenBradleyAddress` 模型类及部分本地化字符串，但它们**尚未被任何客户端实现消费**，不构成可用能力。

---

## 3. 目标框架与依赖

**核心库** `Wombat.IndustrialCommunication`

- 目标框架：`netstandard2.0` / `net8.0` / `net10.0`
- 语言版本：C# 10
- AOT：在 `net8.0+` 上标记 `IsAotCompatible=true`
- NuGet 依赖：
  - `Microsoft.Extensions.Logging` 10.0.12
  - `System.IO.Ports` 10.0.12
  - `System.Reactive` 7.0.0
  - `Wombat.Extensions.DataTypeExtensions` 1.1.1
  - `Wombat.Network` 1.1.9

**蓝牙扩展** `Wombat.IndustrialCommunication.Extensions.Bluetooth`

- 目标框架：`netstandard2.0` / `net10.0`

**采集网关** `Wombat.IndustrialCommunication.Gateway`

- 目标框架：`net10.0`（ASP.NET Core）
- 依赖：`FreeSql.Provider.Sqlite`、`FreeSql.Provider.PostgreSQL`、`Microsoft.AspNetCore.Authentication.JwtBearer`、`Wombat.Extensions.FreeSql`

---

## 4. 安装

### 通过 NuGet 安装

```
Install-Package Wombat.IndustrialCommunication
```

或在 NuGet 包管理器中搜索 `Wombat.IndustrialCommunication`。蓝牙扩展：

```
Install-Package Wombat.IndustrialCommunication.Extensions.Bluetooth
```

### 从源码编译

```bash
git clone https://github.com/wombatwebcg/Wombat.IndustrialCommunication.git
# 使用 Visual Studio 打开 Wombat.IndustrialCommunication.sln
# 或命令行编译：
dotnet build Wombat.IndustrialCommunication.sln -c Release
```

---

## 5. 核心概念

### 5.1 统一返回类型 `OperationResult` / `OperationResult<T>`

所有读写与连接操作都返回 `OperationResult`（`Wombat.IndustrialCommunication.Models`），避免依赖异常控制流程：

```csharp
public class OperationResult
{
    public bool IsSuccess { get; set; }             // 是否成功
    public string Message { get; set; }             // 错误/成功描述
    public int ErrorCode { get; set; }              // 错误码（成功为 0）
    public Exception Exception { get; set; }        // 原始异常（如有）
    public OperationFailureKind FailureKind { get; set; } // 结构化故障分类
    public double? TimeConsuming { get; internal set; }   // 耗时(ms)
    public List<string> Requsts { get; set; }       // 请求报文(十六进制文本)
    public List<string> Responses { get; set; }     // 响应报文(十六进制文本)
}

public class OperationResult<T> : OperationResult
{
    public T ResultValue { get; set; }              // 泛型返回值
}
```

`FailureKind`（`OperationFailureKind`）用于结构化判断故障类型，取值包括：`None / Timeout / ConnectionClosed / Transport / Cancelled / Validation / Protocol / Business / InvalidConfiguration / InvalidAddress / QueueTimeout / ConnectTimeout / SendTimeout / ReceiveTimeout / TransportFailure / ProtocolViolation / DeviceRejected / OutcomeUnknown`。

> 当 `IsSuccess == true` 时，忽略 `Message` 与 `ErrorCode`。

### 5.2 读写接口 `IReadWrite`

`IReadWrite`（`Wombat.IndustrialCommunication.Models`）是客户端与服务端共用的读写契约，所有读写方法都以**地址字符串**寻址（Modbus 的站号/功能码就编码在该字符串中）：

| 类别 | 方法（同步 / 异步） |
|---|---|
| 布尔 | `ReadBoolean(address[, length])` / `Write(address, bool)` |
| 整型 | `ReadByte / ReadInt16 / ReadUInt16 / ReadInt32 / ReadUInt32 / ReadInt64 / ReadUInt64`（含数组重载） |
| 浮点 | `ReadFloat / ReadDouble`（含数组重载） |
| 字符串 | `ReadString(address, length)` |
| 对象化 | `Read(DataTypeEnums, address[, length])` / `Write(DataTypeEnums, address, object)` |
| 批量 | `BatchRead(Dictionary<string, DataTypeEnums>)` / `BatchWrite(Dictionary<string, (DataTypeEnums,object)>)` |

- 同步方法内部包装 `Task.Run(...).GetAwaiter().GetResult()`，**异步方法（`...Async`）为推荐用法**。
- `length` 语义为**元素个数**（如 `ReadInt16(addr, 3)` 读取 3 个 Int16）。

### 5.3 客户端协议接口

```
IProtocolClient            // bool Connected; ConnectAsync(); DisconnectAsync()
  └─ IClient              // + Version / Logger / IClientConfiguration
       └─ IDeviceClient   // + IReadWrite
```

- `ConnectAsync(CancellationToken)` / `DisconnectAsync()` 是**唯一的连接/断开方式**（无同步 `Connect()`）。
- `IClientConfiguration` 提供：`Retries`、`WaitToRetryMilliseconds`、`ConnectTimeout`、`ReceiveTimeout`、`SendTimeout`、`ResponseInterval`。
- 每个客户端均有 `IsLongConnection`（默认 `true`）：`true` 为长连接（复用 socket），`false` 为短连接（每次操作自动 连接→操作→断开）。

### 5.4 地址字符串（Modbus 通用）

Modbus 客户端 / 服务端支持两种地址格式：

- **标准格式**：`站号;功能码;地址`，例如 `1;3;0`
- **增强（逻辑）格式**：`站号;逻辑地址`，例如

  | 逻辑地址 | 寄存器区 | 读功能码 | 写功能码 |
  |---|---|---|---|
  | `0xxxx`（如 `1;10001` → 线圈 #1） | 线圈 Coil | 0x01 | 0x05 / 0x0F |
  | `1xxxx`（如 `1;20001` → 离散输入 #1） | 离散输入 | 0x02 | — |
  | `3xxxx`（如 `1;30001` → 输入寄存器 #1） | 输入寄存器 | 0x04 | — |
  | `4xxxx`（如 `1;40001` → 保持寄存器 #1） | 保持寄存器 | 0x03 | 0x06 / 0x10 |

  > 逻辑地址 `40001` 映射为协议地址 `0`（即 `逻辑号 - 1`）。

---

## 6. 快速开始

三种接入方式，按场景选择：

| 方式 | 适用场景 | 入口类型 |
|---|---|---|
| 直接使用协议客户端 | 单连接、简单脚本 | `ModbusTcpClient` / `SiemensClient` / `FinsClient` ... |
| **`ChannelManager`（推荐）** | 生产环境：自动重连、并发控制、状态快照 | `Wombat.IndustrialCommunication.Channels.ChannelManager` |
| `IReadWrite` 地址字符串 | 与通道/客户端解耦的统一读写 | 上述客户端均实现 `IDeviceClient : IReadWrite` |

最简 Modbus TCP 采集：

```csharp
using Wombat.IndustrialCommunication.Modbus;
using Wombat.Extensions.DataTypeExtensions;

var client = new ModbusTcpClient("192.168.1.10", 502);

var connect = await client.ConnectAsync();
if (!connect.IsSuccess) throw new Exception(connect.Message);

var value = await client.ReadUInt16Async("1;3;0");   // 站号1，读保持寄存器，地址0
if (value.IsSuccess)
    Console.WriteLine($"值 = {value.ResultValue}");

await client.DisconnectAsync();
```

---

## 7. Modbus 客户端（TCP / RTU）

命名空间：`Wombat.IndustrialCommunication.Modbus`

### 7.1 构造

```csharp
// Modbus TCP
var tcp = new ModbusTcpClient("192.168.1.10", 502); // (ip, port=502)，ip 支持域名

// Modbus RTU（串口）
using System.IO.Ports;
var rtu = new ModbusRtuClient(
    portName: "COM3",
    baudRate: 9600,
    dataBits: 8,
    stopBits: StopBits.One,
    parity: Parity.None,
    handshake: Handshake.None);
```

### 7.2 连接 / 断开

```csharp
await tcp.ConnectAsync();     // Task<OperationResult>
await tcp.DisconnectAsync();
```

常用属性：`ConnectTimeout` / `ReceiveTimeout` / `SendTimeout`（`TimeSpan`）、`Retries`、`WaitToRetryMilliseconds`、`IsLongConnection`、`ResponseInterval`、`BatchReadStationInterval`（批量跨站号间隔）。

### 7.3 IModbusClient 快捷接口（站号 + 协议地址）

`ModbusTcpClient` / `ModbusRtuClient` 实现了 `IModbusClient`，可直接按“站号 + 0 基协议地址”读写，无需拼接字符串：

```csharp
byte station = 1;

// 线圈（功能码 01 / 05 / 0F）
await tcp.WriteCoilAsync(station, 0, true);
var coil   = await tcp.ReadCoilAsync(station, 0);
var coils  = await tcp.ReadCoilsAsync(station, 0, 8);        // 读 8 个线圈
await tcp.WriteCoilsAsync(station, 0, new[] { true, false });

// 离散输入（功能码 02）
var di = await tcp.ReadDiscreteInputAsync(station, 0);

// 保持寄存器（功能码 03 / 06 / 10）
await tcp.WriteHoldingRegisterAsync(station, 0, 1234);
var reg  = await tcp.ReadHoldingRegisterAsync(station, 0);
var regs = await tcp.ReadHoldingRegistersAsync(station, 0, 10);
await tcp.WriteHoldingRegistersAsync(station, 0, new ushort[] { 1, 2, 3 });

// 输入寄存器（功能码 04）
var input = await tcp.ReadInputRegisterAsync(station, 0);
```

同步版本同样存在：`ReadCoil / ReadCoils / ReadDiscreteInput(s) / ReadHoldingRegister(s) / ReadInputRegister(s)` 及 `WriteCoil(s) / WriteHoldingRegister(s)`。

### 7.4 地址字符串读写（跨协议统一风格）

通过基类 `DeviceDataReaderWriterBase` 提供的类型化方法：

```csharp
// 标准格式：站号;功能码;地址
var a = await tcp.ReadInt16Async("1;3;0");          // 功能码3 读保持寄存器
var b = await tcp.ReadBooleanAsync("1;1;0");        // 功能码1 读线圈
await tcp.WriteAsync("1;6;0", (short)123);          // 功能码6 写单寄存器
await tcp.WriteAsync("1;16;0", new short[] { 1, 2, 3 });

// 增强（逻辑）格式：站号;逻辑地址
var c = await tcp.ReadFloatAsync("1;40001");        // 保持寄存器 #1
var d = await tcp.ReadUInt16Async("1;30001");       // 输入寄存器 #1
```

### 7.5 批量读写（自动合并为最少报文）

```csharp
using Wombat.Extensions.DataTypeExtensions;

// 批量读：地址 -> 数据类型
var readMap = new Dictionary<string, DataTypeEnums>
{
    ["1;40001"] = DataTypeEnums.Float,
    ["1;40003"] = DataTypeEnums.UInt16,
    ["2;40001"] = DataTypeEnums.Int32,
};
var readResult = await tcp.BatchReadAsync(readMap);
if (readResult.IsSuccess)
{
    foreach (var kv in readResult.ResultValue)
        Console.WriteLine($"{kv.Key} => {kv.Value.Item2}");  // (DataType, Value)
}

// 批量写：地址 -> (数据类型, 值)
var writeMap = new Dictionary<string, (DataTypeEnums, object)>
{
    ["1;5;0"] = (DataTypeEnums.Bool, true),
    ["1;6;1"] = (DataTypeEnums.UInt16, (ushort)100),
};
var writeResult = await tcp.BatchWriteAsync(writeMap);
```

批量读返回 `OperationResult<Dictionary<string, (DataTypeEnums, object)>>`，键为**原始地址字符串**，值为 `(请求的数据类型, 值)`；某地址失败时对应值为 `null`，并把错误写入 `Message`。

批量机制自动按 `(站号, 功能码)` 将相邻地址合并为数据块（`maxBlockSize` 默认 125），跨站号按 `BatchReadStationInterval` 间隔（TCP 默认 1000ms，RTU 默认 200ms），可用 `BatchReadStationInterval` 调整。

### 7.6 报文跟踪

客户端的报文通过 `OperationResult.Requsts` / `Responses` 暴露（十六进制文本），可用于落日志与排障。

---

## 8. 西门子 S7 客户端

命名空间：`Wombat.IndustrialCommunication.PLC`

### 8.1 构造与连接

```csharp
using Wombat.IndustrialCommunication.PLC;

var plc = new SiemensClient(
    ip: "192.168.1.20",
    port: 102,
    siemensVersion: SiemensVersion.S7_1200,
    slot: 1,
    rack: 0);

var connect = await plc.ConnectAsync();
if (!connect.IsSuccess) throw new Exception(connect.Message);
```

`SiemensVersion` 取值：`S7_200`、`S7_200Smart`、`S7_300`、`S7_400`、`S7_1200`、`S7_1500`。

### 8.2 地址格式

| 区域 | 位 | 字节 | 字（2B） | 双字（4B） | 示例 |
|---|---|---|---|---|---|
| DB | `DBn.DBX字节.位` | `DBn.DBB字节` | `DBn.DBW字节` | `DBn.DBD字节` | `DB1.DBX0.0`、`DB1.DBW0`、`DB1.DBD0` |
| M（存储位） | `M字节.位` | `MB字节` | `MW字节` | `MD字节` | `M0.0`、`MW0` |
| I（输入） | `I字节.位` | `IB字节` | `IW字节` | `ID字节` | `I0.0`、`IW0` |
| Q（输出） | `Q字节.位` | `QB字节` | `QW字节` | `QD字节` | `Q0.0`、`QW0` |
| V（S7-200 Smart，映射到 DB1） | `V字节.位` | `VB字节` | `VW字节` | `VD字节` | `V0.0`、`VW0` |

### 8.3 读写

```csharp
await plc.WriteAsync("DB1.DBW0", (ushort)32000);
var w = await plc.ReadUInt16Async("DB1.DBW0");

await plc.WriteAsync("DB1.DBX2.0", true);
var x = await plc.ReadBooleanAsync("DB1.DBX2.0");

var f = await plc.ReadFloatAsync("DB200.DBD196");    // 读 DB200 起始于字节196 的浮点数
await plc.WriteAsync("M0.0", true);
```

### 8.4 批量读写

与 Modbus 相同的 `BatchReadAsync` / `BatchWriteAsync` 签名，地址使用 S7 地址字符串：

```csharp
var readMap = new Dictionary<string, DataTypeEnums>
{
    ["DB200.DBD196"] = DataTypeEnums.Float,
    ["MW0"]          = DataTypeEnums.UInt16,
};
var r = await plc.BatchReadAsync(readMap);
```

底层按 PDU 协商大小（`NegotiatedPduLimit`，默认 480）与 `EfficiencyRatio` 优化合并请求。

---

## 9. 欧姆龙 FINS 客户端

命名空间：`Wombat.IndustrialCommunication.PLC`

### 9.1 构造与连接

```csharp
var fins = new FinsClient("192.168.0.10", 9600, TimeSpan.FromSeconds(5));
await fins.ConnectAsync();
```

常用属性：`SourceNodeAddress` / `DestinationNodeAddress`（默认 `0x01` / `0x00`）、`Retries`（默认 2）、`ResponseInterval`（默认 50ms）。

### 9.2 地址格式

- 字地址：`区域码 + 数字`，如 `D100`、`CIO200`、`W300`、`H400`。
- 位地址：`区域码 + 数字.位号`，如 `D100.5`、`CIO200.10`。

| 区域 | 长码 / 短码 | 说明 |
|---|---|---|
| CIO | `CIO` | 公共 I/O |
| WR | `WR` / `W` | 工作继电器 |
| HR | `HR` / `H` | 保持继电器 |
| AR | `AR` / `A` | 辅助继电器 |
| DM | `DM` / `D` | 数据存储区 |
| EM | `EM` / `E` | 扩展数据存储区 |

> ⚠️ **批量操作（`BatchReadAsync` / `OptimizedBatchReadAsync`）只识别短码**（`D` / `W` / `H` / `A` / `E` / `T` / `C` / `CIO`），请使用 `D100` 而非 `DM100`。点对点 `ReadAsync` / `WriteAsync` 两种写法都支持。

### 9.3 读写

```csharp
using Wombat.Extensions.DataTypeExtensions;

var r = await fins.ReadAsync("D100", 1, DataTypeEnums.UInt16);
await fins.WriteAsync("D100", (ushort)123, DataTypeEnums.UInt16);

var s = await fins.ReadStringAsync("D200", 8);
await fins.WriteStringAsync("D200", "HELLO");

var batch = await fins.BatchReadAsync(new Dictionary<string, DataTypeEnums>
{
    ["D100"] = DataTypeEnums.UInt16,
    ["W300"] = DataTypeEnums.Int16,
});
```

另提供优化批量方法 `OptimizedBatchReadAsync` / `OptimizedBatchWriteAsync`（可控 `minEfficiencyRatio` / `maxBlockSize`）。

---

## 10. 通道管理 ChannelManager（推荐的生产接入方式）

命名空间：`Wombat.IndustrialCommunication.Channels`

`ChannelManager` 将“一个物理端点”注册为一个**通道**，统一负责：连接生命周期、**自动重连**（指数退避）、并发控制（`MaxConcurrency`）、操作超时、状态快照与状态变更事件。

### 10.1 注册与执行

```csharp
using Wombat.IndustrialCommunication.Channels;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.Extensions.DataTypeExtensions;

await using var channels = new ChannelManager();

// 注册一个 Modbus TCP 通道
await channels.AddAsync(new ModbusTcpChannelOptions("modbus-tcp-1", "192.168.0.100", 502));

// 在同一通道上执行操作（同一通道默认串行，受 MaxConcurrency 限制）
var result = await channels.ExecuteAsync(
    "modbus-tcp-1",
    async (client, ct) =>
        await ((ModbusTcpClient)client).BatchReadAsync(
            new Dictionary<string, DataTypeEnums>
            {
                ["1;40001"] = DataTypeEnums.UInt16,
                ["2;40001"] = DataTypeEnums.UInt16,
                ["3;40001"] = DataTypeEnums.UInt16,
            }, ct));
```

### 10.2 通道选项

所有选项继承自 `ChannelOptions`，公共可调项：

| 属性 | 默认 | 说明 |
|---|---|---|
| `MaxConcurrency` | 1 | 同通道最大并发操作数 |
| `ConnectTimeout` | 3s | 连接超时 |
| `OperationTimeout` | 3s | 单次操作超时 |
| `Reconnect.InitialDelay` | 100ms | 重连初始退避 |
| `Reconnect.MaxDelay` | 2s | 重连最大退避 |
| `Reconnect.MaxAttempts` | 3 | 重连最大次数 |

内置通道选项类型：

```csharp
new ModbusTcpChannelOptions(id, host, port = 502);
new ModbusRtuChannelOptions(id, portName) { BaudRate = 9600, DataBits = 8, Parity = Parity.None, StopBits = StopBits.One, Handshake = Handshake.None };
new SiemensS7ChannelOptions(id, host, version, port = 102) { Slot = 0, Rack = 0 };
new FinsTcpChannelOptions(id, host, port = 9600);
```

### 10.3 生命周期与状态

```csharp
channels.StateChanged += (_, e) =>
    Console.WriteLine($"{e.ChannelId}: {e.Previous} -> {e.Current}");

await channels.RestartAsync("modbus-tcp-1", new ModbusTcpChannelOptions("modbus-tcp-1", "192.168.0.101", 502));
await channels.RemoveAsync("modbus-tcp-1");

if (channels.TryGetSnapshot("modbus-tcp-1", out ChannelSnapshot snap))
    Console.WriteLine($"{snap.State} 连续失败={snap.ConsecutiveFailures} 重连次数={snap.ReconnectCount}");
```

`ChannelState` 取值：`Created / Connecting / Online / Faulted / Reconnecting / Stopping / Stopped`。
通道在传输类故障（`IOException` / `SocketException` / `ObjectDisposedException`）时会自动标记 `Faulted` 并在下次操作时重连。

> 也可通过 `new ChannelManager(Func<ChannelOptions, IProtocolClient> factory)` 注入自定义客户端工厂。

---

## 11. 服务端（Modbus / S7 模拟）

服务端用于**模拟从站 / PLC**，与真实设备对接时第三方程序可作为主站向它读写。

### 11.1 Modbus TCP / RTU 服务端

```csharp
using Wombat.IndustrialCommunication.Modbus;

var server = new ModbusTcpServer("0.0.0.0", 502);
server.SlaveId = 1;                    // 1..247
server.MaxConnections = 200;
server.UseLogger(logger);

var listen = await server.ListenAsync();   // 加载快照(如启用)后启动
// ...
await server.ShutdownAsync();              // 保存快照后停止
```

Modbus 服务端的核心是 `DataStore` 内存镜像：

```csharp
server.DataStore.CoilDiscretes[0]     = true;
server.DataStore.InputDiscretes[0]    = false;
server.DataStore.HoldingRegisters[0]  = 1234;
server.DataStore.InputRegisters[0]    = 5678;
```

支持的功能码：`0x01 / 0x02 / 0x03 / 0x04 / 0x05 / 0x06 / 0x0F / 0x10`；读线圈 ≤2000、读寄存器 ≤125、写线圈 ≤1968、写寄存器 ≤123。

> ⚠️ Modbus 服务端上的 `ReadXxx / WriteXxx / BatchXxx` 这类**客户端语义**接口返回“不支持”，属设计行为。请通过维护 `DataStore`（或服务端的类型化写接口）来更新从站数据。

RTU 服务端同理：

```csharp
var rtuServer = new ModbusRtuServer("COM3", 9600, 8, StopBits.One, Parity.None, Handshake.None);
await rtuServer.ListenAsync();
```

数据事件：

```csharp
server.DataWritten += (_, e) => Console.WriteLine($"写: {e.ModbusDataType} @{e.StartAddress}");
server.DataRead    += (_, e) => Console.WriteLine($"读: {e.ModbusDataType} @{e.StartAddress}");
```

### 11.2 西门子 S7 服务端

```csharp
using Wombat.IndustrialCommunication.PLC;

var s7Server = new S7TcpServer("0.0.0.0", 102);
s7Server.SetSiemensVersion(SiemensVersion.S7_1200);
s7Server.SetRackSlot(0, 1);
s7Server.CreateDataBlock(1, 1024);

var listen = await s7Server.ListenAsync();
```

S7 服务端既响应 S7 协议请求，又支持**本地按区域或按地址字符串读写**：

```csharp
s7Server.WriteDB(1, 0, new byte[] { 0x00, 0x64 });
var db = s7Server.ReadDB(1, 0, 2);

s7Server.WriteMerkers(0, new byte[] { 1, 2, 3, 4 });
var m = s7Server.ReadMerkers(0, 4);

s7Server.Write("DB1.DBW0", (ushort)32000);     // IReadWrite 风格
var w = s7Server.ReadUInt16("DB1.DBW0");
```

`S7DataStore` 暴露 `DataBlocks`（`Dictionary<int, MemoryLite<byte>>`）、`Merkers` / `Inputs` / `Outputs` / `Timers` / `Counters`。

事件：`s7Server.DataRead` / `s7Server.DataWritten`（`S7DataStoreEventArgs`）。

### 11.3 统一服务端宿主 `ServerHost`

`ServerHost`（`Wombat.IndustrialCommunication.Server`）封装单个服务端的启停与释放，适合在网关/宿主中批量托管多个服务端：

```csharp
using Wombat.IndustrialCommunication.Server;
using Wombat.IndustrialCommunication.PLC;

await using var host = new ServerHost("s7-server-0", new S7TcpServer("127.0.0.1", 21000));
await host.StartAsync();     // -> server.ListenAsync()
await host.StopAsync();      // -> server.ShutdownAsync()
```

### 11.4 快照持久化

Modbus / S7 服务端支持将 `DataStore` 快照落盘并在启动时恢复：

```csharp
server.EnableSnapshotPersistence = true;
server.SnapshotSaveInterval = TimeSpan.FromSeconds(5);
server.ConfigureSnapshotPersistence("my-server");  // 自定义文件名
// server.DeleteSnapshot();  /  server.ResetDataAndDeleteSnapshot();
```

---

## 12. 蓝牙扩展（Bluetooth Modbus RTU）

包：`Wombat.IndustrialCommunication.Extensions.Bluetooth`，命名空间根 `Wombat.IndustrialCommunication.Extensions.Bluetooth`。用于通过 BLE 透传通道跑 Modbus RTU。扩展本身**不绑定任何平台蓝牙 SDK**，由宿主实现 `IBluetoothChannel`（对接 Windows / Linux BlueZ / Android `BluetoothGatt` 等）。

链路：`IBluetoothChannel` → `BluetoothStreamAdapter`(`IStreamResource`) → `DeviceMessageTransport` → `ModbusRtuBluetoothClient`。

```csharp
using Wombat.IndustrialCommunication.Extensions.Bluetooth;

// 1) 宿主实现 IBluetoothChannel（负责发现设备/服务/特征值并收发字节）
IBluetoothChannel channel = /* 平台实现 */;

// 2) 创建客户端
var options = new BluetoothConnectionOptions
{
    DeviceId = "...",
    ServiceId = "...",
    WriteCharacteristicId = "...",
    NotifyCharacteristicId = "...",
};
var client = BluetoothClientFactory.CreateModbusRtuClient(channel, options);
await client.ConnectAsync();
var r = await client.ReadHoldingRegisterAsync(1, 0);   // 与 IModbusClient 相同
```

关键接口：

```csharp
public interface IBluetoothChannel : IDisposable
{
    bool Connected { get; }
    Task<OperationResult> ConnectAsync(CancellationToken ct);
    Task<OperationResult> DisconnectAsync();
    Task<OperationResult<int>> ReceiveAsync(byte[] buffer, int offset, int size, CancellationToken ct);
    Task<OperationResult> SendAsync(byte[] buffer, int offset, int size, CancellationToken ct);
}
```

服务端路径：`IBluetoothServerChannel`（派生自 `IBluetoothChannel`） → `BluetoothServerAdapter`(`IServerListener`) → `ModbusRtuBluetoothServer`，用 `BluetoothServerFactory.CreateModbusRtuServer(channel, options)` 创建；`BluetoothServerOptions` 支持 `SlaveId` 等配置。

> 注意：`Send` / `Receive` 必须**串行化**，且 `ReceiveAsync` 需在超时前返回可消费的完整字节，否则 Modbus RTU 帧会错位。

---

## 13. 采集网关 Gateway

项目：`Wombat.IndustrialCommunication.Gateway`（ASP.NET Core，`net10.0`），一个可独立部署的**数据采集网关**。以“通道（Channel）+ 点位（Point）”为中心，提供：

- 数据采集（定时轮询）、点位读写
- 转发：变化转发（OnChange）/ 定时转发（Timed）/ 双向（Bidirectional）
- 历史采样与聚合、采集/操作/登录日志
- JWT 鉴权 + RBAC 权限
- 既能作为**客户端**接入 ModbusTcp / ModbusRtu / S7 / FinsTcp，也能作为**服务端**托管 ModbusTcp / ModbusRtu / S7

### 13.1 运行与配置

```bash
# 在 Gateway 项目目录下
dotnet run
```

- 默认监听 `https://localhost:7564; http://localhost:7565`（见 `Properties/launchSettings.json`）。
- **JWT 配置为必填**：未提供 `Auth:JwtKey` 时启动会直接抛错。可通过用户机密 / 环境变量提供，例如：

  ```bash
  Auth__JwtKey='01234567890123456789012345678901' dotnet run
  ```

- 数据库：默认 SQLite（`Database/gateway.db3`，`IsSyncStructure=true` 自动建表）；生产可切换 PostgreSQL。
- 自注册默认关闭（`Auth:AllowSelfRegistration=false`）。
- 首次启动且设置了 `BOOTSTRAP_ADMIN_PASSWORD` 环境变量时，会创建管理员 `admin` 并授予 `Admin` 角色。

### 13.2 HTTP API 概览

鉴权：`POST /api/auth/login` 获取 `AccessToken`（8 小时）与 `RefreshToken`（30 天），后续请求带 `Authorization: Bearer <token>`。

| 方法 | 路由 | 说明 | 权限 |
|---|---|---|---|
| POST | `/api/auth/login` | 登录 | — |
| POST | `/api/auth/register` | 自注册（需开启） | — |
| POST | `/api/auth/refresh` | 刷新令牌 | — |
| POST | `/api/auth/change-password` | 修改本人密码 | `profile:operate` |
| GET | `/api/users` | 用户列表 | `user:view` |
| POST | `/api/users` | 创建用户 | `user:create` |
| PUT | `/api/users/{id}/roles` | 设置角色 | `user:update` |
| PUT | `/api/users/{id}/disabled` | 启用/禁用 | `user:update` |
| POST | `/api/users/{id}/reset-password` | 重置密码 | `user:update` |
| GET | `/api/roles` | 角色列表 | `role:view` |
| GET | `/api/permissions` | 权限列表 | `permission:view` |
| GET | `/api/channels` | 通道列表 | `channel:view` |
| GET | `/api/channels/{id}` | 通道详情 + 运行时快照 | `channel:view` |
| POST | `/api/channels` | 创建通道（同步运行时） | `channel:create` |
| PUT | `/api/channels/{id}` | 更新通道 | `channel:update` |
| DELETE | `/api/channels/{id}` | 删除通道（级联点位/转发） | `channel:delete` |
| POST | `/api/channels/{id}/start` | 启动通道 | `channel:operate` |
| POST | `/api/channels/{id}/stop` | 停止通道 | `channel:operate` |
| GET | `/api/channels/{id}/snapshot` | 运行时快照 | `channel:view` |
| GET | `/api/points?channelId=` | 点位列表 | `channel:view` |
| GET | `/api/points/{id}` | 点位详情 | `channel:view` |
| POST | `/api/channels/{channelId}/points` | 新增点位 | `channel:create` |
| PUT | `/api/points/{id}` | 更新点位 | `channel:update` |
| DELETE | `/api/points/{id}` | 删除点位 | `channel:delete` |
| POST | `/api/points/{id}/read` | 读取单点 | `channel:view` |
| POST | `/api/points/{id}/write` | 写入单点 | `channel:operate` |
| POST | `/api/points/read` | 批量读 | `channel:view` |
| POST | `/api/points/write` | 批量写 | `channel:operate` |
| GET | `/api/forwardings` | 转发规则列表 | `channel:view` |
| POST | `/api/forwardings` | 创建转发 | `channel:create` |
| PUT | `/api/forwardings/{id}` | 更新转发 | `channel:update` |
| DELETE | `/api/forwardings/{id}` | 删除转发 | `channel:delete` |
| GET | `/api/history/policies?pointId=` | 历史策略列表 | `history:view` |
| PUT | `/api/history/policies/{pointId}` | 设置历史策略 | `history:update` |
| GET | `/api/history/samples` | 历史采样 | `history:view` |
| GET | `/api/history/aggregates` | 历史聚合 | `history:view` |
| GET | `/api/logs/collection` | 采集日志 | `log:view` |
| GET | `/api/logs/operation` | 操作日志 | `log:view` |
| GET | `/api/logs/login` | 登录日志 | `log:view` |
| GET | `/api/logs/collection/export` | 导出采集日志 CSV | `log:view` |
| DELETE | `/api/logs?before=` | 清理日志 | `log:delete` |

主要请求 DTO（`GatewayRuntime` / `GatewayEndpoints`）：

```csharp
ChannelRequest   { Name, Protocol="ModbusTcp", Role="Client", Enabled=true, JsonElement Configuration,
                   MaxConcurrency=1, ConnectTimeoutMs=3000, OperationTimeoutMs=3000 }
PointRequest     { Name, Address, DataType="UInt16", Writable, Enabled=true, ScanIntervalMs=1000, ValueTransformJson? }
ForwardingRequest{ SourcePointId, TargetPointId, ForwardingMode Mode=OnChange, IntervalMs=1000, RetryCount=3, Enabled=true }
PointWriteRequest      { JsonElement Value }
PointBatchReadRequest  { long[] PointIds }
PointBatchWriteRequest { Dictionary<long, JsonElement> Values }
```

`Protocol` 支持 `ModbusTcp` / `ModbusRtu` / `S7` / `FinsTcp`；`Role` 取 `Client`（客户端采集）或 `Server`（托管服务端）。

---

## 14. 项目结构

```
Wombat.IndustrialCommunication.sln
├─ Wombat.IndustrialCommunication/                     # 核心库
│  ├─ Abstractions/    # IClient / IDeviceClient / IProtocolClient / IReadWrite 相关契约
│  ├─ Adapters/        # TcpClientAdapter / TcpServerAdapter / SerialPortAdapter / SerialPortServerAdapter
│  ├─ Channels/        # ChannelManager / ChannelOptions / ChannelRuntime / ChannelSnapshot
│  ├─ Core/            # DeviceDataReaderWriterBase / ObservableArray / ProtocolDataNumber
│  ├─ Enums/           # SiemensVersion / MitsubishiVersion / ModbusStandardFunctionCode / OperationMethod ...
│  ├─ Events/          # DataReceivedEventArgs / PacketTraceEventArgs / SessionEventArgs
│  ├─ Helpers/         # CRC16 / LRC / AsyncLock / SerialConfigOptions / DiscriminatedUnion
│  ├─ Messaging/       # DeviceMessageTransport / DeviceReadWriteMessage / DeviceMessageFactory
│  ├─ Modbus/          # ModbusTcpClient|Server、ModbusRtuClient|Server、IModbusClient、地址解析、批量、DataStore
│  ├─ PLC/
│  │  ├─ S7/           # SiemensClient / S7TcpServer / S7DataStore / 批量与报文构建
│  │  ├─ FINS/         # FinsClient / FinsAddress / FinsBatchHelper / FinsEthernetTransport
│  │  └─ Models/       # 地址模型
│  ├─ Models/          # OperationResult / IReadWrite / StringResources ...
│  └─ Server/          # IServer / ServerBase / DeviceServerBase / ServerHost / 快照持久化
├─ Wombat.IndustrialCommunication.Extensions.Bluetooth/ # 蓝牙透传 Modbus RTU 扩展
├─ Wombat.IndustrialCommunication.Gateway/              # 采集网关 (ASP.NET Core)
├─ Wombat.IndustrialCommunicationTestProject/           # xUnit 单元/集成测试
├─ Wombat.IndustrialCommunication.ClientTest/           # 客户端示例（Modbus TCP 批量读）
├─ Wombat.IndustrialCommunication.ServerTest/           # 服务端示例（批量托管 S7TcpServer）
├─ 四类通信组件用法指南.md
└─ docs/                                                # 设计/修复方案文档
```

---

## 15. 构建与测试

```bash
# 还原 + 编译
dotnet build Wombat.IndustrialCommunication.sln -c Release

# 运行测试
dotnet test Wombat.IndustrialCommunicationTestProject/Wombat.IndustrialCommunicationTestProject.csproj
```

测试项目覆盖：Modbus CRC16 / 地址解析 / 批量算法 / 报文一致性、S7 握手与批量、Modbus 与 S7 服务端快照持久化、`ServerHost` 生命周期、`ChannelManager`、传输层超时与分帧等。

---

## 16. 常见问题 / 注意事项

- **没有同步 `Connect()` / `Disconnect()`**：客户端一律使用 `ConnectAsync()` / `DisconnectAsync()`。
- **服务端启用**：使用 `ListenAsync()` / `ShutdownAsync()`（或 `ServerHost`）；`ServerBase` 上的同步 `Listen()` / `Shutdown()` 仅供 `DeviceServer` 分支使用，协议服务端请优先用异步方法。
- **Modbus 服务端不是采集客户端**：其 `ReadXxx / WriteXxx` 返回“不支持”，请维护 `DataStore`。
- **长/短连接**：默认 `IsLongConnection = true`；短连接模式下每次操作都会连接/断开，吞吐较低但更健壮。
- **重连不在客户端**：单个客户端只执行一次协议操作，**恢复策略由 `ChannelManager` 承担**。生产环境建议统一用通道。
- **FINS 批量短码**：批量接口只认 `D/W/H/A/E/T/C/CIO` 短码。
- **S7 地址需带大小标识**：`DB1.0` 非法，应为 `DB1.DBX0.0` / `DB1.DBB0` / `DB1.DBW0` / `DB1.DBD0`。
- **规划中协议**：三菱 Q3E/FX、汇川 H3U、Allen-Bradley 尚未实现，请勿对接。
- **AOT/裁剪**：`net8.0+` 标记 AOT 兼容，但请以实际发布裁剪验证为准。

---

## 17. 文档索引

| 文档 | 说明 |
|---|---|
| [`四类通信组件用法指南.md`](四类通信组件用法指南.md) | 六个核心组件（Modbus 客户端/服务端、S7 客户端/服务端等）的跨项目接入指南 |
| [`docs/MODBUS_S7_RELIABILITY_REMEDIATION_PLAN.md`](docs/MODBUS_S7_RELIABILITY_REMEDIATION_PLAN.md) | Modbus / S7 可靠性修复两阶段整改计划 |
| [`docs/S7批量读取连接失步修复方案.md`](docs/S7批量读取连接失步修复方案.md) | S7 长连接批量读取失步问题的原因与修复方案 |
| [`docs/gateway-implementation-plan.md`](docs/gateway-implementation-plan.md) | 采集网关 Gateway 的实现计划 |
| [`LICENSE.md`](LICENSE.md) | 许可证 |

---

## 18. 贡献指南

1. Fork 本仓库
2. 创建特性分支（`git checkout -b feature/amazing-feature`）
3. 提交更改（`git commit -m 'Add some amazing feature'`）
4. 推送分支（`git push origin feature/amazing-feature`）
5. 发起 Pull Request

欢迎的贡献类型：新增协议客户端/服务端、性能与稳定性改进、缺陷修复、文档完善、单元测试与示例。

代码要求：遵循现有代码风格；为变更补充单元测试；确保测试通过；同步更新相关文档。

---

## 19. 许可证

本项目采用 **MIT License**，详见 [`LICENSE.md`](LICENSE.md)。

Copyright (c) 2023 wombatwebcg

### 相关项目

- [Wombat.Network](https://github.com/wombatwebcg/Wombat.Network) — 网络通信库
- [Wombat.Extensions.DataTypeExtensions](https://github.com/wombatwebcg/Wombat.Extensions.DataTypeExtensions) — 数据类型扩展库

### 联系方式

- GitHub：[@wombatwebcg](https://github.com/wombatwebcg)
- 项目地址：[Wombat.IndustrialCommunication](https://github.com/wombatwebcg/Wombat.IndustrialCommunication)

如有问题、建议或反馈，请通过 GitHub Issues 提交。

# ConnectionPool 第三方接入指南

## 1. 文档目标

本文档面向第三方调用方，介绍 `Wombat.IndustrialCommunication.ConnectionPool` 的实际使用方式，重点说明：

- 如何创建客户端池和服务端池
- 如何注册连接并执行读写
- 如何使用点位批量读写接口
- 如何查看连接状态、订阅事件、执行维护和回收
- 如何理解当前支持的协议、参数和行为边界

本文档基于当前代码实现整理，只保留外部接入所需信息。

## 2. 核心概念

`ConnectionPool` 的设计目标，是把“连接创建、复用、租约控制、执行串行化、状态快照、事件通知、异常恢复、后台维护”收口到统一入口中。

### 2.1 资源角色

- `Client`：设备客户端连接，用于主动连接 PLC / 工业设备并执行读写
- `Server`：设备服务端连接，用于在本机启动工业协议服务端

### 2.2 两个主要入口

- 客户端池：`DeviceClientPool`，接口为 `IDeviceClientPool`
- 服务端池：`DeviceServerPool`，接口为 `IDeviceServerPool`

### 2.3 连接唯一标识

每个连接通过 `ConnectionIdentity` 唯一标识：

- `DeviceId`：业务设备 ID
- `ProtocolType`：协议类型字符串，例如 `ModbusTcp`、`SiemensS7`
- `Endpoint`：端点，例如 `192.168.1.10:502` 或 `COM3`

连接池内部使用这三个字段做等价判断，因此第三方应保证同一池内唯一。

## 3. 支持的协议范围

### 3.1 客户端池支持

- `ModbusTcp`
- `ModbusRtu`
- `SiemensS7`
- `Fins`

### 3.2 服务端池支持

- `ModbusTcp`
- `ModbusRtu`
- `SiemensS7`

说明：

- 当前没有 `Fins` 服务端池包装器
- 默认工厂按 `ResourceDescriptor.DeviceConnectionType` 路由协议类型
- 建议同时显式设置 `ResourceDescriptor.ResourceRole`，避免同协议下客户端和服务端混用

## 4. 公开能力总览

### 4.1 所有资源池共有能力

客户端池和服务端池都继承统一接口：

```csharp
IResourcePool<TResource> :
    IResourcePoolQuery,
    IResourcePoolExecution<TResource>,
    IResourcePoolControl,
    IResourcePoolEvents,
    IDisposable
```

因此都具备以下能力：

- `Register`：注册连接描述
- `Acquire / AcquireAsync`：手动获取连接租约
- `Release`：释放租约
- `ExecuteAsync`：在池内执行委托
- `Invalidate`：标记连接失效
- `ForceCloseAsync`：强制关闭连接并中断活跃执行
- `Unregister`：注销连接条目
- `ForceReconnect`：强制重连
- `CleanupIdle`：清理空闲连接
- `CleanupExpiredLeases`：清理过期租约
- `GetState / GetStates / GetEntrySnapshots / GetPoolSnapshot`：获取状态快照
- 事件订阅：`PoolEventOccurred`、`ConnectionStateChanged`、`LeaseChanged`、`MaintenanceCompleted`

### 4.2 客户端池特有能力

`IDeviceClientPool` 额外提供：

- `ReadPointsAsync`
- `WritePointsAsync`

适用于点位列表读写、批量点位访问等场景。

### 4.3 服务端池特有能力

`IDeviceServerPool` 额外提供：

- `Start / StartAsync`
- `Stop / StopAsync`

适用于将协议服务端的启动与停止纳入连接池统一管理。

## 5. 基本使用流程

第三方接入时，通常遵循以下步骤：

1. 创建 `ConnectionPoolOptions`
2. 创建对应工厂
3. 创建客户端池或服务端池
4. 构造 `ConnectionIdentity`
5. 构造 `ResourceDescriptor`
6. 填充协议参数对象 `ConnectionParameters`
7. 调用 `Register`
8. 通过 `ExecuteAsync`、`ReadPointsAsync`、`WritePointsAsync` 或 `StartAsync` 使用连接
9. 按需查询状态、订阅事件、执行维护
10. 最终 `Dispose`

## 6. 客户端池快速开始

### 6.1 创建池

```csharp
using System;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

var options = new ConnectionPoolOptions
{
    MaxConnections = 256,
    LeaseTimeout = TimeSpan.FromSeconds(10),
    IdleTimeout = TimeSpan.FromMinutes(5),
    EnableBackgroundMaintenance = true,
    MaxRetryCount = 1,
    RetryBackoff = TimeSpan.FromMilliseconds(200)
};

var factory = new DefaultPooledDeviceClientConnectionFactory();
IDeviceClientPool clientPool = new DeviceClientPool(options, factory);
```

### 6.2 注册一个 ModbusTcp 客户端连接

```csharp
var identity = new ConnectionIdentity
{
    DeviceId = "plc-001",
    ProtocolType = "ModbusTcp",
    Endpoint = "192.168.1.10:502"
};

var descriptor = new ResourceDescriptor
{
    Identity = identity,
    ResourceRole = ResourceRole.Client,
    DeviceConnectionType = DeviceConnectionType.ModbusTcp,
    ConnectionParameters = new ModbusTcpClientConnectionParameters
    {
        Ip = "192.168.1.10",
        Port = 502,
        ConnectTimeoutMilliseconds = 3000,
        ReceiveTimeoutMilliseconds = 3000,
        SendTimeoutMilliseconds = 3000,
        Retries = 1,
        BatchReadStationIntervalMilliseconds = 100
    }
};

var registerResult = clientPool.Register(descriptor);
if (!registerResult.IsSuccess)
{
    throw new Exception(registerResult.Message);
}
```

### 6.3 使用 `ExecuteAsync` 执行单次读写

当你已经知道底层客户端提供的接口时，最通用的用法是 `ExecuteAsync`。

```csharp
using Wombat.Extensions.DataTypeExtensions;

var readResult = await clientPool.ExecuteAsync<object>(
    identity,
    async client => await client.ReadAsync(DataTypeEnums.UInt16, "1;40001"));

if (readResult.IsSuccess)
{
    var value = readResult.ResultValue;
}
```

写入示例：

```csharp
var writeResult = await clientPool.ExecuteAsync(
    identity,
    async client => await client.WriteAsync(DataTypeEnums.UInt16, "1;40001", (ushort)123));
```

### 6.4 使用点位列表读写

如果希望按统一模型做批量点位读写，建议使用 `ReadPointsAsync` / `WritePointsAsync`。

```csharp
using System.Collections.Generic;
using Wombat.Extensions.DataTypeExtensions;

var writes = new List<DevicePointWriteRequest>
{
    new DevicePointWriteRequest
    {
        Name = "temperature",
        Address = "1;40961",
        DataType = DataTypeEnums.Int16,
        Length = 1,
        EnableBatch = true,
        Value = (short)25
    },
    new DevicePointWriteRequest
    {
        Name = "pressure",
        Address = "1;40962",
        DataType = DataTypeEnums.UInt16,
        Length = 1,
        EnableBatch = true,
        Value = (ushort)100
    }
};

var writePointsResult = await clientPool.WritePointsAsync(identity, writes);

var reads = new List<DevicePointReadRequest>
{
    new DevicePointReadRequest
    {
        Name = "temperature",
        Address = "1;40961",
        DataType = DataTypeEnums.Int16,
        Length = 1,
        EnableBatch = true
    },
    new DevicePointReadRequest
    {
        Name = "pressure",
        Address = "1;40962",
        DataType = DataTypeEnums.UInt16,
        Length = 1,
        EnableBatch = true
    }
};

var readPointsResult = await clientPool.ReadPointsAsync(identity, reads);
```

返回结果说明：

- 总结果是 `OperationResult<IList<DevicePointReadResult>>` 或 `OperationResult<IList<DevicePointWriteResult>>`
- 列表中的每一项都有自己的 `IsSuccess`、`Message`、`Value`
- 即使总调用成功，也建议逐项检查每个点位结果

### 6.5 何时使用 `ExecuteAsync`，何时使用点位接口

建议如下：

- 需要直接调用底层协议对象原生方法时，用 `ExecuteAsync`
- 需要传输标准化点位请求列表时，用 `ReadPointsAsync` / `WritePointsAsync`
- 需要让连接池统一处理点位批量优化与逐点回退时，优先使用点位接口

## 7. 服务端池快速开始

### 7.1 创建池

```csharp
using System;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

var options = new ConnectionPoolOptions
{
    EnableBackgroundMaintenance = false,
    MaxRetryCount = 2,
    RetryBackoff = TimeSpan.FromMilliseconds(200)
};

var factory = new DefaultPooledDeviceServerConnectionFactory();
IDeviceServerPool serverPool = new DeviceServerPool(options, factory);
```

### 7.2 注册并启动一个 ModbusTcp 服务端

```csharp
var identity = new ConnectionIdentity
{
    DeviceId = "modbus-server-001",
    ProtocolType = "ModbusTcp",
    Endpoint = "0.0.0.0:502"
};

var descriptor = new ResourceDescriptor
{
    Identity = identity,
    ResourceRole = ResourceRole.Server,
    DeviceConnectionType = DeviceConnectionType.ModbusTcp,
    ConnectionParameters = new ModbusTcpServerConnectionParameters
    {
        Ip = "0.0.0.0",
        Port = 502,
        ConnectTimeoutMilliseconds = 3000,
        ReceiveTimeoutMilliseconds = 3000,
        SendTimeoutMilliseconds = 3000,
        MaxConnections = 20
    }
};

var registerResult = serverPool.Register(descriptor);
if (!registerResult.IsSuccess)
{
    throw new Exception(registerResult.Message);
}

var startResult = await serverPool.StartAsync(identity);
if (!startResult.IsSuccess)
{
    throw new Exception(startResult.Message);
}
```

停止示例：

```csharp
var stopResult = await serverPool.StopAsync(identity, "业务侧主动停止");
```

说明：

- `StartAsync` 内置重试和退避逻辑
- 当底层监听失败符合端口冲突特征时，会返回更明确的端口占用提示

## 8. `ResourceDescriptor.ConnectionParameters` 参数说明

### 8.1 统一入口

`ResourceDescriptor` 通过 `ConnectionParameters` 挂接强类型参数对象。

旧的 `Parameters` 字典兼容层已经移除，第三方调用方需要直接构造对应协议的参数类。

示意：

```csharp
var descriptor = new ResourceDescriptor
{
    Identity = identity,
    ResourceRole = ResourceRole.Client,
    DeviceConnectionType = DeviceConnectionType.ModbusTcp,
    ConnectionParameters = new ModbusTcpClientConnectionParameters
    {
        Ip = "192.168.1.10",
        Port = 502
    }
};
```

这意味着：

- 外部调用方不再手写字符串键名
- 参数值类型在编译期可见
- 不同协议的参数边界由不同参数类表达
- 继续使用旧 `descriptor.Parameters["..."]` 写法会直接编译失败

### 8.2 客户端参数类

`ModbusTcpClientConnectionParameters`：

- `Ip`
- `Port`
- `ConnectTimeoutMilliseconds`
- `ReceiveTimeoutMilliseconds`
- `SendTimeoutMilliseconds`
- `Retries`
- `ProbeAddress`
- `ProbeDataType`
- `ProbeLength`
- `BatchReadStationIntervalMilliseconds`

`ModbusRtuClientConnectionParameters`：

- `PortName`
- `BaudRate`
- `DataBits`
- `StopBits`
- `Parity`
- `Handshake`
- `ConnectTimeoutMilliseconds`
- `ReceiveTimeoutMilliseconds`
- `SendTimeoutMilliseconds`
- `Retries`
- `ProbeAddress`
- `ProbeDataType`
- `ProbeLength`
- `BatchReadStationIntervalMilliseconds`

`SiemensS7ClientConnectionParameters`：

- `Ip`
- `Port`
- `SiemensVersion`
- `Slot`
- `Rack`
- `ConnectTimeoutMilliseconds`
- `ReceiveTimeoutMilliseconds`
- `SendTimeoutMilliseconds`
- `Retries`
- `ProbeAddress`
- `ProbeDataType`
- `ProbeLength`
- `BatchReadStationIntervalMilliseconds`

`FinsClientConnectionParameters`：

- `Ip`
- `Port`
- `TimeoutMilliseconds`
- `ConnectTimeoutMilliseconds`
- `ReceiveTimeoutMilliseconds`
- `SendTimeoutMilliseconds`
- `Retries`
- `ProbeAddress`
- `ProbeDataType`
- `ProbeLength`

### 8.3 服务端参数类

`ModbusTcpServerConnectionParameters`：

- `Ip`
- `Port`
- `ConnectTimeoutMilliseconds`
- `ReceiveTimeoutMilliseconds`
- `SendTimeoutMilliseconds`
- `MaxConnections`
- `EnableSnapshotPersistence`

`ModbusRtuServerConnectionParameters`：

- `PortName`
- `BaudRate`
- `DataBits`
- `StopBits`
- `Parity`
- `Handshake`
- `ConnectTimeoutMilliseconds`
- `ReceiveTimeoutMilliseconds`
- `SendTimeoutMilliseconds`
- `EnableSnapshotPersistence`

`SiemensS7ServerConnectionParameters`：

- `Ip`
- `Port`
- `ConnectTimeoutMilliseconds`
- `ReceiveTimeoutMilliseconds`
- `SendTimeoutMilliseconds`
- `MaxConnections`
- `EnableSnapshotPersistence`

### 8.4 参数使用要求与建议

- `DeviceConnectionType` 与 `ConnectionParameters` 类型必须一致，例如 `ModbusTcp` 搭配 `ModbusTcpClientConnectionParameters`
- 对 TCP 类协议，`Ip` 一般必填，`Port` 未设置时会使用工厂默认端口
- 对 RTU 协议，`PortName` 必填
- 所有毫秒参数都应传非负整数
- `ProbeLength` 若设置，必须大于 `0`
- `BatchReadStationIntervalMilliseconds` 仅对 `ModbusTcp`、`ModbusRtu`、`SiemensS7` 客户端有意义
- `FinsClientConnectionParameters.TimeoutMilliseconds` 仅影响 `FinsClient` 构造时的可选超时

## 9. 连接执行与重试行为

### 9.1 默认规则

`ExecuteAsync` 可接收 `ConnectionExecutionOptions`。

当前默认规则如下：

- `Diagnostic`：默认不重试
- `Read`：默认允许重试
- `Write`：默认不重试

### 9.2 显式指定读策略

```csharp
var readOptions = ConnectionExecutionOptions.CreateRead();
var result = await clientPool.ExecuteAsync<object>(
    identity,
    async client => await client.ReadAsync(DataTypeEnums.UInt16, "1;40001"),
    readOptions);
```

### 9.3 覆盖写入重试策略

```csharp
var writeOptions = ConnectionExecutionOptions.CreateWrite();
writeOptions.EnableRetry = true;
writeOptions.MaxRetryCount = 1;
writeOptions.RetryBackoff = TimeSpan.FromMilliseconds(100);

var result = await clientPool.ExecuteAsync(
    identity,
    async client => await client.WriteAsync(DataTypeEnums.UInt16, "1;40001", (ushort)123),
    writeOptions);
```

## 10. 手动租约模式

除了 `ExecuteAsync` 之外，也可以手动获取和释放租约：

```csharp
var leaseResult = await clientPool.AcquireAsync(identity);
if (!leaseResult.IsSuccess)
{
    throw new Exception(leaseResult.Message);
}

try
{
    // 这里通常只在你确实需要显式控制租约生命周期时使用
}
finally
{
    clientPool.Release(leaseResult.ResultValue);
}
```

建议：

- 一般业务场景优先使用 `ExecuteAsync`
- 只有在确实需要跨多个步骤复用同一租约时，才使用手动租约模式
- 不要忘记 `Release`

## 11. 状态查询与快照

### 11.1 对外稳定状态

对外公开的稳定状态是 `ConnectionEntryState`：

- `Disconnected`：未建立可用连接，或已释放
- `Ready`：连接可用且空闲
- `Busy`：连接正在执行
- `Unavailable`：连接不可用，需要人工干预或等待恢复

### 11.2 内部生命周期状态

如果需要更细粒度观察，可查看 `ConnectionEntryLifecycleState`：

- `Uninitialized`
- `Connecting`
- `Ready`
- `Leased`
- `Reconnecting`
- `Faulted`
- `Invalidated`
- `ForceClosing`
- `Disposed`

### 11.3 查询示例

```csharp
var stateResult = clientPool.GetState(identity);
var poolSnapshotResult = clientPool.GetPoolSnapshot();

if (stateResult.IsSuccess)
{
    var snapshot = stateResult.ResultValue;
    var state = snapshot.State;
    var lifecycle = snapshot.LifecycleState;
    var activeLeaseCount = snapshot.ActiveLeaseCount;
}

if (poolSnapshotResult.IsSuccess)
{
    var poolSnapshot = poolSnapshotResult.ResultValue;
    var total = poolSnapshot.TotalEntries;
    var ready = poolSnapshot.ReadyEntries;
    var busy = poolSnapshot.BusyEntries;
    var forceClosing = poolSnapshot.ForceClosingEntries;
}
```

## 12. 事件订阅

连接池提供四类事件：

- `PoolEventOccurred`
- `ConnectionStateChanged`
- `LeaseChanged`
- `MaintenanceCompleted`

示例：

```csharp
clientPool.PoolEventOccurred += (sender, args) =>
{
    Console.WriteLine(
        $"[{args.OccurredAtUtc:O}] {args.ResourceRole} {args.EventType} {args.Identity} {args.Message}");
};

clientPool.ConnectionStateChanged += (sender, args) =>
{
    Console.WriteLine(
        $"StateChanged: {args.Identity} {args.PreviousState} -> {args.CurrentState}, " +
        $"{args.PreviousLifecycleState} -> {args.CurrentLifecycleState}");
};
```

常见事件类型包括：

- `Registered`
- `ConnectStarting`
- `ConnectSucceeded`
- `ConnectFailed`
- `LeaseAcquired`
- `LeaseReleased`
- `ExecuteFailed`
- `Retrying`
- `Reconnecting`
- `Recovered`
- `Invalidated`
- `IdleCleaned`
- `LeaseExpired`
- `Disposed`
- `BackgroundMaintenanceCompleted`
- `ForceReconnectRequested`
- `Unregistered`
- `ForceCloseRequested`
- `ForceCloseCancelling`
- `ForceClosed`

建议：

- 第三方监控系统可按 `ResourceRole + EventType + Identity` 维度聚合
- 对异常排查最有价值的是 `ConnectFailed`、`ExecuteFailed`、`Retrying`、`ForceClosed`

## 13. 常用控制操作

### 13.1 失效连接

```csharp
var result = clientPool.Invalidate(identity, "探活失败，人工标记失效");
```

适用于：

- 明确知道当前连接不可再用
- 希望后续阻止其继续作为正常可用连接参与执行

### 13.2 强制关闭连接

```csharp
var result = await clientPool.ForceCloseAsync(identity, "业务要求立即中断当前连接");
```

适用于：

- 正在执行中的连接需要立即打断
- 需要在活跃执行期间强制终止读写
- 需要快速清理租约并把条目置为不可用

行为特点：

- 会阻止新的租约进入
- 会尝试打断活跃执行
- 重复调用是幂等的
- 强制关闭后，条目通常进入 `Invalidated` / `Unavailable` 观察状态

### 13.3 强制重连

```csharp
var result = clientPool.ForceReconnect(identity, "网络恢复后主动重连");
```

适用于：

- 连接已故障但业务希望立即触发重建
- 不想等待后台维护自然恢复

注意：

- 若存在活跃租约，可能会被拒绝

### 13.4 注销连接

```csharp
var result = clientPool.Unregister(identity, "设备下线，移除连接条目");
```

注意：

- `Unregister` 需要连接条目可以安全移除
- 若存在活跃租约，通常会失败
- 如果你的目标是打断正在运行的连接，不要直接依赖 `Unregister`，优先使用 `ForceCloseAsync`

## 14. 维护与回收

### 14.1 后台维护

`ConnectionPoolOptions.EnableBackgroundMaintenance = true` 时，连接池会启动后台维护循环。

常见维护内容包括：

- 健康检查
- 过期租约扫描
- 故障恢复
- 空闲连接回收

### 14.2 手动维护

```csharp
var idleCleanup = clientPool.CleanupIdle();
var expiredCleanup = clientPool.CleanupExpiredLeases();
```

适用于：

- 后台维护关闭时
- 单元测试或集成测试中希望显式触发
- 运维侧希望在固定时机执行手工清理

## 15. 第三方接入建议

### 15.1 推荐做法

- 每种业务设备维持稳定的 `ConnectionIdentity`
- 优先复用同一个池实例，而不是每次操作都新建池
- 普通业务调用优先使用 `ExecuteAsync`
- 点位列表场景优先使用 `ReadPointsAsync` / `WritePointsAsync`
- 使用 `GetPoolSnapshot` 和事件做监控
- 在应用退出时调用 `Dispose`

### 15.2 不推荐做法

- 同一个连接反复 `Register` / `Unregister`
- 忘记释放手动获取的租约
- 客户端连接注册到 `DeviceServerPool`
- 服务端连接注册到 `DeviceClientPool`
- 省略 `ResourceRole` 和 `DeviceConnectionType`
- 继续沿用已删除的 `Parameters` 字典写法

## 16. 常见问题

### 16.1 为什么 `Register` 成功，但第一次执行才真正连上设备？

这是当前设计的正常行为。`Register` 只是将条目加入连接池；真正的建连通常发生在首次 `AcquireAsync`、`ExecuteAsync`、`ReadPointsAsync`、`WritePointsAsync` 或服务端 `StartAsync` 时。

### 16.2 为什么同一设备的两个操作不会完全并行？

连接池对同一 `ConnectionIdentity` 的执行做了串行化保护，这样可以避免同一底层连接被并发访问导致状态错乱。不同设备之间可以并行。

### 16.3 点位批量读取一定会走底层批量接口吗？

不一定。`EnableBatch = true` 表示优先尝试批量读取，但仅对兼容的标量点位有效。字符串、数组或不兼容的点位会自动回退到逐点读取。

### 16.4 强制关闭和失效有什么区别？

- `Invalidate` 更偏向标记失效
- `ForceCloseAsync` 更偏向中断当前连接和活跃执行

如果场景是“马上打断正在执行的连接”，优先选择 `ForceCloseAsync`。

### 16.5 旧版 `Parameters` 字典还能继续用吗？

不能。

当前版本已经移除 `ResourceDescriptor.Parameters` 入口，第三方代码需要改为：

- 设置 `ResourceDescriptor.DeviceConnectionType`
- 设置 `ResourceDescriptor.ResourceRole`
- 设置 `ResourceDescriptor.ConnectionParameters`

例如 `ModbusTcp` 客户端应直接传 `new ModbusTcpClientConnectionParameters { ... }`。

## 17. 一个完整的客户端示例

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.ConnectionPool.Core;
using Wombat.IndustrialCommunication.ConnectionPool.Factories;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

public static async Task DemoAsync()
{
    var options = new ConnectionPoolOptions
    {
        EnableBackgroundMaintenance = true,
        LeaseTimeout = TimeSpan.FromSeconds(10),
        IdleTimeout = TimeSpan.FromMinutes(5),
        MaxRetryCount = 1,
        RetryBackoff = TimeSpan.FromMilliseconds(100)
    };

    using (var pool = new DeviceClientPool(options, new DefaultPooledDeviceClientConnectionFactory()))
    {
        var identity = new ConnectionIdentity
        {
            DeviceId = "plc-001",
            ProtocolType = "ModbusTcp",
            Endpoint = "192.168.1.10:502"
        };

        var descriptor = new ResourceDescriptor
        {
            Identity = identity,
            ResourceRole = ResourceRole.Client,
            DeviceConnectionType = DeviceConnectionType.ModbusTcp,
            ConnectionParameters = new ModbusTcpClientConnectionParameters
            {
                Ip = "192.168.1.10",
                Port = 502,
                ConnectTimeoutMilliseconds = 3000,
                ReceiveTimeoutMilliseconds = 3000,
                SendTimeoutMilliseconds = 3000
            }
        };

        var register = pool.Register(descriptor);
        if (!register.IsSuccess)
        {
            throw new Exception(register.Message);
        }

        var singleRead = await pool.ExecuteAsync<object>(
            identity,
            async client => await client.ReadAsync(DataTypeEnums.UInt16, "1;40001"));

        var pointReads = await pool.ReadPointsAsync(
            identity,
            new List<DevicePointReadRequest>
            {
                new DevicePointReadRequest
                {
                    Name = "reg40001",
                    Address = "1;40001",
                    DataType = DataTypeEnums.UInt16,
                    Length = 1,
                    EnableBatch = true
                },
                new DevicePointReadRequest
                {
                    Name = "reg40002",
                    Address = "1;40002",
                    DataType = DataTypeEnums.UInt16,
                    Length = 1,
                    EnableBatch = true
                }
            });

        var snapshot = pool.GetPoolSnapshot();
    }
}
```

## 18. 总结

对第三方调用方来说，可以把 `ConnectionPool` 理解为一层“面向工业设备连接的统一运行时”：

- 客户端场景用 `DeviceClientPool`
- 服务端场景用 `DeviceServerPool`
- 连接通过 `ConnectionIdentity + ResourceDescriptor` 描述
- 常规执行优先用 `ExecuteAsync`
- 点位批量场景优先用 `ReadPointsAsync` / `WritePointsAsync`
- 状态通过快照查询，过程通过事件观察
- 异常连接通过 `Invalidate`、`ForceCloseAsync`、`ForceReconnect`、`Unregister` 管理

如果你正在做第三方封装，建议先抽象出一层自己的“设备注册模型”和“读写任务模型”，再将其映射到本库的 `ConnectionIdentity`、`ResourceDescriptor`、`ConnectionParameters` 和点位请求模型上。

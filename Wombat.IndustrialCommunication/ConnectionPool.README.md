# ConnectionPool 使用说明

## 目标
`ConnectionPool` 用于统一管理工业设备长连接，解决以下几类典型问题：

- 同一设备连接的复用，避免重复创建客户端。
- 多设备并发访问时的统一入口管理。
- 单连接的租约控制、状态切换、故障恢复和资源回收。
- 上层业务按“设备标识 + 操作委托”执行读写，而不是直接关心连接创建与释放。
- 为 Modbus TCP、Modbus RTU、Siemens S7、FINS 等协议提供一致的池化访问方式。
- 暴露事件和快照，方便监控、诊断和运维。

`ConnectionPool` 不是简单的“字典 + client 缓存”。它内部把“池级管理”和“单连接生命周期维护”拆开处理：

- `DeviceConnectionPool` 负责多连接注册表、统一入口和后台维护调度。
- `PooledConnectionEntry` 负责单设备连接的状态机、租约、故障记录和对象维护。
- `PooledOperationExecutor` 负责执行重试、可恢复故障识别和自动重连。
- `DefaultPooledDeviceConnectionFactory` 负责把 `DeviceConnectionDescriptor` 转换为协议对应的池化连接包装对象。

## 适用场景
- 一个采集服务持续访问大量 PLC / 仪表 / 网关。
- 同一设备会被周期采集、写入命令、状态查询等多个业务逻辑重复访问。
- 需要将连接建立、异常恢复、超时重试统一沉淀到基础层。
- 需要查询当前连接池整体健康状态，而不是只拿到一次读写结果。

## 整体结构
### 核心对象
- `DeviceConnectionPool`：连接池主入口，对外提供 `注册 / 获取租约 / 释放 / 执行 / 查询 / 维护 / 事件`。
- `PooledConnectionEntry`：单个设备条目，内部持有一个 `IPooledDeviceConnection` 和若干 `ConnectionLease`。
- `IPooledDeviceConnection`：协议包装后的统一抽象，屏蔽 Modbus、S7、FINS 等差异。
- `ConnectionIdentity`：连接唯一标识，由 `DeviceId + ProtocolType + Endpoint` 组成，忽略大小写比较。
- `DeviceConnectionDescriptor`：连接描述，包含 `Identity`、连接类型、连接参数。
- `ConnectionLease`：连接租约，表示上层当前占用了一个连接访问窗口。
- `ConnectionEntrySnapshot / ConnectionPoolSnapshot`：用于观测单连接和整个连接池的实时状态。

### 接口分层
如果上层不希望直接依赖完整聚合接口，可以按职责依赖更小接口：

- `IDeviceConnectionPoolQuery`：查询配置、状态和快照。
- `IDeviceConnectionPoolControl`：注册、租约、失效、注销、回收、强制重连。
- `IDeviceConnectionPoolExecution`：执行委托和点位列表读写。
- `IDeviceConnectionPoolEvents`：订阅生命周期事件。
- `IDeviceConnectionPool`：兼容聚合入口，继承上述四类能力并实现 `IDisposable`。

## 生命周期概览
### 典型调用链
最常见的业务路径如下：

1. 构造 `ConnectionPoolOptions`。
2. 构造工厂 `IPooledDeviceConnectionFactory`，通常使用 `DefaultPooledDeviceConnectionFactory`。
3. 创建 `DeviceConnectionPool`。
4. 为每台设备准备 `DeviceConnectionDescriptor` 并调用 `Register()` 注册。
5. 上层通过 `ExecuteAsync()`、`ReadPointsAsync()`、`WritePointsAsync()` 或 `Acquire()/Release()` 使用连接。
6. 连接池根据空闲时间、租约过期、健康检查结果自动做清理和恢复。
7. 应用退出时调用 `Dispose()` 释放所有条目。

### 单连接状态机
`PooledConnectionEntry` 维护两套视图：

- 对外公开稳定状态（业务默认消费）：`Disconnected`、`Ready`、`Busy`、`Unavailable`。
- 对内生命周期状态（用于诊断与事件扩展）：`Uninitialized`、`Connecting`、`Ready`、`Leased`、`Reconnecting`、`Faulted`、`Invalidated`、`Disposed`。

公开状态映射规则：

- `Ready` -> `Ready`
- `Leased` -> `Busy`
- `Faulted` / `Invalidated` -> `Unavailable`
- `Uninitialized` / `Connecting` / `Reconnecting` / `Disposed` -> `Disconnected`

常见状态流转：

- `Register` 后初始状态为 `Uninitialized`。
- 首次 `Acquire` 或 `ExecuteAsync` 时触发 `Connecting -> Ready/Leased`。
- 执行失败时可能进入 `Faulted`。
- 可恢复失败会进入 `Reconnecting`，成功后回到 `Ready/Leased`。
- 调用 `Invalidate()` 后进入 `Invalidated`。
- `CleanupIdle()`、`Unregister()` 或 `Dispose()` 最终会进入 `Disposed`。

## 快速开始
```csharp
var options = new ConnectionPoolOptions
{
    MaxConnections = 128,
    IdleTimeout = TimeSpan.FromMinutes(2),
    LeaseTimeout = TimeSpan.FromSeconds(30),
    MaxRetryCount = 2,
    RetryBackoff = TimeSpan.FromMilliseconds(200),
    EnableBackgroundMaintenance = true,
    HealthCheckInterval = TimeSpan.FromSeconds(30),
    LeaseExpirationSweepInterval = TimeSpan.FromSeconds(5)
};

var factory = new DefaultPooledDeviceConnectionFactory();
var pool = new DeviceConnectionPool(options, factory);

var descriptor = new DeviceConnectionDescriptor
{
    Identity = new ConnectionIdentity
    {
        DeviceId = "modbus-1",
        ProtocolType = "ModbusTcp",
        Endpoint = "192.168.1.10:502"
    },
    DeviceConnectionType = DeviceConnectionType.ModbusTcp
};

descriptor.Parameters["ip"] = "192.168.1.10";
descriptor.Parameters["port"] = 502;
descriptor.Parameters["connectTimeoutMilliseconds"] = 3000;
descriptor.Parameters["receiveTimeoutMilliseconds"] = 3000;
descriptor.Parameters["sendTimeoutMilliseconds"] = 3000;

var registerResult = pool.Register(descriptor);
if (!registerResult.IsSuccess)
{
    Console.WriteLine(registerResult.Message);
    return;
}

var readResult = await pool.ExecuteAsync(
    descriptor.Identity,
    async client => client.ReadUInt16Async("1;3;0"));

if (readResult.IsSuccess)
{
    Console.WriteLine(readResult.ResultValue);
}
else
{
    Console.WriteLine(readResult.Message);
}
```

## 身份、描述与工厂参数
### ConnectionIdentity
连接池以 `ConnectionIdentity` 作为唯一键。建议遵循下面的约定：

- `DeviceId`：业务设备编号，最好在业务域内稳定且唯一。
- `ProtocolType`：协议名称，建议和 `DeviceConnectionType` 语义保持一致，例如 `ModbusTcp`、`ModbusRtu`、`SiemensS7`、`Fins`。
- `Endpoint`：终端地址标识，例如 `192.168.1.10:502`、`COM3`。

`ConnectionIdentity` 的相等比较忽略大小写，因此以下身份会被视为同一条目：

- `ModbusTcp:PLC-01@192.168.1.10:502`
- `modbustcp:plc-01@192.168.1.10:502`

### DeviceConnectionDescriptor
连接描述负责告诉工厂如何创建底层连接，关键字段如下：

- `Identity`：连接池唯一键，不能为空。
- `DeviceConnectionType`：标准化连接类型，建议优先设置。
- `ConnectionType`：字符串形式类型名；当 `DeviceConnectionType` 为 `Unknown` 时用于工厂解析。
- `Parameters`：协议相关参数字典，键名大小写不敏感。

### DefaultPooledDeviceConnectionFactory 支持的协议
默认工厂当前支持：

- `ModbusTcp`
- `ModbusRtu`
- `SiemensS7`
- `Fins`

### 协议参数说明
#### ModbusTcp
- 必填：`ip`
- 可选：`port`，默认 `502`

#### ModbusRtu
- 必填：`portName`
- 可选：`baudRate`，默认 `9600`
- 可选：`dataBits`，默认 `8`
- 可选：`stopBits`，默认 `StopBits.One`
- 可选：`parity`，默认 `Parity.None`
- 可选：`handshake`，默认 `Handshake.None`

#### SiemensS7
- 必填：`ip`
- 可选：`port`，默认 `102`
- 可选：`siemensVersion`，默认 `SiemensVersion.S7_1200`
- 可选：`slot`，默认 `0`
- 可选：`rack`，默认 `0`

#### Fins
- 必填：`ip`
- 可选：`port`，默认 `9600`
- 可选：`timeoutMilliseconds`

### 通用客户端参数
以下参数由默认工厂统一写入底层客户端配置：

- `connectTimeoutMilliseconds`：连接超时。
- `receiveTimeoutMilliseconds`：接收超时。
- `sendTimeoutMilliseconds`：发送超时。
- `retries`：底层客户端自己的重试次数。

## 连接池配置项
`ConnectionPoolOptions` 是池级行为的核心开关：

- `MaxConnections`：最大条目数。超过后 `Register()` 会失败。
- `IdleTimeout`：连接在“无租约”状态下允许空闲多久，超时后可被 `CleanupIdle()` 清理。
- `LeaseTimeout`：每个租约的默认过期时间。
- `HealthCheckInterval`：后台健康检查周期。
- `ProbeTimeout`：协议级探活超时时间。
- `EnableBackgroundMaintenance`：是否启用内部后台维护循环。
- `HealthCheckLeaseFreeOnly`：是否仅检查无活跃租约的条目，默认 `true`。
- `LeaseExpirationSweepInterval`：过期租约扫描周期。
- `FaultedReconnectCooldown`：故障恢复冷却时间，用于限制故障态的连续重连风暴。
- `MaxConcurrentMaintenanceOperations`：后台维护并发上限。
- `IsolateEventSubscriberExceptions`：是否隔离事件订阅者异常，避免影响主流程。
- `MaxConsecutiveHealthCheckFailures`：健康检查连续失败多少次后自动失效。
- `MaxRetryCount`：池级执行时的最大恢复重试次数。
- `RetryBackoff`：每次池级重试之间的退避时间。

## 执行策略模型
连接池执行入口支持显式策略 `ConnectionExecutionOptions`，用于区分读、写、诊断三类行为：

- `Read`：默认启用恢复性重试。
- `Write`：默认不启用自动重试。
- `Diagnostic`：默认不启用自动重试。

调用方可通过 `EnableRetry`、`MaxRetryCount`、`RetryBackoff` 显式覆盖默认策略。

建议：

- 采集频率高、设备数量多时，适当增大 `MaxConnections`。
- 短周期任务较多时，`IdleTimeout` 不要过短，否则会频繁建连。
- 写入动作对实时性要求高时，适当减小 `MaxRetryCount`，避免长时间阻塞。
- 如果现场网络波动较大，保留后台维护并适当缩短 `HealthCheckInterval`。

## 常用用法
### 方式一：推荐使用 ExecuteAsync
大多数业务场景建议直接使用 `ExecuteAsync`，它会自动完成：

- 获取租约。
- 确保连接已建立。
- 执行读写委托。
- 识别可恢复故障并按执行策略决定是否重试/重连。
- 在执行结束后释放租约。

```csharp
var result = await pool.ExecuteAsync(
    identity,
    async client => client.WriteAsync(DataTypeEnums.UInt16, "1;3;20", (ushort)1500));
```

如果业务只关心“执行一次动作”，这是最稳妥也最不容易出错的入口。

补充说明（与当前实现一致）：

- `ExecuteAsync(...)` 默认使用 `ConnectionExecutionOptions.CreateDiagnostic()`，`Diagnostic` 默认不启用恢复性重试。
- `ReadPointsAsync(...)` 默认使用 `CreateRead()`，读操作默认启用恢复性重试。
- `WritePointsAsync(...)` 默认使用 `CreateWrite()`，写操作默认不启用恢复性重试。
- 若希望 `ExecuteAsync(...)` 启用重试，请显式传入 `ConnectionExecutionOptions` 并设置 `EnableRetry = true`。

### 默认简化入口
普通调用场景可直接依赖 `ISimpleDeviceConnectionPool`，仅使用注册、执行与快照查询，不必显式处理高级维护控制接口：

```csharp
ISimpleDeviceConnectionPool simplePool = new DeviceConnectionPool(options, factory);
var registerResult = simplePool.Register(descriptor);
var readResult = await simplePool.ReadPointsAsync(identity, points);
var snapshotResult = simplePool.GetPoolSnapshot();
```

### 方式二：手动 Acquire / Release
当你需要显式保留一个“连接占用标记”、控制某条目的租约数量，或者与外部调度逻辑协同时，可以手动租用：

```csharp
var leaseResult = await pool.AcquireAsync(identity);
if (!leaseResult.IsSuccess)
{
    Console.WriteLine(leaseResult.Message);
    return;
}

try
{
    var snapshot = pool.GetState(identity);
    if (snapshot.IsSuccess)
    {
        Console.WriteLine("当前活跃租约数: " + snapshot.ResultValue.ActiveLeaseCount);
        Console.WriteLine("当前状态: " + snapshot.ResultValue.State);
    }
}
finally
{
    pool.Release(leaseResult.ResultValue);
}
```

注意：

- `Acquire()` 返回的是 `ConnectionLease`，不是底层 `IDeviceClient`。
- 如果只是想执行读写，优先使用 `ExecuteAsync()`、`ReadPointsAsync()` 或 `WritePointsAsync()`。
- 租约对象必须通过 `Release()` 归还。
- 同一个条目允许存在多个租约，但单条目内部执行是串行保护的。
- 若租约过期，后台或显式调用 `CleanupExpiredLeases()` 时会自动释放。

### 方式三：点位列表读写
当上层已经有点位配置集合时，可以直接交给连接池，不需要手动拼装一条条读写委托。

```csharp
var points = new List<DevicePointReadRequest>
{
    new DevicePointReadRequest
    {
        Name = "温度",
        Address = "1;3;0",
        DataType = DataTypeEnums.UInt16,
        EnableBatch = true
    },
    new DevicePointReadRequest
    {
        Name = "状态字",
        Address = "1;3;10",
        DataType = DataTypeEnums.UInt16,
        Length = 4,
        EnableBatch = false
    }
};

var pointReadResult = await pool.ReadPointsAsync(identity, points);
if (pointReadResult.IsSuccess)
{
    foreach (var item in pointReadResult.ResultValue)
    {
        Console.WriteLine(item.Name + ": " + item.Value);
    }
}
```

```csharp
var writePoints = new List<DevicePointWriteRequest>
{
    new DevicePointWriteRequest
    {
        Name = "启动命令",
        Address = "1;5;0",
        DataType = DataTypeEnums.Bool,
        EnableBatch = true,
        Value = true
    },
    new DevicePointWriteRequest
    {
        Name = "设定值",
        Address = "1;3;20",
        DataType = DataTypeEnums.UInt16,
        EnableBatch = true,
        Value = (ushort)1500
    }
};

var pointWriteResult = await pool.WritePointsAsync(identity, writePoints);
```

点位列表入口同样走连接池租约和执行链路，因此会继承相同的连接复用和重试恢复能力。

## 点位列表规则
### 归一化规则
- `points` 不能为空，列表项也不能为空。
- `Address` 不能为空。
- `DataType` 不能是 `DataTypeEnums.None`。
- `Name` 为空时自动回退为 `Address`。
- `Length <= 0` 时自动修正为 `1`。

### 读取规则
- 字符串读取走 `ReadStringAsync(address, length)`。
- 非字符串且 `Length > 1` 时走连续块读取 `ReadAsync(dataType, address, length)`。
- 标量点位走普通单点读取 `ReadAsync(dataType, address)`。

### 写入规则
- `Value` 不能为空。
- 字符串写入走字符串专用写入分支。
- 数组写入会转换为 `object[]` 后按数组写入。
- 其余标量值按单点写入。

### 批量优化规则
- `EnableBatch = true` 时，连接池会优先尝试客户端的 `BatchReadAsync/BatchWriteAsync`。
- 只有“标量、非字符串、非数组、非重复地址”的点位会参与批量。
- 同一个地址如果重复出现，会自动排除出批量请求，改为逐点执行。
- 如果批量调用失败，连接池会自动记录提示信息并回退到逐点读写。
- 即使某个点位失败，结果列表仍会逐项返回，便于上层做局部容错。

### 结果特征
每个 `DevicePointReadResult / DevicePointWriteResult` 都会保留：

- `Name`
- `Address`
- `DataType`
- `Length`
- `IsSuccess`
- `Message`
- `Value`

适合上层做逐点诊断、告警和局部重试。

## 对象维护与控制方法详解
本节重点说明连接池“对象维护”的一系列方法，包括什么时候调用、内部做什么、有哪些限制。

### Register
`Register(DeviceConnectionDescriptor descriptor)` 用于向池中注册一个连接条目。

行为：

- 校验 `descriptor` 和 `descriptor.Identity`。
- 校验是否已存在相同 `ConnectionIdentity`。
- 校验池容量是否超过 `MaxConnections`。
- 通过工厂创建对应的 `IPooledDeviceConnection`。
- 创建 `PooledConnectionEntry` 并写入 `_entries`。
- 发布 `Registered` 事件。

注意：

- `Register()` 只注册条目，不一定立即建立网络连接。
- 同一身份重复注册会失败。
- 并发重复注册同一身份时，只有一个调用会成功。

### Acquire / AcquireAsync
`Acquire()` / `AcquireAsync()` 用于获取一个连接租约。

行为：

- 根据 `ConnectionIdentity` 找到条目。
- 调用条目的 `AcquireAsync()`。
- 条目内部会先 `EnsureConnected()`，必要时建立连接。
- 成功后创建新的 `ConnectionLease`，记录获取时间和到期时间。
- 条目状态切为 `Leased`，并发布租约事件。

失败条件：

- 连接未注册。
- 连接池已释放。
- 条目已失效、已释放或正在移除。
- 建连失败。
- 调用方传入了已取消的 `CancellationToken`。

### Release
`Release(ConnectionLease lease)` 用于归还租约。

行为：

- 根据租约中的 `Identity` 找到条目。
- 条目内部移除租约记录。
- 根据当前剩余租约数切换状态为 `Ready` 或继续维持 `Leased`。
- 发布 `LeaseReleased` 事件。

补充说明：

- 如果连接池中该条目已经被移除，`Release()` 会返回成功，表示租约已被自动关闭。
- 如果租约不存在或已释放，则会返回失败。

### ExecuteAsync
`ExecuteAsync()` 是最常用的业务入口，内部流程大致如下：

1. 校验 `identity` 和委托。
2. 获取条目。
3. 自动执行 `AcquireAsync()`。
4. 调用 `PooledOperationExecutor.ExecuteAsync()`。
5. 最终自动 `Release()`。

`PooledOperationExecutor` 的职责：

- 每次执行前调用 `EnsureConnectedAsync()`。
- 当连接或执行失败时，判断是否属于可恢复故障。
- 可恢复时触发 `Retrying`、`Reconnecting`、`Recovered` 等生命周期动作。
- 到达 `MaxRetryCount` 上限后返回失败。

当前可恢复故障判定包括：

- `TimeoutException`
- `IOException`
- `ObjectDisposedException`
- 异常消息或结果消息中包含 `timeout`、`timed out`、`connection`、`socket`、`closed`

这意味着网络抖动、连接断开、socket 关闭等典型现场问题，有机会在池级被自动修复。

### Invalidate
`Invalidate(ConnectionIdentity identity, string reason)` 用于显式失效连接。

适用场景：

- 上层确认该设备连接参数已变化，需要阻止继续使用旧连接。
- 发现设备状态异常，需要暂停该条目后续访问。
- 外部诊断流程决定临时熔断该设备连接。

行为：

- 设置故障原因和故障时间。
- 调用底层 `Connection.Invalidate(reason)`。
- 条目状态进入 `Invalidated`。
- 后续新的租约请求会被拒绝。

注意：

- `Invalidate()` 不会把条目从池中删除。
- 如果想彻底移除，需要再调用 `Unregister()`。

### Unregister
`Unregister(ConnectionIdentity identity, string reason)` 用于注销并移除条目。

行为：

- 找到条目并标记为“准备移除”。
- 默认不允许存在活跃租约。
- 从池字典中删除条目。
- 调用条目 `DisposeAsync()` 断开底层连接并关闭租约。
- 发布 `Unregistered` 事件。

失败条件：

- 条目不存在。
- 存在活跃租约。
- 条目正在移除中。
- 从字典移除失败。

这是“彻底删除连接条目”的主入口。

### ForceReconnect
`ForceReconnect(ConnectionIdentity identity, string reason)` 用于主动触发强制重连。

适用场景：

- 上位机确认设备端已重启。
- 业务需要立即刷新连接状态。
- 故障已恢复，希望人工触发一次重连而不是等待后台维护。

行为：

- 如果存在活跃租约，则拒绝强制重连。
- 发布 `ForceReconnectRequested` 事件。
- 进入 `TryRecoverAsync()` 流程。
- 先断开原连接，再重新 `EnsureConnected()`。
- 成功后重置失败计数，记录恢复时间并发布恢复事件。

### CleanupIdle
`CleanupIdle()` 用于回收长时间空闲的连接条目。

判断条件：

- 条目当前没有活跃租约。
- 条目不在移除中。
- 条目状态是 `Ready`、`Faulted` 或 `Invalidated`。
- `DateTime.UtcNow - LastActiveTimeUtc >= IdleTimeout`

行为：

- 先尝试把条目标记为“准备移除”。
- 从池中移除。
- 调用条目 `DisposeAsync()` 断开底层连接。
- 发布 `IdleCleaned` 事件。
- 返回本次实际清理的条目数。

适合：

- 手动定时清理。
- 后台维护关闭时由外部调度器显式触发。
- 在连接量大、设备访问波动明显的场景下降低资源占用。

### CleanupExpiredLeases
`CleanupExpiredLeases()` 用于扫描并清理已超时的租约。

行为：

- 遍历所有条目。
- 调用条目 `ExpireLeasesAsync()`。
- 条目会移除所有过期租约并发布 `LeaseExpired` 事件。
- 若条目已无租约，会自动刷新状态。

适用场景：

- 某些业务逻辑可能忘记释放租约时，作为兜底修复。
- 后台维护关闭时由外部定时触发。

## 后台维护机制
当 `EnableBackgroundMaintenance = true` 时，连接池构造时会启动内部维护任务，周期性执行三类动作：

### 1. 过期租约扫描
由 `LeaseExpirationSweepInterval` 控制周期。

作用：

- 自动释放超时租约。
- 防止调用方异常退出或遗漏 `Release()` 导致条目长期占用。

### 2. 健康检查
由 `HealthCheckInterval` 控制周期。

行为：

- 默认只检查没有活跃租约的条目，避免打断正在执行的业务。
- 如果条目处于 `Faulted`，会带恢复意图地尝试重新建连。
- 连续失败达到 `MaxConsecutiveHealthCheckFailures` 后，会自动 `Invalidate()`。

### 3. 空闲清理
每轮维护都会执行 `CleanupIdle()`。

作用：

- 自动回收长期不用的连接。
- 控制内存、socket 和底层客户端对象占用。

### 维护事件
每轮后台维护结束后会发布 `BackgroundMaintenanceCompleted` 事件，并携带：

- `ScannedEntryCount`
- `AffectedEntryCount`
- `OccurredAtUtc`

其中：

- `ScannedEntryCount` 表示本轮扫描过的条目数量。
- `AffectedEntryCount` 表示过期租约释放、健康检查失败、空闲清理等产生影响的累计数量。

## PooledConnectionEntry 内部维护信息
如果你要理解“对象维护”的细节，`PooledConnectionEntry` 是最关键的对象。它内部维护了以下状态数据：

- 当前池级状态 `State`
- 活跃租约集合 `_leases`
- 最近活跃时间 `LastActiveTimeUtc`
- 失败次数 `FailureCount`
- 连续健康检查失败次数 `_consecutiveHealthCheckFailures`
- 最后一次连接成功时间 `LastConnectedTimeUtc`
- 最后一次失败时间 `LastFailureTimeUtc`
- 最后一次恢复时间 `LastRecoveredTimeUtc`
- 最后一次维护时间 `LastMaintenanceTimeUtc`
- 最后失败原因 `LastFailureReason`
- 最后维护触发来源 `LastMaintenanceMode`
- 当前是否处于维护中 `IsUnderMaintenance`
- 当前是否已进入移除流程 `_isRemoving`

这些数据最终都会体现在快照接口中，适合做日志、诊断页或监控面板。

## 快照与观测
### GetStates
`GetStates()` 返回一个字典，只包含：

- `ConnectionIdentity`
- `ConnectionEntryState`

适合做轻量级状态总览。

### GetState
`GetState(identity)` 返回单条目的详细快照，包含：

- 当前状态
- 活跃租约数量
- 失败次数
- 是否存在过期租约
- 是否处于维护中
- 最近活跃/连接/失败/恢复/维护时间
- 最后失败原因
- 最后维护来源

### GetEntrySnapshots
`GetEntrySnapshots()` 返回所有条目的详细快照列表。

适合：

- 监控页面
- 导出诊断信息
- 全量健康巡检

### GetPoolSnapshot
`GetPoolSnapshot()` 在所有单条目快照基础上，再构建整体统计：

- `TotalEntries`
- `ReadyEntries`
- `BusyEntries`
- `DisconnectedEntries`
- `UnavailableEntries`
- `TotalActiveLeases`
- `CapturedAtUtc`
- `Entries`

示例：
```csharp
var entrySnapshot = pool.GetState(identity);
var allSnapshots = pool.GetEntrySnapshots();
var poolSnapshot = pool.GetPoolSnapshot();

if (poolSnapshot.IsSuccess)
{
    Console.WriteLine("总条目数: " + poolSnapshot.ResultValue.TotalEntries);
    Console.WriteLine("不可用条目数: " + poolSnapshot.ResultValue.UnavailableEntries);
    Console.WriteLine("活跃租约总数: " + poolSnapshot.ResultValue.TotalActiveLeases);
}
```

## 生命周期事件
连接池提供四类事件接口，便于外部做日志、埋点、监控与告警。

### PoolEventOccurred
通用生命周期事件，典型事件类型包括：

- `Registered`
- `ConnectStarting`
- `ConnectSucceeded`
- `ConnectFailed`
- `ExecuteStarting`
- `ExecuteFailed`
- `Retrying`
- `Reconnecting`
- `Recovered`
- `Invalidated`
- `IdleCleaned`
- `Disposed`
- `BackgroundMaintenanceCompleted`
- `ForceReconnectRequested`
- `Unregistered`

### ConnectionStateChanged
状态变化事件，关注 `PreviousState -> CurrentState` 的转换过程。

### LeaseChanged
租约生命周期事件，关注：

- `LeaseAcquired`
- `LeaseReleased`
- `LeaseExpired`

### MaintenanceCompleted
后台维护完成事件。

示例：
```csharp
pool.PoolEventOccurred += (sender, args) =>
{
    Console.WriteLine(args.EventType + ": " + args.Identity + " - " + args.Message);
};

pool.ConnectionStateChanged += (sender, args) =>
{
    Console.WriteLine("状态变化: " + args.PreviousState + " -> " + args.CurrentState);
};

pool.LeaseChanged += (sender, args) =>
{
    Console.WriteLine("租约事件: " + args.EventType + ", LeaseId=" + args.Lease.LeaseId);
};

pool.MaintenanceCompleted += (sender, args) =>
{
    Console.WriteLine("后台维护完成，扫描=" + args.ScannedEntryCount + "，受影响=" + args.AffectedEntryCount);
};
```

多数事件参数中还包含以下辅助信息：

- `Identity`
- `State`
- `Message`
- `Exception`
- `ActiveLeaseCount`
- `FailureCount`
- `TriggerMode`
- `OccurredAtUtc`

其中 `TriggerMode` 可以帮助你区分本次动作是由：

- `UserCall`
- `Background`
- `Cleanup`
- `ForceReconnect`
- `Dispose`

所触发。

## 并发行为说明
根据当前实现，连接池的并发特征如下：

- 不同设备之间可以并行执行。
- 同一设备条目内部通过异步锁串行化，避免并发改写连接状态。
- 同一身份的重复注册具备原子性保护。
- 后台维护和手动控制方法在条目级会共享同一状态保护逻辑。

这意味着：

- 可以安全地同时访问多个不同设备。
- 对同一个设备的大量并发请求不会导致条目状态混乱，但会串行排队。

## 释放与关闭
### Dispose
`DeviceConnectionPool.Dispose()` 会：

- 标记连接池已释放。
- 取消后台维护任务。
- 将全部条目标记为准备移除。
- 从 `_entries` 中移除所有条目。
- 依次断开并释放每个连接条目。

注意：

- 连接池释放后，再调用大多数公开方法会抛出 `ObjectDisposedException`。
- 已经拿到的租约在池释放后仍可调用 `Release()`，会返回成功，便于上层做善后。

## 推荐实践
- 业务层优先使用 `ExecuteAsync()`，除非确实需要手动租约控制。
- `ConnectionIdentity` 保持稳定且唯一，不要动态拼接无意义字段。
- 同一设备只注册一次，避免重复建条目。
- 让 `EnableBackgroundMaintenance` 保持开启，除非你确定由外部统一调度维护动作。
- 定期抓取 `GetPoolSnapshot()` 做监控，尤其关注 `UnavailableEntries`、`TotalActiveLeases`。
- 对重要写操作，结合 `OperationResult.Message`、事件和快照做统一诊断日志。
- 业务如果手动 `Acquire()`，必须使用 `try/finally` 保证 `Release()`。

## 常见问题
### 为什么 Register 成功后没有立刻连设备？
因为当前实现采用“注册条目”和“首次真正建连”分离的方式。底层连接通常在第一次 `Acquire()` 或 `ExecuteAsync()` 时建立。

### Invalidate 和 Unregister 有什么区别？
- `Invalidate()`：保留条目，但标记为失效，后续不可继续租用。
- `Unregister()`：直接把条目从池中移除并释放底层资源。

### ForceReconnect 为什么在有活跃租约时失败？
因为强制重连会主动断开并重建连接，如果此时仍有业务在使用，会破坏执行中的上下文，所以实现上直接拒绝。

### CleanupIdle 会不会清掉正在使用的连接？
不会。它只清理“无活跃租约且空闲超时”的条目。

### CleanupExpiredLeases 的意义是什么？
它是租约泄漏的兜底机制，适合在调用方异常、超时或遗漏释放时回收条目占用。

## 简短示例：维护与诊断
```csharp
var invalidateResult = pool.Invalidate(identity, "设备参数已变更，暂停使用旧连接");
var reconnectResult = pool.ForceReconnect(identity, "参数更新后触发重连");
var idleCleanResult = pool.CleanupIdle();
var expiredCleanResult = pool.CleanupExpiredLeases();
var snapshotResult = pool.GetPoolSnapshot();

Console.WriteLine("失效结果: " + invalidateResult.IsSuccess);
Console.WriteLine("重连结果: " + reconnectResult.IsSuccess);
Console.WriteLine("空闲回收数量: " + idleCleanResult.ResultValue);
Console.WriteLine("过期租约清理数量: " + expiredCleanResult.ResultValue);
Console.WriteLine("池内总条目数: " + snapshotResult.ResultValue.TotalEntries);
```

## 兼容说明
- 原有 `Register / Acquire / Release / ExecuteAsync / GetStates / CleanupIdle` 用法继续保留。
- 新增能力以增量方式提供，不影响现有通过连接池执行读写的基本调用路径。
- 如果上层只需要部分能力，可以直接依赖更小接口：`IDeviceConnectionPoolQuery`、`IDeviceConnectionPoolControl`、`IDeviceConnectionPoolExecution`、`IDeviceConnectionPoolEvents`。

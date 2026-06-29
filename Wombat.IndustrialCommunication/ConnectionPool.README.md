# ConnectionPool（重构后）使用说明

## 文档目的
本文档描述 `ConnectionPool` 在“对外分开、内核合并”重构后的实际形态，并对照 `ConnectionPool.RefactorPlan.md` 给出一致性检查结论，供调用方迁移与接入。

## 重构一致性检查结论
结论：`ConnectionPool` 已按“对外分开、内核合并”的目标完成重构，当前实现与方案关键点一致。

### 已落实项
- 对外接口已分离：`IDeviceClientPool` 与 `IDeviceServerPool` 独立存在。
- 统一资源池接口已落地：`IResourcePool<TResource>`、`IResourcePoolControl`、`IResourcePoolQuery`、`IResourcePoolExecution<TResource>`、`IResourcePoolEvents`。
- 通用池基类已落地：`ResourcePool<TResource>` 统一承载注册、租约、执行、维护、快照、释放与事件分发。
- 池化连接抽象已切换：`IPooledResourceConnection<TResource>`、`IPooledResourceConnectionFactory<TResource>`。
- 描述模型已升级：`ResourceDescriptor` 已包含 `ResourceRole`、`DeviceConnectionType` 与 `ConnectionParameters` 强类型参数对象。
- 服务端池已接入：`DeviceServerPool` + `DefaultPooledDeviceServerConnectionFactory` +
  - `ModbusTcpServerPooledConnection`
  - `ModbusRtuServerPooledConnection`
  - `S7TcpServerPooledConnection`
- 客户端池已保留点位读写能力：`ReadPointsAsync/WritePointsAsync` 仅在 `IDeviceClientPool` 暴露。
- 服务端生命周期能力已分离：`Start/Stop` 仅在 `IDeviceServerPool` 暴露。
- 事件语义已增强：`ConnectionPoolEventArgs` 新增 `ResourceRole` 字段，池级与条目级事件会补齐角色信息（`Client/Server`）。
- 旧命名已清理：在 `ConnectionPool` 目录中未发现 `IDeviceConnectionPool*`、`IPooledDeviceConnection*`、`DeviceConnectionPool` 实装残留。
- 高并发控制面已补强：强制关闭、强制重连、故障恢复已接入内部调度器，支持批量提交与独立并发阈值。
- 事件分发已改为后台异步出队，慢订阅者不会同步阻塞池主流程。
- 客户端同步 `Ping` 探测已移除，连接真实性以协议层 `ConnectAsync/ProbeAsync` 与业务执行结果为准。

### 与方案对照（当前代码现态）
- 对照 1：方案中的统一入口基类 `ResourcePool<TResource>` 已实现，并被 `DeviceClientPool` 与 `DeviceServerPool` 继承。
- 对照 2：方案建议的 `ConnectionPoolEventArgs.ResourceRole` 已实现，并在事件发布链路中自动归一化。

## 目录结构（重构后）
- `ConnectionPool/Interfaces`
  - 资源池聚合与分层接口（`IResourcePool*`、`IDeviceClientPool`、`IDeviceServerPool`）
- `ConnectionPool/Core`
  - 双门面池实现（`DeviceClientPool`、`DeviceServerPool`）
  - 通用池基类（`ResourcePool<T>`）
  - 泛型条目/执行器/维护服务/控制调度器（`PooledResourceEntry<T>`、`PooledResourceExecutor<T>`、`ConnectionPoolMaintenanceService<T>`、`ConnectionControlScheduler<T>`）
- `ConnectionPool/Factories`
  - `DefaultPooledDeviceClientConnectionFactory`
  - `DefaultPooledDeviceServerConnectionFactory`
- `ConnectionPool/Wrappers`
  - 客户端与服务端池化包装器
- `ConnectionPool/Models`
  - `ResourceDescriptor`、`ResourceRole`、`Connection*` 状态/快照/租约模型

## 对外 API（重构后）
### 客户端池
- 接口：`IDeviceClientPool : IResourcePool<IDeviceClient>, IDeviceClientPointExecution`
- 实现：`DeviceClientPool`
- 典型能力：
  - `Register/Acquire/Release/Invalidate/Unregister/ForceReconnect`
  - `ForceCloseAsync/ForceReconnectAsync`
  - `ForceCloseManyAsync/ForceReconnectManyAsync/RecoverManyAsync`
  - `ExecuteAsync`
  - `ReadPointsAsync/WritePointsAsync`
  - `GetStates/GetState/GetEntrySnapshots/GetPoolSnapshot`

### 服务端池
- 接口：`IDeviceServerPool : IResourcePool<IDeviceServer>, IDeviceServerLifecycleExecution`
- 实现：`DeviceServerPool`
- 典型能力：
  - 资源池通用控制与执行能力（同 `IResourcePool`）
  - `Start/StartAsync`
  - `Stop/StopAsync`

## 资源语义映射
当前包装器语义与方案一致：
- `IDeviceClient`：
  - `EnsureAvailable` -> `ConnectAsync`
  - `DisconnectOrShutdown` -> `Disconnect`
- `IDeviceServer`：
  - `EnsureAvailable` -> `Listen`
  - `DisconnectOrShutdown` -> `Shutdown`

## 注册与路由规则
### 统一描述模型
`ResourceDescriptor` 关键字段：
- `Identity`：`DeviceId + ProtocolType + Endpoint`
- `ResourceRole`：`Client | Server`
- `DeviceConnectionType`：协议/连接类型主路由键
- `ConnectionType`：可选的展示或兼容字段，默认工厂当前不依赖它做路由
- `ConnectionParameters`：强类型连接参数对象

### 工厂路由
- Client 工厂：仅接受 `ResourceRole = Unknown/Client`，支持 `ModbusTcp/ModbusRtu/SiemensS7/Fins`。
- Server 工厂：仅接受 `ResourceRole = Unknown/Server`，支持 `ModbusTcp/ModbusRtu/SiemensS7`（对应 `ModbusTcpServer/ModbusRtuServer/S7TcpServer`）。
- 默认工厂当前直接根据 `DeviceConnectionType + ConnectionParameters` 路由和创建连接，不再解析字符串参数字典。

### 常用 `ConnectionParameters` 参数对象
- `ModbusTcpClientConnectionParameters`：`Ip`、`Port`、`ConnectTimeoutMilliseconds`、`ReceiveTimeoutMilliseconds`、`SendTimeoutMilliseconds`、`Retries`、`ProbeAddress`、`ProbeDataType`、`ProbeLength`、`BatchReadStationIntervalMilliseconds`。
- `ModbusRtuClientConnectionParameters`：`PortName`、`BaudRate`、`DataBits`、`StopBits`、`Parity`、`Handshake`，以及客户端通用超时、重试、探活和批量读取间隔属性。
- `SiemensS7ClientConnectionParameters`：`Ip`、`Port`、`SiemensVersion`、`Slot`、`Rack`，以及客户端通用超时、重试、探活和批量读取间隔属性。
- `FinsClientConnectionParameters`：`Ip`、`Port`、`TimeoutMilliseconds`，以及客户端通用超时、重试和探活属性。
- `ModbusTcpServerConnectionParameters` / `SiemensS7ServerConnectionParameters`：`Ip`、`Port`、`ConnectTimeoutMilliseconds`、`ReceiveTimeoutMilliseconds`、`SendTimeoutMilliseconds`、`MaxConnections`、`EnableSnapshotPersistence`。
- `ModbusRtuServerConnectionParameters`：`PortName`、`BaudRate`、`DataBits`、`StopBits`、`Parity`、`Handshake`，以及服务端通用超时和快照持久化属性。

## 快速开始
### 客户端池示例
```csharp
var options = new ConnectionPoolOptions();
var clientFactory = new DefaultPooledDeviceClientConnectionFactory();
IDeviceClientPool clientPool = new DeviceClientPool(options, clientFactory);

var descriptor = new ResourceDescriptor
{
    Identity = new ConnectionIdentity
    {
        DeviceId = "plc-1",
        ProtocolType = "ModbusTcp",
        Endpoint = "192.168.1.10:502"
    },
    ResourceRole = ResourceRole.Client,
    DeviceConnectionType = DeviceConnectionType.ModbusTcp,
    ConnectionParameters = new ModbusTcpClientConnectionParameters
    {
        Ip = "192.168.1.10",
        Port = 502,
        ConnectTimeoutMilliseconds = 3000,
        ReceiveTimeoutMilliseconds = 3000,
        SendTimeoutMilliseconds = 3000,
        BatchReadStationIntervalMilliseconds = 100
    }
};

var register = clientPool.Register(descriptor);
if (register.IsSuccess)
{
    var read = await clientPool.ExecuteAsync(
        descriptor.Identity,
        async c => c.ReadUInt16Async("1;3;0"));
}
```

### Modbus 批量读取跨站延迟示例
当一次批量读取包含多个站号，并且希望每个站号之间留出固定间隔时，可以这样配置：

```csharp
var descriptor = new ResourceDescriptor
{
    Identity = new ConnectionIdentity
    {
        DeviceId = "modbus-multi-station",
        ProtocolType = "ModbusTcp",
        Endpoint = "192.168.1.10:502"
    },
    ResourceRole = ResourceRole.Client,
    DeviceConnectionType = DeviceConnectionType.ModbusTcp,
    ConnectionParameters = new ModbusTcpClientConnectionParameters
    {
        Ip = "192.168.1.10",
        Port = 502,
        BatchReadStationIntervalMilliseconds = 100
    }
};

await clientPool.ExecuteAsync(
    descriptor.Identity,
    async client => await client.BatchReadAsync(new Dictionary<string, DataTypeEnums>
    {
        ["1;40001"] = DataTypeEnums.UInt16,
        ["2;40001"] = DataTypeEnums.UInt16,
        ["3;40001"] = DataTypeEnums.UInt16
    }));
```

上面的配置表示：
- 同一站号内的地址仍会按现有优化逻辑尽量合并读取。
- 当批量读取从一个站号切换到下一个站号时，会等待 `100ms`。
- 若未配置该参数或配置为 `0`，则保持原有行为，不额外等待。

### 服务端池示例
```csharp
var options = new ConnectionPoolOptions();
var serverFactory = new DefaultPooledDeviceServerConnectionFactory();
IDeviceServerPool serverPool = new DeviceServerPool(options, serverFactory);

var descriptor = new ResourceDescriptor
{
    Identity = new ConnectionIdentity
    {
        DeviceId = "mb-server-1",
        ProtocolType = "ModbusTcp",
        Endpoint = "0.0.0.0:502"
    },
    ResourceRole = ResourceRole.Server,
    DeviceConnectionType = DeviceConnectionType.ModbusTcp,
    ConnectionParameters = new ModbusTcpServerConnectionParameters
    {
        Ip = "0.0.0.0",
        Port = 502
    }
};

var register = serverPool.Register(descriptor);
if (register.IsSuccess)
{
    var start = await serverPool.StartAsync(descriptor.Identity);
}
```

### 批量控制示例
批量控制接口只负责提交控制命令并汇总每个条目的结果，实际执行受 `ConnectionPoolOptions` 中的并发阈值控制。

```csharp
var identities = new[]
{
    new ConnectionIdentity { DeviceId = "plc-1", ProtocolType = "SiemensS7", Endpoint = "192.168.1.10:102" },
    new ConnectionIdentity { DeviceId = "plc-2", ProtocolType = "SiemensS7", Endpoint = "192.168.1.11:102" }
};

var closeMany = await clientPool.ForceCloseManyAsync(identities, "批量强制关闭");
foreach (var item in closeMany.ResultValue)
{
    Console.WriteLine($"{item.Key.DeviceId}: {item.Value.IsSuccess}, {item.Value.Message}");
}

var reconnectMany = await clientPool.ForceReconnectManyAsync(identities, "批量强制重连");
```

## 维护与恢复
`DeviceClientPool` 与 `DeviceServerPool` 通过 `ResourcePool<TResource>` 共享同一套维护机制：
- 过期租约清理：`CleanupExpiredLeases()`
- 空闲条目回收：`CleanupIdle()`
- 后台维护循环：由 `ConnectionPoolOptions.EnableBackgroundMaintenance` 控制
- 可恢复故障重试：由 `PooledResourceExecutor<TResource>`、`ConnectionExecutionOptions` 与 `ConnectionControlScheduler<TResource>` 协同决定

高并发控制面行为：
- 条目进入 `Faulted` 后会投递恢复任务，恢复不再依赖后台全池扫描内联执行。
- 后台维护保留租约过期回收、空闲在线连接探活和轻量清理。
- 健康检查与恢复使用不同并发阈值，避免离线恢复风暴占满探活通道。
- `ForceCloseAsync` 与 `ForceReconnectAsync` 经内部调度器执行，批量接口不会在调用线程逐条串行跑完整控制流程。
- 恢复退避包含指数退避和随机抖动，避免大量离线条目同拍重连。
- 租约过期扫描会跳过存在活跃执行的条目，避免长时间 IO 被误回收。

服务端侧补充：
- `StartAsync` 内置重试与指数退避（上限封顶），并对常见端口冲突信息做专门失败提示。

## 高并发参数
`ConnectionPoolOptions` 中与控制面相关的参数：
- `MaxConcurrentRecoveries`：同时允许执行的故障恢复数，默认 `4`。
- `MaxConcurrentForceCloses`：同时允许执行的强制关闭/强制重连数，默认 `16`。
- `MaxConcurrentHealthChecks`：后台健康检查并发数，默认 `4`。
- `MaxConcurrentExecutionsPerEntry`：单条目同时允许的业务执行数，默认 `1`；设置为 `0` 或负数表示不限制。
- `FaultedReconnectCooldown`：故障态再次恢复前的冷却时间。
- `RetryBackoff`：恢复与执行重试的基础退避时间。
- `MaxRetryCount`：恢复性重试次数。

示例：

```csharp
var options = new ConnectionPoolOptions
{
    MaxConnections = 512,
    EnableBackgroundMaintenance = true,
    MaxConcurrentRecoveries = 32,
    MaxConcurrentForceCloses = 64,
    MaxConcurrentHealthChecks = 16,
    MaxConcurrentExecutionsPerEntry = 1,
    FaultedReconnectCooldown = TimeSpan.FromSeconds(2),
    RetryBackoff = TimeSpan.FromMilliseconds(200),
    MaxRetryCount = 3
};
```

## 高并发测试
当前已增加的连接池高并发测试：
- `ConnectionPoolS7StableHundredClientBatchReadTests.ConnectionPool_S7_100Servers_100Clients_Should_BatchRead_Stably`
  - 本地启动 100 个 `S7TcpServer`
  - 注册 100 个 S7 client
  - 预写入 248 个混合 DB1 地址
  - 多轮 100 路并发 `BatchReadAsync`，校验每个 client 的读回值
- `ConnectionPoolS7StableHundredClientBatchReadTests.ConnectionPool_S7_100Servers_100Clients_Should_Recover_When_Servers_Randomly_Drop_10Times_In_2Minutes`
  - 基于同样的 100 server/client 场景
  - 2 分钟内随机执行 10 次单 server `Shutdown -> 短暂停机 -> Listen`
  - 掉线窗口内允许读取失败
  - 测试结束后要求 100 个 client 全部恢复并读回正确值

单独运行示例：

```powershell
dotnet test Wombat.IndustrialCommunicationTestProject\Wombat.IndustrialCommunicationTestProject.csproj --filter "FullyQualifiedName~ConnectionPoolS7StableHundredClientBatchReadTests"
```

## 破坏性变更与迁移提示
### 已删除/废弃的旧类型
- `IDeviceConnectionPool*`
- `IPooledDeviceConnection*`
- `DeviceConnectionPool`

### 迁移要点
- 原 `DeviceConnectionPool` 调用方迁移到 `DeviceClientPool`。
- 服务端对象统一通过 `DeviceServerPool` 注册与启动，不再走客户端池。
- 构造 `ResourceDescriptor` 时建议显式设置 `ResourceRole`，避免同协议 Client/Server 语义歧义。
- 旧的 `descriptor.Parameters["..."]` 写法已移除，需改为 `ConnectionParameters = new XxxConnectionParameters { ... }`。

## 当前限制与后续建议
- 当前事件模型已包含 `ResourceRole`，建议订阅方按 `ResourceRole + EventType` 组合做路由与聚合统计。
- `DeviceClientPool/DeviceServerPool` 仍保留各自业务能力扩展点（点位读写、服务端启停）；后续可继续在不破坏基类职责边界前提下增量演进。

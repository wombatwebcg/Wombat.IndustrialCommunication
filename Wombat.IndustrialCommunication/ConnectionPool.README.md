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
- 描述模型已升级：`ResourceDescriptor` 已包含 `ResourceRole`，并保留 `DeviceConnectionType` 与参数字典。
- 服务端池已接入：`DeviceServerPool` + `DefaultPooledDeviceServerConnectionFactory` +
  - `ModbusTcpServerPooledConnection`
  - `ModbusRtuServerPooledConnection`
  - `S7TcpServerPooledConnection`
- 客户端池已保留点位读写能力：`ReadPointsAsync/WritePointsAsync` 仅在 `IDeviceClientPool` 暴露。
- 服务端生命周期能力已分离：`Start/Stop` 仅在 `IDeviceServerPool` 暴露。
- 事件语义已增强：`ConnectionPoolEventArgs` 新增 `ResourceRole` 字段，池级与条目级事件会补齐角色信息（`Client/Server`）。
- 旧命名已清理：在 `ConnectionPool` 目录中未发现 `IDeviceConnectionPool*`、`IPooledDeviceConnection*`、`DeviceConnectionPool` 实装残留。

### 与方案对照（当前代码现态）
- 对照 1：方案中的统一入口基类 `ResourcePool<TResource>` 已实现，并被 `DeviceClientPool` 与 `DeviceServerPool` 继承。
- 对照 2：方案建议的 `ConnectionPoolEventArgs.ResourceRole` 已实现，并在事件发布链路中自动归一化。

## 目录结构（重构后）
- `ConnectionPool/Interfaces`
  - 资源池聚合与分层接口（`IResourcePool*`、`IDeviceClientPool`、`IDeviceServerPool`）
- `ConnectionPool/Core`
  - 双门面池实现（`DeviceClientPool`、`DeviceServerPool`）
  - 通用池基类（`ResourcePool<T>`）
  - 泛型条目/执行器/维护服务（`PooledResourceEntry<T>`、`PooledResourceExecutor<T>`、`ConnectionPoolMaintenanceService<T>`）
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
- `ConnectionType`：字符串兜底路由键
- `Parameters`：连接参数

### 工厂路由
- Client 工厂：仅接受 `ResourceRole = Unknown/Client`，支持 `ModbusTcp/ModbusRtu/SiemensS7/Fins`。
- Server 工厂：仅接受 `ResourceRole = Unknown/Server`，支持 `ModbusTcp/ModbusRtu/SiemensS7`（对应 `ModbusTcpServer/ModbusRtuServer/S7TcpServer`）。

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
    DeviceConnectionType = DeviceConnectionType.ModbusTcp
};

descriptor.Parameters["ip"] = "192.168.1.10";
descriptor.Parameters["port"] = 502;

var register = clientPool.Register(descriptor);
if (register.IsSuccess)
{
    var read = await clientPool.ExecuteAsync(
        descriptor.Identity,
        async c => c.ReadUInt16Async("1;3;0"));
}
```

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
    DeviceConnectionType = DeviceConnectionType.ModbusTcp
};

descriptor.Parameters["ip"] = "0.0.0.0";
descriptor.Parameters["port"] = 502;

var register = serverPool.Register(descriptor);
if (register.IsSuccess)
{
    var start = await serverPool.StartAsync(descriptor.Identity);
}
```

## 维护与恢复
`DeviceClientPool` 与 `DeviceServerPool` 通过 `ResourcePool<TResource>` 共享同一套维护机制：
- 过期租约清理：`CleanupExpiredLeases()`
- 空闲条目回收：`CleanupIdle()`
- 后台维护循环：由 `ConnectionPoolOptions.EnableBackgroundMaintenance` 控制
- 可恢复故障重试：由 `PooledResourceExecutor<TResource>` 与 `ConnectionExecutionOptions` 协同决定

服务端侧补充：
- `StartAsync` 内置重试与指数退避（上限封顶），并对常见端口冲突信息做专门失败提示。

## 破坏性变更与迁移提示
### 已删除/废弃的旧类型
- `IDeviceConnectionPool*`
- `IPooledDeviceConnection*`
- `DeviceConnectionPool`

### 迁移要点
- 原 `DeviceConnectionPool` 调用方迁移到 `DeviceClientPool`。
- 服务端对象统一通过 `DeviceServerPool` 注册与启动，不再走客户端池。
- 构造 `ResourceDescriptor` 时建议显式设置 `ResourceRole`，避免同协议 Client/Server 语义歧义。

## 当前限制与后续建议
- 当前事件模型已包含 `ResourceRole`，建议订阅方按 `ResourceRole + EventType` 组合做路由与聚合统计。
- `DeviceClientPool/DeviceServerPool` 仍保留各自业务能力扩展点（点位读写、服务端启停）；后续可继续在不破坏基类职责边界前提下增量演进。

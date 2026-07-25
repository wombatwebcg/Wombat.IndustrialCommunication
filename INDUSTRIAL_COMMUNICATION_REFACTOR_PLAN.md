# Wombat 工业通信库破坏性重构设计

## 1. 文档状态

- 日期：2026-07-25
- 范围：`Wombat.IndustrialCommunication`
- 参考实现：`ThingsGateway` 的通道、设备运行时和通道生命周期模型
- 兼容策略：不兼容旧 `ConnectionPool` API，不提供适配层，不保留废弃类型
- 当前阶段：设计完成，尚未修改业务代码

## 2. 结论

删除现有数据库式“连接池”模型，替换为工业通信的“物理通道管理”模型。

工业通信连接通常不是可互换资源：串口由物理端口独占，PLC TCP 会话具有设备、协议和时序上下文，同一端点上的操作需要串行或受控并发。因此库的核心不应是“租用任意连接并归还”，而应是：

```text
ChannelManager
  └─ ChannelRuntime（一个物理通信通道）
       ├─ 固定 Transport/ProtocolClient
       ├─ 生命周期状态机
       ├─ 单通道执行闸门
       ├─ 故障恢复
       ├─ 状态快照
       └─ 一个或多个 LogicalDevice（可选）
```

客户端通道和服务器监听器是两种生命周期，必须分开：

```text
主动通信：ChannelManager -> ChannelRuntime -> ProtocolClient -> Transport
被动监听：ServerHost -> Listener -> Session -> ProtocolHandler
```

服务器不进入 `ChannelManager`，网络会话也不作为可租用连接。

## 3. 当前问题

### 3.1 模型错位

现有 `ConnectionPool` 包含连接租约、租约过期、空闲池回收、通用资源角色和任意资源工厂。这些概念适合数据库或 HTTP 连接池，不符合固定物理端点的工业通信模型。

当前连接身份由 `DeviceId + ProtocolType + Endpoint` 组成。它混合了逻辑设备和物理通道：

- Modbus RTU 的多个站号可能共享同一个串口。
- Modbus TCP 网关后的多个站号可能共享同一个 TCP 连接。
- 串口是否相同不能只看端口名，还必须比较波特率、数据位、停止位、校验和握手等连接参数。
- PLC 的机架、槽位或站号属于协议上下文，不一定属于物理传输身份。

### 3.2 生命周期存在多个所有者

连接、断开和重连同时存在于：

- `TcpClientAdapter` / `SerialPortAdapter`
- `SiemensClient` / `FinsClient` 等协议客户端
- `PooledResourceEntry`
- `PooledResourceExecutor`
- `ConnectionControlScheduler`
- 后台维护服务

同一故障可能触发协议客户端重连、执行器重试和后台恢复。多个所有者会产生重复重连、断开与读写交错、错误被覆盖以及关闭期间重新连接等竞态。

### 3.3 公共 API 过大

旧模型对外暴露了注册、租约、过期、清理、恢复、强制关闭、批量控制、池快照、条目快照、事件发布器、通用资源包装器及客户端/服务器池。调用方必须理解内部调度才能正确使用通信库。

目标 API 应让调用方只关心：

1. 通道配置。
2. 启动、停止或重启通道。
3. 在指定通道执行协议操作。
4. 查询通道状态。

### 3.4 取消和错误语义不统一

`IClient.ConnectAsync()` 和 `DisconnectAsync()` 没有 `CancellationToken`。部分同步 API 使用 `Task.Run(...).GetAwaiter().GetResult()` 包装异步代码。连接池又单独增加取消、超时和恢复逻辑。

错误既由异常表达，也由可变 `OperationResult`、错误消息和 `OperationInfo` 表达。恢复逻辑无法稳定区分：

- 参数错误。
- 协议拒绝。
- 通信超时。
- 传输断开。
- 调用方取消。
- 写入请求已发送但响应丢失。

最后一种情况不能安全自动重试，否则可能重复写入。

## 4. 重构原则

1. 一个物理通道只有一个生命周期所有者：`ChannelRuntime`。
2. 协议客户端只完成一次连接、一次断开和一次协议操作，不自行后台重连。
3. Transport 只负责字节传输，不理解连接恢复、协议重试或设备状态。
4. 默认每通道并发数为 1；仅协议明确支持且经过验证时提高。
5. 所有 I/O，包括探测和初始化，都经过同一个通道执行闸门。
6. 取消必须贯穿等待、连接、收发和恢复；关闭资源本身不能被取消后遗留。
7. 不用错误消息判断故障类别。
8. 写入出现不确定结果时明确返回 `OutcomeUnknown`，不得自动重试。
9. 不保留旧 API 兜底；编译错误用于暴露所有待迁移调用点。
10. 不引入新的任务调度框架，使用 `SemaphoreSlim`、`CancellationTokenSource` 和现有异步原语。

## 5. 目标分层

### 5.1 Transport 层

职责：建立物理连接并可靠收发字节。

```csharp
public interface ITransport : IAsyncDisposable
{
    bool IsConnected { get; }
    ValueTask ConnectAsync(CancellationToken cancellationToken);
    ValueTask DisconnectAsync();
    ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
    ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);
}
```

要求：

- TCP、串口分别实现，不包含自动重连。
- `DisconnectAsync` 必须幂等并完成实际资源释放，不接受取消令牌。
- 超时通过链接的取消令牌实现，最终转换为结构化通信错误。
- 不提供同步包装；确有同步协议 API 时，它必须走独立同步实现，不能 sync-over-async。

### 5.2 ProtocolClient 层

职责：协议握手、报文编码、请求响应关联、批量读写。

```csharp
public interface IProtocolClient
{
    bool IsConnected { get; }
    ValueTask ConnectAsync(CancellationToken cancellationToken);
    ValueTask DisconnectAsync();
}
```

协议客户端：

- 拥有一个 `ITransport`。
- 不拥有重连循环、健康检查循环或全局并发控制。
- 不感知 `ChannelManager`。
- 保留协议自身必须的事务关联能力，例如 Modbus TCP Transaction ID。
- 地址解析和批量合并留在协议层，不进入通道管理层。

### 5.3 Channel 层

#### ChannelId

`ChannelId` 是调用方明确提供的稳定标识，不根据可变字符串临时拼接。它标识一个配置和生命周期单元。

物理配置由具体配置类型表达：

```csharp
public abstract record ChannelOptions(string Id)
{
    public int MaxConcurrency { get; init; } = 1;
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public ReconnectOptions Reconnect { get; init; } = new();
}

public sealed record TcpChannelOptions(string Id, string Host, int Port)
    : ChannelOptions(Id);

public sealed record SerialChannelOptions(string Id, string PortName)
    : ChannelOptions(Id)
{
    public int BaudRate { get; init; } = 9600;
    public int DataBits { get; init; } = 8;
    public Parity Parity { get; init; } = Parity.None;
    public StopBits StopBits { get; init; } = StopBits.One;
    public Handshake Handshake { get; init; } = Handshake.None;
}
```

不再使用 `Dictionary<string, string>` 传递协议和连接参数。缺少必需配置时在注册阶段立即失败。

#### ChannelRuntime

每个运行时包含：

- 不可变 `ChannelOptions`。
- 一个协议客户端实例。
- 一个执行 `SemaphoreSlim`。
- 一个生命周期异步锁。
- 一个关闭 `CancellationTokenSource`。
- 当前结构化状态与只读快照。
- 一个正在执行的连接/恢复任务，用于合并并发恢复请求。

状态机：

```text
Created -> Connecting -> Online
   |          |           |
   |          v           v
   +-------> Faulted <-----+
                |
                v
           Reconnecting -> Online
                |
                v
             Stopping -> Stopped

任意非终态 -> Stopping -> Stopped
Stopped 为终态；重新启动创建新的 ChannelRuntime。
```

禁止无状态跳转。状态变化在锁内完成，事件在锁外发布。

#### ChannelManager

```csharp
public interface IChannelManager : IAsyncDisposable
{
    ValueTask AddAsync(ChannelOptions options, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(string channelId);
    ValueTask RestartAsync(string channelId, ChannelOptions options, CancellationToken cancellationToken = default);
    ValueTask<TResult> ExecuteAsync<TResult>(
        string channelId,
        Func<IProtocolClient, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default);
    bool TryGetSnapshot(string channelId, out ChannelSnapshot snapshot);
}
```

实际实现可以使用泛型或协议专用句柄避免调用方强制转换，但只保留一种公开执行路径，不同时维护租约和执行器两套入口。

### 5.4 LogicalDevice 层

逻辑设备不是连接，不参与连接生命周期。它用于表达共享通道上的协议寻址信息：

```text
Modbus：UnitId
S7：Rack/Slot（若连接握手要求，则进入协议客户端配置）
FINS：源/目标节点地址
```

只有确实存在多逻辑设备共享物理通道的协议才建立该模型。第一阶段可直接由操作参数携带站号，不先创建设备注册中心。

### 5.5 Server 层

删除 `DeviceServerPool`。服务器改为明确的 Host 模型：

```text
ServerHost
  ├─ Listener 生命周期
  ├─ Session 集合
  ├─ 每 Session 接收循环
  └─ ProtocolHandler
```

- TCP Server 的连接由远端建立，不能提前注册或租用。
- 串口 Server 本质是单一监听通道，由 `ServerHost` 独占串口。
- 会话上限、空闲会话清理属于服务器配置，不属于客户端通道配置。
- 数据快照持久化属于 Server/DataStore，不进入通道生命周期。

## 6. 并发模型

### 6.1 默认规则

| 通道/协议 | 默认并发 | 说明 |
|---|---:|---|
| Modbus RTU | 1 | 半双工，同一串口严格串行 |
| Modbus TCP | 1 | 先保证兼容性；验证事务并发后显式提高 |
| Siemens S7 | 1 | PLC PDU/作业并发能力因型号而异 |
| Omron FINS TCP | 1 | 当前实现未建立完整并发请求关联保证 |
| Server session | 每会话 1 个接收循环 | 不通过客户端执行闸门 |

### 6.2 排队与背压

- `SemaphoreSlim.WaitAsync` 负责可取消等待。
- 不实现无界自定义任务队列。
- `OperationTimeout` 同时约束排队和 I/O，超时返回明确阶段信息。
- 通道停止时取消所有等待者和在途操作。
- 如果将来出现严格公平或优先级需求，再引入每通道有界队列；没有测量前不实现。

### 6.3 批量优先

提升工业通信效率的首选顺序：

1. 地址规范化和排序。
2. 合并连续地址。
3. 按协议 PDU 上限切包。
4. 减少请求往返次数。
5. 最后才评估提高单通道并发或增加物理连接。

连接数不是默认吞吐量旋钮。

## 7. 故障与恢复语义

### 7.1 结构化错误

用明确类别替代消息匹配：

```csharp
public enum CommunicationError
{
    None,
    InvalidConfiguration,
    InvalidAddress,
    Cancelled,
    QueueTimeout,
    ConnectTimeout,
    SendTimeout,
    ReceiveTimeout,
    ConnectionClosed,
    TransportFailure,
    ProtocolViolation,
    DeviceRejected,
    OutcomeUnknown
}
```

错误必须保留原始异常和协议错误码。日志文本只用于诊断，不参与控制流。

### 7.2 恢复规则

- 参数、地址和协议拒绝错误：不重连、不重试。
- 连接关闭、Socket/IO 错误：通道进入 `Faulted`，合并为单个恢复任务。
- 读操作：连接恢复后最多重试一次，且必须由明确策略开启。
- 写操作在发送前失败：允许恢复后重试一次。
- 写操作在已发送后响应丢失：返回 `OutcomeUnknown`，不自动重试。
- 调用方取消：不标记通道故障。
- 重连使用有上限的指数退避和抖动；停止后立即终止。
- 连续恢复失败保持 `Faulted`，由下一次操作或显式 `RestartAsync` 再触发，不运行永久后台重连循环。

### 7.3 健康判断

删除固定周期对所有连接主动发包的通用健康检查。

- 有正常业务流量时，以最近一次协议操作结果作为健康信号。
- 长期空闲连接默认不探测；下一次操作先使用，失败后恢复。
- 确实需要协议心跳时，由协议提供显式 `ProbeAsync`，并经过同一执行闸门。
- TCP `Connected` 属性只作为提示，不能证明远端仍在线。

## 8. 可观测性

保留一个只读 `ChannelSnapshot`：

```text
ChannelId
State
ConnectedAtUtc
LastOperationAtUtc
LastSuccessAtUtc
LastFailureAtUtc
LastError
ConsecutiveFailures
WaitingOperations
ActiveOperations
ReconnectCount
```

只提供一个状态变化事件。事件处理器异常必须隔离并记录，不得影响通道状态机。报文跟踪继续通过现有 trace/logging 能力提供，不把请求和响应字节永久累积在每次结果对象中。

## 9. 删除清单

### 9.1 整体删除

删除现有 `Wombat.IndustrialCommunication/ConnectionPool` 目录及其公共模型，包括：

- `ResourcePool<TResource>`、`DeviceClientPool`、`DeviceServerPool`
- `ConnectionLease` 及全部租约事件、租约过期扫描
- `PooledResourceEntry<TResource>`、`PooledResourceExecutor<TResource>`
- `IPooledResourceConnection*` 及全部 `*PooledConnection` 包装器
- `ResourceDescriptor`、`ResourceRole`
- `ConnectionPoolOptions`、`ConnectionPoolParameters`
- `ConnectionPoolMaintenanceMode`
- 池/条目快照和池级维护事件
- 批量 ForceClose/ForceReconnect/Recover 控制 API
- 通用客户端/服务器连接工厂

旧 `ConnectionPool.README.md` 和根 README 中对应示例同时删除或改写。

### 9.2 删除的属性和语义

- `MaxConnections`：客户端通道由显式注册决定，不做池容量淘汰。
- `IdleTimeout`：长期工业连接默认常驻，不因无人租用回收。
- `LeaseTimeout`、租约计数、租约过期：无租约模型。
- `HealthCheckLeaseFreeOnly`：无租约模型。
- `LeaseExpirationSweepInterval`：无租约模型。
- `ResourceRole`：客户端和服务器使用不同类型系统。
- `Dictionary<string, string> Parameters`：改为强类型配置。
- `ConnectionExecutionKind`：读写重试语义由明确操作类型或协议 API 决定。
- `IsLongConnection`：通道本身就是长期连接；短连接应是显式的一次性客户端，不是布尔分支。
- 协议客户端中的 `EnableAutoReconnect`、`ReconnectDelay`、`MaxReconnectAttempts` 等：统一迁移至通道恢复策略。

### 9.3 不保留兼容层

- 不把 `ConnectionIdentity` 别名映射为 `ChannelId`。
- 不用旧 `DeviceClientPool` 包装新 `ChannelManager`。
- 不保留同步租约 API。
- 不使用 `[Obsolete]` 延迟删除。
- 测试、示例和文档必须在同一阶段改到新 API。

## 10. ThingsGateway 的采用边界

采用：

- 通道是物理资源和并发边界。
- 多个逻辑设备可以共享一个通道。
- 通道统一拥有设备操作的生命周期。
- 配置改变时替换整个运行时。
- 默认单通道并发为 1。

不采用：

- 通过全局字典加锁并扫描相同端点。
- 运行时替换正在使用的锁对象。
- `Task.Factory.StartNew` 启动未纳入所有权的后台任务。
- 固定 `Task.Delay` 等待资源关闭。
- 将数据库实体、UI 属性和运行时状态混入通信核心。
- 通过业务集合计数猜测共享通道是否可以释放。

## 11. 实施阶段

各阶段必须删除旧职责，不能以并存方式兜底。

### 阶段 1：统一底层异步契约

- 重构 `IClient`、`IStreamResource` 和 Adapter，所有异步 I/O 接收 `CancellationToken`。
- 删除 sync-over-async。
- 明确连接、断开和释放的幂等语义。
- 建立结构化通信错误。

硬门槛：TCP 和串口的连接取消、接收取消、并发关闭测试通过。

### 阶段 2：协议客户端去生命周期化

- 从 Siemens、FINS、Modbus 客户端删除自动重连和重复重试。
- 每个协议操作只执行一次，并准确标记发送阶段。
- 所有协议保留原始异常和协议响应码。

硬门槛：源码中协议客户端不存在 `EnableAutoReconnect` 或后台重连入口。

### 阶段 3：建立 ChannelRuntime

- 实现状态机、单通道闸门、关闭取消、恢复合并和快照。
- 首先接入 Modbus TCP 和 Modbus RTU，验证共享通道模型。
- 不实现逻辑设备注册中心，站号先作为操作参数。

硬门槛：并发读写、断线、取消、停止和重启竞态测试通过。

### 阶段 4：迁移 S7 和 FINS

- S7、FINS 接入统一通道生命周期。
- 清理协议内部重复锁，只保留报文关联必需的锁。
- 实机验证并发默认值和恢复行为。

硬门槛：现有真实 PLC 场景迁移为 Channel 测试并通过。

### 阶段 5：服务器模型独立

- 删除 `DeviceServerPool`。
- 建立 `ServerHost` 的监听器和会话所有权。
- 将会话上限、空闲清理、快照持久化放回 Server 配置。

硬门槛：启动/停止、端口占用、并发会话、会话取消和快照测试通过。

### 阶段 6：删除旧连接池

- 一次性删除整个 `ConnectionPool` 目录。
- 修改 ClientTest、ServerTest、README 和使用指南。
- 删除旧连接池测试，用通道行为测试替代。
- 删除不再使用的 Helper 和依赖。

硬门槛：仓库中 `rg "ConnectionPool|ConnectionLease|PooledResource"` 无业务命中。

### 阶段 7：全库稳定性收口

- 对所有公开异步 API 检查取消传播。
- 对所有后台任务检查所有权和关闭等待。
- 对所有写操作检查 `OutcomeUnknown`。
- 对批量读取检查协议上限和地址合并。
- 对日志检查敏感数据和无限增长。

硬门槛：构建、单元测试、网络故障注入和真实设备测试全部通过，无遗留后台任务。

## 12. 测试矩阵

### 12.1 通用通道测试

- 同一通道默认严格串行。
- 不同通道可以并行。
- 排队等待可取消、可超时。
- 连接中停止不会挂起。
- 操作中停止会取消操作并完整释放资源。
- 多个并发故障只触发一次重连。
- 重连期间新请求等待同一恢复任务。
- 状态跳转合法且事件顺序确定。
- 事件订阅者异常不影响通道。
- 重启后旧运行时不能重新上线。

### 12.2 错误语义测试

- 参数错误不重连。
- 协议异常响应不重连。
- Socket/IO 失败进入 `Faulted`。
- 调用方取消不进入 `Faulted`。
- 读操作恢复后按策略最多重试一次。
- 写入发送前失败可重试。
- 写入发送后断线返回 `OutcomeUnknown` 且不重试。

### 12.3 协议测试

- Modbus RTU 多站号共享一个串口且报文不交叉。
- Modbus TCP Transaction ID 与响应严格匹配。
- S7 PDU 协商失败能完整释放 Socket。
- S7 批量读写遵守协商后的 PDU 上限。
- FINS 节点握手失败能完整释放 Socket。
- 所有协议在畸形响应和半包情况下返回结构化错误。

### 12.4 服务器测试

- 重复启动明确失败。
- 端口占用明确失败。
- 停止等待所有会话退出。
- 单个会话异常不终止监听器。
- 会话上限拒绝行为可观测。
- 串口 Server 独占冲突明确失败。

## 13. 验收标准

重构完成必须同时满足：

1. 不再存在数据库式租约或可互换连接池 API。
2. 每个物理通道只有一个生命周期所有者。
3. 协议客户端不自行自动重连。
4. 客户端与服务器使用不同运行模型。
5. 所有等待和 I/O 支持取消。
6. 写入结果不确定时不会自动重试。
7. 默认同一物理通道串行执行。
8. 配置缺失、非法状态和协议错误立即暴露，不静默兜底。
9. 所有后台任务在停止/释放后结束。
10. `netstandard2.0` 与 `net10.0` 均构建通过，或在实施前明确决定删除旧目标框架；不通过条件编译掩盖行为差异。

## 14. 实施时的首个改动集

第一批代码只做以下内容：

1. 新建结构化错误类型。
2. 修改 Transport/Adapter 的连接和收发取消契约。
3. 为 TCP、串口补充取消与并发关闭测试。
4. 删除这些路径中的 sync-over-async。

第一批不创建 `ChannelManager`。底层连接的取消和关闭语义未稳定前，上层状态机没有可靠基础。


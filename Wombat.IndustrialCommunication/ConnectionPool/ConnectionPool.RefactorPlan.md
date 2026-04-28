# ConnectionPool 激进重构方案（对外分开、内核合并）

## 0. 执行现态说明（2026-04-28）

1. 本文档保留“迁移前 -> 迁移后”的历史对照语义，用于解释破坏性改名来源。
2. 文档中旧命名（如 `IDeviceConnectionPool*`、`IPooledDeviceConnection*`）仅表示“已废弃对象”，不代表当前代码实现仍在使用。
3. 当前实现命名以 `IDeviceClientPool / IDeviceServerPool / IResourcePool* / IPooledResourceConnection* / ResourceDescriptor` 为准。
4. 进度与实装详情以 `ConnectionPool.RefactorProgress.md` 为主记录。

## 1. 改造原则

本方案采用两条硬约束：

1. 对外分开：对业务公开 `ClientPool` 与 `ServerPool` 两套接口，不做混合入口。
2. 内核合并：底层统一为一套泛型资源池核心，复用状态机、租约、维护、事件、执行器。

本方案为激进改造：

1. 不兼容旧接口命名。
2. 不提供旧接口别名、`Obsolete` 过渡层或桥接类。
3. 调用方一次性迁移到新 API。

## 2. 改造目标

1. 将 `ConnectionPool` 从“仅客户端连接池”升级为“客户端池 + 服务端池”双门面体系。
2. 清理 `DeviceConnection*` 命名，统一切换为 `ResourcePool*` 命名。
3. 使 `S7TcpServer`、`ModbusRtuServer`、`ModbusTcpServer` 能纳入服务端池统一管理。
4. 通过统一核心减少重复实现，提升后续协议扩展速度。

## 3. 重命名与拆分方案

### 3.1 旧命名直接废弃

以下命名整体废弃，不保留：

1. `IDeviceConnectionPool`
2. `ISimpleDeviceConnectionPool`
3. `IDeviceConnectionPoolControl`
4. `IDeviceConnectionPoolExecution`
5. `IDeviceConnectionPoolQuery`
6. `IDeviceConnectionPoolEvents`
7. `IPooledDeviceConnection`
8. `IPooledDeviceConnectionFactory`

### 3.2 新命名体系

统一核心接口：

1. `IResourcePool<TResource>`
2. `IResourcePoolControl`
3. `IResourcePoolQuery`
4. `IResourcePoolExecution<TResource>`
5. `IResourcePoolEvents`
6. `IPooledResourceConnection<TResource>`
7. `IPooledResourceConnectionFactory<TResource>`

对外门面接口：

1. `IDeviceClientPool : IResourcePool<IDeviceClient>, IDeviceClientPointExecution`
2. `IDeviceServerPool : IResourcePool<IDeviceServer>, IDeviceServerLifecycleExecution`

说明：

1. 客户端点位能力（`ReadPointsAsync/WritePointsAsync`）只存在于 `IDeviceClientPool`。
2. 服务端生命周期能力（`Start/Stop` 或等效命名）只存在于 `IDeviceServerPool`。

## 4. 统一内核设计

### 4.1 泛型核心组件

1. `ResourcePool<TResource>`：统一入口与调度核心。
2. `PooledResourceEntry<TResource>`：单资源条目状态机。
3. `PooledResourceExecutor<TResource>`：执行、重试、恢复策略。
4. `BasePooledResourceConnection<TResource>`：包装器基类。

### 4.2 状态与事件复用

以下模型继续复用：

1. `ConnectionIdentity`
2. `ConnectionLease`
3. `ConnectionEntryState`
4. `ConnectionEntryLifecycleState`
5. `ConnectionPoolEventArgs`（建议新增 `ResourceRole` 字段）

### 4.3 资源语义映射

统一抽象动作：

1. `EnsureAvailable`
2. `Invalidate`
3. `DisconnectOrShutdown`

语义映射：

1. `IDeviceClient`：`EnsureAvailable => Connect`，`DisconnectOrShutdown => Disconnect`
2. `IDeviceServer`：`EnsureAvailable => Listen`，`DisconnectOrShutdown => Shutdown`

## 5. Client/Server 外层实现

### 5.1 客户端池

1. `DeviceClientPool` 基于 `ResourcePool<IDeviceClient>`。
2. 保留批量点位读写与现有执行策略。
3. 工厂 `DefaultPooledDeviceClientConnectionFactory` 负责创建 Modbus/S7/Fins 客户端包装。

### 5.2 服务端池

1. `DeviceServerPool` 基于 `ResourcePool<IDeviceServer>`。
2. 新增服务端包装器：
   1. `ModbusTcpServerPooledConnection`
   2. `ModbusRtuServerPooledConnection`
   3. `S7TcpServerPooledConnection`
3. 工厂 `DefaultPooledDeviceServerConnectionFactory` 按描述创建服务端包装对象。

## 6. 描述模型与路由

`DeviceConnectionDescriptor` 已升级为中性资源描述 `ResourceDescriptor`：

1. 增加 `ResourceRole = Client | Server`
2. 保留协议、端点、参数字段
3. 工厂与池注册流程基于 `ResourceRole + DeviceConnectionType` 双维路由

目标：

1. 避免 `ModbusTcp` 客户端/服务端歧义。
2. 事件和快照可直接区分资源角色。

## 7. 分阶段实施（激进版）

### 阶段 A：一次性接口切换

1. 新建 `ResourcePool*` 接口与 `DeviceClientPool/DeviceServerPool` 对外门面。
2. 删除旧 `DeviceConnection*` 接口与实现引用。
3. 编译失败由调用方同步改造处理，不保留兼容层。

交付标准：

1. 新接口体系完整。
2. 工程可编译（业务调用方已同步迁移）。

### 阶段 B：核心泛型化落地

1. 将 Entry/Executor/Pool 主链路全部泛型化。
2. 客户端池迁移到新核心并通过回归测试。
3. 清理 `IDeviceClient` 硬编码泛型外泄。

交付标准：

1. 客户端行为与重构前等价。
2. 核心层无客户端专属依赖。

### 阶段 C：服务端池接入

1. 实现服务端包装器与服务端工厂。
2. 将 `S7TcpServer`、`ModbusRtuServer`、`ModbusTcpServer` 纳入池化。
3. 完成服务端维护策略（自动重启、冷却、失败阈值）。

交付标准：

1. 三类服务端对象可注册、可执行、可维护、可回收。

### 阶段 D：命名收敛与清理

1. 清理旧文件、旧注释、旧文档中的 `DeviceConnection*` 残留。
2. 更新示例、工具层接入说明、架构图。
3. 完成最终 API 冻结。

## 8. 破坏性变更清单

1. 所有 `IDeviceConnectionPool*` 类型删除。
2. 所有 `IPooledDeviceConnection*` 类型删除。
3. `DeviceConnectionPool` 被 `DeviceClientPool` 替换。
4. 新增 `DeviceServerPool`，服务端必须走新入口。
5. 旧依赖编译期直接报错，强制迁移。

## 9. 风险与控制

1. 风险：一次性改名导致改动面大、编译红线多。
   对策：分支内批量替换 + 编译门禁 + 迁移清单逐项清零。
2. 风险：客户端/服务端恢复策略差异导致行为偏差。
   对策：恢复判定器按 `ResourceRole` 分流，默认策略独立配置。
3. 风险：服务端自动恢复触发端口冲突。
   对策：服务端池引入端口占用检测和指数退避。
4. 风险：测试覆盖不足导致隐性回归。
   对策：客户端回归套件必跑 + 服务端新增套件强制门禁。

## 10. 验收标准

1. 架构：核心层仅依赖泛型 `TResource`，无 `IDeviceClient` 硬编码。
2. 外层：`IDeviceClientPool` 与 `IDeviceServerPool` 完整可用。
3. 能力：客户端与服务端都可纳入统一池化模型。
4. 质量：全量编译通过，核心回归与新增服务端测试通过。

## 11. 实施检查清单

1. 定义 `ResourcePool*` 全套新接口并替换所有引用。
2. 重命名并泛型化 Entry/Executor/Pool 核心实现。
3. 落地 `DeviceClientPool` 与客户端工厂。
4. 落地 `DeviceServerPool`、服务端包装器与服务端工厂。
5. 升级描述模型，加入 `ResourceRole` 并改造路由。
6. 删除旧接口与旧实现，不保留兼容层。
7. 跑通编译、回归、服务端新增测试。
8. 更新架构文档、使用指南、迁移说明。

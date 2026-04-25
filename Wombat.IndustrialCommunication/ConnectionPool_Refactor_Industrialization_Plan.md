# ConnectionPool 简化与工业级改造计划

## 上下文
- 文件名：`ConnectionPool_Refactor_Industrialization_Plan.md`
- 创建时间：`2026-04-25`
- 创建者：AI
- 关联对象：`Wombat.IndustrialCommunication/ConnectionPool`
- 关联协议：`RIPER-5 + Multidimensional + Agent Protocol`

## 任务描述
围绕 `ConnectionPool` 完成两类改造规划：

1. 降低连接池对象状态与接口复杂度，减少不必要的概念数量和用户心智负担。
2. 输出工业级改造清单，优先补齐安全性、稳定性、可恢复性和可观测性短板。

## 项目概述
`Wombat.IndustrialCommunication` 是工业设备通信库，基于 `.NET Standard 2.0`，面向 Modbus TCP、Modbus RTU、Siemens S7、FINS 等协议的统一访问场景。`ConnectionPool` 当前已具备连接注册、租约管理、执行代理、后台维护、事件通知与快照观测能力，但在状态模型、默认接口易用性、异常隔离与工业级安全边界方面仍有进一步收敛和加固空间。

---
*以下部分由 AI 在协议执行过程中维护*
---

## 分析

### 当前结构
- `DeviceConnectionPool` 是池级入口，负责注册表、租约入口、统一执行、后台维护启动、快照输出和事件分发。
- `PooledConnectionEntry` 是单设备生命周期中心，负责状态机、租约集合、健康检查、故障恢复和条目移除。
- `PooledOperationExecutor` 负责统一重试与重连策略。
- `ConnectionPoolMaintenanceService` 负责租约过期扫描、健康检查和空闲回收。
- `BasePooledDeviceConnection` 与各协议包装类负责屏蔽底层 Modbus、S7、FINS 差异。

### 当前主要问题

#### 1. 状态模型复杂度偏高
- 对外公开状态目前包含 `Uninitialized`、`Connecting`、`Ready`、`Leased`、`Reconnecting`、`Faulted`、`Invalidated`、`Disposed`。
- 其中一部分状态是内部控制状态，不一定适合直接暴露给上层业务。
- 用户需要理解“可用性状态”“流程状态”“终态”三类概念，心智负担偏高。

#### 2. 接口默认使用门槛较高
- 公开接口同时暴露 `Acquire/Release`、`ForceReconnect`、`CleanupExpiredLeases` 等高级能力。
- 这些能力对专家用户有价值，但普通调用方通常只需要“注册后直接执行读写”。
- 当前 API 更偏“可控”，但不够“默认简单”。

#### 3. 工业级安全边界不足
- 写操作与读操作共用统一重试链路，缺少“写默认不重试”的安全边界。
- 事件是同步直调，且发生在状态锁路径内，订阅者异常或阻塞会影响主流程。
- 健康检查主要验证建连能力，缺少协议级探活。
- 故障恢复缺少冷却与风暴节流，`FaultedReconnectCooldown` 目前未真正落地。
- 失效未必伴随断链，底层资源和现场会话隔离不够彻底。

#### 4. 工业级测试覆盖不足
- 现有测试更偏功能正确性。
- 缺少重复写保护、事件订阅者异常隔离、维护高并发、慢探活、故障风暴等专项验证。

## 提议的解决方案

### 方案方向
采用“对外简化、对内保真、分阶段加固”的策略。

### 核心思路

#### A. 对外状态收敛
- 对外只保留 4 个稳定状态：
  - `Disconnected`
  - `Ready`
  - `Busy`
  - `Unavailable`
- 将现有细粒度状态转为内部生命周期状态，不再让上层直接感知所有中间过程。
- 将“正在连接 / 重连 / 故障 / 失效 / 释放”等细节更多通过事件和诊断字段表达，而不是继续膨胀公开状态枚举。

#### B. 对内状态保真
- 引入内部生命周期枚举，仅在 `PooledConnectionEntry` 和维护执行链路中使用。
- 内部仍保留足够细的恢复、移除、探活、故障判断能力。
- 通过状态映射函数把内部状态统一投影成简化后的公开状态。

#### C. 默认接口简化
- 新增一个更易用的简化接口，面向普通调用方。
- 普通用户使用“注册 + Execute/Read/Write + 查询快照”即可。
- 高级租约控制、强制重连、维护操作继续保留在现有高级接口中。

#### D. 工业级安全边界前置
- 写操作默认禁止自动重试。
- 事件发布增加异常隔离，并调整为锁外派发。
- 增加协议级探活接口 `ProbeAsync`。
- 恢复链路接入冷却与节流。
- 失效与断链策略统一。

### 目标状态
- 用户看到更少的状态、更少的入口、更清晰的默认行为。
- 内部实现具备工业现场需要的恢复、隔离、探活、节流与可观测能力。
- 先解决高风险问题，再逐步优化性能和扩展性。

## 实施计划

### 阶段 A：状态与接口降复杂度

#### 文件
- `ConnectionPool/Models/ConnectionEntryState.cs`
- `ConnectionPool/Core/PooledConnectionEntry.cs`
- `ConnectionPool/Models/ConnectionEntrySnapshot.cs`
- `ConnectionPool/Events/ConnectionPoolEventArgs.cs`
- `ConnectionPool/Events/ConnectionStateChangedEventArgs.cs`
- `ConnectionPool/Interfaces/IDeviceConnectionPoolExecution.cs`
- `ConnectionPool/Interfaces/IDeviceConnectionPool.cs`
- `ConnectionPool/Core/DeviceConnectionPool.cs`

#### 计划内容
- 将公开状态收敛为 4 态模型。
- 新增内部生命周期枚举，例如 `InternalConnectionLifecycleState`。
- `PooledConnectionEntry` 内部使用内部状态机，对外只投影公开状态。
- 快照和事件参数统一输出新 4 态模型。
- 新增 `ISimpleDeviceConnectionPool`，作为默认推荐入口。
- 保留现有高级接口，避免破坏现有调用方。

#### 理由
- 优先降低心智负担。
- 先把“对外复杂”问题压下去，再做工业级增强时更容易建立清晰边界。

### 阶段 B：工业级 P0 改造

#### 文件
- `ConnectionPool/Core/PooledOperationExecutor.cs`
- `ConnectionPool/Core/DeviceConnectionPool.cs`
- `ConnectionPool/Core/ConnectionPoolEventDispatcher.cs`
- `ConnectionPool/Core/PooledConnectionEntry.cs`
- `ConnectionPool/Interfaces/IPooledDeviceConnection.cs`
- `ConnectionPool/Wrappers/BasePooledDeviceConnection.cs`
- `ConnectionPool/Wrappers/ModbusTcpPooledConnection.cs`
- `ConnectionPool/Wrappers/ModbusRtuPooledConnection.cs`
- `ConnectionPool/Wrappers/SiemensPooledConnection.cs`
- `ConnectionPool/Wrappers/FinsPooledConnection.cs`
- `ConnectionPool/Core/ConnectionPoolMaintenanceService.cs`
- `ConnectionPool/Models/ConnectionPoolOptions.cs`
- `ConnectionPool/Core/PointListOperationHelper.cs`

#### 计划内容
- 增加执行分类与执行选项模型，例如：
  - `ConnectionExecutionKind`
  - `ConnectionExecutionOptions`
- 统一重试器改为基于执行类型分流：
  - 读默认可重试
  - 写默认不可重试
  - 诊断类默认不可重试
- 事件分发逐订阅者隔离异常，并从锁内同步派发调整为锁外派发。
- 为池化连接抽象增加 `ProbeAsync`。
- 各协议连接包装类提供轻量探活实现。
- 维护链路使用协议级探活替代单纯“EnsureConnected 即健康”的判断。
- 恢复链路接入 `FaultedReconnectCooldown` 与维护并发限制。
- `Invalidate` 改为“标记失效 + 最佳努力断链”。

#### 理由
- 这些问题直接决定是否具备工业级安全边界。
- 不解决这些问题，不应宣称连接池达到工业级。

### 阶段 C：测试与验证补齐

#### 文件
- `Wombat.IndustrialCommunicationTestProject/ConnectionPoolTests/DeviceConnectionPoolTests.cs`
- `Wombat.IndustrialCommunicationTestProject/ConnectionPoolTests/ConnectionPoolMaintenanceTests.cs`
- `Wombat.IndustrialCommunicationTestProject/ConnectionPoolTests/ConnectionPoolEventsTests.cs`
- 新增 `Wombat.IndustrialCommunicationTestProject/ConnectionPoolTests/ConnectionPoolSafetyTests.cs`

#### 计划内容
- 验证公开状态只剩 4 态。
- 验证写操作默认不重试。
- 验证读操作在可恢复故障下可重试。
- 验证事件订阅者抛异常不会影响主流程。
- 验证事件回调阻塞不会导致连接池死锁。
- 验证 `ProbeAsync` 被维护流程优先使用。
- 验证恢复冷却和维护并发限制生效。
- 验证失效后底层连接被断开。

#### 理由
- 工业级不是靠设计说明成立，而是靠“故障条件下仍然符合预期”的测试证明成立。

### 阶段 D：文档与使用方式更新

#### 文件
- `ConnectionPool.README.md`
- 本计划文档

#### 计划内容
- 将使用文档改为“默认简化接口优先，高级接口按需使用”。
- 更新状态说明、重试说明、事件说明、健康检查说明。
- 明确写操作默认不自动重试。

#### 理由
- 降复杂度不仅是代码问题，也是用户理解和使用方式问题。

实施检查清单：
1. 新建 `ConnectionPool_Refactor_Industrialization_Plan.md` 作为本次改造总计划文档。
2. 在 `ConnectionPool/Models/ConnectionEntryState.cs` 收敛公开状态为 4 态。
3. 在 `ConnectionPool/Core` 新增内部生命周期枚举文件，承接细粒度内部状态。
4. 重构 `PooledConnectionEntry`，分离“内部状态机”和“外部状态投影”。
5. 更新 `ConnectionEntrySnapshot` 与事件参数模型，使公开可观测面统一到 4 态。
6. 新增简化接口 `ISimpleDeviceConnectionPool`，并在 `DeviceConnectionPool` 中实现。
7. 在执行接口中引入 `ConnectionExecutionOptions`，建立显式执行策略模型。
8. 重构 `PooledOperationExecutor`，实现读写分流的重试策略。
9. 重构 `DeviceConnectionPool` 的 `ReadPointsAsync`、`WritePointsAsync`、`ExecuteAsync` 入口，接入默认执行策略。
10. 重构 `ConnectionPoolEventDispatcher`，实现逐订阅者异常隔离。
11. 重构 `PooledConnectionEntry` 事件发布路径，改为锁外派发。
12. 扩展 `IPooledDeviceConnection`，增加 `ProbeAsync`。
13. 在 `BasePooledDeviceConnection` 中提供默认 `ProbeAsync` 和“失效即断链”实现。
14. 在各协议包装类中实现轻量探活逻辑。
15. 重构 `ConnectionPoolMaintenanceService`，引入探活优先、恢复冷却和有界并发维护。
16. 更新 `ConnectionPoolOptions`，补齐事件隔离、探活超时、维护并发等配置项。
17. 更新测试，覆盖状态收敛、重复写保护、事件隔离、探活和恢复节流。
18. 更新 `ConnectionPool.README.md`，同步新的默认用法和工业级边界说明。

## 当前执行步骤
> 正在执行: “步骤 1 - 新建 ConnectionPool 简化与工业级改造总计划文档”

## 任务进度
*   [2026-04-25]
    *   步骤：1. 新建 `ConnectionPool_Refactor_Industrialization_Plan.md` 作为本次改造总计划文档
    *   修改：新增计划文档，整理当前结构、问题、方案方向、阶段划分与实施检查清单
    *   更改摘要：完成计划底稿落地，可作为后续执行和审查的统一依据
    *   原因：执行计划步骤 1
    *   阻碍：无
    *   用户确认状态：待确认

## 最终审查
- 当前文档已覆盖本轮规划目标：
  - 状态复杂度收敛方向
  - 简化接口方向
  - 工业级 P0 改造项
  - 测试与文档更新要求
- 当前阶段仅生成计划文档，尚未实施代码改造。

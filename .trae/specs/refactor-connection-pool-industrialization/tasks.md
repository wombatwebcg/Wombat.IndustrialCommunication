# Tasks

- [x] Task 1: 收敛公开状态模型与默认入口
  - [x] 更新 `ConnectionEntryState` 为四态公开模型，并新增内部生命周期枚举承接细粒度状态。
  - [x] 重构 `PooledConnectionEntry` 的状态投影逻辑，使内部状态机与外部公开状态解耦。
  - [x] 更新快照、事件参数和 `DeviceConnectionPool` 的对外观测面，确保只暴露四态状态。
  - [x] 新增 `ISimpleDeviceConnectionPool` 并在 `DeviceConnectionPool` 中实现默认简化入口。

- [x] Task 2: 引入执行策略并重构执行链路
  - [x] 新增执行分类与执行选项模型，覆盖读、写、诊断三类操作。
  - [x] 重构 `PooledOperationExecutor`，实现读可重试、写默认不可重试、诊断默认不可重试。
  - [x] 调整 `DeviceConnectionPool` 的 `ExecuteAsync`、`ReadPointsAsync`、`WritePointsAsync` 入口接入默认策略。
  - [x] 评估并更新 `PointListOperationHelper` 以匹配新的执行策略边界。

- [x] Task 3: 加固事件隔离、探活和恢复机制
  - [x] 重构 `ConnectionPoolEventDispatcher` 与 `PooledConnectionEntry` 事件发布路径，实现锁外派发和逐订阅者异常隔离。
  - [x] 扩展 `IPooledDeviceConnection`、`BasePooledDeviceConnection` 以及各协议包装类，增加 `ProbeAsync` 和失效即断链行为。
  - [x] 重构 `ConnectionPoolMaintenanceService`，让维护流程优先使用 `ProbeAsync`，并引入恢复冷却与有界并发。
  - [x] 更新 `ConnectionPoolOptions`，补齐事件隔离、探活超时和维护并发相关配置项。

- [x] Task 4: 补齐测试与文档
  - [x] 更新连接池测试，覆盖四态状态、读写执行策略、事件异常隔离和锁外派发。
  - [x] 新增安全性测试，覆盖探活优先、恢复冷却、维护并发限制和失效断链。
  - [x] 更新 `ConnectionPool.README.md`，说明新的默认接口、状态模型和工业级安全边界。
  - [x] 执行相关测试与诊断检查，确认改造结果满足规格。

# Task Dependencies

- `Task 2` depends on `Task 1`
- `Task 3` depends on `Task 2`
- `Task 4` depends on `Task 1`, `Task 2`, and `Task 3`

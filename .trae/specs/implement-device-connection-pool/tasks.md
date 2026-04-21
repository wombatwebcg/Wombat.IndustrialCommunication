# Tasks
- [x] Task 1: 建立连接池基础模型与接口，明确设备标识、池配置、连接条目状态与租约语义。
  - [x] SubTask 1.1: 新增 `ConnectionIdentity`、`DeviceConnectionDescriptor`、`ConnectionPoolOptions` 模型。
  - [x] SubTask 1.2: 新增 `IPooledDeviceConnection`、`IPooledDeviceConnectionFactory`、`IDeviceConnectionPool` 接口。
  - [x] SubTask 1.3: 定义连接条目状态机与租约生命周期约束（获取、释放、失效）。

- [ ] Task 2: 实现连接池核心流程，支持注册、获取、执行、释放、失效和空闲回收。
  - [ ] SubTask 2.1: 实现 `PooledConnectionEntry` 并发保护、引用计数、失败计数和活跃时间。
  - [ ] SubTask 2.2: 实现 `DeviceConnectionPool` 的注册、获取、释放、失效、销毁能力。
  - [ ] SubTask 2.3: 实现空闲回收与容量控制策略，确保无租约条目可回收。

- [ ] Task 3: 实现协议包装与工厂映射，接入 Modbus TCP/RTU、S7、FINS 客户端。
  - [ ] SubTask 3.1: 实现默认工厂，支持通过描述符创建对应协议包装连接。
  - [ ] SubTask 3.2: 实现各协议 `PooledConnection` 包装，统一 `EnsureConnected/Execute` 行为。
  - [ ] SubTask 3.3: 在池模式下约束连接策略入口，避免协议间行为不一致。

- [ ] Task 4: 实现池化读写执行器与容错策略，统一重试、重连与故障分类。
  - [ ] SubTask 4.1: 新增执行器封装读写前连接检查与执行链路。
  - [ ] SubTask 4.2: 实现可恢复与不可恢复错误分类。
  - [ ] SubTask 4.3: 实现有限次重试、退避重连与失败后失效标记。

- [ ] Task 5: 完成验证与文档，确保能力可回归、可使用。
  - [ ] SubTask 5.1: 新增连接池单元测试（复用、并发隔离、回收、失效）。
  - [ ] SubTask 5.2: 新增协议集成测试（至少 Modbus TCP 与 S7 通过池执行读写）。
  - [ ] SubTask 5.3: 补充使用说明（注册设备、执行读写、策略配置）。

# Task Dependencies
- Task 2 depends on Task 1
- Task 3 depends on Task 1
- Task 4 depends on Task 2, Task 3
- Task 5 depends on Task 4

# Parallelizable Work
- Task 3 可在 Task 2 完成基础连接条目后并行推进包装层实现。
- Task 5.1 可与 Task 5.3 并行执行；Task 5.2 在 Task 4 完成后执行。

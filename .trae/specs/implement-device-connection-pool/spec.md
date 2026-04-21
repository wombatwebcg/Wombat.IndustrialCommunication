# 设备连接池与通用连接动作 Spec

## Why
当前库在 Modbus/S7/FINS 各客户端中分别维护连接、重连与长短连接逻辑，存在重复实现与行为不一致问题。需要提供一套稳定的连接池能力，统一维护实际应用中的多设备连接与读写执行路径。

## What Changes
- 新增连接池能力：按设备唯一标识维护客户端连接实例，并提供获取、释放、失效、空闲回收。
- 新增通用设备连接动作抽象：统一 `Connect/Disconnect/EnsureConnected/Execute` 行为。
- 新增连接池执行入口：在池层统一封装客户端读写调用、重试、重连与故障分类。
- 新增协议包装层：对 Modbus TCP、Modbus RTU、S7、FINS 提供统一池化包装接口。
- 新增连接池配置模型：最大连接数、空闲回收时间、重连退避、租约超时、健康检查周期。
- 保持现有 `IDeviceClient` 直连用法可继续使用，不强制迁移。

## Impact
- Affected specs: 设备连接管理、客户端读写执行路径、连接可靠性保障。
- Affected code: `Wombat.IndustrialCommunication` 下新增 `ConnectionPool` 目录及相关接口/实现；适配现有 Modbus/S7/FINS 客户端包装层。

## ADDED Requirements
### Requirement: 连接池统一管理多设备连接
系统 SHALL 通过设备唯一标识在进程内维护可复用连接，并支持不同连接类型与协议客户端。

#### Scenario: 按设备标识复用连接
- **WHEN** 调用方以同一设备标识连续申请连接
- **THEN** 连接池返回同一活跃连接实例（未失效情况下）

#### Scenario: 跨设备并发隔离
- **WHEN** 调用方同时对不同设备发起读写
- **THEN** 连接池允许并发执行且互不阻塞

### Requirement: 通用连接动作抽象
系统 SHALL 提供统一连接动作接口以封装具体协议客户端差异。

#### Scenario: 通用连接建立
- **WHEN** 连接池创建新的设备连接条目
- **THEN** 通过统一接口执行连接建立并返回标准 `OperationResult`

#### Scenario: 通用连接保活检查
- **WHEN** 执行读写前检测连接状态不可用
- **THEN** 统一触发 `EnsureConnected` 流程并依据策略尝试恢复

### Requirement: 池化读写执行与容错
系统 SHALL 在连接池层统一执行客户端读写动作，并支持重试、重连和失败分类处理。

#### Scenario: 读写前自动确保连接
- **WHEN** 调用方通过连接池执行读写委托
- **THEN** 连接池先确保连接可用，再执行委托

#### Scenario: 短暂故障恢复
- **WHEN** 出现可恢复连接故障（如超时/连接中断）
- **THEN** 连接池按配置执行有限次重试与退避重连

#### Scenario: 不可恢复错误快速失败
- **WHEN** 出现不可恢复协议错误或超过重试阈值
- **THEN** 返回失败结果并将连接条目标记为失效或故障态

### Requirement: 生命周期与资源回收
系统 SHALL 管理连接租约与空闲回收，避免连接泄漏与无限增长。

#### Scenario: 租约释放
- **WHEN** 调用方完成操作并释放连接租约
- **THEN** 连接条目引用计数减少并更新最后活跃时间

#### Scenario: 空闲连接回收
- **WHEN** 连接超过空闲阈值且无活跃租约
- **THEN** 连接池关闭并回收对应连接资源

## MODIFIED Requirements
### Requirement: 客户端连接策略一致性
现有协议客户端在连接池模式下应以池层策略为主，客户端内部连接逻辑作为底层能力被统一编排。

## REMOVED Requirements
### Requirement: 无
**Reason**: 本次为增量能力建设，不移除现有公开能力。
**Migration**: 无需迁移；现有调用方式继续有效。

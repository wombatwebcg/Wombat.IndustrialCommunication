# 通讯库 ForceClose 三阶段彻底修复计划

## 1. 背景

当前 `PlatformX.Application` 在禁用客户端通道时，会同步调用底层连接池的 `ForceCloseAsync` 和 `Unregister`。从现网日志看，真正的耗时不在注销，而在 `ForceCloseAsync`。

现有底层库 `Wombat.IndustrialCommunication\ConnectionPool` 的 `ForceClose` 行为与名称语义不一致：

- 名称语义看起来是“强制关闭连接”
- 实际实现却是“请求取消活跃执行，然后等待活跃 I/O 自然退出，再完成关闭”

这导致：

- 前端禁用通道时会等待 5 到 8 秒甚至更久
- `S7`、`Modbus TCP`、`Modbus RTU` 都会出现同类问题
- 行为取决于当前正在执行的读写是否能及时结束，而不是取决于“是否已经强制断链”

本次目标不是做表层规避，而是彻底修正通讯库底层语义与执行模型。

## 2. 问题归因

### 2.1 当前 ForceClose 的错误语义

`PooledResourceEntry.ForceCloseAsync(...)` 目前的核心路径是：

1. 标记 `ForceClosing`
2. 取消活跃执行 token
3. 调用 `Connection.DisconnectOrShutdown()`
4. 等待 `WaitForDrainAsync(...)`
5. 活跃执行数归零后才真正完成 ForceClose

这实际上是 `CancelThenDrainThenCloseResultFinalize`，不是 `ForceClose`。

### 2.2 活跃执行无法被真正中断

连接池层虽然创建了 linked token，但设备客户端接口和协议实现没有统一支持取消透传：

- `PointListOperationHelper` 发起点位读写时没有把取消 token 传到底层设备 API
- `IDeviceClient` 现有读写接口没有统一 `CancellationToken`
- `SiemensClient`、`ModbusTcpClient`、`ModbusRtuClient` 的读写和批量读写都不是可取消接口

结果是：

- `ForceClose` 发出取消，只能改变上层状态
- 底层 I/O 仍会继续阻塞到网络/串口超时
- `PooledResourceEntry` 只能一直等活跃执行计数回落

### 2.3 协议无关，是共享架构问题

不是某一个协议实现坏了，而是共享边界设计有问题：

- `ConnectionPool` 的关闭语义设计错误
- `IPooledResourceConnection` / `IDeviceClient` 的取消模型不完整
- 协议实现没有统一接入可取消 I/O

因此这个问题必须按“连接池核心 + 客户端接口 + 协议实现”一起修。

## 3. 修复目标

### 3.1 语义目标

- `ForceClose` 的定义统一为：强制终止底层连接，并尽快向调用方返回
- `ForceClose` 不再等待活跃 I/O 自然完成
- 活跃 I/O 在底层连接被切断后，应尽快以“连接已被强制关闭”或“操作已取消”失败返回

### 3.2 架构目标

- 建立一条从连接池到协议 I/O 的完整取消链路
- 让 `S7`、`Modbus TCP`、`Modbus RTU` 的读写模型统一支持取消
- 让连接池关闭路径与执行路径解耦，不再互相阻塞

### 3.3 结果目标

- 禁用客户端通道时，耗时主要由“断链动作”决定，而不是由“当前采集周期自然超时”决定
- 强制关闭后，不再出现大量长时间挂起的执行尾部
- 关闭、禁用、删除、重启通道的行为与名称语义一致

## 4. 不做事项

- 不做只在应用层绕过等待的临时修补
- 不保留当前 `ForceClose = Cancel + Drain` 的旧语义
- 不只修 `S7` 而忽略 `Modbus`
- 不接受“前端先返回，后台慢慢关”的掩盖式方案

## 5. 总体方案

本次按三个阶段实施。

### 阶段一原则

先修正 `ForceClose` 的返回语义，让“强制关闭连接后尽快返回”真正成立。

### 阶段二原则

再收敛活跃执行尾部、状态机和事件语义，确保晚到执行不会破坏强关终态。

### 阶段三原则

最后把取消能力完整下沉到协议 I/O，减少强制关闭后的尾部残留和异常噪音。

## 6. 阶段一：ForceClose 返回语义纠偏

### 6.1 阶段目标

- `ForceCloseAsync` 不再等待活跃执行 drain 完成
- 连接一旦被强制关闭，调用方能立即获得关闭结果
- 阶段一不改公共设备接口，不做协议层取消透传

### 6.2 改造范围

- `Wombat.IndustrialCommunication\ConnectionPool\Core\PooledResourceEntry.cs`
- `Wombat.IndustrialCommunication\ConnectionPool\Core\ResourcePool.cs`
- `Wombat.IndustrialCommunication\ConnectionPool\Core\ConnectionControlScheduler.cs`
- `Wombat.IndustrialCommunication\ConnectionPool\Wrappers\BasePooledResourceConnection.cs`

### 6.3 关键设计

#### 6.3.1 ForceClose 改为立即完成型关闭

`PooledResourceEntry.ForceCloseAsync(...)` 调整为：

1. 进入 `ForceClosing`
2. 取消活跃执行 token
3. 立即执行 `DisconnectOrShutdown()`
4. 立即释放 lease
5. 立即切换到 `ForceClosed/Faulted/Unavailable` 的终态
6. 立即返回

删除当前“关闭前等待 drain 完成”的同步阻塞。

#### 6.3.2 不扩大接口改造

阶段一只改连接池关闭路径，不修改 `IDeviceClient`、`IReadWrite`、`SiemensClient`、`ModbusTcpClient`、`ModbusRtuClient` 的公共读写签名。

已经在跑的执行可以晚一点失败返回，但不能再阻塞 `ForceClose`。

#### 6.3.3 保持最小状态改动

如果继续沿用当前 `Faulted` 作为强制关闭后的主状态，至少保留“由 ForceClose 触发”的明确标识，避免和普通故障混淆。阶段一不强制新增 `ForceClosed` 生命周期枚举，除非现有状态无法表达验收标准。

### 6.4 执行步骤

1. 重写 `PooledResourceEntry.ForceCloseAsync(...)`，移除 `WaitForDrainAsync(...)` 作为返回前置。
2. 在 `DisconnectOrShutdown()` 后立即释放租约并写入强关终态。
3. 确认 `ResourcePool.Unregister(...)` 在 ForceClose 后仍能继续移除条目。
4. 保留现有强关事件，必要时补充日志区分“发起强制关闭”和“底层断链完成”。

### 6.5 验收标准

- 禁用通道时，`ForceClose` 耗时显著下降，不再受单次读写超时主导
- `Unregister` 在 ForceClose 后仍可稳定执行
- 关闭后新租约被拒绝
- 活跃执行即使未返回，也不阻塞关闭返回

## 7. 阶段二：执行尾部和状态收敛

### 7.1 阶段目标

- 活跃执行改为“异步尾部失败收敛”
- 晚到执行不能覆盖 ForceClose 终态
- 释放、移除、释放资源路径在 ForceClose 后保持幂等

### 7.2 改造范围

- `Wombat.IndustrialCommunication\ConnectionPool\Core\PooledResourceEntry.cs`
- `Wombat.IndustrialCommunication\ConnectionPool\Core\ResourcePool.cs`
- 相关状态模型和事件派发类型

### 7.3 关键设计

#### 7.3.1 活跃执行尾部自收敛

已经在跑的执行可以晚一点失败返回，但只能做尾部清理，不能再影响关闭流程。

执行尾部需要保证：

- 不重复释放
- 不重复回写 Ready/Faulted
- 不把 `ForceClosed` 状态覆盖掉
- 不因为晚到的执行结果破坏连接条目终态

#### 7.3.2 引入更清晰的终态约束

需要明确关闭后的状态机边界：

- `ForceClosing`：关闭动作已发起
- `ForceClosed` 或等价终态：连接已被强制关闭，条目不可继续执行业务 I/O
- `Unregister`：资源池条目生命周期结束

如果继续沿用当前 `Faulted` 作为强制关闭后的主状态，至少要补充“由 ForceClose 触发”的明确标识，避免和普通故障混淆。

#### 7.3.3 统一取消后返回语义

所有被强制关闭中断的执行，优先返回统一错误语义：

- `连接已被强制关闭`
- 或 `操作已取消（由 ForceClose 触发）`

不要再让调用方主要看到普通超时消息，否则会混淆“设备超时”和“人为关闭”。

### 7.4 执行步骤

1. 审查 `ExecuteAsync(...)` 收尾逻辑，确保晚到执行不会覆盖关闭终态。
2. 审查 `ReleaseAsync(...)`、`PrepareForRemovalAsync(...)`、`DisposeAsync(...)` 在 ForceClose 后的幂等行为。
3. 审查 `PrepareForRemovalAsync(...)` 与 ForceClose 后状态的兼容性。
4. 增加连接池事件和日志，明确区分：
   - 发起强制关闭
   - 底层断链完成
   - 活跃执行尾部清退完成

### 7.5 验收标准

- 无死锁、无悬空 lease、无执行计数永远不归零
- 旧执行只做尾部失败，不阻塞关闭返回
- 晚到成功结果不会把强关条目恢复成 Ready
- 日志能区分强关完成和尾部清退完成

## 8. 阶段三：取消链路全量下沉

### 8.1 阶段目标

- 从连接池到设备协议 I/O 建立统一 `CancellationToken` 透传
- 除了“断链硬中断”，还要支持“正在执行的 I/O 主动响应取消”
- 降低关闭后的尾部等待和异常噪音

### 8.2 改造范围

- `Wombat.IndustrialCommunication\IDeviceClient.cs`
- `Wombat.IndustrialCommunication\IClient.cs`
- `Wombat.IndustrialCommunication\ConnectionPool\Core\PointListOperationHelper.cs`
- `Wombat.IndustrialCommunication\ConnectionPool\Interfaces\IPooledResourceConnection.cs`
- `Wombat.IndustrialCommunication\ConnectionPool\Wrappers\BasePooledDeviceClientConnection.cs`
- `Wombat.IndustrialCommunication\PLC\S7\SiemensClient.cs`
- `Wombat.IndustrialCommunication\Modbus\ModbusTcpClient.cs`
- `Wombat.IndustrialCommunication\Modbus\ModbusRtuClient.cs`
- 相关基类、消息传输层、串口/TCP 适配器

### 8.3 关键设计

#### 8.3.1 设备客户端接口支持取消

为读写接口补充 `CancellationToken` 版本，至少覆盖：

- 单点读
- 单点写
- 批量读
- 批量写
- 必要的内部协议收发入口

要求：

- 新接口为主路径
- 旧接口保留兼容期，但逐步收敛为包装器

#### 8.3.2 PointListOperationHelper 透传取消

当前 helper 层接收了取消 token，但没有把它传到底层设备 API。阶段三要修成真正透传。

#### 8.3.3 协议层读写支持取消

`S7`、`Modbus TCP`、`Modbus RTU` 都要支持把取消 token 继续传到：

- 协议封包前后的等待
- 网络收发
- 串口收发
- 批量读写中的块级循环

#### 8.3.4 批量读写中的取消粒度统一

批量操作必须支持在块与块之间、点与点之间尽快停止，而不是把整批做完才响应取消。

#### 8.3.5 错误语义去歧义

取消触发时：

- 优先返回 cancelled / force-closed
- 不混用普通 timeout
- 保留原始异常信息作为附加诊断，不作为主业务消息

### 8.4 执行步骤

1. 梳理 `IDeviceClient` 现有读写接口，设计兼容扩展方案。
2. 改造 `IPooledResourceConnection.ExecuteAsync(...)` 及包装器，使执行 token 能透传。
3. 改造 `PointListOperationHelper`，统一调用可取消接口。
4. 逐个协议实现取消版读写：
   - `SiemensClient`
   - `ModbusTcpClient`
   - `ModbusRtuClient`
5. 审查 TCP/Serial 适配器，确保 token 最终能进入真正的阻塞 I/O。
6. 清理旧的不支持取消的中间包装逻辑。

### 8.5 验收标准

- `ForceClose` 后，活跃中的 S7/Modbus 读写能更快失败返回
- 批量读写在取消后不会继续执行整批
- 错误日志能区分：主动取消、强制关闭、普通超时、协议失败
- 协议行为一致，不再出现只有某个协议好用的情况

## 9. 风险与对策

### 风险 1：状态机改造引入并发回写问题

对策：

- 明确 ForceClose 后的状态优先级
- 所有执行尾部回写都要检查“是否已进入强制关闭终态”
- 关键路径补并发日志和单元测试

### 风险 2：接口改造影响面大

对策：

- 阶段一先不改公共设备接口，先修 ForceClose 语义
- 阶段二只收敛连接池内部尾部和状态
- 阶段三再做接口升级，并保留兼容包装层

### 风险 3：串口/TCP 某些底层实现对取消支持不完全

对策：

- 阶段一已经保证“断链优先返回”
- 阶段三尽量让取消下沉，不能取消的地方至少靠断链快速失败

### 风险 4：调用方依赖旧错误消息

对策：

- 统一错误语义时保留详细原始错误到诊断字段
- 对应用层只暴露稳定主消息

## 10. 回滚策略

- 三个阶段分开提交、分开验收
- 阶段一只改连接池关闭语义，不改协议接口签名
- 阶段二只改连接池尾部收敛和状态保护
- 阶段三再升级设备客户端接口和协议实现
- 任一阶段出问题都可以独立回滚，不强绑其他阶段

## 11. 最终预期

修完后，`ForceClose` 的行为应满足下面这条原则：

> 一旦调用 `ForceClose`，底层连接必须被立即强制终止，调用方不需要等待正在通讯的 I/O 自然结束。

正在执行的通讯 I/O 可以在关闭后异步失败返回，但不能反过来阻塞关闭动作本身。这才符合 `ForceClose` 的语义，也符合通道禁用、删除、重启等管理动作的预期。

## 12. 三阶段完成情况检查

检查日期：2026-07-07。

### 12.1 阶段一完成情况：已完成

- `PooledResourceEntry.ForceCloseAsync(...)` 已移除关闭返回前的 drain 等待，不再以活跃执行自然结束作为返回前置。
- `ForceCloseAsync(...)` 当前流程为：标记 `ForceClosing`、发布强关取消事件、取消活跃执行 token、调用 `Connection.DisconnectOrShutdown()`、释放全部租约、写入强关终态并返回。
- 强关后通过 `_closedByForceClose` 阻止新租约，`AcquireAsync(...)` 会拒绝已强关条目。
- 强关终态目前仍复用 `Faulted` 生命周期状态，并用 `_closedByForceClose` 和 `ConnectionPoolMaintenanceMode.ForceClose` 区分普通故障。

### 12.2 阶段二完成情况：已完成

- `ExecuteAsync(...)` 已使用外部 token 与条目活跃执行 token 创建 linked token，`ForceClose` 取消后执行路径可收到取消信号。
- 执行收尾已区分强关尾部：强关、移除、终态场景只做执行计数和快照收敛，不再恢复 `Ready/Leased`。
- 活跃执行归零时会发布 `ForceCloseDrained`，与 `ForceCloseRequested`、`ForceCloseCancelling`、`ForceClosed` 区分开。
- `ReleaseAsync(...)`、`PrepareForRemovalAsync(...)`、`DisposeAsync(...)` 在强关后保持幂等，强关后重复释放租约不会破坏终态。
- 被强关取消的执行会归一为 `连接已被强制关闭，读取已终止`，普通取消为 `操作已取消`。

### 12.3 阶段三完成情况：已完成，已改为统一接口模型

- `IReadWrite` 异步主读写入口已统一补充 `CancellationToken` 默认参数，覆盖批量读、单点读、字符串读、批量写、单点写。
- `IDeviceClient` 已回到只继承 `IClient` 与 `IReadWrite`，不再单独声明一套可取消读写兼容接口。
- `PointListOperationHelper` 已把连接池执行 token 传入统一的 `IReadWrite` 可取消接口。
- `DeviceMessageTransport` 和 `IDeviceMessageTransport` 已补充可取消的 `SendRequestAsync`、`ReceiveResponseAsync`、`UnicastReadMessageAsync`、`UnicastWriteMessageAsync`。
- `S7EthernetTransport` 已把 token 传入 S7 专用收发和完整响应读取流程。
- `SiemensClient`、`S7Communication` 已在单点读写、块读、随机批量读写、批量写循环和批间 delay 中响应取消。
- `ModbusTcpClient`、`ModbusTcpClientBase` 已在单点读写、分段读、批量读写、站号间 delay 和底层 Modbus 收发中响应取消。
- `ModbusRtuClient`、`ModbusRtuClientBase` 已在单点读写、批量读写、站号间 delay 和底层 Modbus 收发中响应取消。
- `ModbusRtuBluetoothClient` 已跟随统一接口签名调整，并在批量循环中响应取消。
- TCP/Serial 适配器已有 `CancellationToken` 收发接口，当前 token 已能从连接池下沉到实际阻塞 I/O。
- `IPooledResourceConnection` 和 `IClient` 未扩大公共接口；当前通过 `ResourcePool/PooledResourceEntry` 的执行委托和统一 `IReadWrite` 可取消入口完成透传。

### 12.4 测试代码和验证状态

- 已补充连接池强关相关测试代码：`Wombat.IndustrialCommunicationTestProject\ConnectionPoolTests\DeviceConnectionPoolTests.cs`。
- 已补充取消 token 透传测试代码：`Wombat.IndustrialCommunicationTestProject\ConnectionPoolTests\DeviceClientCancellationTokenTests.cs`。
- 已执行 `dotnet build Wombat.IndustrialCommunication.sln --no-restore`，构建通过。
- 按要求未执行测试用例。

### 12.5 剩余注意事项

- 当前强关终态仍使用 `Faulted + _closedByForceClose` 表达，没有新增独立 `ForceClosed` 生命周期枚举。
- `IReadWrite` 已采用统一可取消异步签名，服务端实现同步完成签名对齐；服务端内部读写没有额外引入新的取消执行模型。

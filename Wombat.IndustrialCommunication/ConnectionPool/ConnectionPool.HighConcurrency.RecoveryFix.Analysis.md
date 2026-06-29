# ConnectionPool 高并发修复分析

## 背景

目标测试：

`Wombat.IndustrialCommunicationTest.ConnectionPoolTests.ConnectionPoolS7HighConcurrencyStabilityTests.ConnectionPool_Should_Remain_Stable_With_100_S7_Servers_And_Random_Disconnects`

本次修复前，核心失败形态已经收敛到：

- S7 连接恢复阶段频繁报 `连接等待超时: 500ms`
- 同一连接条目在 `Faulted/Reconnecting` 间反复切换
- 后续恢复仍复用原来的 `SiemensClient` 实例

这说明问题不再是“消息链丢失”或“恢复线程阻塞”，而是“旧连接实例本身已经不可恢复，但恢复逻辑还在继续用它”。

## 根因判断

共享根因是：

1. 某次超时或异常后，旧 `SiemensClient` 可能仍卡在其内部锁或底层读写状态里。
2. 连接池恢复时继续对同一个实例做 `DisconnectOrShutdown()` / `EnsureAvailableAsync()`。
3. 即使前台快速失败，后台恢复仍在旧实例上空转。
4. 结果就是少数条目持续恢复失败，最终拖垮整轮压测。

简化表达：

`恢复失败` 不是因为“不会重试”，而是因为“重试对象错了”。

## 本次修复

### 1. 恢复时不再复用旧连接实例

在 [PooledResourceEntry.cs](D:/Wombat/Wombat.IndustrialCommunication/Wombat.IndustrialCommunication/Wombat.IndustrialCommunication/ConnectionPool/Core/PooledResourceEntry.cs:392) 中，`TryRecoverAsync(...)` 从“对当前 `Connection` 做断开再重连”，改成：

1. 先记录当前旧连接 `previousConnection`
2. 通过工厂重新创建一个新的 `IPooledResourceConnection<TResource>`
3. 在条目锁内把 `Connection` 切换到新实例
4. 用新实例执行 `EnsureAvailableAsync()`
5. 旧实例仅做后台 `SafeDisconnect(...)`，不阻塞恢复主流程

这一步直接绕过了“旧 `SiemensClient` 内部状态已坏但还被复用”的问题。

### 2. 给条目注入重建连接的最小能力

在 [ResourcePool.cs](D:/Wombat/Wombat.IndustrialCommunication/Wombat.IndustrialCommunication/Wombat.IndustrialCommunication/ConnectionPool/Core/ResourcePool.cs:120) 中，注册条目时把工厂能力传进 `PooledResourceEntry`：

```csharp
descriptorForRecovery => _factory.Create(descriptorForRecovery)
```

这样恢复逻辑复用现有工厂，不新增新的恢复工厂、策略接口或额外抽象。

这符合最短修复路径：

- 不改 `SiemensClient` 构造方式
- 不改 `ResourceDescriptor` 结构
- 不新增恢复专用层

### 3. 保留兼容构造函数

在 [PooledResourceEntry.cs](D:/Wombat/Wombat.IndustrialCommunication/Wombat.IndustrialCommunication/Wombat.IndustrialCommunication/ConnectionPool/Core/PooledResourceEntry.cs:67) 保留了旧构造函数。

作用很直接：

- 不用改现有测试里手工 `new PooledResourceEntry(...)` 的地方
- 默认退化为“始终返回当前 connection”的工厂

这只是编译兼容层，不影响生产恢复路径。

## 为什么这次修复有效

修复后的恢复链路，关键变化不是“更努力地连”，而是“换对象再连”。

旧路径：

- `Faulted`
- 继续对旧 `Connection` 恢复
- 旧 `SiemensClient` 卡住
- 多次恢复都打在同一个坏实例上

新路径：

- `Faulted`
- 按原 `Descriptor` 新建 `Connection`
- 当前条目切到新实例
- 新实例单独建连

这等价于把“无法清理的旧内部状态”整体丢弃。

对 S7 这种存在底层 socket/协议状态残留风险的客户端，这个修法比“继续在原实例上做更多超时控制”更接近根因。

## 风险与边界

### 风险 1

旧连接的后台 `SafeDisconnect(...)` 是 best-effort。

影响：

- 如果旧实例内部已经卡死，它可能最终只能靠进程生命周期回收
- 但这比继续阻塞当前恢复链路要好得多

### 风险 2

`Connection` 在恢复时会被切换。

当前之所以可接受，是因为执行路径里已经用条目状态版本和租约控制避免并发误用，且恢复本身只在故障路径发生。

### 风险 3

这个修复针对的是“实例污染后不可恢复”的根因，不是全局消灭所有超时。

如果后续还有失败，更可能是：

- 真实网络抖动超过当前超时预算
- 某个底层协议栈调用完全不响应

那就该继续修底层客户端，而不是再往连接池上加一层复杂恢复逻辑。

## 验证结果

执行命令：

```powershell
dotnet test '.\Wombat.IndustrialCommunicationTestProject\Wombat.IndustrialCommunicationTestProject.csproj' --filter "FullyQualifiedName=Wombat.IndustrialCommunicationTest.ConnectionPoolTests.ConnectionPoolS7HighConcurrencyStabilityTests.ConnectionPool_Should_Remain_Stable_With_100_S7_Servers_And_Random_Disconnects" --no-build
```

结果：

- `通过: 1`
- `失败: 0`
- `持续时间: 1 m 4 s`

## 结论

这次修复的有效点不在“恢复流程更复杂”，而在“恢复对象换成了新实例”。

最小成立结论：

- 原问题的共享根因高概率就是“故障后继续复用已污染的 `SiemensClient`”
- 用工厂重建连接实例，确实切断了这条失败链
- 对当前压测场景，这已经是最短且命中根因的修复

## 后续建议

只建议保留一条后续跟踪：

- 如果后面还出现极少量超时，再优先看 `SiemensClient` 底层调用是否还有不可中断阻塞

不建议现在继续加：

- 新的恢复状态机
- 额外恢复策略接口
- 更复杂的池内同步层

这些都属于在当前验证已通过后继续堆复杂度。

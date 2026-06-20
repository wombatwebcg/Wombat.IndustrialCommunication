# S7 响应错配问题彻底修复计划

## 背景

现场现象：

- 批量读取中出现 `读取预期长度与返回数据长度不一致`。
- 典型日志包括：
  - `读取块 V:2000-2001 失败: VB2000 0 1 读取预期长度与返回数据长度不一致`
  - `读取块 V:2100-2100 失败: VB2100 0 1 读取预期长度与返回数据长度不一致`
- `VB2000` 和 `VB2100` 的值在 PLC 内是固定值，但客户端侧可观察到两个地址的值互换。
- 这不是失败后保留旧值，也不是缓存刷新问题，而是地址和值的对应关系在异常后出现反转。

涉及文件：

- `PLC/S7/SiemensClient.cs`
- `PLC/S7/S7EthernetTransport.cs`
- `PLC/S7/S7Communication.cs`
- `PLC/S7/S7ReadRequest.cs`
- `PLC/S7/S7WriteRequest.cs`
- `PLC/S7/S7BatchHelper.cs`

## 当前判断

### 已排除方向

1. TCP 半包直接导致读取短包

   `TcpClientAdapter.Receive` 当前会循环读取到指定长度后才返回，因此 `读取预期长度与返回数据长度不一致` 不是简单的 `NetworkStream.ReadAsync` 只读到半包造成的。

2. 批量结果本地字典回填互换

   `S7BatchHelper.ExtractDataFromS7Blocks` 使用块 key 和 `OriginalAddress` 回填结果。只要块数据本身正确，本地提取不会把 `VB2000` 和 `VB2100` 互换。

3. 失败后沿用旧值

   现场看到的是两个固定地址的值互换，不是某个地址保留上次值。

### 高可信根因

当前问题更符合下面的链路：

1. 某次 S7 读取响应 payload 与请求预期不一致。
2. 代码检测到 `读取预期长度与返回数据长度不一致`。
3. 批量读取或边界回退逻辑继续在同一条连接上发后续读取请求。
4. 当前 S7 请求的 PDU Reference 固定为 `0x0001`，请求和响应缺少唯一匹配标识。
5. 如果异常发生后连接内已有延迟响应、错位响应或残留响应，后续 `VB2000` / `VB2100` 这类同长度读取可能按顺序错配。
6. 客户端把响应 B 当成请求 A 的结果解析，表现为两个固定地址的值互换。

换句话说：

`读取预期长度与返回数据长度不一致` 不是互换的直接原因，而是请求/响应同步已经失效的信号。同步失效后继续复用连接，且没有 PDU Reference 校验，才是值互换的根因。

## 修复目标

1. 任意一次读取响应必须能验证它属于当前请求。
2. 一旦发现协议同步异常，当前连接不能继续用于后续读取。
3. 批量读取遇到协议同步异常时，不能部分继续并返回可能错配的数据。
4. `VB2000` 和 `VB2100` 这种同长度单字节读取，即使连续轮询，也不能发生响应错配。
5. 保持现有 API 兼容，不改变调用方读取接口。

## 修复方案

### 方案一：增加 S7 PDU Reference 生成与校验

这是核心修复。

当前 `S7ReadRequest` 中 PDU Reference 固定：

```text
command[11] = 0x00
command[12] = 0x01
```

应改为每个请求分配递增 PDU Reference：

- 在 `S7Communication` 或 `S7EthernetTransport` 内维护 `ushort` 序号。
- 每次构造读写请求时分配下一个序号。
- `S7ReadRequest` / `S7WriteRequest` 支持传入 PDU Reference。
- 响应解析后读取响应帧中的 PDU Reference。
- 如果响应 PDU Reference 与请求不一致，立即判定为协议同步异常。

建议新增错误信息：

```text
S7响应PDU Reference不匹配，请求:{requestRef} 响应:{responseRef}
```

处理策略：

- 当前读写失败。
- 标记为 dirty response。
- 长连接模式下立即关闭连接并重连。
- 短连接模式下立即丢弃当前连接。

### 方案二：协议同步异常时终止当前批量读取

当前 `BatchReadAsync` 中某个块失败后仍会继续读取后续块，这对普通地址越界错误可以接受，但对协议同步异常不安全。

需要区分两类失败：

1. 普通业务失败

   例如地址不存在、PLC 返回明确错误码。可以继续其他块读取。

2. 协议同步失败

   例如：

   - `读取预期长度与返回数据长度不一致`
   - `S7响应头长度不足`
   - `响应参数或数据长度无效`
   - `响应功能码异常`
   - `S7响应PDU Reference不匹配`

   这类错误必须终止当前批量读取，不继续后面的块。

建议新增方法：

```csharp
private static bool IsProtocolSynchronizationFailure(OperationResult result)
```

在 `BatchReadAsync` 中：

- 块读取失败后，如果是协议同步失败，直接结束本轮批量读取。
- 返回失败结果，不返回部分成功数据。
- 上层可按现有重试机制重新执行整批读取。

### 方案三：边界回退遇到同步异常时不继续二分重试

当前 `ReadBlockWithBoundaryFallbackAsync` 在初始读取失败后会进行二分缩短读取长度。

这对真实边界越界有价值，但对协议同步异常有风险：

- 初始读取已经说明响应帧不可信。
- 后续继续二分读取可能进一步消费错位响应。
- 最终可能使后续块 `VB2100` 读到前一个地址的响应。

调整策略：

- 如果失败原因是协议同步异常，直接返回失败，不进入二分回退。
- 只有明确业务边界错误才进入二分回退，例如 PLC 返回地址不存在或访问越界状态码。

### 方案四：连接重置应发生在批量读内部的同步异常点

当前 `SiemensClient.ReadAsync` 已经能在单次读取失败时根据错误信息重置连接并重读。

但批量读内部存在多层逻辑：

- `ReadBlockWithBoundaryFallbackAsync`
- `ReadBatchBlockAsync`
- `BatchReadAsync` 继续下一块

修复后应保证：

- 协议同步异常一旦被识别，连接马上进入 dirty 状态。
- 不再继续当前批量读中的下一块。
- 重连后的重试应发生在完整批量读层面，而不是在已错位的块读取流程中继续推进。

### 方案五：移除当前基于长度错误文本的单次读取重连重读

当前未提交修改中，`SiemensClient.ReadAsync` 通过 `ShouldResetConnectionOnReadFailure` 判断以下错误文本后关闭 stream 并立即重读：

```text
读取预期长度与返回数据长度不一致
响应长度不足
S7响应头长度不足
响应参数或数据长度无效
响应功能码异常
```

这部分属于临时缓解，不作为最终修复保留。

原因：

- 它只能根据错误文本判断连接可能已经脏了，不能证明下一次响应属于当前请求。
- 它在单点读取层面重试，批量读取内部仍可能继续推进后续块。
- 它会掩盖真实的请求/响应错配问题，使现场只看到偶发互换。
- 它没有 PDU Reference 校验，不能从协议层确认响应归属。

最终处理：

- 移除 `DirtyResponseRetryAttempts` 的临时单点重读语义，或改为仅服务于正式的整批重试流程。
- 移除 `ShouldResetConnectionOnReadFailure` 中仅基于字符串的最终判定逻辑，改为统一的协议同步异常类型/方法。
- 移除 `ResetConnectionAfterDirtyResponseAsync` 在 `ReadAsync` 内直接重读的调用路径。
- 保留“协议同步异常后关闭连接”的动作，但触发点应来自 PDU Reference 不匹配、响应结构校验失败等明确协议校验结果。
- 批量读取中出现同步异常时，由 `SiemensClient.BatchReadAsync` 断链、重连、整批重试，而不是 `ReadAsync` 局部重读。

### 方案六：可选增强：对高风险离散 V 区单字节点位并块读取

`VB2000` 和 `VB2100` 当前不会被合并到同一块，因为间隔过大且效率比过低。

并块读取不是根因修复，但可以降低连续同长度请求数量。

可选策略：

- 对同一区域、同一 DB、同一轮批量读取中的小字节点位提供配置项。
- 允许把间隔较大的少量点位合并为一个连续块。
- 默认保持现有合并策略，避免读取过大范围影响性能。

建议该项作为二阶段优化，不作为第一阶段根因修复的必要条件。

## 实施步骤

## 当前落地状态

截至 2026-06-20，已完成以下事项：

- 已完成：`S7ReadRequest.cs` / `S7WriteRequest.cs` 支持传入 `ushort pduReference`，并将 PDU Reference 写入 `command[11]`、`command[12]`。
- 已完成：请求对象保留 `PduReference` 属性，供传输层校验使用。
- 已完成：`S7Communication.cs` 增加 `_pduReference` 字段和递增 `GetNextPduReference()` 方法，返回值跳过 `0`。
- 已完成：读写请求在通信层构造时分配递增 PDU Reference。
- 已完成：`S7EthernetTransport.cs` 在完整拼帧后按当前 COTP 偏移定位 S7 Header，校验响应 PDU Reference。
- 已完成：响应 PDU Reference 不匹配时返回失败，错误信息为 `S7响应PDU Reference不匹配，请求:{requestRef} 响应:{responseRef}`。
- 已完成：`S7Communication.cs` 抽象协议同步异常判断，统一收敛以下错误：
  - `读取预期长度与返回数据长度不一致`
  - `响应长度不足`
  - `S7响应头长度不足`
  - `响应参数或数据长度无效`
  - `响应功能码异常`
  - `S7响应PDU Reference不匹配`
- 已完成：`ReadBlockWithBoundaryFallbackAsync` 遇到协议同步异常时直接失败，不再进入二分边界回退。
- 已完成：`BatchReadAsync` 在块读取遇到协议同步异常时立即停止后续块读取，并将本轮批量读取标记为失败，不返回部分成功结果。
- 已完成：`SiemensClient.BatchReadAsync` 上提为整批重试策略；协议同步异常时执行断链、重连、整批重试，重试次数复用 `DirtyResponseRetryAttempts`。
- 已完成：移除 `SiemensClient.ReadAsync` 中基于错误字符串的局部 dirty response retry loop，不再在单次读取路径内直接重读。
- 已完成：保留关闭当前 stream 丢弃脏连接、自动重连、日志记录能力，并迁移到批量读取协议同步异常处理流程中使用。
- 已完成：补充回归测试，已覆盖：
  - `S7ReadRequest` 写入指定 PDU Reference
  - `S7WriteRequest` 写入指定 PDU Reference
  - 响应 PDU Reference 不匹配时读取失败
  - `ReadBlockWithBoundaryFallbackAsync` 遇到协议同步异常不进入二分重试
  - `BatchReadAsync` 遇到协议同步异常停止后续块

当前仍未完成或未完全完成的事项：

- 已完成：单点 `ReadAsync` / `WriteAsync` 在识别到协议同步异常后，立即废弃当前连接的统一处理。
- 已完成：严格 PDU 校验的可配置开关（默认开启，允许兼容性关闭）。
- 已完成：`SiemensClient.BatchReadAsync` 协议同步异常后整批重试成功的自动化测试。
- 已完成：PDU Reference 匹配成功路径的自动化测试。
- 已完成：基于本地 fake TCP server 的“错配 -> 重连 -> 重试成功”集成测试。
- 已完成：协议同步异常相关日志已补充连接废弃、整批重试和重连失败记录；如需继续扩展到更细粒度的 debug 字段，可作为后续增强。

### 第 1 步：补充 PDU Reference 字段

修改：

- `S7ReadRequest.cs`
- `S7WriteRequest.cs`

内容：

- 构造函数增加可选参数 `ushort pduReference`。
- 默认值可暂时为 `1`，保证兼容旧调用。
- 将 `command[11]`、`command[12]` 写为传入的高低字节。
- 请求对象保留 `PduReference` 属性，便于传输层校验。

### 第 2 步：在通信层生成递增 PDU Reference

修改：

- `S7Communication.cs`

内容：

- 增加私有字段：

```csharp
private ushort _pduReference = 0;
```

- 增加方法：

```csharp
private ushort GetNextPduReference()
```

要求：

- 返回值避开 `0`，从 `1` 到 `ushort.MaxValue` 循环。
- 在当前 `_lock` 内调用即可，不需要额外并发锁。

### 第 3 步：响应 PDU Reference 校验

修改：

- `S7EthernetTransport.cs` 或 `S7Communication.cs`

推荐在 `S7EthernetTransport` 完整拼帧后校验：

- 从完整响应帧中定位 S7 Header。
- 读取响应 PDU Reference。
- 与请求的 PDU Reference 比较。
- 不匹配则返回失败。

注意：

- 需要沿用当前 COTP 偏移计算逻辑，不能硬编码固定偏移。
- 初始化握手不走 `S7ReadRequest`，不要误校验握手响应。

### 第 4 步：抽象协议同步异常判断

修改：

- `S7Communication.cs`
- `SiemensClient.cs`

内容：

- 将 dirty response 判断集中为一个方法。
- 同步 `ShouldResetConnectionOnReadFailure` 和批量读内部判断，避免两个地方错误文本不一致。

建议包含：

```text
读取预期长度与返回数据长度不一致
响应长度不足
S7响应头长度不足
响应参数或数据长度无效
响应功能码异常
S7响应PDU Reference不匹配
```

### 第 5 步：调整边界回退策略

修改：

- `S7Communication.cs`

内容：

- `ReadBlockWithBoundaryFallbackAsync` 初始失败后，先判断是否协议同步异常。
- 如果是，直接返回失败，不再二分缩短读取。
- 如果是地址边界类错误，再保留现有二分回退。

### 第 6 步：调整批量读失败策略

修改：

- `S7Communication.cs`

内容：

- 块读取失败后，如果是协议同步异常：
  - 记录错误。
  - 不继续后续块。
  - `result.IsSuccess = false`。
  - 不返回部分成功数据，或至少不把本轮结果标记为成功。

推荐行为：

```text
协议同步异常时整批失败，由外层重试整批读取。
```

原因：

- 协议同步异常后，之前和之后的块结果都不能完全信任。
- 返回部分成功可能继续污染上层展示。

### 第 7 步：连接重置与整批重试

修改：

- `SiemensClient.cs`

内容：

- 移除当前单次 `ReadAsync` 中基于长度错误文本的 dirty response 重置重读逻辑。
- 补充 `BatchReadAsync` 层面的协议同步异常整批重试。

建议：

- `SiemensClient` override `BatchReadAsync`。
- 调用 `base.BatchReadAsync`。
- 如果失败原因是协议同步异常：
  - 关闭连接。
  - 重连。
  - 整批重试一次。
- 重试次数复用 `DirtyResponseRetryAttempts`。

这样比在块内部继续二分更安全。

### 第 8 步：删除临时重连重读代码

修改：

- `SiemensClient.cs`

删除或重构：

- `DirtyResponseRetryAttempts` 当前作为单次读取重试次数的用法。
- `ShouldResetConnectionOnReadFailure` 的字符串判断作为最终错误分类的用法。
- `ResetConnectionAfterDirtyResponseAsync` 在 `ReadAsync` 失败后立即重读的调用路径。
- 长连接 `ReadAsync` 中围绕 `base.ReadAsync` 的 dirty response retry loop。
- 短连接 `ReadAsync` 中围绕 `base.ReadAsync` 的 dirty response retry loop。

保留或迁移：

- 关闭当前 stream 丢弃脏连接的能力。
- 自动重连能力。
- 日志记录能力。

迁移目标：

- 这些能力只由正式的协议同步异常处理流程调用。
- 单点读取可失败返回，由调用方或正式重试策略决定是否重试。
- 批量读取采用整批重试，不进行块内局部重读。

## 测试计划

### 单元测试

新增 S7 测试用例，建议放在：

- `Wombat.IndustrialCommunicationTestProject/S7Tests`

测试项：

1. `S7ReadRequest` 写入指定 PDU Reference

   验证请求帧 `command[11]`、`command[12]` 与传入值一致。

2. 响应 PDU Reference 匹配时读取成功

   构造合法响应帧，PDU Reference 与请求一致。

3. 响应 PDU Reference 不匹配时读取失败

   期望错误包含 `S7响应PDU Reference不匹配`。

4. `ReadBlockWithBoundaryFallbackAsync` 遇到协议同步异常不进入二分重试

   通过 fake transport 统计读取次数，应为 1。

5. `BatchReadAsync` 遇到协议同步异常停止后续块

   构造两个块：第一个协议同步异常，第二个不应被读取。

6. `SiemensClient.BatchReadAsync` 遇到协议同步异常后整批重试

   第一次返回 PDU 不匹配，重连后第二次返回正确结果。

### 集成测试

使用可控 fake stream 模拟以下序列：

1. 请求 `VB2000`，返回 `VB2100` 的响应，PDU Reference 不匹配。
2. 确认客户端不会把 `VB2100` 的值填给 `VB2000`。
3. 确认连接被重置。
4. 重试后 `VB2000`、`VB2100` 值分别正确。

### 现场验证

准备两个固定值点位：

- `VB2000 = A`
- `VB2100 = B`

连续批量读取至少 30 分钟，记录：

- 请求报文
- 响应报文
- PDU Reference
- 点位值
- 是否发生 `读取预期长度与返回数据长度不一致`
- 是否发生重连
- 是否发生整批重试

通过条件：

- 任意一次异常后，不出现 `VB2000=B` 且 `VB2100=A`。
- 协议同步异常时本轮批量读失败或整批重试成功。
- 日志中能看到 PDU Reference 匹配关系。

## 风险与兼容性

### 风险 1：部分 PLC 对递增 PDU Reference 兼容性

S7 协议正常支持 PDU Reference 回显。递增引用是标准做法，风险较低。

缓解：

- 保留配置项允许关闭严格 PDU 校验。
- 默认开启校验。

### 风险 2：整批失败会改变部分成功语义

协议同步异常下整批失败可能比当前更严格。

这是有意调整：

- 普通业务失败仍可部分成功。
- 协议同步失败不能部分成功，因为数据可信度已经丢失。

### 风险 3：增加重试会拉长异常轮询周期

异常时会断链重连并整批重试一次，耗时增加。

缓解：

- 复用 `DirtyResponseRetryAttempts`。
- 默认只重试一次。
- 日志明确记录耗时和重试次数。

## 日志增强建议

为便于现场确认，建议在 debug 或 warning 日志中增加：

- 请求地址
- 请求长度
- 请求 PDU Reference
- 响应 PDU Reference
- 响应 payload bit length
- 期望字节长度
- 实际 payload 字节长度
- 是否触发连接重置
- 是否触发整批重试

示例：

```text
S7读取响应校验失败，地址:VB2000，长度:1，请求PDU:12，响应PDU:13，原因:PDU Reference不匹配，准备重置连接
```

## 建议提交拆分

1. `S7: add PDU reference support to read/write requests`
2. `S7: validate response PDU reference`
3. `S7: stop boundary fallback on protocol synchronization failure`
4. `S7: fail and retry whole batch on dirty S7 response`
5. `S7: add regression tests for VB2000/VB2100 response mismatch`

## 最终验收标准

修复完成后应满足：

- 每次 S7 读写请求都有唯一 PDU Reference。
- 响应 PDU Reference 不匹配时不会解析 payload 为业务值。
- `读取预期长度与返回数据长度不一致` 后不会继续使用当前连接读取后续块。
- 批量读取遇到协议同步异常时不会返回可能错配的部分成功数据。
- `VB2000` 和 `VB2100` 不再出现固定值互换。
- 单元测试覆盖 PDU 校验、批量中断、整批重试。

# S7 批量读写迁移计划

## 1. 目标

本计划按以下固定思路实施：

- 批量读取
  - 连续地址块场景：继续使用 `Wombat` 现有连续块批量读取。
  - 多个 `length = 1` 点位或明显离散/random 场景：使用 `IoTClient` 风格的原生多 Item 随机批量读报文。
- 批量写入
  - 用 `IoTClient` 风格的原生多 Item 随机批量写报文，替换 `Wombat` 当前逐地址单点写实现。
- 全流程必须显式处理协议上限
  - 单批 Item 数上限
  - 单 Item 长度上限
  - 参数区长度上限
  - 数据区长度上限
  - 协商 PDU 长度上限
  - 连续块最大字节长度上限

本次不是做一个“宽泛可选”的新方案，也不是向用户暴露“手动切换批量模式”的公共 API，而是把 `Wombat` 的批量调度明确成内部自动策略：

- 连续块读走 `Wombat`
- 离散随机读走 `IoTClient`
- 批量写统一走 `IoTClient`

## 2. 当前现状

### 2.1 Wombat 当前能力

`Wombat` 当前西门子客户端批量实现：

- 批量读取：`S7Communication.BatchReadAsync`
  - 地址解析
  - 连续块优化
  - 按块读取
  - 块边界回退
- 批量写入：`S7Communication.BatchWriteAsync`
  - 当前是逐地址循环调用单点 `WriteAsync`

优点：

- 连续块读取在连续 DB / I / Q / M 地址场景下效率高
- 已集成 `Wombat` 现有长短连接、自动重连、PDU Reference 校验、脏响应处理

不足：

- 离散随机地址读取时，块优化会带来无效字节读取
- 批量写入不是真批量，只是“批量执行单写”

### 2.2 IoTClient 可迁移能力

`IoTClient` 西门子客户端的核心价值：

- 多 Item 随机批量读报文
- 多 Item 随机批量写报文
- 单报文承载多个离散地址
- 按 Item 顺序解析响应

这部分适合移植到 `Wombat`，但只移植“协议能力”，不照搬整个客户端实现。

## 3. 最终调度策略

## 3.1 读取调度总规则

批量读取入口统一保留在 `S7Communication.BatchReadAsync`，内部自动调度为两条路径：

- `BlockReadPath`
  - 使用 `Wombat` 现有连续块优化批量读取
- `NativeRandomReadPath`
  - 使用 `IoTClient` 风格多 Item 随机批量读

固定判断原则：

1. 地址分布连续，块合并效率高：走 `BlockReadPath`
2. 地址大量离散，尤其是大量 `length = 1` 点位：走 `NativeRandomReadPath`
3. 单批报文若超出协议长度上限：必须拆批
4. 不允许只凭“地址数量少”就直接走随机批量，必须结合效率判断

## 3.2 写入调度总规则

批量写入入口统一保留在 `S7Communication.BatchWriteAsync`，但底层实现替换为：

- `NativeRandomWritePath`
  - 使用 `IoTClient` 风格多 Item 随机批量写报文

不再保留当前逐地址单点写作为默认批量实现。

保底策略：

- 原生随机批量写构造失败、单项越界、报文超限无法切批时直接失败
- 不再通过连接池参数或公共配置暴露“禁用随机批量写 / 降级单点写”

## 4. 读取调度里的效率判断

这是本次计划最关键的部分。

目标不是“只要能连续块就用连续块”，而是判断：

- 连续块读取实际总成本
- 原生随机批量读取实际总成本
- 哪个更划算

这里的“效率”至少从两个维度判断：

- PLC 实际读取字节效率
- 网络往返次数与报文长度效率

## 4.1 连续块读取效率定义

对输入地址集合先调用现有：

- `S7BatchHelper.ParseS7Addresses`
- `S7BatchHelper.OptimizeS7AddressBlocks`

得到 `optimizedBlocks` 后，计算：

- `RequestedBytes`
  - 所有地址真实需要的数据字节总和
- `BlockReadBytes`
  - 所有连续块实际要读取的总字节数
- `BlockCount`
  - 连续块数量

定义：

- `BlockByteEfficiency = RequestedBytes / BlockReadBytes`
- `BlockRequestCost = BlockCount`

解释：

- `BlockByteEfficiency` 越接近 `1`，说明连续块方案浪费字节越少
- `BlockCount` 越小，说明网络往返越少

## 4.2 原生随机批量读取效率定义

原生随机批量读不会读取额外无效字节，但会带来：

- 每个 Item 固定 12 字节 parameter 开销
- 响应中每个 Item 固定返回头开销
- 受单批最大 Item 数和 PDU 限制，需要拆批

需要先根据地址集合估算随机批量读的分批结果：

- `NativeBatchCount`
  - 拆成几批随机读请求
- `NativeParameterBytes`
  - 所有批次的参数区总字节
- `NativePayloadBytes`
  - 所有批次真实读取数据字节
- `NativeResponseOverheadBytes`
  - 响应每个 Item 的固定开销总和

定义：

- `NativeRequestCost = NativeBatchCount`
- `NativeProtocolOverhead = NativeParameterBytes + NativeResponseOverheadBytes`
- `NativeTotalBytes = NativePayloadBytes + NativeProtocolOverhead`

解释：

- 随机批量不会浪费 PLC 数据区字节
- 但会消耗更多协议 item 开销

## 4.3 调度判定建议

建议不要只用一个指标，而是用一组硬规则加软比较。

### 硬规则 1：大量 `length = 1` 点位优先随机批量

如果满足以下任一条件，优先 `NativeRandomReadPath`：

- `length = 1` 的地址数占比超过阈值，例如 `>= 60%`
- bit / byte 点位数量很多，且优化后块效率明显低
- 多个地址分散在多个 DB、多个区域、多个稀疏偏移上

原因：

- 这种场景下块读通常会读很多无效字节
- 多 Item 随机读更符合真实访问形态

### 硬规则 2：块效率很高时优先连续块

如果满足：

- `BlockByteEfficiency >= 0.85`
- 且 `BlockCount <= NativeBatchCount`

优先 `BlockReadPath`

原因：

- 块读取浪费不大
- 网络往返也不更多
- 没必要引入额外 item 协议开销

### 硬规则 3：连续块跨距过大时禁止继续扩块

如果某些块由于稀疏地址被拉得很长，导致：

- `BlockByteEfficiency < 0.5`

则优先 `NativeRandomReadPath`

原因：

- 说明一半以上数据都是无效读取

### 软比较规则

当不命中硬规则时，比较以下指标：

- `BlockReadBytes`
- `BlockCount`
- `NativeBatchCount`
- `NativeTotalBytes`

建议第一版使用简单打分：

- `BlockScore = BlockReadBytes * BlockByteWeight + BlockCount * RequestWeight`
- `NativeScore = NativeTotalBytes * NativeByteWeight + NativeBatchCount * RequestWeight`

默认可先简化为：

- `BlockByteWeight = 1`
- `NativeByteWeight = 1`
- `RequestWeight = 64 ~ 128`

含义：

- 一次网络请求的成本，按几十到上百字节等价成本折算
- 最终选择 `Score` 更低的方案

第一版建议不要把模型做得过复杂，保证行为可解释即可。

## 4.4 调度判断建议落地为独立方法

建议新增内部方法：

- `AnalyzeBatchReadDispatch(...)`
- `EstimateBlockReadCost(...)`
- `EstimateNativeRandomReadCost(...)`
- `ShouldUseNativeRandomRead(...)`

分析结果返回一个内部分析对象，例如：

- `SelectedMode`
- `RequestedBytes`
- `BlockReadBytes`
- `BlockByteEfficiency`
- `BlockCount`
- `NativeBatchCount`
- `NativeTotalBytes`
- `DecisionReason`

这样后续日志和问题排查会很清楚。

## 5. 长度上限与拆包策略

这部分必须写成硬约束。

## 5.1 连续块读取上限

当前 `Wombat` 已有单块读取最大长度控制，现计划继续保留并显式化：

- `MaxBlockReadLength`
  - 默认建议先保留 `180` 字节

若单块超过上限：

- 必须继续切块
- 或沿用当前边界回退逻辑

## 5.2 原生随机批量读上限

必须同时检查：

- `MaxNativeReadItemsPerRequest`
  - 默认建议先对齐 `IoTClient`：`19`
- `MaxNativeReadPayloadBytesPerRequest`
  - 单批真实读取字节总数上限
- `MaxNativeReadParameterBytesPerRequest`
  - parameter 区长度上限
- `NegotiatedPduLimit`
  - 结合协商 PDU 计算本次允许的最大请求长度

拆批规则：

1. 先按 Item 数限制切批
2. 再按估算报文总长度检查
3. 若超限，进一步缩小批次
4. 必须保证单批 request 和 response 都不超限

## 5.3 原生随机批量写上限

必须同时检查：

- `MaxNativeWriteItemsPerRequest`
  - 默认建议先对齐 `IoTClient`：`10`
- `MaxNativeWritePayloadBytesPerRequest`
  - 所有写入值的总字节数上限
- `MaxNativeWriteParameterBytesPerRequest`
  - parameter 区长度上限
- `NegotiatedPduLimit`

写入尤其要注意：

- bit 写入与 byte/word 写入混合时的数据区膨胀
- `WriteData.Length == 1` 的补齐逻辑对总长度的影响

## 5.4 寄存器长度上限要落到 Item 级校验

对每个地址项，在进入批量之前就要校验：

- 该地址的逻辑长度是否合法
- 是否超过当前协议和 `Wombat` 定义允许的最大长度
- bit / byte / word / dword 的编码长度是否正确

建议新增校验方法：

- `ValidateNativeReadItemLength(...)`
- `ValidateNativeWriteItemLength(...)`

## 6. 代码改造方案

## 6.1 读取部分

### 保留

保留当前：

- `S7BatchHelper.ParseS7Addresses`
- `S7BatchHelper.OptimizeS7AddressBlocks`
- `S7Communication.BatchReadAsync` 主入口
- 现有连续块读实现

### 新增

建议新增：

- `S7NativeReadItem.cs`
- `S7NativeReadRequest.cs`
- `S7NativeReadResponse.cs`

职责：

- 基于已解析地址构造多 Item 随机读请求
- 解析多 Item 响应
- 回填到原始地址集合

### 修改

修改：

- `S7Communication.BatchReadAsync`

新增内部流程：

1. 解析地址
2. 计算连续块方案成本
3. 计算随机批量方案成本
4. 调度选择
5. 执行对应路径
6. 聚合结果

建议新增内部方法：

- `DispatchBatchReadAsync(...)`
- `BatchReadByBlockAsync(...)`
- `BatchReadByNativeRandomAsync(...)`

## 6.2 写入部分

### 替换

替换当前：

- `S7Communication.BatchWriteAsync` 内部逐点写逻辑

改为：

- 基于 `IoTClient` 风格多 Item 报文进行随机批量写

### 新增

建议新增：

- `S7NativeWriteItem.cs`
- `S7NativeWriteRequest.cs`
- `S7NativeWriteResponse.cs`

### 修改

修改：

- `S7Communication.BatchWriteAsync`

建议内部流程：

1. 解析地址和值
2. 转换为写入 Item
3. 按长度上限拆批
4. 逐批发送多 Item 写报文
5. 逐 Item 解析写结果
6. 聚合最终成功/失败

## 6.3 传输层

需要扩展：

- `S7EthernetTransport`

要求：

- 支持新的多 Item 读写请求类型
- 继续复用 `PduReference` 校验
- 继续复用 `ReceiveFullResponseAsync`

## 6.4 客户端层

需要修改：

- `SiemensClient`

要求：

- 原生随机批量读写也要走当前长短连接、自动重连、脏响应重试逻辑
- 整批为一个重试单位
- 日志明确打印：
  - 当前批量模式
  - 为什么这样调度
  - 连续块效率值
  - 拆成了几批

## 7. 内部策略约束

本次新增的“不连续地址批量读写”属于 `S7Communication` 内部调度行为，不设计为用户或连接池可配置能力。

明确约束：

- 用户侧入口仍然只有 `BatchReadAsync(...)` / `BatchWriteAsync(...)`
- 不对外提供“手动选择 BlockRead / NativeRandomRead / NativeRandomWrite”的公共 API
- 连接池参数模型和工厂不透传随机批量读写开关、阈值、Item 上限、payload 上限
- 调度分析对象、路径枚举、成本估算方法仅作为库内部实现细节存在
- 随机批量读写的 Item 上限、payload 上限、调度阈值由库内部固定策略维护

当前内部默认策略：

- `NativeRandomReadMaxItems = 19`
- `NativeRandomWriteMaxItems = 10`
- `NativeRandomReadMaxPayloadBytes = 180`
- `NativeRandomWriteMaxPayloadBytes = 180`
- `BlockReadMinEfficiency = 0.8`
- `RandomReadPreferSingleLengthThreshold = 4`
- `BatchReadDispatchRequestWeight = 1.0`

## 8. 测试计划

## 8.1 连续块与随机批量调度测试

必须覆盖：

- 明显连续地址，命中 `BlockReadPath`
- 大量 `length = 1` 离散点位，命中 `NativeRandomReadPath`
- 地址介于两者之间，走成本比较
- 调度结果与日志原因一致

## 8.2 长度上限与拆包测试

必须覆盖：

- Item 数刚好到上限
- Item 数超过上限
- parameter 区接近上限
- payload 区接近上限
- 超过协商 PDU 限制时自动拆批

## 8.3 协议正确性测试

必须覆盖：

- 多 Item 随机批量读 request 十六进制正确
- 多 Item 随机批量写 request 十六进制正确
- 多 Item 响应按顺序解析正确
- bit / byte 混合场景长度编码正确

## 8.4 回归测试

必须保证：

- 现有连续块批量读取不退化
- 单点读写不退化
- 长连接脏响应重连逻辑不退化

## 8.5 真实 PLC 验证现状

已新增真实 PLC 大批量测试：

- 测试文件：`Wombat.IndustrialCommunicationTestProject/PLCTests/S7Smart200LargeBatchRealPlcTests.cs`
- 设备：`192.168.10.100:102`
- 机型：`S7_200Smart`
- 地址约束：测试地址限制在 `VB0` 到 `VB6000` 范围内

当前真实设备验证结论：

- 大于 200 的连续 / 离散 / 混合批量“先写随机值，再批量读回校验”测试已落地
- 测试已能稳定复现 `S7_200Smart` 在 `NativeRandomWrite` 路径上的兼容性问题
- 当前失败现象表现为：批量随机写后，响应解析报 `批量随机写响应长度不足`

因此，真实 PLC 验证状态应理解为：

- 测试能力已具备
- `S7_200Smart` 的原生随机批量写兼容性仍需单独修复

## 9. 实施阶段

### Phase 1：调度分析层

先实现：

- 成本估算
- 连续块效率计算
- 随机批量成本估算
- 调度决策对象

完成标准：

- 不发真实报文也能先算出“应该走哪条路径”

### Phase 2：原生随机批量读取

实现：

- 多 Item 读请求/响应
- 读取拆批
- 接入 `BatchReadAsync`

完成标准：

- 连续块和随机批量读可自动分流

### Phase 3：原生随机批量写入

实现：

- 多 Item 写请求/响应
- 写入拆批
- 替换当前 `BatchWriteAsync`

完成标准：

- 批量写不再逐点写

### Phase 4：稳定化

实现：

- 参数调优
- 日志增强
- 文档补充
- 特定 PLC 机型兼容性收尾（尤其 `S7_200Smart` 原生随机批量写响应处理）

## 10. 验收标准

满足以下条件才算完成：

- 连续块批量读取仍由 `Wombat` 原有方案执行
- 离散随机批量读取已改为 `IoTClient` 风格多 Item 报文
- 批量写入已改为 `IoTClient` 风格多 Item 报文
- 调度逻辑能解释“为什么走连续块，为什么走随机批量”
- 所有长度上限和拆包规则已显式实现
- 调度结果、拆批结果、失败原因都能从日志中看清楚
- 连接池与用户侧不暴露随机批量调度控制参数

## 11. 最终建议

实现时不要简单理解为“加一个随机批量模式”，而应按下面方式落地：

- 读：做成调度器
  - 连续块效率高就走 `Wombat`
  - 连续块效率低、尤其大量 `length = 1` 点位就走 `IoTClient`
- 写：直接迁移到 `IoTClient` 风格原生随机批量写
- 所有选择都必须服从协议长度上限和拆包规则

这样才真正符合你的思路：  
既保住 `Wombat` 在连续块读上的优势，又把 `IoTClient` 在随机离散读写上的协议能力引进来。

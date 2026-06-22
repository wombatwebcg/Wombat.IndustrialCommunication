# S7 原生随机批量读写开发任务清单

## 阶段划分原则

本任务拆成两个阶段：

- 阶段一：先完成批量读取调度和原生随机批量读
- 阶段二：完成原生随机批量写，替换现有批量写，并做整体稳定化

原因：

- 读取的副作用小，适合先把调度模型和协议报文走通
- 写入涉及更多 PLC 兼容性和风险，放到第二阶段更稳妥

---

## 阶段一：批量读取调度 + 原生随机批量读

## 目标

完成以下能力：

- 连续块读继续走 `Wombat`
- 离散/random 读走 `IoTClient` 风格多 Item 随机批量读
- 批量读取具备明确的调度判断与长度上限控制

## 任务 1：定义调度与内部策略模型

目标：

- 为读取调度建立固定的数据结构和内部策略入口

任务：

- 新增内部读取路径类型，例如 `S7BatchReadPathKind`
- 新增内部读取调度分析结果类，例如 `S7BatchReadDispatchAnalysis`
- 在 `S7Communication` 内增加读取相关内部策略常量或内部可覆写策略方法

完成标准：

- 调度逻辑所需内部策略具备统一入口
- 内部分析对象能完整描述一次批量读取为什么走某条路径
- 用户侧不暴露“手动选择读取路径”的公共 API
- ConnectionPool 不透传随机批量读取控制参数

## 任务 2：实现读取成本估算器

目标：

- 在真正发报文前，先评估连续块读和随机批量读的成本

任务：

- 新增 `EstimateBlockReadCost(...)`
- 新增 `EstimateNativeRandomReadCost(...)`
- 新增 `AnalyzeBatchReadDispatch(...)`
- 新增 `ShouldUseNativeRandomRead(...)`

估算指标至少包含：

- `RequestedBytes`
- `BlockReadBytes`
- `BlockByteEfficiency`
- `BlockCount`
- `NativeBatchCount`
- `NativeTotalBytes`
- `DecisionReason`

完成标准：

- 给定一组地址，不发 PLC 请求也能稳定得出调度结果
- 上述方法仅作为库内部调度实现存在

## 任务 3：实现原生随机批量读模型

目标：

- 在 `Wombat` 内定义多 Item 随机读所需的内部模型

任务：

- 新增 `S7NativeReadItem`
- 明确每个 item 的字段
  - 原始地址
  - 数据类型
  - 区域类型
  - DB 号
  - 偏移
  - 长度
  - bit/byte 访问方式
  - 原始顺序索引

完成标准：

- 可以把 `S7BatchHelper.ParseS7Addresses` 的结果稳定映射到随机批量读 item

## 任务 4：实现原生随机批量读请求报文

目标：

- 支持构造 `IoTClient` 风格多 Item 读报文

任务：

- 新增 `S7NativeReadRequest`
- 实现多 Item `Read Var` 报文组包
- 接入 `PduReference`
- 增加单批长度估算方法，供拆批器复用

必须处理：

- Item 数限制
- parameter 区长度限制
- payload 读取长度限制
- 协商 PDU 限制

完成标准：

- 单批多 Item 读请求可生成正确十六进制报文

## 任务 5：实现原生随机批量读响应解析

目标：

- 正确解析多 Item 读响应并回填到原始地址

任务：

- 新增 `S7NativeReadResponse`
- 逐 Item 解析返回码、长度、数据区
- 对部分成功场景做结果聚合
- 支持 bit / byte / word / dword 数据提取

完成标准：

- 多 Item 响应能逐项映射回地址
- 单个 item 失败不会破坏其他 item 成功结果

## 任务 6：实现随机批量读拆批器

目标：

- 保证随机批量读严格遵守报文长度上限

任务：

- 新增 `SplitNativeRandomReadBatches(...)`
- 按以下规则拆批
  - 先按 Item 数限制
  - 再按 request 长度估算
  - 再按 response 长度估算
  - 超限时继续缩小批次

完成标准：

- 任意输入地址集合都能被拆成合法批次

## 任务 7：接入 S7Communication 读取主流程

目标：

- 在现有 `BatchReadAsync` 中完成连续块读和随机批量读的调度接入

任务：

- 重构 `S7Communication.BatchReadAsync`
- 保留现有连续块读取路径为 `BatchReadByBlockAsync`
- 新增 `BatchReadByNativeRandomAsync`
- 接入调度分析结果
- 将调度原因写入日志

完成标准：

- `BatchReadAsync` 能根据地址分布自动走正确路径
- 用户只调用 `BatchReadAsync`，不直接选择路径类型

## 任务 8：接入 SiemensClient 运行时语义

目标：

- 原生随机批量读也具备 `Wombat` 当前客户端的连接和重试行为

任务：

- 在 `SiemensClient.BatchReadAsync` 上保持：
  - 长连接自动重连
  - 短连接整批执行
  - 脏响应整批重试
- 日志输出当前路径和调度原因

完成标准：

- 新旧读取路径在连接管理语义上保持一致

## 任务 9：阶段一测试

目标：

- 验证读取调度和随机批量读正确性

测试项：

- 连续块地址命中 `Wombat` 块读
- 多个 `length = 1` 离散地址命中随机批量读
- 中间场景命中成本比较结果
- 随机批量读 request 报文正确
- 随机批量读 response 解析正确
- 上限拆批正确
- 长连接/短连接/重试逻辑不回归

阶段一验收标准：

- 批量读取双路径调度完成
- 调度原因可解释
- 所有读取长度上限和拆批规则生效

---

## 阶段二：原生随机批量写 + 替换现有批量写 + 稳定化

## 目标

完成以下能力：

- 用 `IoTClient` 风格多 Item 随机批量写替换 `Wombat` 当前逐点批量写
- 明确写入长度上限、拆批策略和降级策略
- 完成整体稳定化、测试与文档补充

## 任务 10：实现原生随机批量写模型

目标：

- 定义多 Item 随机写内部模型

任务：

- 新增 `S7NativeWriteItem`
- 字段至少包含
  - 原始地址
  - 数据类型
  - 区域类型
  - DB 号
  - 偏移
  - 写入字节数据
  - bit/byte 访问方式
  - 原始顺序索引

完成标准：

- 任意批量写入参数都能转换为内部写 item

## 任务 11：实现原生随机批量写请求报文

目标：

- 构造 `IoTClient` 风格多 Item 写报文

任务：

- 新增 `S7NativeWriteRequest`
- 迁移并适配多 Item `Write Var` 组包逻辑
- 处理 bit 写、byte 写、填充字节、transport size
- 接入 `PduReference`

重点：

- `WriteData.Length == 1` 的补齐逻辑必须严格测试
- bit / byte 混合写入的长度编码必须正确

完成标准：

- 单批多 Item 写请求可生成正确报文

## 任务 12：实现原生随机批量写响应解析

目标：

- 逐 Item 判断批量写响应是否成功

任务：

- 新增 `S7NativeWriteResponse`
- 逐 Item 读取返回码
- 聚合成功数、失败数和失败原因

完成标准：

- 支持部分成功场景
- 失败原因能定位到具体地址

## 任务 13：实现随机批量写拆批器

目标：

- 保证批量写报文不超过协议上限

任务：

- 新增 `SplitNativeRandomWriteBatches(...)`
- 同时按以下约束拆批
  - Item 数上限
  - parameter 区长度上限
  - data 区长度上限
  - 协商 PDU 上限

完成标准：

- 任意批量写入请求都能拆成合法写批次

## 任务 14：替换现有 BatchWriteAsync 主流程

目标：

- 用随机批量写替换当前逐点写

任务：

- 重构 `S7Communication.BatchWriteAsync`
- 删除默认逐地址单点写主逻辑
- 新增 `BatchWriteByNativeRandomAsync`
- 原生随机批量写构造失败或无法合法拆批时直接失败

完成标准：

- `BatchWriteAsync` 默认不再逐点写
- 不存在连接池或公共配置层面的随机批量写开关/降级控制

## 任务 15：接入 SiemensClient 写入运行时语义

目标：

- 随机批量写与当前客户端重连/重试体系兼容

任务：

- 在 `SiemensClient.BatchWriteAsync` 上保持：
  - 长连接自动重连
  - 短连接整批执行
  - 脏响应整批重试
- 日志记录拆批和失败项

完成标准：

- 批量写在客户端层语义与批量读保持一致

## 任务 16：稳定化与调优

目标：

- 让批量读写调度和长度控制进入可维护状态

任务：

- 抽取统一的长度估算工具
- 抽取统一的拆批工具
- 统一日志格式
- 固化必要内部默认策略
- 对默认阈值做调优
- 收敛内部命名，避免将调度分析类型误暴露为公共扩展点

完成标准：

- 读写路径共用规则清晰
- 调试信息足够定位问题
- ConnectionPool 不透传随机批量调度控制项

## 任务 17：阶段二测试

目标：

- 验证批量写替换后的正确性和稳定性

测试项：

- 多 Item 随机批量写 request 报文正确
- 多 Item 写 response 解析正确
- bit / byte / word 混合写正确
- Item 数和长度上限拆批正确
- 批量写部分成功场景正确
- 与现有单点写结果一致
- 长连接/短连接/重连/脏响应重试不回归
- 真实 `S7_200Smart` 批量大于 200 地址的连续 / 随机 / 混合读写回读一致性验证

阶段二验收标准：

- `BatchWriteAsync` 已完成协议级随机批量写替换
- 长度上限控制生效
- 读写双路径行为稳定

当前补充说明：

- 真实 PLC 测试文件已落地：`Wombat.IndustrialCommunicationTestProject/PLCTests/S7Smart200LargeBatchRealPlcTests.cs`
- 真实设备为 `192.168.10.100:102`，机型 `S7_200Smart`
- 测试地址限制在 `VB0` 到 `VB6000`
- 目前该测试已验证出 `S7_200Smart` 的 `NativeRandomWrite` 仍存在响应兼容性问题，尚未达到“真实 PLC 完整通过”的最终验收状态

---

## 最终交付标准

两个阶段完成后，应达到：

- 连续地址块批量读取仍由 `Wombat` 原有块读方案执行
- 离散/random 批量读取已由 `IoTClient` 风格原生随机读执行
- 批量写入已由 `IoTClient` 风格原生随机写执行
- 调度规则、效率评估、拆批原因都可从日志中解释
- 读写流程都具备明确的长度上限与拆批控制

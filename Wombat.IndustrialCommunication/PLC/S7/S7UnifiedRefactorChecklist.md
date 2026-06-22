# S7 统一 Read/Write 模型重构清单

## 重构目标

把当前两套模型：

- 旧模型：`S7ReadRequest` / `S7WriteRequest` / `S7ReadResponse` / `S7WriteResponse`
- Native模型：`S7NativeRead*` / `S7NativeWrite*`

收敛成一套统一模型：

- `S7ReadRequest`
- `S7ReadResponse`
- `S7WriteRequest`
- `S7WriteResponse`
- `SiemensAddress`

并让上层 `S7Communication`、下层 `S7EthernetTransport` 只认这一套。

## 一、模型层重构清单

### 1. `PLC/S7/SiemensAddress.cs`

- 保留现有字段：
  - `Address`
  - `DataType`
  - `TypeCode`
  - `DbBlock`
  - `BeginAddress`
  - `ReadWriteLength`
  - `IsBit`
- 新增字段，用于吸收 `S7NativeReadItem` / `S7NativeWriteItem`：
  - `OriginalAddress`
  - `ByteOffset`
  - `BitOffset`
  - `Length`
  - `RequestedLength`
  - `OriginalIndex`
  - `WriteData`
- 新增计算属性：
  - `RequestLength`
    - 读场景：`IsBit && RequestedLength > 1 ? (RequestedLength + 7) / 8 : Length`
    - 写场景：如果 `WriteData != null`，则取 `WriteData.Length`
  - `EffectiveIsBit`
    - 从 `S7NativeWriteItem.EffectiveIsBit` 迁移
- 统一字段语义：
  - `TypeCode` 作为唯一“区域类型码”
  - `DbBlock` 作为唯一“DB号”
- 删除后续模型中的同义字段映射需求

### 2. `PLC/S7/SiemensWriteAddress.cs`

- 评估是否保留
- 推荐处理：
  - 第一阶段保留，避免外部编译面太大
  - 第二阶段删除，让 `SiemensAddress.WriteData` 直接承担写入能力
- 如果保留：
  - 明确只作为兼容层，不再扩展新逻辑

## 二、请求模型重构清单

### 3. `PLC/S7/S7ReadRequest.cs`

- 删除类内私有 `ConvertSiemensAddress`
  - 全部统一改为调用 `S7CommonMethods.ConvertArg`
- 保留现有单地址构造器：
  - `S7ReadRequest(string address, int offset, int length, bool isBit, ushort pduReference = 1)`
- 新增批量构造器：
  - `S7ReadRequest(IReadOnlyList<SiemensAddress> items, ushort pduReference)`
- 新增属性：
  - `IReadOnlyList<SiemensAddress> Items`
- 迁移 `S7NativeReadRequest` 的方法：
  - `EstimateRequestLength`
  - `EstimateResponsePayloadLength`
  - `EstimateResponseFrameLength`
  - `BuildCommand`
- 保留旧的 `GetReadCommand(SiemensAddress data, ushort pduReference)` 作为兼容入口
- 新增统一实现：
  - `GetReadCommand(IReadOnlyList<SiemensAddress> items, ushort pduReference)`
- 统一 `RegisterAddress`
  - 单地址：原地址
  - 批量：逗号拼接所有地址
- 统一 `RegisterCount`
  - 单地址：原长度
  - 批量：`Items.Count`

### 4. `PLC/S7/S7WriteRequest.cs`

- 保留现有单地址构造器：
  - `S7WriteRequest(string address, int offset, byte[] writeData, bool isBit, ushort pduReference = 1)`
- 新增批量构造器：
  - `S7WriteRequest(IReadOnlyList<SiemensAddress> items, ushort pduReference)`
- 新增属性：
  - `IReadOnlyList<SiemensAddress> Items`
- 迁移 `S7NativeWriteRequest` 的方法：
  - `EstimateParameterLength`
  - `EstimateDataLength`
  - `EstimateRequestLength`
  - `EstimateResponseFrameLength`
  - `BuildCommand`
- 逐步废弃：
  - `GetWriteCommand(SiemensWriteAddress write, ushort pduReference)`
  - `GetWriteCommand(SiemensWriteAddress[] writes, ushort pduReference)`
- 改为统一实现：
  - `GetWriteCommand(IReadOnlyList<SiemensAddress> items, ushort pduReference)`
- 保留这些关键语义：
  - 单字节写且非最后一项要补位
  - `EffectiveIsBit` 决定参数区和数据区的 transport size
- `RegisterAddress` / `RegisterCount` 同读请求统一处理

## 三、响应模型重构清单

### 5. `PLC/S7/S7ReadResponse.cs`

- 从“原始帧壳子”升级为“可解析响应对象”
- 保留：
  - `ProtocolMessageFrame`
  - `RegisterAddress`
  - `RegisterCount`
- 新增内部结果模型：
  - `ItemResult`
    - `SiemensAddress Item`
    - `bool IsSuccess`
    - `byte ReturnCode`
    - `byte TransportSize`
    - `byte[] Data`
    - `string Message`
- 新增属性：
  - `List<ItemResult> Items`
  - `bool HasProtocolFailure`
  - `string ProtocolFailureMessage`
- 迁移 `S7NativeReadResponse.Parse`
  - 签名建议：
    - `Parse(byte[] dataPackage, IReadOnlyList<SiemensAddress> items)`
- 保留原构造器：
  - `S7ReadResponse(byte[] data)`
- 可选新增：
  - `TryParseSingleValue<T>()`
  - 但这不是第一阶段必须项

### 6. `PLC/S7/S7WriteResponse.cs`

- 从“原始帧壳子”升级为“可解析响应对象”
- 新增内部结果模型：
  - `ItemResult`
    - `SiemensAddress Item`
    - `bool IsSuccess`
    - `byte ReturnCode`
    - `string Message`
- 新增属性：
  - `List<ItemResult> Items`
  - `int SuccessCount`
  - `int FailureCount`
- 迁移 `S7NativeWriteResponse.Parse`
  - 签名建议：
    - `Parse(byte[] dataPackage, IReadOnlyList<SiemensAddress> items)`
- 保留原构造器：
  - `S7WriteResponse(byte[] data)`

## 四、公共解析工具重构清单

### 7. `PLC/S7/S7CommonMethods.cs`

- 保留并作为唯一地址解析入口
- 补齐/确认：
  - `ConvertArg(string address, int offset = 0)`
  - `ConvertArg(Dictionary<string, DataTypeEnums> addresses, int offset = 0)`
  - `ConvertWriteArg(...)`
- 推荐新增：
  - `BuildReadAddress(...)`
  - `BuildWriteAddress(...)`
  - 或者统一为 `CreateAddressItem(...)`
- 目标：
  - 让 `S7Communication` 不再手工组装 `S7NativeReadItem` / `S7NativeWriteItem`
  - 直接产出统一 `SiemensAddress`

## 五、服务端辅助重构清单

### 8. `PLC/S7/S7ResponseBuilder.cs`

- 不作为 Native 合并主承载
- 只做配套统一
- 建议改动：
  - `ParseReadRequest` / `ParseWriteRequest` 返回统一结构，或内部复用统一地址项解析逻辑
- 可选：
  - 把 `S7ReadParameter` / `S7WriteParameter` 改成复用 `SiemensAddress`
- 如果服务端解析逻辑和客户端模型耦合太高，则先不强行统一，避免扩大变更面

## 六、调用链重构清单

### 9. `PLC/S7/S7Communication.cs`

- 重命名方法：
  - `BuildNativeReadItems` -> `BuildReadItems`
  - `BuildNativeWriteItems` -> `BuildWriteItems`
  - `SplitNativeRandomReadBatches` -> `SplitReadBatches`
  - `SplitNativeRandomWriteBatches` -> `SplitWriteBatches`
- 修改返回类型：
  - 从 `List<S7NativeReadItem>` 改为 `List<SiemensAddress>`
  - 从 `List<S7NativeWriteItem>` 改为 `List<SiemensAddress>`
- 修改批次类型：
  - 第一阶段可保留 `S7NativeReadBatch` / `S7NativeWriteBatch` 外壳
  - 但内部 `Items` 类型改为 `List<SiemensAddress>`
- 修改执行链：
  - `ExecuteNativeRandomReadBatchAsync`
    - 改为创建 `new S7ReadRequest(batch.Items, GetNextPduReference())`
    - 响应解析改为 `S7ReadResponse.Parse(...)`
  - `ExecuteNativeRandomWriteBatchAsync`
    - 改为创建 `new S7WriteRequest(batch.Items, GetNextPduReference())`
    - 响应解析改为 `S7WriteResponse.Parse(...)`
- 修改数据转换：
  - `ConvertNativeReadBytesToValue` -> `ConvertReadBytesToValue`
  - 参数类型改为 `SiemensAddress`

### 10. `PLC/S7/S7EthernetTransport.cs`

- 删除对以下类型的专门分支：
  - `S7NativeReadRequest`
  - `S7NativeWriteRequest`
- 只保留：
  - `S7ReadRequest`
  - `S7WriteRequest`
- 保持现有：
  - PDU Reference 校验
  - 收包逻辑
- 返回值仍可继续包装：
  - `S7ReadResponse`
  - `S7WriteResponse`

### 11. `PLC/S7/S7NativeBatchLimits.cs`

- 修改内部引用：
  - `S7NativeReadRequest.Estimate...` -> `S7ReadRequest.Estimate...`
  - `S7NativeWriteRequest.Estimate...` -> `S7WriteRequest.Estimate...`
- 如果构造 baseline item 时使用 `S7NativeWriteItem`
  - 改为 `SiemensAddress`

## 七、兼容层与删除清单

### 12. 第一批标记废弃的文件

- `S7NativeReadItem.cs`
- `S7NativeWriteItem.cs`
- `S7NativeReadRequest.cs`
- `S7NativeWriteRequest.cs`
- `S7NativeReadResponse.cs`
- `S7NativeWriteResponse.cs`

### 13. 第二批清理的文件

- `S7NativeReadBatch.cs`
- `S7NativeWriteBatch.cs`
- 视 `S7Communication` 重构结果决定是否保留为通用 `S7ReadBatch` / `S7WriteBatch`

### 14. 清理要求

- 所有 `rg "S7Native"` 引用清零后再删文件
- 删除前先保证：
  - 编译通过
  - `S7Communication` 不再创建 Native 对象
  - `S7EthernetTransport` 不再判断 Native 类型

## 八、建议的分批提交顺序

### 1. 提交一

- 改 `SiemensAddress.cs`
- 改 `S7CommonMethods.cs`
- 不动调用链

### 2. 提交二

- 改 `S7ReadRequest.cs`
- 改 `S7WriteRequest.cs`
- 支持批量构造，但暂不切换上层调用

### 3. 提交三

- 改 `S7ReadResponse.cs`
- 改 `S7WriteResponse.cs`
- 完成统一解析能力

### 4. 提交四

- 改 `S7Communication.cs`
- 改 `S7EthernetTransport.cs`
- 批量链路切到统一模型

### 5. 提交五

- 改 `S7NativeBatchLimits.cs`
- 清理 `S7Native*`
- 编译与回归测试

## 九、必测清单

### 1. 单地址读

- `DB1.DBX0.0`
- `DB1.DBB2`
- `DB1.DBW4`
- `DB1.DBD8`
- `M0.0`
- `V100`

### 2. 单地址写

- bit写
- 1字节写
- 多字节写

### 3. 批量随机读

- 多个 DB 地址混合
- DB + M + I + Q 混合
- bit 和 byte/word/dword 混合

### 4. 批量随机写

- 单字节项非最后项补位
- bit写与 byte写混合

### 5. 边界场景

- PDU 超限拆包
- 某一项地址不存在
- 返回项数和请求项数不一致
- payload 长度不足

## 十、执行标准

重构完成后，应满足这几个标准：

- 工程内不再直接 new `S7NativeReadRequest` / `S7NativeWriteRequest`
- `S7EthernetTransport` 不再识别 Native 请求类型
- 批量随机读写全部走 `S7ReadRequest` / `S7WriteRequest`
- 项级响应解析全部走 `S7ReadResponse.Parse` / `S7WriteResponse.Parse`
- `SiemensAddress` 成为唯一地址项模型


# ConnectionPool 对外硬编码参数模型改造建议

## 1. 文档目标

本文档用于分析 `Wombat.IndustrialCommunication.ConnectionPool` 中“通讯参数以字符串键名传递”的现状，并给出改造成“对外强类型硬编码参数模型”的方案。

重点说明：

- 为什么不再继续沿用 `ResourceDescriptor.Parameters`
- 如何把字符串键参数改为强类型参数对象
- 工厂、文档、测试和第三方接入方需要同步调整哪些内容
- 在不保留兼容层的前提下，如何一次性完成升级

本文档描述的是目标方案和实施方向，不代表代码已经完成修改。

## 2. 现状说明

### 2.1 当前参数入口

当前连接池通过 `ResourceDescriptor.Parameters` 传递协议相关参数：

```csharp
public IDictionary<string, object> Parameters { get; set; }
```

调用方通常会这样写：

```csharp
descriptor.Parameters["ip"] = "192.168.1.10";
descriptor.Parameters["port"] = 502;
descriptor.Parameters["connectTimeoutMilliseconds"] = 3000;
```

### 2.2 当前设计带来的事实结果

当前做法意味着：

- 外部调用方必须记忆字符串键名
- 参数值在编译期缺少类型约束
- 工厂内部需要持续做字符串查找和类型转换
- 文档和测试示例天然会继续扩散裸字符串写法

因此这里的问题不只是“字符串散落”，而是“对外配置模型本身就是弱类型”。

## 3. 当前问题

### 3.1 拼写错误只能在运行期暴露

例如：

```csharp
descriptor.Parameters["receiveTimeoutMillseconds"] = 3000;
```

上面 `Milliseconds` 少写一个字母时，编译器不会报错，最终常常表现为参数未生效，排查成本较高。

### 3.2 参数值类型缺少编译期约束

例如：

```csharp
descriptor.Parameters["port"] = "abc";
descriptor.Parameters["slot"] = "not-number";
```

这类错误只能在运行期转换阶段被发现，或者被默认值静默吞掉。

### 3.3 协议参数边界不清晰

当前字典入口无法自然表达：

- 哪些参数属于 ModbusTcp
- 哪些参数属于 ModbusRtu
- 哪些参数属于 SiemensS7
- 哪些参数只允许服务端使用

结果就是不同协议参数容易误传，语义边界不明确。

### 3.4 工厂承担了过多参数解析职责

当前默认工厂除了负责创建连接对象，还需要负责：

- 读取字符串键
- 解析 `object` 值
- 处理默认值
- 判断参数是否存在

这会让工厂更偏向“动态解析器”，而不是“强类型装配器”。

## 4. 改造目标

本次方案不再以“参数键名常量化”为目标，而是直接改为“对外强类型硬编码参数模型”。

目标如下：

- 移除 `ResourceDescriptor.Parameters` 的字典式配置入口
- 外部调用方不再通过字符串键传参
- 为每类连接提供明确的强类型参数对象
- 工厂直接消费强类型参数对象，不再做字符串查找
- 不保留现有 `Parameters` 兼容层

这意味着本次升级是一次明确的 API 形态调整，而不是低风险兼容增强。

## 5. 推荐模型

### 5.1 ResourceDescriptor 改为持有强类型参数对象

建议将：

```csharp
public IDictionary<string, object> Parameters { get; set; }
```

替换为：

```csharp
public IConnectionPoolParameters ConnectionParameters { get; set; }
```

其中 `IConnectionPoolParameters` 作为所有连接参数模型的统一标记接口。

### 5.2 按协议定义参数类

建议至少引入以下参数模型：

- `ModbusTcpClientConnectionParameters`
- `ModbusRtuClientConnectionParameters`
- `SiemensS7ClientConnectionParameters`
- `FinsClientConnectionParameters`
- `ModbusTcpServerConnectionParameters`
- `ModbusRtuServerConnectionParameters`
- `SiemensS7ServerConnectionParameters`

这样可以把客户端与服务端、TCP 与串口、不同 PLC 协议的配置边界直接体现在类型系统中。

### 5.3 参数对象以属性表达配置

示意：

```csharp
public class ModbusTcpClientConnectionParameters : IConnectionPoolParameters
{
    public string Ip { get; set; }
    public int Port { get; set; }
    public int ConnectTimeoutMilliseconds { get; set; }
    public int ReceiveTimeoutMilliseconds { get; set; }
    public int SendTimeoutMilliseconds { get; set; }
    public int Retries { get; set; }
    public string ProbeAddress { get; set; }
    public DataTypeEnums ProbeDataType { get; set; }
    public int ProbeLength { get; set; }
    public int BatchReadStationIntervalMilliseconds { get; set; }
}
```

外部调用示意：

```csharp
var descriptor = new ResourceDescriptor
{
    Identity = identity,
    DeviceConnectionType = DeviceConnectionType.ModbusTcp,
    ConnectionParameters = new ModbusTcpClientConnectionParameters
    {
        Ip = "192.168.1.10",
        Port = 502,
        ConnectTimeoutMilliseconds = 3000
    }
};
```

这种写法才是真正的“硬编码形式给外部调用”。

## 6. 为什么不再保留兼容层

本次方案明确不保留 `Parameters` 兼容层，原因如下：

- 同时保留两套入口会让文档、测试、工厂分支长期双轨并存
- 第三方接入方会继续沿用旧字典写法，削弱改造效果
- 工厂仍需维护字符串解析逻辑，无法真正简化实现
- API 语义会长期含混，难以明确“推荐入口”到底是哪一个

既然目标是“对外硬编码参数”，那就应当直接让旧入口退出主流程，而不是继续保留。

## 7. 参数模型建议

### 7.1 通用客户端参数基类

对于多个客户端共用的配置，建议抽取公共基类，例如：

- `Ip`
- `Port`
- `ConnectTimeoutMilliseconds`
- `ReceiveTimeoutMilliseconds`
- `SendTimeoutMilliseconds`
- `Retries`
- `ProbeAddress`
- `ProbeDataType`
- `ProbeLength`

如果 .NET Standard 2.0 下不希望引入过深继承层次，也可以直接在各参数类中显式定义，优先保证可读性。

### 7.2 Modbus RTU 参数

建议显式建模以下属性：

- `PortName`
- `BaudRate`
- `DataBits`
- `StopBits`
- `Parity`
- `Handshake`
- `ProbeAddress`
- `ProbeDataType`
- `ProbeLength`

### 7.3 Siemens S7 参数

建议显式建模以下属性：

- `Ip`
- `Port`
- `SiemensVersion`
- `Slot`
- `Rack`
- 通用超时参数
- 读探测参数
- 批量读取间隔参数

### 7.4 Fins 参数

建议显式建模以下属性：

- `Ip`
- `Port`
- `TimeoutMilliseconds`
- 通用重试与探测参数

### 7.5 服务端参数

建议服务端参数对象显式包含：

- `Ip`
- `Port`
- `ConnectTimeoutMilliseconds`
- `ReceiveTimeoutMilliseconds`
- `SendTimeoutMilliseconds`
- `MaxConnections`
- `EnableSnapshotPersistence`

## 8. 工厂改造思路

### 8.1 工厂不再读取字符串字典

改造后默认工厂的职责应变为：

- 根据 `DeviceConnectionType` 判定目标协议
- 校验 `ConnectionParameters` 是否为预期参数类型
- 直接读取强类型属性构造连接对象
- 对必填字段做显式校验

### 8.2 失败场景要更明确

例如：

- `DeviceConnectionType` 为 `ModbusTcp`，但 `ConnectionParameters` 是 `SiemensS7ClientConnectionParameters`
- `Ip` 为空
- `Port` 超出有效范围
- `ProbeLength` 小于 1

这些错误应当直接返回清晰的失败结果，而不是靠字典解析失败后再兜底。

### 8.3 工厂内部示意

示意方向：

```csharp
if (!(descriptor.ConnectionParameters is ModbusTcpClientConnectionParameters parameters))
{
    return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>("ModbusTcp 参数类型不正确");
}
```

这样工厂逻辑会更直观，也更容易被测试覆盖。

## 9. ResourceDescriptor 同步调整建议

### 9.1 属性收口

建议 `ResourceDescriptor` 至少保留：

- `Identity`
- `ResourceRole`
- `ConnectionType`
- `DeviceConnectionType`
- `ConnectionParameters`

### 9.2 是否保留 `ConnectionType`

当前已有 `DeviceConnectionType` 枚举，因此建议将工厂路由完全基于 `DeviceConnectionType`。

`ConnectionType` 如果仍保留，应仅作为展示或序列化字段，不应再作为工厂路由的主要依据。

### 9.3 是否继续保留 `ProtocolType`

`ConnectionIdentity.ProtocolType` 目前仍是字符串。建议本次同时梳理其职责：

- 如果只是展示用途，可以允许继续保留
- 如果参与路由判断，则应同步改为从 `DeviceConnectionType` 派生，避免双源不一致

本次改造至少应保证：连接创建逻辑不再依赖多个平行字符串来源。

## 10. 推荐落地策略

### 10.1 第一阶段：建立强类型参数模型

目标：

- 新增 `IConnectionPoolParameters`
- 新增各协议客户端和服务端参数类
- 修改 `ResourceDescriptor`，移除 `Parameters`

特点：

- 会直接破坏旧调用方式
- 但能一次性完成 API 语义切换

### 10.2 第二阶段：改造默认工厂

目标：

- 客户端默认工厂全部改为消费强类型参数对象
- 服务端默认工厂全部改为消费强类型参数对象
- 删除所有字符串键查找与 `object` 转换逻辑

### 10.3 第三阶段：全面迁移调用方

目标：

- 更新 `ConnectionPool.README.md`
- 更新 `ConnectionPool.ThirdPartyUsage.md`
- 更新测试项目中的连接池示例
- 更新示例程序和第三方接入代码

由于不保留兼容层，这一阶段必须与核心代码升级同步完成。

## 11. 推荐改造范围

如果后续实施代码改造，建议覆盖以下内容：

### 11.1 核心代码

- `ConnectionPool/Models/ResourceDescriptor.cs`
- 新增 `ConnectionPool/Models/Parameters/` 下的参数模型
- `DefaultPooledDeviceClientConnectionFactory`
- `DefaultPooledDeviceServerConnectionFactory`

### 11.2 文档

- `ConnectionPool.ThirdPartyUsage.md`
- `ConnectionPool.README.md`
- 其他涉及连接池注册示例的说明文档

### 11.3 测试

- 连接池工厂集成测试
- 客户端池相关测试
- 服务端池相关测试
- 示例程序中的描述符创建代码

## 12. 兼容性评估

本次方案不兼容现有外部调用方式，影响包括：

- 所有 `descriptor.Parameters["..."] = ...` 写法都需要修改
- 所有依赖字符串键示例的文档都需要改写
- 所有测试数据构造都需要迁移到参数对象
- 第三方使用方升级时必须同步调整代码

换言之，这不是渐进式兼容增强，而是一次明确的 API 升级。

## 13. 风险与注意事项

### 13.1 不要只替换表面 API

如果只是把外部调用改成参数对象，但工厂内部仍偷偷转成字典再解析，那么并没有真正完成改造。

应当做到：

- 对外是强类型参数对象
- 对内也是强类型参数消费逻辑

### 13.2 不要让同一协议出现多个参数入口

例如同时保留：

- `ModbusTcpClientConnectionParameters`
- `Parameters["ip"]`

这种双入口会让后续维护成本持续升高，本次方案应明确禁止。

### 13.3 必须同步补全校验

从字典改为强类型模型后，仍需补齐：

- 必填字段校验
- 默认值策略
- 数值范围检查
- 协议与参数类型匹配检查

### 13.4 文档和测试必须同批次迁移

如果核心实现已不支持 `Parameters`，但 README 或测试还保留旧示例，团队后续会很快出现误用和回退。

## 14. 最终建议

综合当前实现现状，建议如下：

1. 直接放弃 `ResourceDescriptor.Parameters` 的字典参数模型。
2. 以协议维度引入强类型参数对象，作为唯一对外配置入口。
3. 让默认工厂直接消费参数对象，不再保留字符串键查找逻辑。
4. 将文档、测试、示例程序与第三方接入示例一次性迁移到新模型。
5. 把这次改造定义为明确的非兼容升级，而不是兼容层增强。

## 15. 实施检查清单

1. 设计 `IConnectionPoolParameters` 及各协议客户端/服务端参数类。
2. 修改 `ResourceDescriptor`，移除 `Parameters`，改为 `ConnectionParameters`。
3. 改造客户端默认工厂，使用强类型参数对象创建连接。
4. 改造服务端默认工厂，使用强类型参数对象创建连接。
5. 删除旧的字符串键解析辅助逻辑和相关常量依赖。
6. 更新 README、第三方接入文档和示例程序，全部改为强类型硬编码写法。
7. 更新连接池相关测试，全部改为参数对象构造方式。
8. 进行编译、诊断和聚焦测试验证，确认新 API 路径可用。

## 16. 一句话结论

如果目标是“让外部调用方直接以硬编码形式配置连接池参数”，就不应继续围绕字符串键常量化打转，而应直接把 `ResourceDescriptor` 改造成强类型参数入口，并彻底移除 `Parameters` 兼容层。

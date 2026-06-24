# Wombat.Network 引入计划

## 目标

在不重写 `Modbus`、`S7`、`FINS` 协议层的前提下，引入 `Wombat.Network` 重写当前 TCP 适配器，优先提升：

1. 稳定性
2. 性能

路径很简单：不新增并行适配器，直接破坏性更新现有 `TcpClientAdapter` 和 `TcpServerAdapter`。

## 当前现状

当前以太网协议的公共 TCP 路径集中在：

- `Wombat.IndustrialCommunication/Adapter/TcpClientAdapter.cs`
- `Wombat.IndustrialCommunication/Adapter/TcpServerAdapter.cs`
- `Wombat.IndustrialCommunication/DeviceMessageTransport.cs`
- `Wombat.IndustrialCommunication/ServerMessageTransport.cs`

影响范围：

- `ModbusTcpClient`
- `ModbusTcpServer`
- `SiemensClient`
- `S7TcpServer`
- `FinsClient`

所以最小且有效的方案不是“改所有协议”，而是先把这两个 TCP 适配器换掉。

## 当前主要问题

### 1. 客户端接收是轮询式

`TcpClientAdapter.Receive(...)` 依赖：

- `stream.DataAvailable`
- `Task.Delay(20ms)`
- 循环拼满固定长度

问题：

- 20ms 轮询粒度会带来延迟抖动
- 空转等待浪费 CPU
- `DataAvailable` 不是可靠的消息边界依据
- 超时控制分散在适配器和上层两处

### 2. 服务端消息边界处理分散

`TcpServerAdapter` 只负责把收到的字节抛给事件，`ServerMessageTransport` 再额外处理 S7 的 TPKT 重组。

问题：

- TCP 半包/粘包没有在统一层处理
- 现在只显式照顾了 S7
- Modbus TCP、FINS 仍然依赖上层各自兜底
- 会话管理和协议边界混在一起

### 3. TCP 生命周期逻辑重复

`ModbusTcpClient`、`SiemensClient`、`FinsClient` 都各自处理：

- Connect/Disconnect
- 自动重连
- 长短连接
- 超时转译

协议初始化当然各不相同，但底层 TCP 行为不该反复自己实现。

## 引入 Wombat.Network 的原则

`Wombat.Network` 已经把网络模型拆成：

- `Transport`
- `Channel`
- `Protocol`

这里最先要用的是 `Transport`，不是直接照搬 README 里的长度前缀示例。

原因很直接：

- `Modbus TCP` 用 MBAP 自带长度
- `S7` 用 TPKT/COTP 自带长度
- `FINS/TCP` 也有自己的报文结构

所以不能先给现有 PLC 报文外面再套一层 `LengthFieldMessagePipe(FourBytes)`。那会破坏兼容性。

## 两阶段方案

## 第一阶段：重写 `TcpClientAdapter`

目标：先把客户端 TCP 传输替换掉，先拿到最直接的稳定性和延迟收益。

范围：

- 直接重写 `TcpClientAdapter`
- 保持 `IStreamResource` 不变
- 不修改 `ModbusTcpClient`、`SiemensClient`、`FinsClient` 的对外接口

实现方式：

- 内部用 `TcpTransportConnection`
- 映射现有 `ConnectTimeout`
- 映射现有 `ReceiveTimeout`
- 映射现有 `SendTimeout`
- 保持 `Send(...)` / `Receive(...)` / `StreamClose()` 语义兼容

第一阶段前先做的基线验证：

- `TransportTests/TcpClientAdapterTimeoutTests.cs`
- `TransportTests/DeviceMessageTransportTimeoutTests.cs`
- `ModbusTests/ModbusTcpClientTests.cs`
- `PLCTests/S7ProtocolSynchronizationTests.cs`

第一阶段验收标准：

1. 接收超时后连接不被误杀
2. 长连接连续读写无回归
3. `ModbusTcpClient`、`SiemensClient`、`FinsClient` 现有测试通过
4. 平均延迟不劣于当前轮询实现

第一阶段预期收益：

- 去掉 `DataAvailable + Delay(20ms)` 轮询
- 减少小包场景额外等待
- 统一客户端 TCP 关闭和异常转译

## 第二阶段：重写 `TcpServerAdapter` 并下沉消息边界

目标：把服务端 listener/session 生命周期和协议帧边界一起收回来。

范围：

- 直接重写 `TcpServerAdapter`
- 保持 `IServerListener` 事件模型不变
- 收缩 `ServerMessageTransport` 中协议特有的重组补丁

实现方式：

- 内部用 `TcpTransportListener`
- 服务端会话包装继续留在 `TcpServerAdapter` 内部，不引入新的公开类型
- 保持 `DataReceived`、`ClientConnected`、`ClientDisconnected` 事件语义兼容
- 同阶段把协议边界下沉：
  - `Modbus TCP Server`：按 MBAP 长度拆包
  - `S7 TCP Server`：按 TPKT 长度拆包

第二阶段前先做的基线验证：

- `PLCTests/S7TcpServerSnapshotTests.cs`
- `ModbusTests/ModbusTcpServerTests.cs`
- 覆盖半包、粘包、连续多帧同包

第二阶段验收标准：

1. 客户端连接/断开事件语义不变
2. 广播、单会话发送、关闭行为兼容
3. 半包、粘包、连续多帧同包能稳定解析
4. `ServerMessageTransport` 不再承担主要协议重组职责
5. S7 协议同步异常显著减少

第二阶段预期收益：

- 去掉旧式 `BeginAccept/EndAccept`
- 统一服务端连接对象生命周期
- 把协议边界处理从上层补丁移回 TCP/协议层

## 建议改动文件

核心改动：

- `Adapter/TcpClientAdapter.cs`
- `Adapter/TcpServerAdapter.cs`
- `ServerMessageTransport.cs`

测试改动：

- `TransportTests/TcpClientAdapterTimeoutTests.cs`
- `TransportTests/DeviceMessageTransportTimeoutTests.cs`
- 服务端 TCP 相关测试

如果第二阶段需要专门的拆包器，再新增：

- `Protocols/ModbusTcpMessagePipe.cs`
- `Protocols/S7TpktMessagePipe.cs`

## 项目接入方式

优先用本地项目引用：

```xml
<ProjectReference Include="..\..\..\Wombat.Network\Wombat.Network\Wombat.Network.csproj" />
```

原因：

- 迁移期联调更快
- 有兼容性问题时能直接修改底层库

当前源码位置按仓库实际结构使用上面的 `ProjectReference`，不要误按 NuGet 包路径理解。

## 风险与规避

### 风险 1：把 README 示例里的长度前缀直接套到 PLC 协议上

规避：

- 第一阶段只使用 `Transport`
- 第二阶段按 `Modbus TCP` 和 `S7` 自己的长度规则拆包

### 风险 2：服务端事件语义变化

规避：

- 第二阶段保持 `IServerListener` 现有事件模型不变
- 只换底层实现，不改上层调用方式

### 风险 3：为了统一而新增过多抽象

规避：

- 不新增公开适配器类型
- 不做新的统一协议框架
- 只重写现有 TCP 适配器

### 风险 4：`netstandard2.0` 兼容性

规避：

- 先确认 `Wombat.Network` 当前公开 API 在 `netstandard2.0` 下可直接引用
- 如有底层 API 差异，先在 `Wombat.Network` 侧解决，不在本库堆兼容补丁

## 最终建议

按两阶段推进：

1. 第一阶段只动 `TcpClientAdapter`
2. 第二阶段再动 `TcpServerAdapter`，并把 `Modbus TCP`、`S7` 的拆包逻辑一起下沉

这个顺序最短，风险也最低。

跳过项：

- 不做双适配器并存
- 不做一次性全协议重构
- 不在第一阶段碰串口、蓝牙、RTU 路径

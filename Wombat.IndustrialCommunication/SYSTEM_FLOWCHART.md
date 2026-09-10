# Wombat.IndustrialCommunication 系统流程图

>  `Wombat.IndustrialCommunication` 通讯库系统流程图。

## 1. 模块调用总图

```mermaid
flowchart TB
    caller[Caller<br/>库使用方]

    subgraph api[Public API / 公共入口]
        clientApi[IDeviceClient · IReadWrite<br/>客户端读写接口]
        serverApi[IDeviceServer · IServer<br/>服务端接口]
        channelApi[ChannelManager · ChannelRuntime<br/>通道生命周期与重连]
        result[OperationResult<br/>统一结果、报文轨迹与失败原因]
    end

    subgraph client[Client Protocol / 客户端协议]
        readerWriter[DeviceDataReaderWriterBase<br/>类型转换、ReadAsync / WriteAsync]
        modbusClient[ModbusTcpClient · ModbusRtuClient<br/>Modbus 地址解析、报文生成]
        s7Client[SiemensClient · S7Communication<br/>S7 建连、地址解析、报文生成]
        finsClient[FinsClient · FinsCommunication<br/>FINS 地址解析、报文生成]
    end

    subgraph transport[Transport / 报文交换]
        messageTransport[DeviceMessageTransport<br/>UnicastReadMessageAsync / UnicastWriteMessageAsync]
        s7Transport[S7EthernetTransport<br/>S7 响应完整性与 PDU 引用校验]
        finsTransport[FinsEthernetTransport<br/>FINS TCP 帧封装与校验]
    end

    subgraph io[Adapter / I/O 适配器]
        tcpClient[TcpClientAdapter<br/>ConnectAsync / Send / Receive]
        serialClient[SerialPortAdapter<br/>Open / Send / Receive]
        tcpServer[TcpServerAdapter<br/>ListenAsync、会话与事件]
        serialServer[SerialPortServerAdapter<br/>串口监听与事件]
    end

    subgraph server[Server Protocol / 服务端协议]
        serverHost[ServerHost<br/>StartAsync / StopAsync 生命周期串行化]
        serverTransport[ServerMessageTransport<br/>接收队列与 SendToSessionAsync]
        modbusServer[ModbusTcpServer · ModbusRtuServer<br/>HandleModbusMessage]
        s7Server[S7TcpServer<br/>HandleS7Message]
        stores[DataStore · S7DataStore<br/>模拟设备内存]
    end

    device[PLC or Industrial Device<br/>真实 PLC、仪表或远程协议客户端]

    caller -->|调用| clientApi --> readerWriter
    caller -->|创建、执行操作| channelApi
    channelApi -->|创建 concrete IProtocolClient| modbusClient & s7Client & finsClient
    readerWriter --> modbusClient & s7Client & finsClient
    modbusClient --> messageTransport
    s7Client --> s7Transport
    finsClient --> finsTransport
    messageTransport --> tcpClient & serialClient
    s7Transport & finsTransport --> tcpClient
    tcpClient & serialClient -->|发送/接收字节| device

    caller -->|调用| serverApi --> serverHost --> modbusServer & s7Server
    modbusServer & s7Server --> serverTransport
    serverTransport --> tcpServer & serialServer
    tcpServer & serialServer <-->|协议请求/响应| device
    modbusServer & s7Server <-->|读写| stores
    readerWriter & modbusServer & s7Server --> result
```

## 2. 客户端读写调用链

```mermaid
sequenceDiagram
    participant C as Caller
    participant B as DeviceDataReaderWriterBase
    participant P as Protocol Client
    participant T as DeviceMessageTransport
    participant A as TcpClientAdapter or SerialPortAdapter
    participant D as Device

    C->>B: ReadAsync(dataType, address) / WriteAsync(dataType, address, value)
    Note over B: 根据 DataTypeEnums 转换值；将类型化请求下沉为字节读写。
    B->>P: ReadAsync(address, length, dataType) / WriteAsync(address, bytes, dataType)
    Note over P: Modbus 解析站号、功能码、地址；S7/FINS 解析协议地址并构建帧。
    P->>T: UnicastReadMessageAsync(request) / UnicastWriteMessageAsync(request)
    T->>A: Send(request bytes)
    A->>D: TCP 或 Serial 字节流
    D-->>A: response bytes
    A-->>T: Receive response bytes
    Note over T: 校验长度、超时、协议异常；保留 Requsts 与 Responses 报文轨迹。
    T-->>P: OperationResult of IDeviceReadWriteMessage
    P-->>B: OperationResult of byte array / OperationResult
    B-->>C: OperationResult of typed value
```

`S7EthernetTransport` 与 `FinsEthernetTransport` 继承 `DeviceMessageTransport` 的交换职责：前者额外完成 S7 报文长度和 PDU 引用校验，后者处理 FINS TCP 帧。`ModbusTcpClient` 与 `ModbusRtuClient` 使用 `DeviceMessageTransport` 的 TCP/RTU 分支。

## 3. 服务端请求调用链

```mermaid
sequenceDiagram
    participant R as Remote Client
    participant A as TcpServerAdapter or SerialPortServerAdapter
    participant T as ServerMessageTransport
    participant S as ModbusTcpServer / ModbusRtuServer / S7TcpServer
    participant M as DataStore / S7DataStore

    R->>A: protocol request bytes
    A->>T: DataReceived event
    T->>T: OnDataReceived → enqueue ReceivedMessage
    T->>T: MessageProcessingLoop
    T->>S: registered HandleModbusMessage / HandleS7Message
    S->>S: 解析请求并构建响应帧
    S->>M: 读取或写入模拟内存
    M-->>S: 数据或变更结果
    S->>T: SendToSessionAsync(session, response)
    T->>A: session.SendAsync(response)
    A-->>R: protocol response bytes
```

`ModbusTcpServerBase` 和 `S7TcpServerBase` 在构造时通过 `RegisterMessageHandler` 注册各自的处理器；服务端不是通过 `DeviceMessageTransport` 主动轮询设备，而是由适配器事件驱动并读写内存数据存储。

## 4. ChannelManager 调用关系

```mermaid
flowchart LR
    options[ChannelOptions<br/>协议、超时、并发与重连配置]
    manager[ChannelManager]
    runtime[ChannelRuntime]
    protocol[IProtocolClient<br/>具体协议客户端]
    operation[ExecuteAsync delegate]

    options -->|AddAsync| manager
    manager -->|CreateClient| protocol
    manager -->|持有| runtime
    manager -->|ExecuteAsync| runtime
    runtime -->|ConnectAsync；失败后按 ReconnectOptions 重试| protocol
    runtime -->|串行/限流后执行委托| operation
    operation -->|使用具体客户端读写 API| protocol
```

`ChannelManager` 只负责连接状态、并发槽位、超时与重连；它不解析工业协议。`CreateClient` 当前映射到 `ModbusTcpClient`、`ModbusRtuClient`、`SiemensClient` 与 `FinsClient`。

## 5. 模块调用分析

| 模块 | 对外英文接口 | 下游调用 | 职责边界 |
| --- | --- | --- | --- |
| `Abstractions` | `IClient`, `IDeviceClient`, `IDeviceServer`, `IStreamResource` | 由实现类依赖 | 只定义契约，不承载协议逻辑。 |
| `Core` | `DeviceDataReaderWriterBase.ReadAsync`, `WriteAsync` | 协议客户端的受保护读写实现 | 统一类型转换和 `OperationResult` 返回。 |
| `Channels` | `AddAsync`, `ExecuteAsync`, `RestartAsync`, `RemoveAsync` | `IProtocolClient.ConnectAsync` 与使用方委托 | 连接编排，不处理单帧协议。 |
| `Modbus` | `ModbusTcpClient`, `ModbusRtuClient` | `ModbusAddressParser` → 报文对象 → `DeviceMessageTransport` | Modbus TCP/RTU 的读写、批量分块和响应解析。 |
| `PLC/S7` | `SiemensClient`, `S7Communication` | `S7EthernetTransport` → `TcpClientAdapter` | S7 初始化握手、读写报文和响应关联。 |
| `PLC/FINS` | `FinsClient`, `FinsCommunication` | `FinsEthernetTransport` → `TcpClientAdapter` | FINS TCP 读写报文的编码与校验。 |
| `Messaging` | `UnicastReadMessageAsync`, `UnicastWriteMessageAsync` | `IStreamResource.Send` / `Receive` | 将单次请求与响应配对，形成可追踪结果。 |
| `Adapters` | `ConnectAsync`, `Send`, `Receive`, `ListenAsync` | TCP 网络或系统串口 | 只处理字节流和会话事件，不理解 Modbus/S7/FINS。 |
| `Server` | `ServerHost`, `ServerMessageTransport` | 适配器事件 → 协议 Server → DataStore | 管理监听生命周期、消息排队、响应回写。 |
| `Modbus/Data`, `PLC/S7` | `DataStore`, `S7DataStore` | 被对应 Server 调用 | 保存模拟 PLC 状态；不参与客户端真实设备通信。 |

## 6. 已实现协议路径

| 协议 | Client path | Server path | 载体 |
| --- | --- | --- | --- |
| Modbus TCP | `ModbusTcpClient` → `DeviceMessageTransport` → `TcpClientAdapter` | `ModbusTcpServer` → `ServerMessageTransport` → `TcpServerAdapter` | TCP |
| Modbus RTU | `ModbusRtuClient` → `DeviceMessageTransport` → `SerialPortAdapter` | `ModbusRtuServer` → `ServerMessageTransport` → `SerialPortServerAdapter` | Serial |
| Siemens S7 | `SiemensClient` → `S7EthernetTransport` → `TcpClientAdapter` | `S7TcpServer` → `ServerMessageTransport` → `TcpServerAdapter` | TCP |
| Omron FINS | `FinsClient` → `FinsEthernetTransport` → `TcpClientAdapter` | 未实现 | TCP |

# Wombat.IndustrialCommunication System Architecture

This document describes the implemented system as of the current source tree. Mermaid diagrams render in GitHub, GitLab, and current VS Code Markdown preview extensions.

## 1. System Overview

```mermaid
flowchart LR
    operator[Operator / HMI / Integration Client]
    plc[Physical PLC or Industrial Device]
    serial[Serial Modbus Device]
    bluetooth[Bluetooth Modbus RTU Device]

    subgraph apps[Applications]
        gateway[Gateway\nASP.NET Core Minimal API]
        clientTest[ClientTest\nConsole sample]
        serverTest[ServerTest\nS7 virtual-server sample]
        tests[Test project\nxUnit]
    end

    subgraph core[Wombat.IndustrialCommunication]
        channel[ChannelManager\nconnection lifecycle and concurrency]
        protocol[Protocol clients and servers]
        transport[Message transport]
        adapter[TCP / serial adapters]
    end

    subgraph extension[Bluetooth extension]
        btClient[ModbusRtuBluetoothClient]
        btAdapter[BluetoothStreamAdapter]
        btChannel[IBluetoothChannel\nplatform implementation supplied by consumer]
    end

    operator -->|HTTPS REST + JWT| gateway
    gateway --> channel
    gateway --> protocol
    clientTest --> protocol
    serverTest --> protocol
    tests --> core
    channel --> protocol --> transport --> adapter
    adapter -->|TCP| plc
    adapter -->|Serial RTU| serial
    btClient --> btAdapter --> btChannel --> bluetooth
    btClient --> transport
```

## 2. Project Dependencies

```mermaid
flowchart TB
    core[Wombat.IndustrialCommunication\nnetstandard2.0; net10.0]
    bt[Extensions.Bluetooth\nnetstandard2.0; net10.0]
    gateway[Gateway\nnet10.0]
    client[ClientTest\nnet8.0]
    server[ServerTest\nnet8.0]
    test[TestProject\nnet10.0]

    gateway --> core
    bt --> core
    client --> core
    server --> core
    test --> core
    test --> bt
```

## 3. Core Library Layers

```mermaid
flowchart TB
    subgraph api[Public API]
        interfaces[IClient / IDeviceClient / IDeviceServer / IReadWrite]
        result[OperationResult and data models]
        channels[ChannelManager\nChannelOptions / ChannelSnapshot]
    end

    subgraph protocol[Protocol layer]
        modbusClient[ModbusTcpClient\nModbusRtuClient]
        modbusServer[ModbusTcpServer\nModbusRtuServer]
        s7Client[SiemensClient]
        s7Server[S7TcpServer]
        finsClient[FinsClient]
        codec[Address parsers, request/response builders, batch helpers]
        datastore[Modbus DataStore\nS7 DataStore]
    end

    subgraph exchange[Exchange layer]
        readerWriter[DeviceDataReaderWriterBase]
        messageTransport[DeviceMessageTransport\nS7EthernetTransport\nFinsEthernetTransport]
        serverTransport[ServerMessageTransport]
    end

    subgraph io[I/O layer]
        tcpClient[TcpClientAdapter]
        tcpServer[TcpServerAdapter]
        serialClient[SerialPortAdapter]
        serialServer[SerialPortServerAdapter]
        network[Wombat.Network TCP transport]
        port[System.IO.Ports]
    end

    interfaces --> protocol
    channels --> modbusClient & s7Client & finsClient
    modbusClient & s7Client & finsClient --> readerWriter --> messageTransport
    modbusServer & s7Server --> serverTransport
    codec --> modbusClient & modbusServer & s7Client & s7Server & finsClient
    datastore --> modbusServer & s7Server
    messageTransport --> tcpClient & serialClient
    serverTransport --> tcpServer & serialServer
    tcpClient & tcpServer --> network
    serialClient & serialServer --> port
```

### Client request path

```mermaid
sequenceDiagram
    participant Caller
    participant CM as ChannelManager / Client
    participant P as Protocol implementation
    participant MT as DeviceMessageTransport
    participant A as TCP or Serial adapter
    participant D as Device

    Caller->>CM: ReadAsync or WriteAsync
    CM->>CM: wait for operation slot; connect/reconnect if needed
    CM->>P: typed operation and address
    P->>P: parse address; build protocol request
    P->>MT: unicast read/write message
    MT->>A: send frame
    A->>D: TCP or serial bytes
    D-->>A: response bytes
    A-->>MT: received bytes
    MT->>MT: validate frame and protocol error
    MT-->>P: OperationResult and response
    P-->>Caller: converted typed value / write result
```

`ChannelRuntime` owns the connection state machine: `Created -> Connecting -> Online`, with `Faulted -> Reconnecting` on a transport failure. It applies connection, operation, and exponential-retry limits from `ChannelOptions`.

### Server request path

```mermaid
sequenceDiagram
    participant Remote as Remote client
    participant A as TcpServerAdapter / SerialPortServerAdapter
    participant ST as ServerMessageTransport
    participant S as Modbus or S7 server
    participant DS as DataStore

    Remote->>A: protocol request bytes
    A->>ST: connection/data event
    ST->>S: queued request callback
    S->>S: parse request and build response
    S->>DS: read or update simulated memory
    DS-->>S: values / change event
    S->>A: response to session
    A-->>Remote: protocol response bytes
```

Implemented core server types are `ModbusTcpServer`, `ModbusRtuServer`, and `S7TcpServer`; `ServerHost` serializes each server's start/stop lifecycle.

## 4. Gateway Architecture

```mermaid
flowchart TB
    user[Web UI / API consumer]
    device[PLC / serial device / remote Modbus or S7 device]

    subgraph gateway[Wombat.IndustrialCommunication.Gateway]
        api[Minimal API endpoints\nauth, users, channels, points, forwarding, history, logs]
        auth[AuthService\npassword hashing, JWT, refresh tokens]
        authz[Authentication + authorization\npermission policy and handler]
        runtime[GatewayRuntimeService\nBackgroundService]
        history[HistoryLogService\nBackgroundService]
        db[GatewayDb / FreeSql]

        subgraph runtimeDetail[Runtime execution]
            poll[100 ms polling loop]
            cm[ChannelManager\nclient-role channels]
            host[ServerHost\nserver-role channels]
            points[Point read/write, transform, quality]
            forward[Timed / on-change / bidirectional forwarding]
        end
    end

    subgraph persistence[Configured database]
        sqlite[(SQLite by default\nDatabase/gateway.db3)]
        postgres[(PostgreSQL supported\nby FreeSql provider)]
    end

    user -->|REST| api
    api --> auth
    api --> authz
    api --> runtime
    auth --> db
    authz --> db
    api --> db
    runtime --> db
    history --> db
    db --> sqlite
    db -. optional configuration .-> postgres
    runtime --> poll --> points
    points --> cm
    points --> host
    points --> history
    points --> forward --> points
    cm -->|Modbus TCP/RTU, S7, FINS TCP| device
    host -->|Modbus TCP/RTU, S7 server| device
```

### Gateway data model

```mermaid
erDiagram
    USER ||--o{ USER_ROLE : has
    ROLE ||--o{ USER_ROLE : assigns
    ROLE ||--o{ ROLE_PERMISSION : has
    PERMISSION ||--o{ ROLE_PERMISSION : grants
    USER ||--o{ REFRESH_TOKEN : owns
    CHANNEL ||--o{ CHANNEL_POINT : contains
    CHANNEL_POINT ||--o| HISTORY_POLICY : configures
    CHANNEL_POINT ||--o{ HISTORY_SAMPLE : records
    CHANNEL_POINT ||--o{ HISTORY_SAMPLE_AGGREGATE : aggregates
    CHANNEL_POINT ||--o{ CHANNEL_FORWARDING : source
    CHANNEL_POINT ||--o{ CHANNEL_FORWARDING : target
    CHANNEL ||--o{ COLLECTION_LOG : identifies
    CHANNEL_POINT ||--o{ COLLECTION_LOG : identifies
    USER ||--o{ OPERATION_LOG : performs
    USER ||--o{ LOGIN_LOG : authenticates
```

Gateway startup first initializes bootstrap data, then starts the history and runtime hosted services. The runtime loads enabled channel records, creates either a client channel or server host according to `Channel.Role`, and then polls enabled points at their configured `ScanIntervalMs` (minimum 100 ms).

## 5. Protocol and Transport Coverage

| Capability | Client | Server | Carrier |
| --- | --- | --- | --- |
| Modbus TCP | `ModbusTcpClient` | `ModbusTcpServer` | TCP |
| Modbus RTU | `ModbusRtuClient` | `ModbusRtuServer` | Serial |
| Siemens S7 | `SiemensClient` | `S7TcpServer` | TCP |
| Omron FINS | `FinsClient` | Not implemented | TCP |
| Modbus RTU over Bluetooth | `ModbusRtuBluetoothClient` | `ModbusRtuBluetoothServer` | Consumer-provided `IBluetoothChannel` |

The library also contains shared address models for Allen-Bradley and Mitsubishi, but this source tree does not contain corresponding protocol client/server implementations. They are intentionally not shown as supported runtime paths.

## 6. Source Map

| Responsibility | Main source location |
| --- | --- |
| Core channel lifecycle | `Wombat.IndustrialCommunication/Channels` |
| Client/server transports and adapters | `Messaging`, `Adapters`, `Server` |
| Modbus implementation | `Wombat.IndustrialCommunication/Modbus` |
| Siemens S7 implementation | `Wombat.IndustrialCommunication/PLC/S7` |
| Omron FINS implementation | `Wombat.IndustrialCommunication/PLC/FINS` |
| Bluetooth extension | `Wombat.IndustrialCommunication.Extensions.Bluetooth` |
| Gateway web/API/runtime/persistence | `Wombat.IndustrialCommunication.Gateway` |
| Executable samples | `ClientTest`, `ServerTest` |
| Automated tests | `Wombat.IndustrialCommunicationTestProject` |

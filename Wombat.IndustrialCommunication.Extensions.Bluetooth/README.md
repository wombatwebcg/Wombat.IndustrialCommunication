# Wombat.IndustrialCommunication.Extensions.Bluetooth

该扩展包为 `Wombat.IndustrialCommunication` 提供“蓝牙透传 Modbus RTU”能力。  
扩展本身不绑定具体平台蓝牙 SDK，只定义统一蓝牙字节通道接口，由宿主实现平台细节。

## 1. 透传模式总览

蓝牙透传模式的核心是“把 BLE/GATT 的收发特征，抽象成有序字节流”，让 Modbus RTU 协议栈可以直接复用。

链路如下：

`平台蓝牙实现(IBluetoothChannel)`  
-> `BluetoothStreamAdapter(IStreamResource)`  
-> `DeviceMessageTransport`  
-> `ModbusRtuBluetoothClient`

对应到本扩展中的关键类型：

- `IBluetoothChannel`：平台无关的蓝牙字节通道抽象（连接、断开、收发、超时）
- `BluetoothStreamAdapter`：把 `IBluetoothChannel` 适配为主库 `IStreamResource`
- `BluetoothClientFactory`：校验并下发 `BluetoothConnectionOptions`，创建客户端
- `ModbusRtuBluetoothClient`：支持长连接/短连接、自动重连、批量读写
- `BluetoothServerAdapter` + `ModbusRtuBluetoothServer`：蓝牙服务端透传（含 DataStore 快照持久化）

## 2. 必填参数与校验规则

客户端必须提供以下参数（`BluetoothConnectionOptions.Validate()` 会校验）：

- `DeviceId`：目标 BLE 设备标识
- `ServiceId`：业务 Service UUID
- `WriteCharacteristicId`：用于请求写入的 Characteristic UUID
- `NotifyCharacteristicId`：用于响应通知/读取的 Characteristic UUID
- `ConnectTimeout` / `ReceiveTimeout` / `SendTimeout`：必须大于 0
- `ReadChunkSize` / `WriteChunkSize`：必须大于 0（用于宿主分片策略）

> 注意：仅配置 `DeviceId` + `ServiceId` 不足以通信。真实可用链路还需要收发特征 UUID。

## 3. 客户端接入步骤

1. 宿主实现 `IBluetoothChannel`（Windows/Linux/Android 均可）
2. 通过设备发现流程拿到 `DeviceId/ServiceId/WriteCharacteristicId/NotifyCharacteristicId`
3. 构建并校验 `BluetoothConnectionOptions`
4. `BluetoothClientFactory.CreateModbusRtuClient(...)` 创建客户端
5. 按需设置连接策略（`IsLongConnection`、`EnableAutoReconnect` 等）
6. `Connect/ConnectAsync` 后进行 Modbus 读写

最小示例：

```csharp
using System;
using Wombat.IndustrialCommunication.Extensions.Bluetooth;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Factory;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Models;

var options = new BluetoothConnectionOptions
{
    DeviceId = "BLE_DEVICE_ID",
    ServiceId = "0000FF00-0000-1000-8000-00805F9B34FB",
    WriteCharacteristicId = "0000FF01-0000-1000-8000-00805F9B34FB",
    NotifyCharacteristicId = "0000FF02-0000-1000-8000-00805F9B34FB",
    ConnectTimeout = TimeSpan.FromSeconds(5),
    ReceiveTimeout = TimeSpan.FromSeconds(3),
    SendTimeout = TimeSpan.FromSeconds(3)
};

var validate = options.Validate();
if (!validate.IsSuccess)
{
    throw new InvalidOperationException(validate.Message);
}

IBluetoothChannel channel = new YourBluetoothChannelImplementation(options);
var client = BluetoothClientFactory.CreateModbusRtuClient(channel, options);

client.IsLongConnection = true;
client.EnableAutoReconnect = true;
client.MaxReconnectAttempts = 5;
client.ReconnectDelay = TimeSpan.FromSeconds(2);

var connectResult = await client.ConnectAsync();
if (!connectResult.IsSuccess)
{
    throw new InvalidOperationException(connectResult.Message);
}

var readResult = await client.ReadHoldingRegisterAsync(1, 0);
if (readResult.IsSuccess)
{
    ushort value = readResult.ResultValue;
}
```

## 4. 服务端接入步骤（本机发布 BLE 服务）

服务端场景使用 `ModbusRtuBluetoothServer`，底层依赖宿主提供“本机蓝牙服务通道”实现：

```csharp
using System;
using Wombat.IndustrialCommunication.Extensions.Bluetooth;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Models;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Modbus;

var serverOptions = new BluetoothServerOptions
{
    ServiceId = "0000FF00-0000-1000-8000-00805F9B34FB",
    WriteCharacteristicId = "0000FF01-0000-1000-8000-00805F9B34FB",
    NotifyCharacteristicId = "0000FF02-0000-1000-8000-00805F9B34FB",
    ConnectTimeout = TimeSpan.FromSeconds(5),
    ReceiveTimeout = TimeSpan.FromSeconds(3),
    SendTimeout = TimeSpan.FromSeconds(3)
};

var validate = serverOptions.Validate();
if (!validate.IsSuccess)
{
    throw new InvalidOperationException(validate.Message);
}

IBluetoothChannel serverChannel = new YourBluetoothServerChannelImplementation(serverOptions);
var server = new ModbusRtuBluetoothServer(serverChannel)
{
    EnableSnapshotPersistence = true,
    SnapshotSaveInterval = TimeSpan.FromSeconds(5)
};

var listenResult = server.Listen();
if (!listenResult.IsSuccess)
{
    throw new InvalidOperationException(listenResult.Message);
}
```

## 5. 来自 `Wombat.IndustrialCommunication.Tools` 的实战经验

`Wombat.IndustrialCommunication.Tools` 已提供一套可参考的落地流程（桌面端当前为 Windows BLE/GATT）：

- 先扫描设备，再按设备加载 Service，再加载 Characteristic
- 自动选择 `CanWrite=true` 作为写入特征
- 自动选择 `CanNotify=true`（或回退 `CanRead=true`）作为接收特征
- 将上述四元组（设备/服务/写入特征/通知特征）写入 `BluetoothConnectionOptions`
- 再创建 `ModbusRtuBluetoothClient` 并启用自动重连参数

对接自定义宿主时，建议按同样顺序做参数发现与回显，避免用户手填 UUID 导致误配。

## 6. 宿主实现建议

- Windows：可基于 BLE GATT（通知或读轮询）实现 `IBluetoothChannel`
- Linux：可基于 BlueZ D-Bus/GATT 映射到有序字节流
- Android：可基于 `BluetoothGatt` 的写特征 + 通知特征

实现 `IBluetoothChannel` 时建议满足：

- `ReceiveAsync` 在超时前返回“完整可消费字节”，避免碎片化导致 Modbus 错帧
- `SendAsync` 与 `ReceiveAsync` 保证串行化，避免并发读写交叉污染帧
- 当 Notify 不可用时提供 Read 轮询降级策略
- 断链后及时上抛失败，让上层自动重连逻辑生效

## 7. 常见问题

- 现象：连接成功但读写超时  
  排查：确认 `NotifyCharacteristicId` 真实可通知或可读；确认设备端确实返回 RTU 响应帧

- 现象：提示参数无效  
  排查：检查 `DeviceId/ServiceId/WriteCharacteristicId/NotifyCharacteristicId` 是否为空或 UUID 格式错误

- 现象：偶发断连后无法恢复  
  排查：开启 `EnableAutoReconnect`，并适当调大 `ReconnectDelay` 与超时参数

- 现象：读到的数据错帧  
  排查：确保底层通道是“有序字节流”，并按 Modbus RTU 请求-响应节奏完整收发

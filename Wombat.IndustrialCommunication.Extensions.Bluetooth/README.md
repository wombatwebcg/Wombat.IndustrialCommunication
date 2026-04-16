# Wombat.IndustrialCommunication.Extensions.Bluetooth

该扩展包为 `Wombat.IndustrialCommunication` 提供蓝牙透传 Modbus RTU 能力。

## 设计方式

- 扩展包仅提供统一蓝牙通道抽象 `IBluetoothChannel`
- Windows / Linux / Android 由宿主实现 `IBluetoothChannel`
- 通过 `BluetoothStreamAdapter` 适配到主库 `IStreamResource`
- 通过 `ModbusRtuBluetoothClient` 复用 Modbus RTU 协议栈

## 快速接入

1. 在宿主项目实现 `IBluetoothChannel`
2. 构建 `BluetoothConnectionOptions`
3. 使用 `BluetoothClientFactory.CreateModbusRtuClient(...)` 创建客户端
4. 调用 `Connect/ConnectAsync` 后执行 Modbus 读写

## 最小示例

```csharp
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Factory;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Models;

var options = new BluetoothConnectionOptions
{
    DeviceId = "00:11:22:33:44:55",
    ServiceId = "00001101-0000-1000-8000-00805F9B34FB"
};

IBluetoothChannel channel = new YourBluetoothChannelImplementation(options);
var client = BluetoothClientFactory.CreateModbusRtuClient(channel, options);

var connectResult = await client.ConnectAsync();
if (connectResult.IsSuccess)
{
    var read = await client.ReadHoldingRegisterAsync(1, 0);
}
```

## 三平台实现建议

- Windows：基于 RFCOMM 或 BLE GATT，实现字节流收发
- Linux：基于 BlueZ 用户态能力，实现字节通道映射
- Android：基于 BluetoothSocket 或 GATT Characteristic 通道

## 常见问题

- 连接成功但读取超时：检查设备服务 UUID 与串口参数映射是否一致
- 偶发断连：提高 `ConnectTimeout` / `ReceiveTimeout` 并启用自动重连
- 数据错帧：确保底层通道是有序字节流并按请求完整返回响应帧

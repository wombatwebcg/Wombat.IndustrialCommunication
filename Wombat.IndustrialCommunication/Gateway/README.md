# Wombat.IndustrialCommunication.Gateway

网关接口层，提供简化的工业通信设备访问API，方便网关应用接入各种工业设备。

## 基本概念

- `IGatewayDeviceFactory`: 网关设备工厂接口，用于创建各种工业通信设备的网关访问实例
- `IGatewayDevice`: 网关设备接口，提供统一的设备操作API
- `GatewayDeviceFactory`: 网关设备工厂实现类，封装IDeviceFactory接口
- `GatewayDevice`: 网关设备实现类，封装IDeviceClient接口

## 快速开始

### 1. 创建网关设备工厂

```csharp
// 方式1：直接创建工厂实例
var gatewayFactory = GatewayExtensions.CreateGatewayDeviceFactory();

// 方式2：使用依赖注入
services.AddIndustrialGateway();
// 然后在需要的地方注入
public class MyService
{
    private readonly IGatewayDeviceFactory _gatewayFactory;
    
    public MyService(IGatewayDeviceFactory gatewayFactory)
    {
        _gatewayFactory = gatewayFactory;
    }
}
```

### 2. 创建设备连接

```csharp
// 创建Modbus TCP设备
var modbusDevice = gatewayFactory.CreateModbusTcpDevice("192.168.1.100", 502);

// 创建西门子S7设备
var siemensDevice = gatewayFactory.CreateSiemensDevice("192.168.1.200", 102, SiemensVersion.S7_1200);

// 创建带连接池的Modbus TCP设备
var modbusTcpWithPool = gatewayFactory.CreateModbusTcpDeviceWithPool(10, "192.168.1.100", 502);
```

### 3. 连接设备

```csharp
// 连接设备
var result = modbusDevice.Connect();
if (result.IsSuccess)
{
    Console.WriteLine("设备连接成功");
}
else
{
    Console.WriteLine($"设备连接失败：{result.Message}");
}

// 异步连接
var connectResult = await siemensDevice.ConnectAsync();
```

### 4. 读写数据

```csharp
// 读取单个值
var boolResult = modbusDevice.ReadBoolean("0");
if (boolResult.IsSuccess)
{
    Console.WriteLine($"读取结果：{boolResult.ResultValue}");
}

// 批量读取
var addresses = new Dictionary<string, DataTypeEnums>
{
    { "DB1.0", DataTypeEnums.Int16 },
    { "DB1.2", DataTypeEnums.Float },
    { "DB2.0", DataTypeEnums.Bool }
};
var batchResult = siemensDevice.BatchRead(addresses);
if (batchResult.IsSuccess)
{
    foreach (var item in batchResult.ResultValue)
    {
        Console.WriteLine($"地址：{item.Key}，值：{item.Value}");
    }
}

// 写入单个值
var writeResult = modbusDevice.Write("100", true);
if (writeResult.IsSuccess)
{
    Console.WriteLine("写入成功");
}

// 批量写入
var writeValues = new Dictionary<string, object>
{
    { "DB1.0", (short)100 },
    { "DB1.2", 123.45f },
    { "DB2.0", true }
};
var batchWriteResult = siemensDevice.BatchWrite(writeValues);
```

### 5. 断开连接和资源释放

```csharp
// 断开连接
modbusDevice.Disconnect();

// 释放资源
modbusDevice.Dispose();
siemensDevice.Dispose();
```

## 连接池使用

对于需要频繁访问同一设备的场景，可以使用连接池提高性能：

```csharp
// 创建带连接池的设备
var pooledDevice = gatewayFactory.CreateModbusTcpDeviceWithPool(10, "192.168.1.100", 502);

// 使用方式与普通设备相同
var readResult = pooledDevice.ReadInt32("100");

// 使用完毕后释放连接池
pooledDevice.Dispose();
```

## 异步操作

所有操作都提供了同步和异步版本：

```csharp
// 异步读取
var intResult = await modbusDevice.ReadInt32Async("100");

// 异步写入
await modbusDevice.WriteAsync("200", 12345);

// 异步批量读取
var batchResultAsync = await siemensDevice.BatchReadAsync(addresses);
``` 
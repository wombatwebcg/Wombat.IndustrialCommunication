# ConnectionPool 使用说明

## 目标
`ConnectionPool` 用于统一管理多设备连接，提供复用、失效、回收与池化执行能力，适配 Modbus TCP/RTU、S7、FINS 客户端。

## 关键组件
- `DeviceConnectionPool`：连接池主入口，负责注册、获取、释放、失效、回收、执行。
- `DefaultPooledDeviceConnectionFactory`：根据连接描述创建协议对应包装连接。
- `PooledOperationExecutor`：统一执行重试、重连与故障分类。

## 快速开始
```csharp
var options = new ConnectionPoolOptions
{
    MaxConnections = 128,
    IdleTimeout = TimeSpan.FromMinutes(2),
    LeaseTimeout = TimeSpan.FromSeconds(30),
    MaxRetryCount = 2,
    RetryBackoff = TimeSpan.FromMilliseconds(200)
};

var factory = new DefaultPooledDeviceConnectionFactory();
var pool = new DeviceConnectionPool(options, factory);

var descriptor = new DeviceConnectionDescriptor
{
    Identity = new ConnectionIdentity
    {
        DeviceId = "modbus-1",
        ProtocolType = "ModbusTcp",
        Endpoint = "192.168.1.10:502"
    },
    DeviceConnectionType = DeviceConnectionType.ModbusTcp
};
descriptor.Parameters["ip"] = "192.168.1.10";
descriptor.Parameters["port"] = 502;
pool.Register(descriptor);

var readResult = await pool.ExecuteAsync(
    descriptor.Identity,
    async client => client.ReadUInt16Async("1;3;0"));
```

## 点位列表读写
连接池现在支持直接接收点位列表请求，适合上层按“点位配置集合”统一读写，而不必手动拼装执行委托。

```csharp
var points = new List<DevicePointReadRequest>
{
    new DevicePointReadRequest
    {
        Name = "温度",
        Address = "1;3;0",
        DataType = DataTypeEnums.UInt16,
        EnableBatch = true
    },
    new DevicePointReadRequest
    {
        Name = "状态字",
        Address = "1;3;10",
        DataType = DataTypeEnums.UInt16,
        Length = 4,
        EnableBatch = false
    }
};

var pointReadResult = await pool.ReadPointsAsync(descriptor.Identity, points);
if (pointReadResult.IsSuccess)
{
    foreach (var item in pointReadResult.ResultValue)
    {
        Console.WriteLine($"{item.Name}: {item.Value}");
    }
}

var writePoints = new List<DevicePointWriteRequest>
{
    new DevicePointWriteRequest
    {
        Name = "启动命令",
        Address = "1;5;0",
        DataType = DataTypeEnums.Bool,
        EnableBatch = true,
        Value = true
    },
    new DevicePointWriteRequest
    {
        Name = "设定值",
        Address = "1;3;20",
        DataType = DataTypeEnums.UInt16,
        EnableBatch = true,
        Value = (ushort)1500
    }
};

var pointWriteResult = await pool.WritePointsAsync(descriptor.Identity, writePoints);
```

### 点位列表约束
- `Name` 为空时自动回退为 `Address`。
- `Length > 1` 时按数组/连续块读取；标量点位可保持默认值 `1`。
- `EnableBatch = true` 时会优先尝试客户端 `BatchReadAsync/BatchWriteAsync`，用于提升标量点位列表的读写效率。
- 仅标量、非字符串、非重复地址点位会参与批量；字符串、数组点位会自动回退到逐点读写。
- 如果某次批量调用失败，连接池会自动回退到逐点执行，兼顾效率与兼容性。
- `DataTypeEnums.String` 读取时必须提供有效 `Length`，写入时使用字符串专用写入分支。
- 返回结果会逐项保留 `IsSuccess`、`Message` 与 `Value`，便于上层按点位处理局部失败。

## 常用参数
- `connectTimeoutMilliseconds`：连接超时（工厂参数）。
- `receiveTimeoutMilliseconds`：接收超时（工厂参数）。
- `sendTimeoutMilliseconds`：发送超时（工厂参数）。
- `retries`：底层客户端重试次数（工厂参数）。
- `MaxRetryCount`：池级可恢复故障重试次数（池配置）。
- `RetryBackoff`：池级重试退避时间（池配置）。

## 回收策略
- `CleanupIdle()` 会回收空闲且无活跃租约的连接条目。
- 连接失效后可通过 `Invalidate(identity, reason)` 阻止新租约获取。

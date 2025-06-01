# Wombat.IndustrialCommunication

开源的基础工业通讯库，支持多种工业通讯协议，如Modbus、西门子S7、三菱Q3E二进制、FX编程口协议、汇川H3U等。

## 项目简介

Wombat.IndustrialCommunication是一个基于.NET Standard 2.0的开源工业通讯库，旨在提供简单、高效、可靠的工业设备通讯解决方案。该库封装了各种工业协议的通讯细节，使开发者能够通过简单的API与各类PLC和工业设备进行通信。

本项目可用于工业自动化、数据采集、远程监控、设备管理等多种工业场景，支持客户端和服务器双向通讯。

## 特点

- **多协议支持**：支持Modbus TCP/RTU、西门子S7、三菱PLC等多种主流工业协议
- **统一接口**：提供统一的API接口，降低学习成本
- **双模式支持**：支持长连接和短连接两种通讯模式
- **自动重连**：内置连接断开自动重连机制，提高通讯可靠性
- **异步支持**：全面支持异步操作，提升性能和并发能力
- **服务端实现**：同时提供客户端和服务端实现，支持完整的通讯测试环境
- **可观察数组**：集成ObservableArray实现数据变化实时监测
- **线程安全**：内部实现保证线程安全，适合多线程环境
- **完善测试**：包含丰富的测试用例，确保库的可靠性和稳定性
- **跨平台**：基于.NET Standard 2.0，可在Windows、Linux、macOS等平台使用

## 支持的协议和设备

### Modbus 协议
- Modbus TCP客户端/服务端
- Modbus RTU客户端/服务端
- 支持所有标准功能码
- 支持线圈、离散输入、保持寄存器、输入寄存器操作
- 新增IModbusClient接口，提供类型安全的寄存器读写操作

### 西门子PLC
- S7协议
- 支持S7-1200、S7-1500等系列PLC
- 支持DB块、输入、输出、内存、计数器、定时器等区域的读写操作
- 支持S7服务端模拟

### 三菱PLC
- 支持Q3E二进制协议
- 支持FX编程口协议
- 支持各种数据区域的读写操作

### 汇川PLC
- 支持H3U系列PLC通讯
- 支持数据读写操作

### 其他设备
- 可扩展的设备接口
- 统一的消息传输机制

## 安装

### 通过NuGet安装

```
Install-Package Wombat.IndustrialCommunication
```

或者在Visual Studio的NuGet包管理器中搜索`Wombat.IndustrialCommunication`进行安装。

### 从源码编译

1. 克隆仓库
```
git clone https://github.com/wombatwebcg/Wombat.IndustrialCommunication.git
```

2. 在Visual Studio中打开解决方案文件 `Wombat.IndustrialCommunication.sln`

3. 编译解决方案

4. 在您的项目中引用生成的DLL文件

### 依赖项

本项目依赖以下NuGet包：
- System.IO.Ports (>= 9.0.5)
- System.Reactive (>= 6.0.1)
- Wombat.Extensions.DataTypeExtensions (>= 1.0.12)
- Wombat.Network (>= 1.1.4)

## 使用示例

### Modbus TCP客户端示例

#### 使用原始地址字符串方式

```csharp
using System;
using Wombat.IndustrialCommunication.Modbus;

// 创建Modbus TCP客户端
var client = new ModbusTcpClient("192.168.0.100", 502);

try
{
    // 连接到设备
    var connectResult = client.Connect();
    if (!connectResult.IsSuccess)
    {
        Console.WriteLine($"连接失败: {connectResult.Message}");
        return;
    }
    
    // 读取保持寄存器
    // 地址格式: "站号;功能码;寄存器地址" - "1;3;0" 表示站号1，功能码3(读保持寄存器)，地址0
    var readResult = client.ReadInt16("1;3;0");
    if (readResult.IsSuccess)
    {
        // 处理读取结果
        Console.WriteLine($"读取成功: 值 = {readResult.ResultValue}");
    }
    else
    {
        Console.WriteLine($"读取失败: {readResult.Message}");
    }
    
    // 读取多个保持寄存器
    var readMultipleResult = client.ReadInt16("1;3;0", 10); // 读取10个连续的Int16值
    if (readMultipleResult.IsSuccess)
    {
        Console.WriteLine("批量读取成功:");
        foreach (var value in readMultipleResult.ResultValue)
        {
            Console.WriteLine($"值: {value}");
        }
    }
    
    // 读取线圈状态
    var readCoilResult = client.ReadBoolean("1;1;0"); // 站号1，功能码1(读线圈)，地址0
    if (readCoilResult.IsSuccess)
    {
        Console.WriteLine($"线圈状态: {readCoilResult.ResultValue}");
    }
    
    // 写入单个保持寄存器
    var writeResult = client.Write("1;6;0", (short)123); // 站号1，功能码6(写单个寄存器)，地址0
    if (writeResult.IsSuccess)
    {
        Console.WriteLine("写入成功");
    }
    else
    {
        Console.WriteLine($"写入失败: {writeResult.Message}");
    }
    
    // 写入单个线圈
    var writeCoilResult = client.Write("1;5;0", true); // 站号1，功能码5(写单个线圈)，地址0
    if (writeCoilResult.IsSuccess)
    {
        Console.WriteLine("线圈写入成功");
    }
    
    // 写入多个寄存器值
    var writeMultipleResult = client.Write("1;16;0", new short[] { 1, 2, 3, 4, 5 }); // 站号1，功能码16(写多个寄存器)，地址0
    if (writeMultipleResult.IsSuccess)
    {
        Console.WriteLine("批量写入成功");
    }
}
finally
{
    // 断开连接并释放资源
    client.Disconnect();
    client.Dispose();
}
```

#### 使用新的IModbusClient接口方式

```csharp
using System;
using Wombat.IndustrialCommunication.Modbus;

// 创建Modbus TCP客户端
var client = new ModbusTcpClient("192.168.0.100", 502);

try
{
    // 连接到设备
    var connectResult = client.Connect();
    if (!connectResult.IsSuccess)
    {
        Console.WriteLine($"连接失败: {connectResult.Message}");
        return;
    }
    
    // 读取单个线圈 - 新接口方式
    var coilResult = client.ReadCoil(1, 0); // 站号1，地址0
    if (coilResult.IsSuccess)
    {
        Console.WriteLine($"线圈状态: {coilResult.Content}");
    }
    
    // 批量读取线圈 - 新接口方式
    var coilsResult = client.ReadCoils(1, 0, 8); // 站号1，起始地址0，读取8个
    if (coilsResult.IsSuccess)
    {
        Console.WriteLine("线圈状态批量读取:");
        for (int i = 0; i < coilsResult.Content.Length; i++)
        {
            Console.WriteLine($"线圈[{i}] = {coilsResult.Content[i]}");
        }
    }
    
    // 读取单个保持寄存器 - 新接口方式
    var registerResult = client.ReadHoldingRegister(1, 100); // 站号1，地址100
    if (registerResult.IsSuccess)
    {
        Console.WriteLine($"保持寄存器值: {registerResult.Content}");
    }
    
    // 批量读取保持寄存器 - 新接口方式
    var registersResult = client.ReadHoldingRegisters(1, 100, 10); // 站号1，起始地址100，读取10个
    if (registersResult.IsSuccess)
    {
        Console.WriteLine("保持寄存器批量读取:");
        for (int i = 0; i < registersResult.Content.Length; i++)
        {
            Console.WriteLine($"寄存器[{100 + i}] = {registersResult.Content[i]}");
        }
    }
    
    // 写入单个保持寄存器 - 新接口方式
    var writeResult = client.WriteHoldingRegister(1, 200, 42); // 站号1，地址200，值42
    if (writeResult.IsSuccess)
    {
        Console.WriteLine("写入保持寄存器成功");
    }
    
    // 写入多个保持寄存器 - 新接口方式
    var writeRegistersResult = client.WriteHoldingRegisters(1, 200, new ushort[] { 1, 2, 3, 4, 5 }); // 站号1，起始地址200
    if (writeRegistersResult.IsSuccess)
    {
        Console.WriteLine("批量写入保持寄存器成功");
    }
    
    // 写入单个线圈 - 新接口方式
    var writeCoilResult = client.WriteCoil(1, 10, true); // 站号1，地址10，值true
    if (writeCoilResult.IsSuccess)
    {
        Console.WriteLine("写入线圈成功");
    }
    
    // 写入多个线圈 - 新接口方式
    var writeCoilsResult = client.WriteCoils(1, 10, new bool[] { true, false, true }); // 站号1，起始地址10
    if (writeCoilsResult.IsSuccess)
    {
        Console.WriteLine("批量写入线圈成功");
    }
}
finally
{
    // 断开连接并释放资源
    client.Disconnect();
    client.Dispose();
}
```

### Modbus RTU客户端示例

#### 使用原始地址字符串方式

```csharp
using System;
using System.IO.Ports;
using Wombat.IndustrialCommunication.Modbus;

// 创建Modbus RTU客户端
var client = new ModbusRTUClient("COM1", 9600, 8, Parity.None, StopBits.One);

try
{
    // 连接到设备
    var connectResult = client.Connect();
    if (!connectResult.IsSuccess)
    {
        Console.WriteLine($"连接失败: {connectResult.Message}");
        return;
    }
    
    // 读取线圈状态 (功能码1)
    var readCoilsResult = client.ReadBoolean("1;1;0", 8); // 站号1，功能码1(读线圈)，起始地址0，长度8
    if (readCoilsResult.IsSuccess)
    {
        // 处理读取结果
        Console.WriteLine("读取线圈成功:");
        for (int i = 0; i < readCoilsResult.ResultValue.Length; i++)
        {
            Console.WriteLine($"线圈[{i}] 状态: {readCoilsResult.ResultValue[i]}");
        }
    }
    else
    {
        Console.WriteLine($"读取失败: {readCoilsResult.Message}");
    }
    
    // 读取保持寄存器 (功能码3)
    var readRegistersResult = client.ReadUInt16("1;3;0", 5); // 站号1，功能码3(读保持寄存器)，起始地址0，长度5
    if (readRegistersResult.IsSuccess)
    {
        Console.WriteLine("读取寄存器成功:");
        for (int i = 0; i < readRegistersResult.ResultValue.Length; i++)
        {
            Console.WriteLine($"寄存器[{i}] 值: {readRegistersResult.ResultValue[i]}");
        }
    }
    
    // 读取浮点数值 (两个连续的寄存器组成一个浮点数)
    var readFloatResult = client.ReadFloat("1;3;0"); // 站号1，功能码3(读保持寄存器)，起始地址0
    if (readFloatResult.IsSuccess)
    {
        Console.WriteLine($"浮点数值: {readFloatResult.ResultValue}");
    }
    
    // 写入单个线圈 (功能码5)
    var writeCoilResult = client.Write("1;5;0", true); // 站号1，功能码5(写单个线圈)，地址0
    if (writeCoilResult.IsSuccess)
    {
        Console.WriteLine("写入线圈成功");
    }
    
    // 写入单个寄存器 (功能码6)
    var writeRegisterResult = client.Write("1;6;0", (ushort)12345); // 站号1，功能码6(写单个寄存器)，地址0
    if (writeRegisterResult.IsSuccess)
    {
        Console.WriteLine("写入寄存器成功");
    }
    
    // 写入多个寄存器 (功能码16)
    var writeMultipleResult = client.Write("1;16;0", new int[] { 11111, 22222, 33333 }); // 站号1，功能码16(写多个寄存器)，地址0
    if (writeMultipleResult.IsSuccess)
    {
        Console.WriteLine("批量写入成功");
    }
}
finally
{
    // 断开连接并释放资源
    client.Disconnect();
    client.Dispose();
}
```

#### 使用新的IModbusClient接口方式

```csharp
using System;
using System.IO.Ports;
using Wombat.IndustrialCommunication.Modbus;

// 创建Modbus RTU客户端
var client = new ModbusRTUClient("COM1", 9600, 8, Parity.None, StopBits.One);

try
{
    // 连接到设备
    var connectResult = client.Connect();
    if (!connectResult.IsSuccess)
    {
        Console.WriteLine($"连接失败: {connectResult.Message}");
        return;
    }
    
    // 读取线圈状态 - 新接口方式
    var coilResult = client.ReadCoil(1, 0); // 站号1，地址0
    if (coilResult.IsSuccess)
    {
        Console.WriteLine($"线圈状态: {coilResult.Content}");
    }
    
    // 批量读取线圈 - 新接口方式
    var coilsResult = client.ReadCoils(1, 0, 8); // 站号1，起始地址0，读取8个
    if (coilsResult.IsSuccess)
    {
        Console.WriteLine("线圈状态批量读取:");
        for (int i = 0; i < coilsResult.Content.Length; i++)
        {
            Console.WriteLine($"线圈[{i}] = {coilsResult.Content[i]}");
        }
    }
    
    // 读取离散输入 - 新接口方式
    var inputResult = client.ReadDiscreteInput(1, 100); // 站号1，地址100
    if (inputResult.IsSuccess)
    {
        Console.WriteLine($"离散输入状态: {inputResult.Content}");
    }
    
    // 读取保持寄存器 - 新接口方式
    var registerResult = client.ReadHoldingRegister(1, 0); // 站号1，地址0
    if (registerResult.IsSuccess)
    {
        Console.WriteLine($"保持寄存器值: {registerResult.Content}");
    }
    
    // 批量读取保持寄存器 - 新接口方式
    var registersResult = client.ReadHoldingRegisters(1, 0, 5); // 站号1，起始地址0，读取5个
    if (registersResult.IsSuccess)
    {
        Console.WriteLine("保持寄存器批量读取:");
        for (int i = 0; i < registersResult.Content.Length; i++)
        {
            Console.WriteLine($"寄存器[{i}] = {registersResult.Content[i]}");
        }
    }
    
    // 读取输入寄存器 - 新接口方式
    var inputRegisterResult = client.ReadInputRegister(1, 0); // 站号1，地址0
    if (inputRegisterResult.IsSuccess)
    {
        Console.WriteLine($"输入寄存器值: {inputRegisterResult.Content}");
    }
    
    // 写入单个保持寄存器 - 新接口方式
    var writeResult = client.WriteHoldingRegister(1, 0, 12345); // 站号1，地址0，值12345
    if (writeResult.IsSuccess)
    {
        Console.WriteLine("写入保持寄存器成功");
    }
    
    // 写入多个保持寄存器 - 新接口方式
    var writeRegistersResult = client.WriteHoldingRegisters(1, 0, new ushort[] { 11111, 22222, 33333 }); // 站号1，起始地址0
    if (writeRegistersResult.IsSuccess)
    {
        Console.WriteLine("批量写入保持寄存器成功");
    }
    
    // 写入单个线圈 - 新接口方式
    var writeCoilResult = client.WriteCoil(1, 0, true); // 站号1，地址0，值true
    if (writeCoilResult.IsSuccess)
    {
        Console.WriteLine("写入线圈成功");
    }
}
finally
{
    // 断开连接并释放资源
    client.Disconnect();
    client.Dispose();
}
```

### 异步操作示例

#### 使用新的IModbusClient接口进行异步操作

```csharp
using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Modbus;

public async Task ModbusAsyncExample()
{
    // 创建Modbus TCP客户端
    var client = new ModbusTcpClient("192.168.0.100", 502);
    
    try
    {
        // 异步连接
        var connectResult = await client.ConnectAsync();
        if (!connectResult.IsSuccess)
        {
            Console.WriteLine($"连接失败: {connectResult.Message}");
            return;
        }
        
        // 异步读取线圈 - 新接口方式
        var coilResult = await client.ReadCoilAsync(1, 0); // 站号1，地址0
        if (coilResult.IsSuccess)
        {
            Console.WriteLine($"线圈状态: {coilResult.Content}");
        }
        
        // 异步批量读取保持寄存器 - 新接口方式
        var registersResult = await client.ReadHoldingRegistersAsync(1, 100, 10); // 站号1，起始地址100，读取10个
        if (registersResult.IsSuccess)
        {
            Console.WriteLine("保持寄存器批量读取:");
            for (int i = 0; i < registersResult.Content.Length; i++)
            {
                Console.WriteLine($"寄存器[{100 + i}] = {registersResult.Content[i]}");
            }
        }
        
        // 异步写入保持寄存器 - 新接口方式
        var writeResult = await client.WriteHoldingRegisterAsync(1, 200, 42); // 站号1，地址200，值42
        if (writeResult.IsSuccess)
        {
            Console.WriteLine("写入保持寄存器成功");
        }
        
        // 异步批量写入线圈 - 新接口方式
        var writeCoilsResult = await client.WriteCoilsAsync(1, 10, new bool[] { true, false, true }); // 站号1，起始地址10
        if (writeCoilsResult.IsSuccess)
        {
            Console.WriteLine("批量写入线圈成功");
        }
    }
    finally
    {
        // 异步断开连接
        await client.DisconnectAsync();
        client.Dispose();
    }
}
```

### 西门子S7客户端示例

```csharp
using System;
using Wombat.IndustrialCommunication.PLC;

// 创建西门子S7-1200客户端
var client = new SiemensClient("192.168.0.100", 102, SiemensVersion.S7_1200);

try
{
    // 连接到PLC
    var connectResult = client.Connect();
    if (!connectResult.IsSuccess)
    {
        Console.WriteLine($"连接失败: {connectResult.Message}");
        return;
    }
    
    // 读取DB块数据
    var readResult = client.ReadBytes("DB1.0", 10);
    if (readResult.IsSuccess)
    {
        // 处理读取结果
        Console.WriteLine("读取成功:");
        foreach (var b in readResult.Data)
        {
            Console.WriteLine($"字节值: {b}");
        }
    }
    else
    {
        Console.WriteLine($"读取失败: {readResult.Message}");
    }
    
    // 写入DB块数据
    byte[] data = new byte[] { 1, 2, 3, 4, 5 };
    var writeResult = client.Write("DB1.0", data);
    if (writeResult.IsSuccess)
    {
        Console.WriteLine("写入成功");
    }
    else
    {
        Console.WriteLine($"写入失败: {writeResult.Message}");
    }
    
    // 读取布尔量
    var readBoolResult = client.ReadBool("M0.0");
    if (readBoolResult.IsSuccess)
    {
        Console.WriteLine($"M0.0的值: {readBoolResult.Data}");
    }
    
    // 写入布尔量
    var writeBoolResult = client.Write("M0.0", true);
    if (writeBoolResult.IsSuccess)
    {
        Console.WriteLine("布尔量写入成功");
    }
}
finally
{
    // 断开连接并释放资源
    client.Disconnect();
    client.Dispose();
}
```

## 项目结构

项目主要由以下几个部分组成：

### 核心组件

- **IClient**：客户端通用接口
- **IDeviceClient**：设备客户端通用接口
- **IDeviceFactory**：设备工厂接口
- **INetworkSession**：网络会话接口
- **IStreamResource**：数据流资源接口

### Modbus协议实现

- **ModbusTcpClient**：Modbus TCP客户端实现
- **ModbusRTUClient**：Modbus RTU客户端实现
- **ModbusTcpServer**：Modbus TCP服务端实现
- **ModbusRtuServer**：Modbus RTU服务端实现
- **IModbusClient**：新增Modbus客户端专用接口，提供更直接的寄存器访问

### PLC协议实现

- **S7**：西门子S7协议实现
  - **SiemensClient**：西门子S7客户端
  - **S7TcpServer**：S7服务端实现
  - **S7DataStore**：S7数据存储实现

### 工具类

- **ObservableArray**：可观察数组，用于数据变化监测
- **DeviceMessageTransport**：设备消息传输基类
- **ServerMessageTransport**：服务器消息传输基类
- **DeviceDataReaderWriterBase**：设备数据读写基类

## 贡献指南

欢迎为Wombat.IndustrialCommunication项目做出贡献！

### 如何贡献

1. Fork本仓库
2. 创建您的特性分支 (`git checkout -b feature/amazing-feature`)
3. 提交您的更改 (`git commit -m 'Add some amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 开启Pull Request

### 贡献类型

您可以通过以下方式为项目做出贡献：

- 添加新的工业协议支持
- 改进现有协议的性能和稳定性
- 修复Bug
- 完善文档
- 添加单元测试
- 提供使用示例

### 开发指南

- 请确保您的代码符合项目的代码风格
- 添加足够的单元测试来验证您的更改
- 确保所有测试都能通过
- 更新相关文档

## 许可证

本项目采用MIT许可证 - 详情请参见 [LICENSE.md](LICENSE.md) 文件。

MIT License

Copyright (c) 2023 wombatwebcg

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

## 联系方式

- **GitHub**: [wombatwebcg](https://github.com/wombatwebcg)
- **项目地址**: [Wombat.IndustrialCommunication](https://github.com/wombatwebcg/Wombat.IndustrialCommunication)

如果您有任何问题、建议或反馈，请通过GitHub Issues提交。

## 致谢

感谢所有为本项目做出贡献的开发者。

### 相关项目

- [Wombat.Network](https://github.com/wombatwebcg/Wombat.Network) - 网络通信库
- [Wombat.Extensions.DataTypeExtensions](https://github.com/wombatwebcg/Wombat.Extensions.DataTypeExtensions) - 数据类型扩展库

### 参考资料

- Modbus协议规范
- 西门子S7通信协议
- 三菱PLC通信协议文档
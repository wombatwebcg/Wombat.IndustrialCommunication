# FINS协议客户端实现计划

## 项目背景
基于现有S7协议的分层架构模式，重构OmronFinsClient为符合项目架构标准的FINS协议客户端实现。

## 架构分析

### S7协议架构模式
- **SiemensClient**: 主客户端类，继承S7Communication，实现IDeviceClient接口
- **S7Communication**: 核心通信基类，继承DeviceDataReaderWriterBase
- **S7EthernetTransport**: 传输层实现，继承DeviceMessageTransport
- **S7ReadRequest/S7ReadResponse**: 请求响应消息类，实现IDeviceReadWriteMessage
- **SiemensAddress**: 地址解析类

### 现有OmronFinsClient问题
- 直接继承SocketBase，不符合项目分层架构
- 缺少传输层抽象
- 缺少请求响应消息封装
- 地址解析与通信逻辑耦合

### FINS协议报文格式
- **基础命令头**: FINS魔数(0x46494E53) + 长度 + 命令字段 + 错误码 + 节点地址
- **读取命令**: ICF + RSV + GCT + 网络地址 + 节点编号 + 单元地址 + 命令码 + 内存区域码 + 地址 + 长度
- **写入命令**: 类似读取，但包含数据部分

## 实施计划

### 文件结构设计
```
FINS/
├── FinsClient.cs              // 主客户端类
├── FinsCommunication.cs       // 核心通信基类
├── FinsEthernetTransport.cs   // 传输层实现
├── FinsReadRequest.cs         // 读取请求消息
├── FinsReadResponse.cs        // 读取响应消息
├── FinsWriteRequest.cs        // 写入请求消息
├── FinsWriteResponse.cs       // 写入响应消息
├── FinsAddress.cs             // 地址解析类
├── FinsCommonMethods.cs       // 通用方法类
└── FinsConstants.cs           // 常量定义
```

### 实施检查清单

1. **创建FinsConstants.cs** - 定义FINS协议常量和枚举
2. **创建FinsAddress.cs** - 实现地址解析和转换逻辑
3. **创建FinsCommonMethods.cs** - 实现通用方法和工具函数
4. **创建FinsReadRequest.cs** - 实现读取请求消息类
5. **创建FinsReadResponse.cs** - 实现读取响应消息类
6. **创建FinsWriteRequest.cs** - 实现写入请求消息类
7. **创建FinsWriteResponse.cs** - 实现写入响应消息类
8. **创建FinsEthernetTransport.cs** - 实现传输层逻辑
9. **创建FinsCommunication.cs** - 实现核心通信基类
10. **创建FinsClient.cs** - 实现主客户端类

### 技术要求
- 遵循项目现有的分层架构模式
- 实现IDeviceClient接口
- 支持异步操作
- 包含完整的错误处理
- 支持连接管理和重连机制
- 兼容现有的数据类型系统
- 提供批量读写功能

### 质量标准
- 代码注释使用中文
- 遵循项目命名约定
- 实现完整的单元测试覆盖
- 性能优化和内存管理
- 线程安全设计
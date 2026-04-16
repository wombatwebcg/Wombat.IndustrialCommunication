# Wombat.IndustrialCommunication.Extensions.Bluetooth 规格说明

## 1. 背景与目标
当前主库已经具备串口与 TCP 适配能力，Modbus RTU/TCP 的上层读写逻辑通过 `IStreamResource + DeviceMessageTransport + ModbusRtuClientBase/ModbusTcpClientBase` 复用。

本扩展包目标是新增“蓝牙透传 Modbus RTU”能力，并作为独立扩展项目维护在：

`Wombat.IndustrialCommunication.Extensions.Bluetooth`

要求支持 Windows / Linux / Android 三端场景，且不破坏主库现有协议实现。

## 2. 范围

### 2.1 本次包含
- 在扩展包中实现蓝牙传输抽象与适配器，兼容主库 `IStreamResource`
- 基于 `ModbusRtuClientBase` 封装蓝牙版 RTU 客户端
- 提供可注入的跨平台蓝牙通道抽象，由宿主或平台子包实现底层蓝牙连接
- 输出基础工厂与使用文档

### 2.2 本次不包含
- 在主库中修改现有串口/TCP 逻辑
- 提供单个项目内的全平台原生蓝牙实现
- 蓝牙服务端（从站）能力

## 3. 约束与兼容性
- 目标框架：`netstandard2.0`
- 语法与 API：仅使用 `netstandard2.0` 可用能力
- 依赖策略：优先复用主库能力，不引入与三平台深度绑定且不可裁剪的依赖
- 行为一致性：超时、重试、连接状态语义与现有 `SerialPortAdapter` / `TcpClientAdapter` 对齐

## 4. 设计总览

### 4.1 分层设计
- **平台蓝牙层（外部实现）**：负责 RFCOMM/BLE 串流连接建立与字节收发
- **扩展适配层（本项目）**：`IBluetoothChannel` + `BluetoothStreamAdapter`
- **协议传输层（复用主库）**：`DeviceMessageTransport`
- **Modbus RTU 层（复用主库）**：`ModbusRtuClientBase`

### 4.2 核心原则
- 通过接口注入隔离平台差异
- 通过 `IStreamResource` 接入主库传输链，不复制协议栈
- 先保证透传稳定性，再扩展平台特性

## 5. 关键接口与模型

### 5.1 `IBluetoothChannel`
用于抽象蓝牙通道，建议包含：
- `bool Connected { get; }`
- `TimeSpan ReceiveTimeout { get; set; }`
- `TimeSpan SendTimeout { get; set; }`
- `Task<OperationResult> ConnectAsync(CancellationToken cancellationToken)`
- `Task<OperationResult> DisconnectAsync()`
- `Task<OperationResult<int>> ReceiveAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)`
- `Task<OperationResult> SendAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)`

### 5.2 `BluetoothConnectionOptions`
用于统一连接参数，至少包含：
- 设备标识（MAC/地址/平台设备 ID）
- 服务标识（SPP UUID / BLE Service + Characteristic 标识）
- 连接超时、发送超时、接收超时
- 读分片大小与可选写分片大小

### 5.3 `BluetoothStreamAdapter : IStreamResource`
职责：
- 将 `IBluetoothChannel` 映射到 `IStreamResource` 语义
- 处理 `Send/Receive` 参数合法性检查
- 实现 `StreamClose`、`Dispose` 与并发安全

### 5.4 `ModbusRtuBluetoothClient : ModbusRtuClientBase`
职责：
- 组合 `DeviceMessageTransport(new BluetoothStreamAdapter(...))`
- 对齐现有 RTU 客户端可见能力：连接、断开、超时、重试、长短连接策略
- 输出一致的 `OperationResult` 语义

## 6. 跨平台策略（Windows/Linux/Android）
- 扩展包只定义统一蓝牙通道接口，不直接绑定某单一平台 SDK
- Windows/Linux/Android 分别在宿主项目实现 `IBluetoothChannel`
- 若后续需要官方实现，可新增平台子包：
  - `Wombat.IndustrialCommunication.Extensions.Bluetooth.Windows`
  - `Wombat.IndustrialCommunication.Extensions.Bluetooth.Linux`
  - `Wombat.IndustrialCommunication.Extensions.Bluetooth.Android`

## 7. 错误处理与可靠性
- 所有连接/读写失败统一转换为 `OperationResult` 失败，不泄露平台底层异常细节
- 发送与接收超时使用取消令牌控制，并保证可中断
- 通道断开后可执行重连策略，重连前确保状态清理
- 日志接口对齐主库 `ILogger` 风格

## 8. 测试策略
- 单元测试使用 Fake `IBluetoothChannel`，验证：
  - 正常透传读写
  - 分片与拼包行为
  - 超时与取消
  - 断线重连路径
  - 重试次数与间隔策略
- 回归保证：不修改主库行为，不影响已有 Modbus RTU/TCP 测试

## 9. 交付物
- `IBluetoothChannel` 抽象
- `BluetoothConnectionOptions` 模型
- `BluetoothStreamAdapter`
- `ModbusRtuBluetoothClient`
- `BluetoothClientFactory`
- 使用文档与示例
- 测试代码

## 10. 验收标准
- 扩展包可独立编译并被主解决方案引用
- 在 Fake 通道下通过透传测试
- 关键错误路径可返回明确失败信息
- 使用方可通过实现 `IBluetoothChannel` 接入任意平台蓝牙栈
- 主库现有测试不出现回归失败

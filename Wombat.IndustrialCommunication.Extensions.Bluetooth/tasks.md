# Wombat.IndustrialCommunication.Extensions.Bluetooth 实施任务

## T1 项目配置
- 更新扩展项目 `csproj`
- 添加主库项目引用
- 补充 NuGet 打包元数据
- 确认目标框架保持 `netstandard2.0`

## T2 蓝牙通道抽象
- 新建 `Abstractions/IBluetoothChannel.cs`
- 定义连接、断开、发送、接收、超时与连接状态能力
- 与 `OperationResult` 统一失败语义

## T3 连接参数模型
- 新建 `Models/BluetoothConnectionOptions.cs`
- 定义设备标识、服务标识、超时与分片参数
- 增加必要的参数合法性校验

## T4 适配层实现
- 新建 `Adapter/BluetoothStreamAdapter.cs`
- 实现 `IStreamResource`
- 对接 `IBluetoothChannel`
- 处理并发锁、资源释放、StreamClose 行为

## T5 Modbus 客户端实现
- 新建 `Modbus/ModbusRtuBluetoothClient.cs`
- 继承 `ModbusRtuClientBase`
- 构造函数注入 `IBluetoothChannel`
- 暴露连接控制与关键传输配置

## T6 工厂入口
- 新建 `Factory/BluetoothClientFactory.cs`
- 提供标准化构造方法
- 统一默认超时与重试配置初始化

## T7 文档
- 新建或更新扩展包 `README.md`
- 给出 Windows/Linux/Android 接入步骤
- 给出最小可运行示例与常见错误说明

## T8 测试
- 在测试项目新增 `ModbusBluetoothClientTests.cs`
- 使用 Fake `IBluetoothChannel` 编写单元测试
- 覆盖成功路径、超时、断连、重试、异常映射

## T9 验证与收尾
- 构建扩展项目
- 运行相关测试
- 修复编译与测试问题
- 产出最终集成说明

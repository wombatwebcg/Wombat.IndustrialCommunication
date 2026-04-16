# Wombat.IndustrialCommunication.Extensions.Bluetooth 检查清单

## 规格完整性
- [x] 已定义目标与边界（包含项/不包含项）
- [x] 已明确 `netstandard2.0` 约束
- [x] 已明确跨平台策略（Windows/Linux/Android）
- [x] 已明确验收标准

## 接口与模型
- [x] `IBluetoothChannel` 方法与属性完整
- [x] `BluetoothConnectionOptions` 字段可覆盖主流蓝牙连接场景
- [x] 参数校验策略已定义

## 适配器实现
- [x] `BluetoothStreamAdapter` 完整实现 `IStreamResource`
- [x] `Send/Receive` 的 offset/size 检查完整
- [x] 超时行为与取消语义清晰
- [x] 释放与断连行为可重复调用且安全

## Modbus 客户端
- [x] `ModbusRtuBluetoothClient` 复用 `ModbusRtuClientBase`
- [x] 连接、断开、重连策略与现有 RTU 客户端一致
- [x] 重试与响应间隔配置可用

## 文档与示例
- [x] README 覆盖三平台接入说明
- [x] README 提供最小示例
- [x] README 包含常见错误与排查建议

## 测试与验证
- [x] 新增 Fake 通道单元测试
- [x] 透传读写测试通过
- [ ] 超时、取消、断线重连测试通过
- [ ] 主库现有测试未回归
- [x] 解决方案构建通过

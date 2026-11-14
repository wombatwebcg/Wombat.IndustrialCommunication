# IEC104 → Modbus TCP 桥接：运维手册

适用位置：`Wombat.IndustrialCommunication` 包的 `Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge` 命名空间（2026-11 起并入核心程序集）　　配套文档：`IEC104_Modbus桥接_部署说明.md`（寄存器表、编码、命令协议）

## 1. 职责边界

桥接进程做两件事：把 104 主站缓存镜像到 Modbus 寄存器；把 Modbus 写入转成 104 遥控。它不缓存历史、不做报警、不做规约转换之外的计算。104 侧断链时数据冻结在最后值，是否可信由健康区回答。

## 2. 启停顺序

启动：

1. 先建 104 主站连接（`Iec104Master.ConnectAsync`），确认 STARTDT 完成；
2. 再启动桥接（`Iec104ModbusBridge.StartAsync`），它会初始化镜像、开监听、起刷写循环。

顺序反过来也能启动，但下游会在 104 建链前读到全 0，健康区 0=0 能看出来。

停止：`StopAsync` 内部按“命令通道 → 刷写循环 → 健康区写 0 → 关端口”的顺序收尾。不要直接杀进程，否则下游会看到连接被重置而不是状态 0。

重启注意：如果点表改过，健康区 5（点表版本）会变，必须与发布记录比对后再让下游继续读。

## 3. 配置文件

一个文件搞定两边的参数。相对路径按配置文件所在目录解析，方便把 CSV 和配置放一起。

```jsonc
{
  // Modbus 从站与地址基准
  "modbus": {
    "bindAddress": "0.0.0.0",
    "port": 502,
    "unitId": 1,
    "discreteBase": 0,
    "telemetryBase": 0,
    "commandBase": 0,
    "healthBase": 1000,
    "wordOrder": "Abcd",          // Abcd | Cdab
    "flushIntervalMs": 50
  },

  // 命令通道，默认关闭
  "command": {
    "enabled": false,
    "mode": "SelectAndExecute",    // SelectAndExecute | Direct
    "allowedIoa": [ 24577, 24578 ],// 为空表示不限制，不建议
    "confirmTimeoutSeconds": 30,
    "selectTimeoutSeconds": 15
  },

  // 断链读策略
  "stale": {
    "readMode": "LastValue",       // LastValue | Exception
    "timeoutSeconds": 30
  },

  // 104 主站
  "iec104": {
    "host": "192.168.1.10",
    "port": 2404,
    "commonAddress": 1,
    "k": 12,
    "w": 8,
    "connectTimeoutSeconds": 10,
    "t1Seconds": 15,
    "t2Seconds": 10,
    "t3Seconds": 20,
    "interrogationTimeoutSeconds": 30,
    "commandTimeoutSeconds": 15,
    "autoReconnect": true,
    "reconnectDelaySeconds": 5,
    "cyclicInterrogationSeconds": 900,
    "interrogateOnConnect": true
  },

  // 点表
  "pointTable": {
    "telemetryPath": "遥测.csv",
    "discretePath": "遥信.csv",
    "commandPath": "遥控.csv",
    "ioaBase": 1,
    "commandTypeId": "SingleCommand"
  }
}
```

加载方式：

```csharp
var configuration = ModbusBridgeConfiguration.Load("bridge.json");
configuration.EnsureValid();                      // 有问题一次性抛出全部
var options = configuration.CreateOptions();
var table = configuration.LoadPointTable();
var masterOptions = configuration.CreateMasterOptions(table);
```

校验覆盖：端口与从站号范围、基准地址非负与越界、超时非正、白名单为空值、CSV 文件不存在、JSON 语法错误（带行号与列号）。转发号重复、地址溢出、点类型不支持在构造桥接（`ModbusRegisterMap.Build`）时统一报出。

启动时把校验结果和点表版本写进日志：

```
桥接映射就绪：遥测 818 点 / 遥信 298 点 / 遥控 4 点，点表版本 3F2A
```

## 4. 映射表导出

`ModbusRegisterMap.ExportCsv()` 生成寄存器表，列固定为：区域、地址、寄存器数、转发号、IOA、类型、描述。`ExportCsv(path)` 直接写文件（UTF-8）。

用途：交付给组态方建点、现场比对点表版本、排查地址错位。发布前把导出的 CSV 随版本归档。

## 5. 日志

| 事件 | 级别 | 关键文字 |
|---|---|---|
| 桥接启动 | Info | `桥接已启动` |
| 映射就绪与版本 | Info | `桥接映射就绪` / `点表版本` |
| 初始镜像完成 | Info | `初始镜像完成` |
| 104 链路变化 | Info | `104 链路状态变为` |
| 命令受理与结果 | Info | `遥控审计` |
| 白名单拒绝 | Warning | `不在白名单内` |
| 确认超时 | Warning | `确认超时` |
| 从站启动失败 | Error | `Modbus 从站启动失败` |

排查时先看链路状态日志，再看健康区，最后才看命令审计。

## 6. 监控指标

轮询是唯一可靠的监控方式，健康区（默认 4x 1000..1007）：

| 地址 | 判定 |
|---|---|
| 1000 | 2 正常；0 或 1 表示 104 侧不通 |
| 1001/1002 | 超过 3 个周期没有回落说明数据停了 |
| 1005 | 与发布记录不一致说明点表被改 |
| 1006 | 必须是 1 |

建议下游每 5 秒读一次健康区，作为独立的“数据可信”通道；不要把数据区数值范围当健康判据。

## 7. 端口与权限

- Modbus 502 属于特权端口。Linux 用 `AmbientCapabilities=CAP_NET_BIND_SERVICE` 或改用 1502/5020；Windows 用管理员启动或用高端口。
- 104 出站 2404 只需普通权限。
- Modbus 无认证，只绑内网网卡。若必须跨网段，靠防火墙白名单限定源地址，不要做端口映射到公网。
- 一个 Modbus 连接就是全部权限，命令区一旦开放写，任何能连上的客户端都能发遥控。这是阶段二白名单存在的原因。

## 8. 容量与调优

实测口径：3 个客户端 × 1 秒周期 × 1116 点。每个周期是 1 次离散输入读 + 14 次 125 寄存器读 + 1 次健康区读，共 16 个请求/客户端/秒。

- `flushIntervalMs` 默认 50ms。104 突发上千点时，每条变化不会单独加锁写，最多延迟一个周期。
- 单次刷写上限 2000 点，超出的留到下一周期；正常点位规模不会触发。
- 单次读最多 125 个寄存器。1636 个遥测必须分 14 次，组态侧按 125 分批是最省事的写法。
- 想跑满 10 分钟：`BRIDGE_CAPACITY_SECONDS=600 dotnet test --filter Category=Capacity`。

## 9. 应急预案

**104 断链**

1. 健康区 0 变 0，数据区冻结；
2. 确认 104 主机 2404 可达、主站日志有重连；
3. 恢复后主站自动重连并总召，桥接在链路激活时全量重点一遍；
4. 若长时间不恢复，下游应切到“数据不可信”状态，不要继续用冻结值做判断。

**点表变更**

1. 停桥接 → 更新 CSV → 启动；
2. 对比健康区 5 与发布记录；
3. 用导出的 CSV 通知组态方核对地址；
4. 转发号只追加不插队；插队会导致后续全部地址平移。

**命令异常**

- 状态 7：操作员超时未确认，属正常，重新走请求；
- 状态 6、原因 1：设备拒绝，多为选择过期或设备不允许该操作；先看 104 侧遥控点是否被闭锁；
- 状态 6、原因 3：链路不可用，检查健康区 0；
- 状态 8：命令通道没开，属于配置问题；
- 状态一直停在 1/4：104 侧没有回确认，查 `FrameTraced` 与设备侧遥控记录。

**进程崩溃**

Modbus 数据区在内存里，重启后先全 0，等 104 总召或突发补齐。健康区 1/2 的秒数从 0 重新计，下游应把“刚重启”视为数据不可信，直到秒数稳定。

## 10. 常见故障

见 `IEC104_Modbus桥接_部署说明.md` 第 11 节“故障排查”表；本手册只补运维视角的处理顺序：先健康区，再日志，最后点表与组态。

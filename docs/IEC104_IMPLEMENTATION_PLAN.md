# IEC 60870-5-104 主站/从站 实现方案

> 约束：**本工程不引用 Wombat.IndustrialCommunication**，只依赖 Wombat.Network 与 Wombat.Extensions.DataTypeExtensions。
> 因此不使用 IStreamResource / OperationResult / IClient / IReadWrite / ChannelManager / DeviceServerBase，全部自洽实现。

## 1. 依赖与目标框架

| 依赖 | 用途 |
|---|---|
| Wombat.Network 1.1.9 | TcpTransportListener / TcpTransportConnection，提供 IDuplexPipe |
| Wombat.Extensions.DataTypeExtensions 1.1.1 | 端序转换、DataTypeEnums |
| Microsoft.Extensions.Logging.Abstractions | 日志接口 |
| System.IO.Pipelines（传递依赖） | PipeReader 拆帧，避免“读满 N 字节”语义 |
| Pipelines.Sockets.Unofficial（传递依赖） | 需要时把 IDuplexPipe 包成 Stream 便于调试 |

TargetFrameworks: netstandard2.0;net8.0，LangVersion 10，Nullable disable（与主库一致）。
工程当前未加入 sln，需要补。

## 2. 目录结构

```
Wombat.IndustrialCommunication.Extensions.IEC104/
  Protocol/                      纯函数，无 IO，可单测
    Iec104Constants.cs           端口 2404、APDU 上限、默认 k/w/t0-t3
    ApciFrame.cs                 0x68 + len + 4 字节控制域，I/S/U 三种
    Iec104FrameParser.cs         流式拆帧（含重同步）
    Asdu.cs                      TypeId / VSQ / COT / CA / 信息对象
    AsduBuilder.cs               组帧：单对象、连续寻址 SQ=1
    InformationObject.cs         SIQ/DIQ/QDS/浮点/命令/CP56Time2a
    Iec104TypeId.cs              类型标识枚举 + 元素长度表
    Iec104Cause.cs               传送原因枚举
    QualityDescriptor.cs         IV/NT/SB/BL/OV
    Cp56Time2a.cs                7 字节时标
  Model/
    Iec104Point.cs               点位定义（CA/IOA/TypeId/系数/偏移/死区/极性/SOE）
    Iec104PointTable.cs          点表 + 转发号→IOA 映射 + CSV 导入
    Iec104Value.cs               值 + 品质 + 时标 + 接收时间
    Iec104CommandResult.cs       遥控结果（COT + P/N）
    Iec104InterrogationResult.cs 总召结果（点计数、耗时、缺失点）
  Transport/
    Iec104Link.cs                封装 Wombat.Network 连接，暴露读/写/关闭
    SendPump.cs                  单写者发送泵（Channel<byte[]>）
  Session/
    Iec104Session.cs             序号、k/w 窗口、t1/t2/t3、总召流程
    Iec104MasterSession.cs
    Iec104SlaveSession.cs
  Options/
    Iec104MasterOptions.cs
    Iec104SlaveOptions.cs
  Iec104Master.cs                主站
  Iec104Slave.cs                 从站
  Diagnostics/
    Iec104TraceEventArgs.cs      收发帧追踪（HEX + 语义）
```

## 3. 帧格式（实现要点）

APDU 最大 255 字节：0x68 | len | 控制域(4) | ASDU(len-4)，len 上限 253。
ASDU 头 6 字节：TypeId(1) + VSQ(1) + COT(2) + CA(2)。

单帧对象上限（SQ=0）：

| 类型 | 每对象字节 | 上限/帧 | 818 遥测 | 298 遥信 |
|---|---|---|---|---|
| 1 单点 | 4 | 60 | — | 5 帧 |
| 13 短浮点 | 8 | 30 | 28 帧 | — |
| 30 带时标单点 | 11 | 22 | — | 14 帧 |

拆帧算法（Iec104FrameParser）：

1. 缓冲不足 2 字节 → 等。
2. buffer[0] != 0x68 → 逐字节前移寻找下一个 0x68，记一次协议违例（丢帧计数）。
3. total = buffer[1] + 2；buffer[1] < 4 → 视为非法，跳过 2 字节重同步。
4. 缓冲不足 total → 等。
5. 切出整帧，返回。

**未知 TypeId**：元素长度未知，无法安全跳过。策略：整帧丢弃 + 记录 UnknownType 统计，不断链。

## 4. 传输层

不复用 core 的适配器。直连 Wombat.Network：

```csharp
// 主站
var conn = await TcpTransportConnection.ConnectAsync(endPoint, cancellationToken: ct);
await conn.StartAsync(ct);
var input  = conn.Transport.Input;    // PipeReader
var output = conn.Transport.Output;   // PipeWriter

// 从站
var listener = new TcpTransportListener(endPoint, backlog);
await listener.StartAsync(ct);
var conn = (TcpTransportConnection)await listener.AcceptAsync(ct);
await conn.StartAsync(ct);
```

读循环：ReadAsync → 循环 TryParseFrame → AdvanceTo。**只有这一个读者**。

写：所有出站帧进 Channel<byte[]>，由单一 SendPump 消费并写 PipeWriter。序号 N(S) 只在发送泵内递增，天然避免竞态；读循环与定时器只入队。

## 5. 会话状态机

### 5.1 共用

| 字段 | 说明 |
|---|---|
| SendSeq / RecvSeq | 15 位，0..32767 回绕 |
| UnackedCount | 已发未确认 I 帧数，达 k 停发 |
| RecvSinceAck | 已收未确认 I 帧数，达 w 发 S 帧 |
| DataTransferActive | STARTDT 之后才允许 I 帧 |
| LastSendUtc / LastRecvUtc | t3 判定 |
| PendingT1 | 待确认帧的时间戳，t1 超时断链 |

定时器：单循环 200ms tick，用 Stopwatch 单调时间，不用 DateTime.Now。
事件处理顺序固定：先处理 U 帧（STARTDT/TESTFR/STOPDT）→ 再处理 S 帧（清未确认）→ 最后处理 I 帧（更新 N(R)、解析 ASDU）。

### 5.2 主站

1. TCP 连上 → 发 STARTDT act（0x07）→ 等 con（0x0B），超时 t1 视为失败。
2. 收 TESTFR act → 回 con；t3 到 → 主动发 TESTFR act，t1 内无 con 则断链。
3. 总召：100 / COT=6 / QOI=20 → 收 COT=7 开始 → 累积 COT=20 数据 → COT=10 结束；整体超时 = max(30s, 帧数×t1/2)。
4. 遥控：45（单点）或 46（双点），S/E=1 选择 → 等 COT=7 且 P/N=0 → S/E=0 执行 → 等 COT=7 + COT=10；选择态 30s 过期。
5. 收值：写 ConcurrentDictionary<int, Iec104Value>（键 IOA；多 CA 时键为 (CA,IOA)）→ 触发 ValueChanged。
6. 断链：LinkStateChanged 事件；可选 AutoReconnect（默认关）。没有 core 的 ChannelManager 兜底，重连策略必须显式配置。

### 5.3 从站

1. 未 STARTDT 前只处理 U 帧，I/S 帧丢弃（记违例）。
2. STARTDT act → 回 con → 可选发 M_EI_NA_1(70) / COT=4 初始化结束（国内设备普遍要求）。
3. 总召：回 COT=7 → 按点表分帧发 COT=20 → 发 COT=10。总召期间默认暂停周期上送与突发，完成后再恢复（可配置允许插入）。
4. 周期上送：按 Iec104SlaveOptions.CyclicPeriod 全量或分组发送。
5. 突发上送：值变化且超过死区才发，COT=3；SOE 点用 30 型带 CP56Time2a。
6. 遥控：校验 CA/IOA/类型 → 触发 CommandRequested（可取消，取消则回 COT=7 + P/N=1）→ 成功回 COT=7，执行完成回 COT=10。
7. 流控：未确认 I 帧达 k 时停止上送并等待 S 帧；长时间无确认按 t1 断链。

## 6. 公开 API

```csharp
public sealed class Iec104Master : IAsyncDisposable
{
    public Iec104Master(Iec104MasterOptions options, ILogger logger = null);

    public bool IsConnected { get; }
    public Iec104MasterOptions Options { get; }

    public Task ConnectAsync(CancellationToken ct = default);
    public Task DisconnectAsync();

    public Task<Iec104InterrogationResult> InterrogateAsync(CancellationToken ct = default);

    public Task<Iec104CommandResult> SingleCommandAsync(
        int ioa, bool on, Iec104CommandMode mode = Iec104CommandMode.Direct, CancellationToken ct = default);

    public Task<Iec104CommandResult> DoubleCommandAsync(
        int ioa, Iec104DoubleCommand state, Iec104CommandMode mode = Iec104CommandMode.Direct, CancellationToken ct = default);

    public bool TryGetValue(int ioa, out Iec104Value value);
    public IReadOnlyDictionary<int, Iec104Value> Snapshot();

    public event EventHandler<Iec104ValueEventArgs> ValueChanged;
    public event EventHandler<Iec104LinkEventArgs> LinkStateChanged;
    public event EventHandler<Iec104TraceEventArgs> FrameTraced;
}
```

```csharp
public sealed class Iec104Slave : IAsyncDisposable
{
    public Iec104Slave(Iec104SlaveOptions options, ILogger logger = null);

    public bool IsListening { get; }
    public IReadOnlyList<Iec104ClientInfo> Clients { get; }

    public Task StartAsync(CancellationToken ct = default);
    public Task StopAsync();

    public void UpdateValue(int ioa, double value);          // 触发突发/COS
    public void UpdateDiscrete(int ioa, bool state);
    public void SetInvalid(int ioa, bool invalid);           // 品质 IV
    public void SetAll(IEnumerable<KeyValuePair<int, Iec104Value>> values);

    public event EventHandler<Iec104CommandRequestEventArgs> CommandRequested;
    public event EventHandler<Iec104ClientEventArgs> ClientConnected;
    public event EventHandler<Iec104ClientEventArgs> ClientDisconnected;
    public event EventHandler<Iec104TraceEventArgs> FrameTraced;
}
```

无 core 的 OperationResult，错误模型：

- 连接/协议/超时 → 抛 Iec104Exception（含 Kind 枚举：Transport / Protocol / Timeout / LinkNotActive）。
- 遥控被拒 → 不抛，返回 Iec104CommandResult { Accepted=false, Cause=..., Negative=true }。
- 值查询 → TryGetValue，不抛。

## 7. 点表与 CSV 导入

```csharp
public sealed class Iec104Point
{
    public int CommonAddress { get; set; } = 1;
    public int Ioa { get; set; }
    public Iec104TypeId TypeId { get; set; }
    public int ForwardNo { get; set; }        // CSV 转发号
    public string Channel { get; set; }
    public string Device { get; set; }
    public string Description { get; set; }
    public double Scale { get; set; } = 1;
    public double Offset { get; set; }
    public double Deadband { get; set; }
    public bool Invert { get; set; }          // 反极性取反
    public bool IsSoe { get; set; }
    public int InterrogationGroup { get; set; } = 1;
}
```

导入规则：

- UTF-8 BOM、CRLF、无引号（当前三张表均满足），手写解析，不引入 CsvHelper。
- 中文类型名 → TypeId 映射：不带时标的短浮点数测量值→13、单点→1、双点→3、带时标的单点→30、带时标的短浮点→36。
- IOA 映射两种模式：
  - Offset：Ioa = ForwardNo + options.IoaBase（猜测主机是 +1，待实测）。
  - Explicit：由探测结果生成 Dictionary<int,int>。
- 死区为 0 时按量程 0.5% 兜底（来自 CSV 全 0 的现状），并在导入报告里提示。

## 8. 测试

新增独立测试工程 Wombat.IndustrialCommunication.Extensions.IEC104.Tests（xunit 2.9.3，net8.0），不引用核心库。

协议单元测试：
- APCI 三种帧编解码往返；长度边界 253；N(S)/N(R) 32767→0 回绕。
- ASDU：SQ=0/SQ=1 组帧、COT 2 字节高低位、CA 2 字节、IOA 3 字节。
- CP56Time2a：毫秒跨 60000、IV/SU 位、2000 年基准。
- 拆帧：半包、粘包、0x68 出现在负载中的重同步、未知 TypeId 丢弃。
- 流控：k 满停发、w 满发 S 帧。

回环集成测试（本机随机端口）：
- 从站起 818 遥测 + 298 遥信 → 主站总召 → 点数、值、帧数完全一致。
- 从站突发 1000 次 → 主站按死区收到，无乱序、无序号错。
- 遥控选择/执行/拒绝三条路径。
- 主站不响应 TESTFR → 从站 t1 断链。

## 9. 里程碑

| 阶段 | 内容 | 验收 |
|---|---|---|
| M1 | Protocol 全部 + 单元测试 | 编解码测试全绿 |
| M2 | 主站 + 探测工具 | 能连 2404 主机跑完总召并导出实际 IOA/TypeId |
| M3 | 从站（总召/周期/突发/遥控） | 自研主站回环通过，第三方主站可读 |
| M4 | SOE、双点、品质、流控强化 | 加压不丢帧、不断链 |
| M5 | CSV 点表导入 + Gateway 桥接示例 | 三张表导入并映射 |

M2 的探测工具建议放在 samples/Iec104Probe（控制台，net8.0），输出示例：

```
CA=1  STARTDT ok
总召: 收到 28 帧 / 818 遥测
TypeId 统计: 13=818
IOA 区间: 16385..17202 (连续)
遥信: 5 帧 / 298 点, TypeId=30 (带时标)
未知类型: 无
```

## 10. 待确认

1. 主机 IOA 基线：转发号+1 还是分段（遥信/遥测/遥控各一段）。探测一次即可确定。
2. 遥信实际类型：1 型还是 30 型（CSV 写“单点”但标了 SOE=是）。
3. 是否要求 M_EI_NA_1(70) 初始化结束帧。
4. 从站是否需要时钟同步 C_CS_NA_1(103)。
5. 是否需要内置自动重连（无 core 的 Channel，必须自带）。
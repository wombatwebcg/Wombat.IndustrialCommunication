using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus.Data;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// S7 TCP服务器，用于模拟西门子S7 PLC
    /// </summary>
    public class S7TcpServer : S7TcpServerBase, IDeviceServer
    {
        private readonly TcpServerAdapter _tcpServerAdapter;
        private readonly ServerMessageTransport _serverTransport;
        private const int DEFAULT_TIMEOUT_MS = 3000;
        private readonly object _snapshotSyncRoot = new object();
        private Timer _snapshotTimer;
        private volatile bool _snapshotDirty;
        private bool _enableSnapshotPersistence;
        
        /// <summary>
        /// IP终结点
        /// </summary>
        public IPEndPoint IPEndPoint { get; private set; }
        
        /// <summary>
        /// 是否正在监听
        /// </summary>
        public new bool IsListening => base.IsListening;
        
        /// <summary>
        /// 最大连接数
        /// </summary>
        public int MaxConnections
        {
            get => _tcpServerAdapter.MaxConnections;
            set => _tcpServerAdapter.MaxConnections = value;
        }
        
        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);
        
        /// <summary>
        /// 接收超时
        /// </summary>
        public TimeSpan ReceiveTimeout
        {
            get => _tcpServerAdapter.ReceiveTimeout;
            set => _tcpServerAdapter.ReceiveTimeout = value;
        }
        
        /// <summary>
        /// 发送超时
        /// </summary>
        public TimeSpan SendTimeout
        {
            get => _tcpServerAdapter.SendTimeout;
            set => _tcpServerAdapter.SendTimeout = value;
        }

        public bool EnableSnapshotPersistence
        {
            get => _enableSnapshotPersistence;
            set
            {
                _enableSnapshotPersistence = value;
                if (value)
                {
                    StartSnapshotTimer();
                }
                else
                {
                    StopSnapshotTimer();
                }
            }
        }

        public string SnapshotFilePath { get; set; }

        public TimeSpan SnapshotSaveInterval { get; set; } = TimeSpan.FromSeconds(5);
        
        /// <summary>
        /// 构造函数，使用默认IP和端口（0.0.0.0:102）
        /// </summary>
        public S7TcpServer()
            : this("0.0.0.0", 102) // S7标准端口为102
        {
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ipEndPoint">IP终结点</param>
        public S7TcpServer(IPEndPoint ipEndPoint)
            : base(CreateTransport(ipEndPoint))
        {
            _tcpServerAdapter = (TcpServerAdapter)base._transport.StreamResource;
            _serverTransport = base._transport;
            IPEndPoint = ipEndPoint;
            InitializeSnapshotPersistence();
        }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口</param>
        public S7TcpServer(string ip, int port)
            : base(CreateTransport(ip, port))
        {
            _tcpServerAdapter = (TcpServerAdapter)base._transport.StreamResource;
            _serverTransport = base._transport;
            
            if (!IPAddress.TryParse(ip, out IPAddress address))
            {
                if (ip.Equals("0.0.0.0") || ip.Equals("any", StringComparison.OrdinalIgnoreCase))
                {
                    address = IPAddress.Any;
                }
                else
                {
                    address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
                }
            }
            
            IPEndPoint = new IPEndPoint(address, port);
            InitializeSnapshotPersistence();
        }
        
        /// <summary>
        /// 开始监听
        /// </summary>
        /// <returns>操作结果</returns>
        public OperationResult Listen()
        {
            if (EnableSnapshotPersistence)
            {
                TryLoadSnapshot();
                StartSnapshotTimer();
            }

            return StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// 停止监听
        /// </summary>
        /// <returns>操作结果</returns>
        public OperationResult Shutdown()
        {
            if (EnableSnapshotPersistence)
            {
                TrySaveSnapshot(true);
                StopSnapshotTimer();
            }

            return StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// 使用日志记录器
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public void UseLogger(ILogger logger)
        {
            Logger = logger;
            _tcpServerAdapter.UseLogger(logger);
        }
        
        /// <summary>
        /// 创建传输
        /// </summary>
        /// <param name="ipEndPoint">IP终结点</param>
        /// <returns>服务器消息传输</returns>
        private static ServerMessageTransport CreateTransport(IPEndPoint ipEndPoint)
        {
            var adapter = new TcpServerAdapter(ipEndPoint);
            return new ServerMessageTransport(adapter);
        }
        
        /// <summary>
        /// 创建传输
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口</param>
        /// <returns>服务器消息传输</returns>
        private static ServerMessageTransport CreateTransport(string ip, int port)
        {
            var adapter = new TcpServerAdapter(ip, port);
            return new ServerMessageTransport(adapter);
        }

        private void InitializeSnapshotPersistence()
        {
            ConfigureSnapshotPersistence();
            DataStore.DataStoreWrittenTo += HandleSnapshotDataWritten;
        }

        public void ConfigureSnapshotPersistence(string name = null)
        {
            SnapshotFilePath = SnapshotFilePathHelper.Build("S7TcpServer", IPEndPoint.Port.ToString(), name);
        }

        private void HandleSnapshotDataWritten(object sender, S7DataStoreEventArgs e)
        {
            if (!EnableSnapshotPersistence)
            {
                return;
            }

            _snapshotDirty = true;
        }

        private void StartSnapshotTimer()
        {
            if (!EnableSnapshotPersistence)
            {
                return;
            }

            var interval = SnapshotSaveInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : SnapshotSaveInterval;
            lock (_snapshotSyncRoot)
            {
                if (_snapshotTimer == null)
                {
                    _snapshotTimer = new Timer(_ => TrySaveSnapshot(false), null, interval, interval);
                    return;
                }

                _snapshotTimer.Change(interval, interval);
            }
        }

        private void StopSnapshotTimer()
        {
            lock (_snapshotSyncRoot)
            {
                _snapshotTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void TryLoadSnapshot()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SnapshotFilePath) || !File.Exists(SnapshotFilePath))
                {
                    return;
                }

                ServerSnapshotPersistence.LoadS7Snapshot(SnapshotFilePath, DataStore);
                _snapshotDirty = false;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "加载S7快照失败: {SnapshotFilePath}", SnapshotFilePath);
            }
        }

        private void TrySaveSnapshot(bool force)
        {
            try
            {
                SaveSnapshot(force);
            }
            catch (Exception ex)
            {
                _snapshotDirty = true;
                Logger?.LogError(ex, "保存S7快照失败: {SnapshotFilePath}", SnapshotFilePath);
            }
        }

        private void SaveSnapshot(bool force)
        {
            if (!EnableSnapshotPersistence || string.IsNullOrWhiteSpace(SnapshotFilePath))
            {
                return;
            }

            lock (_snapshotSyncRoot)
            {
                if (!force && !_snapshotDirty)
                {
                    return;
                }

                ServerSnapshotPersistence.SaveS7Snapshot(SnapshotFilePath, DataStore);
                _snapshotDirty = false;
            }
        }

        public OperationResult DeleteSnapshot()
        {
            try
            {
                lock (_snapshotSyncRoot)
                {
                    ServerSnapshotPersistence.DeleteSnapshot(SnapshotFilePath);
                    _snapshotDirty = false;
                }

                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "删除S7快照失败: {SnapshotFilePath}", SnapshotFilePath);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        public OperationResult ResetDataAndDeleteSnapshot()
        {
            var resetResult = ResetAllDataAreas();
            if (!resetResult.IsSuccess)
            {
                return resetResult;
            }

            return DeleteSnapshot();
        }
        
        #region 数据变化监听接口

        /// <summary>
        /// 数据写入事件
        /// </summary>
        public event EventHandler<S7DataStoreEventArgs> DataWritten
        {
            add { DataStore.DataStoreWrittenTo += value; }
            remove { DataStore.DataStoreWrittenTo -= value; }
        }

        /// <summary>
        /// 数据读取事件
        /// </summary>
        public event EventHandler<S7DataStoreEventArgs> DataRead
        {
            add { DataStore.DataStoreReadFrom += value; }
            remove { DataStore.DataStoreReadFrom -= value; }
        }

        /// <summary>
        /// 启用数据变化监听
        /// </summary>
        /// <param name="enable">是否启用</param>
        public void EnableDataMonitoring(bool enable)
        {
            if (enable)
            {
                Logger?.LogInformation("启用S7数据变化监听");
            }
            else
            {
                Logger?.LogInformation("禁用S7数据变化监听");
            }
        }

        /// <summary>
        /// 获取数据变化统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public string GetDataMonitoringStats()
        {
            var stats = new System.Text.StringBuilder();
            stats.AppendLine("S7数据变化监听统计:");
            stats.AppendLine($"  数据块数量: {DataStore.DataBlocks.Count}");
            stats.AppendLine($"  M区大小: {DataStore.Merkers?.Size ?? 0} 字节");
            stats.AppendLine($"  I区大小: {DataStore.Inputs?.Size ?? 0} 字节");
            stats.AppendLine($"  Q区大小: {DataStore.Outputs?.Size ?? 0} 字节");
            stats.AppendLine($"  T区大小: {DataStore.Timers?.Size ?? 0} 字节");
            stats.AppendLine($"  C区大小: {DataStore.Counters?.Size ?? 0} 字节");
            
            return stats.ToString();
        }

        #endregion
        
        #region 数据区操作接口

        /// <summary>
        /// 获取所有已创建的数据块编号
        /// </summary>
        /// <returns>数据块编号列表</returns>
        public List<int> GetDataBlockNumbers()
        {
            return DataStore.DataBlocks.Keys.ToList();
        }
        
        /// <summary>
        /// 创建数据块
        /// </summary>
        /// <param name="dbNumber">数据块编号</param>
        /// <param name="size">数据块大小（字节）</param>
        /// <returns>操作结果</returns>
        public OperationResult CreateDataBlock(int dbNumber, int size)
        {
            try
            {
                DataStore.CreateDataBlock(dbNumber, size);
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "创建数据块DB{DbNumber}失败", dbNumber);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        /// <summary>
        /// 删除数据块
        /// </summary>
        /// <param name="dbNumber">数据块编号</param>
        /// <returns>操作结果</returns>
        public OperationResult DeleteDataBlock(int dbNumber)
        {
            try
            {
                DataStore.DeleteDataBlock(dbNumber);
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "删除数据块DB{DbNumber}失败", dbNumber);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        /// <summary>
        /// 获取数据块大小
        /// </summary>
        /// <param name="dbNumber">数据块编号</param>
        /// <returns>数据块大小（字节）</returns>
        public OperationResult<int> GetDataBlockSize(int dbNumber)
        {
            try
            {
                var db = DataStore.GetDataBlock(dbNumber);
                return OperationResult.CreateSuccessResult(db.Size);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "获取数据块DB{DbNumber}大小失败", dbNumber);
                return OperationResult.CreateFailedResult<int>(ex.Message);
            }
        }
        
        /// <summary>
        /// 读取数据区域数据
        /// </summary>
        /// <param name="area">数据区域</param>
        /// <param name="dbNumber">数据块编号（仅当area为DB时有效）</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="length">读取长度（字节）</param>
        /// <returns>读取的数据</returns>
        public OperationResult<byte[]> ReadArea(S7Area area, int dbNumber, int startAddress, int length)
        {
            try
            {
                var data = DataStore.ReadArea(area, dbNumber, startAddress, length);
                return OperationResult.CreateSuccessResult(data);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "读取区域{Area}数据失败", area);
                return OperationResult.CreateFailedResult<byte[]>(ex.Message);
            }
        }
        
        /// <summary>
        /// 写入数据区域数据
        /// </summary>
        /// <param name="area">数据区域</param>
        /// <param name="dbNumber">数据块编号（仅当area为DB时有效）</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">要写入的数据</param>
        /// <returns>操作结果</returns>
        public OperationResult WriteArea(S7Area area, int dbNumber, int startAddress, byte[] data)
        {
            try
            {
                DataStore.WriteArea(area, dbNumber, startAddress, data);
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "写入区域{Area}数据失败", area);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        /// <summary>
        /// 读取DB区数据
        /// </summary>
        /// <param name="dbNumber">数据块编号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="length">读取长度（字节）</param>
        /// <returns>读取的数据</returns>
        public OperationResult<byte[]> ReadDB(int dbNumber, int startAddress, int length)
        {
            return ReadArea(S7Area.DB, dbNumber, startAddress, length);
        }
        
        /// <summary>
        /// 写入DB区数据
        /// </summary>
        /// <param name="dbNumber">数据块编号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">要写入的数据</param>
        /// <returns>操作结果</returns>
        public OperationResult WriteDB(int dbNumber, int startAddress, byte[] data)
        {
            return WriteArea(S7Area.DB, dbNumber, startAddress, data);
        }
        
        /// <summary>
        /// 读取输入区(I)数据
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="length">读取长度（字节）</param>
        /// <returns>读取的数据</returns>
        public OperationResult<byte[]> ReadInputs(int startAddress, int length)
        {
            return ReadArea(S7Area.I, 0, startAddress, length);
        }
        
        /// <summary>
        /// 写入输入区(I)数据
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">要写入的数据</param>
        /// <returns>操作结果</returns>
        public OperationResult WriteInputs(int startAddress, byte[] data)
        {
            return WriteArea(S7Area.I, 0, startAddress, data);
        }
        
        /// <summary>
        /// 读取输出区(Q)数据
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="length">读取长度（字节）</param>
        /// <returns>读取的数据</returns>
        public OperationResult<byte[]> ReadOutputs(int startAddress, int length)
        {
            return ReadArea(S7Area.Q, 0, startAddress, length);
        }
        
        /// <summary>
        /// 写入输出区(Q)数据
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">要写入的数据</param>
        /// <returns>操作结果</returns>
        public OperationResult WriteOutputs(int startAddress, byte[] data)
        {
            return WriteArea(S7Area.Q, 0, startAddress, data);
        }
        
        /// <summary>
        /// 读取标志位区(M)数据
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="length">读取长度（字节）</param>
        /// <returns>读取的数据</returns>
        public OperationResult<byte[]> ReadMerkers(int startAddress, int length)
        {
            return ReadArea(S7Area.M, 0, startAddress, length);
        }
        
        /// <summary>
        /// 写入标志位区(M)数据
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">要写入的数据</param>
        /// <returns>操作结果</returns>
        public OperationResult WriteMerkers(int startAddress, byte[] data)
        {
            return WriteArea(S7Area.M, 0, startAddress, data);
        }
        
        /// <summary>
        /// 读取计时器区(T)数据
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="length">读取长度（字节）</param>
        /// <returns>读取的数据</returns>
        public OperationResult<byte[]> ReadTimers(int startAddress, int length)
        {
            return ReadArea(S7Area.T, 0, startAddress, length);
        }
        
        /// <summary>
        /// 写入计时器区(T)数据
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">要写入的数据</param>
        /// <returns>操作结果</returns>
        public OperationResult WriteTimers(int startAddress, byte[] data)
        {
            return WriteArea(S7Area.T, 0, startAddress, data);
        }
        
        /// <summary>
        /// 读取计数器区(C)数据
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="length">读取长度（字节）</param>
        /// <returns>读取的数据</returns>
        public OperationResult<byte[]> ReadCounters(int startAddress, int length)
        {
            return ReadArea(S7Area.C, 0, startAddress, length);
        }
        
        /// <summary>
        /// 写入计数器区(C)数据
        /// </summary>
        /// <param name="startAddress">起始地址</param>
        /// <param name="data">要写入的数据</param>
        /// <returns>操作结果</returns>
        public OperationResult WriteCounters(int startAddress, byte[] data)
        {
            return WriteArea(S7Area.C, 0, startAddress, data);
        }
        
        /// <summary>
        /// 清除所有数据块
        /// </summary>
        /// <returns>操作结果</returns>
        public OperationResult ClearAllDataBlocks()
        {
            try
            {
                // 保存当前数据块编号列表
                var dbNumbers = GetDataBlockNumbers();
                
                // 删除所有数据块
                foreach (var dbNumber in dbNumbers)
                {
                    DataStore.DeleteDataBlock(dbNumber);
                }
                
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "清除所有数据块失败");
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        /// <summary>
        /// 重置所有数据区为默认值(0)
        /// </summary>
        /// <returns>操作结果</returns>
        public OperationResult ResetAllDataAreas()
        {
            try
            {
                // 清除所有数据块
                ClearAllDataBlocks();
                
                // 重置所有其他数据区为0
                if (DataStore.Merkers != null)
                {
                    for (int i = 0; i < DataStore.Merkers.Size; i++)
                    {
                        DataStore.Merkers[i] = 0;
                    }
                }
                
                if (DataStore.Inputs != null)
                {
                    for (int i = 0; i < DataStore.Inputs.Size; i++)
                    {
                        DataStore.Inputs[i] = 0;
                    }
                }
                
                if (DataStore.Outputs != null)
                {
                    for (int i = 0; i < DataStore.Outputs.Size; i++)
                    {
                        DataStore.Outputs[i] = 0;
                    }
                }
                
                if (DataStore.Timers != null)
                {
                    for (int i = 0; i < DataStore.Timers.Size; i++)
                    {
                        DataStore.Timers[i] = 0;
                    }
                }
                
                if (DataStore.Counters != null)
                {
                    for (int i = 0; i < DataStore.Counters.Size; i++)
                    {
                        DataStore.Counters[i] = 0;
                    }
                }
                
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "重置所有数据区失败");
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 创建不支持功能的结果
        /// </summary>
        /// <returns>不支持功能的结果</returns>
        private OperationResult CreateNotSupportedResult()
        {
            return OperationResult.CreateFailedResult("此功能暂不支持，请使用S7协议特定的读写方法");
        }
        
        /// <summary>
        /// 创建不支持功能的结果
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <returns>不支持功能的结果</returns>
        private OperationResult<T> CreateNotSupportedResult<T>()
        {
            return OperationResult.CreateFailedResult<T>("此功能暂不支持，请使用S7协议特定的读写方法");
        }

        /// <summary>
        /// 解析S7地址
        /// </summary>
        /// <param name="address">地址字符串</param>
        /// <returns>地址信息</returns>
        private (S7AddressInfo AddressInfo, S7Area Area)? ParseS7Address(string address)
        {
            try
            {
                if (string.IsNullOrEmpty(address))
                    return null;

                address = address.ToUpper();
                var addressInfo = new S7AddressInfo
                {
                    OriginalAddress = address,
                    DbNumber = 0,
                    StartByte = 0,
                    Length = 1
                };

                S7Area area = S7Area.M; // 默认区域

                // 解析地址格式：DB1.DBW0, M0, I0, Q0, T0, C0, V0
                if (address.StartsWith("DB"))
                {
                    area = S7Area.DB;
                    addressInfo.DataType = S7DataType.DBB;
                    var parts = address.Split('.');
                    if (parts.Length >= 2)
                    {
                        // 解析DB号
                        addressInfo.DbNumber = int.Parse(parts[0].Substring(2));
                        
                        // 解析数据类型和地址
                        var dataPart = parts[1];
                        if (dataPart.StartsWith("DBX"))
                        {
                            addressInfo.DataType = S7DataType.DBX;
                            var bitAddress = dataPart.Substring(3);
                            var splitIndex = bitAddress.IndexOf('.');
                            addressInfo.StartByte = splitIndex > 0 ? int.Parse(bitAddress.Substring(0, splitIndex)) : int.Parse(bitAddress);
                            addressInfo.Length = 1;
                        }
                        else if (dataPart.StartsWith("DBB"))
                        {
                            addressInfo.DataType = S7DataType.DBB;
                            addressInfo.StartByte = int.Parse(dataPart.Substring(3));
                            addressInfo.Length = 1;
                        }
                        else if (dataPart.StartsWith("DBW"))
                        {
                            addressInfo.DataType = S7DataType.DBW;
                            addressInfo.StartByte = int.Parse(dataPart.Substring(3));
                            addressInfo.Length = 2;
                        }
                        else if (dataPart.StartsWith("DBD"))
                        {
                            addressInfo.DataType = S7DataType.DBD;
                            addressInfo.StartByte = int.Parse(dataPart.Substring(3));
                            addressInfo.Length = 4;
                        }
                    }
                }
                else if (address.StartsWith("M"))
                {
                    area = S7Area.M;
                    addressInfo.DataType = S7DataType.MB;
                    var memoryAddress = address.Substring(1);
                    var splitIndex = memoryAddress.IndexOf('.');
                    addressInfo.StartByte = splitIndex > 0 ? int.Parse(memoryAddress.Substring(0, splitIndex)) : int.Parse(memoryAddress);
                    addressInfo.Length = 1;
                }
                else if (address.StartsWith("I"))
                {
                    area = S7Area.I;
                    addressInfo.DataType = S7DataType.IB;
                    var inputAddress = address.Substring(1);
                    var splitIndex = inputAddress.IndexOf('.');
                    addressInfo.StartByte = splitIndex > 0 ? int.Parse(inputAddress.Substring(0, splitIndex)) : int.Parse(inputAddress);
                    addressInfo.Length = 1;
                }
                else if (address.StartsWith("Q"))
                {
                    area = S7Area.Q;
                    addressInfo.DataType = S7DataType.QB;
                    var outputAddress = address.Substring(1);
                    var splitIndex = outputAddress.IndexOf('.');
                    addressInfo.StartByte = splitIndex > 0 ? int.Parse(outputAddress.Substring(0, splitIndex)) : int.Parse(outputAddress);
                    addressInfo.Length = 1;
                }
                else if (address.StartsWith("V"))
                {
                    area = S7Area.DB; // V区映射到DB1
                    addressInfo.DbNumber = 1;
                    addressInfo.DataType = S7DataType.VB;
                    var vAddress = address.Substring(1);
                    var splitIndex = vAddress.IndexOf('.');
                    addressInfo.StartByte = splitIndex > 0 ? int.Parse(vAddress.Substring(0, splitIndex)) : int.Parse(vAddress);
                    addressInfo.Length = 1;
                }

                return (addressInfo, area);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "解析S7地址失败: {Address}", address);
                return null;
            }
        }

        /// <summary>
        /// 将字节数组转换为指定类型的值
        /// </summary>
        /// <param name="data">字节数组</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="length">长度</param>
        /// <returns>转换后的值</returns>
        private object ConvertBytesToValue(byte[] data, DataTypeEnums dataType, int length)
        {
            try
            {
                if (data == null || data.Length == 0)
                    return null;

                byte[] normalized = NormalizeS7BytesForRead(data, dataType);

                switch (dataType)
                {
                    case DataTypeEnums.Bool:
                        return normalized[0] != 0;
                    case DataTypeEnums.Byte:
                        return normalized[0];
                    case DataTypeEnums.Int16:
                        return BitConverter.ToInt16(normalized, 0);
                    case DataTypeEnums.UInt16:
                        return BitConverter.ToUInt16(normalized, 0);
                    case DataTypeEnums.Int32:
                        return BitConverter.ToInt32(normalized, 0);
                    case DataTypeEnums.UInt32:
                        return BitConverter.ToUInt32(normalized, 0);
                    case DataTypeEnums.Int64:
                        return BitConverter.ToInt64(normalized, 0);
                    case DataTypeEnums.UInt64:
                        return BitConverter.ToUInt64(normalized, 0);
                    case DataTypeEnums.Float:
                        return BitConverter.ToSingle(normalized, 0);
                    case DataTypeEnums.Double:
                        return BitConverter.ToDouble(normalized, 0);
                    case DataTypeEnums.String:
                        return Encoding.ASCII.GetString(normalized);
                    default:
                        return normalized;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "数据类型转换失败: {DataType}", dataType);
                return null;
            }
        }

        /// <summary>
        /// 将值转换为字节数组
        /// </summary>
        /// <param name="value">要转换的值</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>字节数组</returns>
        private byte[] ConvertValueToBytes(object value, DataTypeEnums dataType)
        {
            try
            {
                if (value == null)
                    return null;

                switch (dataType)
                {
                    case DataTypeEnums.Bool:
                        return new byte[] { (byte)((bool)value ? 1 : 0) };
                    case DataTypeEnums.Byte:
                        return new byte[] { (byte)value };
                    case DataTypeEnums.Int16:
                        return NormalizeS7BytesForWrite(BitConverter.GetBytes((short)value));
                    case DataTypeEnums.UInt16:
                        return NormalizeS7BytesForWrite(BitConverter.GetBytes((ushort)value));
                    case DataTypeEnums.Int32:
                        return NormalizeS7BytesForWrite(BitConverter.GetBytes((int)value));
                    case DataTypeEnums.UInt32:
                        return NormalizeS7BytesForWrite(BitConverter.GetBytes((uint)value));
                    case DataTypeEnums.Int64:
                        return NormalizeS7BytesForWrite(BitConverter.GetBytes((long)value));
                    case DataTypeEnums.UInt64:
                        return NormalizeS7BytesForWrite(BitConverter.GetBytes((ulong)value));
                    case DataTypeEnums.Float:
                        return NormalizeS7BytesForWrite(BitConverter.GetBytes((float)value));
                    case DataTypeEnums.Double:
                        return NormalizeS7BytesForWrite(BitConverter.GetBytes((double)value));
                    case DataTypeEnums.String:
                        return Encoding.ASCII.GetBytes((string)value);
                    default:
                        Logger?.LogWarning("不支持的数据类型转换: {DataType}", dataType);
                        return null;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "值转换为字节数组失败: {DataType}", dataType);
                return null;
            }
        }

        private static byte[] NormalizeS7BytesForRead(byte[] data, DataTypeEnums dataType)
        {
            if (data == null)
                return null;

            var result = (byte[])data.Clone();
            if (!BitConverter.IsLittleEndian)
                return result;

            switch (dataType)
            {
                case DataTypeEnums.Int16:
                case DataTypeEnums.UInt16:
                case DataTypeEnums.Int32:
                case DataTypeEnums.UInt32:
                case DataTypeEnums.Int64:
                case DataTypeEnums.UInt64:
                case DataTypeEnums.Float:
                case DataTypeEnums.Double:
                    Array.Reverse(result);
                    break;
            }

            return result;
        }

        private static byte[] NormalizeS7BytesForWrite(byte[] data)
        {
            if (data == null)
                return null;

            var result = (byte[])data.Clone();
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(result);
            }

            return result;
        }
        
        #endregion
        
        #region 服务器管理接口
        
        /// <summary>
        /// 设置PLC型号
        /// </summary>
        /// <param name="siemensVersion">西门子版本</param>
        public void SetSiemensVersion(SiemensVersion siemensVersion)
        {
            SiemensVersion = siemensVersion;
            Logger?.LogInformation("设置PLC型号为: {SiemensVersion}", siemensVersion);
        }
        
        /// <summary>
        /// 设置机架号和槽号
        /// </summary>
        /// <param name="rack">机架号</param>
        /// <param name="slot">槽号</param>
        public void SetRackSlot(byte rack, byte slot)
        {
            Rack = rack;
            Slot = slot;
            Logger?.LogInformation("设置机架号: {Rack}, 槽号: {Slot}", rack, slot);
        }
        
        /// <summary>
        /// 获取服务器状态信息
        /// </summary>
        /// <returns>状态信息</returns>
        public string GetServerStatus()
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine($"S7 TCP服务器状态:");
            status.AppendLine($"  监听状态: {(IsListening ? "正在监听" : "未监听")}");
            status.AppendLine($"  监听地址: {IPEndPoint}");
            status.AppendLine($"  PLC型号: {SiemensVersion}");
            status.AppendLine($"  机架号: {Rack}, 槽号: {Slot}");
            status.AppendLine($"  数据块数量: {DataStore.DataBlocks.Count}");
            status.AppendLine($"  M区大小: {DataStore.Merkers?.Size ?? 0} 字节");
            status.AppendLine($"  I区大小: {DataStore.Inputs?.Size ?? 0} 字节");
            status.AppendLine($"  Q区大小: {DataStore.Outputs?.Size ?? 0} 字节");
            status.AppendLine($"  T区大小: {DataStore.Timers?.Size ?? 0} 字节");
            status.AppendLine($"  C区大小: {DataStore.Counters?.Size ?? 0} 字节");
            
            return status.ToString();
        }
        
        // BatchRead
        public OperationResult<Dictionary<string, (DataTypeEnums, object)>> BatchRead(Dictionary<string, DataTypeEnums> addresses)
        {
            try
            {
                if (addresses == null || addresses.Count == 0)
                {
                    return OperationResult.CreateSuccessResult(new Dictionary<string, (DataTypeEnums, object)>());
                }

                var internalAddresses = addresses.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value, (object)null));
                var addressInfos = S7BatchHelper.ParseS7Addresses(internalAddresses);
                if (addressInfos.Count == 0)
                {
                    return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>("没有有效的S7地址可以读取");
                }

                var optimizedBlocks = S7BatchHelper.OptimizeS7AddressBlocks(addressInfos);
                if (optimizedBlocks.Count == 0)
                {
                    return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>("S7地址块优化失败");
                }

                var blockData = new Dictionary<string, byte[]>();
                var errors = new List<string>();

                foreach (var block in optimizedBlocks)
                {
                    var readResult = ReadBlock(block);
                    if (readResult.IsSuccess)
                    {
                        blockData[BuildBlockKey(block)] = readResult.ResultValue;
                    }
                    else
                    {
                        errors.Add($"读取块 {DescribeBlock(block)} 失败: {readResult.Message}");
                    }
                }

                var extractedValues = ExtractValuesFromBlocks(blockData, optimizedBlocks);
                var result = new Dictionary<string, (DataTypeEnums, object)>();

                foreach (var kvp in addresses)
                {
                    extractedValues.TryGetValue(kvp.Key, out var value);
                    result[kvp.Key] = (kvp.Value, value);
                }

                return new OperationResult<Dictionary<string, (DataTypeEnums, object)>>
                {
                    IsSuccess = errors.Count == 0,
                    Message = errors.Count == 0 ? string.Empty : string.Join("; ", errors),
                    ResultValue = result
                }.Complete();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "批量读取失败");
                return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>(ex.Message);
            }
        }
        
        // ReadByte
        public OperationResult<byte> ReadByte(string address)
        {
            try
            {
                var addressParseResult = ParseS7Address(address);
                if (addressParseResult == null)
                {
                    return OperationResult.CreateFailedResult<byte>($"无效的S7地址: {address}");
                }

                var addressInfo = addressParseResult.Value.AddressInfo;
                var area = addressParseResult.Value.Area;

                var readResult = ReadArea(area, addressInfo.DbNumber, addressInfo.StartByte, 1);
                if (readResult.IsSuccess)
                {
                    return OperationResult.CreateSuccessResult(readResult.ResultValue[0]);
                }
                else
                {
                    return OperationResult.CreateFailedResult<byte>(readResult.Message);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "读取字节失败: {Address}", address);
                return OperationResult.CreateFailedResult<byte>(ex.Message);
            }
        }
        
        // ReadByte
        public OperationResult<byte[]> ReadByte(string address, int length)
        {
            try
            {
                var addressParseResult = ParseS7Address(address);
                if (addressParseResult == null)
                {
                    return OperationResult.CreateFailedResult<byte[]>($"无效的S7地址: {address}");
                }

                var addressInfo = addressParseResult.Value.AddressInfo;
                var area = addressParseResult.Value.Area;

                var readResult = ReadArea(area, addressInfo.DbNumber, addressInfo.StartByte, length);
                if (readResult.IsSuccess)
                {
                    return OperationResult.CreateSuccessResult(readResult.ResultValue);
                }
                else
                {
                    return OperationResult.CreateFailedResult<byte[]>(readResult.Message);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "读取字节数组失败: {Address}, {Length}", address, length);
                return OperationResult.CreateFailedResult<byte[]>(ex.Message);
            }
        }
        
        // ReadBoolean
        public OperationResult<bool> ReadBoolean(string address)
        {
            try
            {
                var addressParseResult = ParseS7Address(address);
                if (addressParseResult == null)
                {
                    return OperationResult.CreateFailedResult<bool>($"无效的S7地址: {address}");
                }

                var addressInfo = addressParseResult.Value.AddressInfo;
                var area = addressParseResult.Value.Area;

                var readResult = ReadArea(area, addressInfo.DbNumber, addressInfo.StartByte, 1);
                if (readResult.IsSuccess)
                {
                    return OperationResult.CreateSuccessResult(readResult.ResultValue[0] != 0);
                }
                else
                {
                    return OperationResult.CreateFailedResult<bool>(readResult.Message);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "读取布尔值失败: {Address}", address);
                return OperationResult.CreateFailedResult<bool>(ex.Message);
            }
        }
        
        // ReadBoolean
        public OperationResult<bool[]> ReadBoolean(string address, int length)
        {
            try
            {
                var addressParseResult = ParseS7Address(address);
                if (addressParseResult == null)
                {
                    return OperationResult.CreateFailedResult<bool[]>($"无效的S7地址: {address}");
                }

                var addressInfo = addressParseResult.Value.AddressInfo;
                var area = addressParseResult.Value.Area;

                var readResult = ReadArea(area, addressInfo.DbNumber, addressInfo.StartByte, length);
                if (readResult.IsSuccess)
                {
                    var boolArray = new bool[length];
                    for (int i = 0; i < length; i++)
                    {
                        boolArray[i] = readResult.ResultValue[i] != 0;
                    }
                    return OperationResult.CreateSuccessResult(boolArray);
                }
                else
                {
                    return OperationResult.CreateFailedResult<bool[]>(readResult.Message);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "读取布尔数组失败: {Address}, {Length}", address, length);
                return OperationResult.CreateFailedResult<bool[]>(ex.Message);
            }
        }
        
        private OperationResult<object> ToObjectResult<T>(OperationResult<T> result)
        {
            if (result.IsSuccess)
            {
                return OperationResult.CreateSuccessResult<object>(result.ResultValue);
            }

            return OperationResult.CreateFailedResult<object>(result.Message);
        }

        private static bool TryConvertValue<T>(object value, out T converted)
        {
            if (value is T typed)
            {
                converted = typed;
                return true;
            }

            try
            {
                converted = (T)Convert.ChangeType(value, typeof(T));
                return true;
            }
            catch
            {
                converted = default;
                return false;
            }
        }

        private int GetTypeByteLength(DataTypeEnums dataType)
        {
            switch (dataType)
            {
                case DataTypeEnums.Bool:
                case DataTypeEnums.Byte:
                    return 1;
                case DataTypeEnums.Int16:
                case DataTypeEnums.UInt16:
                    return 2;
                case DataTypeEnums.Int32:
                case DataTypeEnums.UInt32:
                case DataTypeEnums.Float:
                    return 4;
                case DataTypeEnums.Int64:
                case DataTypeEnums.UInt64:
                case DataTypeEnums.Double:
                    return 8;
                default:
                    return 1;
            }
        }

        private OperationResult<T> ReadSingleValue<T>(string address, DataTypeEnums dataType)
        {
            var readBytes = ReadByte(address, GetTypeByteLength(dataType));
            if (!readBytes.IsSuccess)
            {
                return OperationResult.CreateFailedResult<T>(readBytes.Message);
            }

            var value = ConvertBytesToValue(readBytes.ResultValue, dataType, readBytes.ResultValue.Length);
            if (!TryConvertValue(value, out T converted))
            {
                return OperationResult.CreateFailedResult<T>($"数据类型转换失败: {dataType}");
            }

            return OperationResult.CreateSuccessResult(converted);
        }

        private OperationResult<T[]> ReadArrayValue<T>(string address, int length, DataTypeEnums dataType)
        {
            if (length <= 0)
            {
                return OperationResult.CreateFailedResult<T[]>("读取长度必须大于0");
            }

            int unitLength = GetTypeByteLength(dataType);
            var readBytes = ReadByte(address, unitLength * length);
            if (!readBytes.IsSuccess)
            {
                return OperationResult.CreateFailedResult<T[]>(readBytes.Message);
            }

            var result = new T[length];
            for (int i = 0; i < length; i++)
            {
                var segment = new byte[unitLength];
                Array.Copy(readBytes.ResultValue, i * unitLength, segment, 0, unitLength);
                var value = ConvertBytesToValue(segment, dataType, unitLength);
                if (!TryConvertValue(value, out T converted))
                {
                    return OperationResult.CreateFailedResult<T[]>($"第{i}项数据类型转换失败: {dataType}");
                }

                result[i] = converted;
            }

            return OperationResult.CreateSuccessResult(result);
        }

        private OperationResult WriteSingleValue(string address, DataTypeEnums dataType, object value)
        {
            var bytes = ConvertValueToBytes(value, dataType);
            if (bytes == null || bytes.Length == 0)
            {
                return OperationResult.CreateFailedResult("写入数据转换失败");
            }

            return Write(address, bytes);
        }

        private OperationResult WriteArrayValue<T>(string address, T[] value, DataTypeEnums dataType)
        {
            if (value == null || value.Length == 0)
            {
                return OperationResult.CreateFailedResult("写入数据不能为空");
            }

            var writeBytes = new List<byte>();
            foreach (var item in value)
            {
                var bytes = ConvertValueToBytes(item, dataType);
                if (bytes == null || bytes.Length == 0)
                {
                    return OperationResult.CreateFailedResult($"数组写入转换失败: {dataType}");
                }

                writeBytes.AddRange(bytes);
            }

            return Write(address, writeBytes.ToArray());
        }
        
        // ReadUInt16
        public OperationResult<ushort> ReadUInt16(string address)
        {
            return ReadSingleValue<ushort>(address, DataTypeEnums.UInt16);
        }
        
        // ReadUInt16
        public OperationResult<ushort[]> ReadUInt16(string address, int length)
        {
            return ReadArrayValue<ushort>(address, length, DataTypeEnums.UInt16);
        }
        
        // ReadInt16
        public OperationResult<short> ReadInt16(string address)
        {
            return ReadSingleValue<short>(address, DataTypeEnums.Int16);
        }
        
        // ReadInt16
        public OperationResult<short[]> ReadInt16(string address, int length)
        {
            return ReadArrayValue<short>(address, length, DataTypeEnums.Int16);
        }
        
        // ReadUInt32
        public OperationResult<uint> ReadUInt32(string address)
        {
            return ReadSingleValue<uint>(address, DataTypeEnums.UInt32);
        }
        
        // ReadUInt32
        public OperationResult<uint[]> ReadUInt32(string address, int length)
        {
            return ReadArrayValue<uint>(address, length, DataTypeEnums.UInt32);
        }
        
        // ReadInt32
        public OperationResult<int> ReadInt32(string address)
        {
            return ReadSingleValue<int>(address, DataTypeEnums.Int32);
        }
        
        // ReadInt32
        public OperationResult<int[]> ReadInt32(string address, int length)
        {
            return ReadArrayValue<int>(address, length, DataTypeEnums.Int32);
        }
        
        // ReadUInt64
        public OperationResult<ulong> ReadUInt64(string address)
        {
            return ReadSingleValue<ulong>(address, DataTypeEnums.UInt64);
        }
        
        // ReadUInt64
        public OperationResult<ulong[]> ReadUInt64(string address, int length)
        {
            return ReadArrayValue<ulong>(address, length, DataTypeEnums.UInt64);
        }
        
        // ReadInt64
        public OperationResult<long> ReadInt64(string address)
        {
            return ReadSingleValue<long>(address, DataTypeEnums.Int64);
        }
        
        // ReadInt64
        public OperationResult<long[]> ReadInt64(string address, int length)
        {
            return ReadArrayValue<long>(address, length, DataTypeEnums.Int64);
        }
        
        // ReadFloat
        public OperationResult<float> ReadFloat(string address)
        {
            return ReadSingleValue<float>(address, DataTypeEnums.Float);
        }
        
        // ReadFloat
        public OperationResult<float[]> ReadFloat(string address, int length)
        {
            return ReadArrayValue<float>(address, length, DataTypeEnums.Float);
        }
        
        // ReadDouble
        public OperationResult<double> ReadDouble(string address)
        {
            return ReadSingleValue<double>(address, DataTypeEnums.Double);
        }
        
        // ReadDouble
        public OperationResult<double[]> ReadDouble(string address, int length)
        {
            return ReadArrayValue<double>(address, length, DataTypeEnums.Double);
        }
        
        // ReadString
        public OperationResult<string> ReadString(string address, int length)
        {
            if (length <= 0)
            {
                return OperationResult.CreateFailedResult<string>("读取长度必须大于0");
            }

            var readResult = ReadByte(address, length);
            if (!readResult.IsSuccess)
            {
                return OperationResult.CreateFailedResult<string>(readResult.Message);
            }

            return OperationResult.CreateSuccessResult<string>(Encoding.ASCII.GetString(readResult.ResultValue));
        }
        
        // Read
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address)
        {
            return Read(dataTypeEnum, address, 1);
        }
        
        // Read
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address, int length)
        {
            switch (dataTypeEnum)
            {
                case DataTypeEnums.Bool:
                    return length <= 1 ? ToObjectResult(ReadBoolean(address)) : ToObjectResult(ReadBoolean(address, length));
                case DataTypeEnums.Byte:
                    return length <= 1 ? ToObjectResult(ReadByte(address)) : ToObjectResult(ReadByte(address, length));
                case DataTypeEnums.UInt16:
                    return length <= 1 ? ToObjectResult(ReadUInt16(address)) : ToObjectResult(ReadUInt16(address, length));
                case DataTypeEnums.Int16:
                    return length <= 1 ? ToObjectResult(ReadInt16(address)) : ToObjectResult(ReadInt16(address, length));
                case DataTypeEnums.UInt32:
                    return length <= 1 ? ToObjectResult(ReadUInt32(address)) : ToObjectResult(ReadUInt32(address, length));
                case DataTypeEnums.Int32:
                    return length <= 1 ? ToObjectResult(ReadInt32(address)) : ToObjectResult(ReadInt32(address, length));
                case DataTypeEnums.UInt64:
                    return length <= 1 ? ToObjectResult(ReadUInt64(address)) : ToObjectResult(ReadUInt64(address, length));
                case DataTypeEnums.Int64:
                    return length <= 1 ? ToObjectResult(ReadInt64(address)) : ToObjectResult(ReadInt64(address, length));
                case DataTypeEnums.Float:
                    return length <= 1 ? ToObjectResult(ReadFloat(address)) : ToObjectResult(ReadFloat(address, length));
                case DataTypeEnums.Double:
                    return length <= 1 ? ToObjectResult(ReadDouble(address)) : ToObjectResult(ReadDouble(address, length));
                case DataTypeEnums.String:
                    return ToObjectResult(ReadString(address, length));
                default:
                    return OperationResult.CreateFailedResult<object>($"不支持的数据类型: {dataTypeEnum}");
            }
        }
        
        // BatchReadAsync
        public ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            return new ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>>(BatchRead(addresses));
        }
        
        // ReadByteAsync
        public ValueTask<OperationResult<byte>> ReadByteAsync(string address)
        {
            return new ValueTask<OperationResult<byte>>(ReadByte(address));
        }
        
        // ReadByteAsync
        public ValueTask<OperationResult<byte[]>> ReadByteAsync(string address, int length)
        {
            return new ValueTask<OperationResult<byte[]>>(ReadByte(address, length));
        }
        
        // ReadBooleanAsync
        public ValueTask<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            return new ValueTask<OperationResult<bool>>(ReadBoolean(address));
        }
        
        // ReadBooleanAsync
        public ValueTask<OperationResult<bool[]>> ReadBooleanAsync(string address, int length)
        {
            return new ValueTask<OperationResult<bool[]>>(ReadBoolean(address, length));
        }
        
        // ReadUInt16Async
        public ValueTask<OperationResult<ushort>> ReadUInt16Async(string address)
        {
            return new ValueTask<OperationResult<ushort>>(ReadUInt16(address));
        }
        
        // ReadUInt16Async
        public ValueTask<OperationResult<ushort[]>> ReadUInt16Async(string address, int length)
        {
            return new ValueTask<OperationResult<ushort[]>>(ReadUInt16(address, length));
        }
        
        // ReadInt16Async
        public ValueTask<OperationResult<short>> ReadInt16Async(string address)
        {
            return new ValueTask<OperationResult<short>>(ReadInt16(address));
        }
        
        // ReadInt16Async
        public ValueTask<OperationResult<short[]>> ReadInt16Async(string address, int length)
        {
            return new ValueTask<OperationResult<short[]>>(ReadInt16(address, length));
        }
        
        // ReadUInt32Async
        public ValueTask<OperationResult<uint>> ReadUInt32Async(string address)
        {
            return new ValueTask<OperationResult<uint>>(ReadUInt32(address));
        }
        
        // ReadUInt32Async
        public ValueTask<OperationResult<uint[]>> ReadUInt32Async(string address, int length)
        {
            return new ValueTask<OperationResult<uint[]>>(ReadUInt32(address, length));
        }
        
        // ReadInt32Async
        public ValueTask<OperationResult<int>> ReadInt32Async(string address)
        {
            return new ValueTask<OperationResult<int>>(ReadInt32(address));
        }
        
        // ReadInt32Async
        public ValueTask<OperationResult<int[]>> ReadInt32Async(string address, int length)
        {
            return new ValueTask<OperationResult<int[]>>(ReadInt32(address, length));
        }
        
        // ReadUInt64Async
        public ValueTask<OperationResult<ulong>> ReadUInt64Async(string address)
        {
            return new ValueTask<OperationResult<ulong>>(ReadUInt64(address));
        }
        
        // ReadUInt64Async
        public ValueTask<OperationResult<ulong[]>> ReadUInt64Async(string address, int length)
        {
            return new ValueTask<OperationResult<ulong[]>>(ReadUInt64(address, length));
        }
        
        // ReadInt64Async
        public ValueTask<OperationResult<long>> ReadInt64Async(string address)
        {
            return new ValueTask<OperationResult<long>>(ReadInt64(address));
        }
        
        // ReadInt64Async
        public ValueTask<OperationResult<long[]>> ReadInt64Async(string address, int length)
        {
            return new ValueTask<OperationResult<long[]>>(ReadInt64(address, length));
        }
        
        // ReadFloatAsync
        public ValueTask<OperationResult<float>> ReadFloatAsync(string address)
        {
            return new ValueTask<OperationResult<float>>(ReadFloat(address));
        }
        
        // ReadFloatAsync
        public ValueTask<OperationResult<float[]>> ReadFloatAsync(string address, int length)
        {
            return new ValueTask<OperationResult<float[]>>(ReadFloat(address, length));
        }
        
        // ReadDoubleAsync
        public ValueTask<OperationResult<double>> ReadDoubleAsync(string address)
        {
            return new ValueTask<OperationResult<double>>(ReadDouble(address));
        }
        
        // ReadDoubleAsync
        public ValueTask<OperationResult<double[]>> ReadDoubleAsync(string address, int length)
        {
            return new ValueTask<OperationResult<double[]>>(ReadDouble(address, length));
        }
        
        // ReadStringAsync
        public ValueTask<OperationResult<string>> ReadStringAsync(string address, int length)
        {
            return new ValueTask<OperationResult<string>>(ReadString(address, length));
        }
        
        // ReadAsync
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address)
        {
            return new ValueTask<OperationResult<object>>(Read(dataTypeEnum, address));
        }
        
        // ReadAsync
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address, int length)
        {
            return new ValueTask<OperationResult<object>>(Read(dataTypeEnum, address, length));
        }
        
        // BatchWrite
        public OperationResult BatchWrite(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            try
            {
                if (addresses == null || addresses.Count == 0)
                {
                    return OperationResult.CreateSuccessResult();
                }

                var addressInfos = S7BatchHelper.ParseS7Addresses(addresses);
                if (addressInfos.Count == 0)
                {
                    return OperationResult.CreateFailedResult("没有有效的S7地址可以写入");
                }

                var optimizedBlocks = S7BatchHelper.OptimizeS7AddressBlocks(addressInfos);
                if (optimizedBlocks.Count == 0)
                {
                    return OperationResult.CreateFailedResult("S7写入地址块优化失败");
                }

                var errors = new List<string>();
                var successCount = 0;

                foreach (var block in optimizedBlocks)
                {
                    var buildResult = BuildWriteBuffer(block, addresses);
                    if (!buildResult.IsSuccess)
                    {
                        errors.Add($"构建写入块 {DescribeBlock(block)} 失败: {buildResult.Message}");
                        continue;
                    }

                    var writeResult = WriteBlock(block, buildResult.ResultValue);
                    if (writeResult.IsSuccess)
                    {
                        successCount += block.Addresses.Count;
                    }
                    else
                    {
                        errors.Add($"写入块 {DescribeBlock(block)} 失败: {writeResult.Message}");
                    }
                }

                if (errors.Count == 0)
                {
                    return OperationResult.CreateSuccessResult();
                }

                return OperationResult.CreateFailedResult($"批量写入部分失败: {string.Join("; ", errors)}");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "批量写入失败");
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        private OperationResult<byte[]> ReadBlock(S7BatchHelper.S7AddressBlock block)
        {
            try
            {
                if (block.Addresses.Count == 0)
                {
                    return OperationResult.CreateFailedResult<byte[]>("地址块为空");
                }

                var area = GetArea(block.Addresses[0]);
                return ReadArea(area, block.DbNumber, block.StartByte, block.TotalLength);
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<byte[]>(ex.Message);
            }
        }

        private OperationResult WriteBlock(S7BatchHelper.S7AddressBlock block, byte[] buffer)
        {
            try
            {
                if (block.Addresses.Count == 0)
                {
                    return OperationResult.CreateFailedResult("地址块为空");
                }

                var area = GetArea(block.Addresses[0]);
                return WriteArea(area, block.DbNumber, block.StartByte, buffer);
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        private OperationResult<byte[]> BuildWriteBuffer(S7BatchHelper.S7AddressBlock block, Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            try
            {
                var readResult = ReadBlock(block);
                if (!readResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<byte[]>(readResult.Message);
                }

                var buffer = readResult.ResultValue.ToArray();
                foreach (var addressInfo in block.Addresses)
                {
                    if (!addresses.TryGetValue(addressInfo.OriginalAddress, out var valueTuple))
                    {
                        return OperationResult.CreateFailedResult<byte[]>($"地址 {addressInfo.OriginalAddress} 缺少写入值");
                    }

                    var converted = S7BatchHelper.ConvertValueToS7Bytes(valueTuple.Item2, addressInfo, IsReverse, DataFormat);
                    if (converted == null || converted.Length == 0)
                    {
                        return OperationResult.CreateFailedResult<byte[]>($"地址 {addressInfo.OriginalAddress} 的值转换失败");
                    }

                    var offset = addressInfo.StartByte - block.StartByte;
                    if (offset < 0 || offset >= buffer.Length)
                    {
                        return OperationResult.CreateFailedResult<byte[]>($"地址 {addressInfo.OriginalAddress} 的写入偏移超出范围");
                    }

                    if (S7BatchHelper.IsBitType(addressInfo.DataType))
                    {
                        if (converted[0] == 0)
                        {
                            buffer[offset] = (byte)(buffer[offset] & ~(1 << addressInfo.BitOffset));
                        }
                        else
                        {
                            buffer[offset] = (byte)(buffer[offset] | (1 << addressInfo.BitOffset));
                        }
                    }
                    else
                    {
                        if (offset + converted.Length > buffer.Length)
                        {
                            return OperationResult.CreateFailedResult<byte[]>($"地址 {addressInfo.OriginalAddress} 的写入长度超出范围");
                        }

                        Array.Copy(converted, 0, buffer, offset, converted.Length);
                    }
                }

                return new OperationResult<byte[]>
                {
                    ResultValue = buffer
                }.Complete();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<byte[]>(ex.Message);
            }
        }

        private Dictionary<string, object> ExtractValuesFromBlocks(Dictionary<string, byte[]> blockData, List<S7BatchHelper.S7AddressBlock> blocks)
        {
            var result = new Dictionary<string, object>();

            foreach (var block in blocks)
            {
                if (!blockData.TryGetValue(BuildBlockKey(block), out var data))
                {
                    foreach (var addressInfo in block.Addresses)
                    {
                        result[addressInfo.OriginalAddress] = null;
                    }

                    continue;
                }

                foreach (var addressInfo in block.Addresses)
                {
                    try
                    {
                        var offset = addressInfo.StartByte - block.StartByte;
                        if (offset < 0 || offset >= data.Length)
                        {
                            result[addressInfo.OriginalAddress] = null;
                            continue;
                        }

                        if (S7BatchHelper.IsBitType(addressInfo.DataType))
                        {
                            result[addressInfo.OriginalAddress] = (data[offset] & (1 << addressInfo.BitOffset)) != 0;
                            continue;
                        }

                        if (offset + addressInfo.Length > data.Length)
                        {
                            result[addressInfo.OriginalAddress] = null;
                            continue;
                        }

                        var segment = new byte[addressInfo.Length];
                        Array.Copy(data, offset, segment, 0, addressInfo.Length);
                        result[addressInfo.OriginalAddress] = ConvertBytesToValue(segment, addressInfo.TargetDataType, addressInfo.Length);
                    }
                    catch
                    {
                        result[addressInfo.OriginalAddress] = null;
                    }
                }
            }

            return result;
        }

        private static string BuildBlockKey(S7BatchHelper.S7AddressBlock block)
        {
            if (block.Addresses.Count == 0)
            {
                return $"{block.DbNumber}_{block.StartByte}_{block.TotalLength}";
            }

            var areaType = S7BatchHelper.GetS7AreaType(block.Addresses[0].DataType);
            return areaType == "DB" || areaType == "V"
                ? $"DB{block.DbNumber}_{block.StartByte}_{block.TotalLength}"
                : $"{areaType}_{block.StartByte}_{block.TotalLength}";
        }

        private static string DescribeBlock(S7BatchHelper.S7AddressBlock block)
        {
            if (block.Addresses.Count == 0)
            {
                return $"{block.StartByte}-{block.StartByte + block.TotalLength - 1}";
            }

            var areaType = S7BatchHelper.GetS7AreaType(block.Addresses[0].DataType);
            return areaType == "DB" || areaType == "V"
                ? $"DB{block.DbNumber}:{block.StartByte}-{block.StartByte + block.TotalLength - 1}"
                : $"{areaType}:{block.StartByte}-{block.StartByte + block.TotalLength - 1}";
        }

        private static S7Area GetArea(S7BatchHelper.S7AddressInfo addressInfo)
        {
            var areaType = S7BatchHelper.GetS7AreaType(addressInfo.DataType);
            switch (areaType)
            {
                case "DB":
                case "V":
                    return S7Area.DB;
                case "I":
                    return S7Area.I;
                case "Q":
                    return S7Area.Q;
                case "M":
                    return S7Area.M;
                default:
                    throw new ArgumentException($"不支持的S7区域类型: {addressInfo.DataType}");
            }
        }
        
        // Write
        public OperationResult Write(string address, byte[] value)
        {
            try
            {
                if (value == null || value.Length == 0)
                {
                    return OperationResult.CreateFailedResult("写入数据不能为空");
                }

                var addressParseResult = ParseS7Address(address);
                if (addressParseResult == null)
                {
                    return OperationResult.CreateFailedResult($"无效的S7地址: {address}");
                }

                var addressInfo = addressParseResult.Value.AddressInfo;
                var area = addressParseResult.Value.Area;

                var writeResult = WriteArea(area, addressInfo.DbNumber, addressInfo.StartByte, value);
                return writeResult;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "写入字节数组失败: {Address}", address);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        // Write
        public OperationResult Write(string address, bool value)
        {
            try
            {
                var addressParseResult = ParseS7Address(address);
                if (addressParseResult == null)
                {
                    return OperationResult.CreateFailedResult($"无效的S7地址: {address}");
                }

                var addressInfo = addressParseResult.Value.AddressInfo;
                var area = addressParseResult.Value.Area;

                var data = new byte[] { (byte)(value ? 1 : 0) };
                var writeResult = WriteArea(area, addressInfo.DbNumber, addressInfo.StartByte, data);
                return writeResult;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "写入布尔值失败: {Address}", address);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        // Write
        public OperationResult Write(string address, bool[] value)
        {
            try
            {
                if (value == null || value.Length == 0)
                {
                    return OperationResult.CreateFailedResult("写入数据不能为空");
                }

                var addressParseResult = ParseS7Address(address);
                if (addressParseResult == null)
                {
                    return OperationResult.CreateFailedResult($"无效的S7地址: {address}");
                }

                var addressInfo = addressParseResult.Value.AddressInfo;
                var area = addressParseResult.Value.Area;

                var data = new byte[value.Length];
                for (int i = 0; i < value.Length; i++)
                {
                    data[i] = (byte)(value[i] ? 1 : 0);
                }

                var writeResult = WriteArea(area, addressInfo.DbNumber, addressInfo.StartByte, data);
                return writeResult;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "写入布尔数组失败: {Address}", address);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        // Write
        public OperationResult Write(string address, byte value)
        {
            try
            {
                var addressParseResult = ParseS7Address(address);
                if (addressParseResult == null)
                {
                    return OperationResult.CreateFailedResult($"无效的S7地址: {address}");
                }

                var addressInfo = addressParseResult.Value.AddressInfo;
                var area = addressParseResult.Value.Area;

                var data = new byte[] { value };
                var writeResult = WriteArea(area, addressInfo.DbNumber, addressInfo.StartByte, data);
                return writeResult;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "写入字节失败: {Address}", address);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        // Write
        public OperationResult Write(string address, ushort value)
        {
            return WriteSingleValue(address, DataTypeEnums.UInt16, value);
        }
        
        // Write
        public OperationResult Write(string address, ushort[] value)
        {
            return WriteArrayValue(address, value, DataTypeEnums.UInt16);
        }
        
        // Write
        public OperationResult Write(string address, short value)
        {
            return WriteSingleValue(address, DataTypeEnums.Int16, value);
        }
        
        // Write
        public OperationResult Write(string address, short[] value)
        {
            return WriteArrayValue(address, value, DataTypeEnums.Int16);
        }
        
        // Write
        public OperationResult Write(string address, uint value)
        {
            return WriteSingleValue(address, DataTypeEnums.UInt32, value);
        }
        
        // Write
        public OperationResult Write(string address, uint[] value)
        {
            return WriteArrayValue(address, value, DataTypeEnums.UInt32);
        }
        
        // Write
        public OperationResult Write(string address, int value)
        {
            return WriteSingleValue(address, DataTypeEnums.Int32, value);
        }
        
        // Write
        public OperationResult Write(string address, int[] value)
        {
            return WriteArrayValue(address, value, DataTypeEnums.Int32);
        }
        
        // Write
        public OperationResult Write(string address, ulong value)
        {
            return WriteSingleValue(address, DataTypeEnums.UInt64, value);
        }
        
        // Write
        public OperationResult Write(string address, ulong[] value)
        {
            return WriteArrayValue(address, value, DataTypeEnums.UInt64);
        }
        
        // Write
        public OperationResult Write(string address, long value)
        {
            return WriteSingleValue(address, DataTypeEnums.Int64, value);
        }
        
        // Write
        public OperationResult Write(string address, long[] value)
        {
            return WriteArrayValue(address, value, DataTypeEnums.Int64);
        }
        
        // Write
        public OperationResult Write(string address, float value)
        {
            return WriteSingleValue(address, DataTypeEnums.Float, value);
        }
        
        // Write
        public OperationResult Write(string address, float[] value)
        {
            return WriteArrayValue(address, value, DataTypeEnums.Float);
        }
        
        // Write
        public OperationResult Write(string address, double value)
        {
            return WriteSingleValue(address, DataTypeEnums.Double, value);
        }
        
        // Write
        public OperationResult Write(string address, double[] value)
        {
            return WriteArrayValue(address, value, DataTypeEnums.Double);
        }
        
        // Write
        public OperationResult Write(string address, string value)
        {
            return WriteSingleValue(address, DataTypeEnums.String, value);
        }
        
        // Write
        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object value)
        {
            return WriteSingleValue(address, dataTypeEnum, value);
        }
        
        // Write
        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object[] value)
        {
            return WriteArrayValue(address, value, dataTypeEnum);
        }
        
        // BatchWriteAsync
        public ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            return new ValueTask<OperationResult>(BatchWrite(addresses));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, byte[] value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, bool value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, byte value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, ushort value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, ushort[] value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, short value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, short[] value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, uint value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, uint[] value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, int value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, int[] value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, ulong value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, ulong[] value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, long value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, long[] value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, float value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, float[] value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, double value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, double[] value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, string value)
        {
            return Task.FromResult(Write(address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object value)
        {
            return Task.FromResult(Write(dataTypeEnum, address, value));
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object[] value)
        {
            return Task.FromResult(Write(dataTypeEnum, address, value));
        }
        
        #endregion
        
        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopSnapshotTimer();
                _snapshotTimer?.Dispose();

                // 确保服务器已关闭
                Shutdown();
                
                // 释放传输资源
                _serverTransport?.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
}

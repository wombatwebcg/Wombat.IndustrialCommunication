using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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
        }
        
        /// <summary>
        /// 开始监听
        /// </summary>
        /// <returns>操作结果</returns>
        public OperationResult Listen()
        {
            return StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// 停止监听
        /// </summary>
        /// <returns>操作结果</returns>
        public OperationResult Shutdown()
        {
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
                            addressInfo.StartByte = int.Parse(dataPart.Substring(3));
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
                    addressInfo.StartByte = int.Parse(address.Substring(1));
                    addressInfo.Length = 1;
                }
                else if (address.StartsWith("I"))
                {
                    area = S7Area.I;
                    addressInfo.DataType = S7DataType.IB;
                    addressInfo.StartByte = int.Parse(address.Substring(1));
                    addressInfo.Length = 1;
                }
                else if (address.StartsWith("Q"))
                {
                    area = S7Area.Q;
                    addressInfo.DataType = S7DataType.QB;
                    addressInfo.StartByte = int.Parse(address.Substring(1));
                    addressInfo.Length = 1;
                }
                else if (address.StartsWith("V"))
                {
                    area = S7Area.DB; // V区映射到DB1
                    addressInfo.DataType = S7DataType.VB;
                    addressInfo.StartByte = int.Parse(address.Substring(1));
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

                switch (dataType)
                {
                    case DataTypeEnums.Bool:
                        return data[0] != 0;
                    case DataTypeEnums.Byte:
                        return data[0];
                    case DataTypeEnums.Int16:
                        return BitConverter.ToInt16(data, 0);
                    case DataTypeEnums.UInt16:
                        return BitConverter.ToUInt16(data, 0);
                    case DataTypeEnums.Int32:
                        return BitConverter.ToInt32(data, 0);
                    case DataTypeEnums.UInt32:
                        return BitConverter.ToUInt32(data, 0);
                    case DataTypeEnums.Int64:
                        return BitConverter.ToInt64(data, 0);
                    case DataTypeEnums.UInt64:
                        return BitConverter.ToUInt64(data, 0);
                    case DataTypeEnums.Float:
                        return BitConverter.ToSingle(data, 0);
                    case DataTypeEnums.Double:
                        return BitConverter.ToDouble(data, 0);
                    case DataTypeEnums.String:
                        return Encoding.ASCII.GetString(data);
                    default:
                        return data;
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
                        return BitConverter.GetBytes((short)value);
                    case DataTypeEnums.UInt16:
                        return BitConverter.GetBytes((ushort)value);
                    case DataTypeEnums.Int32:
                        return BitConverter.GetBytes((int)value);
                    case DataTypeEnums.UInt32:
                        return BitConverter.GetBytes((uint)value);
                    case DataTypeEnums.Int64:
                        return BitConverter.GetBytes((long)value);
                    case DataTypeEnums.UInt64:
                        return BitConverter.GetBytes((ulong)value);
                    case DataTypeEnums.Float:
                        return BitConverter.GetBytes((float)value);
                    case DataTypeEnums.Double:
                        return BitConverter.GetBytes((double)value);
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

                var result = new Dictionary<string, (DataTypeEnums, object)>();
                var errors = new List<string>();

                foreach (var kvp in addresses)
                {
                    try
                    {
                        var address = kvp.Key;
                        var dataType = kvp.Value;
                        
                        // 解析S7地址
                        var addressParseResult = ParseS7Address(address);
                        if (addressParseResult == null)
                        {
                            errors.Add($"无效的S7地址: {address}");
                            continue;
                        }

                        var addressInfo = addressParseResult.Value.AddressInfo;
                        var area = addressParseResult.Value.Area;

                        // 读取数据
                        var readResult = ReadArea(area, addressInfo.DbNumber, addressInfo.StartByte, addressInfo.Length);
                        if (readResult.IsSuccess)
                        {
                            // 转换数据类型
                            var convertedValue = ConvertBytesToValue(readResult.ResultValue, dataType, addressInfo.Length);
                            result[address] = (dataType, convertedValue);
                        }
                        else
                        {
                            errors.Add($"读取地址 {address} 失败: {readResult.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"处理地址 {kvp.Key} 时发生错误: {ex.Message}");
                    }
                }

                if (errors.Count > 0)
                {
                    return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>(
                        $"批量读取部分失败: {string.Join("; ", errors)}");
                }

                return OperationResult.CreateSuccessResult(result);
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
        
        // ReadUInt16
        public OperationResult<ushort> ReadUInt16(string address)
        {
            return CreateNotSupportedResult<ushort>();
        }
        
        // ReadUInt16
        public OperationResult<ushort[]> ReadUInt16(string address, int length)
        {
            return CreateNotSupportedResult<ushort[]>();
        }
        
        // ReadInt16
        public OperationResult<short> ReadInt16(string address)
        {
            return CreateNotSupportedResult<short>();
        }
        
        // ReadInt16
        public OperationResult<short[]> ReadInt16(string address, int length)
        {
            return CreateNotSupportedResult<short[]>();
        }
        
        // ReadUInt32
        public OperationResult<uint> ReadUInt32(string address)
        {
            return CreateNotSupportedResult<uint>();
        }
        
        // ReadUInt32
        public OperationResult<uint[]> ReadUInt32(string address, int length)
        {
            return CreateNotSupportedResult<uint[]>();
        }
        
        // ReadInt32
        public OperationResult<int> ReadInt32(string address)
        {
            return CreateNotSupportedResult<int>();
        }
        
        // ReadInt32
        public OperationResult<int[]> ReadInt32(string address, int length)
        {
            return CreateNotSupportedResult<int[]>();
        }
        
        // ReadUInt64
        public OperationResult<ulong> ReadUInt64(string address)
        {
            return CreateNotSupportedResult<ulong>();
        }
        
        // ReadUInt64
        public OperationResult<ulong[]> ReadUInt64(string address, int length)
        {
            return CreateNotSupportedResult<ulong[]>();
        }
        
        // ReadInt64
        public OperationResult<long> ReadInt64(string address)
        {
            return CreateNotSupportedResult<long>();
        }
        
        // ReadInt64
        public OperationResult<long[]> ReadInt64(string address, int length)
        {
            return CreateNotSupportedResult<long[]>();
        }
        
        // ReadFloat
        public OperationResult<float> ReadFloat(string address)
        {
            return CreateNotSupportedResult<float>();
        }
        
        // ReadFloat
        public OperationResult<float[]> ReadFloat(string address, int length)
        {
            return CreateNotSupportedResult<float[]>();
        }
        
        // ReadDouble
        public OperationResult<double> ReadDouble(string address)
        {
            return CreateNotSupportedResult<double>();
        }
        
        // ReadDouble
        public OperationResult<double[]> ReadDouble(string address, int length)
        {
            return CreateNotSupportedResult<double[]>();
        }
        
        // ReadString
        public OperationResult<string> ReadString(string address, int length)
        {
            return CreateNotSupportedResult<string>();
        }
        
        // Read
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address)
        {
            return CreateNotSupportedResult<object>();
        }
        
        // Read
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address, int length)
        {
            return CreateNotSupportedResult<object>();
        }
        
        // BatchReadAsync
        public ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            return new ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>>(CreateNotSupportedResult<Dictionary<string, (DataTypeEnums, object)>>());
        }
        
        // ReadByteAsync
        public ValueTask<OperationResult<byte>> ReadByteAsync(string address)
        {
            return new ValueTask<OperationResult<byte>>(CreateNotSupportedResult<byte>());
        }
        
        // ReadByteAsync
        public ValueTask<OperationResult<byte[]>> ReadByteAsync(string address, int length)
        {
            return new ValueTask<OperationResult<byte[]>>(CreateNotSupportedResult<byte[]>());
        }
        
        // ReadBooleanAsync
        public ValueTask<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            return new ValueTask<OperationResult<bool>>(CreateNotSupportedResult<bool>());
        }
        
        // ReadBooleanAsync
        public ValueTask<OperationResult<bool[]>> ReadBooleanAsync(string address, int length)
        {
            return new ValueTask<OperationResult<bool[]>>(CreateNotSupportedResult<bool[]>());
        }
        
        // ReadUInt16Async
        public ValueTask<OperationResult<ushort>> ReadUInt16Async(string address)
        {
            return new ValueTask<OperationResult<ushort>>(CreateNotSupportedResult<ushort>());
        }
        
        // ReadUInt16Async
        public ValueTask<OperationResult<ushort[]>> ReadUInt16Async(string address, int length)
        {
            return new ValueTask<OperationResult<ushort[]>>(CreateNotSupportedResult<ushort[]>());
        }
        
        // ReadInt16Async
        public ValueTask<OperationResult<short>> ReadInt16Async(string address)
        {
            return new ValueTask<OperationResult<short>>(CreateNotSupportedResult<short>());
        }
        
        // ReadInt16Async
        public ValueTask<OperationResult<short[]>> ReadInt16Async(string address, int length)
        {
            return new ValueTask<OperationResult<short[]>>(CreateNotSupportedResult<short[]>());
        }
        
        // ReadUInt32Async
        public ValueTask<OperationResult<uint>> ReadUInt32Async(string address)
        {
            return new ValueTask<OperationResult<uint>>(CreateNotSupportedResult<uint>());
        }
        
        // ReadUInt32Async
        public ValueTask<OperationResult<uint[]>> ReadUInt32Async(string address, int length)
        {
            return new ValueTask<OperationResult<uint[]>>(CreateNotSupportedResult<uint[]>());
        }
        
        // ReadInt32Async
        public ValueTask<OperationResult<int>> ReadInt32Async(string address)
        {
            return new ValueTask<OperationResult<int>>(CreateNotSupportedResult<int>());
        }
        
        // ReadInt32Async
        public ValueTask<OperationResult<int[]>> ReadInt32Async(string address, int length)
        {
            return new ValueTask<OperationResult<int[]>>(CreateNotSupportedResult<int[]>());
        }
        
        // ReadUInt64Async
        public ValueTask<OperationResult<ulong>> ReadUInt64Async(string address)
        {
            return new ValueTask<OperationResult<ulong>>(CreateNotSupportedResult<ulong>());
        }
        
        // ReadUInt64Async
        public ValueTask<OperationResult<ulong[]>> ReadUInt64Async(string address, int length)
        {
            return new ValueTask<OperationResult<ulong[]>>(CreateNotSupportedResult<ulong[]>());
        }
        
        // ReadInt64Async
        public ValueTask<OperationResult<long>> ReadInt64Async(string address)
        {
            return new ValueTask<OperationResult<long>>(CreateNotSupportedResult<long>());
        }
        
        // ReadInt64Async
        public ValueTask<OperationResult<long[]>> ReadInt64Async(string address, int length)
        {
            return new ValueTask<OperationResult<long[]>>(CreateNotSupportedResult<long[]>());
        }
        
        // ReadFloatAsync
        public ValueTask<OperationResult<float>> ReadFloatAsync(string address)
        {
            return new ValueTask<OperationResult<float>>(CreateNotSupportedResult<float>());
        }
        
        // ReadFloatAsync
        public ValueTask<OperationResult<float[]>> ReadFloatAsync(string address, int length)
        {
            return new ValueTask<OperationResult<float[]>>(CreateNotSupportedResult<float[]>());
        }
        
        // ReadDoubleAsync
        public ValueTask<OperationResult<double>> ReadDoubleAsync(string address)
        {
            return new ValueTask<OperationResult<double>>(CreateNotSupportedResult<double>());
        }
        
        // ReadDoubleAsync
        public ValueTask<OperationResult<double[]>> ReadDoubleAsync(string address, int length)
        {
            return new ValueTask<OperationResult<double[]>>(CreateNotSupportedResult<double[]>());
        }
        
        // ReadStringAsync
        public ValueTask<OperationResult<string>> ReadStringAsync(string address, int length)
        {
            return new ValueTask<OperationResult<string>>(CreateNotSupportedResult<string>());
        }
        
        // ReadAsync
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address)
        {
            return new ValueTask<OperationResult<object>>(CreateNotSupportedResult<object>());
        }
        
        // ReadAsync
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address, int length)
        {
            return new ValueTask<OperationResult<object>>(CreateNotSupportedResult<object>());
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

                var errors = new List<string>();
                var successCount = 0;

                foreach (var kvp in addresses)
                {
                    try
                    {
                        var address = kvp.Key;
                        var (dataType, value) = kvp.Value;

                        // 解析S7地址
                        var addressParseResult = ParseS7Address(address);
                        if (addressParseResult == null)
                        {
                            errors.Add($"无效的S7地址: {address}");
                            continue;
                        }

                        var addressInfo = addressParseResult.Value.AddressInfo;
                        var area = addressParseResult.Value.Area;

                        // 将值转换为字节数组
                        byte[] data = ConvertValueToBytes(value, dataType);
                        if (data == null)
                        {
                            errors.Add($"地址 {address} 的值转换失败");
                            continue;
                        }

                        // 写入数据
                        var writeResult = WriteArea(area, addressInfo.DbNumber, addressInfo.StartByte, data);
                        if (writeResult.IsSuccess)
                        {
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"写入地址 {address} 失败: {writeResult.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"处理地址 {kvp.Key} 时发生错误: {ex.Message}");
                    }
                }

                if (errors.Count > 0)
                {
                    return OperationResult.CreateFailedResult(
                        $"批量写入部分失败: {string.Join("; ", errors)}");
                }

                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "批量写入失败");
                return OperationResult.CreateFailedResult(ex.Message);
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
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, ushort[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, short value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, short[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, uint value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, uint[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, int value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, int[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, ulong value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, ulong[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, long value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, long[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, float value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, float[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, double value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, double[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, string value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // BatchWriteAsync
        public ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            return new ValueTask<OperationResult>(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, byte[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, bool value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, byte value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, ushort value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, ushort[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, short value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, short[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, uint value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, uint[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, int value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, int[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, ulong value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, ulong[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, long value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, long[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, float value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, float[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, double value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, double[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(string address, string value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        // WriteAsync
        public Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
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
                // 确保服务器已关闭
                Shutdown();
                
                // 释放传输资源
                _serverTransport?.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
}
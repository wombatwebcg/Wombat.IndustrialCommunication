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
        
        #region IReadWrite 接口实现
        
        private OperationResult<T> CreateNotSupportedResult<T>()
        {
            return new OperationResult<T>
            {
                IsSuccess = false,
                Message = "S7 TCP服务器不支持此操作。服务器端不应直接调用读取方法。"
            };
        }
        
        private OperationResult CreateNotSupportedResult()
        {
            return new OperationResult
            {
                IsSuccess = false,
                Message = "S7 TCP服务器不支持此操作。服务器端不应直接调用写入方法。"
            };
        }
        
        // BatchRead
        public OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnums> addresses)
        {
            return CreateNotSupportedResult<Dictionary<string, object>>();
        }
        
        // ReadByte
        public OperationResult<byte> ReadByte(string address)
        {
            return CreateNotSupportedResult<byte>();
        }
        
        // ReadByte
        public OperationResult<byte[]> ReadByte(string address, int length)
        {
            return CreateNotSupportedResult<byte[]>();
        }
        
        // ReadBoolean
        public OperationResult<bool> ReadBoolean(string address)
        {
            return CreateNotSupportedResult<bool>();
        }
        
        // ReadBoolean
        public OperationResult<bool[]> ReadBoolean(string address, int length)
        {
            return CreateNotSupportedResult<bool[]>();
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
        public ValueTask<OperationResult<Dictionary<string, object>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            return new ValueTask<OperationResult<Dictionary<string, object>>>(CreateNotSupportedResult<Dictionary<string, object>>());
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
        public OperationResult BatchWrite(Dictionary<string, object> addresses)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, byte[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, bool value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, bool[] value)
        {
            return CreateNotSupportedResult();
        }
        
        // Write
        public OperationResult Write(string address, byte value)
        {
            return CreateNotSupportedResult();
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
        public Task<OperationResult> BatchWriteAsync(Dictionary<string, object> addresses)
        {
            return Task.FromResult(CreateNotSupportedResult());
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
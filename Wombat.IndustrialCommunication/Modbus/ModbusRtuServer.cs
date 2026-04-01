using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus.Data;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Modbus RTU服务器
    /// </summary>
    public class ModbusRtuServer : ModbusRtuServerBase, IDeviceServer
    {
        private readonly SerialPortServerAdapter _serialPortServerAdapter;
        private readonly ServerMessageTransport _serverTransport;
        private const int DEFAULT_TIMEOUT_MS = 3000;
        private readonly object _snapshotSyncRoot = new object();
        private Timer _snapshotTimer;
        private volatile bool _snapshotDirty;
        private bool _enableSnapshotPersistence;
        
        /// <summary>
        /// 串口名称
        /// </summary>
        public string PortName { get; private set; }
        
        /// <summary>
        /// 波特率
        /// </summary>
        public int BaudRate { get; private set; }
        
        /// <summary>
        /// 数据位
        /// </summary>
        public int DataBits { get; private set; }
        
        /// <summary>
        /// 停止位
        /// </summary>
        public StopBits StopBits { get; private set; }
        
        /// <summary>
        /// 校验位
        /// </summary>
        public Parity Parity { get; private set; }
        
        /// <summary>
        /// 握手协议
        /// </summary>
        public Handshake Handshake { get; private set; }
        
        /// <summary>
        /// 是否正在监听
        /// </summary>
        public new bool IsListening => base.IsListening;

        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);

        /// <summary>
        /// 接收超时
        /// </summary>
        public TimeSpan ReceiveTimeout
        {
            get => _serialPortServerAdapter.ReceiveTimeout;
            set => _serialPortServerAdapter.ReceiveTimeout = value;
        }

        /// <summary>
        /// 发送超时
        /// </summary>
        public TimeSpan SendTimeout
        {
            get => _serialPortServerAdapter.SendTimeout;
            set => _serialPortServerAdapter.SendTimeout = value;
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
        /// 构造函数
        /// </summary>
        public ModbusRtuServer()
            : this("COM1", 9600, 8, StopBits.One, Parity.None, Handshake.None)
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="portName">串口名称</param>
        public ModbusRtuServer(string portName)
            : this(portName, 9600, 8, StopBits.One, Parity.None, Handshake.None)
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        public ModbusRtuServer(string portName, int baudRate)
            : this(portName, baudRate, 8, StopBits.One, Parity.None, Handshake.None)
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <param name="handshake">握手协议</param>
        public ModbusRtuServer(string portName, int baudRate, int dataBits, StopBits stopBits, Parity parity, Handshake handshake)
            : base(CreateTransport(portName, baudRate, dataBits, stopBits, parity, handshake))
        {
            _serialPortServerAdapter = (SerialPortServerAdapter)base._transport.StreamResource;
            _serverTransport = base._transport;
            
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            StopBits = stopBits;
            Parity = parity;
            Handshake = handshake;
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
            _serialPortServerAdapter.UseLogger(logger);
        }

        /// <summary>
        /// 创建传输
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <param name="handshake">握手协议</param>
        /// <returns>服务器消息传输</returns>
        private static ServerMessageTransport CreateTransport(string portName, int baudRate, int dataBits, StopBits stopBits, Parity parity, Handshake handshake)
        {
            var adapter = new SerialPortServerAdapter(portName, baudRate, dataBits, stopBits, parity, handshake);
            return new ServerMessageTransport(adapter);
        }

        private void InitializeSnapshotPersistence()
        {
            ConfigureSnapshotPersistence();
            DataStore.DataStoreWrittenTo += HandleSnapshotDataWritten;
        }

        public void ConfigureSnapshotPersistence(string name = null)
        {
            SnapshotFilePath = SnapshotFilePathHelper.Build("ModbusRtuServer", PortName, name);
        }

        private void HandleSnapshotDataWritten(object sender, DataStoreEventArgs e)
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

                ServerSnapshotPersistence.LoadModbusSnapshot(SnapshotFilePath, DataStore);
                _snapshotDirty = false;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "加载Modbus RTU快照失败: {SnapshotFilePath}", SnapshotFilePath);
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
                Logger?.LogError(ex, "保存Modbus RTU快照失败: {SnapshotFilePath}", SnapshotFilePath);
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

                ServerSnapshotPersistence.SaveModbusSnapshot(SnapshotFilePath, DataStore);
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
                Logger?.LogError(ex, "删除Modbus RTU快照失败: {SnapshotFilePath}", SnapshotFilePath);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        public OperationResult ResetDataAndDeleteSnapshot()
        {
            try
            {
                lock (_snapshotSyncRoot)
                {
                    ResetDataStore();
                    ServerSnapshotPersistence.DeleteSnapshot(SnapshotFilePath);
                    _snapshotDirty = false;
                }

                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "重置Modbus RTU数据并删除快照失败: {SnapshotFilePath}", SnapshotFilePath);
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        public event EventHandler<DataStoreEventArgs> DataWritten
        {
            add { DataStore.DataStoreWrittenTo += value; }
            remove { DataStore.DataStoreWrittenTo -= value; }
        }

        public event EventHandler<DataStoreEventArgs> DataRead
        {
            add { DataStore.DataStoreReadFrom += value; }
            remove { DataStore.DataStoreReadFrom -= value; }
        }

        private void ResetDataStore()
        {
            lock (DataStore.SyncRoot)
            {
                if (DataStore.CoilDiscretes != null)
                {
                    for (var index = 0; index < DataStore.CoilDiscretes.Size; index++)
                    {
                        DataStore.CoilDiscretes[index] = false;
                    }
                }

                if (DataStore.InputDiscretes != null)
                {
                    for (var index = 0; index < DataStore.InputDiscretes.Size; index++)
                    {
                        DataStore.InputDiscretes[index] = false;
                    }
                }

                if (DataStore.HoldingRegisters != null)
                {
                    for (var index = 0; index < DataStore.HoldingRegisters.Size; index++)
                    {
                        DataStore.HoldingRegisters[index] = 0;
                    }
                }

                if (DataStore.InputRegisters != null)
                {
                    for (var index = 0; index < DataStore.InputRegisters.Size; index++)
                    {
                        DataStore.InputRegisters[index] = 0;
                    }
                }
            }
        }
        
        #region IReadWrite 接口实现
        
        private OperationResult<T> CreateNotSupportedResult<T>()
        {
            return new OperationResult<T>
            {
                IsSuccess = false,
                Message = "Modbus RTU服务器不支持此操作。服务器端不应直接调用读取方法。"
            };
        }
        
        private OperationResult CreateNotSupportedResult()
        {
            return new OperationResult
            {
                IsSuccess = false,
                Message = "Modbus RTU服务器不支持此操作。服务器端不应直接调用写入方法。"
            };
        }
        
        /// <summary>
        /// 分批读取
        /// </summary>
        public OperationResult<Dictionary<string, (DataTypeEnums, object)>> BatchRead(Dictionary<string, DataTypeEnums> addresses)
        {
            try
            {
                if (addresses == null || addresses.Count == 0)
                {
                    return OperationResult.CreateSuccessResult(new Dictionary<string, (DataTypeEnums, object)>());
                }

                var requestAddresses = new Dictionary<string, (DataTypeEnums, object)>();
                foreach (var kvp in addresses)
                {
                    requestAddresses[kvp.Key] = (kvp.Value, null);
                }

                var addressInfos = ModbusBatchHelper.ParseModbusAddresses(requestAddresses, false);
                if (addressInfos.Count == 0)
                {
                    return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>("没有有效的地址可以读取");
                }

                var optimizedBlocks = ModbusBatchHelper.OptimizeModbusAddressBlocks(addressInfos);
                if (optimizedBlocks.Count == 0)
                {
                    return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>("地址优化失败");
                }

                var blockDataDict = new Dictionary<string, byte[]>();
                var errors = new List<string>();

                foreach (var block in optimizedBlocks)
                {
                    var blockResult = ReadBlockBytes(block);
                    if (blockResult.IsSuccess)
                    {
                        blockDataDict[$"{block.StationNumber}_{block.FunctionCode}_{block.StartAddress}_{block.TotalLength}"] = blockResult.ResultValue;
                    }
                    else
                    {
                        errors.Add($"读取块 {block.StationNumber};{block.FunctionCode};{block.StartAddress} 失败: {blockResult.Message}");
                    }
                }

                var extractedData = ModbusBatchHelper.ExtractDataFromModbusBlocks(blockDataDict, optimizedBlocks, addressInfos);
                var finalResult = new Dictionary<string, (DataTypeEnums, object)>();

                foreach (var kvp in addresses)
                {
                    extractedData.TryGetValue(kvp.Key, out var value);
                    finalResult[kvp.Key] = (kvp.Value, value);
                }

                return new OperationResult<Dictionary<string, (DataTypeEnums, object)>>
                {
                    IsSuccess = errors.Count == 0,
                    Message = errors.Count == 0 ? string.Empty : string.Join("; ", errors),
                    ResultValue = finalResult
                }.Complete();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<Dictionary<string, (DataTypeEnums, object)>>(ex.Message);
            }
        }
        
        /// <summary>
        /// 读取Byte
        /// </summary>
        public OperationResult<byte> ReadByte(string address)
        {
            return CreateNotSupportedResult<byte>();
        }
        
        /// <summary>
        /// 读取Byte数组
        /// </summary>
        public OperationResult<byte[]> ReadByte(string address, int length)
        {
            return CreateNotSupportedResult<byte[]>();
        }
        
        /// <summary>
        /// 读取Boolean
        /// </summary>
        public OperationResult<bool> ReadBoolean(string address)
        {
            return CreateNotSupportedResult<bool>();
        }
        
        /// <summary>
        /// 读取Boolean数组
        /// </summary>
        public OperationResult<bool[]> ReadBoolean(string address, int length)
        {
            return CreateNotSupportedResult<bool[]>();
        }
        
        /// <summary>
        /// 读取UInt16
        /// </summary>
        public OperationResult<ushort> ReadUInt16(string address)
        {
            return CreateNotSupportedResult<ushort>();
        }
        
        /// <summary>
        /// 读取UInt16数组
        /// </summary>
        public OperationResult<ushort[]> ReadUInt16(string address, int length)
        {
            return CreateNotSupportedResult<ushort[]>();
        }
        
        /// <summary>
        /// 读取Int16
        /// </summary>
        public OperationResult<short> ReadInt16(string address)
        {
            return CreateNotSupportedResult<short>();
        }
        
        /// <summary>
        /// 读取Int16数组
        /// </summary>
        public OperationResult<short[]> ReadInt16(string address, int length)
        {
            return CreateNotSupportedResult<short[]>();
        }
        
        /// <summary>
        /// 读取UInt32
        /// </summary>
        public OperationResult<uint> ReadUInt32(string address)
        {
            return CreateNotSupportedResult<uint>();
        }
        
        /// <summary>
        /// 读取UInt32数组
        /// </summary>
        public OperationResult<uint[]> ReadUInt32(string address, int length)
        {
            return CreateNotSupportedResult<uint[]>();
        }
        
        /// <summary>
        /// 读取Int32
        /// </summary>
        public OperationResult<int> ReadInt32(string address)
        {
            return CreateNotSupportedResult<int>();
        }
        
        /// <summary>
        /// 读取Int32数组
        /// </summary>
        public OperationResult<int[]> ReadInt32(string address, int length)
        {
            return CreateNotSupportedResult<int[]>();
        }
        
        /// <summary>
        /// 读取UInt64
        /// </summary>
        public OperationResult<ulong> ReadUInt64(string address)
        {
            return CreateNotSupportedResult<ulong>();
        }
        
        /// <summary>
        /// 读取UInt64数组
        /// </summary>
        public OperationResult<ulong[]> ReadUInt64(string address, int length)
        {
            return CreateNotSupportedResult<ulong[]>();
        }
        
        /// <summary>
        /// 读取Int64
        /// </summary>
        public OperationResult<long> ReadInt64(string address)
        {
            return CreateNotSupportedResult<long>();
        }
        
        /// <summary>
        /// 读取Int64数组
        /// </summary>
        public OperationResult<long[]> ReadInt64(string address, int length)
        {
            return CreateNotSupportedResult<long[]>();
        }
        
        /// <summary>
        /// 读取Float
        /// </summary>
        public OperationResult<float> ReadFloat(string address)
        {
            return CreateNotSupportedResult<float>();
        }
        
        /// <summary>
        /// 读取Float数组
        /// </summary>
        public OperationResult<float[]> ReadFloat(string address, int length)
        {
            return CreateNotSupportedResult<float[]>();
        }
        
        /// <summary>
        /// 读取Double
        /// </summary>
        public OperationResult<double> ReadDouble(string address)
        {
            return CreateNotSupportedResult<double>();
        }
        
        /// <summary>
        /// 读取Double数组
        /// </summary>
        public OperationResult<double[]> ReadDouble(string address, int length)
        {
            return CreateNotSupportedResult<double[]>();
        }
        
        /// <summary>
        /// 读取String
        /// </summary>
        public OperationResult<string> ReadString(string address, int length)
        {
            return CreateNotSupportedResult<string>();
        }
        
        /// <summary>
        /// 根据类型读取数据
        /// </summary>
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address)
        {
            try
            {
                var addressInfo = ModbusBatchHelper.ParseSingleModbusAddress(address, dataTypeEnum);
                var rawResult = ReadRawBytes(addressInfo);
                if (!rawResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<object>(rawResult.Message);
                }

                var value = ModbusBatchHelper.ExtractValueFromModbusBytes(rawResult.ResultValue, 0, addressInfo, IsReverse, DataFormat);
                return new OperationResult<object>
                {
                    ResultValue = value
                }.Complete();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<object>(ex.Message);
            }
        }
        
        /// <summary>
        /// 根据类型读取数据
        /// </summary>
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address, int length)
        {
            if (length <= 1)
            {
                return Read(dataTypeEnum, address);
            }

            return OperationResult.CreateFailedResult<object>("Modbus服务端暂不支持数组长度读取");
        }
        
        /// <summary>
        /// 异步分批读取
        /// </summary>
        public ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            return new ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>>(BatchRead(addresses));
        }
        
        /// <summary>
        /// 异步读取Byte
        /// </summary>
        public ValueTask<OperationResult<byte>> ReadByteAsync(string address)
        {
            return new ValueTask<OperationResult<byte>>(CreateNotSupportedResult<byte>());
        }
        
        /// <summary>
        /// 异步读取Byte数组
        /// </summary>
        public ValueTask<OperationResult<byte[]>> ReadByteAsync(string address, int length)
        {
            return new ValueTask<OperationResult<byte[]>>(CreateNotSupportedResult<byte[]>());
        }
        
        /// <summary>
        /// 异步读取Boolean
        /// </summary>
        public ValueTask<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            return new ValueTask<OperationResult<bool>>(CreateNotSupportedResult<bool>());
        }
        
        /// <summary>
        /// 异步读取Boolean数组
        /// </summary>
        public ValueTask<OperationResult<bool[]>> ReadBooleanAsync(string address, int length)
        {
            return new ValueTask<OperationResult<bool[]>>(CreateNotSupportedResult<bool[]>());
        }
        
        /// <summary>
        /// 异步读取UInt16
        /// </summary>
        public ValueTask<OperationResult<ushort>> ReadUInt16Async(string address)
        {
            return new ValueTask<OperationResult<ushort>>(CreateNotSupportedResult<ushort>());
        }
        
        /// <summary>
        /// 异步读取UInt16数组
        /// </summary>
        public ValueTask<OperationResult<ushort[]>> ReadUInt16Async(string address, int length)
        {
            return new ValueTask<OperationResult<ushort[]>>(CreateNotSupportedResult<ushort[]>());
        }
        
        /// <summary>
        /// 异步读取Int16
        /// </summary>
        public ValueTask<OperationResult<short>> ReadInt16Async(string address)
        {
            return new ValueTask<OperationResult<short>>(CreateNotSupportedResult<short>());
        }
        
        /// <summary>
        /// 异步读取Int16数组
        /// </summary>
        public ValueTask<OperationResult<short[]>> ReadInt16Async(string address, int length)
        {
            return new ValueTask<OperationResult<short[]>>(CreateNotSupportedResult<short[]>());
        }
        
        /// <summary>
        /// 异步读取UInt32
        /// </summary>
        public ValueTask<OperationResult<uint>> ReadUInt32Async(string address)
        {
            return new ValueTask<OperationResult<uint>>(CreateNotSupportedResult<uint>());
        }
        
        /// <summary>
        /// 异步读取UInt32数组
        /// </summary>
        public ValueTask<OperationResult<uint[]>> ReadUInt32Async(string address, int length)
        {
            return new ValueTask<OperationResult<uint[]>>(CreateNotSupportedResult<uint[]>());
        }
        
        /// <summary>
        /// 异步读取Int32
        /// </summary>
        public ValueTask<OperationResult<int>> ReadInt32Async(string address)
        {
            return new ValueTask<OperationResult<int>>(CreateNotSupportedResult<int>());
        }
        
        /// <summary>
        /// 异步读取Int32数组
        /// </summary>
        public ValueTask<OperationResult<int[]>> ReadInt32Async(string address, int length)
        {
            return new ValueTask<OperationResult<int[]>>(CreateNotSupportedResult<int[]>());
        }
        
        /// <summary>
        /// 异步读取UInt64
        /// </summary>
        public ValueTask<OperationResult<ulong>> ReadUInt64Async(string address)
        {
            return new ValueTask<OperationResult<ulong>>(CreateNotSupportedResult<ulong>());
        }
        
        /// <summary>
        /// 异步读取UInt64数组
        /// </summary>
        public ValueTask<OperationResult<ulong[]>> ReadUInt64Async(string address, int length)
        {
            return new ValueTask<OperationResult<ulong[]>>(CreateNotSupportedResult<ulong[]>());
        }
        
        /// <summary>
        /// 异步读取Int64
        /// </summary>
        public ValueTask<OperationResult<long>> ReadInt64Async(string address)
        {
            return new ValueTask<OperationResult<long>>(CreateNotSupportedResult<long>());
        }
        
        /// <summary>
        /// 异步读取Int64数组
        /// </summary>
        public ValueTask<OperationResult<long[]>> ReadInt64Async(string address, int length)
        {
            return new ValueTask<OperationResult<long[]>>(CreateNotSupportedResult<long[]>());
        }
        
        /// <summary>
        /// 异步读取Float
        /// </summary>
        public ValueTask<OperationResult<float>> ReadFloatAsync(string address)
        {
            return new ValueTask<OperationResult<float>>(CreateNotSupportedResult<float>());
        }
        
        /// <summary>
        /// 异步读取Float数组
        /// </summary>
        public ValueTask<OperationResult<float[]>> ReadFloatAsync(string address, int length)
        {
            return new ValueTask<OperationResult<float[]>>(CreateNotSupportedResult<float[]>());
        }
        
        /// <summary>
        /// 异步读取Double
        /// </summary>
        public ValueTask<OperationResult<double>> ReadDoubleAsync(string address)
        {
            return new ValueTask<OperationResult<double>>(CreateNotSupportedResult<double>());
        }
        
        /// <summary>
        /// 异步读取Double数组
        /// </summary>
        public ValueTask<OperationResult<double[]>> ReadDoubleAsync(string address, int length)
        {
            return new ValueTask<OperationResult<double[]>>(CreateNotSupportedResult<double[]>());
        }
        
        /// <summary>
        /// 异步读取String
        /// </summary>
        public ValueTask<OperationResult<string>> ReadStringAsync(string address, int length)
        {
            return new ValueTask<OperationResult<string>>(CreateNotSupportedResult<string>());
        }
        
        /// <summary>
        /// 根据类型异步读取数据
        /// </summary>
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address)
        {
            return new ValueTask<OperationResult<object>>(Read(dataTypeEnum, address));
        }
        
        /// <summary>
        /// 根据类型异步读取数据
        /// </summary>
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address, int length)
        {
            return new ValueTask<OperationResult<object>>(Read(dataTypeEnum, address, length));
        }
        
        #endregion

        /// <summary>
        /// 分批写入
        /// </summary>
        public OperationResult BatchWrite(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            try
            {
                if (addresses == null || addresses.Count == 0)
                {
                    return OperationResult.CreateSuccessResult();
                }

                var writeItems = BuildWriteItems(addresses, out var buildErrors);
                if (buildErrors.Count > 0)
                {
                    return OperationResult.CreateFailedResult(string.Join("; ", buildErrors));
                }

                if (writeItems.Count == 0)
                {
                    return OperationResult.CreateFailedResult("没有有效的地址可以写入");
                }

                foreach (var block in BuildWriteBlocks(writeItems))
                {
                    if (block.IsBit)
                    {
                        ApplyCoilBlock(block);
                    }
                    else
                    {
                        WriteHoldingRegisters(block.StartAddress, block.Buffer);
                    }
                }

                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, byte[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, bool value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, bool[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, byte value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, ushort value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, ushort[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, short value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, short[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, uint value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, uint[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, int value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, int[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, ulong value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, ulong[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, long value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, long[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, float value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, float[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, double value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, double[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(string address, string value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object value)
        {
            try
            {
                var addressInfo = ModbusBatchHelper.ParseSingleModbusAddress(address, dataTypeEnum, true);
                var normalizedValue = NormalizeSingleValue(dataTypeEnum, value);

                switch (addressInfo.FunctionCode)
                {
                    case 0x05:
                    case 0x0F:
                        if (!(normalizedValue is bool))
                        {
                            return OperationResult.CreateFailedResult("写入的值不是布尔类型");
                        }

                        var boolValue = (bool)normalizedValue;
                        DataStore.WriteCoilsDirect(addressInfo.Address, new[] { boolValue });
                        return new OperationResult().Complete();

                    case 0x06:
                    case 0x10:
                        var bytes = ModbusBatchHelper.ConvertValueToModbusBytes((dataTypeEnum, normalizedValue), addressInfo, IsReverse, DataFormat);
                        if (bytes == null || bytes.Length == 0)
                        {
                            return OperationResult.CreateFailedResult("写入值转换失败");
                        }

                        WriteHoldingRegisters(addressInfo.Address, bytes);
                        return new OperationResult().Complete();

                    default:
                        return OperationResult.CreateFailedResult($"不支持的Modbus写入功能码: {addressInfo.FunctionCode}");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }
        
        /// <summary>
        /// 写入数据
        /// </summary>
        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object[] value)
        {
            return CreateNotSupportedResult();
        }
        
        /// <summary>
        /// 异步分批写入
        /// </summary>
        public ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            return new ValueTask<OperationResult>(BatchWrite(addresses));
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, byte[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, bool value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, byte value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, ushort value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, ushort[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, short value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, short[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, uint value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, uint[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, int value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, int[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, ulong value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, ulong[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, long value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, long[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, float value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, float[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, double value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, double[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(string address, string value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object value)
        {
            return Task.FromResult(Write(dataTypeEnum, address, value));
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
        }

        private OperationResult<byte[]> ReadRawBytes(ModbusBatchHelper.ModbusAddressInfo addressInfo)
        {
            try
            {
                switch (addressInfo.FunctionCode)
                {
                    case 0x01:
                        return new OperationResult<byte[]>
                        {
                            ResultValue = new[] { DataStore.CoilDiscretes[addressInfo.Address] ? (byte)1 : (byte)0 }
                        }.Complete();
                    case 0x02:
                        return new OperationResult<byte[]>
                        {
                            ResultValue = new[] { DataStore.InputDiscretes[addressInfo.Address] ? (byte)1 : (byte)0 }
                        }.Complete();
                    case 0x03:
                        return BuildRegisterBytes(addressInfo.Address, addressInfo.Length, true);
                    case 0x04:
                        return BuildRegisterBytes(addressInfo.Address, addressInfo.Length, false);
                    default:
                        return OperationResult.CreateFailedResult<byte[]>($"不支持的Modbus读取功能码: {addressInfo.FunctionCode}");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<byte[]>(ex.Message);
            }
        }

        private OperationResult<byte[]> BuildRegisterBytes(ushort startAddress, int byteLength, bool useHoldingRegister)
        {
            try
            {
                var registerCount = Math.Max(1, (byteLength + 1) / 2);
                var bytes = new byte[registerCount * 2];

                for (var index = 0; index < registerCount; index++)
                {
                    var registerValue = useHoldingRegister
                        ? (ushort)DataStore.HoldingRegisters[startAddress + index]
                        : (ushort)DataStore.InputRegisters[startAddress + index];

                    bytes[index * 2] = (byte)(registerValue >> 8);
                    bytes[index * 2 + 1] = (byte)(registerValue & 0xFF);
                }

                return new OperationResult<byte[]>
                {
                    ResultValue = bytes
                }.Complete();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<byte[]>(ex.Message);
            }
        }

        private static object NormalizeSingleValue(DataTypeEnums dataTypeEnum, object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (dataTypeEnum == DataTypeEnums.String)
            {
                return value.ToString() ?? string.Empty;
            }

            return value.ToString().ConvertFromStringToObject(dataTypeEnum);
        }

        private void WriteHoldingRegisters(ushort startAddress, byte[] bytes)
        {
            var registerCount = Math.Max(1, (bytes.Length + 1) / 2);
            var values = new ushort[registerCount];
            for (var index = 0; index < registerCount; index++)
            {
                var high = bytes[index * 2];
                var low = index * 2 + 1 < bytes.Length ? bytes[index * 2 + 1] : (byte)0;
                values[index] = (ushort)((high << 8) | low);
            }

            DataStore.WriteHoldingRegistersDirect(startAddress, values);
        }

        private OperationResult<byte[]> ReadBlockBytes(ModbusBatchHelper.ModbusAddressBlock block)
        {
            switch (block.FunctionCode)
            {
                case 0x01:
                    return BuildDiscreteBytes(block.StartAddress, block.TotalLength, false);
                case 0x02:
                    return BuildDiscreteBytes(block.StartAddress, block.TotalLength, true);
                case 0x03:
                    return BuildRegisterBytes(block.StartAddress, block.TotalLength, true);
                case 0x04:
                    return BuildRegisterBytes(block.StartAddress, block.TotalLength, false);
                default:
                    return OperationResult.CreateFailedResult<byte[]>($"不支持的Modbus读取功能码: {block.FunctionCode}");
            }
        }

        private OperationResult<byte[]> BuildDiscreteBytes(ushort startAddress, ushort bitLength, bool useInputDiscrete)
        {
            try
            {
                var byteCount = Math.Max(1, (bitLength + 7) / 8);
                var bytes = new byte[byteCount];

                for (var index = 0; index < bitLength; index++)
                {
                    var bitValue = useInputDiscrete
                        ? DataStore.InputDiscretes[startAddress + index]
                        : DataStore.CoilDiscretes[startAddress + index];

                    if (bitValue)
                    {
                        bytes[index / 8] |= (byte)(1 << (index % 8));
                    }
                }

                return new OperationResult<byte[]>
                {
                    ResultValue = bytes
                }.Complete();
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<byte[]>(ex.Message);
            }
        }

        private List<ModbusWriteItem> BuildWriteItems(Dictionary<string, (DataTypeEnums, object)> addresses, out List<string> errors)
        {
            errors = new List<string>();
            var result = new List<ModbusWriteItem>();

            foreach (var kvp in addresses)
            {
                try
                {
                    var addressInfo = ModbusBatchHelper.ParseSingleModbusAddress(kvp.Key, kvp.Value.Item1, true);
                    var normalizedValue = NormalizeSingleValue(kvp.Value.Item1, kvp.Value.Item2);
                    var buffer = ModbusBatchHelper.ConvertValueToModbusBytes((kvp.Value.Item1, normalizedValue), addressInfo, IsReverse, DataFormat);
                    if (buffer == null || buffer.Length == 0)
                    {
                        errors.Add($"地址 {kvp.Key} 的值转换失败");
                        continue;
                    }

                    result.Add(new ModbusWriteItem
                    {
                        AddressInfo = addressInfo,
                        Buffer = buffer,
                        IsBit = addressInfo.FunctionCode == 0x05 || addressInfo.FunctionCode == 0x0F
                    });
                }
                catch (Exception ex)
                {
                    errors.Add($"地址 {kvp.Key} 处理失败: {ex.Message}");
                }
            }

            return result;
        }

        private List<ModbusWriteBlock> BuildWriteBlocks(List<ModbusWriteItem> items)
        {
            var blocks = new List<ModbusWriteBlock>();
            foreach (var group in items.GroupBy(item => new { item.AddressInfo.StationNumber, item.IsBit }))
            {
                var orderedItems = group.OrderBy(item => item.AddressInfo.Address).ToList();
                if (orderedItems.Count == 0)
                {
                    continue;
                }

                var currentItems = new List<ModbusWriteItem>();
                var startAddress = orderedItems[0].AddressInfo.Address;
                var endAddress = GetEndAddress(orderedItems[0]);

                foreach (var item in orderedItems)
                {
                    if (currentItems.Count == 0)
                    {
                        currentItems.Add(item);
                        startAddress = item.AddressInfo.Address;
                        endAddress = GetEndAddress(item);
                        continue;
                    }

                    if (item.AddressInfo.Address <= endAddress)
                    {
                        currentItems.Add(item);
                        endAddress = Math.Max(endAddress, GetEndAddress(item));
                    }
                    else
                    {
                        blocks.Add(CreateWriteBlock(group.Key.StationNumber, group.Key.IsBit, startAddress, endAddress, currentItems));
                        currentItems = new List<ModbusWriteItem> { item };
                        startAddress = item.AddressInfo.Address;
                        endAddress = GetEndAddress(item);
                    }
                }

                if (currentItems.Count > 0)
                {
                    blocks.Add(CreateWriteBlock(group.Key.StationNumber, group.Key.IsBit, startAddress, endAddress, currentItems));
                }
            }

            return blocks;
        }

        private ModbusWriteBlock CreateWriteBlock(byte stationNumber, bool isBit, ushort startAddress, int endAddress, List<ModbusWriteItem> items)
        {
            if (isBit)
            {
                var bitCount = Math.Max(1, endAddress - startAddress);
                var buffer = new byte[(bitCount + 7) / 8];

                foreach (var item in items)
                {
                    var bitOffset = item.AddressInfo.Address - startAddress;
                    var bitValue = item.Buffer[0] == 0xFF || item.Buffer[0] == 1;
                    if (bitValue)
                    {
                        buffer[bitOffset / 8] |= (byte)(1 << (bitOffset % 8));
                    }
                }

                return new ModbusWriteBlock
                {
                    StationNumber = stationNumber,
                    IsBit = true,
                    StartAddress = startAddress,
                    Length = bitCount,
                    Buffer = buffer
                };
            }

            var registerCount = Math.Max(1, endAddress - startAddress);
            var registerBuffer = new byte[registerCount * 2];

            foreach (var item in items)
            {
                var offset = (item.AddressInfo.Address - startAddress) * 2;
                Array.Copy(item.Buffer, 0, registerBuffer, offset, item.Buffer.Length);
            }

            return new ModbusWriteBlock
            {
                StationNumber = stationNumber,
                IsBit = false,
                StartAddress = startAddress,
                Length = registerCount,
                Buffer = registerBuffer
            };
        }

        private void ApplyCoilBlock(ModbusWriteBlock block)
        {
            var values = new bool[block.Length];
            for (var index = 0; index < block.Length; index++)
            {
                values[index] = (block.Buffer[index / 8] & (1 << (index % 8))) != 0;
            }

            DataStore.WriteCoilsDirect(block.StartAddress, values);
        }

        private static int GetEndAddress(ModbusWriteItem item)
        {
            if (item.IsBit)
            {
                return item.AddressInfo.Address + 1;
            }

            return item.AddressInfo.Address + Math.Max(1, (item.Buffer.Length + 1) / 2);
        }

        private sealed class ModbusWriteItem
        {
            public ModbusBatchHelper.ModbusAddressInfo AddressInfo { get; set; } = default;

            public byte[] Buffer { get; set; } = Array.Empty<byte>();

            public bool IsBit { get; set; }
        }

        private sealed class ModbusWriteBlock
        {
            public byte StationNumber { get; set; }

            public bool IsBit { get; set; }

            public ushort StartAddress { get; set; }

            public int Length { get; set; }

            public byte[] Buffer { get; set; } = Array.Empty<byte>();
        }
        
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

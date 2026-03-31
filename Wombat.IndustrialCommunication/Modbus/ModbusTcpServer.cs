using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus.Data;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Modbus TCP服务器
    /// </summary>
    public class ModbusTcpServer : ModbusTcpServerBase, IDeviceServer
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
        /// 构造函数
        /// </summary>
        public ModbusTcpServer()
            : this("0.0.0.0", 502)
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ipEndPoint">IP终结点</param>
        public ModbusTcpServer(IPEndPoint ipEndPoint)
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
        public ModbusTcpServer(string ip, int port)
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
        
        #region IReadWrite 接口实现
        
        private OperationResult<T> CreateNotSupportedResult<T>()
        {
            return new OperationResult<T>
            {
                IsSuccess = false,
                Message = "Modbus TCP服务器不支持此操作。服务器端不应直接调用读取方法。"
            };
        }
        
        private OperationResult CreateNotSupportedResult()
        {
            return new OperationResult
            {
                IsSuccess = false,
                Message = "Modbus TCP服务器不支持此操作。服务器端不应直接调用写入方法。"
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
        /// 异步根据类型读取数据
        /// </summary>
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address)
        {
            return new ValueTask<OperationResult<object>>(Read(dataTypeEnum, address));
        }
        
        /// <summary>
        /// 异步根据类型读取数据
        /// </summary>
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address, int length)
        {
            return new ValueTask<OperationResult<object>>(Read(dataTypeEnum, address, length));
        }
        
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
                        DataStore.CoilDiscretes[addressInfo.Address] = boolValue;
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
        
        #endregion

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
            for (var index = 0; index < registerCount; index++)
            {
                var high = bytes[index * 2];
                var low = index * 2 + 1 < bytes.Length ? bytes[index * 2 + 1] : (byte)0;
                DataStore.HoldingRegisters[startAddress + index] = (ushort)((high << 8) | low);
            }
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
            for (var index = 0; index < block.Length; index++)
            {
                var bitValue = (block.Buffer[index / 8] & (1 << (index % 8))) != 0;
                DataStore.CoilDiscretes[block.StartAddress + index] = bitValue;
            }
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
                // 确保服务器已关闭
                Shutdown();
                
                // 释放传输资源
                _serverTransport?.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
}

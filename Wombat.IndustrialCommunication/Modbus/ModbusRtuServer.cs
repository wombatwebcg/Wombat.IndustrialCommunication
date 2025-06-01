using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
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
        public OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnums> addresses)
        {
            return CreateNotSupportedResult<Dictionary<string, object>>();
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
            return CreateNotSupportedResult<object>();
        }
        
        /// <summary>
        /// 根据类型读取数据
        /// </summary>
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address, int length)
        {
            return CreateNotSupportedResult<object>();
        }
        
        /// <summary>
        /// 异步分批读取
        /// </summary>
        public ValueTask<OperationResult<Dictionary<string, object>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            return new ValueTask<OperationResult<Dictionary<string, object>>>(CreateNotSupportedResult<Dictionary<string, object>>());
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
            return new ValueTask<OperationResult<object>>(CreateNotSupportedResult<object>());
        }
        
        /// <summary>
        /// 根据类型异步读取数据
        /// </summary>
        public ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address, int length)
        {
            return new ValueTask<OperationResult<object>>(CreateNotSupportedResult<object>());
        }
        
        #endregion

        /// <summary>
        /// 分批写入
        /// </summary>
        public OperationResult BatchWrite(Dictionary<string, object> addresses)
        {
            return CreateNotSupportedResult();
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
            return CreateNotSupportedResult();
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
        public Task<OperationResult> BatchWriteAsync(Dictionary<string, object> addresses)
        {
            return Task.FromResult(CreateNotSupportedResult());
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
            return Task.FromResult(CreateNotSupportedResult());
        }
        
        /// <summary>
        /// 异步写入数据
        /// </summary>
        public Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object[] value)
        {
            return Task.FromResult(CreateNotSupportedResult());
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
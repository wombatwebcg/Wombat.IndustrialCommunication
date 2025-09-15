using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Models;


namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// FINS协议客户端
    /// </summary>
    public class FinsClient : FinsCommunication, IDeviceClient, IClient
    {
        private FinsEthernetTransport _transport;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ipAddress">IP地址</param>
        /// <param name="port">端口号，默认9600</param>
        /// <param name="timeout">超时时间</param>
        public FinsClient(string ipAddress, int port = 9600, TimeSpan? timeout = null) 
            : base(new FinsEthernetTransport(ipAddress, port, timeout))
        {
            _transport = (FinsEthernetTransport)Transport;
        }

        /// <summary>
        /// IP地址
        /// </summary>
        public string IpAddress => _transport.IpAddress;

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port => _transport.Port;

        /// <summary>
        /// 超时时间
        /// </summary>
        public TimeSpan Timeout 
        { 
            get => _transport.Timeout; 
            set => _transport.Timeout = value; 
        }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _transport.IsConnected;

        /// <summary>
        /// 是否已连接（IClient接口实现）
        /// </summary>
        public bool Connected => IsConnected;

        /// <summary>
        /// 字节序格式
        /// </summary>
        public new Extensions.DataTypeExtensions.EndianFormat EndianFormat => Extensions.DataTypeExtensions.EndianFormat.ABCD;

        /// <summary>
        /// 日志记录器
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// 是否长连接
        /// </summary>
        public bool IsLongConnection { get; set; } = true;

        /// <summary>
        /// 重试次数
        /// </summary>
        public int Retries
        {
            get => _transport?.Retries ?? 2;
            set { if (_transport != null) _transport.Retries = value; }
        }

        /// <summary>
        /// 重试等待时间
        /// </summary>
        public TimeSpan WaitToRetryMilliseconds
        {
            get => _transport?.WaitToRetryMilliseconds ?? TimeSpan.FromMilliseconds(100);
            set { if (_transport != null) _transport.WaitToRetryMilliseconds = value; }
        }

        /// <summary>
        /// 连接超时时间
        /// </summary>
        public TimeSpan ConnectTimeout
        {
            get => _transport?.ConnectTimeout ?? TimeSpan.FromSeconds(3);
            set { if (_transport != null) _transport.ConnectTimeout = value; }
        }

        /// <summary>
        /// 接收超时时间
        /// </summary>
        public TimeSpan ReceiveTimeout
        {
            get => _transport?.ReceiveTimeout ?? TimeSpan.FromSeconds(3);
            set { if (_transport != null) _transport.ReceiveTimeout = value; }
        }

        /// <summary>
        /// 发送超时时间
        /// </summary>
        public TimeSpan SendTimeout
        {
            get => _transport?.SendTimeout ?? TimeSpan.FromSeconds(3);
            set { if (_transport != null) _transport.SendTimeout = value; }
        }

        /// <summary>
        /// 响应间隔时间
        /// </summary>
        public TimeSpan ResponseInterval
        {
            get => _transport?.ResponseInterval ?? TimeSpan.FromMilliseconds(50);
            set { if (_transport != null) _transport.ResponseInterval = value; }
        }

        /// <summary>
        /// 连接到PLC
        /// </summary>
        /// <returns>连接结果</returns>
        public async Task<OperationResult> ConnectAsync()
        {
            // 首先建立传输层连接
            var transportResult = await _transport.ConnectAsync();
            if (!transportResult.IsSuccess)
            {
                return transportResult;
            }

            // 然后进行FINS协议初始化
            var initResult = await InitAsync(Timeout);
            if (!initResult.IsSuccess)
            {
                await _transport.DisconnectAsync(); // 如果初始化失败，断开传输层连接
                return initResult;
            }

            return OperationResult.CreateSuccessResult("FINS客户端连接成功");
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns>断开连接结果</returns>
        public async Task<OperationResult> DisconnectAsync()
        {
            return await _transport.DisconnectAsync();
        }

        /// <summary>
        /// 同步连接到PLC
        /// </summary>
        /// <returns>连接结果</returns>
        public OperationResult Connect()
        {
            return Task.Run(async () => await ConnectAsync()).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 同步断开连接
        /// </summary>
        /// <returns>断开连接结果</returns>
        public OperationResult Disconnect()
        {
            return Task.Run(async () => await DisconnectAsync()).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length">长度</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<byte[]>> ReadAsync(string address, ushort length, DataTypeEnums dataType = DataTypeEnums.UInt16)
        {
            if (!IsConnected)
            {
                var connectResult = await ConnectAsync();
                if (!connectResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<byte[]>($"连接失败: {connectResult.Message}");
                }
            }

            return await base.ReadAsync(address, length, dataType);
        }

        /// <summary>
        /// 读取布尔值
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            return await base.ReadBooleanAsync(address);
        }

        /// <summary>
        /// 读取16位无符号整数
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<ushort>> ReadUInt16Async(string address)
        {
            return await base.ReadUInt16Async(address);
        }

        /// <summary>
        /// 读取16位有符号整数
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<short>> ReadInt16Async(string address)
        {
            return await base.ReadInt16Async(address);
        }

        /// <summary>
        /// 读取32位无符号整数
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<uint>> ReadUInt32Async(string address)
        {
            return await base.ReadUInt32Async(address);
        }

        /// <summary>
        /// 读取32位有符号整数
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<int>> ReadInt32Async(string address)
        {
            return await base.ReadInt32Async(address);
        }

        /// <summary>
        /// 读取单精度浮点数
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<float>> ReadFloatAsync(string address)
        {
            return await base.ReadFloatAsync(address);
        }

        /// <summary>
        /// 读取双精度浮点数
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<double>> ReadDoubleAsync(string address)
        {
            return await base.ReadDoubleAsync(address);
        }

        /// <summary>
        /// 读取字符串
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length">长度</param>
        /// <returns>读取结果</returns>
        public async Task<OperationResult<string>> ReadStringAsync(string address, ushort length)
        {
            return await base.ReadStringAsync(address, length);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteAsync(string address, object value, DataTypeEnums dataType = DataTypeEnums.UInt16)
        {
            if (!IsConnected)
            {
                var connectResult = await ConnectAsync();
                if (!connectResult.IsSuccess)
                {
                    return OperationResult.CreateFailedResult($"连接失败: {connectResult.Message}");
                }
            }

            // 将object转换为byte[]数组
            byte[] data;
            try
            {
                data = ConvertValueToBytes(value, dataType);
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult($"数据转换失败: {ex.Message}");
            }

            return await base.WriteAsync(address, data, dataType);
        }

        /// <summary>
        /// 将值转换为字节数组
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="dataType">数据类型</param>
        /// <returns>字节数组</returns>
        private byte[] ConvertValueToBytes(object value, DataTypeEnums dataType)
        {
            switch (dataType)
            {
                case DataTypeEnums.Bool:
                    return new byte[] { (bool)value ? (byte)1 : (byte)0 };
                case DataTypeEnums.Byte:
                    return new byte[] { (byte)value };
                case DataTypeEnums.UInt16:
                    return BitConverter.GetBytes((ushort)value);
                case DataTypeEnums.Int16:
                    return BitConverter.GetBytes((short)value);
                case DataTypeEnums.UInt32:
                    return BitConverter.GetBytes((uint)value);
                case DataTypeEnums.Int32:
                    return BitConverter.GetBytes((int)value);
                case DataTypeEnums.Float:
                    return BitConverter.GetBytes((float)value);
                case DataTypeEnums.Double:
                    return BitConverter.GetBytes((double)value);
                case DataTypeEnums.String:
                    return System.Text.Encoding.UTF8.GetBytes(value.ToString());
                default:
                    throw new ArgumentException($"不支持的数据类型: {dataType}");
            }
        }

        /// <summary>
        /// 写入布尔值
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteBooleanAsync(string address, bool value)
        {
            return await base.WriteBooleanAsync(address, value);
        }

        /// <summary>
        /// 写入16位无符号整数
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteUInt16Async(string address, ushort value)
        {
            return await base.WriteUInt16Async(address, value);
        }

        /// <summary>
        /// 写入16位有符号整数
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteInt16Async(string address, short value)
        {
            return await base.WriteInt16Async(address, value);
        }

        /// <summary>
        /// 写入32位无符号整数
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteUInt32Async(string address, uint value)
        {
            return await base.WriteUInt32Async(address, value);
        }

        /// <summary>
        /// 写入32位有符号整数
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteInt32Async(string address, int value)
        {
            return await base.WriteInt32Async(address, value);
        }

        /// <summary>
        /// 写入单精度浮点数
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteFloatAsync(string address, float value)
        {
            return await base.WriteFloatAsync(address, value);
        }

        /// <summary>
        /// 写入双精度浮点数
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteDoubleAsync(string address, double value)
        {
            return await base.WriteDoubleAsync(address, value);
        }

        /// <summary>
        /// 写入字符串
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="length">长度</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> WriteStringAsync(string address, string value, int length = -1)
        {
            return await base.WriteStringAsync(address, value);
        }

        /// <summary>
        /// 批量读取
        /// </summary>
        /// <param name="addresses">地址列表</param>
        /// <returns>读取结果</returns>
        public override async ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            return await base.BatchReadAsync(addresses);
        }

        /// <summary>
        /// 批量写入
        /// </summary>
        /// <param name="addresses">地址数据列表</param>
        /// <returns>写入结果</returns>
        public async Task<OperationResult> BatchWriteAsync(Dictionary<string, object> addresses)
        {
            // 转换参数格式
            var convertedAddresses = new Dictionary<string, (DataTypeEnums, object)>();
            foreach (var kvp in addresses)
            {
                // 根据值的类型推断DataTypeEnums
                var dataType = InferDataType(kvp.Value);
                convertedAddresses[kvp.Key] = (dataType, kvp.Value);
            }
            return await base.BatchWriteAsync(convertedAddresses);
        }

        /// <summary>
        /// 根据值推断数据类型
        /// </summary>
        /// <param name="value">值</param>
        /// <returns>数据类型</returns>
        private DataTypeEnums InferDataType(object value)
        {
            if (value == null) return DataTypeEnums.None;
            
            switch (value)
            {
                case bool _:
                    return DataTypeEnums.Bool;
                case byte _:
                    return DataTypeEnums.Byte;
                case ushort _:
                    return DataTypeEnums.UInt16;
                case short _:
                    return DataTypeEnums.Int16;
                case uint _:
                    return DataTypeEnums.UInt32;
                case int _:
                    return DataTypeEnums.Int32;
                case float _:
                    return DataTypeEnums.Float;
                case double _:
                    return DataTypeEnums.Double;
                case string _:
                    return DataTypeEnums.String;
                default:
                    return DataTypeEnums.None;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _transport?.Dispose();
        }
    }
}
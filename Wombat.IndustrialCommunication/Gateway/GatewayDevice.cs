using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Gateway
{
    /// <summary>
    /// 网关设备实现类，封装IDeviceClient接口
    /// </summary>
    public class GatewayDevice : IGatewayDevice
    {
        private readonly IDeviceClient _deviceClient;
        private readonly ConnectionPoolManager<IDeviceClient> _connectionPool;
        private bool _isUsingPool;
        private bool _isDisposed;

        /// <summary>
        /// 构造函数 - 使用单一设备客户端
        /// </summary>
        /// <param name="deviceClient">设备客户端实例</param>
        public GatewayDevice(IDeviceClient deviceClient)
        {
            _deviceClient = deviceClient ?? throw new ArgumentNullException(nameof(deviceClient));
            _isUsingPool = false;
        }

        /// <summary>
        /// 构造函数 - 使用连接池
        /// </summary>
        /// <param name="connectionPool">连接池管理器</param>
        public GatewayDevice(ConnectionPoolManager<IDeviceClient> connectionPool)
        {
            _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
            _isUsingPool = true;
        }

        /// <summary>
        /// 设备是否已连接
        /// </summary>
        public bool Connected
        {
            get
            {
                if (_isUsingPool)
                {
                    // 连接池模式下，无法直接确定连接状态
                    return true;
                }
                return _deviceClient.Connected;
            }
        }

        /// <summary>
        /// 连接设备
        /// </summary>
        public OperationResult Connect()
        {
            if (_isUsingPool)
            {
                // 连接池模式下，无需显式连接
                return OperationResult.CreateSuccessResult();
            }
            return _deviceClient.Connect();
        }

        /// <summary>
        /// 异步连接设备
        /// </summary>
        public async Task<OperationResult> ConnectAsync()
        {
            if (_isUsingPool)
            {
                // 连接池模式下，无需显式连接
                return OperationResult.CreateSuccessResult();
            }
            return await _deviceClient.ConnectAsync();
        }

        /// <summary>
        /// 断开设备连接
        /// </summary>
        public OperationResult Disconnect()
        {
            if (_isUsingPool)
            {
                // 连接池模式下，无需显式断开连接
                return OperationResult.CreateSuccessResult();
            }
            return _deviceClient.Disconnect();
        }

        /// <summary>
        /// 异步断开设备连接
        /// </summary>
        public async Task<OperationResult> DisconnectAsync()
        {
            if (_isUsingPool)
            {
                // 连接池模式下，无需显式断开连接
                return OperationResult.CreateSuccessResult();
            }
            return await _deviceClient.DisconnectAsync();
        }

        /// <summary>
        /// 批量读取不同地址的数据
        /// </summary>
        public OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnums> addresses)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.BatchRead(addresses));
            }
            return _deviceClient.BatchRead(addresses);
        }

        /// <summary>
        /// 异步批量读取不同地址的数据
        /// </summary>
        public async Task<OperationResult<Dictionary<string, object>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.BatchReadAsync(addresses));
            }
            return await _deviceClient.BatchReadAsync(addresses);
        }

        /// <summary>
        /// 读取单个布尔值
        /// </summary>
        public OperationResult<bool> ReadBoolean(string address)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.ReadBoolean(address));
            }
            return _deviceClient.ReadBoolean(address);
        }

        /// <summary>
        /// 异步读取单个布尔值
        /// </summary>
        public async Task<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.ReadBooleanAsync(address));
            }
            return await _deviceClient.ReadBooleanAsync(address);
        }

        /// <summary>
        /// 读取单个整数值(16位)
        /// </summary>
        public OperationResult<short> ReadInt16(string address)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.ReadInt16(address));
            }
            return _deviceClient.ReadInt16(address);
        }

        /// <summary>
        /// 异步读取单个整数值(16位)
        /// </summary>
        public async Task<OperationResult<short>> ReadInt16Async(string address)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.ReadInt16Async(address));
            }
            return await _deviceClient.ReadInt16Async(address);
        }

        /// <summary>
        /// 读取单个整数值(32位)
        /// </summary>
        public OperationResult<int> ReadInt32(string address)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.ReadInt32(address));
            }
            return _deviceClient.ReadInt32(address);
        }

        /// <summary>
        /// 异步读取单个整数值(32位)
        /// </summary>
        public async Task<OperationResult<int>> ReadInt32Async(string address)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.ReadInt32Async(address));
            }
            return await _deviceClient.ReadInt32Async(address);
        }

        /// <summary>
        /// 读取单个浮点值
        /// </summary>
        public OperationResult<float> ReadFloat(string address)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.ReadFloat(address));
            }
            return _deviceClient.ReadFloat(address);
        }

        /// <summary>
        /// 异步读取单个浮点值
        /// </summary>
        public async Task<OperationResult<float>> ReadFloatAsync(string address)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.ReadFloatAsync(address));
            }
            return await _deviceClient.ReadFloatAsync(address);
        }

        /// <summary>
        /// 读取单个双精度浮点值
        /// </summary>
        public OperationResult<double> ReadDouble(string address)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.ReadDouble(address));
            }
            return _deviceClient.ReadDouble(address);
        }

        /// <summary>
        /// 异步读取单个双精度浮点值
        /// </summary>
        public async Task<OperationResult<double>> ReadDoubleAsync(string address)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.ReadDoubleAsync(address));
            }
            return await _deviceClient.ReadDoubleAsync(address);
        }

        /// <summary>
        /// 读取字符串
        /// </summary>
        public OperationResult<string> ReadString(string address, int length)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.ReadString(address, length));
            }
            return _deviceClient.ReadString(address, length);
        }

        /// <summary>
        /// 异步读取字符串
        /// </summary>
        public async Task<OperationResult<string>> ReadStringAsync(string address, int length)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.ReadStringAsync(address, length));
            }
            return await _deviceClient.ReadStringAsync(address, length);
        }

        /// <summary>
        /// 批量写入数据
        /// </summary>
        public OperationResult BatchWrite(Dictionary<string, object> addresses)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.BatchWrite(addresses));
            }
            return _deviceClient.BatchWrite(addresses);
        }

        /// <summary>
        /// 异步批量写入数据
        /// </summary>
        public async Task<OperationResult> BatchWriteAsync(Dictionary<string, object> addresses)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.BatchWriteAsync(addresses));
            }
            return await _deviceClient.BatchWriteAsync(addresses);
        }

        /// <summary>
        /// 写入布尔值
        /// </summary>
        public OperationResult Write(string address, bool value)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.Write(address, value));
            }
            return _deviceClient.Write(address, value);
        }

        /// <summary>
        /// 异步写入布尔值
        /// </summary>
        public async Task<OperationResult> WriteAsync(string address, bool value)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.WriteAsync(address, value));
            }
            return await _deviceClient.WriteAsync(address, value);
        }

        /// <summary>
        /// 写入整数值(16位)
        /// </summary>
        public OperationResult Write(string address, short value)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.Write(address, value));
            }
            return _deviceClient.Write(address, value);
        }

        /// <summary>
        /// 异步写入整数值(16位)
        /// </summary>
        public async Task<OperationResult> WriteAsync(string address, short value)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.WriteAsync(address, value));
            }
            return await _deviceClient.WriteAsync(address, value);
        }

        /// <summary>
        /// 写入整数值(32位)
        /// </summary>
        public OperationResult Write(string address, int value)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.Write(address, value));
            }
            return _deviceClient.Write(address, value);
        }

        /// <summary>
        /// 异步写入整数值(32位)
        /// </summary>
        public async Task<OperationResult> WriteAsync(string address, int value)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.WriteAsync(address, value));
            }
            return await _deviceClient.WriteAsync(address, value);
        }

        /// <summary>
        /// 写入浮点值
        /// </summary>
        public OperationResult Write(string address, float value)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.Write(address, value));
            }
            return _deviceClient.Write(address, value);
        }

        /// <summary>
        /// 异步写入浮点值
        /// </summary>
        public async Task<OperationResult> WriteAsync(string address, float value)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.WriteAsync(address, value));
            }
            return await _deviceClient.WriteAsync(address, value);
        }

        /// <summary>
        /// 写入双精度浮点值
        /// </summary>
        public OperationResult Write(string address, double value)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.Write(address, value));
            }
            return _deviceClient.Write(address, value);
        }

        /// <summary>
        /// 异步写入双精度浮点值
        /// </summary>
        public async Task<OperationResult> WriteAsync(string address, double value)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.WriteAsync(address, value));
            }
            return await _deviceClient.WriteAsync(address, value);
        }

        /// <summary>
        /// 写入字符串
        /// </summary>
        public OperationResult Write(string address, string value)
        {
            if (_isUsingPool)
            {
                return ExecuteWithPooledClient(client => client.Write(address, value));
            }
            return _deviceClient.Write(address, value);
        }

        /// <summary>
        /// 异步写入字符串
        /// </summary>
        public async Task<OperationResult> WriteAsync(string address, string value)
        {
            if (_isUsingPool)
            {
                return await ExecuteWithPooledClientAsync(async client => await client.WriteAsync(address, value));
            }
            return await _deviceClient.WriteAsync(address, value);
        }

        /// <summary>
        /// 使用连接池中的客户端执行操作
        /// </summary>
        private T ExecuteWithPooledClient<T>(Func<IDeviceClient, T> action)
        {
            if (!_isUsingPool)
            {
                throw new InvalidOperationException("设备未使用连接池模式");
            }

            var connectionId = Guid.NewGuid().ToString();
            IDeviceClient client = null;
            
            try
            {
                client = _connectionPool.GetConnectionAsync(connectionId).GetAwaiter().GetResult();
                return action(client);
            }
            finally
            {
                if (client != null)
                {
                    _connectionPool.ReleaseConnection(client, connectionId);
                }
            }
        }

        /// <summary>
        /// 异步使用连接池中的客户端执行操作
        /// </summary>
        private async Task<T> ExecuteWithPooledClientAsync<T>(Func<IDeviceClient, Task<T>> action)
        {
            if (!_isUsingPool)
            {
                throw new InvalidOperationException("设备未使用连接池模式");
            }

            var connectionId = Guid.NewGuid().ToString();
            IDeviceClient client = null;
            
            try
            {
                client = await _connectionPool.GetConnectionAsync(connectionId);
                return await action(client);
            }
            finally
            {
                if (client != null)
                {
                    _connectionPool.ReleaseConnection(client, connectionId);
                }
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (!_isUsingPool)
            {
                _deviceClient?.Disconnect();
                if (_deviceClient is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            else
            {
                _connectionPool?.DisposeAsync().GetAwaiter().GetResult();
            }

            _isDisposed = true;
        }
    }
} 
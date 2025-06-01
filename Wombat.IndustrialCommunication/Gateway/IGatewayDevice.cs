using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Gateway
{
    /// <summary>
    /// 网关设备接口，提供统一的设备操作API
    /// </summary>
    public interface IGatewayDevice : IDisposable
    {
        /// <summary>
        /// 设备是否已连接
        /// </summary>
        bool Connected { get; }
        
        /// <summary>
        /// 连接设备
        /// </summary>
        /// <returns>操作结果</returns>
        OperationResult Connect();
        
        /// <summary>
        /// 异步连接设备
        /// </summary>
        /// <returns>操作结果</returns>
        Task<OperationResult> ConnectAsync();
        
        /// <summary>
        /// 断开设备连接
        /// </summary>
        /// <returns>操作结果</returns>
        OperationResult Disconnect();
        
        /// <summary>
        /// 异步断开设备连接
        /// </summary>
        /// <returns>操作结果</returns>
        Task<OperationResult> DisconnectAsync();
        
        /// <summary>
        /// 批量读取不同地址的数据
        /// </summary>
        /// <param name="addresses">地址和数据类型的字典</param>
        /// <returns>包含地址和值的字典</returns>
        OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnums> addresses);
        
        /// <summary>
        /// 异步批量读取不同地址的数据
        /// </summary>
        /// <param name="addresses">地址和数据类型的字典</param>
        /// <returns>包含地址和值的字典</returns>
        Task<OperationResult<Dictionary<string, object>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses);
        
        /// <summary>
        /// 读取单个布尔值
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>布尔值结果</returns>
        OperationResult<bool> ReadBoolean(string address);
        
        /// <summary>
        /// 异步读取单个布尔值
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>布尔值结果</returns>
        Task<OperationResult<bool>> ReadBooleanAsync(string address);
        
        /// <summary>
        /// 读取单个整数值(16位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>整数结果</returns>
        OperationResult<short> ReadInt16(string address);
        
        /// <summary>
        /// 异步读取单个整数值(16位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>整数结果</returns>
        Task<OperationResult<short>> ReadInt16Async(string address);
        
        /// <summary>
        /// 读取单个整数值(32位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>整数结果</returns>
        OperationResult<int> ReadInt32(string address);
        
        /// <summary>
        /// 异步读取单个整数值(32位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>整数结果</returns>
        Task<OperationResult<int>> ReadInt32Async(string address);
        
        /// <summary>
        /// 读取单个浮点值
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>浮点数结果</returns>
        OperationResult<float> ReadFloat(string address);
        
        /// <summary>
        /// 异步读取单个浮点值
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>浮点数结果</returns>
        Task<OperationResult<float>> ReadFloatAsync(string address);
        
        /// <summary>
        /// 读取单个双精度浮点值
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>双精度浮点数结果</returns>
        OperationResult<double> ReadDouble(string address);
        
        /// <summary>
        /// 异步读取单个双精度浮点值
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns>双精度浮点数结果</returns>
        Task<OperationResult<double>> ReadDoubleAsync(string address);
        
        /// <summary>
        /// 读取字符串
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length">长度</param>
        /// <returns>字符串结果</returns>
        OperationResult<string> ReadString(string address, int length);
        
        /// <summary>
        /// 异步读取字符串
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length">长度</param>
        /// <returns>字符串结果</returns>
        Task<OperationResult<string>> ReadStringAsync(string address, int length);
        
        /// <summary>
        /// 批量写入数据
        /// </summary>
        /// <param name="addresses">地址和值的字典</param>
        /// <returns>操作结果</returns>
        OperationResult BatchWrite(Dictionary<string, object> addresses);
        
        /// <summary>
        /// 异步批量写入数据
        /// </summary>
        /// <param name="addresses">地址和值的字典</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> BatchWriteAsync(Dictionary<string, object> addresses);
        
        /// <summary>
        /// 写入布尔值
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        OperationResult Write(string address, bool value);
        
        /// <summary>
        /// 异步写入布尔值
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> WriteAsync(string address, bool value);
        
        /// <summary>
        /// 写入整数值(16位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        OperationResult Write(string address, short value);
        
        /// <summary>
        /// 异步写入整数值(16位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> WriteAsync(string address, short value);
        
        /// <summary>
        /// 写入整数值(32位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        OperationResult Write(string address, int value);
        
        /// <summary>
        /// 异步写入整数值(32位)
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> WriteAsync(string address, int value);
        
        /// <summary>
        /// 写入浮点值
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        OperationResult Write(string address, float value);
        
        /// <summary>
        /// 异步写入浮点值
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> WriteAsync(string address, float value);
        
        /// <summary>
        /// 写入双精度浮点值
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        OperationResult Write(string address, double value);
        
        /// <summary>
        /// 异步写入双精度浮点值
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> WriteAsync(string address, double value);
        
        /// <summary>
        /// 写入字符串
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        OperationResult Write(string address, string value);
        
        /// <summary>
        /// 异步写入字符串
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> WriteAsync(string address, string value);
    }
} 
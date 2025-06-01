using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// IDeviceClient接口的扩展方法类，提供通用的操作执行方法
    /// </summary>
    public static class IDeviceClientExtensions
    {
        /// <summary>
        /// 执行一个读取操作，包含异常处理和日志记录
        /// </summary>
        /// <typeparam name="T">读取结果类型</typeparam>
        /// <param name="client">设备客户端</param>
        /// <param name="readOperation">读取操作</param>
        /// <param name="operationName">操作名称</param>
        /// <param name="address">地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeout">超时时间(毫秒)，-1表示不设置超时</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult<T>> ExecuteReadAsync<T>(
            this IDeviceClient client,
            Func<CancellationToken, Task<OperationResult<T>>> readOperation,
            string operationName,
            string address = null,
            CancellationToken cancellationToken = default,
            int timeout = -1)
        {
            string contextInfo = BuildContextInfo(client, address);
            
            // 检查连接状态
            if (client.IsLongConnection && !client.Connected)
            {
                // 尝试重新连接
                if (client is IAutoReconnectClient autoReconnectClient && autoReconnectClient.EnableAutoReconnect)
                {
                    var reconnectResult = await autoReconnectClient.CheckAndReconnectAsync();
                    if (!reconnectResult.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult<T>($"客户端自动重连失败，无法执行读取操作 [{contextInfo}]");
                    }
                }
                else
                {
                    return OperationResult.CreateFailedResult<T>($"客户端没有连接 [{contextInfo}]");
                }
            }
            
            // 执行读取操作
            return await ClientOperationExecutor.ExecuteAsync(
                readOperation,
                operationName,
                client.Logger,
                contextInfo,
                cancellationToken,
                timeout);
        }
        
        /// <summary>
        /// 执行一个读取操作，包含异常处理和日志记录
        /// </summary>
        /// <typeparam name="T">读取结果类型</typeparam>
        /// <param name="client">设备客户端</param>
        /// <param name="readOperation">读取操作</param>
        /// <param name="operationName">操作名称</param>
        /// <param name="address">地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static Task<OperationResult<T>> ExecuteReadAsync<T>(
            this IDeviceClient client,
            Func<Task<OperationResult<T>>> readOperation,
            string operationName,
            string address = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteReadAsync(
                client,
                (ct) => readOperation(),
                operationName,
                address,
                cancellationToken);
        }
        
        /// <summary>
        /// 执行一个写入操作，包含异常处理和日志记录
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="writeOperation">写入操作</param>
        /// <param name="operationName">操作名称</param>
        /// <param name="address">地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeout">超时时间(毫秒)，-1表示不设置超时</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult> ExecuteWriteAsync(
            this IDeviceClient client,
            Func<CancellationToken, Task<OperationResult>> writeOperation,
            string operationName,
            string address = null,
            CancellationToken cancellationToken = default,
            int timeout = -1)
        {
            string contextInfo = BuildContextInfo(client, address);
            
            // 检查连接状态
            if (client.IsLongConnection && !client.Connected)
            {
                // 尝试重新连接
                if (client is IAutoReconnectClient autoReconnectClient && autoReconnectClient.EnableAutoReconnect)
                {
                    var reconnectResult = await autoReconnectClient.CheckAndReconnectAsync();
                    if (!reconnectResult.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult($"客户端自动重连失败，无法执行写入操作 [{contextInfo}]");
                    }
                }
                else
                {
                    return OperationResult.CreateFailedResult($"客户端没有连接 [{contextInfo}]");
                }
            }
            
            // 执行写入操作
            return await ClientOperationExecutor.ExecuteAsync(
                writeOperation,
                operationName,
                client.Logger,
                contextInfo,
                cancellationToken,
                timeout);
        }
        
        /// <summary>
        /// 执行一个写入操作，包含异常处理和日志记录
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="writeOperation">写入操作</param>
        /// <param name="operationName">操作名称</param>
        /// <param name="address">地址</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static Task<OperationResult> ExecuteWriteAsync(
            this IDeviceClient client,
            Func<Task<OperationResult>> writeOperation,
            string operationName,
            string address = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteWriteAsync(
                client,
                (ct) => writeOperation(),
                operationName,
                address,
                cancellationToken);
        }
        
        /// <summary>
        /// 执行一个批量读取操作，包含异常处理和日志记录
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="batchReadOperation">批量读取操作</param>
        /// <param name="addresses">地址列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeout">超时时间(毫秒)，-1表示不设置超时</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult<Dictionary<string, object>>> ExecuteBatchReadAsync(
            this IDeviceClient client,
            Func<CancellationToken, Task<OperationResult<Dictionary<string, object>>>> batchReadOperation,
            Dictionary<string, string> addresses,
            CancellationToken cancellationToken = default,
            int timeout = -1)
        {
            string addressList = string.Join(",", addresses.Keys);
            string contextInfo = BuildContextInfo(client, addressList);
            
            // 检查连接状态
            if (client.IsLongConnection && !client.Connected)
            {
                // 尝试重新连接
                if (client is IAutoReconnectClient autoReconnectClient && autoReconnectClient.EnableAutoReconnect)
                {
                    var reconnectResult = await autoReconnectClient.CheckAndReconnectAsync();
                    if (!reconnectResult.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult<Dictionary<string, object>>($"客户端自动重连失败，无法执行批量读取操作 [{contextInfo}]");
                    }
                }
                else
                {
                    return OperationResult.CreateFailedResult<Dictionary<string, object>>($"客户端没有连接 [{contextInfo}]");
                }
            }
            
            // 执行批量读取操作
            return await ClientOperationExecutor.ExecuteAsync(
                batchReadOperation,
                "BatchRead",
                client.Logger,
                contextInfo,
                cancellationToken,
                timeout);
        }
        
        /// <summary>
        /// 执行一个批量读取操作，包含异常处理和日志记录
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="batchReadOperation">批量读取操作</param>
        /// <param name="addresses">地址列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static Task<OperationResult<Dictionary<string, object>>> ExecuteBatchReadAsync(
            this IDeviceClient client,
            Func<Task<OperationResult<Dictionary<string, object>>>> batchReadOperation,
            Dictionary<string, string> addresses,
            CancellationToken cancellationToken = default)
        {
            return ExecuteBatchReadAsync(
                client,
                (ct) => batchReadOperation(),
                addresses,
                cancellationToken);
        }
        
        /// <summary>
        /// 执行一个批量写入操作，包含异常处理和日志记录
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="batchWriteOperation">批量写入操作</param>
        /// <param name="addresses">地址和值的字典</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeout">超时时间(毫秒)，-1表示不设置超时</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult> ExecuteBatchWriteAsync(
            this IDeviceClient client,
            Func<CancellationToken, Task<OperationResult>> batchWriteOperation,
            Dictionary<string, object> addresses,
            CancellationToken cancellationToken = default,
            int timeout = -1)
        {
            string addressList = string.Join(",", addresses.Keys);
            string contextInfo = BuildContextInfo(client, addressList);
            
            // 检查连接状态
            if (client.IsLongConnection && !client.Connected)
            {
                // 尝试重新连接
                if (client is IAutoReconnectClient autoReconnectClient && autoReconnectClient.EnableAutoReconnect)
                {
                    var reconnectResult = await autoReconnectClient.CheckAndReconnectAsync();
                    if (!reconnectResult.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult($"客户端自动重连失败，无法执行批量写入操作 [{contextInfo}]");
                    }
                }
                else
                {
                    return OperationResult.CreateFailedResult($"客户端没有连接 [{contextInfo}]");
                }
            }
            
            // 执行批量写入操作
            return await ClientOperationExecutor.ExecuteAsync(
                batchWriteOperation,
                "BatchWrite",
                client.Logger,
                contextInfo,
                cancellationToken,
                timeout);
        }
        
        /// <summary>
        /// 执行一个批量写入操作，包含异常处理和日志记录
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="batchWriteOperation">批量写入操作</param>
        /// <param name="addresses">地址和值的字典</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static Task<OperationResult> ExecuteBatchWriteAsync(
            this IDeviceClient client,
            Func<Task<OperationResult>> batchWriteOperation,
            Dictionary<string, object> addresses,
            CancellationToken cancellationToken = default)
        {
            return ExecuteBatchWriteAsync(
                client,
                (ct) => batchWriteOperation(),
                addresses,
                cancellationToken);
        }
        
        /// <summary>
        /// 执行一个连接操作，包含异常处理和日志记录
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult> ExecuteConnectAsync(
            this IDeviceClient client,
            CancellationToken cancellationToken = default)
        {
            string contextInfo = BuildContextInfo(client, null);
            
            // 已经连接，直接返回成功
            if (client.Connected)
            {
                client.Logger?.LogDebug("客户端已连接 [{Context}]", contextInfo);
                return OperationResult.CreateSuccessResult();
            }
            
            // 执行连接操作
            return await ClientOperationExecutor.ExecuteWithRetryAsync(
                (ct) => client.ConnectAsync(),
                "Connect",
                3, // 默认重试3次
                1000, // 默认间隔1秒
                ClientOperationExecutor.IsTransientException,
                client.Logger,
                contextInfo,
                cancellationToken);
        }
        
        /// <summary>
        /// 执行一个断开连接操作，包含异常处理和日志记录
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult> ExecuteDisconnectAsync(
            this IDeviceClient client,
            CancellationToken cancellationToken = default)
        {
            string contextInfo = BuildContextInfo(client, null);
            
            // 已经断开连接，直接返回成功
            if (!client.Connected)
            {
                client.Logger?.LogDebug("客户端已断开连接 [{Context}]", contextInfo);
                return OperationResult.CreateSuccessResult();
            }
            
            // 执行断开连接操作
            return await ClientOperationExecutor.ExecuteAsync(
                (ct) => client.DisconnectAsync(),
                "Disconnect",
                client.Logger,
                contextInfo,
                cancellationToken);
        }
        
        /// <summary>
        /// 使用连接池执行操作
        /// </summary>
        /// <typeparam name="T">设备客户端类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="connectionPool">连接池</param>
        /// <param name="connectionId">连接标识</param>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult<TResult>> ExecuteWithConnectionPoolAsync<T, TResult>(
            this ConnectionPoolManager<T> connectionPool,
            string connectionId,
            Func<T, CancellationToken, Task<OperationResult<TResult>>> operation,
            string operationName,
            CancellationToken cancellationToken = default) where T : IDeviceClient
        {
            try
            {
                // 从连接池获取连接
                var client = await connectionPool.GetConnectionAsync(connectionId, cancellationToken);
                
                try
                {
                    // 执行操作
                    var result = await operation(client, cancellationToken);
                    return result;
                }
                finally
                {
                    // 释放连接回连接池
                    connectionPool.ReleaseConnection(client, connectionId);
                }
            }
            catch (Exception ex)
            {
                // 创建操作失败结果
                return OperationResult.CreateFailedResult<TResult>($"使用连接池执行操作失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 使用连接池执行操作
        /// </summary>
        /// <typeparam name="T">设备客户端类型</typeparam>
        /// <param name="connectionPool">连接池</param>
        /// <param name="connectionId">连接标识</param>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult> ExecuteWithConnectionPoolAsync<T>(
            this ConnectionPoolManager<T> connectionPool,
            string connectionId,
            Func<T, CancellationToken, Task<OperationResult>> operation,
            string operationName,
            CancellationToken cancellationToken = default) where T : IDeviceClient
        {
            try
            {
                // 从连接池获取连接
                var client = await connectionPool.GetConnectionAsync(connectionId, cancellationToken);
                
                try
                {
                    // 执行操作
                    var result = await operation(client, cancellationToken);
                    return result;
                }
                finally
                {
                    // 释放连接回连接池
                    connectionPool.ReleaseConnection(client, connectionId);
                }
            }
            catch (Exception ex)
            {
                // 创建操作失败结果
                return OperationResult.CreateFailedResult($"使用连接池执行操作失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 构建操作上下文信息
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="address">地址</param>
        /// <returns>格式化的上下文信息</returns>
        private static string BuildContextInfo(IDeviceClient client, string address)
        {
            string contextInfo = client.ToString();
            
            // 尝试获取IP终结点信息
            if (client.GetType().GetProperty("IPEndPoint") != null)
            {
                var ipEndPoint = client.GetType().GetProperty("IPEndPoint").GetValue(client);
                if (ipEndPoint != null)
                {
                    contextInfo = ipEndPoint.ToString();
                }
            }
            // 尝试获取串口信息
            else if (client.GetType().GetProperty("PortName") != null)
            {
                var portName = client.GetType().GetProperty("PortName").GetValue(client);
                if (portName != null)
                {
                    contextInfo = portName.ToString();
                }
            }
            
            // 添加地址信息
            if (!string.IsNullOrEmpty(address))
            {
                contextInfo += " @ " + address;
            }
            
            return contextInfo;
        }
    }
    
    /// <summary>
    /// 支持自动重连的客户端接口
    /// </summary>
    public interface IAutoReconnectClient : IDeviceClient
    {
        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        bool EnableAutoReconnect { get; set; }
        
        /// <summary>
        /// 最大自动重连次数
        /// </summary>
        int MaxReconnectAttempts { get; set; }
        
        /// <summary>
        /// 重连等待时间
        /// </summary>
        TimeSpan ReconnectDelay { get; set; }
        
        /// <summary>
        /// 连接检查间隔
        /// </summary>
        TimeSpan ConnectionCheckInterval { get; set; }
        
        /// <summary>
        /// 短连接模式下的最大重连次数
        /// </summary>
        int ShortConnectionReconnectAttempts { get; set; }
        
        /// <summary>
        /// 检查连接状态并在必要时自动重连
        /// </summary>
        /// <returns>连接操作结果</returns>
        Task<OperationResult> CheckAndReconnectAsync();
    }
} 
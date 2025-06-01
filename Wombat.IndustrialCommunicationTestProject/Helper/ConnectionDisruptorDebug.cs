using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;

namespace Wombat.IndustrialCommunicationTestProject.Helper
{
    /// <summary>
    /// 连接中断模拟器的调试版本，简化实现，避免栈溢出
    /// </summary>
    public class ConnectionDisruptorDebug
    {
        private readonly ILogger _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器，如果为null则不记录日志</param>
        public ConnectionDisruptorDebug(ILogger logger = null)
        {
            _logger = logger;
            _logger?.LogDebug("创建简化版连接中断模拟器实例");
        }

        /// <summary>
        /// 模拟连接中断 - 简化版本，直接调用客户端的断开方法
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> SimulateDisruption(IClient client)
        {
            _logger?.LogInformation("【调试版】模拟连接中断开始");
            
            try
            {
                // 如果客户端未连接，直接返回成功
                if (!client.Connected)
                {
                    _logger?.LogInformation("【调试版】客户端当前未连接，无需模拟中断");
                    return OperationResult.CreateSuccessResult("客户端当前未连接");
                }
                
                // 临时禁用自动重连（如果客户端支持）
                var wasAutoReconnectEnabled = false;
                if (client is IAutoReconnectClient autoReconnectClient)
                {
                    wasAutoReconnectEnabled = autoReconnectClient.EnableAutoReconnect;
                    if (wasAutoReconnectEnabled)
                    {
                        _logger?.LogDebug("【调试版】临时禁用自动重连功能");
                        autoReconnectClient.EnableAutoReconnect = false;
                    }
                }
                
                try
                {
                    // 直接调用客户端的Disconnect方法，避免复杂的反射操作
                    _logger?.LogDebug("【调试版】直接调用客户端Disconnect方法");
                    await client.DisconnectAsync();
                    
                    // 增加一个小延迟，确保断开操作完成
                    await Task.Delay(100);
                }
                finally
                {
                    // 恢复自动重连设置
                    if (client is IAutoReconnectClient autoReconnectClient2 && wasAutoReconnectEnabled)
                    {
                        _logger?.LogDebug("【调试版】恢复自动重连功能");
                        autoReconnectClient2.EnableAutoReconnect = true;
                    }
                }
                
                _logger?.LogInformation("【调试版】模拟连接中断完成");
                return OperationResult.CreateSuccessResult();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "【调试版】模拟连接中断失败");
                return OperationResult.CreateFailedResult(ex);
            }
        }

        /// <summary>
        /// 模拟短线重连场景 - 简化版本
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="disruptionDurationMs">中断持续时间(毫秒)</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> SimulateDisruptAndWait(IClient client, int disruptionDurationMs = 2000)
        {
            _logger?.LogInformation("【调试版】开始模拟短线重连场景，中断持续时间: {Duration}ms", disruptionDurationMs);
            
            try
            {
                // 1. 模拟连接中断
                _logger?.LogInformation("【调试版】开始模拟连接中断");
                var disruptResult = await SimulateDisruption(client);
                if (!disruptResult.IsSuccess)
                {
                    _logger?.LogWarning("【调试版】模拟连接中断失败: {Message}", disruptResult.Message);
                    return disruptResult;
                }

                // 2. 等待指定时间
                _logger?.LogInformation("【调试版】等待 {Duration}ms", disruptionDurationMs);
                await Task.Delay(disruptionDurationMs);
                
                // 3. 提示完成
                _logger?.LogInformation("【调试版】模拟短线重连场景完成");
                return OperationResult.CreateSuccessResult("中断模拟完成，等待客户端自行处理重连");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "【调试版】模拟短线重连过程中发生异常");
                return OperationResult.CreateFailedResult(ex);
            }
        }
    }
} 
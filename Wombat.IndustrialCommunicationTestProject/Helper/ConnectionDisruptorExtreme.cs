using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;

namespace Wombat.IndustrialCommunicationTestProject.Helper
{
    /// <summary>
    /// 连接中断模拟器的极简版本，彻底避免任何可能导致栈溢出的操作
    /// </summary>
    public class ConnectionDisruptorExtreme
    {
        private readonly ILogger _logger;
        private readonly Stopwatch _diagnosticTimer;
        private readonly string _instanceId;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器，如果为null则不记录日志</param>
        public ConnectionDisruptorExtreme(ILogger logger = null)
        {
            _logger = logger;
            _diagnosticTimer = new Stopwatch();
            _instanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _logger?.LogDebug($"[{_instanceId}] 创建极简版连接中断模拟器实例");
        }

        /// <summary>
        /// 模拟连接中断 - 超级安全版本，不执行任何实际操作，只返回成功
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <returns>操作结果</returns>
        public Task<OperationResult> SimulateSafeDisruption(IClient client)
        {
            _diagnosticTimer.Restart();
            _logger?.LogInformation($"[{_instanceId}] 【超级安全版】模拟连接中断开始");
            
            // 记录当前线程和内存信息，但不记录调用栈(可能很大)
            try
            {
                // 记录诊断信息
                _logger?.LogDebug($"[{_instanceId}] 线程ID: {Thread.CurrentThread.ManagedThreadId}，" +
                                 $"内存使用: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");

                // 记录客户端基本信息（不调用任何方法）
                if (client != null)
                {
                    _logger?.LogDebug($"[{_instanceId}] 客户端类型: {client.GetType().FullName}");
                }
                else
                {
                    _logger?.LogWarning($"[{_instanceId}] 提供的客户端为null");
                }
            }
            catch (Exception ex)
            {
                // 记录异常但继续执行
                _logger?.LogWarning($"[{_instanceId}] 记录诊断信息时发生异常: {ex.Message}");
            }
            
            // 对客户端不做任何操作，直接返回成功
            _diagnosticTimer.Stop();
            _logger?.LogInformation($"[{_instanceId}] 【超级安全版】模拟连接中断成功（不执行任何实际操作），耗时: {_diagnosticTimer.ElapsedMilliseconds}ms");
            
            return Task.FromResult(OperationResult.CreateSuccessResult($"[{_instanceId}] 超级安全模式下的模拟中断成功"));
        }

        /// <summary>
        /// 模拟短线重连场景 - 超级安全版本
        /// </summary>
        /// <param name="client">设备客户端</param>
        /// <param name="disruptionDurationMs">中断持续时间(毫秒)</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> SimulateSafeWait(IClient client, int disruptionDurationMs = 2000)
        {
            _diagnosticTimer.Restart();
            _logger?.LogInformation($"[{_instanceId}] 【超级安全版】开始模拟短线重连场景，中断持续时间: {disruptionDurationMs}ms");
            
            try
            {
                // 直接模拟成功
                var result = await SimulateSafeDisruption(client);
                
                // 等待指定时间
                _logger?.LogInformation($"[{_instanceId}] 【超级安全版】等待 {disruptionDurationMs}ms");
                await Task.Delay(disruptionDurationMs);
                
                // 完成
                _diagnosticTimer.Stop();
                _logger?.LogInformation($"[{_instanceId}] 【超级安全版】模拟短线重连场景完成，总耗时: {_diagnosticTimer.ElapsedMilliseconds}ms");
                return OperationResult.CreateSuccessResult($"[{_instanceId}] 超级安全等待完成，持续时间: {disruptionDurationMs}ms");
            }
            catch (Exception ex)
            {
                _diagnosticTimer.Stop();
                _logger?.LogError($"[{_instanceId}] 【超级安全版】模拟短线重连场景异常: {ex.Message}");
                return OperationResult.CreateFailedResult($"[{_instanceId}] 模拟短线重连场景异常: {ex.Message}");
            }
        }
    }
} 
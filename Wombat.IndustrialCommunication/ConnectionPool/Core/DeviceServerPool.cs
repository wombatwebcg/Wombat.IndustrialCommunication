using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 默认设备服务端资源池实现。
    /// </summary>
    public class DeviceServerPool : ResourcePool<IDeviceServer>, IDeviceServerPool
    {
        public DeviceServerPool(ConnectionPoolOptions options, IPooledResourceConnectionFactory<IDeviceServer> factory)
            : base(options, factory, ResourceRole.Server, "资源角色不是服务端")
        {
        }

        public OperationResult Start(ConnectionIdentity identity)
        {
            return StartAsync(identity).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> StartAsync(ConnectionIdentity identity, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (identity == null)
            {
                return OperationResult.CreateFailedResult("连接标识不能为空");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return OperationResult.CreateFailedResult("操作已取消");
            }

            var entryResult = GetRegisteredEntry(identity, "连接未注册");
            if (!entryResult.IsSuccess)
            {
                return OperationResult.CreateFailedResult(entryResult);
            }

            var entry = entryResult.ResultValue;
            var maxRetry = ResolveStartRetryCount();
            var baseBackoff = ResolveStartRetryBackoff();
            var attempt = 0;
            OperationResult lastFailure = null;

            while (attempt <= maxRetry)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;

                var ensureResult = await entry.EnsureAvailableAsync(ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                if (ensureResult.IsSuccess)
                {
                    if (attempt > 1)
                    {
                        ensureResult.OperationInfo.Add(string.Format("服务端启动重试成功，共尝试 {0} 次。", attempt));
                    }

                    return ensureResult;
                }

                lastFailure = ensureResult;
                if (attempt > maxRetry)
                {
                    break;
                }

                var retryBackoff = CalculateStartRetryBackoff(baseBackoff, attempt);
                await entry.NotifyRetryingAsync(attempt, maxRetry, retryBackoff, ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                await Task.Delay(retryBackoff, cancellationToken).ConfigureAwait(false);
            }

            if (lastFailure != null && IsPortConflictFailure(lastFailure))
            {
                return OperationResult.CreateFailedResult(
                    string.Format("检测到服务端端口占用冲突，启动失败。请检查端口配置或释放端口。详细信息: {0}", lastFailure.Message));
            }

            return lastFailure ?? OperationResult.CreateFailedResult("服务端启动失败");
        }

        public OperationResult Stop(ConnectionIdentity identity, string reason)
        {
            return StopAsync(identity, reason).GetAwaiter().GetResult();
        }

        public Task<OperationResult> StopAsync(ConnectionIdentity identity, string reason, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (identity == null)
            {
                return Task.FromResult(OperationResult.CreateFailedResult("连接标识不能为空"));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(OperationResult.CreateFailedResult("操作已取消"));
            }

            var entryResult = GetRegisteredEntry(identity, "连接未注册");
            if (!entryResult.IsSuccess)
            {
                return Task.FromResult(OperationResult.CreateFailedResult(entryResult.Message));
            }

            var stopResult = entryResult.ResultValue.Connection.DisconnectOrShutdown();
            if (!stopResult.IsSuccess)
            {
                return Task.FromResult(stopResult);
            }

            var message = string.IsNullOrWhiteSpace(reason) ? "服务端已停止" : reason;
            return Task.FromResult(OperationResult.CreateSuccessResult(message));
        }

        private int ResolveStartRetryCount()
        {
            return Options == null || Options.MaxRetryCount < 0 ? 0 : Options.MaxRetryCount;
        }

        private TimeSpan ResolveStartRetryBackoff()
        {
            if (Options == null || Options.RetryBackoff <= TimeSpan.Zero)
            {
                return TimeSpan.FromMilliseconds(200);
            }

            return Options.RetryBackoff;
        }

        private static TimeSpan CalculateStartRetryBackoff(TimeSpan baseBackoff, int attempt)
        {
            var multiplier = attempt <= 1 ? 1 : (1 << Math.Min(attempt - 1, 4));
            var delayMs = baseBackoff.TotalMilliseconds * multiplier;
            var cappedDelayMs = Math.Min(delayMs, 5000);
            return TimeSpan.FromMilliseconds(cappedDelayMs);
        }

        private static bool IsPortConflictFailure(OperationResult result)
        {
            if (result == null)
            {
                return false;
            }

            var message = result.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            var normalized = message.ToLowerInvariant();
            return normalized.Contains("address already in use")
                || normalized.Contains("only one usage")
                || normalized.Contains("端口")
                || normalized.Contains("port")
                || normalized.Contains("listen");
        }
    }
}

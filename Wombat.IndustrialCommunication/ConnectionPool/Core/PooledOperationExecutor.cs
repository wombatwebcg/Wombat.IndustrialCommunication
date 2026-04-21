using System;
using System.IO;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 池化执行器，负责统一重试与重连策略。
    /// </summary>
    public class PooledOperationExecutor
    {
        public async Task<OperationResult<T>> ExecuteAsync<T>(PooledConnectionEntry entry, Func<IDeviceClient, Task<OperationResult<T>>> action, ConnectionPoolOptions options)
        {
            if (entry == null)
            {
                return OperationResult.CreateFailedResult<T>("连接条目不能为空");
            }

            if (action == null)
            {
                return OperationResult.CreateFailedResult<T>("执行委托不能为空");
            }

            var maxRetry = options?.MaxRetryCount ?? 0;
            var retryBackoff = options?.RetryBackoff ?? TimeSpan.FromMilliseconds(200);
            var attempt = 0;

            while (true)
            {
                attempt++;
                var ensure = await entry.EnsureConnectedAsync().ConfigureAwait(false);
                if (!ensure.IsSuccess)
                {
                    if (attempt > maxRetry + 1)
                    {
                        return OperationResult.CreateFailedResult<T>(ensure);
                    }

                    await Task.Delay(retryBackoff).ConfigureAwait(false);
                    continue;
                }

                var executeResult = await entry.ExecuteAsync(action).ConfigureAwait(false);
                if (executeResult.IsSuccess)
                {
                    return executeResult;
                }

                var recoverable = IsRecoverable(executeResult);
                if (!recoverable || attempt > maxRetry + 1)
                {
                    return executeResult;
                }


                await entry.MarkFailureAsync().ConfigureAwait(false);
                await Task.Delay(retryBackoff).ConfigureAwait(false);
            }
        }

        public async Task<OperationResult> ExecuteAsync(PooledConnectionEntry entry, Func<IDeviceClient, Task<OperationResult>> action, ConnectionPoolOptions options)
        {
            var wrapped = await ExecuteAsync<object>(entry, async c =>
            {
                var r = await action(c).ConfigureAwait(false);
                if (r.IsSuccess)
                {
                    return OperationResult.CreateSuccessResult<object>(null);
                }

                return OperationResult.CreateFailedResult<object>(r);
            }, options).ConfigureAwait(false);

            return wrapped.IsSuccess ? OperationResult.CreateSuccessResult() : OperationResult.CreateFailedResult(wrapped);
        }

        private static bool IsRecoverable(OperationResult result)
        {
            if (result == null)
            {
                return false;
            }

            if (result.Exception is TimeoutException
                || result.Exception is IOException
                || result.Exception is ObjectDisposedException)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(result.Message))
            {
                return false;
            }

            var message = result.Message.ToLowerInvariant();
            return message.Contains("timeout")
                || message.Contains("timed out")
                || message.Contains("connection")
                || message.Contains("socket")
                || message.Contains("closed");
        }
    }
}

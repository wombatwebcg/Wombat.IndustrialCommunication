using System;
using System.IO;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    public class PooledResourceExecutor<TResource>
    {
        public async Task<OperationResult<T>> ExecuteAsync<T>(PooledResourceEntry<TResource> entry, System.Func<TResource, Task<OperationResult<T>>> action, ConnectionPoolOptions options, ConnectionExecutionOptions executionOptions)
        {
            if (entry == null)
            {
                return OperationResult.CreateFailedResult<T>("资源条目不能为空");
            }

            if (action == null)
            {
                return OperationResult.CreateFailedResult<T>("执行委托不能为空");
            }

            var effectiveExecutionOptions = NormalizeExecutionOptions(executionOptions);
            var retryEnabled = effectiveExecutionOptions.ResolveRetryEnabled();
            var maxRetry = retryEnabled ? effectiveExecutionOptions.ResolveRetryCount(options) : 0;
            var retryBackoff = effectiveExecutionOptions.ResolveRetryBackoff(options);
            var attempt = 0;
            var diagnostics = new OperationResult();

            while (true)
            {
                attempt++;
                var ensure = await entry.EnsureAvailableAsync(ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                MergeDiagnostics(diagnostics, ensure);
                if (!ensure.IsSuccess)
                {
                    if (!ShouldRetry(attempt, maxRetry))
                    {
                        AddRetryDecisionInfo(diagnostics, effectiveExecutionOptions, ensure, false, attempt, maxRetry);
                        return CompleteResult(OperationResult.CreateFailedResult<T>(ensure), diagnostics);
                    }

                    await entry.NotifyRetryingAsync(attempt, maxRetry, retryBackoff, ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                    AddRetryDecisionInfo(diagnostics, effectiveExecutionOptions, ensure, true, attempt, maxRetry);
                    await Task.Delay(retryBackoff).ConfigureAwait(false);
                    continue;
                }

                var executeResult = await entry.ExecuteAsync(action, ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                MergeDiagnostics(diagnostics, executeResult);
                if (executeResult.IsSuccess)
                {
                    return CompleteResult(executeResult, diagnostics);
                }

                var recoverable = IsRecoverable(executeResult);
                if (!recoverable || !ShouldRetry(attempt, maxRetry))
                {
                    AddRetryDecisionInfo(diagnostics, effectiveExecutionOptions, executeResult, false, attempt, maxRetry);
                    return CompleteResult(executeResult, diagnostics);
                }

                await entry.MarkFailureAsync(executeResult.Message, executeResult.Exception, ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                await entry.NotifyRetryingAsync(attempt, maxRetry, retryBackoff, ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                AddRetryDecisionInfo(diagnostics, effectiveExecutionOptions, executeResult, true, attempt, maxRetry);
                await entry.TryRecoverAsync("检测到可恢复异常，准备重建资源", ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                await Task.Delay(retryBackoff).ConfigureAwait(false);
            }
        }

        public async Task<OperationResult> ExecuteAsync(PooledResourceEntry<TResource> entry, System.Func<TResource, Task<OperationResult>> action, ConnectionPoolOptions options, ConnectionExecutionOptions executionOptions)
        {
            var wrapped = await ExecuteAsync<object>(entry, async resource =>
            {
                var result = await action(resource).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    return OperationResult.CreateSuccessResult<object>(null);
                }

                return OperationResult.CreateFailedResult<object>(result);
            }, options, executionOptions).ConfigureAwait(false);

            if (wrapped.IsSuccess)
            {
                return new OperationResult().SetInfo(wrapped).Complete();
            }

            return OperationResult.CreateFailedResult(wrapped);
        }

        private static ConnectionExecutionOptions NormalizeExecutionOptions(ConnectionExecutionOptions executionOptions)
        {
            return (executionOptions ?? ConnectionExecutionOptions.CreateDiagnostic()).Normalize();
        }

        private static bool ShouldRetry(int attempt, int maxRetry)
        {
            return attempt <= maxRetry;
        }

        private static OperationResult<T> CompleteResult<T>(OperationResult<T> result, OperationResult diagnostics)
        {
            MergeDiagnostics(result, diagnostics);
            return result.Complete();
        }

        private static void AddRetryDecisionInfo(OperationResult diagnostics, ConnectionExecutionOptions executionOptions, OperationResult result, bool willRetry, int attempt, int maxRetry)
        {
            if (diagnostics == null || executionOptions == null || result == null || result.IsSuccess)
            {
                return;
            }

            if (willRetry)
            {
                AddOperationInfo(diagnostics, string.Format("执行分类 {0} 第 {1}/{2} 次失败，命中恢复性重试条件。", executionOptions.Kind, attempt, maxRetry + 1));
                return;
            }

            if (!executionOptions.ResolveRetryEnabled() && IsRecoverable(result))
            {
                AddOperationInfo(diagnostics, string.Format("执行分类 {0} 默认或显式禁用恢复性重试，失败后直接返回。", executionOptions.Kind));
                return;
            }

            if (executionOptions.ResolveRetryEnabled() && IsRecoverable(result) && attempt > maxRetry)
            {
                AddOperationInfo(diagnostics, string.Format("执行分类 {0} 已达到最大恢复性重试次数 {1}。", executionOptions.Kind, maxRetry));
            }
        }

        private static void MergeDiagnostics(OperationResult target, OperationResult source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (source.Requsts != null && source.Requsts.Count > 0)
            {
                foreach (var request in source.Requsts)
                {
                    if (!target.Requsts.Contains(request))
                    {
                        target.Requsts.Add(request);
                    }
                }
            }

            if (source.Responses != null && source.Responses.Count > 0)
            {
                foreach (var response in source.Responses)
                {
                    if (!target.Responses.Contains(response))
                    {
                        target.Responses.Add(response);
                    }
                }
            }

            if (source.OperationInfo != null && source.OperationInfo.Count > 0)
            {
                foreach (var info in source.OperationInfo)
                {
                    AddOperationInfo(target, info);
                }
            }
        }

        private static void AddOperationInfo(OperationResult target, string info)
        {
            if (target == null || string.IsNullOrWhiteSpace(info))
            {
                return;
            }

            if (!target.OperationInfo.Contains(info))
            {
                target.OperationInfo.Add(info);
            }
        }

        private static bool IsRecoverable(OperationResult result)
        {
            if (result == null)
            {
                return false;
            }

            if (result.Exception is TimeoutException || result.Exception is IOException || result.Exception is ObjectDisposedException)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(result.Message))
            {
                return false;
            }

            var message = result.Message.ToLowerInvariant();
            return message.Contains("timeout") || message.Contains("timed out") || message.Contains("connection") || message.Contains("socket") || message.Contains("closed") || message.Contains("listen") || message.Contains("port");
        }
    }
}

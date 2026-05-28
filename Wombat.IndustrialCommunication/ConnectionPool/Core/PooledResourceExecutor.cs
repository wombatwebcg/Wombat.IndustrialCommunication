using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    public class PooledResourceExecutor<TResource>
    {
        public async Task<OperationResult<T>> ExecuteAsync<T>(PooledResourceEntry<TResource> entry, Func<TResource, CancellationToken, Task<OperationResult<T>>> action, ConnectionPoolOptions options, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
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
                if (IsCancellationRequested(entry, cancellationToken))
                {
                    return CompleteCancelledResult(entry.CreateCancelledExecutionResult<T>(cancellationToken), diagnostics);
                }

                attempt++;
                var ensure = await entry.EnsureAvailableAsync(ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                MergeDiagnostics(diagnostics, ensure);
                if (!ensure.IsSuccess)
                {
                    if (IsCancellationRequested(entry, cancellationToken) || !ShouldRetry(attempt, maxRetry))
                    {
                        AddRetryDecisionInfo(diagnostics, effectiveExecutionOptions, ensure, false, attempt, maxRetry);
                        return CompleteCancelledResult(ShouldReturnCancelled(entry, cancellationToken, ensure) ? entry.CreateCancelledExecutionResult<T>(cancellationToken) : OperationResult.CreateFailedResult<T>(ensure), diagnostics);
                    }

                    await entry.NotifyRetryingAsync(attempt, maxRetry, retryBackoff, ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                    AddRetryDecisionInfo(diagnostics, effectiveExecutionOptions, ensure, true, attempt, maxRetry);
                    if (!await WaitRetryBackoffAsync(entry, retryBackoff, cancellationToken).ConfigureAwait(false))
                    {
                        return CompleteCancelledResult(entry.CreateCancelledExecutionResult<T>(cancellationToken), diagnostics);
                    }
                    continue;
                }

                if (IsCancellationRequested(entry, cancellationToken))
                {
                    return CompleteCancelledResult(entry.CreateCancelledExecutionResult<T>(cancellationToken), diagnostics);
                }

                var executeResult = await entry.ExecuteAsync(action, cancellationToken, ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                MergeDiagnostics(diagnostics, executeResult);
                if (executeResult.IsSuccess)
                {
                    return CompleteResult(executeResult, diagnostics);
                }

                if (executeResult.IsCancelled || IsCancellationRequested(entry, cancellationToken))
                {
                    AddRetryDecisionInfo(diagnostics, effectiveExecutionOptions, executeResult, false, attempt, maxRetry);
                    return CompleteCancelledResult(executeResult, diagnostics);
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
                if (IsCancellationRequested(entry, cancellationToken))
                {
                    return CompleteCancelledResult(entry.CreateCancelledExecutionResult<T>(cancellationToken), diagnostics);
                }

                await entry.TryRecoverAsync("检测到可恢复异常，准备重建资源", ConnectionPoolMaintenanceMode.UserCall).ConfigureAwait(false);
                if (!await WaitRetryBackoffAsync(entry, retryBackoff, cancellationToken).ConfigureAwait(false))
                {
                    return CompleteCancelledResult(entry.CreateCancelledExecutionResult<T>(cancellationToken), diagnostics);
                }
            }
        }

        public async Task<OperationResult> ExecuteAsync(PooledResourceEntry<TResource> entry, Func<TResource, CancellationToken, Task<OperationResult>> action, ConnectionPoolOptions options, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            var wrapped = await ExecuteAsync<object>(entry, async (resource, executionToken) =>
            {
                var result = await action(resource, executionToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    return OperationResult.CreateSuccessResult<object>(null);
                }

                var failedResult = OperationResult.CreateFailedResult<object>(result);
                failedResult.IsCancelled = result.IsCancelled;
                return failedResult;
            }, options, executionOptions, cancellationToken).ConfigureAwait(false);

            if (wrapped.IsSuccess)
            {
                return new OperationResult().SetInfo(wrapped).Complete();
            }

            var failed = OperationResult.CreateFailedResult(wrapped);
            failed.IsCancelled = wrapped.IsCancelled;
            return failed.Complete();
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

        private static OperationResult<T> CompleteCancelledResult<T>(OperationResult<T> result, OperationResult diagnostics)
        {
            MergeDiagnostics(result, diagnostics);
            result.IsCancelled = true;
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

        private static bool IsCancellationRequested(PooledResourceEntry<TResource> entry, CancellationToken cancellationToken)
        {
            return cancellationToken.IsCancellationRequested || (entry != null && entry.IsForceClosingRequested);
        }

        private static bool ShouldReturnCancelled(PooledResourceEntry<TResource> entry, CancellationToken cancellationToken, OperationResult result)
        {
            return IsCancellationRequested(entry, cancellationToken) || (result != null && result.IsCancelled);
        }

        private static async Task<bool> WaitRetryBackoffAsync(PooledResourceEntry<TResource> entry, TimeSpan retryBackoff, CancellationToken cancellationToken)
        {
            if (retryBackoff <= TimeSpan.Zero)
            {
                return !IsCancellationRequested(entry, cancellationToken);
            }

            using (var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, entry.GetExecutionCancellationToken()))
            {
                try
                {
                    await Task.Delay(retryBackoff, linkedCancellationTokenSource.Token).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }
    }
}

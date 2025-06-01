using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 客户端操作执行器，提供统一的异常处理、日志记录和性能跟踪
    /// </summary>
    public static class ClientOperationExecutor
    {
        /// <summary>
        /// 执行一个异步操作并返回结果，包含统一的异常处理和日志记录
        /// </summary>
        /// <typeparam name="TResult">操作结果类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="contextInfo">操作上下文信息（如设备地址、参数等）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeout">操作超时时间（毫秒），小于等于0表示不设置超时</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult<TResult>> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<OperationResult<TResult>>> operation,
            string operationName,
            ILogger logger = null,
            string contextInfo = null,
            CancellationToken cancellationToken = default,
            int timeout = 0)
        {
            var stopwatch = Stopwatch.StartNew();
            var context = string.IsNullOrEmpty(contextInfo) ? string.Empty : $" [{contextInfo}]";
            
            // 如果设置了超时，创建超时取消令牌源
            using (var timeoutCts = timeout > 0 ? 
                  new CancellationTokenSource(timeout) : 
                  new CancellationTokenSource())
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                timeout > 0 ? timeoutCts.Token : CancellationToken.None))
            {
                try
                {
                    logger?.LogDebug("开始执行{Context} {Operation}", context, operationName);
                    
                    // 使用链接的取消令牌执行操作
                    var result = await operation(linkedCts.Token);
                    
                    stopwatch.Stop();
                    if (result.IsSuccess)
                    {
                        logger?.LogDebug("成功执行{Context} {Operation}，耗时: {ElapsedMs}ms", 
                            context, operationName, stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        logger?.LogWarning("执行{Context} {Operation}失败，耗时: {ElapsedMs}ms，错误: {ErrorMessage}", 
                            context, operationName, stopwatch.ElapsedMilliseconds, result.Message);
                    }
                    
                    // 记录操作耗时
                    result.TimeConsuming = stopwatch.ElapsedMilliseconds;
                    
                    return result;
                }
                catch (OperationCanceledException ex)
                {
                    stopwatch.Stop();
                    
                    // 判断是取消还是超时
                    string errorMessage;
                    if (timeout > 0 && timeoutCts.IsCancellationRequested)
                    {
                        errorMessage = $"执行{context} {operationName}超时: {timeout}ms";
                        logger?.LogError("执行{Context} {Operation}超时: {Timeout}ms，耗时: {ElapsedMs}ms", 
                            context, operationName, timeout, stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        errorMessage = $"执行{context} {operationName}被取消";
                        logger?.LogWarning("执行{Context} {Operation}被取消，耗时: {ElapsedMs}ms", 
                            context, operationName, stopwatch.ElapsedMilliseconds);
                    }
                    
                    // 创建操作失败结果
                    var result = OperationResult.CreateFailedResult<TResult>(errorMessage);
                    result.Exception = ex;
                    result.TimeConsuming = stopwatch.ElapsedMilliseconds;
                    result.IsCancelled = true;
                    
                    return result;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    
                    // 构建详细的异常信息
                    var errorMessage = BuildExceptionMessage(ex, operationName, context);
                    logger?.LogError(ex, "执行{Context} {Operation}时发生异常，耗时: {ElapsedMs}ms，异常: {ErrorMessage}", 
                        context, operationName, stopwatch.ElapsedMilliseconds, errorMessage);
                    
                    // 创建操作失败结果，包含详细的异常信息
                    var result = OperationResult.CreateFailedResult<TResult>(errorMessage);
                    result.Exception = ex;
                    result.TimeConsuming = stopwatch.ElapsedMilliseconds;
                    
                    return result;
                }
            }
        }
        
        /// <summary>
        /// 执行一个异步操作并返回结果，包含统一的异常处理和日志记录
        /// </summary>
        /// <typeparam name="TResult">操作结果类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="contextInfo">操作上下文信息（如设备地址、参数等）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static Task<OperationResult<TResult>> ExecuteAsync<TResult>(
            Func<Task<OperationResult<TResult>>> operation,
            string operationName,
            ILogger logger = null,
            string contextInfo = null,
            CancellationToken cancellationToken = default)
        {
            // 转换为支持取消的委托
            return ExecuteAsync(
                (ct) => operation(),
                operationName,
                logger,
                contextInfo,
                cancellationToken);
        }
        
        /// <summary>
        /// 执行一个异步操作并返回结果，包含统一的异常处理和日志记录
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="contextInfo">操作上下文信息（如设备地址、参数等）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeout">操作超时时间（毫秒），小于等于0表示不设置超时</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult> ExecuteAsync(
            Func<CancellationToken, Task<OperationResult>> operation,
            string operationName,
            ILogger logger = null,
            string contextInfo = null,
            CancellationToken cancellationToken = default,
            int timeout = 0)
        {
            var stopwatch = Stopwatch.StartNew();
            var context = string.IsNullOrEmpty(contextInfo) ? string.Empty : $" [{contextInfo}]";
            
            // 如果设置了超时，创建超时取消令牌源
            using (var timeoutCts = timeout > 0 ? 
                  new CancellationTokenSource(timeout) : 
                  new CancellationTokenSource())
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                timeout > 0 ? timeoutCts.Token : CancellationToken.None))
            {
                try
                {
                    logger?.LogDebug("开始执行{Context} {Operation}", context, operationName);
                    
                    // 使用链接的取消令牌执行操作
                    var result = await operation(linkedCts.Token);
                    
                    stopwatch.Stop();
                    if (result.IsSuccess)
                    {
                        logger?.LogDebug("成功执行{Context} {Operation}，耗时: {ElapsedMs}ms", 
                            context, operationName, stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        logger?.LogWarning("执行{Context} {Operation}失败，耗时: {ElapsedMs}ms，错误: {ErrorMessage}", 
                            context, operationName, stopwatch.ElapsedMilliseconds, result.Message);
                    }
                    
                    // 记录操作耗时
                    result.TimeConsuming = stopwatch.ElapsedMilliseconds;
                    
                    return result;
                }
                catch (OperationCanceledException ex)
                {
                    stopwatch.Stop();
                    
                    // 判断是取消还是超时
                    string errorMessage;
                    if (timeout > 0 && timeoutCts.IsCancellationRequested)
                    {
                        errorMessage = $"执行{context} {operationName}超时: {timeout}ms";
                        logger?.LogError("执行{Context} {Operation}超时: {Timeout}ms，耗时: {ElapsedMs}ms", 
                            context, operationName, timeout, stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        errorMessage = $"执行{context} {operationName}被取消";
                        logger?.LogWarning("执行{Context} {Operation}被取消，耗时: {ElapsedMs}ms", 
                            context, operationName, stopwatch.ElapsedMilliseconds);
                    }
                    
                    // 创建操作失败结果
                    var result = OperationResult.CreateFailedResult(errorMessage);
                    result.Exception = ex;
                    result.TimeConsuming = stopwatch.ElapsedMilliseconds;
                    result.IsCancelled = true;
                    
                    return result;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    
                    // 构建详细的异常信息
                    var errorMessage = BuildExceptionMessage(ex, operationName, context);
                    logger?.LogError(ex, "执行{Context} {Operation}时发生异常，耗时: {ElapsedMs}ms，异常: {ErrorMessage}", 
                        context, operationName, stopwatch.ElapsedMilliseconds, errorMessage);
                    
                    // 创建操作失败结果，包含详细的异常信息
                    var result = OperationResult.CreateFailedResult(errorMessage);
                    result.Exception = ex;
                    result.TimeConsuming = stopwatch.ElapsedMilliseconds;
                    
                    return result;
                }
            }
        }
        
        /// <summary>
        /// 执行一个异步操作并返回结果，包含统一的异常处理和日志记录
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="contextInfo">操作上下文信息（如设备地址、参数等）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static Task<OperationResult> ExecuteAsync(
            Func<Task<OperationResult>> operation,
            string operationName,
            ILogger logger = null,
            string contextInfo = null,
            CancellationToken cancellationToken = default)
        {
            // 转换为支持取消的委托
            return ExecuteAsync(
                (ct) => operation(),
                operationName,
                logger,
                contextInfo,
                cancellationToken);
        }
        
        /// <summary>
        /// 执行一个带有重试逻辑的异步操作
        /// </summary>
        /// <typeparam name="TResult">操作结果类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="retryCount">重试次数</param>
        /// <param name="retryDelayMs">重试间隔（毫秒）</param>
        /// <param name="shouldRetry">判断是否应该重试的函数</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="contextInfo">操作上下文信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeout">操作超时时间（毫秒），小于等于0表示不设置超时</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult<TResult>> ExecuteWithRetryAsync<TResult>(
            Func<CancellationToken, Task<OperationResult<TResult>>> operation,
            string operationName,
            int retryCount = 3,
            int retryDelayMs = 1000,
            Func<Exception, bool> shouldRetry = null,
            ILogger logger = null,
            string contextInfo = null,
            CancellationToken cancellationToken = default,
            int timeout = 0)
        {
            var context = string.IsNullOrEmpty(contextInfo) ? string.Empty : $" [{contextInfo}]";
            var currentAttempt = 0;
            var totalStopwatch = Stopwatch.StartNew();
            
            // 如果设置了超时，创建超时取消令牌源
            using (var timeoutCts = timeout > 0 ? 
                  new CancellationTokenSource(timeout) : 
                  new CancellationTokenSource())
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                timeout > 0 ? timeoutCts.Token : CancellationToken.None))
            {
                try
                {
                    // 获取合并的取消令牌
                    var combinedToken = linkedCts.Token;
                    
                    while (true)
                    {
                        // 检查是否已取消
                        combinedToken.ThrowIfCancellationRequested();
                        
                        currentAttempt++;
                        var attemptStopwatch = Stopwatch.StartNew();
                        
                        try
                        {
                            logger?.LogDebug("开始执行{Context} {Operation} (尝试 {Attempt}/{MaxAttempts})", 
                                context, operationName, currentAttempt, retryCount + 1);
                            
                            var result = await operation(combinedToken);
                            
                            attemptStopwatch.Stop();
                            if (result.IsSuccess)
                            {
                                totalStopwatch.Stop();
                                logger?.LogDebug("成功执行{Context} {Operation}，尝试: {Attempt}/{MaxAttempts}，当前尝试耗时: {AttemptMs}ms，总耗时: {TotalMs}ms", 
                                    context, operationName, currentAttempt, retryCount + 1, attemptStopwatch.ElapsedMilliseconds, totalStopwatch.ElapsedMilliseconds);
                                
                                result.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                                return result;
                            }
                            
                            // 操作失败但没有抛出异常，判断是否需要重试
                            if (currentAttempt <= retryCount)
                            {
                                logger?.LogWarning("执行{Context} {Operation}失败，尝试: {Attempt}/{MaxAttempts}，错误: {ErrorMessage}，准备重试...", 
                                    context, operationName, currentAttempt, retryCount + 1, result.Message);
                                
                                await Task.Delay(retryDelayMs, combinedToken);
                                continue;
                            }
                            
                            // 已达到最大重试次数
                            totalStopwatch.Stop();
                            logger?.LogWarning("执行{Context} {Operation}失败，已达最大尝试次数: {MaxAttempts}，总耗时: {TotalMs}ms，错误: {ErrorMessage}", 
                                context, operationName, retryCount + 1, totalStopwatch.ElapsedMilliseconds, result.Message);
                            
                            result.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                            return result;
                        }
                        catch (OperationCanceledException ex)
                        {
                            totalStopwatch.Stop();
                            
                            // 判断是取消还是超时
                            string errorMessage;
                            if (timeout > 0 && timeoutCts.IsCancellationRequested)
                            {
                                errorMessage = $"执行{context} {operationName}超时: {timeout}ms";
                                logger?.LogError("执行{Context} {Operation}超时: {Timeout}ms，耗时: {ElapsedMs}ms", 
                                    context, operationName, timeout, totalStopwatch.ElapsedMilliseconds);
                            }
                            else
                            {
                                errorMessage = $"执行{context} {operationName}被取消";
                                logger?.LogWarning("执行{Context} {Operation}被取消，耗时: {ElapsedMs}ms", 
                                    context, operationName, totalStopwatch.ElapsedMilliseconds);
                            }
                            
                            // 创建操作失败结果
                            var result = OperationResult.CreateFailedResult<TResult>(errorMessage);
                            result.Exception = ex;
                            result.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                            result.IsCancelled = true;
                            
                            return result;
                        }
                        catch (Exception ex)
                        {
                            attemptStopwatch.Stop();
                            
                            // 判断是否应该重试
                            bool canRetry = currentAttempt <= retryCount && (shouldRetry == null || shouldRetry(ex));
                            
                            if (canRetry)
                            {
                                logger?.LogWarning(ex, "执行{Context} {Operation}时发生异常，尝试: {Attempt}/{MaxAttempts}，准备重试...", 
                                    context, operationName, currentAttempt, retryCount + 1);
                                
                                try
                                {
                                    await Task.Delay(retryDelayMs, combinedToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    // 如果在延迟等待期间被取消，处理取消请求
                                    totalStopwatch.Stop();
                                    
                                    string errorMessage = $"执行{context} {operationName}被取消";
                                    logger?.LogWarning("执行{Context} {Operation}在重试等待期间被取消，耗时: {ElapsedMs}ms", 
                                        context, operationName, totalStopwatch.ElapsedMilliseconds);
                                    
                                    var result = OperationResult.CreateFailedResult<TResult>(errorMessage);
                                    result.Exception = ex;
                                    result.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                                    result.IsCancelled = true;
                                    
                                    return result;
                                }
                                
                                continue;
                            }
                            
                            // 不能重试，返回失败结果
                            totalStopwatch.Stop();
                            var errorMessage2 = BuildExceptionMessage(ex, operationName, context);
                            logger?.LogError(ex, "执行{Context} {Operation}时发生异常，已达最大尝试次数: {MaxAttempts}，总耗时: {TotalMs}ms，异常: {ErrorMessage}", 
                                context, operationName, retryCount + 1, totalStopwatch.ElapsedMilliseconds, errorMessage2);
                            
                            var result2 = OperationResult.CreateFailedResult<TResult>(errorMessage2);
                            result2.Exception = ex;
                            result2.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                            
                            return result2;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 捕获未在内部循环中处理的异常
                    totalStopwatch.Stop();
                    
                    var errorMessage = BuildExceptionMessage(ex, operationName, context);
                    logger?.LogError(ex, "执行{Context} {Operation}时发生未处理的异常，总耗时: {TotalMs}ms，异常: {ErrorMessage}", 
                        context, operationName, totalStopwatch.ElapsedMilliseconds, errorMessage);
                    
                    var result = OperationResult.CreateFailedResult<TResult>(errorMessage);
                    result.Exception = ex;
                    result.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                    
                    return result;
                }
            }
        }
        
        /// <summary>
        /// 执行一个带有重试逻辑的异步操作
        /// </summary>
        /// <typeparam name="TResult">操作结果类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="retryCount">重试次数</param>
        /// <param name="retryDelayMs">重试间隔（毫秒）</param>
        /// <param name="shouldRetry">判断是否应该重试的函数</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="contextInfo">操作上下文信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static Task<OperationResult<TResult>> ExecuteWithRetryAsync<TResult>(
            Func<Task<OperationResult<TResult>>> operation,
            string operationName,
            int retryCount = 3,
            int retryDelayMs = 1000,
            Func<Exception, bool> shouldRetry = null,
            ILogger logger = null,
            string contextInfo = null,
            CancellationToken cancellationToken = default)
        {
            // 转换为支持取消的委托
            return ExecuteWithRetryAsync(
                (ct) => operation(),
                operationName,
                retryCount,
                retryDelayMs,
                shouldRetry,
                logger,
                contextInfo,
                cancellationToken);
        }
        
        /// <summary>
        /// 执行一个带有重试逻辑的异步操作
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="retryCount">重试次数</param>
        /// <param name="retryDelayMs">重试间隔（毫秒）</param>
        /// <param name="shouldRetry">判断是否应该重试的函数</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="contextInfo">操作上下文信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="timeout">操作超时时间（毫秒），小于等于0表示不设置超时</param>
        /// <returns>操作结果</returns>
        public static async Task<OperationResult> ExecuteWithRetryAsync(
            Func<CancellationToken, Task<OperationResult>> operation,
            string operationName,
            int retryCount = 3,
            int retryDelayMs = 1000,
            Func<Exception, bool> shouldRetry = null,
            ILogger logger = null,
            string contextInfo = null,
            CancellationToken cancellationToken = default,
            int timeout = 0)
        {
            var context = string.IsNullOrEmpty(contextInfo) ? string.Empty : $" [{contextInfo}]";
            var currentAttempt = 0;
            var totalStopwatch = Stopwatch.StartNew();
            
            // 如果设置了超时，创建超时取消令牌源
            using (var timeoutCts = timeout > 0 ? 
                  new CancellationTokenSource(timeout) : 
                  new CancellationTokenSource())
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                timeout > 0 ? timeoutCts.Token : CancellationToken.None))
            {
                try
                {
                    // 获取合并的取消令牌
                    var combinedToken = linkedCts.Token;
                    
                    while (true)
                    {
                        // 检查是否已取消
                        combinedToken.ThrowIfCancellationRequested();
                        
                        currentAttempt++;
                        var attemptStopwatch = Stopwatch.StartNew();
                        
                        try
                        {
                            logger?.LogDebug("开始执行{Context} {Operation} (尝试 {Attempt}/{MaxAttempts})", 
                                context, operationName, currentAttempt, retryCount + 1);
                            
                            var result = await operation(combinedToken);
                            
                            attemptStopwatch.Stop();
                            if (result.IsSuccess)
                            {
                                totalStopwatch.Stop();
                                logger?.LogDebug("成功执行{Context} {Operation}，尝试: {Attempt}/{MaxAttempts}，当前尝试耗时: {AttemptMs}ms，总耗时: {TotalMs}ms", 
                                    context, operationName, currentAttempt, retryCount + 1, attemptStopwatch.ElapsedMilliseconds, totalStopwatch.ElapsedMilliseconds);
                                
                                result.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                                return result;
                            }
                            
                            // 操作失败但没有抛出异常，判断是否需要重试
                            if (currentAttempt <= retryCount)
                            {
                                logger?.LogWarning("执行{Context} {Operation}失败，尝试: {Attempt}/{MaxAttempts}，错误: {ErrorMessage}，准备重试...", 
                                    context, operationName, currentAttempt, retryCount + 1, result.Message);
                                
                                await Task.Delay(retryDelayMs, combinedToken);
                                continue;
                            }
                            
                            // 已达到最大重试次数
                            totalStopwatch.Stop();
                            logger?.LogWarning("执行{Context} {Operation}失败，已达最大尝试次数: {MaxAttempts}，总耗时: {TotalMs}ms，错误: {ErrorMessage}", 
                                context, operationName, retryCount + 1, totalStopwatch.ElapsedMilliseconds, result.Message);
                            
                            result.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                            return result;
                        }
                        catch (OperationCanceledException ex)
                        {
                            totalStopwatch.Stop();
                            
                            // 判断是取消还是超时
                            string errorMessage;
                            if (timeout > 0 && timeoutCts.IsCancellationRequested)
                            {
                                errorMessage = $"执行{context} {operationName}超时: {timeout}ms";
                                logger?.LogError("执行{Context} {Operation}超时: {Timeout}ms，耗时: {ElapsedMs}ms", 
                                    context, operationName, timeout, totalStopwatch.ElapsedMilliseconds);
                            }
                            else
                            {
                                errorMessage = $"执行{context} {operationName}被取消";
                                logger?.LogWarning("执行{Context} {Operation}被取消，耗时: {ElapsedMs}ms", 
                                    context, operationName, totalStopwatch.ElapsedMilliseconds);
                            }
                            
                            // 创建操作失败结果
                            var result = OperationResult.CreateFailedResult(errorMessage);
                            result.Exception = ex;
                            result.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                            result.IsCancelled = true;
                            
                            return result;
                        }
                        catch (Exception ex)
                        {
                            attemptStopwatch.Stop();
                            
                            // 判断是否应该重试
                            bool canRetry = currentAttempt <= retryCount && (shouldRetry == null || shouldRetry(ex));
                            
                            if (canRetry)
                            {
                                logger?.LogWarning(ex, "执行{Context} {Operation}时发生异常，尝试: {Attempt}/{MaxAttempts}，准备重试...", 
                                    context, operationName, currentAttempt, retryCount + 1);
                                
                                try
                                {
                                    await Task.Delay(retryDelayMs, combinedToken);
                                }
                                catch (OperationCanceledException)
                                {
                                    // 如果在延迟等待期间被取消，处理取消请求
                                    totalStopwatch.Stop();
                                    
                                    string errorMessage = $"执行{context} {operationName}被取消";
                                    logger?.LogWarning("执行{Context} {Operation}在重试等待期间被取消，耗时: {ElapsedMs}ms", 
                                        context, operationName, totalStopwatch.ElapsedMilliseconds);
                                    
                                    var result = OperationResult.CreateFailedResult(errorMessage);
                                    result.Exception = ex;
                                    result.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                                    result.IsCancelled = true;
                                    
                                    return result;
                                }
                                
                                continue;
                            }
                            
                            // 不能重试，返回失败结果
                            totalStopwatch.Stop();
                            var errorMessage2 = BuildExceptionMessage(ex, operationName, context);
                            logger?.LogError(ex, "执行{Context} {Operation}时发生异常，已达最大尝试次数: {MaxAttempts}，总耗时: {TotalMs}ms，异常: {ErrorMessage}", 
                                context, operationName, retryCount + 1, totalStopwatch.ElapsedMilliseconds, errorMessage2);
                            
                            var result2 = OperationResult.CreateFailedResult(errorMessage2);
                            result2.Exception = ex;
                            result2.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                            
                            return result2;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 捕获未在内部循环中处理的异常
                    totalStopwatch.Stop();
                    
                    var errorMessage = BuildExceptionMessage(ex, operationName, context);
                    logger?.LogError(ex, "执行{Context} {Operation}时发生未处理的异常，总耗时: {TotalMs}ms，异常: {ErrorMessage}", 
                        context, operationName, totalStopwatch.ElapsedMilliseconds, errorMessage);
                    
                    var result = OperationResult.CreateFailedResult(errorMessage);
                    result.Exception = ex;
                    result.TimeConsuming = totalStopwatch.ElapsedMilliseconds;
                    
                    return result;
                }
            }
        }
        
        /// <summary>
        /// 执行一个带有重试逻辑的异步操作
        /// </summary>
        /// <param name="operation">要执行的操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="retryCount">重试次数</param>
        /// <param name="retryDelayMs">重试间隔（毫秒）</param>
        /// <param name="shouldRetry">判断是否应该重试的函数</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="contextInfo">操作上下文信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public static Task<OperationResult> ExecuteWithRetryAsync(
            Func<Task<OperationResult>> operation,
            string operationName,
            int retryCount = 3,
            int retryDelayMs = 1000,
            Func<Exception, bool> shouldRetry = null,
            ILogger logger = null,
            string contextInfo = null,
            CancellationToken cancellationToken = default)
        {
            // 转换为支持取消的委托
            return ExecuteWithRetryAsync(
                (ct) => operation(),
                operationName,
                retryCount,
                retryDelayMs,
                shouldRetry,
                logger,
                contextInfo,
                cancellationToken);
        }
        
        /// <summary>
        /// 记录请求和响应的详细信息
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="operationName">操作名称</param>
        /// <param name="requestData">请求数据</param>
        /// <param name="responseData">响应数据</param>
        /// <param name="contextInfo">上下文信息</param>
        public static void LogRequestResponse(
            ILogger logger,
            string operationName,
            byte[] requestData,
            byte[] responseData,
            string contextInfo = null)
        {
            if (logger == null || logger.IsEnabled(LogLevel.Trace) == false)
                return;
                
            var context = string.IsNullOrEmpty(contextInfo) ? string.Empty : $" [{contextInfo}]";
            
            if (requestData != null && requestData.Length > 0)
            {
                logger.LogTrace("请求数据{Context} {Operation}: {RequestHex}", 
                    context, operationName, BitConverter.ToString(requestData).Replace("-", " "));
            }
            
            if (responseData != null && responseData.Length > 0)
            {
                logger.LogTrace("响应数据{Context} {Operation}: {ResponseHex}", 
                    context, operationName, BitConverter.ToString(responseData).Replace("-", " "));
            }
        }
        
        /// <summary>
        /// 构建详细的异常信息
        /// </summary>
        /// <param name="ex">异常</param>
        /// <param name="operationName">操作名称</param>
        /// <param name="context">上下文信息</param>
        /// <returns>格式化的异常信息</returns>
        private static string BuildExceptionMessage(Exception ex, string operationName, string context)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"执行{context} {operationName}时发生异常: {ex.Message}");
            
            // 添加内部异常信息
            var innerEx = ex.InnerException;
            int depth = 1;
            while (innerEx != null)
            {
                sb.AppendLine($"内部异常 {depth}: {innerEx.Message}");
                innerEx = innerEx.InnerException;
                depth++;
            }
            
            // 添加堆栈跟踪信息（仅在调试模式下）
            #if DEBUG
            sb.AppendLine("堆栈跟踪:");
            sb.AppendLine(ex.StackTrace);
            #endif
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 判断异常是否是通信相关的临时异常（可以重试）
        /// </summary>
        /// <param name="ex">异常</param>
        /// <returns>是否可以重试</returns>
        public static bool IsTransientException(Exception ex)
        {
            // 检查异常类型和消息，判断是否是临时性的网络或IO异常
            if (ex == null)
                return false;
                
            // 检查异常类型
            if (ex is System.IO.IOException || 
                ex is System.Net.Sockets.SocketException ||
                ex is TimeoutException ||
                ex is System.IO.InvalidDataException)
            {
                return true;
            }
            
            // 检查异常消息中的关键词
            string message = ex.Message.ToLowerInvariant();
            if (message.Contains("timeout") ||
                message.Contains("连接") ||
                message.Contains("connection") ||
                message.Contains("断开") ||
                message.Contains("reset") ||
                message.Contains("重置") ||
                message.Contains("aborted") ||
                message.Contains("network") ||
                message.Contains("网络"))
            {
                return true;
            }
            
            // 递归检查内部异常
            return ex.InnerException != null && IsTransientException(ex.InnerException);
        }
    }
    
    /// <summary>
    /// 扩展OperationResult类，添加取消状态
    /// </summary>
    public static class OperationResultExtensions
    {
        /// <summary>
        /// 设置操作结果为已取消
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="result">操作结果</param>
        /// <param name="message">取消消息</param>
        /// <returns>已取消的操作结果</returns>
        public static OperationResult<T> SetCancelled<T>(this OperationResult<T> result, string message = null)
        {
            result.IsSuccess = false;
            result.IsCancelled = true;
            
            if (!string.IsNullOrEmpty(message))
            {
                result.Message = message;
            }
            else if (string.IsNullOrEmpty(result.Message))
            {
                result.Message = "操作已取消";
            }
            
            return result;
        }
        
        /// <summary>
        /// 设置操作结果为已取消
        /// </summary>
        /// <param name="result">操作结果</param>
        /// <param name="message">取消消息</param>
        /// <returns>已取消的操作结果</returns>
        public static OperationResult SetCancelled(this OperationResult result, string message = null)
        {
            result.IsSuccess = false;
            result.IsCancelled = true;
            
            if (!string.IsNullOrEmpty(message))
            {
                result.Message = message;
            }
            else if (string.IsNullOrEmpty(result.Message))
            {
                result.Message = "操作已取消";
            }
            
            return result;
        }
        
        /// <summary>
        /// 标记操作结果已完成，并设置耗时
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="result">操作结果</param>
        /// <param name="startTime">开始时间（如不指定，则使用已设置的TimeConsuming）</param>
        /// <returns>已设置耗时的操作结果</returns>
        public static OperationResult<T> Complete<T>(this OperationResult<T> result, DateTime? startTime = null)
        {
            if (startTime.HasValue)
            {
                result.TimeConsuming = (long)(DateTime.Now - startTime.Value).TotalMilliseconds;
            }
            
            return result;
        }
        
        /// <summary>
        /// 标记操作结果已完成，并设置耗时
        /// </summary>
        /// <param name="result">操作结果</param>
        /// <param name="startTime">开始时间（如不指定，则使用已设置的TimeConsuming）</param>
        /// <returns>已设置耗时的操作结果</returns>
        public static OperationResult Complete(this OperationResult result, DateTime? startTime = null)
        {
            if (startTime.HasValue)
            {
                result.TimeConsuming = (long)(DateTime.Now - startTime.Value).TotalMilliseconds;
            }
            
            return result;
        }
    }
} 
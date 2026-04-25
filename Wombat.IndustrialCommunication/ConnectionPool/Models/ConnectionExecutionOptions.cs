using System;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 连接池执行选项。
    /// </summary>
    public sealed class ConnectionExecutionOptions
    {
        /// <summary>
        /// 执行分类。
        /// </summary>
        public ConnectionExecutionKind Kind { get; set; } = ConnectionExecutionKind.Diagnostic;

        /// <summary>
        /// 是否启用恢复性重试。未指定时按执行分类决定默认值。
        /// </summary>
        public bool? EnableRetry { get; set; }

        /// <summary>
        /// 恢复性重试次数。未指定时回退到连接池默认值。
        /// </summary>
        public int? MaxRetryCount { get; set; }

        /// <summary>
        /// 恢复性重试退避时长。未指定时回退到连接池默认值。
        /// </summary>
        public TimeSpan? RetryBackoff { get; set; }

        /// <summary>
        /// 创建默认读策略。
        /// </summary>
        public static ConnectionExecutionOptions CreateRead()
        {
            return new ConnectionExecutionOptions
            {
                Kind = ConnectionExecutionKind.Read
            };
        }

        /// <summary>
        /// 创建默认写策略。
        /// </summary>
        public static ConnectionExecutionOptions CreateWrite()
        {
            return new ConnectionExecutionOptions
            {
                Kind = ConnectionExecutionKind.Write
            };
        }

        /// <summary>
        /// 创建默认诊断策略。
        /// </summary>
        public static ConnectionExecutionOptions CreateDiagnostic()
        {
            return new ConnectionExecutionOptions
            {
                Kind = ConnectionExecutionKind.Diagnostic
            };
        }

        internal ConnectionExecutionOptions Normalize()
        {
            return new ConnectionExecutionOptions
            {
                Kind = Kind,
                EnableRetry = EnableRetry,
                MaxRetryCount = MaxRetryCount,
                RetryBackoff = RetryBackoff
            };
        }

        internal bool ResolveRetryEnabled()
        {
            if (EnableRetry.HasValue)
            {
                return EnableRetry.Value;
            }

            return Kind == ConnectionExecutionKind.Read;
        }

        internal int ResolveRetryCount(ConnectionPoolOptions poolOptions)
        {
            var retryCount = MaxRetryCount ?? (poolOptions == null ? 0 : poolOptions.MaxRetryCount);
            return retryCount < 0 ? 0 : retryCount;
        }

        internal TimeSpan ResolveRetryBackoff(ConnectionPoolOptions poolOptions)
        {
            if (RetryBackoff.HasValue && RetryBackoff.Value > TimeSpan.Zero)
            {
                return RetryBackoff.Value;
            }

            if (poolOptions != null && poolOptions.RetryBackoff > TimeSpan.Zero)
            {
                return poolOptions.RetryBackoff;
            }

            return TimeSpan.FromMilliseconds(200);
        }
    }
}

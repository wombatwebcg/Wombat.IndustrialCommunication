using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 池化资源统一抽象，封装协议差异。
    /// </summary>
    public interface IPooledResourceConnection<TResource>
    {
        ConnectionIdentity Identity { get; }

        ConnectionEntryLifecycleState State { get; }

        DateTime LastActiveTimeUtc { get; }

        bool IsAvailable { get; }

        TResource Resource { get; }

        OperationResult EnsureAvailable();

        Task<OperationResult> EnsureAvailableAsync();

        Task<OperationResult> ProbeAsync(TimeSpan timeout);

        OperationResult Invalidate(string reason);

        OperationResult DisconnectOrShutdown();

        Task<OperationResult<T>> ExecuteAsync<T>(Func<TResource, Task<OperationResult<T>>> action);

        Task<OperationResult> ExecuteAsync(Func<TResource, Task<OperationResult>> action);
    }
}

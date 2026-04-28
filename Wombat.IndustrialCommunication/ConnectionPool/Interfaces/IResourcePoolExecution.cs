using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 资源池执行接口。
    /// </summary>
    public interface IResourcePoolExecution<TResource>
    {
        Task<OperationResult<T>> ExecuteAsync<T>(ConnectionIdentity identity, Func<TResource, Task<OperationResult<T>>> action, CancellationToken cancellationToken = default(CancellationToken));

        Task<OperationResult<T>> ExecuteAsync<T>(ConnectionIdentity identity, Func<TResource, Task<OperationResult<T>>> action, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken));

        Task<OperationResult> ExecuteAsync(ConnectionIdentity identity, Func<TResource, Task<OperationResult>> action, CancellationToken cancellationToken = default(CancellationToken));

        Task<OperationResult> ExecuteAsync(ConnectionIdentity identity, Func<TResource, Task<OperationResult>> action, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken));
    }
}

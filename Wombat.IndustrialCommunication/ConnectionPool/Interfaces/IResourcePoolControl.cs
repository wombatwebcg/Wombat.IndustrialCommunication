using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 资源池控制接口。
    /// </summary>
    public interface IResourcePoolControl
    {
        OperationResult Register(ResourceDescriptor descriptor);

        OperationResult<ConnectionLease> Acquire(ConnectionIdentity identity);

        Task<OperationResult<ConnectionLease>> AcquireAsync(ConnectionIdentity identity, CancellationToken cancellationToken = default(CancellationToken));

        OperationResult Release(ConnectionLease lease);

        OperationResult Invalidate(ConnectionIdentity identity, string reason);

        OperationResult Unregister(ConnectionIdentity identity, string reason);

        OperationResult ForceReconnect(ConnectionIdentity identity, string reason);

        OperationResult<int> CleanupIdle();

        OperationResult<int> CleanupExpiredLeases();
    }
}

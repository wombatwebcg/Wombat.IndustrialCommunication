using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 服务端生命周期执行能力。
    /// </summary>
    public interface IDeviceServerLifecycleExecution
    {
        OperationResult Start(ConnectionIdentity identity);

        Task<OperationResult> StartAsync(ConnectionIdentity identity, CancellationToken cancellationToken = default(CancellationToken));

        OperationResult Stop(ConnectionIdentity identity, string reason);

        Task<OperationResult> StopAsync(ConnectionIdentity identity, string reason, CancellationToken cancellationToken = default(CancellationToken));
    }
}

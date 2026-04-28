using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 客户端点位执行能力。
    /// </summary>
    public interface IDeviceClientPointExecution
    {
        Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointReadRequest> points, CancellationToken cancellationToken = default(CancellationToken));

        Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointReadRequest> points, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken));

        Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointWriteRequest> points, CancellationToken cancellationToken = default(CancellationToken));

        Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointWriteRequest> points, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken));
    }
}

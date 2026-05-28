using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Core
{
    /// <summary>
    /// 默认设备客户端资源池实现。
    /// </summary>
    public class DeviceClientPool : ResourcePool<IDeviceClient>, IDeviceClientPool
    {
        public DeviceClientPool(ConnectionPoolOptions options, IPooledResourceConnectionFactory<IDeviceClient> factory)
            : base(options, factory, ResourceRole.Client, "资源角色不是客户端")
        {
        }

        public async Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointReadRequest> points, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ReadPointsAsync(identity, points, ConnectionExecutionOptions.CreateRead(), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointReadRequest> points, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();

            var normalized = PointListOperationHelper.NormalizeReadRequests(points);
            if (!normalized.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IList<DevicePointReadResult>>(normalized);
            }

            return await ExecuteWithExecutionTokenAsync<IList<DevicePointReadResult>>(identity, (client, executionToken) =>
                PointListOperationHelper.ReadPointsAsync(client, normalized.ResultValue, executionToken), executionOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointWriteRequest> points, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await WritePointsAsync(identity, points, ConnectionExecutionOptions.CreateWrite(), cancellationToken).ConfigureAwait(false);
        }

        public async Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointWriteRequest> points, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();

            var normalized = PointListOperationHelper.NormalizeWriteRequests(points);
            if (!normalized.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IList<DevicePointWriteResult>>(normalized);
            }

            return await ExecuteWithExecutionTokenAsync<IList<DevicePointWriteResult>>(identity, (client, executionToken) =>
                PointListOperationHelper.WritePointsAsync(client, normalized.ResultValue, executionToken), executionOptions, cancellationToken).ConfigureAwait(false);
        }
    }
}

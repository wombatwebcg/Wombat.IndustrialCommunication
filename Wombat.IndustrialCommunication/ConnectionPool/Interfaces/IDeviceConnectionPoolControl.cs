using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 连接池控制接口。
    /// </summary>
    public interface IDeviceConnectionPoolControl
    {
        /// <summary>
        /// 注册设备连接描述。
        /// </summary>
        OperationResult Register(DeviceConnectionDescriptor descriptor);

        /// <summary>
        /// 获取连接租约。
        /// </summary>
        OperationResult<ConnectionLease> Acquire(ConnectionIdentity identity);

        /// <summary>
        /// 异步获取连接租约。
        /// </summary>
        Task<OperationResult<ConnectionLease>> AcquireAsync(ConnectionIdentity identity, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 释放连接租约。
        /// </summary>
        OperationResult Release(ConnectionLease lease);

        /// <summary>
        /// 失效连接条目。
        /// </summary>
        OperationResult Invalidate(ConnectionIdentity identity, string reason);

        /// <summary>
        /// 注销并移除连接条目。
        /// </summary>
        OperationResult Unregister(ConnectionIdentity identity, string reason);

        /// <summary>
        /// 强制重连指定连接。
        /// </summary>
        OperationResult ForceReconnect(ConnectionIdentity identity, string reason);

        /// <summary>
        /// 回收空闲连接。
        /// </summary>
        OperationResult<int> CleanupIdle();

        /// <summary>
        /// 清理过期租约。
        /// </summary>
        OperationResult<int> CleanupExpiredLeases();
    }
}

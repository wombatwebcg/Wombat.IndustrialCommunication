using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 设备连接池统一接口。
    /// </summary>
    public interface IDeviceConnectionPool : System.IDisposable
    {
        /// <summary>
        /// 当前连接池配置。
        /// </summary>
        ConnectionPoolOptions Options { get; }

        /// <summary>
        /// 注册设备连接描述。
        /// </summary>
        OperationResult Register(DeviceConnectionDescriptor descriptor);

        /// <summary>
        /// 获取连接租约。
        /// 语义约束：仅当条目处于 Ready/Leased 状态且未失效时允许获取租约。
        /// </summary>
        OperationResult<ConnectionLease> Acquire(ConnectionIdentity identity);

        /// <summary>
        /// 异步获取连接租约。
        /// </summary>
        Task<OperationResult<ConnectionLease>> AcquireAsync(ConnectionIdentity identity, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 释放连接租约。
        /// 语义约束：释放后引用计数减少，且在引用计数为 0 时更新最后活跃时间。
        /// </summary>
        OperationResult Release(ConnectionLease lease);

        /// <summary>
        /// 失效连接条目。
        /// 语义约束：失效后禁止新租约；现存租约释放完成后条目可被回收。
        /// </summary>
        OperationResult Invalidate(ConnectionIdentity identity, string reason);

        /// <summary>
        /// 基于连接池执行带返回值的操作。
        /// </summary>
        Task<OperationResult<T>> ExecuteAsync<T>(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult<T>>> action, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 基于连接池执行无返回值的操作。
        /// </summary>
        Task<OperationResult> ExecuteAsync(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult>> action, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 通过连接池读取点位列表。
        /// </summary>
        Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointReadRequest> points, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 通过连接池写入点位列表。
        /// </summary>
        Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointWriteRequest> points, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 回收空闲连接。
        /// </summary>
        OperationResult<int> CleanupIdle();

        /// <summary>
        /// 获取连接条目状态快照。
        /// </summary>
        OperationResult<IDictionary<ConnectionIdentity, ConnectionEntryState>> GetStates();
    }
}

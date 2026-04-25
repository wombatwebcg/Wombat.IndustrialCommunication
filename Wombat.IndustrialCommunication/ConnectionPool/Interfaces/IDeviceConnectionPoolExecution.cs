using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 连接池执行接口。
    /// </summary>
    public interface IDeviceConnectionPoolExecution
    {
        /// <summary>
        /// 基于连接池执行带返回值的操作。
        /// </summary>
        Task<OperationResult<T>> ExecuteAsync<T>(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult<T>>> action, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 基于连接池按指定执行策略执行带返回值的操作。
        /// </summary>
        Task<OperationResult<T>> ExecuteAsync<T>(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult<T>>> action, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 基于连接池执行无返回值的操作。
        /// </summary>
        Task<OperationResult> ExecuteAsync(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult>> action, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 基于连接池按指定执行策略执行无返回值的操作。
        /// </summary>
        Task<OperationResult> ExecuteAsync(ConnectionIdentity identity, Func<IDeviceClient, Task<OperationResult>> action, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 通过连接池读取点位列表。
        /// </summary>
        Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointReadRequest> points, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 通过连接池按指定执行策略读取点位列表。
        /// </summary>
        Task<OperationResult<IList<DevicePointReadResult>>> ReadPointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointReadRequest> points, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 通过连接池写入点位列表。
        /// </summary>
        Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointWriteRequest> points, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 通过连接池按指定执行策略写入点位列表。
        /// </summary>
        Task<OperationResult<IList<DevicePointWriteResult>>> WritePointsAsync(ConnectionIdentity identity, IEnumerable<DevicePointWriteRequest> points, ConnectionExecutionOptions executionOptions, CancellationToken cancellationToken = default(CancellationToken));
    }
}

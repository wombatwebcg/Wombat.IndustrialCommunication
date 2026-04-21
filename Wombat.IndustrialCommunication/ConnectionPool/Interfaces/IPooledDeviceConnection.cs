using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 池化连接统一抽象，封装协议差异。
    /// </summary>
    public interface IPooledDeviceConnection
    {
        /// <summary>
        /// 连接身份标识。
        /// </summary>
        ConnectionIdentity Identity { get; }

        /// <summary>
        /// 当前底层连接状态（主要用于诊断，不表达池级租约状态）。
        /// </summary>
        ConnectionEntryState State { get; }

        /// <summary>
        /// 最近一次活跃时间（UTC）。
        /// </summary>
        DateTime LastActiveTimeUtc { get; }

        /// <summary>
        /// 底层设备客户端实例。
        /// </summary>
        IDeviceClient Client { get; }

        /// <summary>
        /// 确保连接可用。
        /// </summary>
        OperationResult EnsureConnected();

        /// <summary>
        /// 异步确保连接可用。
        /// </summary>
        Task<OperationResult> EnsureConnectedAsync();

        /// <summary>
        /// 将连接标记为失效，不再允许新租约。
        /// </summary>
        OperationResult Invalidate(string reason);

        /// <summary>
        /// 断开连接并释放底层资源。
        /// </summary>
        OperationResult Disconnect();

        /// <summary>
        /// 异步执行读写委托并刷新活跃时间。
        /// </summary>
        Task<OperationResult<T>> ExecuteAsync<T>(Func<IDeviceClient, Task<OperationResult<T>>> action);

        /// <summary>
        /// 异步执行无返回值读写委托并刷新活跃时间。
        /// </summary>
        Task<OperationResult> ExecuteAsync(Func<IDeviceClient, Task<OperationResult>> action);
    }
}

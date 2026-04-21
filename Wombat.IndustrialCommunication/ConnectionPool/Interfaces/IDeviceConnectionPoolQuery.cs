using System.Collections.Generic;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 连接池查询接口。
    /// </summary>
    public interface IDeviceConnectionPoolQuery
    {
        /// <summary>
        /// 当前连接池配置。
        /// </summary>
        ConnectionPoolOptions Options { get; }

        /// <summary>
        /// 获取连接条目状态快照。
        /// </summary>
        OperationResult<IDictionary<ConnectionIdentity, ConnectionEntryState>> GetStates();

        /// <summary>
        /// 获取指定连接的详细快照。
        /// </summary>
        OperationResult<ConnectionEntrySnapshot> GetState(ConnectionIdentity identity);

        /// <summary>
        /// 获取所有连接条目的详细快照。
        /// </summary>
        OperationResult<IList<ConnectionEntrySnapshot>> GetEntrySnapshots();

        /// <summary>
        /// 获取连接池整体快照。
        /// </summary>
        OperationResult<ConnectionPoolSnapshot> GetPoolSnapshot();
    }
}

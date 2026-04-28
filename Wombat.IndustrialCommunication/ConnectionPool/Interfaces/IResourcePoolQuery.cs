using System.Collections.Generic;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 资源池查询接口。
    /// </summary>
    public interface IResourcePoolQuery
    {
        ConnectionPoolOptions Options { get; }

        OperationResult<IDictionary<ConnectionIdentity, ConnectionEntryState>> GetStates();

        OperationResult<ConnectionEntrySnapshot> GetState(ConnectionIdentity identity);

        OperationResult<IList<ConnectionEntrySnapshot>> GetEntrySnapshots();

        OperationResult<ConnectionPoolSnapshot> GetPoolSnapshot();
    }
}

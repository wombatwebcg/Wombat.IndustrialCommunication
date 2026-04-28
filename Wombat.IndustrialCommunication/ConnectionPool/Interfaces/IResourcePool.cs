using System;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 统一资源池接口。
    /// </summary>
    public interface IResourcePool<TResource> :
        IResourcePoolQuery,
        IResourcePoolExecution<TResource>,
        IResourcePoolControl,
        IResourcePoolEvents,
        IDisposable
    {
    }
}

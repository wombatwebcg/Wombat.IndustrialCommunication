using System;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 设备连接池统一接口。
    /// </summary>
    public interface IDeviceConnectionPool :
        IDeviceConnectionPoolEvents,
        IDeviceConnectionPoolQuery,
        IDeviceConnectionPoolControl,
        IDeviceConnectionPoolExecution,
        System.IDisposable
    {
    }
}

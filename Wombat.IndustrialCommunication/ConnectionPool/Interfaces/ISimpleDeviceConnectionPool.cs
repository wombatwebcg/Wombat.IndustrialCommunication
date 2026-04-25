using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 面向默认调用场景的简化连接池接口。
    /// </summary>
    public interface ISimpleDeviceConnectionPool :
        IDeviceConnectionPoolQuery,
        IDeviceConnectionPoolExecution,
        System.IDisposable
    {
        /// <summary>
        /// 注册设备连接描述。
        /// </summary>
        OperationResult Register(DeviceConnectionDescriptor descriptor);
    }
}

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 设备服务端资源池接口。
    /// </summary>
    public interface IDeviceServerPool : IResourcePool<IDeviceServer>, IDeviceServerLifecycleExecution
    {
    }
}

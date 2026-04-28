namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 设备客户端资源池接口。
    /// </summary>
    public interface IDeviceClientPool : IResourcePool<IDeviceClient>, IDeviceClientPointExecution
    {
    }
}

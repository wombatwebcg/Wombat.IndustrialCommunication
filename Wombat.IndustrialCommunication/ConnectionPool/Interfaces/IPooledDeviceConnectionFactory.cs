using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 创建池化连接实例的工厂接口。
    /// </summary>
    public interface IPooledDeviceConnectionFactory
    {
        /// <summary>
        /// 根据连接描述创建池化连接。
        /// </summary>
        OperationResult<IPooledDeviceConnection> Create(DeviceConnectionDescriptor descriptor);

        /// <summary>
        /// 异步根据连接描述创建池化连接。
        /// </summary>
        Task<OperationResult<IPooledDeviceConnection>> CreateAsync(DeviceConnectionDescriptor descriptor);
    }
}

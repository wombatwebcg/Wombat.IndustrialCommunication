using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Models;

namespace Wombat.IndustrialCommunication.ConnectionPool.Interfaces
{
    /// <summary>
    /// 创建池化资源实例的工厂接口。
    /// </summary>
    public interface IPooledResourceConnectionFactory<TResource>
    {
        OperationResult<IPooledResourceConnection<TResource>> Create(ResourceDescriptor descriptor);

        Task<OperationResult<IPooledResourceConnection<TResource>>> CreateAsync(ResourceDescriptor descriptor);
    }
}

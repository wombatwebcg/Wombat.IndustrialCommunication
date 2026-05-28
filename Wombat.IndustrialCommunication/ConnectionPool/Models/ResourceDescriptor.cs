namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 描述池化资源创建所需的基础信息。
    /// </summary>
    public class ResourceDescriptor
    {
        public ConnectionIdentity Identity { get; set; }

        public ResourceRole ResourceRole { get; set; }

        public string ConnectionType { get; set; }

        public DeviceConnectionType DeviceConnectionType { get; set; }

        public IConnectionPoolParameters ConnectionParameters { get; set; }

        public ResourceDescriptor()
        {
            Identity = new ConnectionIdentity();
            ResourceRole = ResourceRole.Unknown;
            ConnectionType = string.Empty;
            DeviceConnectionType = DeviceConnectionType.Unknown;
        }
    }
}

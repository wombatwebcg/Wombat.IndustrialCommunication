using System;
using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 描述设备连接创建所需的基础信息。
    /// </summary>
    public class DeviceConnectionDescriptor
    {
        /// <summary>
        /// 连接池唯一键。
        /// </summary>
        public ConnectionIdentity Identity { get; set; }

        /// <summary>
        /// 连接实现类型标识，可用于工厂路由。
        /// </summary>
        public string ConnectionType { get; set; }

        /// <summary>
        /// 标准化连接类型。
        /// </summary>
        public DeviceConnectionType DeviceConnectionType { get; set; }

        /// <summary>
        /// 连接参数（地址、端口、站号、超时等）。
        /// </summary>
        public IDictionary<string, object> Parameters { get; set; }

        public DeviceConnectionDescriptor()
        {
            Identity = new ConnectionIdentity();
            ConnectionType = string.Empty;
            DeviceConnectionType = DeviceConnectionType.Unknown;
            Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.ConnectionPool.Wrappers;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.ConnectionPool.Factories
{
    /// <summary>
    /// 默认池化设备服务端连接工厂。
    /// </summary>
    public class DefaultPooledDeviceServerConnectionFactory : IPooledResourceConnectionFactory<IDeviceServer>
    {
        public OperationResult<IPooledResourceConnection<IDeviceServer>> Create(ResourceDescriptor descriptor)
        {
            try
            {
                if (descriptor == null)
                {
                    return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("连接描述不能为空");
                }

                if (descriptor.ResourceRole != ResourceRole.Unknown && descriptor.ResourceRole != ResourceRole.Server)
                {
                    return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("资源角色不是服务端");
                }

                if (descriptor.Identity == null)
                {
                    return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("连接标识不能为空");
                }

                if (string.IsNullOrWhiteSpace(descriptor.Identity.DeviceId)
                    || string.IsNullOrWhiteSpace(descriptor.Identity.ProtocolType)
                    || string.IsNullOrWhiteSpace(descriptor.Identity.Endpoint))
                {
                    return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("连接标识缺少必要字段");
                }

                var type = ResolveConnectionType(descriptor);
                switch (type)
                {
                    case DeviceConnectionType.ModbusTcp:
                        return CreateModbusTcpServer(descriptor);
                    case DeviceConnectionType.ModbusRtu:
                        return CreateModbusRtuServer(descriptor);
                    case DeviceConnectionType.SiemensS7:
                        return CreateS7TcpServer(descriptor);
                    default:
                        return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("不支持的服务端类型");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(ex);
            }
        }

        public Task<OperationResult<IPooledResourceConnection<IDeviceServer>>> CreateAsync(ResourceDescriptor descriptor)
        {
            return Task.FromResult(Create(descriptor));
        }

        private static DeviceConnectionType ResolveConnectionType(ResourceDescriptor descriptor)
        {
            if (descriptor.DeviceConnectionType != DeviceConnectionType.Unknown)
            {
                return descriptor.DeviceConnectionType;
            }

            if (string.IsNullOrWhiteSpace(descriptor.ConnectionType))
            {
                return DeviceConnectionType.Unknown;
            }

            DeviceConnectionType parsed;
            if (Enum.TryParse(descriptor.ConnectionType, true, out parsed))
            {
                return parsed;
            }

            return DeviceConnectionType.Unknown;
        }

        private static OperationResult<IPooledResourceConnection<IDeviceServer>> CreateModbusTcpServer(ResourceDescriptor descriptor)
        {
            var parameters = descriptor.Parameters ?? new Dictionary<string, object>();
            var ip = GetRequiredString(parameters, "ip");
            if (string.IsNullOrWhiteSpace(ip))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("ModbusTcpServer 参数缺少 ip");
            }

            var port = GetInt(parameters, "port", 502);
            var server = new ModbusTcpServer(ip, port);
            ApplyCommonServerOptions(parameters, server);
            return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceServer>>(
                new ModbusTcpServerPooledConnection(descriptor.Identity, server));
        }

        private static OperationResult<IPooledResourceConnection<IDeviceServer>> CreateModbusRtuServer(ResourceDescriptor descriptor)
        {
            var parameters = descriptor.Parameters ?? new Dictionary<string, object>();
            var portName = GetRequiredString(parameters, "portName");
            if (string.IsNullOrWhiteSpace(portName))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("ModbusRtuServer 参数缺少 portName");
            }

            var baudRate = GetInt(parameters, "baudRate", 9600);
            var dataBits = GetInt(parameters, "dataBits", 8);
            var stopBits = GetEnum(parameters, "stopBits", StopBits.One);
            var parity = GetEnum(parameters, "parity", Parity.None);
            var handshake = GetEnum(parameters, "handshake", Handshake.None);
            var server = new ModbusRtuServer(portName, baudRate, dataBits, stopBits, parity, handshake);
            ApplyCommonServerOptions(parameters, server);
            return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceServer>>(
                new ModbusRtuServerPooledConnection(descriptor.Identity, server));
        }

        private static OperationResult<IPooledResourceConnection<IDeviceServer>> CreateS7TcpServer(ResourceDescriptor descriptor)
        {
            var parameters = descriptor.Parameters ?? new Dictionary<string, object>();
            var ip = GetRequiredString(parameters, "ip");
            if (string.IsNullOrWhiteSpace(ip))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("S7TcpServer 参数缺少 ip");
            }

            var port = GetInt(parameters, "port", 102);
            var server = new S7TcpServer(ip, port);
            ApplyCommonServerOptions(parameters, server);
            return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceServer>>(
                new S7TcpServerPooledConnection(descriptor.Identity, server));
        }

        private static void ApplyCommonServerOptions(IDictionary<string, object> parameters, IDeviceServer server)
        {
            if (server == null || parameters == null)
            {
                return;
            }

            if (server is ModbusTcpServer modbusTcpServer)
            {
                ApplyTcpLikeServerOptions(parameters, modbusTcpServer);
                return;
            }

            if (server is S7TcpServer s7Server)
            {
                ApplyTcpLikeServerOptions(parameters, s7Server);
                return;
            }

            if (server is ModbusRtuServer modbusRtuServer)
            {
                ApplyRtuServerOptions(parameters, modbusRtuServer);
            }
        }

        private static void ApplyTcpLikeServerOptions(IDictionary<string, object> parameters, ModbusTcpServer server)
        {
            if (parameters.ContainsKey("connectTimeoutMilliseconds"))
            {
                server.ConnectTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "connectTimeoutMilliseconds", (int)server.ConnectTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("receiveTimeoutMilliseconds"))
            {
                server.ReceiveTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "receiveTimeoutMilliseconds", (int)server.ReceiveTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("sendTimeoutMilliseconds"))
            {
                server.SendTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "sendTimeoutMilliseconds", (int)server.SendTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("maxConnections"))
            {
                server.MaxConnections = GetInt(parameters, "maxConnections", server.MaxConnections);
            }
        }

        private static void ApplyTcpLikeServerOptions(IDictionary<string, object> parameters, S7TcpServer server)
        {
            if (parameters.ContainsKey("connectTimeoutMilliseconds"))
            {
                server.ConnectTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "connectTimeoutMilliseconds", (int)server.ConnectTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("receiveTimeoutMilliseconds"))
            {
                server.ReceiveTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "receiveTimeoutMilliseconds", (int)server.ReceiveTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("sendTimeoutMilliseconds"))
            {
                server.SendTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "sendTimeoutMilliseconds", (int)server.SendTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("maxConnections"))
            {
                server.MaxConnections = GetInt(parameters, "maxConnections", server.MaxConnections);
            }
        }

        private static void ApplyRtuServerOptions(IDictionary<string, object> parameters, ModbusRtuServer server)
        {
            if (parameters.ContainsKey("connectTimeoutMilliseconds"))
            {
                server.ConnectTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "connectTimeoutMilliseconds", (int)server.ConnectTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("receiveTimeoutMilliseconds"))
            {
                server.ReceiveTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "receiveTimeoutMilliseconds", (int)server.ReceiveTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("sendTimeoutMilliseconds"))
            {
                server.SendTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "sendTimeoutMilliseconds", (int)server.SendTimeout.TotalMilliseconds));
            }
        }

        private static string GetRequiredString(IDictionary<string, object> parameters, string key)
        {
            object value;
            if (!parameters.TryGetValue(key, out value) || value == null)
            {
                return null;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(IDictionary<string, object> parameters, string key, int defaultValue)
        {
            object value;
            if (!parameters.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            int parsed;
            if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        private static TEnum GetEnum<TEnum>(IDictionary<string, object> parameters, string key, TEnum defaultValue) where TEnum : struct
        {
            object value;
            if (!parameters.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            var raw = Convert.ToString(value, CultureInfo.InvariantCulture);
            TEnum parsed;
            if (Enum.TryParse(raw, true, out parsed))
            {
                return parsed;
            }

            return defaultValue;
        }
    }
}

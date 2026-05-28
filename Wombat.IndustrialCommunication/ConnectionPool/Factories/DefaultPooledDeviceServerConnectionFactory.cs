using System;
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
            return descriptor == null ? DeviceConnectionType.Unknown : descriptor.DeviceConnectionType;
        }

        private static OperationResult<IPooledResourceConnection<IDeviceServer>> CreateModbusTcpServer(ResourceDescriptor descriptor)
        {
            ModbusTcpServerConnectionParameters parameters;
            OperationResult<IPooledResourceConnection<IDeviceServer>> failure;
            if (!TryGetConnectionParameters(descriptor, "ModbusTcpServer", out parameters, out failure))
            {
                return failure;
            }

            if (string.IsNullOrWhiteSpace(parameters.Ip))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("ModbusTcpServer 参数缺少 Ip");
            }

            var validation = ValidateCommonServerParameters(parameters, "ModbusTcpServer");
            if (!validation.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(validation);
            }

            int port;
            string validationMessage;
            if (!TryResolvePort(parameters.Port, 502, "ModbusTcpServer.Port", out port, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(validationMessage);
            }

            int maxConnections;
            if (!TryResolveOptionalPositiveInt(parameters.MaxConnections, "ModbusTcpServer.MaxConnections", out maxConnections, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(validationMessage);
            }

            var server = new ModbusTcpServer(parameters.Ip, port);
            ApplyCommonServerOptions(parameters, server, descriptor.Identity.DeviceId);
            return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceServer>>(
                new ModbusTcpServerPooledConnection(descriptor.Identity, server));
        }

        private static OperationResult<IPooledResourceConnection<IDeviceServer>> CreateModbusRtuServer(ResourceDescriptor descriptor)
        {
            ModbusRtuServerConnectionParameters parameters;
            OperationResult<IPooledResourceConnection<IDeviceServer>> failure;
            if (!TryGetConnectionParameters(descriptor, "ModbusRtuServer", out parameters, out failure))
            {
                return failure;
            }

            if (string.IsNullOrWhiteSpace(parameters.PortName))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("ModbusRtuServer 参数缺少 PortName");
            }

            var validation = ValidateCommonServerParameters(parameters, "ModbusRtuServer");
            if (!validation.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(validation);
            }

            string validationMessage;
            int baudRate;
            if (!TryResolvePositiveInt(parameters.BaudRate, 9600, "ModbusRtuServer.BaudRate", out baudRate, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(validationMessage);
            }

            int dataBits;
            if (!TryResolvePositiveInt(parameters.DataBits, 8, "ModbusRtuServer.DataBits", out dataBits, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(validationMessage);
            }

            var server = new ModbusRtuServer(
                parameters.PortName,
                baudRate,
                dataBits,
                parameters.StopBits ?? StopBits.One,
                parameters.Parity ?? Parity.None,
                parameters.Handshake ?? Handshake.None);
            ApplyCommonServerOptions(parameters, server, descriptor.Identity.DeviceId);
            return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceServer>>(
                new ModbusRtuServerPooledConnection(descriptor.Identity, server));
        }

        private static OperationResult<IPooledResourceConnection<IDeviceServer>> CreateS7TcpServer(ResourceDescriptor descriptor)
        {
            SiemensS7ServerConnectionParameters parameters;
            OperationResult<IPooledResourceConnection<IDeviceServer>> failure;
            if (!TryGetConnectionParameters(descriptor, "S7TcpServer", out parameters, out failure))
            {
                return failure;
            }

            if (string.IsNullOrWhiteSpace(parameters.Ip))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>("S7TcpServer 参数缺少 Ip");
            }

            var validation = ValidateCommonServerParameters(parameters, "S7TcpServer");
            if (!validation.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(validation);
            }

            int port;
            string validationMessage;
            if (!TryResolvePort(parameters.Port, 102, "S7TcpServer.Port", out port, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(validationMessage);
            }

            int maxConnections;
            if (!TryResolveOptionalPositiveInt(parameters.MaxConnections, "S7TcpServer.MaxConnections", out maxConnections, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(validationMessage);
            }

            var server = new S7TcpServer(parameters.Ip, port);
            ApplyCommonServerOptions(parameters, server, descriptor.Identity.DeviceId);
            return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceServer>>(
                new S7TcpServerPooledConnection(descriptor.Identity, server));
        }

        private static void ApplyCommonServerOptions(ServerConnectionParametersBase parameters, IDeviceServer server, string snapshotName)
        {
            if (server == null || parameters == null)
            {
                return;
            }

            if (server is ModbusTcpServer modbusTcpServer && parameters is TcpServerConnectionParametersBase tcpParameters)
            {
                ApplyTcpLikeServerOptions(tcpParameters, modbusTcpServer);
            }
            else if (server is S7TcpServer s7Server && parameters is TcpServerConnectionParametersBase s7Parameters)
            {
                ApplyTcpLikeServerOptions(s7Parameters, s7Server);
            }
            else if (server is ModbusRtuServer modbusRtuServer && parameters is ModbusRtuServerConnectionParameters rtuParameters)
            {
                ApplyRtuServerOptions(rtuParameters, modbusRtuServer);
            }

            ApplySnapshotOptions(parameters, server, snapshotName);
        }

        private static void ApplySnapshotOptions(ServerConnectionParametersBase parameters, IDeviceServer server, string snapshotName)
        {
            if (server == null || parameters == null || !parameters.EnableSnapshotPersistence.HasValue)
            {
                return;
            }

            server.ConfigureSnapshotPersistence(snapshotName);
            server.EnableSnapshotPersistence = parameters.EnableSnapshotPersistence.Value;
        }

        private static void ApplyTcpLikeServerOptions(TcpServerConnectionParametersBase parameters, ModbusTcpServer server)
        {
            if (parameters.ConnectTimeoutMilliseconds.HasValue)
            {
                server.ConnectTimeout = TimeSpan.FromMilliseconds(parameters.ConnectTimeoutMilliseconds.Value);
            }

            if (parameters.ReceiveTimeoutMilliseconds.HasValue)
            {
                server.ReceiveTimeout = TimeSpan.FromMilliseconds(parameters.ReceiveTimeoutMilliseconds.Value);
            }

            if (parameters.SendTimeoutMilliseconds.HasValue)
            {
                server.SendTimeout = TimeSpan.FromMilliseconds(parameters.SendTimeoutMilliseconds.Value);
            }

            if (parameters.MaxConnections.HasValue)
            {
                server.MaxConnections = parameters.MaxConnections.Value;
            }
        }

        private static void ApplyTcpLikeServerOptions(TcpServerConnectionParametersBase parameters, S7TcpServer server)
        {
            if (parameters.ConnectTimeoutMilliseconds.HasValue)
            {
                server.ConnectTimeout = TimeSpan.FromMilliseconds(parameters.ConnectTimeoutMilliseconds.Value);
            }

            if (parameters.ReceiveTimeoutMilliseconds.HasValue)
            {
                server.ReceiveTimeout = TimeSpan.FromMilliseconds(parameters.ReceiveTimeoutMilliseconds.Value);
            }

            if (parameters.SendTimeoutMilliseconds.HasValue)
            {
                server.SendTimeout = TimeSpan.FromMilliseconds(parameters.SendTimeoutMilliseconds.Value);
            }

            if (parameters.MaxConnections.HasValue)
            {
                server.MaxConnections = parameters.MaxConnections.Value;
            }
        }

        private static void ApplyRtuServerOptions(ModbusRtuServerConnectionParameters parameters, ModbusRtuServer server)
        {
            if (parameters.ConnectTimeoutMilliseconds.HasValue)
            {
                server.ConnectTimeout = TimeSpan.FromMilliseconds(parameters.ConnectTimeoutMilliseconds.Value);
            }

            if (parameters.ReceiveTimeoutMilliseconds.HasValue)
            {
                server.ReceiveTimeout = TimeSpan.FromMilliseconds(parameters.ReceiveTimeoutMilliseconds.Value);
            }

            if (parameters.SendTimeoutMilliseconds.HasValue)
            {
                server.SendTimeout = TimeSpan.FromMilliseconds(parameters.SendTimeoutMilliseconds.Value);
            }
        }

        private static OperationResult ValidateCommonServerParameters(ServerConnectionParametersBase parameters, string connectionName)
        {
            string validationMessage;
            if (!TryResolveNonNegativeMilliseconds(parameters.ConnectTimeoutMilliseconds, connectionName + ".ConnectTimeoutMilliseconds", out validationMessage)
                || !TryResolveNonNegativeMilliseconds(parameters.ReceiveTimeoutMilliseconds, connectionName + ".ReceiveTimeoutMilliseconds", out validationMessage)
                || !TryResolveNonNegativeMilliseconds(parameters.SendTimeoutMilliseconds, connectionName + ".SendTimeoutMilliseconds", out validationMessage))
            {
                return OperationResult.CreateFailedResult(validationMessage);
            }

            return OperationResult.CreateSuccessResult();
        }

        private static bool TryResolvePort(int? value, int defaultValue, string name, out int resolved, out string validationMessage)
        {
            return TryResolveIntInRange(value, defaultValue, 1, 65535, name, out resolved, out validationMessage);
        }

        private static bool TryResolvePositiveInt(int? value, int defaultValue, string name, out int resolved, out string validationMessage)
        {
            return TryResolveIntInRange(value, defaultValue, 1, int.MaxValue, name, out resolved, out validationMessage);
        }

        private static bool TryResolveOptionalPositiveInt(int? value, string name, out int resolved, out string validationMessage)
        {
            resolved = 0;
            validationMessage = null;
            if (!value.HasValue)
            {
                return true;
            }

            if (value.Value <= 0)
            {
                validationMessage = name + " 必须大于 0";
                return false;
            }

            resolved = value.Value;
            return true;
        }

        private static bool TryResolveIntInRange(int? value, int defaultValue, int minValue, int maxValue, string name, out int resolved, out string validationMessage)
        {
            resolved = defaultValue;
            validationMessage = null;
            if (!value.HasValue)
            {
                return true;
            }

            if (value.Value < minValue || value.Value > maxValue)
            {
                validationMessage = name + " 必须在 " + minValue + " 到 " + maxValue + " 之间";
                return false;
            }

            resolved = value.Value;
            return true;
        }

        private static bool TryResolveNonNegativeMilliseconds(int? value, string name, out string validationMessage)
        {
            validationMessage = null;
            if (!value.HasValue)
            {
                return true;
            }

            if (value.Value < 0)
            {
                validationMessage = name + " 不能小于 0";
                return false;
            }

            return true;
        }

        private static bool TryGetConnectionParameters<TParameters>(
            ResourceDescriptor descriptor,
            string connectionName,
            out TParameters parameters,
            out OperationResult<IPooledResourceConnection<IDeviceServer>> failure)
            where TParameters : class, IConnectionPoolParameters
        {
            parameters = descriptor.ConnectionParameters as TParameters;
            if (parameters != null)
            {
                failure = null;
                return true;
            }

            if (descriptor.ConnectionParameters == null)
            {
                failure = OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(connectionName + " 缺少 ConnectionParameters");
                return false;
            }

            failure = OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceServer>>(
                connectionName + " 参数类型不正确，应为 " + typeof(TParameters).Name);
            return false;
        }
    }
}

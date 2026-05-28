using System;
using System.IO.Ports;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.ConnectionPool.Interfaces;
using Wombat.IndustrialCommunication.ConnectionPool.Models;
using Wombat.IndustrialCommunication.ConnectionPool.Wrappers;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.ConnectionPool.Factories
{
    /// <summary>
    /// 默认池化设备客户端连接工厂。
    /// </summary>
    public class DefaultPooledDeviceClientConnectionFactory : IPooledResourceConnectionFactory<IDeviceClient>
    {
        public OperationResult<IPooledResourceConnection<IDeviceClient>> Create(ResourceDescriptor descriptor)
        {
            try
            {
                if (descriptor == null)
                {
                    return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>("连接描述不能为空");
                }

                if (descriptor.ResourceRole != ResourceRole.Unknown && descriptor.ResourceRole != ResourceRole.Client)
                {
                    return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>("资源角色不是客户端");
                }

                if (descriptor.Identity == null)
                {
                    return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>("连接标识不能为空");
                }

                if (string.IsNullOrWhiteSpace(descriptor.Identity.DeviceId)
                    || string.IsNullOrWhiteSpace(descriptor.Identity.ProtocolType)
                    || string.IsNullOrWhiteSpace(descriptor.Identity.Endpoint))
                {
                    return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>("连接标识缺少必要字段");
                }

                var type = ResolveConnectionType(descriptor);
                switch (type)
                {
                    case DeviceConnectionType.ModbusTcp:
                        return CreateModbusTcp(descriptor);
                    case DeviceConnectionType.ModbusRtu:
                        return CreateModbusRtu(descriptor);
                    case DeviceConnectionType.SiemensS7:
                        return CreateSiemens(descriptor);
                    case DeviceConnectionType.Fins:
                        return CreateFins(descriptor);
                    default:
                        return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>("不支持的连接类型");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(ex);
            }
        }

        public Task<OperationResult<IPooledResourceConnection<IDeviceClient>>> CreateAsync(ResourceDescriptor descriptor)
        {
            return Task.FromResult(Create(descriptor));
        }

        private static DeviceConnectionType ResolveConnectionType(ResourceDescriptor descriptor)
        {
            return descriptor == null ? DeviceConnectionType.Unknown : descriptor.DeviceConnectionType;
        }

        private static OperationResult<IPooledResourceConnection<IDeviceClient>> CreateModbusTcp(ResourceDescriptor descriptor)
        {
            ModbusTcpClientConnectionParameters parameters;
            OperationResult<IPooledResourceConnection<IDeviceClient>> failure;
            if (!TryGetConnectionParameters(descriptor, "ModbusTcp", out parameters, out failure))
            {
                return failure;
            }

            if (string.IsNullOrWhiteSpace(parameters.Ip))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>("ModbusTcp 参数缺少 Ip");
            }

            var commonValidation = ValidateCommonClientParameters(parameters, "ModbusTcp");
            if (!commonValidation.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(commonValidation);
            }

            int port;
            string validationMessage;
            if (!TryResolvePort(parameters.Port, 502, "ModbusTcp.Port", out port, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(validationMessage);
            }

            if (!TryResolveNonNegativeMilliseconds(parameters.BatchReadStationIntervalMilliseconds, "ModbusTcp.BatchReadStationIntervalMilliseconds", out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(validationMessage);
            }

            var client = new ModbusTcpClient(parameters.Ip, port);
            ApplyCommonOptions(parameters, client);
            ApplyModbusBatchReadOptions(parameters, client);
            return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceClient>>(new ModbusTcpPooledConnection(
                descriptor.Identity,
                client,
                parameters.ProbeAddress,
                parameters.ProbeDataType ?? DataTypeEnums.UInt16,
                parameters.ProbeLength ?? 1));
        }

        private static OperationResult<IPooledResourceConnection<IDeviceClient>> CreateModbusRtu(ResourceDescriptor descriptor)
        {
            ModbusRtuClientConnectionParameters parameters;
            OperationResult<IPooledResourceConnection<IDeviceClient>> failure;
            if (!TryGetConnectionParameters(descriptor, "ModbusRtu", out parameters, out failure))
            {
                return failure;
            }

            if (string.IsNullOrWhiteSpace(parameters.PortName))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>("ModbusRtu 参数缺少 PortName");
            }

            var commonValidation = ValidateCommonClientParameters(parameters, "ModbusRtu");
            if (!commonValidation.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(commonValidation);
            }

            string validationMessage;
            int baudRate;
            if (!TryResolvePositiveInt(parameters.BaudRate, 9600, "ModbusRtu.BaudRate", out baudRate, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(validationMessage);
            }

            int dataBits;
            if (!TryResolvePositiveInt(parameters.DataBits, 8, "ModbusRtu.DataBits", out dataBits, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(validationMessage);
            }

            if (!TryResolveNonNegativeMilliseconds(parameters.BatchReadStationIntervalMilliseconds, "ModbusRtu.BatchReadStationIntervalMilliseconds", out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(validationMessage);
            }

            var client = new ModbusRtuClient(
                parameters.PortName,
                baudRate,
                dataBits,
                parameters.StopBits ?? StopBits.One,
                parameters.Parity ?? Parity.None,
                parameters.Handshake ?? Handshake.None);
            ApplyCommonOptions(parameters, client);
            ApplyModbusBatchReadOptions(parameters, client);
            return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceClient>>(new ModbusRtuPooledConnection(
                descriptor.Identity,
                client,
                parameters.ProbeAddress,
                parameters.ProbeDataType ?? DataTypeEnums.UInt16,
                parameters.ProbeLength ?? 1));
        }

        private static OperationResult<IPooledResourceConnection<IDeviceClient>> CreateSiemens(ResourceDescriptor descriptor)
        {
            SiemensS7ClientConnectionParameters parameters;
            OperationResult<IPooledResourceConnection<IDeviceClient>> failure;
            if (!TryGetConnectionParameters(descriptor, "SiemensS7", out parameters, out failure))
            {
                return failure;
            }

            if (string.IsNullOrWhiteSpace(parameters.Ip))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>("SiemensS7 参数缺少 Ip");
            }

            var commonValidation = ValidateCommonClientParameters(parameters, "SiemensS7");
            if (!commonValidation.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(commonValidation);
            }

            int port;
            string validationMessage;
            if (!TryResolvePort(parameters.Port, 102, "SiemensS7.Port", out port, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(validationMessage);
            }

            if (!TryResolveNonNegativeMilliseconds(parameters.BatchReadStationIntervalMilliseconds, "SiemensS7.BatchReadStationIntervalMilliseconds", out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(validationMessage);
            }

            var client = new SiemensClient(
                parameters.Ip,
                port,
                parameters.SiemensVersion ?? SiemensVersion.S7_1200,
                parameters.Slot ?? 0,
                parameters.Rack ?? 0);
            ApplyCommonOptions(parameters, client);
            ApplySiemensBatchReadOptions(parameters, client);
            return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceClient>>(new SiemensPooledConnection(
                descriptor.Identity,
                client,
                parameters.ProbeAddress,
                parameters.ProbeDataType ?? DataTypeEnums.UInt16,
                parameters.ProbeLength ?? 1));
        }

        private static OperationResult<IPooledResourceConnection<IDeviceClient>> CreateFins(ResourceDescriptor descriptor)
        {
            FinsClientConnectionParameters parameters;
            OperationResult<IPooledResourceConnection<IDeviceClient>> failure;
            if (!TryGetConnectionParameters(descriptor, "Fins", out parameters, out failure))
            {
                return failure;
            }

            if (string.IsNullOrWhiteSpace(parameters.Ip))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>("Fins 参数缺少 Ip");
            }

            var commonValidation = ValidateCommonClientParameters(parameters, "Fins");
            if (!commonValidation.IsSuccess)
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(commonValidation);
            }

            int port;
            string validationMessage;
            if (!TryResolvePort(parameters.Port, 9600, "Fins.Port", out port, out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(validationMessage);
            }

            if (!TryResolveNonNegativeMilliseconds(parameters.TimeoutMilliseconds, "Fins.TimeoutMilliseconds", out validationMessage))
            {
                return OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(validationMessage);
            }

            TimeSpan? timeout = parameters.TimeoutMilliseconds.HasValue && parameters.TimeoutMilliseconds.Value > 0
                ? TimeSpan.FromMilliseconds(parameters.TimeoutMilliseconds.Value)
                : (TimeSpan?)null;
            var client = new FinsClient(parameters.Ip, port, timeout);
            ApplyCommonOptions(parameters, client);
            return OperationResult.CreateSuccessResult<IPooledResourceConnection<IDeviceClient>>(new FinsPooledConnection(
                descriptor.Identity,
                client,
                parameters.ProbeAddress,
                parameters.ProbeDataType ?? DataTypeEnums.UInt16,
                parameters.ProbeLength ?? 1));
        }

        private static void ApplyCommonOptions(ClientConnectionParametersBase parameters, IClientConfiguration client)
        {
            if (client == null || parameters == null)
            {
                return;
            }

            if (parameters.ConnectTimeoutMilliseconds.HasValue)
            {
                client.ConnectTimeout = TimeSpan.FromMilliseconds(parameters.ConnectTimeoutMilliseconds.Value);
            }

            if (parameters.ReceiveTimeoutMilliseconds.HasValue)
            {
                client.ReceiveTimeout = TimeSpan.FromMilliseconds(parameters.ReceiveTimeoutMilliseconds.Value);
            }

            if (parameters.SendTimeoutMilliseconds.HasValue)
            {
                client.SendTimeout = TimeSpan.FromMilliseconds(parameters.SendTimeoutMilliseconds.Value);
            }

            if (parameters.Retries.HasValue)
            {
                client.Retries = parameters.Retries.Value;
            }
        }

        private static void ApplyModbusBatchReadOptions(ModbusTcpClientConnectionParameters parameters, ModbusTcpClient client)
        {
            if (client == null || parameters == null)
            {
                return;
            }

            if (parameters.BatchReadStationIntervalMilliseconds.HasValue)
            {
                client.BatchReadStationInterval = TimeSpan.FromMilliseconds(parameters.BatchReadStationIntervalMilliseconds.Value);
            }
        }

        private static void ApplyModbusBatchReadOptions(ModbusRtuClientConnectionParameters parameters, ModbusRtuClient client)
        {
            if (client == null || parameters == null)
            {
                return;
            }

            if (parameters.BatchReadStationIntervalMilliseconds.HasValue)
            {
                client.BatchReadStationInterval = TimeSpan.FromMilliseconds(parameters.BatchReadStationIntervalMilliseconds.Value);
            }
        }

        private static void ApplySiemensBatchReadOptions(SiemensS7ClientConnectionParameters parameters, SiemensClient client)
        {
            if (client == null || parameters == null)
            {
                return;
            }

            if (parameters.BatchReadStationIntervalMilliseconds.HasValue)
            {
                client.BatchReadStationInterval = TimeSpan.FromMilliseconds(parameters.BatchReadStationIntervalMilliseconds.Value);
            }
        }

        private static OperationResult ValidateCommonClientParameters(ClientConnectionParametersBase parameters, string connectionName)
        {
            string validationMessage;
            if (!TryResolveNonNegativeMilliseconds(parameters.ConnectTimeoutMilliseconds, connectionName + ".ConnectTimeoutMilliseconds", out validationMessage)
                || !TryResolveNonNegativeMilliseconds(parameters.ReceiveTimeoutMilliseconds, connectionName + ".ReceiveTimeoutMilliseconds", out validationMessage)
                || !TryResolveNonNegativeMilliseconds(parameters.SendTimeoutMilliseconds, connectionName + ".SendTimeoutMilliseconds", out validationMessage))
            {
                return OperationResult.CreateFailedResult(validationMessage);
            }

            if (parameters.Retries.HasValue && parameters.Retries.Value < 0)
            {
                return OperationResult.CreateFailedResult(connectionName + ".Retries 不能小于 0");
            }

            if (parameters.ProbeLength.HasValue && parameters.ProbeLength.Value <= 0)
            {
                return OperationResult.CreateFailedResult(connectionName + ".ProbeLength 必须大于 0");
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
            out OperationResult<IPooledResourceConnection<IDeviceClient>> failure)
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
                failure = OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(connectionName + " 缺少 ConnectionParameters");
                return false;
            }

            failure = OperationResult.CreateFailedResult<IPooledResourceConnection<IDeviceClient>>(
                connectionName + " 参数类型不正确，应为 " + typeof(TParameters).Name);
            return false;
        }
    }
}

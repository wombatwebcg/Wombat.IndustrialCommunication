using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// 默认池化连接工厂。
    /// </summary>
    public class DefaultPooledDeviceConnectionFactory : IPooledDeviceConnectionFactory
    {
        public OperationResult<IPooledDeviceConnection> Create(DeviceConnectionDescriptor descriptor)
        {
            try
            {
                if (descriptor == null)
                {
                    return OperationResult.CreateFailedResult<IPooledDeviceConnection>("连接描述不能为空");
                }

                if (descriptor.Identity == null)
                {
                    return OperationResult.CreateFailedResult<IPooledDeviceConnection>("连接标识不能为空");
                }

                if (string.IsNullOrWhiteSpace(descriptor.Identity.DeviceId)
                    || string.IsNullOrWhiteSpace(descriptor.Identity.ProtocolType)
                    || string.IsNullOrWhiteSpace(descriptor.Identity.Endpoint))
                {
                    return OperationResult.CreateFailedResult<IPooledDeviceConnection>("连接标识缺少必要字段");
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
                        return OperationResult.CreateFailedResult<IPooledDeviceConnection>("不支持的连接类型");
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult<IPooledDeviceConnection>(ex);
            }
        }

        public Task<OperationResult<IPooledDeviceConnection>> CreateAsync(DeviceConnectionDescriptor descriptor)
        {
            return Task.FromResult(Create(descriptor));
        }

        private static DeviceConnectionType ResolveConnectionType(DeviceConnectionDescriptor descriptor)
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

        private static OperationResult<IPooledDeviceConnection> CreateModbusTcp(DeviceConnectionDescriptor descriptor)
        {
            var parameters = descriptor.Parameters ?? new Dictionary<string, object>();
            var ip = GetRequiredString(parameters, "ip");
            if (string.IsNullOrWhiteSpace(ip))
            {
                return OperationResult.CreateFailedResult<IPooledDeviceConnection>("ModbusTcp 参数缺少 ip");
            }

            var port = GetInt(parameters, "port", 502);
            var client = new ModbusTcpClient(ip, port);
            ApplyCommonOptions(parameters, client);
            return OperationResult.CreateSuccessResult<IPooledDeviceConnection>(new ModbusTcpPooledConnection(
                descriptor.Identity,
                client,
                GetOptionalString(parameters, "probeAddress"),
                GetEnum(parameters, "probeDataType", DataTypeEnums.UInt16),
                GetInt(parameters, "probeLength", 1)));
        }

        private static OperationResult<IPooledDeviceConnection> CreateModbusRtu(DeviceConnectionDescriptor descriptor)
        {
            var parameters = descriptor.Parameters ?? new Dictionary<string, object>();
            var portName = GetRequiredString(parameters, "portName");
            if (string.IsNullOrWhiteSpace(portName))
            {
                return OperationResult.CreateFailedResult<IPooledDeviceConnection>("ModbusRtu 参数缺少 portName");
            }

            var baudRate = GetInt(parameters, "baudRate", 9600);
            var dataBits = GetInt(parameters, "dataBits", 8);
            var stopBits = GetEnum(parameters, "stopBits", StopBits.One);
            var parity = GetEnum(parameters, "parity", Parity.None);
            var handshake = GetEnum(parameters, "handshake", Handshake.None);
            var client = new ModbusRtuClient(portName, baudRate, dataBits, stopBits, parity, handshake);
            ApplyCommonOptions(parameters, client);
            return OperationResult.CreateSuccessResult<IPooledDeviceConnection>(new ModbusRtuPooledConnection(
                descriptor.Identity,
                client,
                GetOptionalString(parameters, "probeAddress"),
                GetEnum(parameters, "probeDataType", DataTypeEnums.UInt16),
                GetInt(parameters, "probeLength", 1)));
        }

        private static OperationResult<IPooledDeviceConnection> CreateSiemens(DeviceConnectionDescriptor descriptor)
        {
            var parameters = descriptor.Parameters ?? new Dictionary<string, object>();
            var ip = GetRequiredString(parameters, "ip");
            if (string.IsNullOrWhiteSpace(ip))
            {
                return OperationResult.CreateFailedResult<IPooledDeviceConnection>("Siemens 参数缺少 ip");
            }

            var port = GetInt(parameters, "port", 102);
            var version = GetEnum(parameters, "siemensVersion", SiemensVersion.S7_1200);
            var slot = (byte)GetInt(parameters, "slot", 0);
            var rack = (byte)GetInt(parameters, "rack", 0);
            var client = new SiemensClient(ip, port, version, slot, rack);
            ApplyCommonOptions(parameters, client);
            return OperationResult.CreateSuccessResult<IPooledDeviceConnection>(new SiemensPooledConnection(
                descriptor.Identity,
                client,
                GetOptionalString(parameters, "probeAddress"),
                GetEnum(parameters, "probeDataType", DataTypeEnums.UInt16),
                GetInt(parameters, "probeLength", 1)));
        }

        private static OperationResult<IPooledDeviceConnection> CreateFins(DeviceConnectionDescriptor descriptor)
        {
            var parameters = descriptor.Parameters ?? new Dictionary<string, object>();
            var ip = GetRequiredString(parameters, "ip");
            if (string.IsNullOrWhiteSpace(ip))
            {
                return OperationResult.CreateFailedResult<IPooledDeviceConnection>("Fins 参数缺少 ip");
            }

            var port = GetInt(parameters, "port", 9600);
            var timeoutMilliseconds = GetInt(parameters, "timeoutMilliseconds", 0);
            TimeSpan? timeout = timeoutMilliseconds > 0 ? TimeSpan.FromMilliseconds(timeoutMilliseconds) : (TimeSpan?)null;
            var client = new FinsClient(ip, port, timeout);
            ApplyCommonOptions(parameters, client);
            return OperationResult.CreateSuccessResult<IPooledDeviceConnection>(new FinsPooledConnection(
                descriptor.Identity,
                client,
                GetOptionalString(parameters, "probeAddress"),
                GetEnum(parameters, "probeDataType", DataTypeEnums.UInt16),
                GetInt(parameters, "probeLength", 1)));
        }

        private static void ApplyCommonOptions(IDictionary<string, object> parameters, IClientConfiguration client)
        {
            if (client == null || parameters == null)
            {
                return;
            }

            if (parameters.ContainsKey("connectTimeoutMilliseconds"))
            {
                client.ConnectTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "connectTimeoutMilliseconds", (int)client.ConnectTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("receiveTimeoutMilliseconds"))
            {
                client.ReceiveTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "receiveTimeoutMilliseconds", (int)client.ReceiveTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("sendTimeoutMilliseconds"))
            {
                client.SendTimeout = TimeSpan.FromMilliseconds(GetInt(parameters, "sendTimeoutMilliseconds", (int)client.SendTimeout.TotalMilliseconds));
            }

            if (parameters.ContainsKey("retries"))
            {
                client.Retries = GetInt(parameters, "retries", client.Retries);
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

        private static string GetOptionalString(IDictionary<string, object> parameters, string key)
        {
            return GetRequiredString(parameters, key);
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

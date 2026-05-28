using System.IO.Ports;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    public interface IConnectionPoolParameters
    {
    }

    public abstract class ClientConnectionParametersBase : IConnectionPoolParameters
    {
        public int? ConnectTimeoutMilliseconds { get; set; }

        public int? ReceiveTimeoutMilliseconds { get; set; }

        public int? SendTimeoutMilliseconds { get; set; }

        public int? Retries { get; set; }

        public string ProbeAddress { get; set; }

        public DataTypeEnums? ProbeDataType { get; set; }

        public int? ProbeLength { get; set; }
    }

    public abstract class TcpClientConnectionParametersBase : ClientConnectionParametersBase
    {
        public string Ip { get; set; }

        public int? Port { get; set; }
    }

    public sealed class ModbusTcpClientConnectionParameters : TcpClientConnectionParametersBase
    {
        public int? BatchReadStationIntervalMilliseconds { get; set; }
    }

    public sealed class ModbusRtuClientConnectionParameters : ClientConnectionParametersBase
    {
        public string PortName { get; set; }

        public int? BaudRate { get; set; }

        public int? DataBits { get; set; }

        public StopBits? StopBits { get; set; }

        public Parity? Parity { get; set; }

        public Handshake? Handshake { get; set; }

        public int? BatchReadStationIntervalMilliseconds { get; set; }
    }

    public sealed class SiemensS7ClientConnectionParameters : TcpClientConnectionParametersBase
    {
        public SiemensVersion? SiemensVersion { get; set; }

        public byte? Slot { get; set; }

        public byte? Rack { get; set; }

        public int? BatchReadStationIntervalMilliseconds { get; set; }
    }

    public sealed class FinsClientConnectionParameters : TcpClientConnectionParametersBase
    {
        public int? TimeoutMilliseconds { get; set; }
    }

    public abstract class ServerConnectionParametersBase : IConnectionPoolParameters
    {
        public int? ConnectTimeoutMilliseconds { get; set; }

        public int? ReceiveTimeoutMilliseconds { get; set; }

        public int? SendTimeoutMilliseconds { get; set; }

        public bool? EnableSnapshotPersistence { get; set; }
    }

    public abstract class TcpServerConnectionParametersBase : ServerConnectionParametersBase
    {
        public string Ip { get; set; }

        public int? Port { get; set; }

        public int? MaxConnections { get; set; }
    }

    public sealed class ModbusTcpServerConnectionParameters : TcpServerConnectionParametersBase
    {
    }

    public sealed class ModbusRtuServerConnectionParameters : ServerConnectionParametersBase
    {
        public string PortName { get; set; }

        public int? BaudRate { get; set; }

        public int? DataBits { get; set; }

        public StopBits? StopBits { get; set; }

        public Parity? Parity { get; set; }

        public Handshake? Handshake { get; set; }
    }

    public sealed class SiemensS7ServerConnectionParameters : TcpServerConnectionParametersBase
    {
    }
}

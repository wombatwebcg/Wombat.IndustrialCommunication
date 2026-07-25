using System;
using System.IO.Ports;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.Channels
{
    public enum ChannelState
    {
        Created,
        Connecting,
        Online,
        Faulted,
        Reconnecting,
        Stopping,
        Stopped
    }

    public sealed class ReconnectOptions
    {
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(2);
        public int MaxAttempts { get; set; } = 3;
    }

    public abstract class ChannelOptions
    {
        protected ChannelOptions(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Channel id is required.", nameof(id));
            Id = id;
        }

        public string Id { get; }
        public int MaxConcurrency { get; set; } = 1;
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);
        public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(3);
        public ReconnectOptions Reconnect { get; set; } = new ReconnectOptions();
    }

    public sealed class ModbusTcpChannelOptions : ChannelOptions
    {
        public ModbusTcpChannelOptions(string id, string host, int port = 502) : base(id)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            Host = host;
            Port = port;
        }

        public string Host { get; }
        public int Port { get; }
    }

    public sealed class ModbusRtuChannelOptions : ChannelOptions
    {
        public ModbusRtuChannelOptions(string id, string portName) : base(id)
        {
            if (string.IsNullOrWhiteSpace(portName)) throw new ArgumentException("Port name is required.", nameof(portName));
            PortName = portName;
        }

        public string PortName { get; }
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public Parity Parity { get; set; } = Parity.None;
        public StopBits StopBits { get; set; } = StopBits.One;
        public Handshake Handshake { get; set; } = Handshake.None;
    }

    public sealed class SiemensS7ChannelOptions : ChannelOptions
    {
        public SiemensS7ChannelOptions(string id, string host, SiemensVersion version, int port = 102) : base(id)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            if (version == SiemensVersion.None) throw new ArgumentOutOfRangeException(nameof(version));
            Host = host;
            Port = port;
            Version = version;
        }

        public string Host { get; }
        public int Port { get; }
        public SiemensVersion Version { get; }
        public byte Slot { get; set; }
        public byte Rack { get; set; }
    }

    public sealed class FinsTcpChannelOptions : ChannelOptions
    {
        public FinsTcpChannelOptions(string id, string host, int port = 9600) : base(id)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            Host = host;
            Port = port;
        }

        public string Host { get; }
        public int Port { get; }
    }

    public sealed class ChannelSnapshot
    {
        internal ChannelSnapshot(string channelId) { ChannelId = channelId; }

        public string ChannelId { get; internal set; }
        public ChannelState State { get; internal set; }
        public DateTimeOffset? ConnectedAtUtc { get; internal set; }
        public DateTimeOffset? LastOperationAtUtc { get; internal set; }
        public DateTimeOffset? LastSuccessAtUtc { get; internal set; }
        public DateTimeOffset? LastFailureAtUtc { get; internal set; }
        public OperationFailureKind LastError { get; internal set; }
        public int ConsecutiveFailures { get; internal set; }
        public int WaitingOperations { get; internal set; }
        public int ActiveOperations { get; internal set; }
        public int ReconnectCount { get; internal set; }
    }

    public sealed class ChannelException : Exception
    {
        public ChannelException(string message, OperationFailureKind failureKind, Exception innerException = null)
            : base(message, innerException) { FailureKind = failureKind; }

        public OperationFailureKind FailureKind { get; }
    }

    public sealed class ChannelStateChangedEventArgs : EventArgs
    {
        public ChannelStateChangedEventArgs(string channelId, ChannelState previous, ChannelState current)
        {
            ChannelId = channelId;
            Previous = previous;
            Current = current;
        }

        public string ChannelId { get; }
        public ChannelState Previous { get; }
        public ChannelState Current { get; }
    }
}

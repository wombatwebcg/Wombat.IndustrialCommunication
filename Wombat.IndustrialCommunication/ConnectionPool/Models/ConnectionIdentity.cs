using System;

namespace Wombat.IndustrialCommunication.ConnectionPool.Models
{
    /// <summary>
    /// 连接池中的设备唯一标识。
    /// </summary>
    public class ConnectionIdentity : IEquatable<ConnectionIdentity>
    {
        /// <summary>
        /// 业务设备 ID（同协议下建议唯一）。
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 协议类型（如 ModbusTcp / ModbusRtu / S7 / Fins）。
        /// </summary>
        public string ProtocolType { get; set; }

        /// <summary>
        /// 端点标识（如 IP:Port、串口名）。
        /// </summary>
        public string Endpoint { get; set; }

        public ConnectionIdentity()
        {
            DeviceId = string.Empty;
            ProtocolType = string.Empty;
            Endpoint = string.Empty;
        }

        public bool Equals(ConnectionIdentity other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(DeviceId, other.DeviceId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ProtocolType, other.ProtocolType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Endpoint, other.Endpoint, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ConnectionIdentity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 17;
                hashCode = (hashCode * 23) + (DeviceId ?? string.Empty).ToUpperInvariant().GetHashCode();
                hashCode = (hashCode * 23) + (ProtocolType ?? string.Empty).ToUpperInvariant().GetHashCode();
                hashCode = (hashCode * 23) + (Endpoint ?? string.Empty).ToUpperInvariant().GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}@{2}", ProtocolType, DeviceId, Endpoint);
        }
    }
}

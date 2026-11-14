using System;

namespace Wombat.IndustrialCommunication.Iec104.Protocol
{
    /// <summary>协议异常类别。</summary>
    public enum Iec104ErrorKind
    {
        Protocol,
        UnknownType,
        InvalidLength,
        InvalidAddress,
        CapacityExceeded,
    }

    /// <summary>
    /// 104 协议异常。仅用于「无法继续解析」的情形；链路应保持的错误由调用方统计，不抛异常。
    /// </summary>
    public sealed class Iec104ProtocolException : Exception
    {
        public Iec104ProtocolException(Iec104ErrorKind kind, string message)
            : base(message)
        {
            Kind = kind;
        }

        public Iec104ProtocolException(Iec104ErrorKind kind, string message, Exception innerException)
            : base(message, innerException)
        {
            Kind = kind;
        }

        /// <summary>异常类别。</summary>
        public Iec104ErrorKind Kind { get; }
    }
}

using Wombat.IndustrialCommunication.Abstractions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 读响应报文：承载已解析的 <see cref="McPacket"/>。
    /// 结束码非正常时由传输层提前判定失败，本类型只构造成功响应。
    /// </summary>
    public sealed class McReadResponse : IDeviceReadWriteMessage
    {
        /// <summary>构造读响应</summary>
        /// <param name="packet">已解析的响应包</param>
        public McReadResponse(McPacket packet)
        {
            Packet = packet;
            ProtocolDataNumber = packet.Data?.Length ?? 0;
        }

        /// <summary>已解析的响应包</summary>
        public McPacket Packet { get; }

        /// <summary>响应数据区</summary>
        public byte[] Data
        {
            get { return Packet.Data ?? new byte[0]; }
        }

        /// <inheritdoc />
        public byte[] ProtocolMessageFrame { get; set; }

        /// <inheritdoc />
        public int ProtocolDataNumber { get; set; }

        /// <inheritdoc />
        public int RegisterCount { get; set; }

        /// <inheritdoc />
        public string RegisterAddress { get; set; }

        /// <inheritdoc />
        public int ProtocolResponseLength { get; set; }

        /// <inheritdoc />
        public void Initialize(byte[] frame)
        {
            ProtocolMessageFrame = frame;
        }
    }
}

using Wombat.IndustrialCommunication.Abstractions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 写响应报文。写响应正常情况下没有数据区，只带结束码。
    /// </summary>
    public sealed class McWriteResponse : IDeviceReadWriteMessage
    {
        /// <summary>构造写响应</summary>
        /// <param name="packet">已解析的响应包</param>
        public McWriteResponse(McPacket packet)
        {
            Packet = packet;
        }

        /// <summary>已解析的响应包</summary>
        public McPacket Packet { get; }

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

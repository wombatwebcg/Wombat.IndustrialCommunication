using System;
using Wombat.IndustrialCommunication.Abstractions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 批量写请求报文。数据区由通信层按「位：4 bit/点」「字：2 字节小端」准备好，
    /// 编解码器不再做任何单位换算。
    /// </summary>
    public sealed class McWriteRequest : IDeviceReadWriteMessage
    {
        /// <summary>构造写请求</summary>
        /// <param name="profile">帧/系列派生规则</param>
        /// <param name="address">起始软元件地址</param>
        /// <param name="points">协议点数</param>
        /// <param name="data">数据区</param>
        public McWriteRequest(McProfile profile, McAddress address, int points, byte[] data)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Points = points;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            ProtocolMessageFrame = McFrameCodec.BuildWriteRequest(profile, address, points, data);

            RegisterAddress = address.OriginalAddress;
            RegisterCount = points;
        }

        /// <summary>帧/系列派生规则</summary>
        public McProfile Profile { get; }

        /// <summary>起始软元件地址</summary>
        public McAddress Address { get; }

        /// <summary>协议点数</summary>
        public int Points { get; }

        /// <summary>写入数据区</summary>
        public byte[] Data { get; }

        /// <inheritdoc />
        public byte[] ProtocolMessageFrame { get; private set; }

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

using System;
using Wombat.IndustrialCommunication.Abstractions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// MC 批量读请求报文。帧由 <see cref="McFrameCodec"/> 构造，点数必须是已换算好的协议点数。
    /// </summary>
    public sealed class McReadRequest : IDeviceReadWriteMessage
    {
        /// <summary>构造读请求</summary>
        /// <param name="profile">帧/系列派生规则</param>
        /// <param name="address">起始软元件地址</param>
        /// <param name="points">协议点数（含长定时器的 4 字/点换算）</param>
        public McReadRequest(McProfile profile, McAddress address, int points)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Points = points;
            ProtocolMessageFrame = McFrameCodec.BuildReadRequest(profile, address, points);

            RegisterAddress = address.OriginalAddress;
            RegisterCount = points;
            ExpectedDataLength = address.IsBitDevice ? (points + 1) / 2 : points * 2;
        }

        /// <summary>帧/系列派生规则</summary>
        public McProfile Profile { get; }

        /// <summary>起始软元件地址</summary>
        public McAddress Address { get; }

        /// <summary>协议点数</summary>
        public int Points { get; }

        /// <summary>期望的响应数据字节数（1E 分帧时需要，3E/4E 由长度字段给出）</summary>
        public int ExpectedDataLength { get; }

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

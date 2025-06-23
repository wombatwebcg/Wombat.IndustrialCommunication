using System.Collections.Generic;
using Wombat.Network.WebSockets.Extensions;

namespace Wombat.Network.WebSockets
{
    public interface IFrameBuilder
    {
        SortedList<int, IWebSocketExtension> NegotiatedExtensions { get; set; }

        byte[] EncodeFrame(PingFrame frame);
        byte[] EncodeFrame(PongFrame frame);
        byte[] EncodeFrame(CloseFrame frame);
        byte[] EncodeFrame(TextFrame frame);
        byte[] EncodeFrame(BinaryFrame frame);
        byte[] EncodeFrame(BinaryFragmentationFrame frame);

        bool TryDecodeFrameHeader(byte[] buffer, int offset, int count, out Header frameHeader);
        void DecodePayload(byte[] buffer, int offset, Header frameHeader, out byte[] payload, out int payloadOffset, out int payloadCount);
    }
}

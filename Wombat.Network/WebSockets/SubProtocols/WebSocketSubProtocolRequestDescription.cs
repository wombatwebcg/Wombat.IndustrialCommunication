using System;

namespace Wombat.Network.WebSockets.SubProtocols
{
    public sealed class WebSocketSubProtocolRequestDescription
    {
        public WebSocketSubProtocolRequestDescription(string requestedSubProtocol)
        {
            if (string.IsNullOrWhiteSpace(requestedSubProtocol))
                throw new ArgumentNullException("requestedSubProtocol");
            this.RequestedSubProtocol = requestedSubProtocol;
        }

        public string RequestedSubProtocol { get; private set; }
    }
}

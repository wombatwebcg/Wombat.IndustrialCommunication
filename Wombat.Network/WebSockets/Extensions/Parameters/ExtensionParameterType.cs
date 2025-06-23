using System;

namespace Wombat.Network.WebSockets.Extensions
{
    [Flags]
    public enum ExtensionParameterType : byte
    {
        Single = 0x1,
        Valuable = 0x2,
    }
}

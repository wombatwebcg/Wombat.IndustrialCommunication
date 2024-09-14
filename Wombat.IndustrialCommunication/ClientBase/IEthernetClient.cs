using System.Net;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 以太网形式
    /// </summary>
    public interface IEthernetClient : IDeviceClient
    {
        /// <summary>
        /// IPEndPoint
        /// </summary>
        IPEndPoint IpEndPoint { get; set; }
    }
}

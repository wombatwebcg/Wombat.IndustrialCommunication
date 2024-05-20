using System.Net;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 以太网形式
    /// </summary>
    public interface IEthernetClient : IClient, IReadWrite
    {
        /// <summary>
        /// IPEndPoint
        /// </summary>
        IPEndPoint IpEndPoint { get; set; }
    }
}

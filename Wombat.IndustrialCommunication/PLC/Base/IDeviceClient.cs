using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;


namespace Wombat.IndustrialCommunication.PLC
{
    public interface IDeviceClient: IClient,IReadWrite
    {
        /// <summary>
        /// 版本
        /// </summary>
        string Version { get; }





    }
}

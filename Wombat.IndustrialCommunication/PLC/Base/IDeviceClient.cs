using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.ObjectConversionExtention;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// IIoTClient 接口
    /// </summary>
    public interface IDeviceClient: IClient,IReadWrite
    {
        /// <summary>
        /// 版本
        /// </summary>
        string Version { get; }





    }
}

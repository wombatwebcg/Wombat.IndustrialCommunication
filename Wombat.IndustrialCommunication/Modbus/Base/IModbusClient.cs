using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
   public interface IModbusClient:IDeviceClient
    {
        byte StationNumber { get; set; }

        byte FunctionCode { get; set; }

    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public interface IModbusSerialPortClient:IModbusReadWrite, IClient
    {
    }
}

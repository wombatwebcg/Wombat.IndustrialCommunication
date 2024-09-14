using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus.Base
{
    // 通用Modbus接口
    public interface IModbusHandler
    {
        byte[] HandleRequest(byte[] request);
    }
}

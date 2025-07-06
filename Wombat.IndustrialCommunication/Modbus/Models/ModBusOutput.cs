using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusOutput: ModbusInput
    {
        public object Value { get; set; }
    }
}

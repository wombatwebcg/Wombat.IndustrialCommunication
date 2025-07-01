using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public  enum  ModbusStandardFunctionCode
    {
        ReadCoils = 0x1,
        ReadDiscreteInputs = 0x2,
        ReadHoldingRegisters = 0x3,
        ReadInputRegisters = 0x4,
        WriteSingleCoil = 0x5,
        WriteSingleRegister = 0x6,
        WriteMultipleCoils = 0xF,
        WriteMultipleRegister = 0x10

    }
}

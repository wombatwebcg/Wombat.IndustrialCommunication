﻿using System;
using System.Collections.Generic;
using System.Text;
using Wombat.IndustrialCommunication.Modbus.Data;

namespace Wombat.IndustrialCommunication.DataBase
{
    public class ModbusDataStore
    {
        public ModbusDataCollection<DeviceInternalValueDataUnit<bool>> CoilDiscretes { get; private set; }

    }
}

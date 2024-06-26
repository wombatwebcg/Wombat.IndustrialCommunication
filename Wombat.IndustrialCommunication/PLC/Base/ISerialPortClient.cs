﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace Wombat.IndustrialCommunication.PLC
{
    public interface ISerialPortClient : IClient, IReadWrite
    {
        string PortName { get; set; }
        int BaudRate { get; set; }
        int DataBits { get; set; }
        StopBits StopBits { get; set; }
        Parity Parity { get; set; }
        Handshake Handshake { get; set; }

    }
}

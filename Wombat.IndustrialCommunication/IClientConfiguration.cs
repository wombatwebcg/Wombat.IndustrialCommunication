using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication
{
    public interface IClientConfiguration
    {
        int Retries { get; set; }
        TimeSpan WaitToRetryMilliseconds { get; set; }
        TimeSpan ConnectTimeout { get; set; }
        TimeSpan ReceiveTimeout { get; set; }
        TimeSpan SendTimeout { get; set; }
    }
}

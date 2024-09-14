using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication
{


    public interface IServer
    {
        string Version { get; }

        ILogger Logger { get; set; }

        TimeSpan ConnectTimeout { get; set; }

        TimeSpan ReceiveTimeout { get; set; }

        TimeSpan SendTimeout { get; set; }

        EndianFormat DataFormat { get; set; }

        bool IsReverse { get; set; }


        void UseLogger(ILogger logger);


        bool IsListening { get; }

        OperationResult Listen();

        OperationResult Shutdown();



    }
}

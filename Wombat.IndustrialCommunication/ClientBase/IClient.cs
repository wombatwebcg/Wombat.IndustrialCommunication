using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication
{


    public delegate void LoggerDelegate(string name, Exception ex = null);

    public interface IClient
    {
        string Version { get; }

        ILogger Logger { get; set; }

        TimeSpan ConnectTimeout { get; set; }

        TimeSpan ReceiveTimeout { get; set; }

        TimeSpan SendTimeout { get; set; }

        EndianFormat DataFormat { get; set; }

        bool IsReverse { get; set; }

        TimeSpan WaiteInterval { get; set; }

        int OperationReTryTimes { get; set; }

        void UseLogger(ILogger logger);

        void SetQueueOperation(int maxConcurrency, int maxQueueSize);


        bool IsLongLivedConnection { get; set; }

        bool Connected { get; }

        OperationResult Connect();

        OperationResult Disconnect();

        Task<OperationResult> ConnectAsync();

        Task<OperationResult> DisconnectAsync();



    }
}

using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication
{


    public delegate void LoggerDelegate(string name, Exception ex = null);

    public interface IClient: IClientConfiguration
    {
        string Version { get; }

        ILogger Logger { get; set; }

        bool IsLongConnection { get; set; }

        bool Connected { get; }

        OperationResult Connect();

        OperationResult Disconnect();

        Task<OperationResult> ConnectAsync();

        Task<OperationResult> DisconnectAsync();



    }
}

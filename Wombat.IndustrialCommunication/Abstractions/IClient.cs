using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.Abstractions
{


    public delegate void LoggerDelegate(string name, Exception ex = null);

    public interface IProtocolClient
    {
        bool Connected { get; }

        Task<OperationResult> ConnectAsync(CancellationToken cancellationToken = default);

        Task<OperationResult> DisconnectAsync();
    }

    public interface IClient: IClientConfiguration, IProtocolClient
    {
        string Version { get; }

        ILogger Logger { get; set; }

    }
}

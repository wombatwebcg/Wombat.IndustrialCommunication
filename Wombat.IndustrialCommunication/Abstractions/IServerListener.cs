using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Events;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.Abstractions
{
    /// <summary>
    /// 具备监听能力的服务端流资源接口。
    /// </summary>
    public interface IServerListener : IStreamResource
    {
        event EventHandler<DataReceivedEventArgs> DataReceived;

        event EventHandler<SessionEventArgs> ClientConnected;

        event EventHandler<SessionEventArgs> ClientDisconnected;

        Task<OperationResult> ListenAsync();

        Task<OperationResult> ShutdownAsync();
    }
}

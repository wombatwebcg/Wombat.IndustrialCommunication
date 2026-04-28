using System;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
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

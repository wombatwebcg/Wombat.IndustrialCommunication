using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;

namespace Wombat.IndustrialCommunication.Extensions.Bluetooth
{
    public interface IBluetoothChannel : IDisposable
    {
        bool Connected { get; }
        TimeSpan ConnectTimeout { get; set; }
        TimeSpan ReceiveTimeout { get; set; }
        TimeSpan SendTimeout { get; set; }
        Task<OperationResult> ConnectAsync(CancellationToken cancellationToken);
        Task<OperationResult> DisconnectAsync();
        Task<OperationResult<int>> ReceiveAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken);
        Task<OperationResult> SendAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken);
    }
}

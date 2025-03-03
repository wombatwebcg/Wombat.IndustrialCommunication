using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    public interface IStreamResource : IDisposable
    {

        TimeSpan ReceiveTimeout { get; set; }
        TimeSpan SendTimeout { get; set; }
        Task<OperationResult<int>> Receive(byte[] buffer, int offset, int length, CancellationToken cancellationToken);
        Task<OperationResult> Send(byte[] buffer, int offset, int length, CancellationToken cancellationToken);
        void StreamClose();
    }
}

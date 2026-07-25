using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.Abstractions
{
    public interface IDeviceMessageTransport
    {

        Task<OperationResult<byte[]>> ReceiveResponseAsync(int index, int length);
        Task<OperationResult<byte[]>> ReceiveResponseAsync(int index, int length, CancellationToken cancellationToken);
        Task<OperationResult> SendRequestAsync(byte[] request);
        Task<OperationResult> SendRequestAsync(byte[] request, CancellationToken cancellationToken);
        Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage readRequest);
        Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage readRequest, CancellationToken cancellationToken);
        Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage writeRequest);
        Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage writeRequest, CancellationToken cancellationToken);
        IStreamResource StreamResource { get; }
    }
}

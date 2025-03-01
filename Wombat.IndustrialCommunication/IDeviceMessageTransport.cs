using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    public interface IDeviceMessageTransport
    {

        Task<OperationResult<byte[]>> ReceiveResponseAsync(int index, int length);
        Task<OperationResult> SendRequestAsync(byte[] request);
        Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage readRequest);
        Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage writeRequest);
        IStreamResource StreamResource { get; }
    }
}

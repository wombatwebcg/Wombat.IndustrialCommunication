using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.PLC
{
    public sealed class S7NativeWriteBatch
    {
        public List<S7NativeWriteItem> Items { get; } = new List<S7NativeWriteItem>();

        public int RequestLength => S7NativeWriteRequest.EstimateRequestLength(Items);

        public int DataLength => S7NativeWriteRequest.EstimateDataLength(Items);

        public int ResponseFrameLength => S7NativeWriteRequest.EstimateResponseFrameLength(Items.Count);
    }
}

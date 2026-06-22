using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.PLC
{
    public sealed class S7NativeReadBatch
    {
        public List<S7NativeReadItem> Items { get; } = new List<S7NativeReadItem>();

        public int RequestLength => S7NativeReadRequest.EstimateRequestLength(Items.Count);

        public int ResponseFrameLength => S7NativeReadRequest.EstimateResponseFrameLength(Items);
    }
}

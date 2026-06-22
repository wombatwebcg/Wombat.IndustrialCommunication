using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.PLC
{
    public sealed class S7WriteBatch
    {
        public List<SiemensAddress> Items { get; } = new List<SiemensAddress>();

        public int RequestLength => S7WriteRequest.EstimateRequestLength(Items);

        public int DataLength => S7WriteRequest.EstimateDataLength(Items);

        public int ResponseFrameLength => S7WriteRequest.EstimateResponseFrameLength(Items.Count);
    }
}

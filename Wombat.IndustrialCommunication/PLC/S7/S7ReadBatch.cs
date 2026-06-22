using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.PLC
{
    public sealed class S7ReadBatch
    {
        public List<SiemensAddress> Items { get; } = new List<SiemensAddress>();

        public int RequestLength => S7ReadRequest.EstimateRequestLength(Items.Count);

        public int ResponseFrameLength => S7ReadRequest.EstimateResponseFrameLength(Items);
    }
}

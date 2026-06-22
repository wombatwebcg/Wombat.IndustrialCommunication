namespace Wombat.IndustrialCommunication.PLC
{
    internal sealed class S7BatchReadDispatchAnalysis
    {
        public S7BatchReadPathKind Mode { get; set; }

        public string DecisionReason { get; set; }

        public int RequestedBytes { get; set; }

        public int BlockReadBytes { get; set; }

        public double BlockByteEfficiency { get; set; }

        public int BlockCount { get; set; }

        public int NativeBatchCount { get; set; }

        public int NativeTotalBytes { get; set; }

        public int AddressCount { get; set; }

        public int MaxAddressLength { get; set; }
    }
}

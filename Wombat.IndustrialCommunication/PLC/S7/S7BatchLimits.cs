using System;

namespace Wombat.IndustrialCommunication.PLC
{
    internal readonly struct S7BatchLimits
    {
        public S7BatchLimits(int maxItems, int requestLimit, int payloadLimit, int responseLimit)
        {
            MaxItems = maxItems;
            RequestLimit = requestLimit;
            PayloadLimit = payloadLimit;
            ResponseLimit = responseLimit;
        }

        public int MaxItems { get; }

        public int RequestLimit { get; }

        public int PayloadLimit { get; }

        public int ResponseLimit { get; }

        public static S7BatchLimits CreateReadLimits(int negotiatedPduLimit, int maxItems, int payloadLimit)
        {
            return new S7BatchLimits(
                Math.Max(1, maxItems),
                Math.Max(S7ReadRequest.EstimateRequestLength(1), negotiatedPduLimit),
                Math.Max(4, payloadLimit),
                Math.Max(SiemensConstant.InitHeadLength + 17 + 6, negotiatedPduLimit));
        }

        public static S7BatchLimits CreateWriteLimits(int negotiatedPduLimit, int maxItems, int payloadLimit)
        {
            var singleItemBaseline = new[]
            {
                new SiemensAddress
                {
                    WriteData = new byte[] { 0x00 }
                }
            };

            return new S7BatchLimits(
                Math.Max(1, maxItems),
                Math.Max(S7WriteRequest.EstimateRequestLength(singleItemBaseline), negotiatedPduLimit),
                Math.Max(5, payloadLimit),
                Math.Max(S7WriteRequest.EstimateResponseFrameLength(1), negotiatedPduLimit));
        }
    }
}

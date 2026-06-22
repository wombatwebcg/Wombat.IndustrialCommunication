using System;

namespace Wombat.IndustrialCommunication.PLC
{
    internal readonly struct S7NativeBatchLimits
    {
        public S7NativeBatchLimits(int maxItems, int requestLimit, int payloadLimit, int responseLimit)
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

        public static S7NativeBatchLimits CreateReadLimits(int negotiatedPduLimit, int maxItems, int payloadLimit)
        {
            return new S7NativeBatchLimits(
                Math.Max(1, maxItems),
                Math.Max(S7NativeReadRequest.EstimateRequestLength(1), negotiatedPduLimit),
                Math.Max(4, payloadLimit),
                Math.Max(SiemensConstant.InitHeadLength + 17 + 6, negotiatedPduLimit));
        }

        public static S7NativeBatchLimits CreateWriteLimits(int negotiatedPduLimit, int maxItems, int payloadLimit)
        {
            var singleItemBaseline = new[]
            {
                new S7NativeWriteItem
                {
                    WriteData = new byte[] { 0x00 }
                }
            };

            return new S7NativeBatchLimits(
                Math.Max(1, maxItems),
                Math.Max(S7NativeWriteRequest.EstimateRequestLength(singleItemBaseline), negotiatedPduLimit),
                Math.Max(5, payloadLimit),
                Math.Max(S7NativeWriteRequest.EstimateResponseFrameLength(1), negotiatedPduLimit));
        }
    }
}

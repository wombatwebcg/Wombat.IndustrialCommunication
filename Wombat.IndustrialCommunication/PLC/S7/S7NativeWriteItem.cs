using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    public sealed class S7NativeWriteItem
    {
        public string OriginalAddress { get; set; }

        public DataTypeEnums DataType { get; set; }

        public byte AreaTypeCode { get; set; }

        public ushort DbNumber { get; set; }

        public int BeginAddress { get; set; }

        public int ByteOffset { get; set; }

        public int BitOffset { get; set; }

        public bool IsBit { get; set; }

        public int OriginalIndex { get; set; }

        public byte[] WriteData { get; set; }

        public bool EffectiveIsBit
        {
            get
            {
                if (!IsBit || WriteData == null || WriteData.Length != 1)
                {
                    return false;
                }

                return WriteData[0] < 2;
            }
        }

        public int RequestLength => WriteData?.Length ?? 0;
    }
}

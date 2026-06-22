using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    public sealed class S7NativeReadItem
    {
        public string OriginalAddress { get; set; }

        public DataTypeEnums DataType { get; set; }

        public byte AreaTypeCode { get; set; }

        public ushort DbNumber { get; set; }

        public int BeginAddress { get; set; }

        public int ByteOffset { get; set; }

        public int Length { get; set; }

        public int RequestedLength { get; set; }

        public int BitOffset { get; set; }

        public bool IsBit { get; set; }

        public int OriginalIndex { get; set; }

        public int RequestLength => IsBit && RequestedLength > 1
            ? (RequestedLength + 7) / 8
            : Length;
    }
}

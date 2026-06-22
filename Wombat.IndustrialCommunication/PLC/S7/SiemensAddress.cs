using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 西门子解析后的统一地址信息
    /// </summary>
    public class SiemensAddress
    {
        public string Address { get; set; }

        public string OriginalAddress { get; set; }

        public DataTypeEnums DataType { get; set; }

        public byte TypeCode { get; set; }

        public ushort DbBlock { get; set; }

        public int BeginAddress { get; set; }

        public int BeginAddressOffest { get; set; }

        public int ReadWriteLength { get; set; }

        public bool IsBit { get; set; }

        public int ByteOffset { get; set; }

        public int BitOffset { get; set; }

        public int Length { get; set; }

        public int RequestedLength { get; set; }

        public int OriginalIndex { get; set; }

        public byte[] WriteData { get; set; }

        public int RequestLength
        {
            get
            {
                if (WriteData != null)
                {
                    return WriteData.Length;
                }

                if (IsBit && RequestedLength > 1)
                {
                    return (RequestedLength + 7) / 8;
                }

                if (Length > 0)
                {
                    return Length;
                }

                return ReadWriteLength;
            }
        }

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
    }
}

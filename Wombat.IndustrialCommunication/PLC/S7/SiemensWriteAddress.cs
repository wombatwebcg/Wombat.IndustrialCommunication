namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 西门子写地址兼容层。新逻辑优先使用 SiemensAddress.WriteData。
    /// </summary>
    public class SiemensWriteAddress : SiemensAddress
    {
        public SiemensWriteAddress(SiemensAddress data)
        {
            Address = data.Address;
            OriginalAddress = data.OriginalAddress;
            DataType = data.DataType;
            TypeCode = data.TypeCode;
            DbBlock = data.DbBlock;
            BeginAddress = data.BeginAddress;
            BeginAddressOffest = data.BeginAddressOffest;
            ReadWriteLength = data.ReadWriteLength;
            IsBit = data.IsBit;
            ByteOffset = data.ByteOffset;
            BitOffset = data.BitOffset;
            Length = data.Length;
            RequestedLength = data.RequestedLength;
            OriginalIndex = data.OriginalIndex;
            WriteData = data.WriteData;
        }
    }
}

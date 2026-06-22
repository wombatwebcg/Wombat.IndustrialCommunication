using System;
using System.Collections.Generic;
using System.Linq;
using Wombat.IndustrialCommunication.Enums;

namespace Wombat.IndustrialCommunication.PLC
{
    public class S7ReadRequest : IDeviceReadWriteMessage
    {
        public S7ReadRequest(string address, int offest, int length, bool isBit, ushort pduReference = 1)
        {
            RegisterAddress = address;
            RegisterCount = length;
            PduReference = pduReference;

            var siemensAddress = S7CommonMethods.ConvertArg(address, offest);
            siemensAddress.IsBit = isBit;
            siemensAddress.ReadWriteLength = length;
            siemensAddress.RequestedLength = length;
            siemensAddress.Length = length;

            Items = new[] { siemensAddress };
            ProtocolMessageFrame = GetReadCommand(siemensAddress, pduReference);
        }

        public S7ReadRequest(IReadOnlyList<SiemensAddress> items, ushort pduReference)
        {
            if (items == null || items.Count == 0)
            {
                throw new ArgumentException("items 不能为空", nameof(items));
            }

            Items = items.ToList().AsReadOnly();
            PduReference = pduReference;
            RegisterAddress = string.Join(",", Items.Select(t => t.OriginalAddress ?? t.Address));
            RegisterCount = Items.Count;
            ProtocolMessageFrame = GetReadCommand(Items, pduReference);
        }

        public byte[] ProtocolMessageFrame { get; set; }

        public int ProtocolDataNumber { get; set; }

        public string RegisterAddress { get; set; }

        public int RegisterCount { get; set; }

        public ushort PduReference { get; }

        public IReadOnlyList<SiemensAddress> Items { get; }

        public int ProtocolResponseLength { get; set; } = SiemensConstant.InitHeadLength;

        public void Initialize(byte[] frame)
        {
            throw new NotImplementedException();
        }

        public static int EstimateRequestLength(int itemCount)
        {
            return 19 + itemCount * 12;
        }

        public static int EstimateResponsePayloadLength(IReadOnlyList<SiemensAddress> items)
        {
            if (items == null || items.Count == 0)
            {
                return 0;
            }

            return items.Sum(t => 4 + (t.RequestLength == 1 ? 2 : t.RequestLength));
        }

        public static int EstimateResponseFrameLength(IReadOnlyList<SiemensAddress> items)
        {
            return SiemensConstant.InitHeadLength + 17 + EstimateResponsePayloadLength(items);
        }

        protected byte[] GetReadCommand(SiemensAddress[] datas, ushort pduReference)
        {
            return GetReadCommand((IReadOnlyList<SiemensAddress>)datas, pduReference);
        }

        protected byte[] GetReadCommand(IReadOnlyList<SiemensAddress> datas, ushort pduReference)
        {
            byte[] command = new byte[EstimateRequestLength(datas.Count)];
            command[0] = 0x03;
            command[1] = 0x00;
            command[2] = (byte)(command.Length / 256);
            command[3] = (byte)(command.Length % 256);
            command[4] = 0x02;
            command[5] = 0xF0;
            command[6] = 0x80;
            command[7] = 0x32;
            command[8] = 0x01;
            command[9] = 0x00;
            command[10] = 0x00;
            command[11] = (byte)(pduReference >> 8);
            command[12] = (byte)(pduReference & 0xFF);
            command[13] = (byte)((command.Length - 17) / 256);
            command[14] = (byte)((command.Length - 17) % 256);
            command[15] = 0x00;
            command[16] = 0x00;
            command[17] = 0x04;
            command[18] = (byte)datas.Count;

            for (int i = 0; i < datas.Count; i++)
            {
                var data = datas[i];
                bool isBit = data.IsBit && data.RequestedLength == 1;
                int realLength = data.RequestLength;

                command[19 + i * 12] = 0x12;
                command[20 + i * 12] = 0x0A;
                command[21 + i * 12] = 0x10;
                command[22 + i * 12] = isBit ? (byte)0x01 : (byte)0x02;
                command[23 + i * 12] = (byte)(realLength / 256);
                command[24 + i * 12] = (byte)(realLength % 256);
                command[25 + i * 12] = (byte)(data.DbBlock / 256);
                command[26 + i * 12] = (byte)(data.DbBlock % 256);
                command[27 + i * 12] = data.TypeCode;
                command[28 + i * 12] = (byte)(data.BeginAddress / 256 / 256 % 256);
                command[29 + i * 12] = (byte)(data.BeginAddress / 256 % 256);
                command[30 + i * 12] = (byte)(data.BeginAddress % 256);
            }

            return command;
        }

        protected byte[] GetReadCommand(SiemensAddress data, ushort pduReference)
        {
            return GetReadCommand(new[] { data }, pduReference);
        }
    }
}

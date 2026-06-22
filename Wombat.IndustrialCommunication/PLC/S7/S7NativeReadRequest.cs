using System;
using System.Collections.Generic;
using System.Linq;

namespace Wombat.IndustrialCommunication.PLC
{
    public sealed class S7NativeReadRequest : IDeviceReadWriteMessage
    {
        public S7NativeReadRequest(IReadOnlyList<S7NativeReadItem> items, ushort pduReference)
        {
            if (items == null || items.Count == 0)
            {
                throw new ArgumentException("items 不能为空", nameof(items));
            }

            Items = items.ToList().AsReadOnly();
            PduReference = pduReference;
            RegisterAddress = string.Join(",", Items.Select(t => t.OriginalAddress));
            RegisterCount = Items.Count;
            ProtocolMessageFrame = BuildCommand(Items, pduReference);
        }

        public IReadOnlyList<S7NativeReadItem> Items { get; }

        public ushort PduReference { get; }

        public byte[] ProtocolMessageFrame { get; set; }

        public int ProtocolDataNumber { get; set; }

        public string RegisterAddress { get; set; }

        public int RegisterCount { get; set; }

        public int ProtocolResponseLength { get; set; } = SiemensConstant.InitHeadLength;

        public void Initialize(byte[] frame)
        {
        }

        public static int EstimateRequestLength(int itemCount)
        {
            return 19 + itemCount * 12;
        }

        public static int EstimateResponsePayloadLength(IReadOnlyList<S7NativeReadItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return 0;
            }

            return items.Sum(t => 4 + (t.RequestLength == 1 ? 2 : t.RequestLength));
        }

        public static int EstimateResponseFrameLength(IReadOnlyList<S7NativeReadItem> items)
        {
            return SiemensConstant.InitHeadLength + 17 + EstimateResponsePayloadLength(items);
        }

        private static byte[] BuildCommand(IReadOnlyList<S7NativeReadItem> items, ushort pduReference)
        {
            var command = new byte[EstimateRequestLength(items.Count)];
            command[0] = 0x03;
            command[1] = 0x00;
            command[2] = (byte)(command.Length >> 8);
            command[3] = (byte)(command.Length & 0xFF);
            command[4] = 0x02;
            command[5] = 0xF0;
            command[6] = 0x80;
            command[7] = 0x32;
            command[8] = 0x01;
            command[9] = 0x00;
            command[10] = 0x00;
            command[11] = (byte)(pduReference >> 8);
            command[12] = (byte)(pduReference & 0xFF);
            command[13] = (byte)((command.Length - 17) >> 8);
            command[14] = (byte)((command.Length - 17) & 0xFF);
            command[15] = 0x00;
            command[16] = 0x00;
            command[17] = 0x04;
            command[18] = (byte)items.Count;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int offset = 19 + i * 12;
                command[offset] = 0x12;
                command[offset + 1] = 0x0A;
                command[offset + 2] = 0x10;
                command[offset + 3] = item.IsBit && item.RequestedLength == 1 ? (byte)0x01 : (byte)0x02;
                command[offset + 4] = (byte)(item.RequestLength >> 8);
                command[offset + 5] = (byte)(item.RequestLength & 0xFF);
                command[offset + 6] = (byte)(item.DbNumber >> 8);
                command[offset + 7] = (byte)(item.DbNumber & 0xFF);
                command[offset + 8] = item.AreaTypeCode;
                command[offset + 9] = (byte)((item.BeginAddress >> 16) & 0xFF);
                command[offset + 10] = (byte)((item.BeginAddress >> 8) & 0xFF);
                command[offset + 11] = (byte)(item.BeginAddress & 0xFF);
            }

            return command;
        }
    }
}

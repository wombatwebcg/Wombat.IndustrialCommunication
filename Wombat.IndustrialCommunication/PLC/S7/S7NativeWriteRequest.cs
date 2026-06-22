using System;
using System.Collections.Generic;
using System.Linq;

namespace Wombat.IndustrialCommunication.PLC
{
    public sealed class S7NativeWriteRequest : IDeviceReadWriteMessage
    {
        public S7NativeWriteRequest(IReadOnlyList<S7NativeWriteItem> items, ushort pduReference)
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

        public IReadOnlyList<S7NativeWriteItem> Items { get; }

        public ushort PduReference { get; }

        public byte[] ProtocolMessageFrame { get; set; }

        public int ProtocolDataNumber { get; set; }

        public string RegisterAddress { get; set; }

        public int RegisterCount { get; set; }

        public int ProtocolResponseLength { get; set; } = SiemensConstant.InitHeadLength;

        public void Initialize(byte[] frame)
        {
        }

        public static int EstimateParameterLength(int itemCount)
        {
            return 2 + itemCount * 12;
        }

        public static int EstimateDataLength(IReadOnlyList<S7NativeWriteItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < items.Count; i++)
            {
                int payloadLength = items[i].RequestLength;
                total += 4 + payloadLength;
                if (payloadLength == 1 && i < items.Count - 1)
                {
                    total += 1;
                }
            }

            return total;
        }

        public static int EstimateRequestLength(IReadOnlyList<S7NativeWriteItem> items)
        {
            return 17 + EstimateParameterLength(items?.Count ?? 0) + EstimateDataLength(items);
        }

        public static int EstimateResponseFrameLength(int itemCount)
        {
            return SiemensConstant.InitHeadLength + 13 + itemCount;
        }

        private static byte[] BuildCommand(IReadOnlyList<S7NativeWriteItem> items, ushort pduReference)
        {
            int parameterLength = EstimateParameterLength(items.Count);
            int dataLength = EstimateDataLength(items);
            var command = new byte[17 + parameterLength + dataLength];

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
            command[13] = (byte)(parameterLength >> 8);
            command[14] = (byte)(parameterLength & 0xFF);
            command[15] = (byte)(dataLength >> 8);
            command[16] = (byte)(dataLength & 0xFF);
            command[17] = 0x05;
            command[18] = (byte)items.Count;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int offset = 19 + i * 12;
                command[offset] = 0x12;
                command[offset + 1] = 0x0A;
                command[offset + 2] = 0x10;
                command[offset + 3] = item.EffectiveIsBit ? (byte)0x01 : (byte)0x02;
                command[offset + 4] = (byte)(item.RequestLength >> 8);
                command[offset + 5] = (byte)(item.RequestLength & 0xFF);
                command[offset + 6] = (byte)(item.DbNumber >> 8);
                command[offset + 7] = (byte)(item.DbNumber & 0xFF);
                command[offset + 8] = item.AreaTypeCode;
                command[offset + 9] = (byte)((item.BeginAddress >> 16) & 0xFF);
                command[offset + 10] = (byte)((item.BeginAddress >> 8) & 0xFF);
                command[offset + 11] = (byte)(item.BeginAddress & 0xFF);
            }

            int dataOffset = 19 + items.Count * 12;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int coefficient = item.EffectiveIsBit ? 1 : 8;
                command[dataOffset] = 0x00;
                command[dataOffset + 1] = item.EffectiveIsBit ? (byte)0x03 : (byte)0x04;
                command[dataOffset + 2] = (byte)((item.RequestLength * coefficient) >> 8);
                command[dataOffset + 3] = (byte)((item.RequestLength * coefficient) & 0xFF);

                if (item.RequestLength == 1)
                {
                    command[dataOffset + 4] = item.EffectiveIsBit && item.WriteData[0] == 0x01
                        ? (byte)0x01
                        : item.WriteData[0];
                    dataOffset += i == items.Count - 1 ? 5 : 6;
                    continue;
                }

                Buffer.BlockCopy(item.WriteData, 0, command, dataOffset + 4, item.RequestLength);
                dataOffset += 4 + item.RequestLength;
            }

            return command;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Wombat.IndustrialCommunication.Enums;

namespace Wombat.IndustrialCommunication.PLC
{
    public class S7WriteRequest : IDeviceReadWriteMessage
    {
        public S7WriteRequest(string address, int offest, byte[] writeData, bool isBit, ushort pduReference = 1)
        {
            RegisterAddress = address;
            RegisterCount = writeData?.Length ?? 0;
            PduReference = pduReference;
            var siemensWriteAddress = S7CommonMethods.ConvertWriteArg(address, offest, writeData, isBit);
            Items = new[] { (SiemensAddress)siemensWriteAddress };
            ProtocolMessageFrame = GetWriteCommand(siemensWriteAddress, pduReference);
        }

        public S7WriteRequest(IReadOnlyList<SiemensAddress> items, ushort pduReference)
        {
            if (items == null || items.Count == 0)
            {
                throw new ArgumentException("items 不能为空", nameof(items));
            }

            Items = items.ToList().AsReadOnly();
            PduReference = pduReference;
            RegisterAddress = string.Join(",", Items.Select(t => t.OriginalAddress ?? t.Address));
            RegisterCount = Items.Count;
            ProtocolMessageFrame = GetWriteCommand(Items, pduReference);
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

        public static int EstimateParameterLength(int itemCount)
        {
            return 2 + itemCount * 12;
        }

        public static int EstimateDataLength(IReadOnlyList<SiemensAddress> items)
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

        public static int EstimateRequestLength(IReadOnlyList<SiemensAddress> items)
        {
            return 17 + EstimateParameterLength(items?.Count ?? 0) + EstimateDataLength(items);
        }

        public static int EstimateResponseFrameLength(int itemCount)
        {
            return SiemensConstant.InitHeadLength + 13 + itemCount;
        }

        protected byte[] GetWriteCommand(SiemensWriteAddress write, ushort pduReference)
        {
            return GetWriteCommand(new[] { (SiemensAddress)write }, pduReference);
        }

        protected byte[] GetWriteCommand(SiemensWriteAddress[] writes, ushort pduReference)
        {
            return GetWriteCommand((IReadOnlyList<SiemensAddress>)writes, pduReference);
        }

        protected byte[] GetWriteCommand(IReadOnlyList<SiemensAddress> writes, ushort pduReference)
        {
            int parameterLength = EstimateParameterLength(writes.Count);
            int writeDataLength = EstimateDataLength(writes);
            byte[] command = new byte[17 + parameterLength + writeDataLength];

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
            command[13] = (byte)(parameterLength >> 8);
            command[14] = (byte)(parameterLength & 0xFF);
            command[15] = (byte)(writeDataLength >> 8);
            command[16] = (byte)(writeDataLength & 0xFF);
            command[17] = 0x05;
            command[18] = (byte)writes.Count;

            for (int i = 0; i < writes.Count; i++)
            {
                var write = writes[i];
                var typeCode = write.TypeCode;
                var beginAddress = write.BeginAddress;
                var dbBlock = write.DbBlock;

                command[19 + i * 12] = 0x12;
                command[20 + i * 12] = 0x0A;
                command[21 + i * 12] = 0x10;
                command[22 + i * 12] = write.EffectiveIsBit ? (byte)0x01 : (byte)0x02;
                command[23 + i * 12] = (byte)(write.RequestLength / 256);
                command[24 + i * 12] = (byte)(write.RequestLength % 256);
                command[25 + i * 12] = (byte)(dbBlock / 256);
                command[26 + i * 12] = (byte)(dbBlock % 256);
                command[27 + i * 12] = typeCode;
                command[28 + i * 12] = (byte)(beginAddress / 256 / 256 % 256);
                command[29 + i * 12] = (byte)(beginAddress / 256 % 256);
                command[30 + i * 12] = (byte)(beginAddress % 256);
            }

            int index = 19 + writes.Count * 12;
            for (int i = 0; i < writes.Count; i++)
            {
                var write = writes[i];
                bool isBit = write.EffectiveIsBit;
                var writeData = write.WriteData;
                var coefficient = isBit ? 1 : 8;

                command[index] = 0x00;
                command[index + 1] = isBit ? (byte)0x03 : (byte)0x04;
                command[index + 2] = (byte)(write.RequestLength * coefficient / 256);
                command[index + 3] = (byte)(write.RequestLength * coefficient % 256);

                if (write.RequestLength == 1)
                {
                    command[index + 4] = isBit
                        ? (writeData[0] == 0x01 ? (byte)0x01 : (byte)0x00)
                        : writeData[0];
                    index += i >= writes.Count - 1 ? 5 : 6;
                }
                else
                {
                    writeData.CopyTo(command, index + 4);
                    index += 4 + writeData.Length;
                }
            }

            return command;
        }
    }
}

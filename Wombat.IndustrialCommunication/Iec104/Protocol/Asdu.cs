using System;
using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.Iec104.Protocol
{
    /// <summary>
    /// 应用服务数据单元。解析时严格校验长度，遇到未知类型直接抛 <see cref="Iec104ProtocolException"/>。
    /// </summary>
    public sealed class Asdu
    {
        private Asdu()
        {
        }

        public Iec104TypeId TypeId { get; private set; }

        /// <summary>VSQ 的 SQ 位：true 表示信息对象地址连续递增，仅首对象携带地址。</summary>
        public bool Sequence { get; private set; }

        public Iec104Cause Cause { get; private set; }

        /// <summary>P/N 位：true 表示否定确认。</summary>
        public bool Negative { get; private set; }

        /// <summary>T 位：测试标志。</summary>
        public bool Test { get; private set; }

        public byte Originator { get; private set; }

        public ushort CommonAddress { get; private set; }

        public IReadOnlyList<Iec104InformationObject> Objects { get; private set; }

        /// <summary>解析 ASDU。</summary>
        public static Asdu Parse(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < Iec104Constants.AsduHeaderLength)
                throw new Iec104ProtocolException(Iec104ErrorKind.InvalidLength, "ASDU 不足 6 字节头部。");

            var typeId = (Iec104TypeId)buffer[offset];
            if (!Iec104TypeInfo.TryGetElementSize(typeId, out var elementSize))
                throw new Iec104ProtocolException(Iec104ErrorKind.UnknownType, $"未知类型标识 {(byte)typeId}。");

            var vsq = buffer[offset + 1];
            var sequence = (vsq & 0x80) != 0;
            var objectCount = vsq & 0x7F;

            var cot = ByteOrder.ReadUInt16(buffer, offset + 2);
            var cause = (Iec104Cause)(cot & 0x3F);
            var negative = (cot & 0x40) != 0;
            var test = (cot & 0x80) != 0;
            var originator = buffer[offset + 3];
            var commonAddress = ByteOrder.ReadUInt16(buffer, offset + 4);

            var hasAddress = Iec104TypeInfo.HasInformationObjectAddress(typeId);
            var addressLength = hasAddress ? Iec104Constants.InformationObjectAddressLength : 0;

            var required = Iec104Constants.AsduHeaderLength
                + (sequence
                    ? addressLength + objectCount * elementSize
                    : objectCount * (addressLength + elementSize));
            if (count < required)
                throw new Iec104ProtocolException(Iec104ErrorKind.InvalidLength, $"ASDU 长度不足，需要 {required} 字节，实际 {count} 字节。");

            var objects = new List<Iec104InformationObject>(objectCount);
            var position = offset + Iec104Constants.AsduHeaderLength;

            if (sequence && objectCount > 0 && hasAddress)
            {
                var baseAddress = ByteOrder.ReadInt24(buffer, position);
                position += addressLength;
                for (var i = 0; i < objectCount; i++)
                {
                    var address = baseAddress + i;
                    if (address > Iec104Constants.MaxInformationObjectAddress)
                        throw new Iec104ProtocolException(Iec104ErrorKind.InvalidAddress, "连续寻址越过地址上限。");

                    var element = new byte[elementSize];
                    Buffer.BlockCopy(buffer, position, element, 0, elementSize);
                    position += elementSize;
                    objects.Add(new Iec104InformationObject(typeId, address, element));
                }
            }
            else
            {
                for (var i = 0; i < objectCount; i++)
                {
                    var address = 0;
                    if (hasAddress)
                    {
                        address = ByteOrder.ReadInt24(buffer, position);
                        position += addressLength;
                    }

                    var element = new byte[elementSize];
                    Buffer.BlockCopy(buffer, position, element, 0, elementSize);
                    position += elementSize;
                    objects.Add(new Iec104InformationObject(typeId, address, element));
                }
            }

            return new Asdu
            {
                TypeId = typeId,
                Sequence = sequence,
                Cause = cause,
                Negative = negative,
                Test = test,
                Originator = originator,
                CommonAddress = commonAddress,
                Objects = objects,
            };
        }
    }
}

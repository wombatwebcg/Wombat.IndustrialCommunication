using System;
using System.Collections.Generic;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Protocol
{
    /// <summary>
    /// ASDU 组帧器。负责容量计算、分帧与头部编码。
    /// </summary>
    public static class AsduBuilder
    {
        /// <summary>按类型计算单帧可容纳的对象数（SQ=0）。</summary>
        public static int GetCapacity(Iec104TypeId typeId)
        {
            if (!Iec104TypeInfo.TryGetElementSize(typeId, out var elementSize))
                throw new Iec104ProtocolException(Iec104ErrorKind.UnknownType, $"未知类型标识 {(byte)typeId}。");

            var addressLength = Iec104TypeInfo.HasInformationObjectAddress(typeId)
                ? Iec104Constants.InformationObjectAddressLength
                : 0;
            var bytesPerObject = addressLength + elementSize;
            if (bytesPerObject <= 0) return Iec104Constants.MaxObjectsPerAsdu;

            var capacity = (Iec104Constants.MaxAsduLength - Iec104Constants.AsduHeaderLength) / bytesPerObject;
            if (capacity > Iec104Constants.MaxObjectsPerAsdu) capacity = Iec104Constants.MaxObjectsPerAsdu;
            return capacity < 1 ? 1 : capacity;
        }

        /// <summary>编码一个 ASDU（SQ=0）。</summary>
        public static byte[] Build(
            Iec104TypeId typeId,
            ushort commonAddress,
            Iec104Cause cause,
            IReadOnlyList<Iec104InformationObject> objects,
            byte originator = 0,
            bool negative = false,
            bool test = false)
        {
            if (objects == null) throw new ArgumentNullException(nameof(objects));
            if (!Iec104TypeInfo.TryGetElementSize(typeId, out var elementSize))
                throw new Iec104ProtocolException(Iec104ErrorKind.UnknownType, $"未知类型标识 {(byte)typeId}。");
            if (objects.Count > Iec104Constants.MaxObjectsPerAsdu)
                throw new Iec104ProtocolException(Iec104ErrorKind.CapacityExceeded, "单帧对象数超过 127。");

            var addressLength = Iec104TypeInfo.HasInformationObjectAddress(typeId)
                ? Iec104Constants.InformationObjectAddressLength
                : 0;
            var total = Iec104Constants.AsduHeaderLength
                + objects.Count * (addressLength + elementSize);
            if (total > Iec104Constants.MaxAsduLength)
                throw new Iec104ProtocolException(Iec104ErrorKind.CapacityExceeded, $"ASDU 超出 {Iec104Constants.MaxAsduLength} 字节上限。");

            var buffer = new byte[total];
            buffer[0] = (byte)typeId;
            buffer[1] = (byte)objects.Count;
            ByteOrder.WriteUInt16(buffer, 2, EncodeCause(cause, negative, test));
            buffer[3] = originator;
            ByteOrder.WriteUInt16(buffer, 4, commonAddress);

            var position = Iec104Constants.AsduHeaderLength;
            for (var i = 0; i < objects.Count; i++)
            {
                var item = objects[i];
                if (item == null) throw new ArgumentException("信息对象不能为空。", nameof(objects));
                if (item.TypeId != typeId)
                    throw new ArgumentException($"对象类型 {item.TypeId} 与 ASDU 类型 {typeId} 不一致。", nameof(objects));
                if (item.Element.Length != elementSize)
                    throw new ArgumentException("信息对象元素长度与类型不匹配。", nameof(objects));

                if (addressLength > 0)
                {
                    ByteOrder.WriteInt24(buffer, position, item.Ioa);
                    position += addressLength;
                }

                Buffer.BlockCopy(item.Element, 0, buffer, position, elementSize);
                position += elementSize;
            }

            return buffer;
        }

        /// <summary>按容量自动分帧，返回可直接封装的 ASDU 序列。</summary>
        public static IReadOnlyList<byte[]> BuildFrames(
            Iec104TypeId typeId,
            ushort commonAddress,
            Iec104Cause cause,
            IReadOnlyList<Iec104InformationObject> objects,
            byte originator = 0,
            bool negative = false,
            bool test = false)
        {
            if (objects == null) throw new ArgumentNullException(nameof(objects));

            var capacity = GetCapacity(typeId);
            var frames = new List<byte[]>();
            for (var start = 0; start < objects.Count; start += capacity)
            {
                var count = Math.Min(capacity, objects.Count - start);
                var slice = new List<Iec104InformationObject>(count);
                for (var i = 0; i < count; i++)
                {
                    slice.Add(objects[start + i]);
                }

                frames.Add(Build(typeId, commonAddress, cause, slice, originator, negative, test));
            }

            return frames;
        }

        /// <summary>总召唤命令（激活）。</summary>
        public static byte[] InterrogationCommand(ushort commonAddress, byte qualifier = Iec104Constants.QualifierStationInterrogation)
        {
            return Build(
                Iec104TypeId.InterrogationCommand,
                commonAddress,
                Iec104Cause.Activation,
                new[] { Iec104InformationObject.InterrogationCommand(qualifier) });
        }

        /// <summary>总召唤激活确认。</summary>
        public static byte[] InterrogationConfirmation(ushort commonAddress, byte qualifier = Iec104Constants.QualifierStationInterrogation)
        {
            return Build(
                Iec104TypeId.InterrogationCommand,
                commonAddress,
                Iec104Cause.ActivationConfirmation,
                new[] { Iec104InformationObject.InterrogationResponse(qualifier) });
        }

        /// <summary>总召唤激活终止。</summary>
        public static byte[] InterrogationTermination(ushort commonAddress, byte qualifier = Iec104Constants.QualifierStationInterrogation)
        {
            return Build(
                Iec104TypeId.InterrogationCommand,
                commonAddress,
                Iec104Cause.ActivationTermination,
                new[] { Iec104InformationObject.InterrogationResponse(qualifier) });
        }

        /// <summary>初始化结束。</summary>
        public static byte[] EndOfInitialization(ushort commonAddress, byte causeOfInitialization = 0)
        {
            return Build(
                Iec104TypeId.EndOfInitialization,
                commonAddress,
                Iec104Cause.Initialized,
                new[] { new Iec104InformationObject(Iec104TypeId.EndOfInitialization, 0, new[] { causeOfInitialization }) });
        }

        /// <summary>时钟同步命令。</summary>
        public static byte[] ClockSynchronization(ushort commonAddress, DateTime time)
        {
            return Build(
                Iec104TypeId.ClockSynchronizationCommand,
                commonAddress,
                Iec104Cause.Activation,
                new[] { Iec104InformationObject.ClockSynchronization(time) });
        }

        private static ushort EncodeCause(Iec104Cause cause, bool negative, bool test)
        {
            ushort value = (ushort)((byte)cause & 0x3F);
            if (negative) value |= 0x40;
            if (test) value |= 0x80;
            return value;
        }
    }
}

using System;
using System.Collections.Generic;
using Wombat.IndustrialCommunication.Extensions.IEC104.Protocol;
using Xunit;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.Tests
{
    public class FrameParserTests
    {
        private static byte[] BuildFrame(ushort send, ushort receive, byte[] asdu)
        {
            return ApciFrame.CreateInformation(send, receive, asdu).ToBytes();
        }

        private static byte[] BuildAsdu(int value)
        {
            return AsduBuilder.Build(
                Iec104TypeId.MeasuredFloat,
                1,
                Iec104Cause.Spontaneous,
                new[] { Iec104InformationObject.MeasuredFloat(1, value) });
        }

        [Fact]
        public void PartialFrame_WaitsForRemainder()
        {
            var frame = BuildFrame(0, 0, BuildAsdu(1));
            var parser = new Iec104FrameParser();

            parser.Append(frame, 0, 3);
            byte[] parsed;
            Assert.False(parser.TryReadFrame(out parsed));

            parser.Append(frame, 3, frame.Length - 3);
            Assert.True(parser.TryReadFrame(out parsed));
            Assert.Equal(frame.Length, parsed.Length);
            Assert.Equal(frame, parsed);
        }

        [Fact]
        public void StickyFrames_AreReadOneByOne()
        {
            var first = BuildFrame(0, 0, BuildAsdu(1));
            var second = BuildFrame(1, 0, BuildAsdu(2));
            var third = BuildFrame(2, 0, BuildAsdu(3));

            var buffer = new List<byte>();
            buffer.AddRange(first);
            buffer.AddRange(second);
            buffer.AddRange(third);
            var data = buffer.ToArray();

            var parser = new Iec104FrameParser();
            parser.Append(data, 0, data.Length);

            byte[] parsed;
            Assert.True(parser.TryReadFrame(out parsed));
            Assert.Equal(first, parsed);
            Assert.True(parser.TryReadFrame(out parsed));
            Assert.Equal(second, parsed);
            Assert.True(parser.TryReadFrame(out parsed));
            Assert.Equal(third, parsed);
            Assert.False(parser.TryReadFrame(out parsed));
        }

        [Fact]
        public void GarbagePrefix_IsDiscardedAndCounted()
        {
            var frame = BuildFrame(0, 0, BuildAsdu(1));
            var data = new byte[frame.Length + 3];
            data[0] = 0x00;
            data[1] = 0x11;
            data[2] = 0x22;
            Buffer.BlockCopy(frame, 0, data, 3, frame.Length);

            var parser = new Iec104FrameParser();
            parser.Append(data, 0, data.Length);

            byte[] parsed;
            Assert.True(parser.TryReadFrame(out parsed));
            Assert.Equal(frame, parsed);
            Assert.Equal(3, parser.Resynchronizations);
        }

        [Fact]
        public void IllegalLengthField_IsDroppedAndParsingContinues()
        {
            var frame = BuildFrame(0, 0, BuildAsdu(1));
            var data = new byte[frame.Length + 2];
            data[0] = 0x68;
            data[1] = 0x03;
            Buffer.BlockCopy(frame, 0, data, 2, frame.Length);

            var parser = new Iec104FrameParser();
            parser.Append(data, 0, data.Length);

            byte[] parsed;
            Assert.True(parser.TryReadFrame(out parsed));
            Assert.Equal(frame, parsed);
            Assert.Equal(1, parser.InvalidFrames);
        }

        [Fact]
        public void PayloadContainingStartByte_DoesNotBreakFraming()
        {
            var objects = new[] { Iec104InformationObject.MeasuredFloat(0x686868 & 0xFFFFFF, 0x68) };
            var asdu = AsduBuilder.Build(Iec104TypeId.MeasuredFloat, 1, Iec104Cause.Spontaneous, objects);
            var frame = BuildFrame(0, 0, asdu);

            var parser = new Iec104FrameParser();
            parser.Append(frame, 0, frame.Length);

            byte[] parsed;
            Assert.True(parser.TryReadFrame(out parsed));
            Assert.Equal(frame, parsed);
        }

        [Fact]
        public void Reset_ClearsBufferAndCounters()
        {
            var parser = new Iec104FrameParser();
            parser.Append(new byte[] { 0x00, 0x00 }, 0, 2);
            parser.TryReadFrame(out _);

            parser.Reset();
            Assert.Equal(0, parser.BufferedBytes);
            Assert.Equal(0, parser.Resynchronizations);
        }
    }
}

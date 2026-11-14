using System;
using System.Collections.Generic;
using Wombat.IndustrialCommunication.Iec104.Protocol;
using Xunit;

namespace Wombat.IndustrialCommunication.Iec104.Tests
{
    public class AsduTests
    {
        [Fact]
        public void FloatMeasurements_RoundTrip()
        {
            var objects = new[]
            {
                Iec104InformationObject.MeasuredFloat(1, 1.5f),
                Iec104InformationObject.MeasuredFloat(2, -2.25f),
                Iec104InformationObject.MeasuredFloat(3, 100f, Iec104QualityFlags.NotTopical),
            };

            var bytes = AsduBuilder.Build(Iec104TypeId.MeasuredFloat, 1, Iec104Cause.Spontaneous, objects);
            var asdu = Asdu.Parse(bytes, 0, bytes.Length);

            Assert.Equal(Iec104TypeId.MeasuredFloat, asdu.TypeId);
            Assert.Equal(Iec104Cause.Spontaneous, asdu.Cause);
            Assert.Equal(1, asdu.CommonAddress);
            Assert.Equal(3, asdu.Objects.Count);

            float value;
            Assert.True(asdu.Objects[0].TryGetFloat(out value));
            Assert.Equal(1.5f, value);
            Assert.True(asdu.Objects[1].TryGetFloat(out value));
            Assert.Equal(-2.25f, value);
            Assert.Equal(Iec104QualityFlags.NotTopical, asdu.Objects[2].GetQuality());
        }

        [Fact]
        public void TimeTaggedSinglePoint_RoundTripsTime()
        {
            var time = new DateTime(2026, 11, 13, 10, 20, 30, 123);
            var objects = new[] { Iec104InformationObject.SinglePointWithTime(42, true, time) };

            var bytes = AsduBuilder.Build(Iec104TypeId.SinglePointWithTime, 1, Iec104Cause.Spontaneous, objects);
            var asdu = Asdu.Parse(bytes, 0, bytes.Length);

            Assert.True(asdu.Objects[0].GetSinglePoint());

            Cp56Time2a parsed;
            Assert.True(asdu.Objects[0].TryGetTime(out parsed));
            Assert.Equal(time, parsed.Value);
            Assert.False(parsed.Invalid);
        }

        [Fact]
        public void SequenceAddressing_ProducesConsecutiveAddresses()
        {
            var bytes = new byte[6 + 3 + 3 * 5];
            bytes[0] = (byte)Iec104TypeId.MeasuredFloat;
            bytes[1] = 0x80 | 3;
            bytes[2] = (byte)Iec104Cause.InterrogatedByStation;
            bytes[3] = 0;
            bytes[4] = 1;
            bytes[5] = 0;
            bytes[6] = 100;
            bytes[7] = 0;
            bytes[8] = 0;
            var position = 9;
            for (var i = 0; i < 3; i++)
            {
                bytes[position + 4] = 0x00;
                position += 5;
            }

            var asdu = Asdu.Parse(bytes, 0, bytes.Length);
            Assert.True(asdu.Sequence);
            Assert.Equal(3, asdu.Objects.Count);
            Assert.Equal(100, asdu.Objects[0].Ioa);
            Assert.Equal(101, asdu.Objects[1].Ioa);
            Assert.Equal(102, asdu.Objects[2].Ioa);
        }

        [Fact]
        public void NegativeAndTestFlags_AreParsed()
        {
            var objects = new[] { Iec104InformationObject.SinglePoint(1, true) };
            var bytes = AsduBuilder.Build(
                Iec104TypeId.SinglePoint, 7, Iec104Cause.ActivationConfirmation, objects, originator: 3, negative: true, test: true);

            var asdu = Asdu.Parse(bytes, 0, bytes.Length);
            Assert.True(asdu.Negative);
            Assert.True(asdu.Test);
            Assert.Equal(3, asdu.Originator);
            Assert.Equal(7, asdu.CommonAddress);
        }

        [Fact]
        public void UnknownType_ThrowsUnknownTypeError()
        {
            var bytes = new byte[] { 200, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00 };
            var ex = Assert.Throws<Iec104ProtocolException>(() => Asdu.Parse(bytes, 0, bytes.Length));
            Assert.Equal(Iec104ErrorKind.UnknownType, ex.Kind);
        }

        [Fact]
        public void ShortHeader_ThrowsInvalidLength()
        {
            var bytes = new byte[] { 0x01, 0x01, 0x03, 0x00 };
            var ex = Assert.Throws<Iec104ProtocolException>(() => Asdu.Parse(bytes, 0, bytes.Length));
            Assert.Equal(Iec104ErrorKind.InvalidLength, ex.Kind);
        }

        [Fact]
        public void TruncatedObjects_ThrowsInvalidLength()
        {
            var objects = new[]
            {
                Iec104InformationObject.MeasuredFloat(1, 1f),
                Iec104InformationObject.MeasuredFloat(2, 2f),
            };
            var bytes = AsduBuilder.Build(Iec104TypeId.MeasuredFloat, 1, Iec104Cause.Spontaneous, objects);

            var ex = Assert.Throws<Iec104ProtocolException>(() => Asdu.Parse(bytes, 0, bytes.Length - 3));
            Assert.Equal(Iec104ErrorKind.InvalidLength, ex.Kind);
        }

        [Fact]
        public void InterrogationCommand_EncodesTypeCauseAndQualifier()
        {
            var asduBytes = AsduBuilder.InterrogationCommand(1);
            var frame = ApciFrame.CreateInformation(0, 0, asduBytes).ToBytes();

            var parser = new Iec104FrameParser();
            parser.Append(frame, 0, frame.Length);

            byte[] parsedFrame;
            Assert.True(parser.TryReadFrame(out parsedFrame));

            ApciFrame apci;
            string error;
            Assert.True(ApciFrame.TryParse(parsedFrame, 0, parsedFrame.Length, out apci, out error), error);

            var asdu = Asdu.Parse(apci.Asdu, 0, apci.Asdu.Length);
            Assert.Equal(Iec104TypeId.InterrogationCommand, asdu.TypeId);
            Assert.Equal(Iec104Cause.Activation, asdu.Cause);
            Assert.Equal(20, asdu.Objects[0].Element[0]);
            Assert.Equal(Iec104Constants.AsduHeaderLength + 1, apci.Asdu.Length);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.Abstractions;
using Wombat.IndustrialCommunication.Models;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.PLCTests
{
    /// <summary>
    /// MC 客户端（传输层 + 通信层）测试：分帧、序列号回显、错误码透出、点数字换算与批量合并。
    /// 全部用脚本化流资源驱动，不需要真机；真机联调用例单独标记跳过。
    /// </summary>
    public class McClientTests
    {
        private static McCommunication CreateCommunication(ScriptedStreamResource stream, McFrameType frameType,
            McSeries series)
        {
            var transport = new McEthernetTransport(stream, frameType);
            return new McCommunication(transport, new McProfile(frameType, series));
        }

        private static byte[] Bytes(string hex)
        {
            return hex.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Convert.ToByte(x, 16))
                .ToArray();
        }

        private static string Hex(byte[] data)
        {
            return string.Join(" ", data.Select(b => b.ToString("X2")));
        }

        private static byte[] Q3EResponse(byte[] data)
        {
            var frame = new List<byte> { 0xD0, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00 };
            int length = data.Length + 2;
            frame.Add((byte)(length & 0xFF));
            frame.Add((byte)(length >> 8));
            frame.Add(0x00);
            frame.Add(0x00);
            frame.AddRange(data);
            return frame.ToArray();
        }

        private static byte[] Q3EError(ushort endCode)
        {
            return Bytes($"D0 00 00 FF FF 03 00 02 00 {(byte)(endCode & 0xFF):X2} {(byte)(endCode >> 8):X2}");
        }

        private static byte[] Q4EResponse(ushort serialNumber, byte[] data)
        {
            var frame = new List<byte> { 0xD4, 0x00, (byte)(serialNumber & 0xFF), (byte)(serialNumber >> 8), 0x00, 0x00 };
            frame.AddRange(new byte[] { 0x00, 0xFF, 0xFF, 0x03, 0x00 });
            int length = data.Length + 2;
            frame.Add((byte)(length & 0xFF));
            frame.Add((byte)(length >> 8));
            frame.Add(0x00);
            frame.Add(0x00);
            frame.AddRange(data);
            return frame.ToArray();
        }

        [Fact]
        public async Task Q3E_ReadTwoWords_ShouldSendOneFrameAndReturnLittleEndian()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Q3EResponse(Bytes("34 12 78 56")));
            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.Q);

            var result = await communication.ReadUInt16Async("D100", 2).AsTask();

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new ushort[] { 0x1234, 0x5678 }, result.ResultValue);
            Assert.Single(stream.SentFrames);
            Assert.Equal(21, stream.SentFrames[0].Length);
            Assert.Equal(0xA8, stream.SentFrames[0][18]);
            Assert.Equal(0x02, stream.SentFrames[0][19]);
        }

        [Fact]
        public async Task Q3E_ReadTwentyWords_ShouldUseSingleFrame()
        {
            var stream = new ScriptedStreamResource();
            var data = new byte[40];
            stream.EnqueueResponse(Q3EResponse(data));
            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.Q);

            var result = await communication.ReadUInt16Async("D100", 20).AsTask();

            Assert.True(result.IsSuccess, result.Message);
            Assert.Single(stream.SentFrames);
            Assert.Equal(20, BitConverter.ToUInt16(stream.SentFrames[0], 19));
        }

        [Fact]
        public async Task Q3E_ReadBits_ShouldUnpackFourBitsPerPoint()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Q3EResponse(Bytes("10 10")));
            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.Q);

            var result = await communication.ReadBooleanAsync("M100", 4).AsTask();

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new[] { true, false, true, false }, result.ResultValue);
        }

        [Fact]
        public async Task Q3E_EndCodeError_ShouldFailWithOriginalCode()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Q3EError(McCommonMethods.EndCodePointCountOutOfRange));
            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.Q);

            var result = await communication.ReadUInt16Async("D100", 1).AsTask();

            Assert.False(result.IsSuccess);
            Assert.Contains("C051", result.Message);
        }

        [Fact]
        public async Task Q3E_EndCodeError_TransportShouldReportDeviceRejectedWithRawCode()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Q3EError(McCommonMethods.EndCodePointCountOutOfRange));
            var profile = new McProfile(McFrameType.Q3E, McSeries.Q);
            var transport = new McEthernetTransport(stream, McFrameType.Q3E);

            var result = await transport.UnicastReadMessageAsync(
                new McReadRequest(profile, McAddress.Parse("D100", profile), 1));

            Assert.False(result.IsSuccess);
            Assert.Equal(OperationFailureKind.DeviceRejected, result.FailureKind);
            Assert.Equal(McCommonMethods.EndCodePointCountOutOfRange, result.ErrorCode);
            Assert.Contains("C051", result.Message);
            Assert.Single(result.Responses);
        }

        [Fact]
        public async Task Q4E_SerialNumber_ShouldIncrementAndEchoBack()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Q4EResponse(1, Bytes("34 12")));
            stream.EnqueueResponse(Q4EResponse(2, Bytes("78 56")));
            var communication = CreateCommunication(stream, McFrameType.Q4E, McSeries.Q);

            var first = await communication.ReadUInt16Async("D100", 1).AsTask();
            var second = await communication.ReadUInt16Async("D100", 1).AsTask();

            Assert.True(first.IsSuccess, first.Message);
            Assert.True(second.IsSuccess, second.Message);
            Assert.Equal(25, stream.SentFrames[0].Length);
            Assert.Equal(1, BitConverter.ToUInt16(stream.SentFrames[0], 2));
            Assert.Equal(2, BitConverter.ToUInt16(stream.SentFrames[1], 2));
            Assert.Equal(0, BitConverter.ToUInt16(stream.SentFrames[0], 4));
        }

        [Fact]
        public async Task Q3E_SubHeaderMismatch_ShouldFailWithoutReadingBody()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Bytes("D1 00 00 FF FF 03 00 04 00 00 00 34 12"));
            var profile = new McProfile(McFrameType.Q3E, McSeries.Q);
            var transport = new McEthernetTransport(stream, McFrameType.Q3E);

            var result = await transport.UnicastReadMessageAsync(
                new McReadRequest(profile, McAddress.Parse("D100", profile), 1));

            Assert.False(result.IsSuccess);
            Assert.Equal(OperationFailureKind.ProtocolViolation, result.FailureKind);
            Assert.Equal(1, stream.ReceiveCallCount);
        }

        [Fact]
        public async Task Q4E_SerialNumberMismatch_ShouldFailAsProtocolViolation()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Q4EResponse(9, Bytes("34 12")));
            var profile = new McProfile(McFrameType.Q4E, McSeries.Q);
            var transport = new McEthernetTransport(stream, McFrameType.Q4E);

            var result = await transport.UnicastReadMessageAsync(
                new McReadRequest(profile, McAddress.Parse("D100", profile), 1));

            Assert.False(result.IsSuccess);
            Assert.Equal(OperationFailureKind.ProtocolViolation, result.FailureKind);
            Assert.Contains("序列号", result.Message);
        }

        [Fact]
        public async Task A1E_ReadWord_ShouldSendTwelveByteFrame()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Bytes("81 00 34 12"));
            var communication = CreateCommunication(stream, McFrameType.A1E, McSeries.A);

            var result = await communication.ReadUInt16Async("D100", 1).AsTask();

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal((ushort)0x1234, result.ResultValue[0]);
            Assert.Equal("01 FF 0A 00 64 00 00 00 20 44 01 00", Hex(stream.SentFrames[0]));
        }

        [Fact]
        public async Task A1E_AbnormalResponse_ShouldReturnErrorWithoutWaiting()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Bytes("81 5B 10"));
            var communication = CreateCommunication(stream, McFrameType.A1E, McSeries.A);

            var result = await communication.ReadUInt16Async("D100", 1).AsTask();

            Assert.False(result.IsSuccess);
            Assert.Contains("5B", result.Message);
            Assert.Contains("10", result.Message);
            Assert.Equal(2, stream.ReceiveCallCount);
        }

        [Fact]
        public async Task A1E_UnsupportedDevice_ShouldFailLocallyWithoutFrame()
        {
            var stream = new ScriptedStreamResource();
            var communication = CreateCommunication(stream, McFrameType.A1E, McSeries.A);

            var result = await communication.ReadBooleanAsync("SB100", 1).AsTask();

            Assert.False(result.IsSuccess);
            Assert.Contains("SB", result.Message);
            Assert.Empty(stream.SentFrames);
        }

        [Fact]
        public async Task A1E_BitReadUnaligned_ShouldAlignHeadToMultipleOfSixteen()
        {
            var stream = new ScriptedStreamResource();
            // M100 对齐到 M96，读 4+2=6 点：nibble 序列 0,0,0,0,1,0
            stream.EnqueueResponse(Bytes("81 00 00 00 10"));
            var communication = CreateCommunication(stream, McFrameType.A1E, McSeries.A);

            var result = await communication.ReadBooleanAsync("M100", 2).AsTask();

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new[] { true, false }, result.ResultValue);
            Assert.Equal(96, BitConverter.ToInt32(stream.SentFrames[0], 4));
            Assert.Equal(6, stream.SentFrames[0][10]);
        }

        [Fact]
        public async Task IqR_ReadLongTimer_TwoElements_ShouldRequestEightPoints()
        {
            var stream = new ScriptedStreamResource();
            var data = Bytes("0A 00 00 00 00 00 00 00 14 00 00 00 00 00 00 00");
            stream.EnqueueResponse(Q3EResponse(data));
            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.IqR);

            var result = await communication.ReadUInt16Async("LTN0", 2).AsTask();

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new ushort[] { 10, 20 }, result.ResultValue);

            var frame = stream.SentFrames[0];
            Assert.Equal(23, frame.Length);
            Assert.Equal(0x52, frame[19]);
            Assert.Equal(0x00, frame[20]);
            Assert.Equal(8, BitConverter.ToUInt16(frame, 21));
        }

        [Fact]
        public async Task Q3E_WriteThreeBits_ShouldPackNibbles()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Q3EPlainAck());
            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.Q);

            var result = await communication.WriteAsync("M100", new[] { true, true, true });

            Assert.True(result.IsSuccess, result.Message);
            var frame = stream.SentFrames[0];
            Assert.Equal(3, BitConverter.ToUInt16(frame, 19));
            Assert.Equal(0x11, frame[21]);
            Assert.Equal(0x10, frame[22]);
        }

        [Fact]
        public async Task Q3E_WriteInt32_ShouldSendTwoWordsInDcbaOrder()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Q3EPlainAck());
            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.Q);

            var result = await communication.WriteAsync("D100", 0x00020001);

            Assert.True(result.IsSuccess, result.Message);
            var frame = stream.SentFrames[0];
            Assert.Equal(2, BitConverter.ToUInt16(frame, 19));
            Assert.Equal("01 00 02 00", Hex(frame.Skip(21).ToArray()));
        }

        [Fact]
        public async Task BatchRead_TwentyConsecutiveWords_ShouldMergeIntoSingleFrame()
        {
            var stream = new ScriptedStreamResource();
            var data = new byte[40];
            for (int i = 0; i < 20; i++)
                BitConverter.GetBytes((ushort)(i + 1)).CopyTo(data, i * 2);
            stream.EnqueueResponse(Q3EResponse(data));

            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.Q);
            var addresses = new Dictionary<string, DataTypeEnums>();
            for (int i = 0; i < 20; i++)
                addresses["D" + (100 + i)] = DataTypeEnums.UInt16;

            var result = await communication.BatchReadAsync(addresses).AsTask();

            Assert.True(result.IsSuccess, result.Message);
            Assert.Single(stream.SentFrames);
            Assert.Equal(20, BitConverter.ToUInt16(stream.SentFrames[0], 19));

            for (int i = 0; i < 20; i++)
                Assert.Equal((ushort)(i + 1), result.ResultValue["D" + (100 + i)].Item2);
        }

        [Fact]
        public async Task BatchRead_BitAndWord_ShouldSplitIntoTwoFrames()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Q3EResponse(Bytes("01 00")));
            stream.EnqueueResponse(Q3EResponse(Bytes("10 00")));
            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.Q);

            var result = await communication.BatchReadAsync(new Dictionary<string, DataTypeEnums>
            {
                { "D100", DataTypeEnums.UInt16 },
                { "M100", DataTypeEnums.Bool }
            }).AsTask();

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(2, stream.SentFrames.Count);
            Assert.Equal((ushort)1, result.ResultValue["D100"].Item2);
            Assert.Equal(true, result.ResultValue["M100"].Item2);
        }

        [Fact]
        public void MitsubishiMcChannelOptions_InvalidFrameSeriesCombination_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                new Wombat.IndustrialCommunication.Channels.MitsubishiMcChannelOptions("mc", "127.0.0.1", McFrameType.A1E, McSeries.Q));

            var options = new Wombat.IndustrialCommunication.Channels.MitsubishiMcChannelOptions("mc", "192.168.1.10", McFrameType.Q4E, McSeries.IqR, 5001);
            Assert.Equal(McFrameType.Q4E, options.FrameType);
            Assert.Equal(McSeries.IqR, options.Series);
            Assert.Equal(5001, options.Port);
        }

        [Fact]
        public void McClient_ShouldExposeConfigurationWithoutConnecting()
        {
            var client = new McClient("127.0.0.1", 5000, McFrameType.Q3E, McSeries.IqR, TimeSpan.FromSeconds(1));

            Assert.Equal(McFrameType.Q3E, client.FrameType);
            Assert.Equal(McSeries.IqR, client.Series);
            Assert.False(client.Connected);
            Assert.Equal(TimeSpan.FromSeconds(1), client.ReceiveTimeout);

            client.AddressRadixOverride = 8;
            Assert.Equal(8, client.AddressRadixOverride);
        }

        [Fact]
        public async Task BatchWrite_ConsecutiveWords_ShouldMergeIntoSingleFrame()
        {
            var stream = new ScriptedStreamResource();
            stream.EnqueueResponse(Q3EPlainAck());
            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.Q);

            var result = await communication.BatchWriteAsync(new Dictionary<string, (DataTypeEnums, object)>
            {
                { "D100", (DataTypeEnums.UInt16, (object)(ushort)5) },
                { "D101", (DataTypeEnums.UInt16, (object)(ushort)6) }
            }).AsTask();

            Assert.True(result.IsSuccess, result.Message);
            Assert.Single(stream.SentFrames);
            Assert.Equal(2, BitConverter.ToUInt16(stream.SentFrames[0], 19));
            Assert.Equal("05 00 06 00", Hex(stream.SentFrames[0].Skip(21).ToArray()));
        }

        [Fact]
        public async Task Q3E_ReadWordDeviceAsBit_ShouldFailLocally()
        {
            var stream = new ScriptedStreamResource();
            var communication = CreateCommunication(stream, McFrameType.Q3E, McSeries.Q);

            var result = await communication.ReadBooleanAsync("D100", 1).AsTask();

            Assert.False(result.IsSuccess);
            Assert.Contains("不支持位访问", result.Message);
            Assert.Empty(stream.SentFrames);
        }

        [Fact(Skip = "需要支持 3E 帧的真机或模拟器")]
        public async Task RealPlc_ReadWriteRoundTrip()
        {
            var client = new McClient("127.0.0.1", 5000, McFrameType.Q3E, McSeries.Q, TimeSpan.FromSeconds(3));
            var connect = await client.ConnectAsync();
            Assert.True(connect.IsSuccess, connect.Message);

            var write = await client.WriteAsync("D100", (ushort)1234);
            Assert.True(write.IsSuccess, write.Message);

            var read = await client.ReadUInt16Async("D100");
            Assert.True(read.IsSuccess, read.Message);
            Assert.Equal((ushort)1234, read.ResultValue);

            await client.DisconnectAsync();
        }

        private static byte[] Q3EPlainAck()
        {
            return Bytes("D0 00 00 FF FF 03 00 02 00 00 00");
        }

        private sealed class ScriptedStreamResource : IStreamResource
        {
            private readonly Queue<byte[]> _responses = new Queue<byte[]>();
            private byte[] _current;
            private int _offset;

            public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);
            public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(1);
            public bool Connected { get { return true; } }
            public int ReceiveCallCount { get; private set; }
            public List<byte[]> SentFrames { get; } = new List<byte[]>();

            public void EnqueueResponse(byte[] response)
            {
                _responses.Enqueue(response);
            }

            public Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
            {
                var copy = new byte[size];
                Array.Copy(buffer, offset, copy, 0, size);
                SentFrames.Add(copy);
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
            {
                ReceiveCallCount++;

                while (_current == null || _offset >= _current.Length)
                {
                    if (_responses.Count == 0)
                        return Task.FromResult(OperationResult.CreateFailedResult<int>("没有更多脚本化响应"));

                    _current = _responses.Dequeue();
                    _offset = 0;
                }

                int count = Math.Min(size, _current.Length - _offset);
                Array.Copy(_current, _offset, buffer, offset, count);
                _offset += count;
                return Task.FromResult(OperationResult.CreateSuccessResult(count));
            }

            public void StreamClose()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}

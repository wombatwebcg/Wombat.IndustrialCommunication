using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ModbusTests
{
    public class ModbusBatchReadStationIntervalTests
    {
        [Fact]
        public async Task BatchReadAsync_ShouldWaitBetweenDifferentStations()
        {
            var transport = new FakeModbusTcpTransport();
            var client = new ModbusTcpClientBase(transport)
            {
                BatchReadStationInterval = TimeSpan.FromMilliseconds(100)
            };

            var addresses = new Dictionary<string, DataTypeEnums>
            {
                { "1;40001", DataTypeEnums.UInt16 },
                { "1;40002", DataTypeEnums.UInt16 },
                { "2;40001", DataTypeEnums.UInt16 }
            };

            var result = await client.BatchReadAsync(addresses);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(2, transport.RequestStations.Count);
            Assert.Equal(new byte[] { 1, 2 }, transport.RequestStations.ToArray());
            Assert.True(transport.RequestIntervals[1] >= TimeSpan.FromMilliseconds(70),
                $"跨站读取间隔不足，实际为 {transport.RequestIntervals[1].TotalMilliseconds}ms");
        }

        private sealed class FakeModbusTcpTransport : DeviceMessageTransport
        {
            private readonly long _startTimestamp;

            public FakeModbusTcpTransport() : base(new NullStreamResource())
            {
                _startTimestamp = Stopwatch.GetTimestamp();
            }

            public List<byte> RequestStations { get; } = new List<byte>();

            public List<TimeSpan> RequestIntervals { get; } = new List<TimeSpan>();

            public override Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage request)
            {
                var requestFrame = request.ProtocolMessageFrame;
                byte stationNumber = requestFrame[6];
                byte functionCode = requestFrame[7];
                ushort registerCount = (ushort)((requestFrame[10] << 8) | requestFrame[11]);
                int byteCount = functionCode == 0x01 || functionCode == 0x02
                    ? (int)Math.Ceiling(registerCount / 8.0)
                    : registerCount * 2;

                var data = new byte[byteCount];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = stationNumber;
                }

                RequestStations.Add(stationNumber);
                RequestIntervals.Add(GetElapsedTime());

                IDeviceReadWriteMessage response = new ModbusTcpResponse(BuildReadResponseFrame(requestFrame, stationNumber, functionCode, data));
                return Task.FromResult(OperationResult.CreateSuccessResult(response));
            }

            private TimeSpan GetElapsedTime()
            {
                long currentTimestamp = Stopwatch.GetTimestamp();
                return TimeSpan.FromSeconds((currentTimestamp - _startTimestamp) / (double)Stopwatch.Frequency);
            }

            private static byte[] BuildReadResponseFrame(byte[] requestFrame, byte stationNumber, byte functionCode, byte[] data)
            {
                int frameLength = 9 + data.Length;
                byte[] responseFrame = new byte[frameLength];
                responseFrame[0] = requestFrame[0];
                responseFrame[1] = requestFrame[1];
                responseFrame[2] = 0x00;
                responseFrame[3] = 0x00;
                responseFrame[4] = 0x00;
                responseFrame[5] = (byte)(data.Length + 3);
                responseFrame[6] = stationNumber;
                responseFrame[7] = functionCode;
                responseFrame[8] = (byte)data.Length;
                Array.Copy(data, 0, responseFrame, 9, data.Length);
                return responseFrame;
            }
        }

        private sealed class NullStreamResource : IStreamResource
        {
            public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public bool Connected => true;

            public void Dispose()
            {
            }

            public Task<OperationResult<int>> Receive(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult.CreateSuccessResult(0));
            }

            public Task<OperationResult> Send(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public void StreamClose()
            {
            }
        }
    }
}

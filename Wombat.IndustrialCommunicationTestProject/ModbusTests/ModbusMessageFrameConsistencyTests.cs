using System;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ModbusTests
{
    public class ModbusMessageFrameConsistencyTests
    {
        [Fact]
        public async Task Tcp_WriteSingleRegister_NewAndOldFormat_ShouldProduceSameFrame()
        {
            string oldFrame = await CaptureTcpWriteRegisterFrameAsync("1;6;195", 1);
            string newFrame = await CaptureTcpWriteRegisterFrameAsync("1;40196", 1);

            Assert.Equal(oldFrame, newFrame);
        }

        [Fact]
        public async Task Tcp_ReadHoldingRegister_NewAndOldFormat_ShouldProduceSameFrame()
        {
            string oldFrame = await CaptureTcpReadRegisterFrameAsync("1;3;401", 1, false);
            string newFrame = await CaptureTcpReadRegisterFrameAsync("1;40402", 1, false);

            Assert.Equal(oldFrame, newFrame);
        }

        [Fact]
        public async Task Tcp_ReadInputRegister_NewAndOldFormat_ShouldProduceSameFrame()
        {
            string oldFrame = await CaptureTcpReadRegisterFrameAsync("1;4;3005", 1, true);
            string newFrame = await CaptureTcpReadRegisterFrameAsync("1;33006", 1, true);

            Assert.Equal(oldFrame, newFrame);
        }

        [Fact]
        public async Task Rtu_WriteSingleRegister_NewAndOldFormat_ShouldProduceSameFrame()
        {
            string oldFrame = await CaptureRtuWriteRegisterFrameAsync("1;6;195", 1);
            string newFrame = await CaptureRtuWriteRegisterFrameAsync("1;40196", 1);

            Assert.Equal(oldFrame, newFrame);
        }

        [Fact]
        public async Task Rtu_ReadHoldingRegister_NewAndOldFormat_ShouldProduceSameFrame()
        {
            string oldFrame = await CaptureRtuReadRegisterFrameAsync("1;3;401", 1, false);
            string newFrame = await CaptureRtuReadRegisterFrameAsync("1;40402", 1, false);

            Assert.Equal(oldFrame, newFrame);
        }

        [Fact]
        public async Task Rtu_ReadInputRegister_NewAndOldFormat_ShouldProduceSameFrame()
        {
            string oldFrame = await CaptureRtuReadRegisterFrameAsync("1;4;3005", 1, true);
            string newFrame = await CaptureRtuReadRegisterFrameAsync("1;33006", 1, true);

            Assert.Equal(oldFrame, newFrame);
        }

        private static async Task<string> CaptureTcpWriteRegisterFrameAsync(string address, ushort value)
        {
            var transport = new CapturingModbusTransport(true);
            var client = new TestableModbusTcpClientBase(transport);

            var result = await client.WriteAsync(address, value);

            Assert.True(result.IsSuccess, result.Message);
            return transport.LastRequestFrameHex;
        }

        private static async Task<string> CaptureTcpReadRegisterFrameAsync(string address, int registerCount, bool isInputRegister)
        {
            var transport = new CapturingModbusTransport(true);
            var client = new TestableModbusTcpClientBase(transport);

            var result = await client.CaptureRegisterReadAsync(
                address,
                registerCount,
                isInputRegister ? DataTypeEnums.UInt16 : DataTypeEnums.UInt16);

            Assert.True(result.IsSuccess, result.Message);
            return transport.LastRequestFrameHex;
        }

        private static async Task<string> CaptureRtuWriteRegisterFrameAsync(string address, ushort value)
        {
            var transport = new CapturingModbusTransport(false);
            var client = new TestableModbusRtuClientBase(transport);

            var result = await client.WriteAsync(address, value);

            Assert.True(result.IsSuccess, result.Message);
            return transport.LastRequestFrameHex;
        }

        private static async Task<string> CaptureRtuReadRegisterFrameAsync(string address, int registerCount, bool isInputRegister)
        {
            var transport = new CapturingModbusTransport(false);
            var client = new TestableModbusRtuClientBase(transport);

            var result = await client.CaptureRegisterReadAsync(
                address,
                registerCount,
                isInputRegister ? DataTypeEnums.UInt16 : DataTypeEnums.UInt16);

            Assert.True(result.IsSuccess, result.Message);
            return transport.LastRequestFrameHex;
        }

        private sealed class TestableModbusTcpClientBase : ModbusTcpClientBase
        {
            public TestableModbusTcpClientBase(DeviceMessageTransport transport) : base(transport)
            {
            }

            public ValueTask<OperationResult<byte[]>> CaptureRegisterReadAsync(string address, int registerCount, DataTypeEnums dataType)
            {
                return base.ReadAsync(address, registerCount * 2, dataType, false);
            }
        }

        private sealed class TestableModbusRtuClientBase : ModbusRtuClientBase
        {
            public TestableModbusRtuClientBase(DeviceMessageTransport transport) : base(transport)
            {
            }

            public ValueTask<OperationResult<byte[]>> CaptureRegisterReadAsync(string address, int registerCount, DataTypeEnums dataType)
            {
                return base.ReadAsync(address, registerCount * 2, dataType, false);
            }
        }

        private sealed class CapturingModbusTransport : DeviceMessageTransport
        {
            private readonly bool _isTcp;

            public CapturingModbusTransport(bool isTcp) : base(new DummyStreamResource())
            {
                _isTcp = isTcp;
            }

            public byte[] LastRequestFrame { get; private set; }

            public string LastRequestFrameHex
            {
                get { return LastRequestFrame == null ? string.Empty : string.Join(" ", Array.ConvertAll(LastRequestFrame, t => t.ToString("X2"))); }
            }

            public override Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(IDeviceReadWriteMessage request)
            {
                return Task.FromResult(CreateSuccessResult(request));
            }

            public override Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(IDeviceReadWriteMessage request)
            {
                return Task.FromResult(CreateSuccessResult(request));
            }

            private OperationResult<IDeviceReadWriteMessage> CreateSuccessResult(IDeviceReadWriteMessage request)
            {
                LastRequestFrame = (byte[])request.ProtocolMessageFrame.Clone();

                byte[] responseFrame = _isTcp
                    ? BuildTcpResponseFrame(LastRequestFrame)
                    : BuildRtuResponseFrame(LastRequestFrame);

                var responseMessage = new DeviceReadWriteMessage
                {
                    ProtocolMessageFrame = responseFrame,
                    ProtocolResponseLength = request.ProtocolResponseLength,
                    RegisterAddress = request.RegisterAddress,
                    RegisterCount = request.RegisterCount
                };

                var result = new OperationResult<IDeviceReadWriteMessage>
                {
                    IsSuccess = true,
                    ResultValue = responseMessage
                };
                result.Requsts.Add(string.Join(" ", Array.ConvertAll(LastRequestFrame, t => t.ToString("X2"))));
                result.Responses.Add(string.Join(" ", Array.ConvertAll(responseFrame, t => t.ToString("X2"))));
                return result.Complete();
            }

            private static byte[] BuildTcpResponseFrame(byte[] requestFrame)
            {
                byte functionCode = requestFrame[7];
                switch (functionCode)
                {
                    case 0x01:
                    case 0x02:
                    {
                        ushort bitCount = ToUInt16BigEndian(requestFrame, 10);
                        int byteCount = (bitCount + 7) / 8;
                        return BuildTcpReadResponse(requestFrame, functionCode, byteCount);
                    }
                    case 0x03:
                    case 0x04:
                    {
                        ushort registerCount = ToUInt16BigEndian(requestFrame, 10);
                        return BuildTcpReadResponse(requestFrame, functionCode, registerCount * 2);
                    }
                    case 0x05:
                    case 0x06:
                    {
                        var response = new byte[12];
                        Array.Copy(requestFrame, 0, response, 0, 12);
                        return response;
                    }
                    case 0x0F:
                    case 0x10:
                    {
                        var response = new byte[12];
                        response[0] = requestFrame[0];
                        response[1] = requestFrame[1];
                        response[2] = requestFrame[2];
                        response[3] = requestFrame[3];
                        response[4] = 0x00;
                        response[5] = 0x06;
                        response[6] = requestFrame[6];
                        response[7] = requestFrame[7];
                        response[8] = requestFrame[8];
                        response[9] = requestFrame[9];
                        response[10] = requestFrame[10];
                        response[11] = requestFrame[11];
                        return response;
                    }
                    default:
                        throw new NotSupportedException("Unsupported Modbus TCP function code in test transport.");
                }
            }

            private static byte[] BuildTcpReadResponse(byte[] requestFrame, byte functionCode, int dataByteCount)
            {
                var response = new byte[9 + dataByteCount];
                ushort length = (ushort)(3 + dataByteCount);

                response[0] = requestFrame[0];
                response[1] = requestFrame[1];
                response[2] = requestFrame[2];
                response[3] = requestFrame[3];
                response[4] = (byte)(length >> 8);
                response[5] = (byte)(length & 0xFF);
                response[6] = requestFrame[6];
                response[7] = functionCode;
                response[8] = (byte)dataByteCount;
                return response;
            }

            private static byte[] BuildRtuResponseFrame(byte[] requestFrame)
            {
                byte functionCode = requestFrame[1];
                switch (functionCode)
                {
                    case 0x01:
                    case 0x02:
                    {
                        ushort bitCount = ToUInt16BigEndian(requestFrame, 4);
                        int byteCount = (bitCount + 7) / 8;
                        return BuildRtuReadResponse(requestFrame[0], functionCode, byteCount);
                    }
                    case 0x03:
                    case 0x04:
                    {
                        ushort registerCount = ToUInt16BigEndian(requestFrame, 4);
                        return BuildRtuReadResponse(requestFrame[0], functionCode, registerCount * 2);
                    }
                    case 0x05:
                    case 0x06:
                    {
                        var response = new byte[requestFrame.Length];
                        Array.Copy(requestFrame, response, requestFrame.Length);
                        return response;
                    }
                    case 0x0F:
                    case 0x10:
                    {
                        var payload = new byte[6];
                        payload[0] = requestFrame[0];
                        payload[1] = requestFrame[1];
                        payload[2] = requestFrame[2];
                        payload[3] = requestFrame[3];
                        payload[4] = requestFrame[4];
                        payload[5] = requestFrame[5];
                        byte[] crc = CalculateModbusCrc(payload);
                        var response = new byte[8];
                        Array.Copy(payload, response, 6);
                        response[6] = crc[0];
                        response[7] = crc[1];
                        return response;
                    }
                    default:
                        throw new NotSupportedException("Unsupported Modbus RTU function code in test transport.");
                }
            }

            private static byte[] BuildRtuReadResponse(byte station, byte functionCode, int dataByteCount)
            {
                var payload = new byte[3 + dataByteCount];
                payload[0] = station;
                payload[1] = functionCode;
                payload[2] = (byte)dataByteCount;

                byte[] crc = CalculateModbusCrc(payload);
                var response = new byte[payload.Length + 2];
                Array.Copy(payload, response, payload.Length);
                response[payload.Length] = crc[0];
                response[payload.Length + 1] = crc[1];
                return response;
            }

            private static ushort ToUInt16BigEndian(byte[] data, int offset)
            {
                return (ushort)((data[offset] << 8) | data[offset + 1]);
            }

            private static byte[] CalculateModbusCrc(byte[] data)
            {
                ushort crc = 0xFFFF;
                for (int i = 0; i < data.Length; i++)
                {
                    crc ^= data[i];
                    for (int j = 0; j < 8; j++)
                    {
                        bool lsb = (crc & 0x0001) != 0;
                        crc >>= 1;
                        if (lsb)
                        {
                            crc ^= 0xA001;
                        }
                    }
                }

                return new[] { (byte)(crc & 0xFF), (byte)(crc >> 8) };
            }
        }

        private sealed class DummyStreamResource : IStreamResource
        {
            public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(1);

            public bool Connected
            {
                get { return true; }
            }

            public void Dispose()
            {
            }

            public Task<OperationResult<int>> Receive(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult.CreateFailedResult<int>("Dummy stream should not receive directly."));
            }

            public Task<OperationResult> Send(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationResult.CreateFailedResult("Dummy stream should not send directly."));
            }

            public void StreamClose()
            {
            }
        }
    }
}

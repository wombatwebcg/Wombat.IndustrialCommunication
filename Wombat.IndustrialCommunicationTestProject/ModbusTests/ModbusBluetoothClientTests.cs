using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.Extensions.Bluetooth;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.ModbusTests
{
    public class ModbusBluetoothClientTests
    {
        [Fact]
        public async Task ReadHoldingRegisterAsync_ShouldReturnValue()
        {
            var channel = new FakeBluetoothChannel();
            var client = new ModbusRtuBluetoothClient(channel);

            var connect = await client.ConnectAsync();
            Assert.True(connect.IsSuccess);

            var readResult = await client.ReadHoldingRegisterAsync(1, 0);
            Assert.True(readResult.IsSuccess, readResult.Message);
            Assert.Equal((ushort)42, readResult.ResultValue);
            Assert.True(client.Connected);
        }

        [Fact]
        public async Task ShortConnection_WriteHoldingRegisterAsync_ShouldAutoDisconnect()
        {
            var channel = new FakeBluetoothChannel();
            var client = new ModbusRtuBluetoothClient(channel) { IsLongConnection = false };

            var writeResult = await client.WriteHoldingRegisterAsync(1, 0, 123);
            Assert.True(writeResult.IsSuccess, writeResult.Message);
            Assert.False(client.Connected);
            Assert.True(channel.ConnectCount >= 1);
            Assert.True(channel.DisconnectCount >= 1);
        }

        private sealed class FakeBluetoothChannel : IBluetoothChannel
        {
            private readonly Queue<byte[]> _responses = new Queue<byte[]>();

            public bool Connected { get; private set; }

            public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(2);

            public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(2);

            public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(2);

            public int ConnectCount { get; private set; }

            public int DisconnectCount { get; private set; }

            public Task<OperationResult> ConnectAsync(CancellationToken cancellationToken)
            {
                ConnectCount++;
                Connected = true;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult> DisconnectAsync()
            {
                DisconnectCount++;
                Connected = false;
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult> SendAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
            {
                if (!Connected)
                {
                    return Task.FromResult(OperationResult.CreateFailedResult("not connected"));
                }

                var request = new byte[size];
                Array.Copy(buffer, offset, request, 0, size);
                EnqueueResponse(request);
                return Task.FromResult(OperationResult.CreateSuccessResult());
            }

            public Task<OperationResult<int>> ReceiveAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
            {
                if (!Connected)
                {
                    return Task.FromResult(OperationResult.CreateFailedResult<int>("not connected"));
                }

                if (_responses.Count == 0)
                {
                    return Task.FromResult(OperationResult.CreateFailedResult<int>("no response"));
                }

                var response = _responses.Dequeue();
                var length = Math.Min(size, response.Length);
                Array.Copy(response, 0, buffer, offset, length);
                return Task.FromResult(OperationResult.CreateSuccessResult(length));
            }

            public void Dispose()
            {
                Connected = false;
                _responses.Clear();
            }

            private void EnqueueResponse(byte[] request)
            {
                if (request.Length < 2)
                {
                    _responses.Enqueue(BuildErrorResponse(1, 3, 4));
                    return;
                }

                var station = request[0];
                var functionCode = request[1];
                if (functionCode == 3)
                {
                    _responses.Enqueue(BuildReadHoldingRegisterResponse(station, 42));
                    return;
                }

                if (functionCode == 6 && request.Length >= 6)
                {
                    var response = request.Take(6).ToArray();
                    _responses.Enqueue(AppendCrc(response));
                    return;
                }

                _responses.Enqueue(BuildErrorResponse(station, functionCode, 1));
            }

            private static byte[] BuildReadHoldingRegisterResponse(byte station, ushort value)
            {
                var payload = new byte[]
                {
                    station,
                    3,
                    2,
                    (byte)(value >> 8),
                    (byte)(value & 0xFF)
                };
                return AppendCrc(payload);
            }

            private static byte[] BuildErrorResponse(byte station, byte functionCode, byte errorCode)
            {
                var payload = new byte[]
                {
                    station,
                    (byte)(functionCode | 0x80),
                    errorCode
                };
                return AppendCrc(payload);
            }

            private static byte[] AppendCrc(byte[] payload)
            {
                ushort crc = 0xFFFF;
                for (int i = 0; i < payload.Length; i++)
                {
                    crc ^= payload[i];
                    for (int j = 0; j < 8; j++)
                    {
                        if ((crc & 0x0001) != 0)
                        {
                            crc = (ushort)((crc >> 1) ^ 0xA001);
                        }
                        else
                        {
                            crc >>= 1;
                        }
                    }
                }

                var result = new byte[payload.Length + 2];
                Array.Copy(payload, result, payload.Length);
                result[result.Length - 2] = (byte)(crc & 0xFF);
                result[result.Length - 1] = (byte)(crc >> 8);
                return result;
            }
        }
    }
}

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ModbusTests
{
    public class ModbusTcpServerTests
    {
        private const string TestIp = "127.0.0.1";
        private const byte Station = 1;

        [Fact]
        public async Task Read_All_Register_Types_Should_Return_Expected_Values()
        {
            var port = GetFreePort();
            var server = new ModbusTcpServer(TestIp, port) { SlaveId = Station };
            var client = new ModbusTcpClient(TestIp, port);

            try
            {
                server.DataStore.CoilDiscretes[0] = true;
                server.DataStore.CoilDiscretes[1] = false;
                server.DataStore.InputDiscretes[0] = false;
                server.DataStore.InputDiscretes[1] = true;
                server.DataStore.HoldingRegisters[0] = 0x1234;
                server.DataStore.HoldingRegisters[1] = 0x5678;
                server.DataStore.InputRegisters[0] = 0x9ABC;
                server.DataStore.InputRegisters[1] = 0xDEF0;

                var startResult = await server.StartAsync();
                Assert.True(startResult.IsSuccess, $"启动服务失败: {startResult.Message}");

                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");

                var coils = await client.ReadCoilsAsync(Station, 0, 2);
                Assert.True(coils.IsSuccess, $"读取线圈失败: {coils.Message}");
                Assert.Equal(new[] { true, false }, coils.ResultValue);

                var inputs = await client.ReadDiscreteInputsAsync(Station, 0, 2);
                Assert.True(inputs.IsSuccess, $"读取离散输入失败: {inputs.Message}");
                Assert.Equal(new[] { false, true }, inputs.ResultValue);

                var holding = await client.ReadHoldingRegistersAsync(Station, 0, 2);
                Assert.True(holding.IsSuccess, $"读取保持寄存器失败: {holding.Message}");
                Assert.Equal(new ushort[] { 0x1234, 0x5678 }, holding.ResultValue);

                var inputRegisters = await client.ReadInputRegistersAsync(Station, 0, 2);
                Assert.True(inputRegisters.IsSuccess, $"读取输入寄存器失败: {inputRegisters.Message}");
                Assert.Equal(new ushort[] { 0x9ABC, 0xDEF0 }, inputRegisters.ResultValue);
            }
            finally
            {
                await client.DisconnectAsync();
                await server.StopAsync();
                client.Dispose();
                server.Dispose();
            }
        }

        [Fact]
        public async Task Write_Coil_And_HoldingRegister_Should_Update_Server_DataStore()
        {
            var port = GetFreePort();
            var server = new ModbusTcpServer(TestIp, port) { SlaveId = Station };
            var client = new ModbusTcpClient(TestIp, port);

            try
            {
                var startResult = await server.StartAsync();
                Assert.True(startResult.IsSuccess, $"启动服务失败: {startResult.Message}");

                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");

                var writeCoil = await client.WriteCoilAsync(Station, 0, true);
                Assert.True(writeCoil.IsSuccess, $"写入线圈失败: {writeCoil.Message}");

                var writeCoils = await client.WriteCoilsAsync(Station, 1, new[] { false, true, true });
                Assert.True(writeCoils.IsSuccess, $"写入多个线圈失败: {writeCoils.Message}");

                var writeRegister = await client.WriteHoldingRegisterAsync(Station, 0, 0x1357);
                Assert.True(writeRegister.IsSuccess, $"写入保持寄存器失败: {writeRegister.Message}");

                var writeRegisters = await client.WriteHoldingRegistersAsync(Station, 1, new ushort[] { 0x2468, 0xAAAA });
                Assert.True(writeRegisters.IsSuccess, $"写入多个保持寄存器失败: {writeRegisters.Message}");

                Assert.True(server.DataStore.CoilDiscretes[0]);
                Assert.False(server.DataStore.CoilDiscretes[1]);
                Assert.True(server.DataStore.CoilDiscretes[2]);
                Assert.True(server.DataStore.CoilDiscretes[3]);
                Assert.Equal(0x1357, server.DataStore.HoldingRegisters[0]);
                Assert.Equal(0x2468, server.DataStore.HoldingRegisters[1]);
                Assert.Equal(0xAAAA, server.DataStore.HoldingRegisters[2]);
            }
            finally
            {
                await client.DisconnectAsync();
                await server.StopAsync();
                client.Dispose();
                server.Dispose();
            }
        }

        [Fact]
        public async Task SingleConnection_Should_Handle_Sticky_And_Split_Modbus_Frames()
        {
            var port = GetFreePort();
            var server = new ModbusTcpServer(TestIp, port) { SlaveId = Station };

            try
            {
                server.DataStore.HoldingRegisters[0] = 0x1234;
                server.DataStore.HoldingRegisters[1] = 0x5678;

                var startResult = await server.StartAsync();
                Assert.True(startResult.IsSuccess, $"启动服务失败: {startResult.Message}");

                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
                using var stream = client.GetStream();

                var read0 = new ModbusTcpRequest(0x0100, Station, 0x03, 0, 1).ProtocolMessageFrame;
                var read1 = new ModbusTcpRequest(0x0101, Station, 0x03, 1, 1).ProtocolMessageFrame;

                var sticky = new byte[read0.Length + read1.Length];
                Buffer.BlockCopy(read0, 0, sticky, 0, read0.Length);
                Buffer.BlockCopy(read1, 0, sticky, read0.Length, read1.Length);

                await stream.WriteAsync(sticky, 0, sticky.Length).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);

                var response0 = await ReadModbusFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                var response1 = await ReadModbusFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);

                var parsed0 = new ModbusTcpResponse(response0);
                var parsed1 = new ModbusTcpResponse(response1);

                Assert.Equal(new byte[] { 0x01, 0x00 }, new[] { response0[0], response0[1] });
                Assert.Equal(new byte[] { 0x01, 0x01 }, new[] { response1[0], response1[1] });
                Assert.Equal(new byte[] { 0x12, 0x34 }, parsed0.Data);
                Assert.Equal(new byte[] { 0x56, 0x78 }, parsed1.Data);

                var write = new ModbusTcpRequest(0x0102, Station, 0x06, 1, 1, new byte[] { 0x9A, 0xBC }).ProtocolMessageFrame;
                await stream.WriteAsync(write, 0, 5).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                await Task.Delay(30).ConfigureAwait(false);
                await stream.WriteAsync(write, 5, write.Length - 5).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);

                var writeResponse = await ReadModbusFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                _ = new ModbusTcpResponse(writeResponse);

                Assert.Equal(new byte[] { 0x01, 0x02 }, new[] { writeResponse[0], writeResponse[1] });
                Assert.Equal(0x9ABC, server.DataStore.HoldingRegisters[1]);
            }
            finally
            {
                await server.StopAsync();
                server.Dispose();
            }
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<byte[]> ReadModbusFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var header = await ReadExactAsync(stream, 6, cancellationToken).ConfigureAwait(false);
            int frameLength = 6 + ((header[4] << 8) | header[5]);
            var rest = await ReadExactAsync(stream, frameLength - 6, cancellationToken).ConfigureAwait(false);
            var frame = new byte[frameLength];
            Buffer.BlockCopy(header, 0, frame, 0, header.Length);
            Buffer.BlockCopy(rest, 0, frame, header.Length, rest.Length);
            return frame;
        }

        private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
        {
            var buffer = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = await stream.ReadAsync(buffer, offset, length - offset, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new InvalidOperationException("连接已关闭，未读满预期长度");
                }

                offset += read;
            }

            return buffer;
        }
    }
}

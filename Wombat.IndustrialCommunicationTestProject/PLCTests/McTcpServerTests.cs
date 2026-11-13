using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.PLCTests
{
    /// <summary>
    /// MC 服务端回环测试：McClient ↔ McTcpServer，覆盖 1E / 3E / 4E 的字/位读写、批量、错误码与快照。
    /// 回环只能证明客户端与服务端互相自洽，真机差异（4E 保留字节、1E 位编码）仍需现场核对。
    /// </summary>
    public class McTcpServerTests
    {
        public static IEnumerable<object[]> Frames
        {
            get
            {
                yield return new object[] { McFrameType.A1E, McSeries.A };
                yield return new object[] { McFrameType.Q3E, McSeries.Q };
                yield return new object[] { McFrameType.Q4E, McSeries.Q };
            }
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task<(McTcpServer Server, McClient Client, int Port)> StartAsync(McFrameType frameType,
            McSeries series)
        {
            int port = GetFreePort();
            var server = new McTcpServer("127.0.0.1", port);
            var start = await server.ListenAsync();
            Assert.True(start.IsSuccess, start.Message);

            var client = new McClient("127.0.0.1", port, frameType, series) { Timeout = TimeSpan.FromSeconds(5) };
            var connect = await client.ConnectAsync();
            Assert.True(connect.IsSuccess, connect.Message);

            return (server, client, port);
        }

        [Theory]
        [MemberData(nameof(Frames))]
        public async Task WordWriteRead_ShouldRoundTrip(McFrameType frameType, McSeries series)
        {
            var context = await StartAsync(frameType, series);
            using (context.Server)
            using (context.Client)
            {
                var write = await context.Client.WriteAsync("D100", new ushort[] { 0x1234, 0x5678, 0xABCD });
                Assert.True(write.IsSuccess, write.Message);

                var read = await context.Client.ReadUInt16Async("D100", 3).AsTask();
                Assert.True(read.IsSuccess, read.Message);
                Assert.Equal(new ushort[] { 0x1234, 0x5678, 0xABCD }, read.ResultValue);

                // 服务端数据区与客户端看到的一致（小端 2 字节/点）
                var area = context.Server.ReadArea(McDeviceKind.D, 100, 3);
                Assert.True(area.IsSuccess, area.Message);
                Assert.Equal(new byte[] { 0x34, 0x12, 0x78, 0x56, 0xCD, 0xAB }, area.ResultValue);
            }
        }

        [Theory]
        [MemberData(nameof(Frames))]
        public async Task BitWriteRead_ShouldRoundTrip(McFrameType frameType, McSeries series)
        {
            var context = await StartAsync(frameType, series);
            using (context.Server)
            using (context.Client)
            {
                var write = await context.Client.WriteAsync("M100", new[] { true, false, true, true });
                Assert.True(write.IsSuccess, write.Message);

                var read = await context.Client.ReadBooleanAsync("M100", 4).AsTask();
                Assert.True(read.IsSuccess, read.Message);
                Assert.Equal(new[] { true, false, true, true }, read.ResultValue);

                // 相邻点不受影响（1E 的位写会整块回写 M96~M111）
                var neighbour = await context.Client.ReadBooleanAsync("M104", 2).AsTask();
                Assert.True(neighbour.IsSuccess, neighbour.Message);
                Assert.Equal(new[] { false, false }, neighbour.ResultValue);
            }
        }

        [Theory]
        [MemberData(nameof(Frames))]
        public async Task BatchReadWrite_ShouldKeepConsecutiveWords(McFrameType frameType, McSeries series)
        {
            var context = await StartAsync(frameType, series);
            using (context.Server)
            using (context.Client)
            {
                var values = new ushort[20];
                for (int i = 0; i < values.Length; i++)
                    values[i] = (ushort)(0x1000 + i);

                var write = await context.Client.WriteAsync("D200", values);
                Assert.True(write.IsSuccess, write.Message);

                var read = await context.Client.ReadUInt16Async("D200", values.Length).AsTask();
                Assert.True(read.IsSuccess, read.Message);
                Assert.Equal(values, read.ResultValue);
            }
        }

        [Theory]
        [MemberData(nameof(Frames))]
        public async Task OutOfRangeRead_ShouldReturnEndCode(McFrameType frameType, McSeries series)
        {
            var context = await StartAsync(frameType, series);
            using (context.Server)
            using (context.Client)
            {
                var read = await context.Client.ReadUInt16Async("D9000", 2).AsTask();

                Assert.False(read.IsSuccess);
                // 3E/4E 透出原始结束码；1E 只有 1 字节结束码 + 异常码
                Assert.Contains(frameType == McFrameType.A1E ? "5B" : "C056", read.Message);
            }
        }

        [Fact]
        public async Task IqR_LongTimer_ShouldUseFourWordsPerPoint()
        {
            int port = GetFreePort();
            using var server = new McTcpServer("127.0.0.1", port);
            Assert.True((await server.ListenAsync()).IsSuccess);

            using var client = new McClient("127.0.0.1", port, McFrameType.Q3E, McSeries.IqR)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            var connect = await client.ConnectAsync();
            Assert.True(connect.IsSuccess, connect.Message);

            var write = await client.WriteAsync("LTN10", (ushort)0x1234);
            Assert.True(write.IsSuccess, write.Message);

            var read = await client.ReadUInt16Async("LTN10", 1).AsTask();
            Assert.True(read.IsSuccess, read.Message);
            Assert.Equal(0x1234, read.ResultValue[0]);

            // 协议上每个元素占 4 个字，服务端数据区按 8 字节/元素存储
            var words = server.DataStore.ReadWords(McDeviceKind.LTN, 10, 4);
            Assert.Equal(new byte[] { 0x34, 0x12, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, words);

            // 对外数据区接口把每个元素的当前值压紧成 2 字节
            var area = server.ReadArea(McDeviceKind.LTN, 10, 1);
            Assert.True(area.IsSuccess, area.Message);
            Assert.Equal(new byte[] { 0x34, 0x12 }, area.ResultValue);
        }

        [Fact]
        public async Task DifferentFrames_ShouldShareSameDataArea()
        {
            int port = GetFreePort();
            using var server = new McTcpServer("127.0.0.1", port);
            Assert.True((await server.ListenAsync()).IsSuccess);

            using var client3E = new McClient("127.0.0.1", port, McFrameType.Q3E, McSeries.Q)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            using var client1E = new McClient("127.0.0.1", port, McFrameType.A1E, McSeries.A)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            Assert.True((await client3E.ConnectAsync()).IsSuccess);
            Assert.True((await client1E.ConnectAsync()).IsSuccess);

            Assert.True((await client3E.WriteAsync("D100", (ushort)0x1234)).IsSuccess);
            var from1E = await client1E.ReadUInt16Async("D100", 1).AsTask();
            Assert.True(from1E.IsSuccess, from1E.Message);
            Assert.Equal(0x1234, from1E.ResultValue[0]);

            Assert.True((await client1E.WriteAsync("M50", true)).IsSuccess);
            var from3E = await client3E.ReadBooleanAsync("M50", 1).AsTask();
            Assert.True(from3E.IsSuccess, from3E.Message);
            Assert.True(from3E.ResultValue[0]);
        }

        [Fact]
        public void ReadWriteArea_ShouldExposeBitAndWordAreas()
        {
            int port = GetFreePort();
            using var server = new McTcpServer("127.0.0.1", port);

            Assert.True(server.Write("D10", (ushort)0x5566).IsSuccess);
            var word = server.ReadArea(McDeviceKind.D, 10, 1);
            Assert.True(word.IsSuccess, word.Message);
            Assert.Equal(new byte[] { 0x66, 0x55 }, word.ResultValue);

            Assert.True(server.WriteArea(McDeviceKind.M, 20, new byte[] { 1, 0, 1 }).IsSuccess);
            var bits = server.ReadArea(McDeviceKind.M, 20, 3);
            Assert.True(bits.IsSuccess, bits.Message);
            Assert.Equal(new byte[] { 1, 0, 1 }, bits.ResultValue);

            var written = server.ReadBoolean("M20", 3);
            Assert.True(written.IsSuccess, written.Message);
            Assert.Equal(new[] { true, false, true }, written.ResultValue);
        }

        [Fact]
        public async Task MalformedWriteLength_ShouldReturnC058()
        {
            int port = GetFreePort();
            using var server = new McTcpServer("127.0.0.1", port);
            Assert.True((await server.ListenAsync()).IsSuccess);

            // 3E 字写：声明 2 点（4 字节数据），实际只带 1 个字
            byte[] request =
            {
                0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
                0x0E, 0x00,                                     // 请求数据长度 = 14
                0x0A, 0x00,                                     // 监视定时器
                0x01, 0x14,                                     // 指令 1401（批量写）
                0x00, 0x00,                                     // 子指令 0000（字）
                0x64, 0x00, 0x00,                               // 设备号 100
                0xA8,                                           // 软元件码 D
                0x02, 0x00,                                     // 点数 2
                0x11, 0x22                                      // 只有 1 个字的数据
            };

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(IPAddress.Loopback, port);
            var stream = tcp.GetStream();
            await stream.WriteAsync(request, 0, request.Length);

            var buffer = new byte[11];
            int read = 0;
            while (read < buffer.Length)
            {
                int count = await stream.ReadAsync(buffer, read, buffer.Length - read);
                Assert.True(count > 0, "服务端未返回响应");
                read += count;
            }

            Assert.Equal(0xD0, buffer[0]);
            Assert.Equal(McCommonMethods.EndCodeRequestDataLengthError, (ushort)(buffer[9] | (buffer[10] << 8)));
        }

        [Fact]
        public async Task Snapshot_ShouldRestoreAfterRestart()
        {
            int port = GetFreePort();
            string path = Path.Combine(Path.GetTempPath(), $"mc-snapshot-{Guid.NewGuid():N}.snapshot");

            try
            {
                using (var server = new McTcpServer("127.0.0.1", port))
                {
                    server.EnableSnapshotPersistence = true;
                    server.SnapshotFilePath = path;
                    Assert.True((await server.ListenAsync()).IsSuccess);
                    Assert.True(server.Write("D10", (ushort)0x5566).IsSuccess);
                    Assert.True(server.SaveSnapshot().IsSuccess, "快照保存失败");
                    await server.ShutdownAsync();
                }

                Assert.True(File.Exists(path));

                using (var server = new McTcpServer("127.0.0.1", port))
                {
                    server.EnableSnapshotPersistence = true;
                    server.SnapshotFilePath = path;
                    Assert.True((await server.ListenAsync()).IsSuccess);

                    var read = server.ReadUInt16("D10");
                    Assert.True(read.IsSuccess, read.Message);
                    Assert.Equal(0x5566, read.ResultValue);
                    await server.ShutdownAsync();
                }
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    public class S7TcpServerSnapshotTests
    {
        [Fact]
        public void Snapshot_Should_Save_And_Load_For_S7TcpServer()
        {
            var snapshotPath = BuildSnapshotPath();
            try
            {
                var firstPort = GetFreePort();
                using (var server = new S7TcpServer("127.0.0.1", firstPort))
                {
                    server.SnapshotFilePath = snapshotPath;
                    server.EnableSnapshotPersistence = true;

                    var createDbResult = server.CreateDataBlock(1, 64);
                    Assert.True(createDbResult.IsSuccess, createDbResult.Message);
                    Assert.True(server.WriteDB(1, 0, new byte[] { 0x11, 0x22, 0x33 }).IsSuccess);
                    Assert.True(server.WriteMerkers(0, new byte[] { 0x5A }).IsSuccess);

                    server.Listen();
                    server.Shutdown();
                }

                Assert.True(File.Exists(snapshotPath));

                var secondPort = GetFreePort();
                using (var server = new S7TcpServer("127.0.0.1", secondPort))
                {
                    server.SnapshotFilePath = snapshotPath;
                    server.EnableSnapshotPersistence = true;

                    server.Listen();

                    var dbRead = server.ReadDB(1, 0, 3);
                    Assert.True(dbRead.IsSuccess, dbRead.Message);
                    Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, dbRead.ResultValue);

                    var merkerRead = server.ReadMerkers(0, 1);
                    Assert.True(merkerRead.IsSuccess, merkerRead.Message);
                    Assert.Equal((byte)0x5A, merkerRead.ResultValue[0]);

                    server.Shutdown();
                }
            }
            finally
            {
                DeleteSnapshotFiles(snapshotPath);
            }
        }

        [Fact]
        public void ResetDataAndDeleteSnapshot_Should_Clear_Data_And_Remove_File_For_S7TcpServer()
        {
            var snapshotPath = BuildSnapshotPath();
            try
            {
                var port = GetFreePort();
                using (var server = new S7TcpServer("127.0.0.1", port))
                {
                    server.SnapshotFilePath = snapshotPath;
                    server.EnableSnapshotPersistence = true;

                    Assert.True(server.CreateDataBlock(1, 64).IsSuccess);
                    Assert.True(server.WriteDB(1, 0, new byte[] { 0x44, 0x55 }).IsSuccess);
                    Assert.True(server.WriteMerkers(0, new byte[] { 0xAA }).IsSuccess);

                    server.Listen();
                    server.Shutdown();

                    Assert.True(File.Exists(snapshotPath));

                    var resetResult = server.ResetDataAndDeleteSnapshot();
                    Assert.True(resetResult.IsSuccess, resetResult.Message);

                    Assert.False(File.Exists(snapshotPath));
                    Assert.Empty(server.GetDataBlockNumbers());

                    var merkerRead = server.ReadMerkers(0, 1);
                    Assert.True(merkerRead.IsSuccess, merkerRead.Message);
                    Assert.Equal((byte)0x00, merkerRead.ResultValue[0]);
                }
            }
            finally
            {
                DeleteSnapshotFiles(snapshotPath);
            }
        }

        [Fact]
        public async Task SingleConnection_Should_Handle_Sticky_Reads_And_Split_Writes_For_S7TcpServer()
        {
            var port = GetFreePort();
            using var server = new S7TcpServer("127.0.0.1", port);
            server.SiemensVersion = SiemensVersion.S7_1200;

            Assert.True(server.CreateDataBlock(1, 64).IsSuccess);
            Assert.True(server.WriteDB(1, 0, new byte[] { 0x11, 0x22, 0x33 }).IsSuccess);

            var startResult = await server.StartAsync().ConfigureAwait(false);
            Assert.True(startResult.IsSuccess, startResult.Message);

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            using var stream = client.GetStream();

            await WriteAndReadHandshakeAsync(stream, (byte[])SiemensConstant.Command1.Clone(), CancellationToken.None).ConfigureAwait(false);
            await WriteAndReadHandshakeAsync(stream, (byte[])SiemensConstant.Command2.Clone(), CancellationToken.None).ConfigureAwait(false);

            var read1 = new S7ReadRequest("DB1.DBB0", 0, 1, false, 0x1001);
            var read2 = new S7ReadRequest("DB1.DBB1", 0, 1, false, 0x1002);
            var stickyReads = read1.ProtocolMessageFrame.Concat(read2.ProtocolMessageFrame).ToArray();

            await stream.WriteAsync(stickyReads, 0, stickyReads.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            var response1 = await ReadTpktFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
            var response2 = await ReadTpktFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);

            var parsed1 = S7ReadResponse.Parse(response1, read1.Items);
            var parsed2 = S7ReadResponse.Parse(response2, read2.Items);

            Assert.True(parsed1.IsSuccess, parsed1.Message);
            Assert.True(parsed2.IsSuccess, parsed2.Message);
            Assert.Equal(new byte[] { 0x11 }, parsed1.ResultValue.Items[0].Data);
            Assert.Equal(new byte[] { 0x22 }, parsed2.ResultValue.Items[0].Data);

            var write = new S7WriteRequest("DB1.DBB2", 0, new byte[] { 0x5A }, false, 0x1003);
            await stream.WriteAsync(write.ProtocolMessageFrame, 0, 7).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            await Task.Delay(30).ConfigureAwait(false);
            await stream.WriteAsync(write.ProtocolMessageFrame, 7, write.ProtocolMessageFrame.Length - 7).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            var writeResponse = await ReadTpktFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
            Assert.NotNull(writeResponse);
            Assert.Equal((byte)0x32, writeResponse[7]);
            Assert.Equal((byte)0x03, writeResponse[8]);
            Assert.Equal((byte)0x05, writeResponse[19]);

            var dbRead = server.ReadDB(1, 2, 1);
            Assert.True(dbRead.IsSuccess, dbRead.Message);
            Assert.Equal((byte)0x5A, dbRead.ResultValue[0]);
        }

        private static string BuildSnapshotPath()
        {
            var root = Path.Combine(Path.GetTempPath(), "WombatSnapshotTests");
            Directory.CreateDirectory(root);
            return Path.Combine(root, $"s7_{Guid.NewGuid():N}.snapshot");
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static void DeleteSnapshotFiles(string snapshotPath)
        {
            if (File.Exists(snapshotPath))
            {
                File.Delete(snapshotPath);
            }

            var tempPath = snapshotPath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        private static async Task WriteAndReadHandshakeAsync(NetworkStream stream, byte[] request, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(request, 0, request.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            var response = await ReadTpktFrameAsync(stream, cancellationToken).ConfigureAwait(false);
            Assert.NotNull(response);
        }

        private static async Task<byte[]> ReadTpktFrameAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var header = await ReadExactAsync(stream, 4, cancellationToken).ConfigureAwait(false);
            int totalLength = (header[2] << 8) | header[3];
            var body = await ReadExactAsync(stream, totalLength - 4, cancellationToken).ConfigureAwait(false);
            var frame = new byte[totalLength];
            Buffer.BlockCopy(header, 0, frame, 0, header.Length);
            Buffer.BlockCopy(body, 0, frame, header.Length, body.Length);
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

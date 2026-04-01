using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
    }
}

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ModbusTests
{
    public class ModbusTcpServerSnapshotTests
    {
        [Fact]
        public void Snapshot_Should_Save_And_Load_For_ModbusTcpServer()
        {
            var snapshotPath = BuildSnapshotPath();
            try
            {
                var firstPort = GetFreePort();
                using (var server = new ModbusTcpServer("127.0.0.1", firstPort))
                {
                    server.SnapshotFilePath = snapshotPath;
                    server.EnableSnapshotPersistence = true;

                    server.DataStore.CoilDiscretes[0] = true;
                    server.DataStore.InputDiscretes[1] = true;
                    server.DataStore.HoldingRegisters[2] = 0x1234;
                    server.DataStore.InputRegisters[3] = 0x5678;

                    server.Listen();
                    server.Shutdown();
                }

                Assert.True(File.Exists(snapshotPath));

                var secondPort = GetFreePort();
                using (var server = new ModbusTcpServer("127.0.0.1", secondPort))
                {
                    server.SnapshotFilePath = snapshotPath;
                    server.EnableSnapshotPersistence = true;

                    server.Listen();

                    Assert.True(server.DataStore.CoilDiscretes[0]);
                    Assert.True(server.DataStore.InputDiscretes[1]);
                    Assert.Equal(0x1234, server.DataStore.HoldingRegisters[2]);
                    Assert.Equal(0x5678, server.DataStore.InputRegisters[3]);

                    server.Shutdown();
                }
            }
            finally
            {
                DeleteSnapshotFiles(snapshotPath);
            }
        }

        [Fact]
        public void ResetDataAndDeleteSnapshot_Should_Clear_Data_And_Remove_File_For_ModbusTcpServer()
        {
            var snapshotPath = BuildSnapshotPath();
            try
            {
                var port = GetFreePort();
                using (var server = new ModbusTcpServer("127.0.0.1", port))
                {
                    server.SnapshotFilePath = snapshotPath;
                    server.EnableSnapshotPersistence = true;

                    server.DataStore.CoilDiscretes[0] = true;
                    server.DataStore.InputDiscretes[1] = true;
                    server.DataStore.HoldingRegisters[2] = 0x4321;
                    server.DataStore.InputRegisters[3] = 0x8765;

                    server.Listen();
                    server.Shutdown();

                    Assert.True(File.Exists(snapshotPath));

                    var resetResult = server.ResetDataAndDeleteSnapshot();
                    Assert.True(resetResult.IsSuccess, resetResult.Message);

                    Assert.False(File.Exists(snapshotPath));
                    Assert.False(server.DataStore.CoilDiscretes[0]);
                    Assert.False(server.DataStore.InputDiscretes[1]);
                    Assert.Equal(0, server.DataStore.HoldingRegisters[2]);
                    Assert.Equal(0, server.DataStore.InputRegisters[3]);
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
            return Path.Combine(root, $"modbus_tcp_{Guid.NewGuid():N}.snapshot");
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

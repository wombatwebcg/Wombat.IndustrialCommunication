using System.Collections.Generic;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{

    public class S7TcpServerBatchTests
    {
        [Fact]
        public void BatchReadWrite_ShouldHandleContinuousDbAddressBlock()
        {
            using var server = CreateServer();

            var writeResult = server.BatchWrite(new Dictionary<string, (DataTypeEnums, object)>
            {
                ["DB1.DBB0"] = (DataTypeEnums.Byte, (byte)0x11),
                ["DB1.DBW1"] = (DataTypeEnums.UInt16, (ushort)0x2233),
                ["DB1.DBD3"] = (DataTypeEnums.UInt32, 0x44556677U)
            });

            Assert.True(writeResult.IsSuccess, writeResult.Message);

            var readResult = server.BatchRead(new Dictionary<string, DataTypeEnums>
            {
                ["DB1.DBB0"] = DataTypeEnums.Byte,
                ["DB1.DBW1"] = DataTypeEnums.UInt16,
                ["DB1.DBD3"] = DataTypeEnums.UInt32
            });

            Assert.True(readResult.IsSuccess, readResult.Message);
            Assert.Equal((byte)0x11, Assert.IsType<byte>(readResult.ResultValue["DB1.DBB0"].Item2));
            Assert.Equal((ushort)0x2233, Assert.IsType<ushort>(readResult.ResultValue["DB1.DBW1"].Item2));
            Assert.Equal(0x44556677U, Assert.IsType<uint>(readResult.ResultValue["DB1.DBD3"].Item2));
        }

        [Fact]
        public void BatchWrite_ShouldHandleMixedBitWritesAcrossMIQAreas()
        {
            using var server = CreateServer();

            Assert.True(server.WriteMerkers(0, new byte[] { 0xFF }).IsSuccess);
            Assert.True(server.WriteInputs(1, new byte[] { 0x00 }).IsSuccess);
            Assert.True(server.WriteOutputs(2, new byte[] { 0x80 }).IsSuccess);

            var writeResult = server.BatchWrite(new Dictionary<string, (DataTypeEnums, object)>
            {
                ["M0.0"] = (DataTypeEnums.Bool, true),
                ["M0.1"] = (DataTypeEnums.Bool, false),
                ["I1.2"] = (DataTypeEnums.Bool, true),
                ["I1.5"] = (DataTypeEnums.Bool, true),
                ["Q2.3"] = (DataTypeEnums.Bool, true),
                ["Q2.7"] = (DataTypeEnums.Bool, false)
            });

            Assert.True(writeResult.IsSuccess, writeResult.Message);

            var readResult = server.BatchRead(new Dictionary<string, DataTypeEnums>
            {
                ["M0.0"] = DataTypeEnums.Bool,
                ["M0.1"] = DataTypeEnums.Bool,
                ["I1.2"] = DataTypeEnums.Bool,
                ["I1.5"] = DataTypeEnums.Bool,
                ["Q2.3"] = DataTypeEnums.Bool,
                ["Q2.7"] = DataTypeEnums.Bool
            });

            Assert.True(readResult.IsSuccess, readResult.Message);
            Assert.True(Assert.IsType<bool>(readResult.ResultValue["M0.0"].Item2));
            Assert.False(Assert.IsType<bool>(readResult.ResultValue["M0.1"].Item2));
            Assert.True(Assert.IsType<bool>(readResult.ResultValue["I1.2"].Item2));
            Assert.True(Assert.IsType<bool>(readResult.ResultValue["I1.5"].Item2));
            Assert.True(Assert.IsType<bool>(readResult.ResultValue["Q2.3"].Item2));
            Assert.False(Assert.IsType<bool>(readResult.ResultValue["Q2.7"].Item2));

            var merkerByte = server.ReadMerkers(0, 1);
            var inputByte = server.ReadInputs(1, 1);
            var outputByte = server.ReadOutputs(2, 1);

            Assert.True(merkerByte.IsSuccess, merkerByte.Message);
            Assert.True(inputByte.IsSuccess, inputByte.Message);
            Assert.True(outputByte.IsSuccess, outputByte.Message);
            Assert.Equal((byte)0xFD, merkerByte.ResultValue[0]);
            Assert.Equal((byte)0x24, inputByte.ResultValue[0]);
            Assert.Equal((byte)0x08, outputByte.ResultValue[0]);
        }

        [Fact]
        public void BatchReadWrite_ShouldKeepVAreaAndDb1Consistent()
        {
            using var server = CreateServer();

            var writeVResult = server.BatchWrite(new Dictionary<string, (DataTypeEnums, object)>
            {
                ["VW10"] = (DataTypeEnums.UInt16, (ushort)0x1234),
                ["V12.3"] = (DataTypeEnums.Bool, true)
            });

            Assert.True(writeVResult.IsSuccess, writeVResult.Message);

            var readFromDbResult = server.BatchRead(new Dictionary<string, DataTypeEnums>
            {
                ["DB1.DBW10"] = DataTypeEnums.UInt16,
                ["DB1.DBX12.3"] = DataTypeEnums.Bool
            });

            Assert.True(readFromDbResult.IsSuccess, readFromDbResult.Message);
            Assert.Equal((ushort)0x1234, Assert.IsType<ushort>(readFromDbResult.ResultValue["DB1.DBW10"].Item2));
            Assert.True(Assert.IsType<bool>(readFromDbResult.ResultValue["DB1.DBX12.3"].Item2));

            var writeDbResult = server.BatchWrite(new Dictionary<string, (DataTypeEnums, object)>
            {
                ["DB1.DBW20"] = (DataTypeEnums.UInt16, (ushort)0x5678),
                ["DB1.DBX22.1"] = (DataTypeEnums.Bool, true)
            });

            Assert.True(writeDbResult.IsSuccess, writeDbResult.Message);

            var readFromVResult = server.BatchRead(new Dictionary<string, DataTypeEnums>
            {
                ["VW20"] = DataTypeEnums.UInt16,
                ["V22.1"] = DataTypeEnums.Bool
            });

            Assert.True(readFromVResult.IsSuccess, readFromVResult.Message);
            Assert.Equal((ushort)0x5678, Assert.IsType<ushort>(readFromVResult.ResultValue["VW20"].Item2));
            Assert.True(Assert.IsType<bool>(readFromVResult.ResultValue["V22.1"].Item2));
        }

        private static S7TcpServer CreateServer()
        {
            var server = new S7TcpServer();
            var createDataBlockResult = server.CreateDataBlock(1, 256);
            Assert.True(createDataBlockResult.IsSuccess, createDataBlockResult.Message);
            return server;
        }
    }
}
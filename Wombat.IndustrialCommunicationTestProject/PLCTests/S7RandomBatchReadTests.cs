using System;
using System.Collections.Generic;
using System.Linq;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    public class S7RandomBatchReadTests
    {
        private sealed class TestableS7Communication : S7Communication
        {
            private readonly int _nativeRandomReadMaxItems;
            private readonly int _nativeRandomReadMaxPayloadBytes;
            private readonly double _blockReadMinEfficiency;
            private readonly int _randomReadPreferSingleLengthThreshold;
            private readonly double _batchReadDispatchRequestWeight;

            public TestableS7Communication() : base(null)
            {
                _nativeRandomReadMaxItems = 19;
                _nativeRandomReadMaxPayloadBytes = 180;
                _blockReadMinEfficiency = 0.8;
                _randomReadPreferSingleLengthThreshold = 4;
                _batchReadDispatchRequestWeight = 1.0;
            }

            public TestableS7Communication(
                int nativeRandomReadMaxItems = 19,
                int nativeRandomReadMaxPayloadBytes = 180,
                double blockReadMinEfficiency = 0.8,
                int randomReadPreferSingleLengthThreshold = 4,
                double batchReadDispatchRequestWeight = 1.0) : base(null)
            {
                _nativeRandomReadMaxItems = nativeRandomReadMaxItems;
                _nativeRandomReadMaxPayloadBytes = nativeRandomReadMaxPayloadBytes;
                _blockReadMinEfficiency = blockReadMinEfficiency;
                _randomReadPreferSingleLengthThreshold = randomReadPreferSingleLengthThreshold;
                _batchReadDispatchRequestWeight = batchReadDispatchRequestWeight;
            }

            public S7BatchReadDispatchAnalysis Analyze(IReadOnlyList<S7BatchHelper.S7AddressInfo> infos) => AnalyzeBatchReadDispatch(infos);

            public List<S7ReadBatch> Split(IReadOnlyList<SiemensAddress> items) => SplitReadBatches(items);

            public void EstimateBlock(IReadOnlyList<S7BatchHelper.S7AddressInfo> infos, S7BatchReadDispatchAnalysis decision) => EstimateBlockReadCost(infos, decision);

            public void EstimateNative(IReadOnlyList<S7BatchHelper.S7AddressInfo> infos, S7BatchReadDispatchAnalysis decision) => EstimateNativeRandomReadCost(infos, decision);

            public bool ShouldUseNative(S7BatchReadDispatchAnalysis decision) => ShouldUseNativeRandomRead(decision);

            internal override int GetNativeRandomReadMaxItems() => _nativeRandomReadMaxItems;
            internal override int GetNativeRandomReadMaxPayloadBytes() => _nativeRandomReadMaxPayloadBytes;
            internal override double GetBlockReadMinEfficiency() => _blockReadMinEfficiency;
            internal override int GetRandomReadPreferSingleLengthThreshold() => _randomReadPreferSingleLengthThreshold;
            internal override double GetBatchReadDispatchRequestWeight() => _batchReadDispatchRequestWeight;
        }

        [Fact]
        public void AnalyzeBatchReadDispatch_ShouldChooseNativeRandom_ForDiscreteShortAddresses()
        {
            var communication = new TestableS7Communication(blockReadMinEfficiency: 0.8, randomReadPreferSingleLengthThreshold: 2);
            var infos = new List<S7BatchHelper.S7AddressInfo>
            {
                S7BatchHelper.ParseSingleS7Address("DB1.DBB0", DataTypeEnums.Byte),
                S7BatchHelper.ParseSingleS7Address("DB1.DBB100", DataTypeEnums.Byte),
                S7BatchHelper.ParseSingleS7Address("DB1.DBB200", DataTypeEnums.Byte)
            };

            var decision = communication.Analyze(infos);

            Assert.Equal(S7BatchReadPathKind.NativeRandomRead, decision.Mode);
            Assert.Contains("随机", decision.DecisionReason);
            Assert.Equal(1, decision.NativeBatchCount);
        }

        [Fact]
        public void ReadRequest_ShouldBuildMultiItemReadVarFrame()
        {
            var items = new List<SiemensAddress>
            {
                new SiemensAddress
                {
                    OriginalAddress = "DB1.DBB0",
                    TypeCode = 0x84,
                    DbBlock = 1,
                    BeginAddress = 0,
                    Length = 1,
                    RequestedLength = 1,
                    DataType = DataTypeEnums.Byte
                },
                new SiemensAddress
                {
                    OriginalAddress = "DB1.DBW2",
                    TypeCode = 0x84,
                    DbBlock = 1,
                    BeginAddress = 16,
                    Length = 2,
                    RequestedLength = 2,
                    DataType = DataTypeEnums.UInt16
                }
            };

            var request = new S7ReadRequest(items, 0x1234);

            Assert.Equal(43, request.ProtocolMessageFrame.Length);
            Assert.Equal(0x32, request.ProtocolMessageFrame[7]);
            Assert.Equal(0x12, request.ProtocolMessageFrame[11]);
            Assert.Equal(0x34, request.ProtocolMessageFrame[12]);
            Assert.Equal(0x04, request.ProtocolMessageFrame[17]);
            Assert.Equal(0x02, request.ProtocolMessageFrame[18]);
            Assert.Equal(0x84, request.ProtocolMessageFrame[27]);
            Assert.Equal(0x84, request.ProtocolMessageFrame[39]);
        }

        [Fact]
        public void ReadResponse_ShouldParsePartialSuccess()
        {
            var items = new List<SiemensAddress>
            {
                new SiemensAddress { OriginalAddress = "DB1.DBB0", RequestedLength = 1, Length = 1, DataType = DataTypeEnums.Byte },
                new SiemensAddress { OriginalAddress = "DB1.DBW2", RequestedLength = 2, Length = 2, DataType = DataTypeEnums.UInt16 }
            };

            var response = new byte[]
            {
                0x03,0x00,0x00,0x1D,
                0x02,0xF0,0x80,
                0x32,0x03,0x00,0x00,0x12,0x34,0x00,0x02,0x00,0x0A,0x00,0x00,
                0x04,0x02,
                0xFF,0x04,0x00,0x08,0x11,0x00,
                0x05,0x00,0x00,0x00
            };

            var parsed = S7ReadResponse.Parse(response, items);

            Assert.True(parsed.IsSuccess);
            Assert.Equal(2, parsed.ResultValue.Items.Count);
            Assert.True(parsed.ResultValue.Items[0].IsSuccess);
            Assert.Equal(0x11, parsed.ResultValue.Items[0].Data[0]);
            Assert.False(parsed.ResultValue.Items[1].IsSuccess);
        }

        [Fact]
        public void SplitReadBatches_ShouldSplit_WhenItemCountExceedsLimit()
        {
            var communication = new TestableS7Communication(nativeRandomReadMaxItems: 2, nativeRandomReadMaxPayloadBytes: 32, randomReadPreferSingleLengthThreshold: 4);
            var items = new List<SiemensAddress>
            {
                CreateReadItem("DB1.DBB0", 0, 1, DataTypeEnums.Byte),
                CreateReadItem("DB1.DBB1", 8, 1, DataTypeEnums.Byte),
                CreateReadItem("DB1.DBB2", 16, 1, DataTypeEnums.Byte)
            };

            var batches = communication.Split(items);

            Assert.Equal(2, batches.Count);
            Assert.Equal(2, batches[0].Items.Count);
            Assert.Single(batches[1].Items);
            Assert.Equal("DB1.DBB2", batches[1].Items[0].OriginalAddress);
        }

        [Fact]
        public void ReadRequest_EstimateResponsePayloadLength_ShouldIncludeHeadersAndPaddingRules()
        {
            var items = new List<SiemensAddress>
            {
                CreateReadItem("DB1.DBX0.0", 0, 1, DataTypeEnums.Bool, true),
                CreateReadItem("DB1.DBW2", 16, 2, DataTypeEnums.UInt16)
            };

            var payloadLength = S7ReadRequest.EstimateResponsePayloadLength(items);
            Assert.Equal(12, payloadLength);
        }

        [Fact]
        public void ReadResponse_ShouldFail_WhenResponseItemCountMismatches()
        {
            var items = new List<SiemensAddress>
            {
                CreateReadItem("DB1.DBB0", 0, 1, DataTypeEnums.Byte),
                CreateReadItem("DB1.DBB1", 8, 1, DataTypeEnums.Byte)
            };

            var response = new byte[]
            {
                0x03,0x00,0x00,0x18,
                0x02,0xF0,0x80,
                0x32,0x03,0x00,0x00,0x12,0x34,0x00,0x02,0x00,0x05,0x00,0x00,
                0x04,0x01,
                0xFF,0x04,0x00,0x08,0x11
            };

            var parsed = S7ReadResponse.Parse(response, items);
            Assert.False(parsed.IsSuccess);
            Assert.Contains("项数与请求不一致", parsed.Message);
        }

        [Fact]
        public void SplitReadBatches_ShouldThrow_WhenSingleItemExceedsPayloadLimit()
        {
            var communication = new TestableS7Communication(nativeRandomReadMaxItems: 19, nativeRandomReadMaxPayloadBytes: 4, randomReadPreferSingleLengthThreshold: 4);
            var items = new List<SiemensAddress>
            {
                new SiemensAddress
                {
                    OriginalAddress = "DB1.DBD0",
                    TypeCode = 0x84,
                    DbBlock = 1,
                    BeginAddress = 0,
                    Length = 4,
                    RequestedLength = 4,
                    DataType = DataTypeEnums.UInt32
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() => communication.Split(items));
            Assert.Contains("超出随机批量读单项上限", ex.Message);
        }

        [Fact]
        public void EstimateReadCosts_ShouldPopulateDecisionMetrics()
        {
            var communication = new TestableS7Communication(nativeRandomReadMaxItems: 19, nativeRandomReadMaxPayloadBytes: 180);
            var infos = new List<S7BatchHelper.S7AddressInfo>
            {
                S7BatchHelper.ParseSingleS7Address("DB1.DBB0", DataTypeEnums.Byte),
                S7BatchHelper.ParseSingleS7Address("DB1.DBB100", DataTypeEnums.Byte)
            };
            var decision = new S7BatchReadDispatchAnalysis
            {
                RequestedBytes = infos.Sum(t => t.Length),
                AddressCount = infos.Count,
                MaxAddressLength = infos.Max(t => t.Length)
            };

            communication.EstimateBlock(infos, decision);
            communication.EstimateNative(infos, decision);

            Assert.Equal(2, decision.RequestedBytes);
            Assert.True(decision.BlockCount >= 1);
            Assert.True(decision.BlockReadBytes >= decision.RequestedBytes);
            Assert.True(decision.NativeBatchCount >= 1);
            Assert.True(decision.NativeTotalBytes > 0);
        }

        [Fact]
        public void ShouldUseNativeRandomRead_ShouldChooseBlock_ForSingleAddress()
        {
            var communication = new TestableS7Communication();
            var decision = new S7BatchReadDispatchAnalysis { AddressCount = 1, MaxAddressLength = 1 };

            var useNative = communication.ShouldUseNative(decision);

            Assert.False(useNative);
            Assert.Equal(S7BatchReadPathKind.BlockRead, decision.Mode);
            Assert.Contains("单地址", decision.DecisionReason);
        }

        private static SiemensAddress CreateReadItem(string address, int beginAddress, int length, DataTypeEnums dataType, bool isBit = false)
        {
            return new SiemensAddress
            {
                OriginalAddress = address,
                TypeCode = 0x84,
                DbBlock = 1,
                BeginAddress = beginAddress,
                Length = length,
                RequestedLength = length,
                DataType = dataType,
                IsBit = isBit
            };
        }
    }
}

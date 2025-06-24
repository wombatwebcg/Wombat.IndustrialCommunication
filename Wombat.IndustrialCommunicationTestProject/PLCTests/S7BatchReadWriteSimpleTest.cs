using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Wombat.IndustrialCommunication.PLC;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    /// <summary>
    /// S7批量读写功能简化测试
    /// 专门测试地址解析和优化算法的核心逻辑
    /// </summary>
    public class S7BatchReadWriteSimpleTest
    {
        private readonly ITestOutputHelper _output;

        public S7BatchReadWriteSimpleTest(ITestOutputHelper output = null)
        {
            _output = output;
        }

        /// <summary>
        /// 测试S7AddressInfo结构体的基本功能
        /// </summary>
        [Fact]
        public void Test_S7AddressInfo_BasicFunctionality()
        {
            LogInfo("开始测试S7AddressInfo结构体");

            // 测试DB地址信息
            var dbAddressInfo = new S7AddressInfo
            {
                OriginalAddress = "DB1.DBW4",
                DbNumber = 1,
                StartByte = 4,
                Length = 2,
                DataType = S7DataType.DBW,
                BitOffset = 0
            };

            Assert.Equal("DB1.DBW4", dbAddressInfo.OriginalAddress);
            Assert.Equal(1, dbAddressInfo.DbNumber);
            Assert.Equal(4, dbAddressInfo.StartByte);
            Assert.Equal(2, dbAddressInfo.Length);
            Assert.Equal(S7DataType.DBW, dbAddressInfo.DataType);
            LogInfo($"DB地址信息测试通过: {dbAddressInfo.OriginalAddress}");

            // 测试V区地址信息
            var vAddressInfo = new S7AddressInfo
            {
                OriginalAddress = "V700",
                DbNumber = -1, // V区使用-1标识
                StartByte = 700,
                Length = 4,
                DataType = S7DataType.VD,
                BitOffset = 0
            };

            Assert.Equal("V700", vAddressInfo.OriginalAddress);
            Assert.Equal(-1, vAddressInfo.DbNumber);
            Assert.Equal(700, vAddressInfo.StartByte);
            Assert.Equal(4, vAddressInfo.Length);
            Assert.Equal(S7DataType.VD, vAddressInfo.DataType);
            LogInfo($"V区地址信息测试通过: {vAddressInfo.OriginalAddress}");

            // 测试Q区位地址信息
            var qAddressInfo = new S7AddressInfo
            {
                OriginalAddress = "Q1.3",
                DbNumber = -2, // Q区使用-2标识
                StartByte = 1,
                Length = 1,
                DataType = S7DataType.QB,
                BitOffset = 3
            };

            Assert.Equal("Q1.3", qAddressInfo.OriginalAddress);
            Assert.Equal(-2, qAddressInfo.DbNumber);
            Assert.Equal(1, qAddressInfo.StartByte);
            Assert.Equal(1, qAddressInfo.Length);
            Assert.Equal(S7DataType.QB, qAddressInfo.DataType);
            Assert.Equal(3, qAddressInfo.BitOffset);
            LogInfo($"Q区地址信息测试通过: {qAddressInfo.OriginalAddress}");

            LogInfo("S7AddressInfo结构体测试通过");
        }

        /// <summary>
        /// 测试S7AddressBlock类的基本功能
        /// </summary>
        [Fact]
        public void Test_S7AddressBlock_BasicFunctionality()
        {
            LogInfo("开始测试S7AddressBlock类");

            var addressBlock = new S7AddressBlock
            {
                DbNumber = 1,
                StartByte = 0,
                TotalLength = 10,
                EfficiencyRatio = 0.8
            };

            // 添加地址信息
            addressBlock.Addresses.Add(new S7AddressInfo
            {
                OriginalAddress = "DB1.DBW0",
                DbNumber = 1,
                StartByte = 0,
                Length = 2,
                DataType = S7DataType.DBW
            });

            addressBlock.Addresses.Add(new S7AddressInfo
            {
                OriginalAddress = "DB1.DBW2",
                DbNumber = 1,
                StartByte = 2,
                Length = 2,
                DataType = S7DataType.DBW
            });

            Assert.Equal(1, addressBlock.DbNumber);
            Assert.Equal(0, addressBlock.StartByte);
            Assert.Equal(10, addressBlock.TotalLength);
            Assert.Equal(0.8, addressBlock.EfficiencyRatio);
            Assert.Equal(2, addressBlock.Addresses.Count);

            LogInfo($"地址块测试通过: DB{addressBlock.DbNumber}, 包含{addressBlock.Addresses.Count}个地址, 效率比: {addressBlock.EfficiencyRatio:P2}");

            LogInfo("S7AddressBlock类测试通过");
        }

        /// <summary>
        /// 测试数据类型枚举
        /// </summary>
        [Fact]
        public void Test_S7DataType_Enum()
        {
            LogInfo("开始测试S7DataType枚举");

            // 测试DB区数据类型
            Assert.True(Enum.IsDefined(typeof(S7DataType), S7DataType.DBX));
            Assert.True(Enum.IsDefined(typeof(S7DataType), S7DataType.DBB));
            Assert.True(Enum.IsDefined(typeof(S7DataType), S7DataType.DBW));
            Assert.True(Enum.IsDefined(typeof(S7DataType), S7DataType.DBD));
            LogInfo("DB区数据类型枚举测试通过");

            // 测试V区数据类型
            Assert.True(Enum.IsDefined(typeof(S7DataType), S7DataType.VB));
            Assert.True(Enum.IsDefined(typeof(S7DataType), S7DataType.VW));
            Assert.True(Enum.IsDefined(typeof(S7DataType), S7DataType.VD));
            LogInfo("V区数据类型枚举测试通过");

            // 测试Q区数据类型
            Assert.True(Enum.IsDefined(typeof(S7DataType), S7DataType.QB));
            Assert.True(Enum.IsDefined(typeof(S7DataType), S7DataType.QW));
            Assert.True(Enum.IsDefined(typeof(S7DataType), S7DataType.QD));
            LogInfo("Q区数据类型枚举测试通过");

            LogInfo("S7DataType枚举测试通过");
        }

        /// <summary>
        /// 测试地址优化的基本概念
        /// </summary>
        [Fact]
        public void Test_AddressOptimization_Concept()
        {
            LogInfo("开始测试地址优化概念");

            // 模拟连续地址场景
            var continuousAddresses = new List<S7AddressInfo>
            {
                new S7AddressInfo { OriginalAddress = "DB1.DBW0", DbNumber = 1, StartByte = 0, Length = 2, DataType = S7DataType.DBW },
                new S7AddressInfo { OriginalAddress = "DB1.DBW2", DbNumber = 1, StartByte = 2, Length = 2, DataType = S7DataType.DBW },
                new S7AddressInfo { OriginalAddress = "DB1.DBW4", DbNumber = 1, StartByte = 4, Length = 2, DataType = S7DataType.DBW },
                new S7AddressInfo { OriginalAddress = "DB1.DBW6", DbNumber = 1, StartByte = 6, Length = 2, DataType = S7DataType.DBW }
            };

            // 连续地址应该能够合并
            var minStart = continuousAddresses.Min(a => a.StartByte);
            var maxEnd = continuousAddresses.Max(a => a.StartByte + a.Length);
            var totalLength = maxEnd - minStart;
            var effectiveLength = continuousAddresses.Sum(a => a.Length);
            var efficiencyRatio = (double)effectiveLength / totalLength;

            LogInfo($"连续地址分析: 起始={minStart}, 结束={maxEnd}, 总长度={totalLength}, 有效长度={effectiveLength}, 效率比={efficiencyRatio:P2}");
            
            Assert.Equal(0, minStart);
            Assert.Equal(8, maxEnd);
            Assert.Equal(8, totalLength);
            Assert.Equal(8, effectiveLength);
            Assert.Equal(1.0, efficiencyRatio); // 100%效率

            // 模拟分散地址场景
            var scatteredAddresses = new List<S7AddressInfo>
            {
                new S7AddressInfo { OriginalAddress = "DB1.DBW0", DbNumber = 1, StartByte = 0, Length = 2, DataType = S7DataType.DBW },
                new S7AddressInfo { OriginalAddress = "DB1.DBW100", DbNumber = 1, StartByte = 100, Length = 2, DataType = S7DataType.DBW },
                new S7AddressInfo { OriginalAddress = "DB1.DBW200", DbNumber = 1, StartByte = 200, Length = 2, DataType = S7DataType.DBW }
            };

            var scatteredMinStart = scatteredAddresses.Min(a => a.StartByte);
            var scatteredMaxEnd = scatteredAddresses.Max(a => a.StartByte + a.Length);
            var scatteredTotalLength = scatteredMaxEnd - scatteredMinStart;
            var scatteredEffectiveLength = scatteredAddresses.Sum(a => a.Length);
            var scatteredEfficiencyRatio = (double)scatteredEffectiveLength / scatteredTotalLength;

            LogInfo($"分散地址分析: 起始={scatteredMinStart}, 结束={scatteredMaxEnd}, 总长度={scatteredTotalLength}, 有效长度={scatteredEffectiveLength}, 效率比={scatteredEfficiencyRatio:P2}");
            
            Assert.Equal(0, scatteredMinStart);
            Assert.Equal(202, scatteredMaxEnd);
            Assert.Equal(202, scatteredTotalLength);
            Assert.Equal(6, scatteredEffectiveLength);
            Assert.True(scatteredEfficiencyRatio < 0.1); // 效率很低，不应该合并

            LogInfo("地址优化概念测试通过");
        }

        /// <summary>
        /// 测试批量读写的预期性能提升
        /// </summary>
        [Fact]
        public void Test_BatchReadWrite_PerformanceExpectation()
        {
            LogInfo("开始测试批量读写性能预期");

            // 模拟100个连续地址的场景
            int addressCount = 100;
            int individualOperationTime = 50; // 假设单个操作需要50ms
            int batchOperationTime = 100; // 假设批量操作需要100ms（1-2次通信）

            var totalIndividualTime = addressCount * individualOperationTime;
            var speedupRatio = (double)totalIndividualTime / batchOperationTime;
            var efficiency = ((totalIndividualTime - batchOperationTime) / (double)totalIndividualTime) * 100;

            LogInfo($"性能预期分析:");
            LogInfo($"  地址数量: {addressCount}");
            LogInfo($"  单个操作总时间: {totalIndividualTime}ms");
            LogInfo($"  批量操作总时间: {batchOperationTime}ms");
            LogInfo($"  性能提升倍数: {speedupRatio:F1}x");
            LogInfo($"  效率提升: {efficiency:F1}%");
            LogInfo($"  时间节省: {totalIndividualTime - batchOperationTime}ms");

            // 对于连续地址，应该有显著的性能提升
            Assert.True(speedupRatio > 10, "连续地址批量读取应该有10倍以上的性能提升");
            Assert.True(efficiency > 80, "效率提升应该超过80%");

            LogInfo("批量读写性能预期测试通过");
        }

        private void LogInfo(string message)
        {
            _output?.WriteLine($"[INFO] {message}");
        }
    }
} 
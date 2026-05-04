using System;
using System.Collections.Generic;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.ModbusTests
{
    public class ModbusBatchHelperTests
    {
        [Fact]
        public void TestParseModbusAddresses_WithValueFormat()
        {
            // 准备测试数据
            var addresses = new Dictionary<string, (DataTypeEnums, object)>
            {
                { "{1;00001, true}", (DataTypeEnums.Bool, true) },
                { "[1;40001, 1111]", (DataTypeEnums.UInt16, 1111) },
                { "(2;40011, 1234)", (DataTypeEnums.UInt16, 1234) },
                { "3;40021", (DataTypeEnums.UInt16, null) }
            };

            // 执行测试
            var result = ModbusBatchHelper.ParseModbusAddresses(addresses);

            // 验证结果
            Assert.Equal(4, result.Count);

            // 验证第一个地址 (1;00001 -> 线圈实际地址0)
            var firstAddress = result[0];
            Assert.Equal("{1;00001, true}", firstAddress.OriginalAddress);
            Assert.Equal(1, firstAddress.StationNumber);
            Assert.Equal(5, firstAddress.FunctionCode);
            Assert.Equal(0, firstAddress.Address);

            // 验证第二个地址 (1;40001 -> 保持寄存器实际地址0)
            var secondAddress = result[1];
            Assert.Equal("[1;40001, 1111]", secondAddress.OriginalAddress);
            Assert.Equal(1, secondAddress.StationNumber);
            Assert.Equal(6, secondAddress.FunctionCode);
            Assert.Equal(0, secondAddress.Address);

            // 验证第三个地址 (2;40011 -> 保持寄存器实际地址10)
            var thirdAddress = result[2];
            Assert.Equal("(2;40011, 1234)", thirdAddress.OriginalAddress);
            Assert.Equal(2, thirdAddress.StationNumber);
            Assert.Equal(6, thirdAddress.FunctionCode);
            Assert.Equal(10, thirdAddress.Address);

            // 验证第四个地址 (3;40021 -> 保持寄存器实际地址20)
            var fourthAddress = result[3];
            Assert.Equal("3;40021", fourthAddress.OriginalAddress);
            Assert.Equal(3, fourthAddress.StationNumber);
            Assert.Equal(6, fourthAddress.FunctionCode);
            Assert.Equal(20, fourthAddress.Address);
        }

        [Fact]
        public void TestParseModbusAddresses_InvalidFormats()
        {
            // 准备包含无效格式的测试数据
            var addresses = new Dictionary<string, (DataTypeEnums, object)>
            {
                { "{1;00001, true}", (DataTypeEnums.Bool, true) },  // 有效格式
                { "invalid_address", (DataTypeEnums.None, null) }, // 无效格式
                { "[1;40001, 1111]", (DataTypeEnums.UInt16, 1111) },  // 有效格式
                { "malformed;address", (DataTypeEnums.None, null) } // 无效格式
            };

            // 执行测试
            var result = ModbusBatchHelper.ParseModbusAddresses(addresses);

            // 验证结果：应该只解析出2个有效地址
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void TestParseSingleModbusAddress_WithValueFormat()
        {
            // 测试各种包含值的地址格式
            var testCases = new[]
            {
                new { Input = "{1;00001, true}", ExpectedStation = (byte)1, ExpectedFunction = (byte)1, ExpectedAddress = (ushort)0 },
                new { Input = "[1;40001, 1111]", ExpectedStation = (byte)1, ExpectedFunction = (byte)3, ExpectedAddress = (ushort)0 },
                new { Input = "(2;30001, 1234)", ExpectedStation = (byte)2, ExpectedFunction = (byte)4, ExpectedAddress = (ushort)0 },
                new { Input = "3;40021", ExpectedStation = (byte)3, ExpectedFunction = (byte)3, ExpectedAddress = (ushort)20 }
            };

            foreach (var testCase in testCases)
            {
                var result = ModbusBatchHelper.ParseSingleModbusAddress(testCase.Input);
                
                Assert.Equal(testCase.Input, result.OriginalAddress);
                Assert.Equal(testCase.ExpectedStation, result.StationNumber);
                Assert.Equal(testCase.ExpectedFunction, result.FunctionCode);
                Assert.Equal(testCase.ExpectedAddress, result.Address);
            }
        }

        [Fact]
        public void TestParseSingleModbusAddress_InvalidFormat()
        {
            // 测试无效地址格式
            Assert.Throws<ArgumentException>(() => 
                ModbusBatchHelper.ParseSingleModbusAddress("invalid_address"));
        }

        [Fact]
        public void TestConvertValueToModbusBytes_WriteSingleCoil()
        {
            var addressInfo = new ModbusBatchHelper.ModbusAddressInfo
            {
                StationNumber = 1,
                FunctionCode = 0x05,
                Address = 0
            };
            // 写true
            var bytesOn = ModbusBatchHelper.ConvertValueToModbusBytes((DataTypeEnums.Bool,true), addressInfo, false);
            Assert.NotNull(bytesOn);
            Assert.Equal(2, bytesOn.Length);
            Assert.Equal(new byte[] { 0xFF, 0x00 }, bytesOn);
            // 写false
            var bytesOff = ModbusBatchHelper.ConvertValueToModbusBytes((DataTypeEnums.Bool, false), addressInfo, false);
            Assert.NotNull(bytesOff);
            Assert.Equal(2, bytesOff.Length);
            Assert.Equal(new byte[] { 0x00, 0x00 }, bytesOff);
        }

        [Theory]
        [InlineData("1;6;195", (byte)6, (ushort)195)]
        [InlineData("1;3;401", (byte)3, (ushort)401)]
        [InlineData("1;4;3005", (byte)4, (ushort)3005)]
        public void TestParseSingleModbusAddress_StandardFormatRegressionCases(string input, byte expectedFunction, ushort expectedAddress)
        {
            var result = ModbusBatchHelper.ParseSingleModbusAddress(input, DataTypeEnums.UInt16, false);

            Assert.Equal(input, result.OriginalAddress);
            Assert.Equal(1, result.StationNumber);
            Assert.Equal(expectedFunction, result.FunctionCode);
            Assert.Equal(expectedAddress, result.Address);
        }
    }
} 

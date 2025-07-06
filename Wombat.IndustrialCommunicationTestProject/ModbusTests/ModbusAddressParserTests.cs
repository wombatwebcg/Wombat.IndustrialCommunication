using System;
using System.Collections.Generic;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.ModbusTests
{
    public class ModbusAddressParserTests
    {
        [Fact]
        public void TestParseEnhancedModbusAddress_StandardFormat()
        {
            // 测试原有格式：站号;功能码;地址
            var result = ModbusAddressParser.ParseEnhancedModbusAddress("1;3;100", DataTypeEnums.UInt16, false);
            
            Assert.NotNull(result);
            Assert.Equal(1, result.StationNumber);
            Assert.Equal(3, result.FunctionCode);
            Assert.Equal(100, result.Address);
        }

        [Fact]
        public void TestParseEnhancedModbusAddress_EnhancedFormat_Read()
        {
            // 测试新格式：站号;地址（读操作）
            var result = ModbusAddressParser.ParseEnhancedModbusAddress("1;40001", DataTypeEnums.UInt16, false);
            
            Assert.NotNull(result);
            Assert.Equal(1, result.StationNumber);
            Assert.Equal(3, result.FunctionCode); // 读保持寄存器
            Assert.Equal(1, result.Address); // 40001 -> 4(前缀) + 0001(地址) = 1
        }

        [Fact]
        public void TestParseEnhancedModbusAddress_EnhancedFormat_WriteInt16()
        {
            // 测试新格式：站号;地址（写Int16）
            var result = ModbusAddressParser.ParseEnhancedModbusAddress("1;40001", DataTypeEnums.Int16, true);
            
            Assert.NotNull(result);
            Assert.Equal(1, result.StationNumber);
            Assert.Equal(6, result.FunctionCode); // 写单个寄存器
            Assert.Equal(1, result.Address); // 40001 -> 4(前缀) + 0001(地址) = 1
        }

        [Fact]
        public void TestParseEnhancedModbusAddress_EnhancedFormat_WriteInt32()
        {
            // 测试新格式：站号;地址（写Int32）
            var result = ModbusAddressParser.ParseEnhancedModbusAddress("1;40010", DataTypeEnums.Int32, true);
            
            Assert.NotNull(result);
            Assert.Equal(1, result.StationNumber);
            Assert.Equal(16, result.FunctionCode); // 写多个寄存器
            Assert.Equal(10, result.Address); // 40010 -> 4(前缀) + 0010(地址) = 10
        }

        [Fact]
        public void TestParseEnhancedModbusAddress_EnhancedFormat_WriteBool()
        {
            // 测试新格式：站号;地址（写Bool）
            var result = ModbusAddressParser.ParseEnhancedModbusAddress("1;10001", DataTypeEnums.Bool, true);
            
            Assert.NotNull(result);
            Assert.Equal(1, result.StationNumber);
            Assert.Equal(5, result.FunctionCode); // 写单个线圈
            Assert.Equal(1, result.Address); // 10001 -> 1(前缀) + 0001(地址) = 1
        }

        [Fact]
        public void TestParseEnhancedModbusAddress_EnhancedFormat_ReadBool()
        {
            // 测试新格式：站号;地址（读Bool）
            var result = ModbusAddressParser.ParseEnhancedModbusAddress("1;10001", DataTypeEnums.Bool, false);
            
            Assert.NotNull(result);
            Assert.Equal(1, result.StationNumber);
            Assert.Equal(1, result.FunctionCode); // 读线圈
            Assert.Equal(1, result.Address); // 10001 -> 1(前缀) + 0001(地址) = 1
        }

        [Fact]
        public void TestParseEnhancedModbusAddress_LargeAddress()
        {
            // 测试大地址范围
            var result = ModbusAddressParser.ParseEnhancedModbusAddress("1;40001", DataTypeEnums.UInt16, false);
            
            Assert.NotNull(result);
            Assert.Equal(1, result.StationNumber);
            Assert.Equal(3, result.FunctionCode);
            Assert.Equal(1, result.Address); // 40001 -> 4(前缀) + 0001(地址) = 1
        }

        [Fact]
        public void TestParseEnhancedModbusAddress_InvalidFormat()
        {
            // 测试无效格式
            var result = ModbusAddressParser.ParseEnhancedModbusAddress("invalid", DataTypeEnums.UInt16, false);
            
            Assert.Null(result);
        }

        [Fact]
        public void TestModbusBatchHelper_EnhancedFormat()
        {
            // 测试批量处理中的新格式
            var addresses = new Dictionary<string, (DataTypeEnums, object)>
            {
                { "1;40001", (DataTypeEnums.Int32, 12345) },
                { "2;40010", (DataTypeEnums.UInt16, (ushort)1234) },
                { "3;10001", (DataTypeEnums.Bool, true) }
            };

            var addressInfos = ModbusBatchHelper.ParseModbusAddresses(addresses, true); // 写操作

            Assert.Equal(3, addressInfos.Count);
            
            // 验证第一个地址（Int32，40001 -> 4(前缀) + 0001(地址) = 1，应该使用功能码16）
            Assert.Equal(1, addressInfos[0].StationNumber);
            Assert.Equal(16, addressInfos[0].FunctionCode);
            Assert.Equal(1, addressInfos[0].Address);
            Assert.Equal(DataTypeEnums.Int32, addressInfos[0].DataType);
            
            // 验证第二个地址（UInt16，40010 -> 4(前缀) + 0010(地址) = 10，应该使用功能码6）
            Assert.Equal(2, addressInfos[1].StationNumber);
            Assert.Equal(6, addressInfos[1].FunctionCode);
            Assert.Equal(10, addressInfos[1].Address);
            Assert.Equal(DataTypeEnums.UInt16, addressInfos[1].DataType);
            
            // 验证第三个地址（Bool，10001 -> 1(前缀) + 0001(地址) = 1，应该使用功能码5）
            Assert.Equal(3, addressInfos[2].StationNumber);
            Assert.Equal(5, addressInfos[2].FunctionCode);
            Assert.Equal(1, addressInfos[2].Address);
            Assert.Equal(DataTypeEnums.Bool, addressInfos[2].DataType);
        }

        [Fact]
        public void TestModbusBatchHelper_BackwardCompatibility()
        {
            // 测试向后兼容性
            var addresses = new Dictionary<string, (DataTypeEnums, object)>
            {
                { "1;3;100", (DataTypeEnums.UInt16, (ushort)1234) },
                { "2;6;200", (DataTypeEnums.UInt16, (ushort)5678) }
            };

            var addressInfos = ModbusBatchHelper.ParseModbusAddresses(addresses, true);

            Assert.Equal(2, addressInfos.Count);
            
            // 验证原有格式仍然正常工作
            Assert.Equal(1, addressInfos[0].StationNumber);
            Assert.Equal(3, addressInfos[0].FunctionCode);
            Assert.Equal(100, addressInfos[0].Address);
            
            Assert.Equal(2, addressInfos[1].StationNumber);
            Assert.Equal(6, addressInfos[1].FunctionCode);
            Assert.Equal(200, addressInfos[1].Address);
        }
    }
} 
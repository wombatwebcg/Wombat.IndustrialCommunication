using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTest.ModbusTests
{
    /// <summary>
    /// ModbusTcpClient 功能测试
    /// </summary>
    public class ModbusTcpClientTests
    {
        private const string TEST_TCP_IP = "127.0.0.1"; // 请根据实际情况修改
        private const int TEST_TCP_PORT = 502;
        private const byte TEST_STATION = 1;
        private const ushort TEST_COIL_ADDR = 0;
        private const ushort TEST_REG_ADDR = 0;
        private readonly ITestOutputHelper _output;

        public ModbusTcpClientTests(ITestOutputHelper output = null)
        {
            _output = output;
        }

        [Fact]
        public void Test_Connect_Disconnect()
        {
            var client = new ModbusTcpClient(TEST_TCP_IP, TEST_TCP_PORT);
            var result = client.Connect();
            Assert.True(result.IsSuccess, $"TCP连接失败: {result.Message}");
            _output?.WriteLine("TCP连接成功");
            var disconnect = client.Disconnect();
            Assert.True(disconnect.IsSuccess, $"TCP断开失败: {disconnect.Message}");
        }

        [Fact]
        public void Test_SyncReadWrite()
        {
            var client = new ModbusTcpClient(TEST_TCP_IP, TEST_TCP_PORT);
            client.Connect();
            // Coil
            var writeCoil = client.WriteCoil(TEST_STATION, TEST_COIL_ADDR, true);
            Assert.True(writeCoil.IsSuccess, $"写Coil失败: {writeCoil.Message}");
            var readCoil = client.ReadCoil(TEST_STATION, TEST_COIL_ADDR);
            Assert.True(readCoil.IsSuccess && readCoil.ResultValue, $"读Coil失败: {readCoil.Message}");
            // Register
            ushort testValue = 12345;
            var writeReg = client.WriteHoldingRegister(TEST_STATION, TEST_REG_ADDR, testValue);
            Assert.True(writeReg.IsSuccess, $"写寄存器失败: {writeReg.Message}");
            var readReg = client.ReadHoldingRegister(TEST_STATION, TEST_REG_ADDR);
            Assert.True(readReg.IsSuccess && readReg.ResultValue == testValue, $"读寄存器失败: {readReg.Message}");
            client.Disconnect();
        }

        [Fact]
        public async Task Test_AsyncReadWrite()
        {
            var client = new ModbusTcpClient(TEST_TCP_IP, TEST_TCP_PORT);
            await client.ConnectAsync();
            // Coil
            var writeCoil = await client.WriteCoilAsync(TEST_STATION, TEST_COIL_ADDR, true);
            Assert.True(writeCoil.IsSuccess, $"写Coil失败: {writeCoil.Message}");
            var readCoil = await client.ReadCoilAsync(TEST_STATION, TEST_COIL_ADDR);
            Assert.True(readCoil.IsSuccess && readCoil.ResultValue, $"读Coil失败: {readCoil.Message}");
            // Register
            ushort testValue = 54321;
            var writeReg = await client.WriteHoldingRegisterAsync(TEST_STATION, TEST_REG_ADDR, testValue);
            Assert.True(writeReg.IsSuccess, $"写寄存器失败: {writeReg.Message}");
            var readReg = await client.ReadHoldingRegisterAsync(TEST_STATION, TEST_REG_ADDR);
            Assert.True(readReg.IsSuccess && readReg.ResultValue == testValue, $"读寄存器失败: {readReg.Message}");
            await client.DisconnectAsync();
        }

        [Fact]
        public async Task Test_BatchReadWrite()
        {
            var client = new ModbusTcpClient(TEST_TCP_IP, TEST_TCP_PORT);
            await client.ConnectAsync();
            var batchWriteData = new Dictionary<string, (DataTypeEnums, object)>
            {
                { $"{TEST_STATION};5;{TEST_COIL_ADDR}", (DataTypeEnums.Bool, true) },
                { $"{TEST_STATION};6;{TEST_REG_ADDR}", (DataTypeEnums.UInt16, (ushort)1111) }
            };
            var writeResult = await client.BatchWriteAsync(batchWriteData);
            Assert.True(writeResult.IsSuccess, $"批量写入失败: {writeResult.Message}");
            var batchReadData = new Dictionary<string, DataTypeEnums>
            {
                { $"{TEST_STATION};1;{TEST_COIL_ADDR}", DataTypeEnums.Bool },
                { $"{TEST_STATION};3;{TEST_REG_ADDR}", DataTypeEnums.UInt16 }
            };
            var readResult = await client.BatchReadAsync(batchReadData);
            Assert.True(readResult.IsSuccess, $"批量读取失败: {readResult.Message}");
            Assert.True(readResult.ResultValue[$"{TEST_STATION};1;{TEST_COIL_ADDR}"].Item2 is bool b && b, "批量读Coil结果错误");
            Assert.True(readResult.ResultValue[$"{TEST_STATION};3;{TEST_REG_ADDR}"].Item2 is ushort u && u == 1111, "批量读寄存器结果错误");
            await client.DisconnectAsync();
        }
    }
} 
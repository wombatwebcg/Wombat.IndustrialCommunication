using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunicationTestProject.Helper;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTest.Modbus
{
    public class ModbusClientServerInteraction_tests
    {
        private ModbusTcpServer server;
        private ModbusTcpClient client;
        private const string LocalHost = "127.0.0.1";
        private const int ModbusPort = 502;
        private byte stationNumber = 1; // 站号
        private readonly ITestOutputHelper _output;

        public ModbusClientServerInteraction_tests(ITestOutputHelper output = null)
        {
            _output = output;
            // 创建服务器和客户端实例，但不立即启动或连接
            server = new ModbusTcpServer(LocalHost, ModbusPort);
            client = new ModbusTcpClient(LocalHost, ModbusPort);
            
            // 配置客户端自动重连参数，用于测试
            client.EnableAutoReconnect = true;
            client.MaxReconnectAttempts = 3;
            client.ReconnectDelay = TimeSpan.FromSeconds(1);
            client.ShortConnectionReconnectAttempts = 1;
        }

        [Fact]
        public void 服务器和客户端基本交互测试()
        {
            try
            {
                // 启动服务器
                var result = server.Listen();
                Assert.True(result.IsSuccess, $"启动Modbus服务器失败: {result.Message}");
                Assert.True(server.IsListening, "服务器应该处于监听状态");

                // 设置客户端为长连接并连接
                client.IsLongConnection = true;
                client.Connect();

                // 执行基本读写测试
                执行基本读写测试();
            }
            finally
            {
                // 清理资源
                client.Disconnect();
                server.Shutdown();
            }
        }

        [Fact]
        public async Task 服务器和客户端异步交互测试()
        {
            try
            {
                // 启动服务器
                var result = await server.StartAsync();
                Assert.True(result.IsSuccess, $"启动Modbus服务器失败: {result.Message}");

                // 设置客户端为长连接并连接
                client.IsLongConnection = true;
                await client.ConnectAsync();

                // 执行异步读写测试
                await 执行异步读写测试();
            }
            finally
            {
                // 清理资源
                await client.DisconnectAsync();
                await server.StopAsync();
            }
        }

        [Fact]
        public void 多数据类型读写测试()
        {
            try
            {
                // 启动服务器
                var result = server.Listen();
                Assert.True(result.IsSuccess);

                // 连接客户端
                client.IsLongConnection = true;
                client.Connect();

                // 执行多数据类型测试
                执行多数据类型测试();
            }
            finally
            {
                // 清理资源
                client.Disconnect();
                server.Shutdown();
            }
        }

        [Fact]
        public void 批量读写测试()
        {
            try
            {
                // 启动服务器
                var result = server.Listen();
                Assert.True(result.IsSuccess);

                // 连接客户端
                client.IsLongConnection = true;
                client.Connect();

                // 执行批量读写测试
                执行批量读写测试();
            }
            finally
            {
                // 清理资源
                client.Disconnect();
                server.Shutdown();
            }
        }

        [Fact]
        public async Task 简化服务器重启测试()
        {
            try
            {
                // 1. 启动服务器
                _output?.WriteLine("启动服务器");
                var result = await server.StartAsync();
                Assert.True(result.IsSuccess, $"启动Modbus服务器失败: {result.Message}");

                // 2. 设置客户端为长连接模式并连接
                _output?.WriteLine("客户端连接（长连接模式）");
                client.IsLongConnection = true;
                await client.ConnectAsync();

                // 3. 执行基本读写测试
                _output?.WriteLine("执行读写操作");
                short testValue = 12345;
                var writeResult = await client.WriteAsync("1;6;100", testValue);
                Assert.True(writeResult.IsSuccess, $"写入失败: {writeResult.Message}");
                
                var readResult = await client.ReadInt16Async("1;3;100");
                Assert.True(readResult.IsSuccess, $"读取失败: {readResult.Message}");
                Assert.Equal(testValue, readResult.ResultValue);

                // 4. 关闭服务器和客户端
                _output?.WriteLine("清理资源");
                await client.DisconnectAsync();
                await server.StopAsync();
            }
            catch (Exception ex)
            {
                _output?.WriteLine($"测试异常: {ex}");
                throw;
            }
            finally
            {
                // 确保资源被释放
                if (client.Connected)
                    await client.DisconnectAsync();
                
                if (server.IsListening)
                    await server.StopAsync();
            }
        }

        [Fact]
        public async Task 客户端连接中断恢复测试()
        {
            try
            {
                // 1. 启动服务器
                _output?.WriteLine("启动服务器");
                var result = await server.StartAsync();
                Assert.True(result.IsSuccess, $"启动Modbus服务器失败: {result.Message}");

                // 2. 设置客户端为长连接模式并连接
                _output?.WriteLine("客户端连接（长连接模式）");
                client.IsLongConnection = true;
                await client.ConnectAsync();

                // 3. 执行初始读写操作验证连接正常
                _output?.WriteLine("执行初始读写操作");
                short initialValue = 12345;
                var writeResult = await client.WriteAsync("1;6;100", initialValue);
                Assert.True(writeResult.IsSuccess, $"初始写入失败: {writeResult.Message}");
                
                var readResult = await client.ReadInt16Async("1;3;100");
                Assert.True(readResult.IsSuccess, $"初始读取失败: {readResult.Message}");
                Assert.Equal(initialValue, readResult.ResultValue);

                // 4. 模拟客户端连接中断（使用极简版模拟器）
                _output?.WriteLine("模拟客户端连接中断");
                var disruptor = new ConnectionDisruptorExtreme();
                var disruptResult = await disruptor.SimulateSafeDisruption(client);
                Assert.True(disruptResult.IsSuccess, $"模拟连接中断失败: {disruptResult.Message}");
                
                // 5. 手动断开连接，模拟连接断开
                _output?.WriteLine("手动断开连接");
                await client.DisconnectAsync();

                // 6. 等待自动重连机制启动
                _output?.WriteLine("等待自动重连机制启动");
                await Task.Delay(3000);
                
                // 7. 手动重新连接，模拟自动重连
                _output?.WriteLine("手动重新连接");
                await client.ConnectAsync();
                
                // 8. 执行新的读写操作，验证重连后功能正常
                _output?.WriteLine("执行新的读写操作，验证重连");
                short newValue = 23456;
                writeResult = await client.WriteAsync("1;6;100", newValue);
                Assert.True(writeResult.IsSuccess, $"重连后写入失败: {writeResult.Message}");
                
                readResult = await client.ReadInt16Async("1;3;100");
                Assert.True(readResult.IsSuccess, $"重连后读取失败: {readResult.Message}");
                Assert.Equal(newValue, readResult.ResultValue);
                
                _output?.WriteLine("客户端连接中断恢复测试完成");
            }
            finally
            {
                // 清理资源
                await client.DisconnectAsync();
                await server.StopAsync();
            }
        }

        [Fact]
        public async Task 短连接模式下的读写测试()
        {
            try
            {
                // 1. 启动服务器
                _output?.WriteLine("启动服务器");
                var result = await server.StartAsync();
                Assert.True(result.IsSuccess, $"启动Modbus服务器失败: {result.Message}");

                // 2. 设置客户端为短连接模式
                _output?.WriteLine("设置客户端为短连接模式");
                client.IsLongConnection = false;
                
                // 3. 确保客户端未连接
                if (client.Connected)
                {
                    await client.DisconnectAsync();
                }
                
                // 4. 执行初始操作，这会建立一个临时连接
                _output?.WriteLine("执行初始读写操作");
                short initialValue = 6789;
                var writeResult = await client.WriteAsync("1;6;300", initialValue);
                Assert.True(writeResult.IsSuccess, $"初始写入失败: {writeResult.Message}");
                
                var readResult = await client.ReadInt16Async("1;3;300");
                Assert.True(readResult.IsSuccess, $"初始读取失败: {readResult.Message}");
                Assert.Equal(initialValue, readResult.ResultValue);
                
                // 5. 在短连接模式下，每次操作后连接应该自动关闭
                Assert.False(client.Connected, "短连接模式下，操作完成后客户端应处于断开状态");
                
                // 6. 尝试第二次操作，这应该建立一个新的临时连接
                _output?.WriteLine("执行第二次读写操作");
                short newValue = 9876;
                
                // 写入新的测试值
                var secondWriteResult = await client.WriteAsync("1;6;300", newValue);
                Assert.True(secondWriteResult.IsSuccess, $"第二次写入失败: {secondWriteResult.Message}");
                
                // 读取并验证新的测试值
                var secondReadResult = await client.ReadInt16Async("1;3;300");
                Assert.True(secondReadResult.IsSuccess, $"第二次读取失败: {secondReadResult.Message}");
                Assert.Equal(newValue, secondReadResult.ResultValue);
                
                // 7. 再次验证连接自动关闭
                Assert.False(client.Connected, "短连接模式下，第二次操作后客户端应处于断开状态");
                
                _output?.WriteLine("短连接模式测试完成");
            }
            finally
            {
                // 清理资源
                if (client.Connected)
                {
                    await client.DisconnectAsync();
                }
                await server.StopAsync();
            }
        }

        [Fact]
        public async Task 短连接模式下的连续操作中断测试()
        {
            try
            {
                // 1. 启动服务器
                _output?.WriteLine("启动服务器");
                var result = await server.StartAsync();
                Assert.True(result.IsSuccess, $"启动Modbus服务器失败: {result.Message}");

                // 2. 配置为短连接模式
                _output?.WriteLine("配置短连接模式和重连参数");
                client.IsLongConnection = false;
                // 配置短连接重连参数，确保即使在短连接模式下也能进行有限次数的重试
                client.ShortConnectionReconnectAttempts = 1;
                
                // 3. 执行初始操作，这会建立一个临时连接
                _output?.WriteLine("执行初始读写操作");
                Random rnd = new Random((int)Stopwatch.GetTimestamp());
                short testValue = (short)rnd.Next(1000, 10000);
                
                // 写入测试值
                var writeResult = await client.WriteAsync("1;6;400", testValue);
                Assert.True(writeResult.IsSuccess, $"初始写入失败: {writeResult.Message}");
                
                // 读取并验证测试值
                var readResult = await client.ReadInt16Async("1;3;400");
                Assert.True(readResult.IsSuccess, $"初始读取失败: {readResult.Message}");
                Assert.Equal(testValue, readResult.ResultValue);
                
                // 4. 模拟服务器问题（使用极简版模拟器）
                _output?.WriteLine("模拟服务器暂时不可用");
                var disruptor = new ConnectionDisruptorExtreme();
                var disruptResult = await disruptor.SimulateSafeWait(client, 1000);
                Assert.True(disruptResult.IsSuccess, $"模拟等待失败: {disruptResult.Message}");
                
                // 5. 尝试在服务器恢复后执行操作
                _output?.WriteLine("在服务器恢复后执行读写操作");
                short newTestValue = (short)(testValue + 1);
                
                // 写入新的测试值
                var secondWriteResult = await client.WriteAsync("1;6;400", newTestValue);
                Assert.True(secondWriteResult.IsSuccess, $"服务器恢复后写入失败: {secondWriteResult.Message}");
                
                // 读取并验证新的测试值
                var secondReadResult = await client.ReadInt16Async("1;3;400");
                Assert.True(secondReadResult.IsSuccess, $"服务器恢复后读取失败: {secondReadResult.Message}");
                Assert.Equal(newTestValue, secondReadResult.ResultValue);
                
                _output?.WriteLine("短连接模式下的连续操作中断测试完成");
            }
            finally
            {
                // 清理资源
                if (client.Connected)
                {
                    await client.DisconnectAsync();
                }
                
                if (server.IsListening)
                {
                    await server.StopAsync();
                }
            }
        }

        private void 执行基本读写测试()
        {
            // 测试线圈操作
            client.Write("1;5;0", true); // 写入单个线圈
            var readBoolResult = client.ReadBoolean("1;1;0");
            Assert.True(readBoolResult.IsSuccess);
            Assert.True(readBoolResult.ResultValue);

            // 测试寄存器操作
            short testValue = 12345;
            client.Write("1;6;100", testValue); // 写入单个寄存器
            var readResult = client.ReadInt16("1;3;100");
            Assert.True(readResult.IsSuccess);
            Assert.Equal(testValue, readResult.ResultValue);
        }

        private async Task 执行异步读写测试()
        {
            // 测试线圈操作
            await client.WriteAsync("1;15;10", true); // 写入单个线圈
            var readBoolResult = await client.ReadBooleanAsync("1;1;10");
            Assert.True(readBoolResult.IsSuccess);
            Assert.True(readBoolResult.ResultValue);

            // 测试寄存器操作
            int testValue = 123456;
            await client.WriteAsync("1;16;200", testValue); // 写入寄存器
            var readResult = await client.ReadInt32Async("1;3;200");
            Assert.True(readResult.IsSuccess);
            Assert.Equal(testValue, readResult.ResultValue);
        }

        private void 执行多数据类型测试()
        {
            Random rnd = new Random((int)Stopwatch.GetTimestamp());

            // 生成随机测试数据
            short shortValue = (short)rnd.Next(short.MinValue, short.MaxValue);
            ushort ushortValue = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
            int intValue = rnd.Next(int.MinValue, int.MaxValue);
            uint uintValue = (uint)rnd.Next(0, int.MaxValue);
            float floatValue = (float)(rnd.NextDouble() * 10000 - 5000);
            double doubleValue = rnd.NextDouble() * 10000 - 5000;
            bool boolValue = rnd.Next(2) == 1;

            // 写入并读取各种数据类型
            client.Write("1;6;300", shortValue);
            Assert.Equal(shortValue, client.ReadInt16("1;3;300").ResultValue);

            client.Write("1;6;301", ushortValue);
            Assert.Equal(ushortValue, client.ReadUInt16("1;3;301").ResultValue);

            client.Write("1;16;302", intValue);
            Assert.Equal(intValue, client.ReadInt32("1;3;302").ResultValue);

            client.Write("1;16;304", uintValue);
            Assert.Equal(uintValue, client.ReadUInt32("1;3;304").ResultValue);

            client.Write("1;16;306", floatValue);
            Assert.Equal(floatValue, client.ReadFloat("1;3;306").ResultValue);

            client.Write("1;16;308", doubleValue);
            Assert.Equal(doubleValue, client.ReadDouble("1;3;308").ResultValue);

            client.Write("1;5;20", boolValue);
            Assert.Equal(boolValue, client.ReadBoolean("1;1;20").ResultValue);
        }

        private void 执行批量读写测试()
        {
            // 批量写入并读取布尔值数组
            bool[] boolValues = { true, false, true, true, false, true, false, false, true, true };
            client.Write("1;15;100", boolValues);
            var boolReadResult = client.ReadBoolean("1;1;100", boolValues.Length);
            Assert.True(boolReadResult.IsSuccess);
            for (int i = 0; i < boolValues.Length; i++)
            {
                Assert.Equal(boolValues[i], boolReadResult.ResultValue[i]);
            }

            // 批量写入并读取短整型数组
            short[] shortValues = { 1000, 2000, 3000, 4000, 5000 };
            client.Write("1;16;400", shortValues);
            var shortReadResult = client.ReadInt16("1;3;400", shortValues.Length);
            Assert.True(shortReadResult.IsSuccess);
            for (int i = 0; i < shortValues.Length; i++)
            {
                Assert.Equal(shortValues[i], shortReadResult.ResultValue[i]);
            }

            // 批量写入并读取整型数组
            int[] intValues = { 100000, 200000, 300000, 400000, 500000 };
            client.Write("1;16;500", intValues);
            var intReadResult = client.ReadInt32("1;3;500", intValues.Length);
            Assert.True(intReadResult.IsSuccess);
            for (int i = 0; i < intValues.Length; i++)
            {
                Assert.Equal(intValues[i], intReadResult.ResultValue[i]);
            }

            // 批量写入并读取浮点数组
            float[] floatValues = { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f };
            client.Write("1;16;600", floatValues);
            var floatReadResult = client.ReadFloat("1;3;600", floatValues.Length);
            Assert.True(floatReadResult.IsSuccess);
            for (int i = 0; i < floatValues.Length; i++)
            {
                Assert.Equal(floatValues[i], floatReadResult.ResultValue[i]);
            }
        }
    }
} 
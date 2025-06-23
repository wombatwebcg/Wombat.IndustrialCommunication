using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.PLC;
using Wombat.IndustrialCommunicationTestProject.Helper;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    public class S7_Smart200
    {
        private SiemensClient client;
        private readonly ITestOutputHelper _output;
        private ConnectionDisruptorExtreme _disruptor;
        
        public S7_Smart200(ITestOutputHelper output = null)
        {
            _output = output;
            // 创建连接中断模拟器
            _disruptor = new ConnectionDisruptorExtreme(new XUnitLogger(_output, "Disruptor"));
        }
        
        [Fact]
        public void Smart200读写测试()
        {

            client = new SiemensClient("192.168.11.51", 102, SiemensVersion.S7_200Smart);
            client.Connect();
            ReadWrite();
            client.Disconnect();

        }

        [Fact]
        public async Task Smart200读写异步测试()
        {
            _output?.WriteLine("开始异步读写测试");
            client = new SiemensClient("192.168.11.51", 102, SiemensVersion.S7_200Smart);
            
            try
            {
                _output?.WriteLine("连接PLC");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                
                _output?.WriteLine("执行异步读写测试");
                await ReadWriteAsync();
                
                _output?.WriteLine("断开连接");
                var disconnectResult = await client.DisconnectAsync();
                Assert.True(disconnectResult.IsSuccess, $"断开连接失败: {disconnectResult.Message}");
            }
            catch (Exception ex)
            {
                _output?.WriteLine($"测试异常: {ex.Message}");
                throw;
            }
            finally
            {
                if (client.Connected)
                {
                    await client.DisconnectAsync();
                }
            }
        }

        [Fact]
        public async Task Smart200断线重连测试()
        {
            _output?.WriteLine("开始断线重连测试");
            client = new SiemensClient("192.168.11.51", 102, SiemensVersion.S7_200Smart);
            
            try
            {
                // 配置自动重连
                _output?.WriteLine("配置自动重连参数");
                client.EnableAutoReconnect = true;
                client.MaxReconnectAttempts = 3;
                client.ReconnectDelay = TimeSpan.FromSeconds(5);
                
                // 连接PLC
                _output?.WriteLine("连接PLC");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                Assert.True(client.Connected, "连接状态应为已连接");
                
                // 进行一次正常读写，确保连接有效
                _output?.WriteLine("执行读写测试，验证连接有效");
                var writeResult = await client.WriteAsync("V700", 12345);
                Assert.True(writeResult.IsSuccess, $"写入失败: {writeResult.Message}");
                var readResult = await client.ReadInt32Async("V700");
                Assert.True(readResult.IsSuccess, $"读取失败: {readResult.Message}");
                Assert.Equal(12345, readResult.ResultValue);
                
                // 模拟连接中断
                _output?.WriteLine("模拟连接中断");
                var disruptResult = await _disruptor.SimulateSafeWait(client, 1000);
                Assert.True(disruptResult.IsSuccess, $"模拟中断失败: {disruptResult.Message}");
                
                // 等待自动重连
                _output?.WriteLine("等待自动重连...");
                await Task.Delay(7000); // 等待时间略长于重连延迟
                
                // 验证是否已重连
                _output?.WriteLine($"检查连接状态: {(client.Connected ? "已连接" : "未连接")}");
                
                // 尝试读写，验证连接是否已恢复
                _output?.WriteLine("尝试读写，验证连接恢复");
                writeResult = await client.WriteAsync("V700", 54321);
                if (writeResult.IsSuccess)
                {
                    _output?.WriteLine("写入成功，连接已恢复");
                    readResult = await client.ReadInt32Async("V700");
                    Assert.True(readResult.IsSuccess, $"读取失败: {readResult.Message}");
                    Assert.Equal(54321, readResult.ResultValue);
                }
                else
                {
                    _output?.WriteLine($"写入失败: {writeResult.Message}，连接可能未恢复");
                    Assert.True(client.Connected, "连接状态应为已连接");
                }
                
                // 断开连接
                _output?.WriteLine("清理：断开连接");
                client.EnableAutoReconnect = false; // 禁用自动重连，确保断开
                await client.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _output?.WriteLine($"测试异常: {ex.Message}");
                throw;
            }
            finally
            {
                // 确保资源释放
                try
                {
                    if (client.Connected)
                    {
                        _output?.WriteLine("确保断开连接");
                        client.EnableAutoReconnect = false;
                        await client.DisconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output?.WriteLine($"断开连接异常: {ex.Message}");
                }
            }
        }

        [Fact]
        public async Task Smart200短连接测试()
        {
            _output?.WriteLine("开始短连接测试");
            client = new SiemensClient("192.168.11.51", 102, SiemensVersion.S7_200Smart);
            
            try
            {
                // 配置短连接模式
                _output?.WriteLine("配置短连接模式");
                client.IsLongConnection = false;
                
                // 执行一系列读写操作
                _output?.WriteLine("执行短连接读写操作");
                
                // 写入并读取布尔值
                _output?.WriteLine("测试布尔值读写");
                var writeBoolResult = await client.WriteAsync("Q1.3", true);
                Assert.True(writeBoolResult.IsSuccess, $"写入布尔值失败: {writeBoolResult.Message}");
                var readBoolResult = await client.ReadBooleanAsync("Q1.3");
                Assert.True(readBoolResult.IsSuccess, $"读取布尔值失败: {readBoolResult.Message}");
                Assert.True(readBoolResult.ResultValue, "读取的布尔值应为true");
                
                // 检查连接状态（短连接模式下应已断开）
                _output?.WriteLine($"操作后连接状态: {(client.Connected ? "已连接" : "未连接")}");
                Assert.False(client.Connected, "短连接模式下操作后应已断开");
                
                // 写入并读取整数
                _output?.WriteLine("测试整数读写");
                var writeIntResult = await client.WriteAsync("V700", 12345);
                Assert.True(writeIntResult.IsSuccess, $"写入整数失败: {writeIntResult.Message}");
                var readIntResult = await client.ReadInt32Async("V700");
                Assert.True(readIntResult.IsSuccess, $"读取整数失败: {readIntResult.Message}");
                Assert.Equal(12345, readIntResult.ResultValue);
                
                // 检查连接状态（短连接模式下应已断开）
                _output?.WriteLine($"操作后连接状态: {(client.Connected ? "已连接" : "未连接")}");
                Assert.False(client.Connected, "短连接模式下操作后应已断开");
                
                // 写入并读取浮点数
                _output?.WriteLine("测试浮点数读写");
                float floatValue = 123.45f;
                var writeFloatResult = await client.WriteAsync("V700", floatValue);
                Assert.True(writeFloatResult.IsSuccess, $"写入浮点数失败: {writeFloatResult.Message}");
                var readFloatResult = await client.ReadFloatAsync("V700");
                Assert.True(readFloatResult.IsSuccess, $"读取浮点数失败: {readFloatResult.Message}");
                Assert.Equal(floatValue, readFloatResult.ResultValue);
                
                // 检查连接状态（短连接模式下应已断开）
                _output?.WriteLine($"操作后连接状态: {(client.Connected ? "已连接" : "未连接")}");
                Assert.False(client.Connected, "短连接模式下操作后应已断开");
                
                // 启用自动重连并再次测试
                _output?.WriteLine("启用自动重连并再次测试短连接模式");
                client.EnableAutoReconnect = true;
                client.MaxReconnectAttempts = 1;
                
                // 写入并读取数组
                _output?.WriteLine("测试数组读写（启用自动重连）");
                int[] intArray = { 1, 2, 3, 4, 5 };
                var writeArrayResult = await client.WriteAsync("V700", intArray);
                Assert.True(writeArrayResult.IsSuccess, $"写入数组失败: {writeArrayResult.Message}");
                var readArrayResult = await client.ReadInt32Async("V700", intArray.Length);
                Assert.True(readArrayResult.IsSuccess, $"读取数组失败: {readArrayResult.Message}");
                for (int i = 0; i < intArray.Length; i++)
                {
                    Assert.Equal(intArray[i], readArrayResult.ResultValue[i]);
                }
                
                // 检查连接状态（短连接模式下应已断开）
                _output?.WriteLine($"操作后连接状态: {(client.Connected ? "已连接" : "未连接")}");
                Assert.False(client.Connected, "短连接模式下操作后应已断开");
            }
            catch (Exception ex)
            {
                _output?.WriteLine($"测试异常: {ex.Message}");
                throw;
            }
            finally
            {
                // 确保资源释放
                try
                {
                    if (client.Connected)
                    {
                        _output?.WriteLine("确保断开连接");
                        client.IsLongConnection = true; // 切换回长连接模式进行安全断开
                        client.EnableAutoReconnect = false;
                        await client.DisconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output?.WriteLine($"断开连接异常: {ex.Message}");
                }
            }
        }

        [Fact]
        public async Task 模拟服务器断线重连测试()
        {
            _output?.WriteLine("开始模拟服务器断线重连测试");
            
            // 创建模拟服务器和客户端
            var server = new S7TcpServer("127.0.0.1", 1102);
            client = new SiemensClient("127.0.0.1", 1102, SiemensVersion.S7_1200);
            
            try
            {
                // 配置服务器和客户端
                _output?.WriteLine("配置服务器和客户端");
                server.CreateDataBlock(1, 1024); // 创建DB1数据块，1024字节
                server.UseLogger(new XUnitLogger(_output, "S7Server"));
                
                client.EnableAutoReconnect = true;
                client.MaxReconnectAttempts = 3;
                client.ReconnectDelay = TimeSpan.FromSeconds(2);
                client.Logger = new XUnitLogger(_output, "S7Client");
                
                // 启动服务器
                _output?.WriteLine("启动服务器");
                var serverResult = await server.StartAsync();
                Assert.True(serverResult.IsSuccess, $"启动服务器失败: {serverResult.Message}");
                _output?.WriteLine("服务器启动成功");
                
                // 连接客户端
                _output?.WriteLine("连接客户端");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接客户端失败: {connectResult.Message}");
                Assert.True(client.Connected, "客户端连接状态应为已连接");
                _output?.WriteLine("客户端连接成功");
                
                // 执行基本读写测试
                _output?.WriteLine("执行基本读写测试");
                await client.WriteAsync("DB1.0", 12345);
                var readResult = await client.ReadInt32Async("DB1.0");
                Assert.True(readResult.IsSuccess, $"读取失败: {readResult.Message}");
                Assert.Equal(12345, readResult.ResultValue);
                _output?.WriteLine("基本读写测试成功");
                
                // 停止服务器模拟断线
                _output?.WriteLine("停止服务器模拟断线");
                await server.StopAsync();
                _output?.WriteLine("服务器已停止");
                
                // 等待连接状态更新
                await Task.Delay(1000);
                _output?.WriteLine($"客户端连接状态: {(client.Connected ? "已连接" : "未连接")}");
                
                // 尝试操作，此时应该失败
                _output?.WriteLine("尝试操作，预期失败");
                var failedWriteResult = await client.WriteAsync("DB1.0", 54321);
                _output?.WriteLine($"断线后写入结果: {(failedWriteResult.IsSuccess ? "意外成功" : "预期失败")}");
                
                // 重启服务器
                _output?.WriteLine("重启服务器");
                serverResult = await server.StartAsync();
                Assert.True(serverResult.IsSuccess, $"重启服务器失败: {serverResult.Message}");
                _output?.WriteLine("服务器重启成功");
                
                // 等待自动重连
                _output?.WriteLine("等待自动重连...");
                await Task.Delay(5000); // 等待时间足够长，确保重连尝试
                
                // 验证是否已重连并恢复通信
                _output?.WriteLine($"客户端连接状态: {(client.Connected ? "已连接" : "未连接")}");
                
                // 尝试读写，验证连接是否已恢复
                _output?.WriteLine("尝试读写，验证连接恢复");
                var writeResult = await client.WriteAsync("DB1.0", 54321);
                if (writeResult.IsSuccess)
                {
                    _output?.WriteLine("写入成功，连接已恢复");
                    readResult = await client.ReadInt32Async("DB1.0");
                    Assert.True(readResult.IsSuccess, $"读取失败: {readResult.Message}");
                    Assert.Equal(54321, readResult.ResultValue);
                    _output?.WriteLine("读写测试成功，连接已恢复");
                }
                else
                {
                    _output?.WriteLine($"写入失败: {writeResult.Message}，连接可能未恢复");
                    // 尝试手动重连
                    _output?.WriteLine("尝试手动重连");
                    connectResult = await client.ConnectAsync();
                    Assert.True(connectResult.IsSuccess, $"手动重连失败: {connectResult.Message}");
                    
                    // 再次尝试读写
                    _output?.WriteLine("再次尝试读写");
                    writeResult = await client.WriteAsync("DB1.0", 54321);
                    Assert.True(writeResult.IsSuccess, $"手动重连后写入失败: {writeResult.Message}");
                    readResult = await client.ReadInt32Async("DB1.0");
                    Assert.True(readResult.IsSuccess, $"手动重连后读取失败: {readResult.Message}");
                    Assert.Equal(54321, readResult.ResultValue);
                    _output?.WriteLine("手动重连后读写测试成功");
                }
            }
            catch (Exception ex)
            {
                _output?.WriteLine($"测试异常: {ex.Message}");
                throw;
            }
            finally
            {
                // 确保资源释放
                _output?.WriteLine("清理资源");
                try
                {
                    if (client.Connected)
                    {
                        _output?.WriteLine("断开客户端连接");
                        client.EnableAutoReconnect = false;
                        await client.DisconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output?.WriteLine($"断开客户端异常: {ex.Message}");
                }
                
                try
                {
                    if (server.IsListening)
                    {
                        _output?.WriteLine("关闭服务器");
                        await server.StopAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output?.WriteLine($"关闭服务器异常: {ex.Message}");
                }
                
                _output?.WriteLine("资源清理完成");
            }
        }

        [Fact]
        public async Task 模拟服务器短连接测试()
        {
            _output?.WriteLine("开始模拟服务器短连接测试");
            
            // 创建模拟服务器和客户端
            var server = new S7TcpServer("127.0.0.1", 1102);
            client = new SiemensClient("127.0.0.1", 1102, SiemensVersion.S7_1200);
            
            try
            {
                // 配置服务器和客户端
                _output?.WriteLine("配置服务器和客户端");
                server.CreateDataBlock(1, 1024); // 创建DB1数据块，1024字节
                server.UseLogger(new XUnitLogger(_output, "S7Server"));
                
                client.IsLongConnection = false; // 配置为短连接模式
                client.Logger = new XUnitLogger(_output, "S7Client");
                
                // 启动服务器
                _output?.WriteLine("启动服务器");
                var serverResult = await server.StartAsync();
                Assert.True(serverResult.IsSuccess, $"启动服务器失败: {serverResult.Message}");
                _output?.WriteLine("服务器启动成功");
                
                // 测试不同数据类型的读写
                await TestShortConnectionReadWrite<bool>("DB1.0.0", true, 
                    (addr) => client.ReadBooleanAsync(addr), 
                    (addr, val) => client.WriteAsync(addr, val));
                await TestShortConnectionReadWrite<short>("DB1.2", (short)12345, 
                    (addr) => client.ReadInt16Async(addr), 
                    (addr, val) => client.WriteAsync(addr, val));
                await TestShortConnectionReadWrite<ushort>("DB1.4", (ushort)54321, 
                    (addr) => client.ReadUInt16Async(addr), 
                    (addr, val) => client.WriteAsync(addr, val));
                await TestShortConnectionReadWrite<int>("DB1.6", 123456789, 
                    (addr) => client.ReadInt32Async(addr), 
                    (addr, val) => client.WriteAsync(addr, val));
                await TestShortConnectionReadWrite<uint>("DB1.10", (uint)987654321, 
                    (addr) => client.ReadUInt32Async(addr), 
                    (addr, val) => client.WriteAsync(addr, val));
                await TestShortConnectionReadWrite<float>("DB1.14", 123.456f, 
                    (addr) => client.ReadFloatAsync(addr), 
                    (addr, val) => client.WriteAsync(addr, val));
                await TestShortConnectionReadWrite<double>("DB1.18", 789.012, 
                    (addr) => client.ReadDoubleAsync(addr), 
                    (addr, val) => client.WriteAsync(addr, val));
                
                // 测试数组读写
                _output?.WriteLine("测试数组读写");
                int[] intArray = { 1, 2, 3, 4, 5 };
                var writeArrayResult = await client.WriteAsync("DB1.30", intArray);
                Assert.True(writeArrayResult.IsSuccess, $"写入数组失败: {writeArrayResult.Message}");
                Assert.False(client.Connected, "短连接操作后应已断开");
                
                var readArrayResult = await client.ReadInt32Async("DB1.30", intArray.Length);
                Assert.True(readArrayResult.IsSuccess, $"读取数组失败: {readArrayResult.Message}");
                Assert.False(client.Connected, "短连接操作后应已断开");
                
                for (int i = 0; i < intArray.Length; i++)
                {
                    Assert.Equal(intArray[i], readArrayResult.ResultValue[i]);
                }
                _output?.WriteLine("数组读写测试成功");
                
                // 启用自动重连并再次测试
                _output?.WriteLine("启用自动重连并再次测试短连接模式");
                client.EnableAutoReconnect = true;
                client.MaxReconnectAttempts = 1;
                
                // 测试带自动重连的短连接
                await TestShortConnectionReadWrite<long>("DB1.50", 9223372036854775807, 
                    (addr) => client.ReadInt64Async(addr), 
                    (addr, val) => client.WriteAsync(addr, val));
                await TestShortConnectionReadWrite<ulong>("DB1.58", 18446744073709551615, 
                    (addr) => client.ReadUInt64Async(addr), 
                    (addr, val) => client.WriteAsync(addr, val));
                
                // 测试服务器停止和启动期间的短连接行为
                _output?.WriteLine("测试服务器停止时的短连接行为");
                await server.StopAsync();
                _output?.WriteLine("服务器已停止");
                
                // 尝试操作，此时应该失败
                var failedWriteResult = await client.WriteAsync("DB1.0", 54321);
                _output?.WriteLine($"服务器停止后写入结果: {(failedWriteResult.IsSuccess ? "意外成功" : "预期失败")}");
                
                // 重启服务器
                _output?.WriteLine("重启服务器");
                serverResult = await server.StartAsync();
                Assert.True(serverResult.IsSuccess, $"重启服务器失败: {serverResult.Message}");
                _output?.WriteLine("服务器重启成功");
                
                // 验证服务器重启后的短连接
                await TestShortConnectionReadWrite<int>("DB1.100", 42, 
                    (addr) => client.ReadInt32Async(addr), 
                    (addr, val) => client.WriteAsync(addr, val));
                _output?.WriteLine("服务器重启后短连接测试成功");
            }
            catch (Exception ex)
            {
                _output?.WriteLine($"测试异常: {ex.Message}");
                throw;
            }
            finally
            {
                // 确保资源释放
                _output?.WriteLine("清理资源");
                try
                {
                    if (client.Connected)
                    {
                        _output?.WriteLine("断开客户端连接");
                        client.IsLongConnection = true; // 切换回长连接模式进行安全断开
                        client.EnableAutoReconnect = false;
                        await client.DisconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output?.WriteLine($"断开客户端异常: {ex.Message}");
                }
                
                try
                {
                    if (server.IsListening)
                    {
                        _output?.WriteLine("关闭服务器");
                        await server.StopAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output?.WriteLine($"关闭服务器异常: {ex.Message}");
                }
                
                _output?.WriteLine("资源清理完成");
            }
        }

        /// <summary>
        /// 测试短连接模式下的读写操作
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="readFunc">读取函数</param>
        /// <param name="writeFunc">写入函数</param>
        private async Task TestShortConnectionReadWrite<T>(string address, T value, 
            Func<string, ValueTask<OperationResult<T>>> readFunc, 
            Func<string, T, Task<OperationResult>> writeFunc)
        {
            _output?.WriteLine($"测试短连接读写: 类型={typeof(T).Name}, 地址={address}, 值={value}");
            
            // 写入值
            var writeResult = await writeFunc(address, value);
            Assert.True(writeResult.IsSuccess, $"写入失败: {writeResult.Message}");
            Assert.False(client.Connected, "短连接操作后应已断开");
            
            // 读取值
            var readResult = await readFunc(address);
            Assert.True(readResult.IsSuccess, $"读取失败: {readResult.Message}");
            Assert.False(client.Connected, "短连接操作后应已断开");
            
            // 验证值
            Assert.Equal(value, readResult.ResultValue);
            _output?.WriteLine($"短连接读写测试成功: {value} == {readResult.ResultValue}");
        }

        [Fact]
        public async Task 模拟服务器混合场景压力测试()
        {
            _output?.WriteLine("开始模拟服务器混合场景压力测试");
            
            // 创建模拟服务器和客户端
            var server = new S7TcpServer("127.0.0.1", 1102);
            client = new SiemensClient("127.0.0.1", 1102, SiemensVersion.S7_1200);
            
            try
            {
                // 配置服务器和客户端
                _output?.WriteLine("配置服务器和客户端");
                server.CreateDataBlock(1, 1024); // 创建DB1数据块，1024字节
                server.UseLogger(new XUnitLogger(_output, "S7Server"));
                
                client.EnableAutoReconnect = true;
                client.MaxReconnectAttempts = 5;
                client.ReconnectDelay = TimeSpan.FromSeconds(1);
                client.Logger = new XUnitLogger(_output, "S7Client");
                
                // 启动服务器
                _output?.WriteLine("启动服务器");
                var serverResult = await server.StartAsync();
                Assert.True(serverResult.IsSuccess, $"启动服务器失败: {serverResult.Message}");
                _output?.WriteLine("服务器启动成功");
                
                // 连接客户端
                _output?.WriteLine("连接客户端");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接客户端失败: {connectResult.Message}");
                Assert.True(client.Connected, "客户端连接状态应为已连接");
                _output?.WriteLine("客户端连接成功");
                
                // 进行高频读写测试
                _output?.WriteLine("执行高频读写测试");
                await RunHighFrequencyReadWriteTest();
                
                // 在读写期间模拟服务器重启
                _output?.WriteLine("在读写期间模拟服务器重启");
                await RunServerRestartDuringOperationTest(server);
                
                // 测试长连接和短连接混合使用
                _output?.WriteLine("测试长连接和短连接混合使用");
                await RunMixedConnectionTest();
                
                // 测试多种数据类型混合读写
                _output?.WriteLine("测试多种数据类型混合读写");
                await RunMixedDataTypeTest();
            }
            catch (Exception ex)
            {
                _output?.WriteLine($"测试异常: {ex.Message}");
                throw;
            }
            finally
            {
                // 确保资源释放
                _output?.WriteLine("清理资源");
                try
                {
                    if (client.Connected)
                    {
                        _output?.WriteLine("断开客户端连接");
                        client.EnableAutoReconnect = false;
                        await client.DisconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output?.WriteLine($"断开客户端异常: {ex.Message}");
                }
                
                try
                {
                    if (server.IsListening)
                    {
                        _output?.WriteLine("关闭服务器");
                        await server.StopAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output?.WriteLine($"关闭服务器异常: {ex.Message}");
                }
                
                _output?.WriteLine("资源清理完成");
            }
        }
        
        /// <summary>
        /// 高频读写测试
        /// </summary>
        private async Task RunHighFrequencyReadWriteTest()
        {
            _output?.WriteLine("开始高频读写测试");
            
            // 创建测试数据
            var random = new Random();
            int[] testValues = new int[10];
            for (int i = 0; i < testValues.Length; i++)
            {
                testValues[i] = random.Next(1, 10000);
            }
            
            // 执行高频读写
            _output?.WriteLine("执行100次快速读写操作");
            int successCount = 0;
            int failureCount = 0;
            
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    // 选择一个随机测试值
                    int valueIndex = random.Next(0, testValues.Length);
                    int testValue = testValues[valueIndex];
                    
                    // 写入数据
                    var writeResult = await client.WriteAsync("DB1.0", testValue);
                    if (writeResult.IsSuccess)
                    {
                        // 读取数据
                        var readResult = await client.ReadInt32Async("DB1.0");
                        if (readResult.IsSuccess && readResult.ResultValue == testValue)
                        {
                            successCount++;
                        }
                        else
                        {
                            failureCount++;
                            _output?.WriteLine($"读写不匹配: 期望={testValue}, 实际={readResult.ResultValue}");
                        }
                    }
                    else
                    {
                        failureCount++;
                        _output?.WriteLine($"写入失败: {writeResult.Message}");
                    }
                    
                    // 短暂延迟，避免过载
                    await Task.Delay(10);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _output?.WriteLine($"读写异常: {ex.Message}");
                }
            }
            
            _output?.WriteLine($"高频读写测试完成: 成功={successCount}, 失败={failureCount}");
            // 断言大部分操作成功
            Assert.True(successCount > 90, $"高频读写测试成功率过低: {successCount}/100");
        }
        
        /// <summary>
        /// 在操作期间重启服务器测试
        /// </summary>
        private async Task RunServerRestartDuringOperationTest(S7TcpServer server)
        {
            _output?.WriteLine("开始服务器重启测试");
            
            // 启动一个后台任务进行连续读写
            var readWriteTask = Task.Run(async () =>
            {
                int successCount = 0;
                int failureCount = 0;
                
                for (int i = 0; i < 50; i++)
                {
                    try
                    {
                        // 写入数据
                        var writeResult = await client.WriteAsync("DB1.10", i);
                        // 读取数据
                        var readResult = await client.ReadInt32Async("DB1.10");
                        
                        if (writeResult.IsSuccess && readResult.IsSuccess && readResult.ResultValue == i)
                        {
                            successCount++;
                        }
                        else
                        {
                            failureCount++;
                        }
                        
                        await Task.Delay(100); // 降低频率，给服务器重启时间
                    }
                    catch
                    {
                        failureCount++;
                    }
                }
                
                return (successCount, failureCount);
            });
            
            // 等待一段时间让读写操作开始
            await Task.Delay(500);
            
            // 停止服务器
            _output?.WriteLine("停止服务器");
            await server.StopAsync();
            _output?.WriteLine("服务器已停止");
            
            // 等待一段时间
            await Task.Delay(2000);
            
            // 重启服务器
            _output?.WriteLine("重启服务器");
            await server.StartAsync();
            _output?.WriteLine("服务器已重启");
            
            // 等待读写任务完成
            var result = await readWriteTask;
            
            _output?.WriteLine($"服务器重启测试完成: 成功={result.successCount}, 失败={result.failureCount}");
            // 应该有一些成功的操作
            Assert.True(result.successCount > 0, "服务器重启后应有成功操作");
        }
        
        /// <summary>
        /// 长连接和短连接混合测试
        /// </summary>
        private async Task RunMixedConnectionTest()
        {
            _output?.WriteLine("开始长短连接混合测试");
            
            // 记录当前连接模式
            bool originalLongConnection = client.IsLongConnection;
            
            try
            {
                // 测试长连接模式
                _output?.WriteLine("使用长连接模式");
                client.IsLongConnection = true;
                
                // 执行几次读写操作
                for (int i = 0; i < 5; i++)
                {
                    await client.WriteAsync("DB1.20", i * 10);
                    var readResult = await client.ReadInt32Async("DB1.20");
                    Assert.True(readResult.IsSuccess, $"长连接读取失败: {readResult.Message}");
                    Assert.Equal(i * 10, readResult.ResultValue);
                    Assert.True(client.Connected, "长连接模式下应保持连接");
                }
                
                // 切换到短连接模式
                _output?.WriteLine("切换到短连接模式");
                client.IsLongConnection = false;
                
                // 执行几次读写操作
                for (int i = 0; i < 5; i++)
                {
                    await client.WriteAsync("DB1.24", i * 100);
                    var readResult = await client.ReadInt32Async("DB1.24");
                    Assert.True(readResult.IsSuccess, $"短连接读取失败: {readResult.Message}");
                    Assert.Equal(i * 100, readResult.ResultValue);
                    Assert.False(client.Connected, "短连接模式下操作后应断开");
                }
                
                // 再次切换回长连接模式
                _output?.WriteLine("切换回长连接模式");
                client.IsLongConnection = true;
                
                // 重新建立连接
                await client.ConnectAsync();
                
                // 验证之前写入的数据
                var longConnResult = await client.ReadInt32Async("DB1.20");
                var shortConnResult = await client.ReadInt32Async("DB1.24");
                
                Assert.True(longConnResult.IsSuccess, "读取长连接数据失败");
                Assert.True(shortConnResult.IsSuccess, "读取短连接数据失败");
                Assert.Equal(40, longConnResult.ResultValue); // 最后写入的值是 4*10
                Assert.Equal(400, shortConnResult.ResultValue); // 最后写入的值是 4*100
                
                _output?.WriteLine("长短连接混合测试成功");
            }
            finally
            {
                // 恢复原始连接模式
                client.IsLongConnection = originalLongConnection;
            }
        }
        
        /// <summary>
        /// 多种数据类型混合读写测试
        /// </summary>
        private async Task RunMixedDataTypeTest()
        {
            _output?.WriteLine("开始多种数据类型混合读写测试");
            
            // 测试不同数据类型的读写
            bool boolValue = true;
            short shortValue = 12345;
            ushort ushortValue = 54321;
            int intValue = 123456789;
            uint uintValue = 987654321;
            float floatValue = 123.456f;
            double doubleValue = 789.012;
            
            // 一次性写入所有数据类型
            _output?.WriteLine("写入不同数据类型");
            await client.WriteAsync("DB1.100.0", boolValue);
            await client.WriteAsync("DB1.102", shortValue);
            await client.WriteAsync("DB1.104", ushortValue);
            await client.WriteAsync("DB1.106", intValue);
            await client.WriteAsync("DB1.110", uintValue);
            await client.WriteAsync("DB1.114", floatValue);
            await client.WriteAsync("DB1.118", doubleValue);
            
            // 随机顺序读取所有数据类型
            _output?.WriteLine("随机顺序读取不同数据类型");
            var doubleResult = await client.ReadDoubleAsync("DB1.118");
            var ushortResult = await client.ReadUInt16Async("DB1.104");
            var floatResult = await client.ReadFloatAsync("DB1.114");
            var intResult = await client.ReadInt32Async("DB1.106");
            var boolResult = await client.ReadBooleanAsync("DB1.100.0");
            var shortResult = await client.ReadInt16Async("DB1.102");
            var uintResult = await client.ReadUInt32Async("DB1.110");
            
            // 验证所有读取结果
            Assert.True(boolResult.IsSuccess, "读取布尔值失败");
            Assert.True(shortResult.IsSuccess, "读取短整数失败");
            Assert.True(ushortResult.IsSuccess, "读取无符号短整数失败");
            Assert.True(intResult.IsSuccess, "读取整数失败");
            Assert.True(uintResult.IsSuccess, "读取无符号整数失败");
            Assert.True(floatResult.IsSuccess, "读取浮点数失败");
            Assert.True(doubleResult.IsSuccess, "读取双精度浮点数失败");
            
            Assert.Equal(boolValue, boolResult.ResultValue);
            Assert.Equal(shortValue, shortResult.ResultValue);
            Assert.Equal(ushortValue, ushortResult.ResultValue);
            Assert.Equal(intValue, intResult.ResultValue);
            Assert.Equal(uintValue, uintResult.ResultValue);
            Assert.Equal(floatValue, floatResult.ResultValue);
            Assert.Equal(doubleValue, doubleResult.ResultValue);
            
            _output?.WriteLine("多种数据类型混合读写测试成功");
        }

        private void ReadWrite()
        {
            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            for (int i = 0; i < 50; i++)
            {
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                ushort short_number_1 = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);

                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                uint int_number_1 = (uint)rnd.Next(0, int.MaxValue);

                float float_number = int_number / 100;
                var bool_value = short_number % 2 == 1;

                string value_string = "BennyZhao";


                var ssss2 = client.Write("Q1.3", true);
                var sss1 = client.ReadBoolean("Q1.3");
                Assert.True(client.ReadBoolean("Q1.3").ResultValue == true);
                client.Write("Q1.4", bool_value);
                Assert.True(client.ReadBoolean("Q1.4").ResultValue == bool_value);
                client.Write("Q1.5", !bool_value);
                Assert.True(client.ReadBoolean("Q1.5").ResultValue == !bool_value);

                client.Write("V700", short_number);
                Assert.True(client.ReadInt16("V700").ResultValue == short_number);
                client.Write("V700", short_number_1);
                Assert.True(client.ReadUInt16("V700").ResultValue == short_number_1);

                client.Write("V700", int_number);
                Assert.True(client.ReadInt32("V700").ResultValue == int_number);
                client.Write("V700", int_number_1);
                Assert.True(client.ReadUInt32("V700").ResultValue == int_number_1);

                client.Write("V700", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64("V700").ResultValue == Convert.ToInt64(int_number));
                client.Write("V700", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64("V700").ResultValue == Convert.ToUInt64(int_number_1));

                client.Write("V700", float_number);
                Assert.True(client.ReadFloat("V700").ResultValue == float_number);
                client.Write("V700", Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble("V700").ResultValue == Convert.ToDouble(float_number));

                //var rrr =  client.Write("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).ResultValue == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                client.Write("V700", bool_values);
                var bool_values_result = client.ReadBoolean("V700", bool_values.Length);
                for (int j = 0; j < bool_values_result.ResultValue.Length; j++)
                {
                    Assert.True(bool_values_result.ResultValue[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", short_values);
                var short_values_result = client.ReadInt16("V700", short_values.Length);
                for (int j = 0; j < short_values_result.ResultValue.Length; j++)
                {
                    Assert.True(short_values_result.ResultValue[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", ushort_values);
                var ushort_values_result = client.ReadInt16("V700", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.ResultValue.Length; j++)
                {
                    Assert.True(ushort_values_result.ResultValue[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", int_values);
                var int_values_result = client.ReadInt32("V700", int_values.Length);
                for (int j = 0; j < int_values_result.ResultValue.Length; j++)
                {
                    Assert.True(int_values_result.ResultValue[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", uint_values);
                var uint_values_result = client.ReadUInt32("V700", uint_values.Length);
                for (int j = 0; j < uint_values_result.ResultValue.Length; j++)
                {
                    Assert.True(uint_values_result.ResultValue[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", long_values);
                var long_values_result = client.ReadInt64("V700", long_values.Length);
                for (long j = 0; j < long_values_result.ResultValue.Length; j++)
                {
                    Assert.True(long_values_result.ResultValue[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", ulong_values);
                var ulong_values_result = client.ReadUInt64("V700", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.ResultValue.Length; j++)
                {
                    Assert.True(ulong_values_result.ResultValue[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", float_values);
                var float_values_result = client.ReadFloat("V700", float_values.Length);
                for (int j = 0; j < float_values_result.ResultValue.Length; j++)
                {
                    Assert.True(float_values_result.ResultValue[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", double_values);
                var double_values_result = client.ReadDouble("V700", double_values.Length);
                for (int j = 0; j < double_values_result.ResultValue.Length; j++)
                {
                    Assert.True(double_values_result.ResultValue[j] == double_values[j]);

                }



            }

        }


        private async Task ReadWriteAsync()
        {

            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            for (int i = 0; i < 50; i++)
            {
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                ushort short_number_1 = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);

                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                uint int_number_1 = (uint)rnd.Next(0, int.MaxValue);

                float float_number = int_number / 100;
                var bool_value = short_number % 2 == 1;

                string value_string = "BennyZhao";

                await client.WriteAsync("Q1.3", true);
                Assert.True(client.ReadBooleanAsync("Q1.3").Result.ResultValue == true);
                await client.WriteAsync("Q1.4", bool_value);
                Assert.True(client.ReadBooleanAsync("Q1.4").Result.ResultValue == bool_value);
                await client.WriteAsync("Q1.5", !bool_value);
                Assert.True(client.ReadBooleanAsync("Q1.5").Result.ResultValue == !bool_value);

                var ssss = await client.WriteAsync("V700", short_number);
                var tttt = client.ReadInt16Async("V700");
                Assert.True(client.ReadInt16Async("V700").Result.ResultValue == short_number);
                await client.WriteAsync("V700", short_number_1);
                Assert.True(client.ReadUInt16Async("V700").Result.ResultValue == short_number_1);

                await client.WriteAsync("V700", int_number);
                Assert.True(client.ReadInt32Async("V700").Result.ResultValue == int_number);
                await client.WriteAsync("V700", int_number_1);
                Assert.True(client.ReadUInt32Async("V700").Result.ResultValue == int_number_1);

                await client.WriteAsync("V700", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64Async("V700").Result.ResultValue == Convert.ToInt64(int_number));
                await client.WriteAsync("V700", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64Async("V700").Result.ResultValue == Convert.ToUInt64(int_number_1));

                await client.WriteAsync("V700", float_number);
                Assert.True(client.ReadFloatAsync("V700").Result.ResultValue == float_number);
                await client.WriteAsync("V700", Convert.ToDouble(float_number));
                Assert.True(client.ReadDoubleAsync("V700").Result.ResultValue == Convert.ToDouble(float_number));

                //var rrr =  await client.WriteAsync("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).ResultValue == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                await client.WriteAsync("V700", bool_values);
                var bool_values_result = client.ReadBooleanAsync("V700", bool_values.Length);
                for (int j = 0; j < bool_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(bool_values_result.Result.ResultValue[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", short_values);
                var short_values_result = client.ReadInt16Async("V700", short_values.Length);
                for (int j = 0; j < short_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(short_values_result.Result.ResultValue[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", ushort_values);
                var ushort_values_result = client.ReadInt16Async("V700", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(ushort_values_result.Result.ResultValue[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", int_values);
                var int_values_result = client.ReadInt32Async("V700", int_values.Length);
                for (int j = 0; j < int_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(int_values_result.Result.ResultValue[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", uint_values);
                var uint_values_result = client.ReadUInt32Async("V700", uint_values.Length);
                for (int j = 0; j < uint_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(uint_values_result.Result.ResultValue[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", long_values);
                var long_values_result = client.ReadInt64Async("V700", long_values.Length);
                for (long j = 0; j < long_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(long_values_result.Result.ResultValue[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", ulong_values);
                var ulong_values_result = client.ReadUInt64Async("V700", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(ulong_values_result.Result.ResultValue[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", float_values);
                var float_values_result = client.ReadFloatAsync("V700", float_values.Length);
                for (int j = 0; j < float_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(float_values_result.Result.ResultValue[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", double_values);
                var double_values_result = client.ReadDoubleAsync("V700", double_values.Length);
                for (int j = 0; j < double_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(double_values_result.Result.ResultValue[j] == double_values[j]);

                }



            }

        }



    }
}

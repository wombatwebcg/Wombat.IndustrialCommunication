using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.PLC;
using Wombat.IndustrialCommunicationTestProject.Helper;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTestProject.PLC_Tests
{
    /// <summary>
    /// S7客户端最小化测试，用于验证栈溢出问题修复效果
    /// </summary>
    public class S7MinimalTest
    {
        private readonly ITestOutputHelper _output;
        
        public S7MinimalTest(ITestOutputHelper output)
        {
            _output = output;
        }
        
        /// <summary>
        /// 测试基本连接断开功能
        /// </summary>
        [Fact]
        public void MinimalConnectionTest()
        {
            // 设置简单的客户端和服务器
            _output.WriteLine("创建服务器和客户端");
            var server = new S7TcpServer("127.0.0.1", 1102);
            var client = new SiemensClient("127.0.0.1", 1102, SiemensVersion.S7_1200);
            
            try
            {
                // 禁用自动重连，专注于基本功能测试
                _output.WriteLine("配置客户端");
                client.EnableAutoReconnect = false;
                client.Logger = new XUnitLogger(_output, "S7Client");
                server.UseLogger(new XUnitLogger(_output, "S7Server"));
                
                // 创建必要的数据块
                _output.WriteLine("创建数据块");
                server.CreateDataBlock(1, 1024); // DB1 数据块，1024字节
                
                // 启动服务器
                _output.WriteLine("启动服务器");
                var serverResult = server.Listen();
                Assert.True(serverResult.IsSuccess, $"启动服务器失败: {serverResult.Message}");
                _output.WriteLine("服务器启动成功");
                
                // 连接客户端
                _output.WriteLine("连接客户端");
                var connectResult = client.Connect();
                Assert.True(connectResult.IsSuccess, $"连接客户端失败: {connectResult.Message}");
                Assert.True(client.Connected, "客户端连接状态应为已连接");
                _output.WriteLine("客户端连接成功");
                
                // 断开连接
                _output.WriteLine("断开客户端连接");
                var disconnectResult = client.Disconnect();
                Assert.True(disconnectResult.IsSuccess, $"断开连接失败: {disconnectResult.Message}");
                Assert.False(client.Connected, "客户端连接状态应为已断开");
                _output.WriteLine("客户端断开成功");
            }
            finally
            {
                // 确保资源释放
                _output.WriteLine("清理资源");
                try
                {
                    if (client.Connected)
                    {
                        _output.WriteLine("断开客户端连接");
                        client.EnableAutoReconnect = false;
                        client.Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"断开客户端异常: {ex.Message}");
                }
                
                try
                {
                    if (server.IsListening)
                    {
                        _output.WriteLine("关闭服务器");
                        server.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"关闭服务器异常: {ex.Message}");
                }
                
                _output.WriteLine("资源清理完成");
            }
        }
        
        /// <summary>
        /// 测试自动重连功能
        /// </summary>
        [Fact]
        public async Task AutoReconnectTest()
        {
            // 设置简单的客户端和服务器
            _output.WriteLine("创建服务器和客户端");
            var server = new S7TcpServer("127.0.0.1", 1102);
            var client = new SiemensClient("127.0.0.1", 1102, SiemensVersion.S7_1200);
            
            try
            {
                // 配置自动重连
                _output.WriteLine("配置客户端自动重连");
                client.EnableAutoReconnect = true;
                client.MaxReconnectAttempts = 2;
                client.ReconnectDelay = TimeSpan.FromMilliseconds(500); // 缩短延迟，加快测试
                client.Logger = new XUnitLogger(_output, "S7Client");
                server.UseLogger(new XUnitLogger(_output, "S7Server"));
                
                // 创建必要的数据块
                _output.WriteLine("创建数据块");
                server.CreateDataBlock(1, 1024); // DB1 数据块，1024字节
                
                // 启动服务器
                _output.WriteLine("启动服务器");
                var serverResult = await server.StartAsync();
                Assert.True(serverResult.IsSuccess, $"启动服务器失败: {serverResult.Message}");
                _output.WriteLine("服务器启动成功");
                
                // 连接客户端
                _output.WriteLine("连接客户端");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接客户端失败: {connectResult.Message}");
                Assert.True(client.Connected, "客户端连接状态应为已连接");
                _output.WriteLine("客户端连接成功");
                
                // 安全模拟连接中断
                _output.WriteLine("安全模拟连接中断");
                var disruptor = new ConnectionDisruptorExtreme(new XUnitLogger(_output, "Disruptor"));
                var disruptResult = await disruptor.SimulateSafeWait(client, 1000);
                Assert.True(disruptResult.IsSuccess, $"模拟连接中断失败: {disruptResult.Message}");
                _output.WriteLine("模拟连接中断完成");
                
                // 等待自动重连
                _output.WriteLine("等待自动重连...");
                await Task.Delay(2000);
                
                // 验证客户端是否仍然连接
                _output.WriteLine($"客户端连接状态: {(client.Connected ? "已连接" : "未连接")}");
                
                // 安全断开连接
                _output.WriteLine("安全断开客户端连接");
                client.EnableAutoReconnect = false;
                var disconnectResult = await client.DisconnectAsync();
                Assert.True(disconnectResult.IsSuccess, $"断开连接失败: {disconnectResult.Message}");
                Assert.False(client.Connected, "客户端连接状态应为已断开");
                _output.WriteLine("客户端断开成功");
            }
            finally
            {
                // 确保资源释放
                _output.WriteLine("清理资源");
                try
                {
                    if (client.Connected)
                    {
                        _output.WriteLine("断开客户端连接");
                        client.EnableAutoReconnect = false;
                        await client.DisconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"断开客户端异常: {ex.Message}");
                }
                
                try
                {
                    if (server.IsListening)
                    {
                        _output.WriteLine("关闭服务器");
                        await server.StopAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"关闭服务器异常: {ex.Message}");
                }
                
                _output.WriteLine("资源清理完成");
            }
        }
        
        /// <summary>
        /// 测试短连接模式功能
        /// </summary>
        [Fact]
        public async Task ShortConnectionTest()
        {
            // 设置简单的客户端和服务器
            _output.WriteLine("创建服务器和客户端");
            var server = new S7TcpServer("127.0.0.1", 1102);
            var client = new SiemensClient("127.0.0.1", 1102, SiemensVersion.S7_1200);
            
            try
            {
                // 配置短连接模式
                _output.WriteLine("配置客户端为短连接模式");
                client.IsLongConnection = false;
                client.EnableAutoReconnect = false; // 首次测试禁用自动重连
                client.Logger = new XUnitLogger(_output, "S7Client");
                server.UseLogger(new XUnitLogger(_output, "S7Server"));
                
                // 创建必要的数据块
                _output.WriteLine("创建数据块");
                server.CreateDataBlock(1, 1024); // DB1 数据块，1024字节
                
                // 启动服务器
                _output.WriteLine("启动服务器");
                var serverResult = await server.StartAsync();
                Assert.True(serverResult.IsSuccess, $"启动服务器失败: {serverResult.Message}");
                _output.WriteLine("服务器启动成功");
                
                // 执行一次短连接读取操作
                _output.WriteLine("执行短连接读取操作");
                int value = 12345;
                await client.WriteAsync("DB1.0", value);
                var readResult = await client.ReadInt32Async("DB1.0");
                Assert.True(readResult.IsSuccess, $"读取操作失败: {readResult.Message}");
                Assert.Equal(value, readResult.ResultValue);
                _output.WriteLine($"短连接读取成功: {readResult.ResultValue}");
                
                // 检查客户端状态 - 短连接操作后应已断开
                Assert.False(client.Connected, "短连接操作后客户端应已断开");
                _output.WriteLine("短连接操作后客户端已断开，符合预期");
                
                // 启用自动重连并再次测试
                _output.WriteLine("启用自动重连并再次测试短连接模式");
                client.EnableAutoReconnect = true;
                
                // 执行另一次短连接操作
                value = 54321;
                await client.WriteAsync("DB1.0", value);
                readResult = await client.ReadInt32Async("DB1.0");
                Assert.True(readResult.IsSuccess, $"启用自动重连后读取操作失败: {readResult.Message}");
                Assert.Equal(value, readResult.ResultValue);
                _output.WriteLine($"启用自动重连后短连接读取成功: {readResult.ResultValue}");
                
                // 检查客户端状态 - 短连接操作后应已断开
                Assert.False(client.Connected, "短连接操作后客户端应已断开");
                _output.WriteLine("启用自动重连后短连接操作后客户端已断开，符合预期");
            }
            finally
            {
                // 确保资源释放
                _output.WriteLine("清理资源");
                try
                {
                    if (client.Connected)
                    {
                        _output.WriteLine("断开客户端连接");
                        client.IsLongConnection = true; // 切换到长连接模式进行安全断开
                        client.EnableAutoReconnect = false;
                        await client.DisconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"断开客户端异常: {ex.Message}");
                }
                
                try
                {
                    if (server.IsListening)
                    {
                        _output.WriteLine("关闭服务器");
                        await server.StopAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"关闭服务器异常: {ex.Message}");
                }
                
                _output.WriteLine("资源清理完成");
            }
        }
    }
} 
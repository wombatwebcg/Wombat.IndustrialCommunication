using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunicationTestProject.Helper;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTestProject.Modbus_Tests
{
    /// <summary>
    /// 简单调试测试类，使用简化版的连接中断模拟器
    /// </summary>
    public class SimpleDebugTest
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;
        
        public SimpleDebugTest(ITestOutputHelper output)
        {
            _output = output;
            _logger = TestLoggerFactory.CreateLogger(output, nameof(SimpleDebugTest));
        }
        
        [Fact]
        public void 验证调试版连接中断模拟器创建()
        {
            _output.WriteLine("验证调试版连接中断模拟器创建");
            
            // 创建带日志记录器的调试版连接中断模拟器
            var disruptor = new ConnectionDisruptorDebug(_logger);
            
            // 确认创建成功
            Assert.NotNull(disruptor);
            _output.WriteLine("调试版连接中断模拟器创建成功");
        }
        
        [Fact]
        public async Task 调试版模拟中断测试()
        {
            _output.WriteLine("调试版模拟中断测试");
            
            // 创建ModbusTcpClient但不连接，使用未占用的端口
            var client = new ModbusTcpClient("127.0.0.1", 5080);
            
            try
            {
                // 创建带日志记录器的调试版连接中断模拟器
                var disruptor = new ConnectionDisruptorDebug(_logger);
                
                // 尝试模拟中断连接
                var result = await disruptor.SimulateDisruption(client);
                
                // 确认操作完成，不导致栈溢出
                _output.WriteLine($"模拟中断结果: {result.IsSuccess}，消息: {result.Message}");
                Assert.NotNull(result);
                
                _output.WriteLine("验证通过：操作未导致栈溢出");
            }
            finally
            {
                client.Dispose();
            }
        }
        
        [Fact]
        public async Task 调试版短线重连测试()
        {
            _output.WriteLine("调试版短线重连测试");
            
            // 创建ModbusTcpClient但不连接，使用未占用的端口
            var client = new ModbusTcpClient("127.0.0.1", 5090);
            
            try
            {
                // 设置为长连接模式，但不启用自动重连
                client.IsLongConnection = true;
                client.EnableAutoReconnect = false;
                
                // 创建带日志记录器的调试版连接中断模拟器
                var disruptor = new ConnectionDisruptorDebug(_logger);
                
                // 执行最简单的短线重连测试
                _output.WriteLine("执行短线重连操作");
                var result = await disruptor.SimulateDisruptAndWait(client, 500);
                
                // 输出结果
                _output.WriteLine($"短线重连结果: {result.IsSuccess}，消息: {result.Message}");
                
                // 确认执行完成
                Assert.NotNull(result);
                _output.WriteLine("调试版短线重连测试完成");
            }
            finally
            {
                client.Dispose();
            }
        }
        
        [Fact]
        public async Task 调试版服务器测试()
        {
            _output.WriteLine("调试版服务器测试");
            
            // 创建服务器和客户端
            var server = new ModbusTcpServer("127.0.0.1", 5095);
            var client = new ModbusTcpClient("127.0.0.1", 5095);
            
            try
            {
                // 启动服务器
                _output.WriteLine("启动服务器");
                var result = await server.StartAsync();
                Assert.True(result.IsSuccess, "服务器启动失败");
                
                // 设置客户端为长连接模式，不启用自动重连
                client.IsLongConnection = true;
                client.EnableAutoReconnect = false;
                
                // 连接客户端
                _output.WriteLine("连接客户端");
                result = await client.ConnectAsync();
                Assert.True(result.IsSuccess, "客户端连接失败");
                
                // 验证连接状态
                Assert.True(client.Connected, "客户端应处于连接状态");
                
                // 创建带日志记录器的调试版连接中断模拟器
                var disruptor = new ConnectionDisruptorDebug(_logger);
                
                // 模拟连接中断
                _output.WriteLine("模拟连接中断");
                var disruptResult = await disruptor.SimulateDisruption(client);
                Assert.True(disruptResult.IsSuccess, "模拟中断失败");
                
                // 验证连接已断开
                Assert.False(client.Connected, "客户端应处于断开状态");
                
                _output.WriteLine("调试版服务器测试完成");
            }
            finally
            {
                // 清理资源
                if (client.Connected)
                {
                    await client.DisconnectAsync();
                }
                client.Dispose();
                
                if (server.IsListening)
                {
                    await server.StopAsync();
                }
                server.Dispose();
            }
        }
    }
} 
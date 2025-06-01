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
    /// 极简测试类，使用极简版的连接中断模拟器
    /// </summary>
    public class ExtremeSimpleTest
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;
        
        public ExtremeSimpleTest(ITestOutputHelper output)
        {
            _output = output;
            _logger = TestLoggerFactory.CreateLogger(output, nameof(ExtremeSimpleTest));
        }
        
        [Fact]
        public void 验证极简版连接中断模拟器创建()
        {
            _output.WriteLine("验证极简版连接中断模拟器创建");
            
            // 创建带日志记录器的极简版连接中断模拟器
            var disruptor = new ConnectionDisruptorExtreme(_logger);
            
            // 确认创建成功
            Assert.NotNull(disruptor);
            _output.WriteLine("极简版连接中断模拟器创建成功");
        }
        
        [Fact]
        public async Task 极简版模拟中断测试()
        {
            _output.WriteLine("极简版模拟中断测试");
            
            // 创建ModbusTcpClient但不连接，使用未占用的端口
            var client = new ModbusTcpClient("127.0.0.1", 5100);
            
            try
            {
                // 创建带日志记录器的极简版连接中断模拟器
                var disruptor = new ConnectionDisruptorExtreme(_logger);
                
                // 尝试模拟中断连接
                var result = await disruptor.SimulateSafeDisruption(client);
                
                // 确认操作完成，不导致栈溢出
                _output.WriteLine($"模拟中断结果: {result.IsSuccess}，消息: {result.Message}");
                Assert.NotNull(result);
                Assert.True(result.IsSuccess);
                
                _output.WriteLine("验证通过：操作未导致栈溢出");
            }
            finally
            {
                client.Dispose();
            }
        }
        
        [Fact]
        public async Task 极简版短线重连测试()
        {
            _output.WriteLine("极简版短线重连测试");
            
            // 创建ModbusTcpClient但不连接
            var client = new ModbusTcpClient("127.0.0.1", 5110);
            
            try
            {
                // 设置为长连接模式，但不启用自动重连
                client.IsLongConnection = true;
                client.EnableAutoReconnect = false;
                
                // 创建带日志记录器的极简版连接中断模拟器
                var disruptor = new ConnectionDisruptorExtreme(_logger);
                
                // 执行最简单的短线重连测试
                _output.WriteLine("执行短线重连操作");
                var result = await disruptor.SimulateSafeWait(client, 500);
                
                // 输出结果
                _output.WriteLine($"短线重连结果: {result.IsSuccess}，消息: {result.Message}");
                
                // 确认执行完成
                Assert.NotNull(result);
                Assert.True(result.IsSuccess);
                _output.WriteLine("极简版短线重连测试完成");
            }
            finally
            {
                client.Dispose();
            }
        }
        
        [Fact]
        public void 验证长连接模式设置()
        {
            _output.WriteLine("验证长连接模式设置");
            
            // 创建ModbusTcpClient
            var client = new ModbusTcpClient("127.0.0.1", 5120);
            
            try
            {
                // 默认应该是长连接模式
                Assert.True(client.IsLongConnection, "默认应为长连接模式");
                
                // 设置为短连接模式
                client.IsLongConnection = false;
                Assert.False(client.IsLongConnection, "应成功设置为短连接模式");
                
                // 设置为长连接模式
                client.IsLongConnection = true;
                Assert.True(client.IsLongConnection, "应成功设置为长连接模式");
                
                _output.WriteLine("验证长连接模式设置完成");
            }
            finally
            {
                client.Dispose();
            }
        }
        
        [Fact]
        public void 验证自动重连设置()
        {
            _output.WriteLine("验证自动重连设置");
            
            // 创建ModbusTcpClient
            var client = new ModbusTcpClient("127.0.0.1", 5130);
            
            try
            {
                // 默认应该启用自动重连
                Assert.True(client.EnableAutoReconnect, "默认应启用自动重连");
                
                // 禁用自动重连
                client.EnableAutoReconnect = false;
                Assert.False(client.EnableAutoReconnect, "应成功禁用自动重连");
                
                // 启用自动重连
                client.EnableAutoReconnect = true;
                Assert.True(client.EnableAutoReconnect, "应成功启用自动重连");
                
                _output.WriteLine("验证自动重连设置完成");
            }
            finally
            {
                client.Dispose();
            }
        }
    }
} 
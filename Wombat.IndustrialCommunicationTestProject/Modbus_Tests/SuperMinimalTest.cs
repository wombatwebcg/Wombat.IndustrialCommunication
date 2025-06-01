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
    /// 最小测试类，专注于验证ConnectionDisruptor的基本功能
    /// </summary>
    public class SuperMinimalTest
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;
        
        public SuperMinimalTest(ITestOutputHelper output)
        {
            _output = output;
            _logger = TestLoggerFactory.CreateLogger(output, nameof(SuperMinimalTest));
        }
        
        [Fact]
        public void 验证ConnectionDisruptor创建()
        {
            _output.WriteLine("验证ConnectionDisruptor创建");
            
            // 创建带日志记录器的连接中断模拟器
            var disruptor = new ConnectionDisruptor(_logger);
            
            // 确认创建成功
            Assert.NotNull(disruptor);
            _output.WriteLine("ConnectionDisruptor创建成功");
        }
        
        [Fact]
        public async Task 验证模拟中断不导致循环调用()
        {
            _output.WriteLine("验证模拟中断不导致循环调用");
            
            // 创建ModbusTcpClient但不连接
            var client = new ModbusTcpClient("127.0.0.1", 5050);
            
            try
            {
                // 创建带日志记录器的连接中断模拟器
                var disruptor = new ConnectionDisruptor(_logger);
                
                // 尝试模拟中断连接
                var result = await disruptor.SimulateConnectionDisruption(client);
                
                // 确认操作完成，不导致栈溢出
                _output.WriteLine($"模拟中断结果: {result.IsSuccess}，消息: {result.Message}");
                Assert.NotNull(result);
                
                // 验证短线重连功能
                result = await disruptor.SimulateDisconnectAndReconnect(client, 500);
                _output.WriteLine($"模拟短线重连结果: {result.IsSuccess}，消息: {result.Message}");
                Assert.NotNull(result);
                
                _output.WriteLine("验证通过：操作未导致栈溢出");
            }
            finally
            {
                client.Dispose();
            }
        }
        
        [Fact]
        public void 验证Logger正常工作()
        {
            _output.WriteLine("验证Logger正常工作");
            
            // 测试各种日志级别
            _logger.LogTrace("这是跟踪日志");
            _logger.LogDebug("这是调试日志");
            _logger.LogInformation("这是信息日志");
            _logger.LogWarning("这是警告日志");
            _logger.LogError("这是错误日志");
            _logger.LogCritical("这是严重错误日志");
            
            // 测试带异常的日志
            try
            {
                throw new InvalidOperationException("测试异常");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "捕获到异常");
            }
            
            _output.WriteLine("日志测试完成");
        }
    }
} 
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunicationTestProject.Helper;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTestProject.Modbus_Tests
{
    /// <summary>
    /// Modbus客户端栈溢出问题测试
    /// </summary>
    public class ModbusStackOverflowTest
    {
        private readonly ITestOutputHelper _output;
        
        public ModbusStackOverflowTest(ITestOutputHelper output)
        {
            _output = output;
        }
        
        /// <summary>
        /// 测试ModbusTcpClient连接和断开连接时的潜在栈溢出问题
        /// </summary>
        [Fact]
        public async Task ModbusTcpClient_ConnectDisconnect_ShouldNotStackOverflow()
        {
            _output?.WriteLine("创建服务器和客户端");
            var server = new ModbusTcpServer("127.0.0.1", 502);
            var client = new ModbusTcpClient("127.0.0.1", 502);
            
            try
            {
                // 配置客户端
                _output?.WriteLine("配置客户端");
                client.IsLongConnection = true;
                client.EnableAutoReconnect = true;
                client.MaxReconnectAttempts = 3;
                client.ReconnectDelay = TimeSpan.FromMilliseconds(500); // 缩短延迟，加快测试
                
                if (_output != null)
                {
                    var logger = new XUnitLogger(_output, "ModbusTest");
                    client.Logger = logger;
                }
                
                // 启动服务器
                _output?.WriteLine("启动服务器");
                var result = await server.StartAsync();
                Assert.True(result.IsSuccess, $"启动Modbus服务器失败: {result.Message}");
                
                // 连接客户端
                _output?.WriteLine("连接客户端");
                await client.ConnectAsync();
                Assert.True(client.Connected, "客户端应该处于连接状态");
                
                // 模拟连接中断
                _output?.WriteLine("模拟连接中断");
                var disruptor = new ConnectionDisruptorExtreme(_output != null ? new XUnitLogger(_output, "Disruptor") : null);
                var disruptResult = await disruptor.SimulateSafeDisruption(client);
                _output?.WriteLine($"模拟连接中断结果: {(disruptResult.IsSuccess ? "成功" : "失败")}");
                
                // 等待自动重连
                _output?.WriteLine("等待自动重连");
                await Task.Delay(2000);
                
                // 尝试读写操作
                _output?.WriteLine("尝试读写操作");
                short testValue = 12345;
                var writeResult = await client.WriteAsync("1;6;100", testValue);
                _output?.WriteLine($"写入结果: {(writeResult.IsSuccess ? "成功" : "失败")}");
                
                if (writeResult.IsSuccess)
                {
                    var readResult = await client.ReadInt16Async("1;3;100");
                    _output?.WriteLine($"读取结果: {(readResult.IsSuccess ? "成功" : "失败")}");
                    
                    if (readResult.IsSuccess)
                    {
                        Assert.Equal(testValue, readResult.ResultValue);
                        _output?.WriteLine("读写操作验证成功");
                    }
                }
                
                // 断开连接和关闭服务器
                _output?.WriteLine("断开连接和关闭服务器");
                client.EnableAutoReconnect = false; // 先禁用自动重连
                await client.DisconnectAsync();
                await server.StopAsync();
                
                _output?.WriteLine("测试完成，没有栈溢出");
            }
            catch (Exception ex)
            {
                _output?.WriteLine($"测试过程中发生异常: {ex.Message}");
                _output?.WriteLine($"异常类型: {ex.GetType().FullName}");
                _output?.WriteLine($"异常堆栈: {ex.StackTrace}");
                throw;
            }
            finally
            {
                // 确保资源被释放
                try
                {
                    if (client.Connected)
                    {
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
                        await server.StopAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output?.WriteLine($"停止服务器异常: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 测试ModbusRTUClient连接和断开连接时的潜在栈溢出问题
        /// </summary>
        [Fact(Skip = "需要物理COM端口，仅作为代码示例")]
        public async Task ModbusRTUClient_ConnectDisconnect_ShouldNotStackOverflow()
        {
            _output?.WriteLine("创建客户端");
            var client = new ModbusRTUClient("COM1", 9600, 8, System.IO.Ports.StopBits.One, System.IO.Ports.Parity.None);
            
            try
            {
                // 配置客户端
                _output?.WriteLine("配置客户端");
                client.IsLongConnection = true;
                client.EnableAutoReconnect = true;
                client.MaxReconnectAttempts = 3;
                client.ReconnectDelay = TimeSpan.FromMilliseconds(500); // 缩短延迟，加快测试
                
                if (_output != null)
                {
                    var logger = new XUnitLogger(_output, "ModbusTest");
                    client.Logger = logger;
                }
                
                // 连接客户端
                _output?.WriteLine("连接客户端");
                await client.ConnectAsync();
                
                // 检查连接状态，RTU可能会始终报告为已连接，因为它只是打开了COM端口
                _output?.WriteLine($"连接状态: {(client.Connected ? "已连接" : "未连接")}");
                
                // 断开连接
                _output?.WriteLine("断开连接");
                client.EnableAutoReconnect = false; // 先禁用自动重连
                await client.DisconnectAsync();
                
                _output?.WriteLine("测试完成，没有栈溢出");
            }
            catch (Exception ex)
            {
                _output?.WriteLine($"测试过程中发生异常: {ex.Message}");
                _output?.WriteLine($"异常类型: {ex.GetType().FullName}");
                _output?.WriteLine($"异常堆栈: {ex.StackTrace}");
                throw;
            }
            finally
            {
                // 确保资源被释放
                try
                {
                    if (client.Connected)
                    {
                        client.EnableAutoReconnect = false;
                        await client.DisconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    _output?.WriteLine($"断开客户端异常: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// XUnit日志记录器，用于将测试输出转发到ILogger接口
    /// </summary>
    public class XUnitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;
        
        public XUnitLogger(ITestOutputHelper output, string categoryName = "Test")
        {
            _output = output;
            _categoryName = categoryName;
        }
        
        public IDisposable BeginScope<TState>(TState state) => null;
        
        public bool IsEnabled(LogLevel logLevel) => true;
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}][{_categoryName}] {message}");
            
            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception.Message}");
                _output.WriteLine($"StackTrace: {exception.StackTrace}");
            }
        }
    }
} 
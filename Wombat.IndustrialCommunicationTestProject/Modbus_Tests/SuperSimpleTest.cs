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
    /// 超级简单的测试类，只包含最基本的测试
    /// </summary>
    public class SuperSimpleTest
    {
        private readonly ITestOutputHelper _output;
        
        public SuperSimpleTest(ITestOutputHelper output)
        {
            _output = output;
        }
        
        [Fact]
        public void 创建ConnectionDisruptor不使用日志()
        {
            _output.WriteLine("创建ConnectionDisruptor不使用日志");
            
            // 创建不带日志记录器的连接中断模拟器
            var disruptor = new ConnectionDisruptor();
            
            // 确认创建成功
            Assert.NotNull(disruptor);
            _output.WriteLine("ConnectionDisruptor创建成功");
        }
        
        [Fact]
        public void 验证ModbusTcpClient创建不导致栈溢出()
        {
            _output.WriteLine("验证ModbusTcpClient创建不导致栈溢出");
            
            // 创建ModbusTcpClient但不连接
            var client = new ModbusTcpClient("127.0.0.1", 5050);
            
            try
            {
                // 检查属性
                _output.WriteLine($"客户端端口：{client.IPEndPoint.Port}");
                _output.WriteLine($"客户端连接状态：{client.Connected}");
                _output.WriteLine($"客户端连接模式：{(client.IsLongConnection ? "长连接" : "短连接")}");
                
                // 切换连接模式
                client.IsLongConnection = false;
                _output.WriteLine($"切换后连接模式：{(client.IsLongConnection ? "长连接" : "短连接")}");
                
                _output.WriteLine("验证通过：操作未导致栈溢出");
            }
            finally
            {
                client.Dispose();
            }
        }
        
        [Fact]
        public async Task 最简单的模拟中断测试()
        {
            _output.WriteLine("最简单的模拟中断测试");
            
            // 创建ModbusTcpClient但不连接，使用未占用的端口
            var client = new ModbusTcpClient("127.0.0.1", 5060);
            
            try
            {
                // 创建连接中断模拟器，不使用日志
                var disruptor = new ConnectionDisruptor();
                
                // 执行最简单的模拟中断测试
                _output.WriteLine("执行模拟中断操作");
                var result = await disruptor.SimulateConnectionDisruption(client);
                
                // 输出结果
                _output.WriteLine($"模拟中断结果: {result.IsSuccess}，消息: {result.Message}");
                
                // 确认执行完成
                Assert.NotNull(result);
                _output.WriteLine("最简单的模拟中断测试完成");
            }
            finally
            {
                client.Dispose();
            }
        }
        
        [Fact]
        public async Task 最简单的短线重连测试()
        {
            _output.WriteLine("最简单的短线重连测试");
            
            // 创建ModbusTcpClient但不连接，使用未占用的端口
            var client = new ModbusTcpClient("127.0.0.1", 5070);
            
            try
            {
                // 设置为长连接模式，但不启用自动重连
                client.IsLongConnection = true;
                client.EnableAutoReconnect = false;
                
                // 创建连接中断模拟器，不使用日志
                var disruptor = new ConnectionDisruptor();
                
                // 执行最简单的短线重连测试
                _output.WriteLine("执行短线重连操作");
                var result = await disruptor.SimulateDisconnectAndReconnect(client, 500);
                
                // 输出结果
                _output.WriteLine($"短线重连结果: {result.IsSuccess}，消息: {result.Message}");
                
                // 确认执行完成
                Assert.NotNull(result);
                _output.WriteLine("最简单的短线重连测试完成");
            }
            finally
            {
                client.Dispose();
            }
        }
    }
} 
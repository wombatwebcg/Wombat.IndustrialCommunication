using System;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunicationTestProject.Helper;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTestProject.Modbus_Tests
{
    /// <summary>
    /// 超极简测试类，只测试基本属性设置，不进行实际连接操作
    /// </summary>
    public class UltraSimpleTest
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;
        
        public UltraSimpleTest(ITestOutputHelper output)
        {
            _output = output;
            _logger = TestLoggerFactory.CreateLogger(output, nameof(UltraSimpleTest));
        }
        
        [Fact]
        public void 测试Modbus客户端属性设置()
        {
            _output.WriteLine("测试Modbus客户端属性设置");
            
            // 创建ModbusTcpClient但不连接
            var client = new ModbusTcpClient("127.0.0.1", 5200);
            
            try
            {
                // 验证初始属性值
                _output.WriteLine($"初始连接状态: {client.Connected}");
                _output.WriteLine($"初始连接模式: {(client.IsLongConnection ? "长连接" : "短连接")}");
                _output.WriteLine($"初始自动重连设置: {(client.EnableAutoReconnect ? "启用" : "禁用")}");
                _output.WriteLine($"初始最大重连次数: {client.MaxReconnectAttempts}");
                _output.WriteLine($"初始重连延迟: {client.ReconnectDelay.TotalSeconds}秒");
                
                // 修改属性值
                client.IsLongConnection = false;
                client.EnableAutoReconnect = false;
                client.MaxReconnectAttempts = 5;
                client.ReconnectDelay = TimeSpan.FromSeconds(2);
                
                // 验证修改后的属性值
                _output.WriteLine($"修改后连接模式: {(client.IsLongConnection ? "长连接" : "短连接")}");
                _output.WriteLine($"修改后自动重连设置: {(client.EnableAutoReconnect ? "启用" : "禁用")}");
                _output.WriteLine($"修改后最大重连次数: {client.MaxReconnectAttempts}");
                _output.WriteLine($"修改后重连延迟: {client.ReconnectDelay.TotalSeconds}秒");
                
                // 进行断言验证
                Assert.False(client.Connected, "客户端应处于断开状态");
                Assert.False(client.IsLongConnection, "客户端应为短连接模式");
                Assert.False(client.EnableAutoReconnect, "客户端应禁用自动重连");
                Assert.Equal(5, client.MaxReconnectAttempts);
                Assert.Equal(2, client.ReconnectDelay.TotalSeconds);
                
                _output.WriteLine("Modbus客户端属性设置测试完成");
            }
            finally
            {
                client.Dispose();
            }
        }
        
        [Fact]
        public void 测试S7客户端属性设置()
        {
            _output.WriteLine("测试S7客户端属性设置");
            
            // 创建SiemensClient但不连接
            var client = new Wombat.IndustrialCommunication.PLC.SiemensClient(
                "127.0.0.1", 
                102, 
                Wombat.IndustrialCommunication.PLC.SiemensVersion.S7_1200);
            
            try
            {
                // 验证初始属性值
                _output.WriteLine($"初始连接状态: {client.Connected}");
                _output.WriteLine($"初始连接模式: {(client.IsLongConnection ? "长连接" : "短连接")}");
                _output.WriteLine($"初始自动重连设置: {(client.EnableAutoReconnect ? "启用" : "禁用")}");
                _output.WriteLine($"初始最大重连次数: {client.MaxReconnectAttempts}");
                _output.WriteLine($"初始重连延迟: {client.ReconnectDelay.TotalSeconds}秒");
                
                // 修改属性值
                client.IsLongConnection = false;
                client.EnableAutoReconnect = false;
                client.MaxReconnectAttempts = 5;
                client.ReconnectDelay = TimeSpan.FromSeconds(2);
                
                // 验证修改后的属性值
                _output.WriteLine($"修改后连接模式: {(client.IsLongConnection ? "长连接" : "短连接")}");
                _output.WriteLine($"修改后自动重连设置: {(client.EnableAutoReconnect ? "启用" : "禁用")}");
                _output.WriteLine($"修改后最大重连次数: {client.MaxReconnectAttempts}");
                _output.WriteLine($"修改后重连延迟: {client.ReconnectDelay.TotalSeconds}秒");
                
                // 进行断言验证
                Assert.False(client.Connected, "客户端应处于断开状态");
                Assert.False(client.IsLongConnection, "客户端应为短连接模式");
                Assert.False(client.EnableAutoReconnect, "客户端应禁用自动重连");
                Assert.Equal(5, client.MaxReconnectAttempts);
                Assert.Equal(2, client.ReconnectDelay.TotalSeconds);
                
                _output.WriteLine("S7客户端属性设置测试完成");
            }
            finally
            {
                client.Dispose();
            }
        }
    }
} 
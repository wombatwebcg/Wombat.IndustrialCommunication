using System;
using System.Net;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunicationTestProject.Helper;
using Xunit;

namespace Wombat.IndustrialCommunicationTestProject.Modbus_Tests
{
    public class MinimalTest
    {
        [Fact]
        public void CanCreateModbusClient()
        {
            // 简单创建一个Modbus客户端对象
            var client = new ModbusTcpClient("127.0.0.1", 502);
            
            // 验证客户端属性
            Assert.Equal("127.0.0.1", client.IPEndPoint.Address.ToString());
            Assert.Equal(502, client.IPEndPoint.Port);
            Assert.False(client.Connected);
        }
        
        [Fact]
        public void CanSetLongConnectionMode()
        {
            var client = new ModbusTcpClient("127.0.0.1", 502);
            
            // 默认应该是长连接模式
            Assert.True(client.IsLongConnection);
            
            // 切换到短连接模式
            client.IsLongConnection = false;
            Assert.False(client.IsLongConnection);
            
            // 切换回长连接模式
            client.IsLongConnection = true;
            Assert.True(client.IsLongConnection);
        }
        
        [Fact]
        public async Task 测试连接中断模拟器_避免循环调用()
        {
            // 使用非标准端口避免与其他测试冲突
            var client = new ModbusTcpClient("127.0.0.1", 503);
            
            try
            {
                // 我们不需要真正连接到服务器，只需要验证中断模拟器工作正常
                client.IsLongConnection = true;
                
                // 创建连接中断模拟器
                var disruptor = new ConnectionDisruptor();
                
                // 调用模拟中断方法，不应该导致栈溢出或循环调用
                var result = await disruptor.SimulateConnectionDisruption(client);
                
                // 由于没有实际连接，结果可能是成功或失败，但不应抛出异常
                // 只要不发生栈溢出，就认为测试通过
                Assert.NotNull(result);
            }
            finally
            {
                client.Dispose();
            }
        }
    }
} 
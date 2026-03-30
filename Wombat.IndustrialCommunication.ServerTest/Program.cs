using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.ServerTest
{
    internal class Program
    {

        static void Main(string[] args)
        {
              
          // 创建服务器实例
            var  _server = new S7TcpServer("127.0.0.1", 102);
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
         var  _logger = loggerFactory.CreateLogger<Program>();
            // 配置服务器参数
            _server.SetSiemensVersion(SiemensVersion.S7_200Smart);
            _server.SetRackSlot(0, 1);
            _server.UseLogger(_logger);

            // 创建测试数据块
            _server.CreateDataBlock(1, 65535);

            // 启动服务器监听
            var listenResult = _server.Listen();





            ModbusTcpServer modbusTcpServer = new ModbusTcpServer("127.0.0.1", 502);
            modbusTcpServer.UseLogger(_logger);
            _=modbusTcpServer.StartAsync();


            Console.ReadKey();
        }
    }
}

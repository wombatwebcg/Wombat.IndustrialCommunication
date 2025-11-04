using System;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.PLC.FINS;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 简单的超时测试程序
    /// </summary>
    public class TimeoutTest
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("开始FINS客户端超时测试...");
            
            const string serverIp = "127.0.0.1";
            const int serverPort = 9600;
            var timeout = TimeSpan.FromSeconds(3); // 设置3秒超时
            
            var finsClient = new FinsClient(serverIp, serverPort, timeout);
            
            try
            {
                Console.WriteLine($"尝试连接到 {serverIp}:{serverPort}，超时时间: {timeout.TotalSeconds}秒");
                
                var startTime = DateTime.Now;
                var connectResult = await finsClient.ConnectAsync();
                var elapsed = DateTime.Now - startTime;
                
                Console.WriteLine($"连接尝试完成，耗时: {elapsed.TotalSeconds:F2}秒");
                
                if (connectResult.IsSuccess)
                {
                    Console.WriteLine("连接成功！");
                    
                    // 尝试读取数据
                    Console.WriteLine("尝试读取数据...");
                    startTime = DateTime.Now;
                    
                    var readResult = await finsClient.ReadAsync("D100", 1);
                    elapsed = DateTime.Now - startTime;
                    
                    Console.WriteLine($"读取操作完成，耗时: {elapsed.TotalSeconds:F2}秒");
                    
                    if (readResult.IsSuccess)
                    {
                        Console.WriteLine($"读取成功: {readResult.ResultValue}");
                    }
                    else
                    {
                        Console.WriteLine($"读取失败: {readResult.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"连接失败: {connectResult.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生异常: {ex.Message}");
                Console.WriteLine($"异常类型: {ex.GetType().Name}");
            }
            finally
            {
                try
                {
                    await finsClient.DisconnectAsync();
                    Console.WriteLine("客户端已断开连接");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"断开连接时发生异常: {ex.Message}");
                }
            }
            
            Console.WriteLine("测试完成，按任意键退出...");
            Console.ReadKey();
        }
    }
}
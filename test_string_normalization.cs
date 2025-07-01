using System;
using Wombat.IndustrialCommunication.PLC;

namespace StringNormalizationTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== 字符串大小写处理一致性测试 ===");
            
            // 测试S7BatchHelper
            TestS7BatchHelper();
            
            // 测试S7CommonMethods
            TestS7CommonMethods();
            
            Console.WriteLine("\n测试完成！");
        }
        
        static void TestS7BatchHelper()
        {
            Console.WriteLine("\n--- S7BatchHelper测试 ---");
            
            string[] testAddresses = {
                "DB1.DBW10",
                "db1.dbw10", 
                "Db1.Dbw10",
                "V700",
                "v700",
                "Q1.3",
                "q1.3",
                "I1.0",
                "i1.0",
                "M5.7",
                "m5.7"
            };
            
            foreach (var address in testAddresses)
            {
                try
                {
                    var result = S7BatchHelper.ParseSingleS7Address(address);
                    Console.WriteLine($"✓ {address} -> 解析成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ {address} -> 解析失败: {ex.Message}");
                }
            }
        }
        
        static void TestS7CommonMethods()
        {
            Console.WriteLine("\n--- S7CommonMethods测试 ---");
            
            string[] testAddresses = {
                "DB1.DBW10",
                "db1.dbw10",
                "V700", 
                "v700",
                "Q1.3",
                "q1.3"
            };
            
            foreach (var address in testAddresses)
            {
                try
                {
                    var result = S7CommonMethods.ConvertArg(address);
                    Console.WriteLine($"✓ {address} -> 转换成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ {address} -> 转换失败: {ex.Message}");
                }
            }
        }
    }
} 
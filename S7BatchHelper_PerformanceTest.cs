using System;
using System.Collections.Generic;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// S7BatchHelper性能测试程序
    /// </summary>
    public class S7BatchHelperPerformanceTest
    {
        public static void Main()
        {
            Console.WriteLine("开始S7BatchHelper性能分析...\n");

            // 测试1：连续地址
            Console.WriteLine("测试1：连续地址 (6个地址)");
            var continuousAddresses = S7BatchHelperPerformanceAnalyzer.CreateTestAddresses(6, "continuous");
            var continuousResult = S7BatchHelperPerformanceAnalyzer.AnalyzeBatchReadPerformance(continuousAddresses);
            S7BatchHelperPerformanceAnalyzer.PrintPerformanceReport(continuousResult);
            Console.WriteLine();

            // 测试2：分散地址
            Console.WriteLine("测试2：分散地址 (6个地址)");
            var scatteredAddresses = S7BatchHelperPerformanceAnalyzer.CreateTestAddresses(6, "scattered");
            var scatteredResult = S7BatchHelperPerformanceAnalyzer.AnalyzeBatchReadPerformance(scatteredAddresses);
            S7BatchHelperPerformanceAnalyzer.PrintPerformanceReport(scatteredResult);
            Console.WriteLine();

            // 测试3：混合数据类型
            Console.WriteLine("测试3：混合数据类型 (8个地址)");
            var mixedAddresses = S7BatchHelperPerformanceAnalyzer.CreateTestAddresses(8, "mixed");
            var mixedResult = S7BatchHelperPerformanceAnalyzer.AnalyzeBatchReadPerformance(mixedAddresses);
            S7BatchHelperPerformanceAnalyzer.PrintPerformanceReport(mixedResult);
            Console.WriteLine();

            // 测试4：大量地址
            Console.WriteLine("测试4：大量连续地址 (50个地址)");
            var largeAddresses = S7BatchHelperPerformanceAnalyzer.CreateTestAddresses(50, "continuous");
            var largeResult = S7BatchHelperPerformanceAnalyzer.AnalyzeBatchReadPerformance(largeAddresses);
            S7BatchHelperPerformanceAnalyzer.PrintPerformanceReport(largeResult);
            Console.WriteLine();

            // 分析性能瓶颈
            Console.WriteLine("=== 性能瓶颈分析 ===");
            Console.WriteLine("根据测试结果，主要性能瓶颈可能来自：");
            Console.WriteLine("1. 地址解析：字符串处理和正则表达式匹配");
            Console.WriteLine("2. 地址优化：排序、分组和效率比计算");
            Console.WriteLine("3. 数据提取：字典查找和字节数组操作");
            Console.WriteLine();
            Console.WriteLine("建议优化方向：");
            Console.WriteLine("1. 缓存地址解析结果");
            Console.WriteLine("2. 优化地址优化算法");
            Console.WriteLine("3. 减少不必要的内存分配");
            Console.WriteLine("4. 使用更高效的数据结构");
        }
    }
} 
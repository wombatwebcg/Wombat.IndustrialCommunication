using System;
using System.Collections.Generic;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;

namespace PerformanceTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("S7BatchHelper 性能分析测试");
            Console.WriteLine("==========================\n");

            // 测试1：连续地址
            Console.WriteLine("测试1：连续地址 (6个地址)");
            var continuousAddresses = CreateTestAddresses(6, "continuous");
            var continuousResult = AnalyzeBatchReadPerformance(continuousAddresses);
            PrintPerformanceReport(continuousResult);
            Console.WriteLine();

            // 测试2：分散地址
            Console.WriteLine("测试2：分散地址 (6个地址)");
            var scatteredAddresses = CreateTestAddresses(6, "scattered");
            var scatteredResult = AnalyzeBatchReadPerformance(scatteredAddresses);
            PrintPerformanceReport(scatteredResult);
            Console.WriteLine();

            // 测试3：混合数据类型
            Console.WriteLine("测试3：混合数据类型 (8个地址)");
            var mixedAddresses = CreateTestAddresses(8, "mixed");
            var mixedResult = AnalyzeBatchReadPerformance(mixedAddresses);
            PrintPerformanceReport(mixedResult);
            Console.WriteLine();

            // 测试4：大量地址
            Console.WriteLine("测试4：大量连续地址 (50个地址)");
            var largeAddresses = CreateTestAddresses(50, "continuous");
            var largeResult = AnalyzeBatchReadPerformance(largeAddresses);
            PrintPerformanceReport(largeResult);
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

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }

        public class PerformanceResult
        {
            public long ParseTime { get; set; }
            public long OptimizeTime { get; set; }
            public long ExtractTime { get; set; }
            public long TotalTime { get; set; }
            public int AddressCount { get; set; }
            public int BlockCount { get; set; }
            public double AverageTimePerAddress { get; set; }
        }

        public static PerformanceResult AnalyzeBatchReadPerformance(Dictionary<string, (DataTypeEnums, object)> addresses, int iterations = 100)
        {
            var result = new PerformanceResult
            {
                AddressCount = addresses.Count
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var parseStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var optimizeStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var extractStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 预热
            for (int i = 0; i < 10; i++)
            {
                var addressInfos = S7BatchHelper.ParseS7Addresses(addresses);
                var optimizedBlocks = S7BatchHelper.OptimizeS7AddressBlocks(addressInfos);
                var mockBlockData = new Dictionary<string, byte[]>();
                foreach (var block in optimizedBlocks)
                {
                    string blockKey = $"DB{block.DbNumber}_{block.StartByte}_{block.TotalLength}";
                    mockBlockData[blockKey] = new byte[block.TotalLength];
                }
                var extractedData = S7BatchHelper.ExtractDataFromS7Blocks(mockBlockData, optimizedBlocks, addressInfos);
            }

            // 正式测试
            for (int i = 0; i < iterations; i++)
            {
                // 测试地址解析性能
                parseStopwatch.Restart();
                var addressInfos = S7BatchHelper.ParseS7Addresses(addresses);
                parseStopwatch.Stop();
                result.ParseTime += parseStopwatch.ElapsedTicks;

                // 测试地址优化性能
                optimizeStopwatch.Restart();
                var optimizedBlocks = S7BatchHelper.OptimizeS7AddressBlocks(addressInfos);
                optimizeStopwatch.Stop();
                result.OptimizeTime += optimizeStopwatch.ElapsedTicks;

                // 测试数据提取性能
                extractStopwatch.Restart();
                var mockBlockData = new Dictionary<string, byte[]>();
                foreach (var block in optimizedBlocks)
                {
                    string blockKey = $"DB{block.DbNumber}_{block.StartByte}_{block.TotalLength}";
                    mockBlockData[blockKey] = new byte[block.TotalLength];
                }
                var extractedData = S7BatchHelper.ExtractDataFromS7Blocks(mockBlockData, optimizedBlocks, addressInfos);
                extractStopwatch.Stop();
                result.ExtractTime += extractStopwatch.ElapsedTicks;

                if (i == 0)
                {
                    result.BlockCount = optimizedBlocks.Count;
                }
            }

            stopwatch.Stop();
            result.TotalTime = stopwatch.ElapsedTicks;

            // 计算平均值
            result.ParseTime /= iterations;
            result.OptimizeTime /= iterations;
            result.ExtractTime /= iterations;
            result.TotalTime /= iterations;
            result.AverageTimePerAddress = (double)result.TotalTime / result.AddressCount;

            return result;
        }

        public static void PrintPerformanceReport(PerformanceResult result)
        {
            Console.WriteLine($"地址数量: {result.AddressCount}");
            Console.WriteLine($"优化后块数量: {result.BlockCount}");
            Console.WriteLine($"测试迭代次数: 100");
            Console.WriteLine();
            Console.WriteLine("各步骤耗时 (微秒):");
            Console.WriteLine($"  地址解析: {result.ParseTime / 10:F2} μs");
            Console.WriteLine($"  地址优化: {result.OptimizeTime / 10:F2} μs");
            Console.WriteLine($"  数据提取: {result.ExtractTime / 10:F2} μs");
            Console.WriteLine($"  总耗时: {result.TotalTime / 10:F2} μs");
            Console.WriteLine($"  平均每地址耗时: {result.AverageTimePerAddress / 10:F2} μs");
            Console.WriteLine();
            Console.WriteLine("性能占比:");
            Console.WriteLine($"  地址解析: {(double)result.ParseTime / result.TotalTime * 100:F1}%");
            Console.WriteLine($"  地址优化: {(double)result.OptimizeTime / result.TotalTime * 100:F1}%");
            Console.WriteLine($"  数据提取: {(double)result.ExtractTime / result.TotalTime * 100:F1}%");
        }

        public static Dictionary<string, (DataTypeEnums, object)> CreateTestAddresses(int count, string type = "continuous")
        {
            var addresses = new Dictionary<string, (DataTypeEnums, object)>();

            switch (type.ToLower())
            {
                case "continuous":
                    // 连续地址
                    for (int i = 0; i < count; i++)
                    {
                        addresses[$"DB200.DBW{i * 2}"] = (DataTypeEnums.Int16, null);
                    }
                    break;

                case "scattered":
                    // 分散地址
                    for (int i = 0; i < count; i++)
                    {
                        addresses[$"DB200.DBW{i * 10}"] = (DataTypeEnums.Int16, null);
                    }
                    break;

                case "mixed":
                    // 混合数据类型
                    for (int i = 0; i < count; i++)
                    {
                        switch (i % 4)
                        {
                            case 0:
                                addresses[$"DB200.DBX{i}.0"] = (DataTypeEnums.Bool, null);
                                break;
                            case 1:
                                addresses[$"DB200.DBW{i * 2}"] = (DataTypeEnums.Int16, null);
                                break;
                            case 2:
                                addresses[$"DB200.DBD{i * 4}"] = (DataTypeEnums.Int32, null);
                                break;
                            case 3:
                                addresses[$"DB200.DBD{i * 4}"] = (DataTypeEnums.Float, null);
                                break;
                        }
                    }
                    break;

                default:
                    throw new ArgumentException($"不支持的地址类型: {type}");
            }

            return addresses;
        }
    }
} 
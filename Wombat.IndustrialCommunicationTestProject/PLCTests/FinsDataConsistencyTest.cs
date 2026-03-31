using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.PLC;
using Wombat.IndustrialCommunication.Models;
using Wombat.Extensions.DataTypeExtensions;
using Xunit;
using Xunit.Abstractions;
using Wombat.IndustrialCommunicationTestProject.Helper;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    /// <summary>
    /// FINS客户端数据一致性测试 - 连接到真实FINS服务器
    /// </summary>
    public class FinsDataConsistencyTest : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;
        
        // 真实FINS服务器配置
        private const string REAL_SERVER_IP = "127.0.0.1";
        private const int REAL_SERVER_PORT = 9600;

        public FinsDataConsistencyTest(ITestOutputHelper output)
        {
            _output = output;
            _logger = TestLoggerFactory.CreateLogger<FinsDataConsistencyTest>(output);
        }

        /// <summary>
        /// 测试基本数据类型的读写一致性
        /// </summary>
        [Fact]
        [Trait("TestCategory", "DataConsistency")]
        public async Task Test_BasicDataTypes_ReadWriteConsistency()
        {
            // 重定向Console输出到测试输出
            var originalOut = Console.Out;
            var stringWriter = new System.IO.StringWriter();
            Console.SetOut(stringWriter);

            try
            {
                // 创建FINS客户端，设置极短的超时时间
                var finsClient = new FinsClient(REAL_SERVER_IP, REAL_SERVER_PORT, TimeSpan.FromMilliseconds(50));
                finsClient.Logger = _logger;

                _output.WriteLine($"开始测试基本数据类型的读写一致性 - 连接到 {REAL_SERVER_IP}:{REAL_SERVER_PORT}");
                _output.WriteLine("注意：此测试需要真实的FINS服务器运行在指定地址");

                // 测试数据集
                var testCases = new[]
                {
                    new { Address = "D1000", WriteValue = (object)(ushort)12345, DataType = DataTypeEnums.UInt16, Description = "UInt16类型" },
                    new { Address = "D1002", WriteValue = (object)(uint)987654321, DataType = DataTypeEnums.UInt32, Description = "UInt32类型" },
                    new { Address = "D1004", WriteValue = (object)(short)-1234, DataType = DataTypeEnums.Int16, Description = "Int16类型" },
                    new { Address = "D1006", WriteValue = (object)-987654, DataType = DataTypeEnums.Int32, Description = "Int32类型" },
                    new { Address = "D1008", WriteValue = (object)123.45f, DataType = DataTypeEnums.Float, Description = "Float类型" },
                    new { Address = "D1010", WriteValue = (object)123.456789, DataType = DataTypeEnums.Double, Description = "Double类型" }
                };

                // 连接到服务器 - 添加超时控制
                using (var connectionTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
                {
                    var connectTask = finsClient.ConnectAsync();
                    var timeoutTask = Task.Delay(Timeout.Infinite, connectionTimeout.Token);
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        _output.WriteLine("连接超时，跳过数据一致性测试");
                        Assert.True(false, $"连接到真实FINS服务器 {REAL_SERVER_IP}:{REAL_SERVER_PORT} 超时 (100ms)");
                        return;
                    }
                    else
                    {
                        var connectResult = await connectTask;
                         _output.WriteLine($"连接结果: {connectResult.IsSuccess}, 消息: {connectResult.Message}");

                         if (!connectResult.IsSuccess)
                         {
                             _output.WriteLine("连接失败，跳过数据一致性测试");
                             Assert.True(false, $"无法连接到真实FINS服务器 {REAL_SERVER_IP}:{REAL_SERVER_PORT} - {connectResult.Message}");
                             return;
                         }
                     }
                }

            int successCount = 0;
            int totalCount = testCases.Length;

            foreach (var testCase in testCases)
            {
                _output.WriteLine($"\n测试 {testCase.Description} - 地址: {testCase.Address}");
                _output.WriteLine($"写入值: {testCase.WriteValue}");

                try
                {
                    // 写入数据 - 添加超时控制
                    using (var writeTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
                    {
                        var writeTask = finsClient.WriteAsync(testCase.Address, testCase.WriteValue, testCase.DataType);
                        var writeTimeoutTask = Task.Delay(Timeout.Infinite, writeTimeout.Token);
                        var writeCompletedTask = await Task.WhenAny(writeTask, writeTimeoutTask);
                        
                        OperationResult writeResult;
                        if (writeCompletedTask == writeTimeoutTask)
                        {
                            writeResult = OperationResult.CreateFailedResult("写入操作超时(50ms)");
                        }
                        else
                        {
                            writeResult = await writeTask;
                        }
                        
                        _output.WriteLine($"写入结果: {writeResult.IsSuccess}, 消息: {writeResult.Message}");

                        if (writeResult.IsSuccess)
                        {
                            // 读取数据 - 添加超时控制
                            using (var readTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
                            {
                                var readTask = finsClient.ReadAsync(testCase.Address, 1, testCase.DataType);
                                var readTimeoutTask = Task.Delay(Timeout.Infinite, readTimeout.Token);
                                var readCompletedTask = await Task.WhenAny(readTask, readTimeoutTask);
                                
                                OperationResult<byte[]> readResult;
                                if (readCompletedTask == readTimeoutTask)
                                {
                                    readResult = OperationResult.CreateFailedResult<byte[]>("读取操作超时(50ms)");
                                }
                                else
                                {
                                    readResult = await readTask;
                                }
                                
                                _output.WriteLine($"读取结果: {readResult.IsSuccess}, 消息: {readResult.Message}");

                                if (readResult.IsSuccess && readResult.ResultValue != null)
                                {
                                    var readValue = readResult.ResultValue;
                                    _output.WriteLine($"读取值: {readValue}");

                                    // 验证数据一致性
                                    bool isConsistent = CompareValues(testCase.WriteValue, readValue, testCase.DataType);
                                    _output.WriteLine($"数据一致性: {(isConsistent ? "通过" : "失败")}");

                                    if (isConsistent)
                                    {
                                        successCount++;
                                    }
                                    else
                                    {
                                        _output.WriteLine($"数据不一致！写入: {testCase.WriteValue}, 读取: {readValue}");
                                    }
                                }
                                else
                                {
                                    _output.WriteLine("读取失败或返回空值");
                                }
                            }
                        }
                        else
                        {
                            _output.WriteLine("写入失败");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"测试异常: {ex.Message}");
                }
            }

                await finsClient.DisconnectAsync();

                _output.WriteLine($"\n数据一致性测试完成");
                _output.WriteLine($"成功: {successCount}/{totalCount}");
                _output.WriteLine($"成功率: {(double)successCount / totalCount * 100:F1}%");

                // 对于真实服务器，期望更高的成功率
                Assert.True(successCount >= totalCount * 0.8, $"数据一致性测试成功率过低: {successCount}/{totalCount} (期望至少80%)");
            }
            finally
            {
                // 恢复Console输出
                Console.SetOut(originalOut);
                
                // 输出所有Console调试信息
                var consoleOutput = stringWriter.ToString();
                if (!string.IsNullOrEmpty(consoleOutput))
                {
                    _output.WriteLine("\n=== Console调试输出 ===");
                    _output.WriteLine(consoleOutput);
                    _output.WriteLine("=== Console调试输出结束 ===");
                }
                
                stringWriter.Dispose();
            }
        }

        /// <summary>
        /// 测试字符串数据的读写一致性
        /// </summary>
        [Fact]
        [Trait("TestCategory", "DataConsistency")]
        public async Task Test_StringData_ReadWriteConsistency()
        {
            // 连接到真实FINS服务器
            var finsClient = new FinsClient(REAL_SERVER_IP, REAL_SERVER_PORT, TimeSpan.FromSeconds(5));
            finsClient.Logger = _logger;

            _output.WriteLine($"开始测试字符串数据的读写一致性 - 连接到 {REAL_SERVER_IP}:{REAL_SERVER_PORT}");

                var testStrings = new[]
                {
                    "Hello",
                    "World",
                    "FINS Test",
                    "数据一致性测试",
                    "1234567890",
                    "!@#$%^&*()"
                };

            var connectResult = await finsClient.ConnectAsync();
            _output.WriteLine($"连接结果: {connectResult.IsSuccess}, 消息: {connectResult.Message}");

            if (!connectResult.IsSuccess)
            {
                _output.WriteLine("连接失败，跳过字符串一致性测试");
                Assert.True(false, $"无法连接到真实FINS服务器 {REAL_SERVER_IP}:{REAL_SERVER_PORT} - {connectResult.Message}");
                return;
            }

            int successCount = 0;
            int totalCount = testStrings.Length;

            for (int i = 0; i < testStrings.Length; i++)
            {
                var testString = testStrings[i];
                var address = $"D{2000 + i * 10}";

                _output.WriteLine($"\n测试字符串 {i + 1} - 地址: {address}");
                _output.WriteLine($"写入字符串: '{testString}'");

                try
                {
                    // 写入字符串
                    var writeResult = await finsClient.WriteAsync(address, testString, DataTypeEnums.String);
                    _output.WriteLine($"写入结果: {writeResult.IsSuccess}, 消息: {writeResult.Message}");

                    if (writeResult.IsSuccess)
                    {
                        // 读取字符串
                        var readResult = await finsClient.ReadAsync(address, (ushort)testString.Length, DataTypeEnums.String);
                        _output.WriteLine($"读取结果: {readResult.IsSuccess}, 消息: {readResult.Message}");

                        if (readResult.IsSuccess && readResult.ResultValue != null)
                        {
                            var readString = readResult.ResultValue.ToString();
                            _output.WriteLine($"读取字符串: '{readString}'");

                            // 验证字符串一致性
                            bool isConsistent = string.Equals(testString, readString, StringComparison.Ordinal);
                            _output.WriteLine($"字符串一致性: {(isConsistent ? "通过" : "失败")}");

                            if (isConsistent)
                            {
                                successCount++;
                            }
                            else
                            {
                                _output.WriteLine($"字符串不一致！写入: '{testString}', 读取: '{readString}'");
                            }
                        }
                        else
                        {
                            _output.WriteLine("读取失败或返回空值");
                        }
                    }
                    else
                    {
                        _output.WriteLine("写入失败");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"测试异常: {ex.Message}");
                }
            }

            await finsClient.DisconnectAsync();

            _output.WriteLine($"\n字符串一致性测试完成");
            _output.WriteLine($"成功: {successCount}/{totalCount}");
            _output.WriteLine($"成功率: {(double)successCount / totalCount * 100:F1}%");

            // 对于真实服务器，期望更高的成功率
            Assert.True(successCount >= totalCount * 0.8, $"字符串一致性测试成功率过低: {successCount}/{totalCount} (期望至少80%)");
        }

        /// <summary>
        /// 比较两个值是否相等
        /// </summary>
        private bool CompareValues(object writeValue, object readValue, DataTypeEnums dataType)
        {
            try
            {
                switch (dataType)
                {
                    case DataTypeEnums.UInt16:
                        return Convert.ToUInt16(writeValue) == Convert.ToUInt16(readValue);
                    case DataTypeEnums.UInt32:
                        return Convert.ToUInt32(writeValue) == Convert.ToUInt32(readValue);
                    case DataTypeEnums.Int16:
                        return Convert.ToInt16(writeValue) == Convert.ToInt16(readValue);
                    case DataTypeEnums.Int32:
                        return Convert.ToInt32(writeValue) == Convert.ToInt32(readValue);
                    case DataTypeEnums.Float:
                        return Math.Abs(Convert.ToSingle(writeValue) - Convert.ToSingle(readValue)) < 0.001f;
                    case DataTypeEnums.Double:
                        return Math.Abs(Convert.ToDouble(writeValue) - Convert.ToDouble(readValue)) < 0.000001;
                    case DataTypeEnums.String:
                        return string.Equals(writeValue?.ToString(), readValue?.ToString(), StringComparison.Ordinal);
                    default:
                        return writeValue?.Equals(readValue) ?? readValue == null;
                }
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            // 不再需要停止服务器，因为我们现在连接到真实的FINS服务器
        }
    }
}
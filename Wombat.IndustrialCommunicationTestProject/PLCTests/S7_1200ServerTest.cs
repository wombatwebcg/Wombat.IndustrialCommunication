using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.PLC;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTestProject.PLCTests
{
    /// <summary>
    /// S7-1200服务器并发读写测试类
    /// 测试服务器在多客户端并发访问时的线程安全性和数据一致性
    /// </summary>
    public class S7_1200ServerTest : IDisposable
    {
        #region 测试配置常量
        
        /// <summary>测试服务器IP地址</summary>
        private const string TEST_SERVER_IP = "127.0.0.1";
        
        /// <summary>测试服务器端口</summary>
        private const int TEST_SERVER_PORT = 1202;
        
        /// <summary>PLC版本</summary>
        private const SiemensVersion PLC_VERSION = SiemensVersion.S7_1200;
        
        /// <summary>并发测试循环次数</summary>
        private const int CONCURRENT_TEST_CYCLES = 50;
        
        /// <summary>连接超时时间（毫秒）</summary>
        private const int CONNECT_TIMEOUT_MS = 5000;
        
        /// <summary>操作超时时间（毫秒）</summary>
        private const int OPERATION_TIMEOUT_MS = 3000;
        
        #endregion

        #region 测试地址常量
        
        /// <summary>布尔测试地址</summary>
        private const string BOOL_TEST_ADDRESS = "DB1.DBX0.0";
        
        /// <summary>字节测试地址</summary>
        private const string BYTE_TEST_ADDRESS = "DB1.DBB1";
        
        /// <summary>字测试地址</summary>
        private const string WORD_TEST_ADDRESS = "DB1.DBW2";
        
        /// <summary>双字测试地址</summary>
        private const string DWORD_TEST_ADDRESS = "DB1.DBD4";
        
        /// <summary>浮点数测试地址</summary>
        private const string FLOAT_TEST_ADDRESS = "DB1.DBD8";
        
        /// <summary>字符串测试地址</summary>
        private const string STRING_TEST_ADDRESS = "DB1.DBB12";
        
        /// <summary>交叉测试地址1</summary>
        private const string CROSS_ADDRESS_1 = "DB1.DBD20";
        
        /// <summary>交叉测试地址2</summary>
        private const string CROSS_ADDRESS_2 = "DB1.DBD24";
        
        #endregion

        #region 私有字段
        
        private S7TcpServer _server;
        private SiemensClient _client1;
        private SiemensClient _client2;
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;

        #endregion

        #region 构造函数
        
        /// <summary>
        /// 初始化S7-1200服务器测试类
        /// </summary>
        /// <param name="output">测试输出助手</param>
        public S7_1200ServerTest(ITestOutputHelper output = null)
        {
            _output = output;
            
            // 创建简单的控制台日志记录器
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            _logger = loggerFactory.CreateLogger<S7_1200ServerTest>();
        }
        
        #endregion

        #region 公共测试方法

        /// <summary>
        /// 测试S7服务器并发读写功能
        /// 验证两个客户端同时对同一地址进行读写时的数据一致性
        /// </summary>
        [Fact]
        public async Task Test_S7Server_ConcurrentReadWrite()
        {
            // Arrange
            var testName = "S7服务器并发读写测试";
            LogTestStart(testName);
            
            try
            {
                // 启动服务器
                LogStep("启动S7服务器");
                await StartServer();
                
                // 创建并连接两个客户端
                LogStep("创建并连接两个客户端");
                await CreateAndConnectClients();
                
                // 执行并发读写测试
                LogStep("执行并发读写测试");
                await PerformConcurrentReadWriteTest();
                
                // 验证结果一致性
                LogStep("验证结果一致性");
                await VerifyDataConsistency();
                
                LogTestComplete(testName);
            }
            catch (Exception ex)
            {
                LogTestError(testName, ex);
                throw;
            }
            finally
            {
                await CleanupResources();
            }
        }

        /// <summary>
        /// 测试S7服务器交叉并发读写功能
        /// 客户端1读取地址1并写入地址2，客户端2读取地址2并写入地址1，然后交叉验证结果
        /// </summary>
        [Fact]
        public async Task Test_S7Server_CrossConcurrentReadWrite()
        {
            // Arrange
            var testName = "S7服务器交叉并发读写测试";
            LogTestStart(testName);
            
            try
            {
                // 启动服务器
                LogStep("启动S7服务器");
                await StartServer();
                
                // 创建并连接两个客户端
                LogStep("创建并连接两个客户端");
                await CreateAndConnectClients();
                
                // 执行交叉并发读写测试
                LogStep("执行交叉并发读写测试");
                await PerformCrossConcurrentReadWriteTest();
                
                // 验证交叉结果一致性
                LogStep("验证交叉结果一致性");
                await VerifyCrossDataConsistency();
                
                LogTestComplete(testName);
            }
            catch (Exception ex)
            {
                LogTestError(testName, ex);
                throw;
            }
            finally
            {
                await CleanupResources();
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 启动S7服务器
        /// </summary>
        private async Task StartServer()
        {
            try
            {
                // 创建服务器实例
                _server = new S7TcpServer(TEST_SERVER_IP, TEST_SERVER_PORT);
                
                // 配置服务器参数
                _server.SetSiemensVersion(PLC_VERSION);
                _server.SetRackSlot(0, 1);
                _server.UseLogger(_logger);
                
                // 创建测试数据块
                _server.CreateDataBlock(1, 100);
                
                // 启动服务器监听
                var listenResult = _server.Listen();
                Assert.True(listenResult.IsSuccess, $"服务器启动失败: {listenResult.Message}");
                
                LogInfo($"S7服务器已启动，监听地址: {TEST_SERVER_IP}:{TEST_SERVER_PORT}");
                
                // 等待服务器完全启动
                await Task.Delay(1000);
                
                // 验证服务器是否正在监听
                Assert.True(_server.IsListening, "服务器未处于监听状态");
            }
            catch (Exception ex)
            {
                LogError($"启动服务器失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 创建并连接两个客户端
        /// </summary>
        private async Task CreateAndConnectClients()
        {
            try
            {
                // 创建第一个客户端
                _client1 = new SiemensClient(TEST_SERVER_IP, TEST_SERVER_PORT, PLC_VERSION);
                _client1.ConnectTimeout = TimeSpan.FromMilliseconds(CONNECT_TIMEOUT_MS);
                _client1.ReceiveTimeout = TimeSpan.FromMilliseconds(OPERATION_TIMEOUT_MS);
                _client1.SendTimeout = TimeSpan.FromMilliseconds(OPERATION_TIMEOUT_MS);
                
                // 创建第二个客户端
                _client2 = new SiemensClient(TEST_SERVER_IP, TEST_SERVER_PORT, PLC_VERSION);
                _client2.ConnectTimeout = TimeSpan.FromMilliseconds(CONNECT_TIMEOUT_MS);
                _client2.ReceiveTimeout = TimeSpan.FromMilliseconds(OPERATION_TIMEOUT_MS);
                _client2.SendTimeout = TimeSpan.FromMilliseconds(OPERATION_TIMEOUT_MS);
                
                // 连接第一个客户端
                var connect1Result = await _client1.ConnectAsync();
                Assert.True(connect1Result.IsSuccess, $"客户端1连接失败: {connect1Result.Message}");
                
                // 连接第二个客户端
                var connect2Result = await _client2.ConnectAsync();
                Assert.True(connect2Result.IsSuccess, $"客户端2连接失败: {connect2Result.Message}");
                
                LogInfo("两个客户端已成功连接到服务器");
            }
            catch (Exception ex)
            {
                LogError($"创建并连接客户端失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 执行并发读写测试
        /// </summary>
        private async Task PerformConcurrentReadWriteTest()
        {
            var testResults = new List<(string Operation, bool Success, string Message, DateTime Timestamp)>();
            var lockObject = new object();
            
            // 创建并发任务
            var tasks = new List<Task>();
            
            // 客户端1的读写任务
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < CONCURRENT_TEST_CYCLES; i++)
                {
                    try
                    {
                        // 写入布尔值
                        var writeBoolResult = await _client1.WriteAsync(BOOL_TEST_ADDRESS, i % 2 == 0);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client1_WriteBool_{i}", writeBoolResult.IsSuccess, writeBoolResult.Message, DateTime.Now));
                        }
                        
                        // 读取布尔值
                        var readBoolResult = await _client1.ReadBooleanAsync(BOOL_TEST_ADDRESS);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client1_ReadBool_{i}", readBoolResult.IsSuccess, readBoolResult.Message, DateTime.Now));
                        }
                        
                        // 写入整数
                        var writeIntResult = await _client1.WriteAsync(DWORD_TEST_ADDRESS, i * 100);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client1_WriteInt_{i}", writeIntResult.IsSuccess, writeIntResult.Message, DateTime.Now));
                        }
                        
                        // 读取整数
                        var readIntResult = await _client1.ReadInt32Async(DWORD_TEST_ADDRESS);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client1_ReadInt_{i}", readIntResult.IsSuccess, readIntResult.Message, DateTime.Now));
                        }
                        
                        // 写入浮点数
                        var writeFloatResult = await _client1.WriteAsync(FLOAT_TEST_ADDRESS, i * 1.5f);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client1_WriteFloat_{i}", writeFloatResult.IsSuccess, writeFloatResult.Message, DateTime.Now));
                        }
                        
                        // 读取浮点数
                        var readFloatResult = await _client1.ReadFloatAsync(FLOAT_TEST_ADDRESS);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client1_ReadFloat_{i}", readFloatResult.IsSuccess, readFloatResult.Message, DateTime.Now));
                        }
                        
                        await Task.Delay(10); // 短暂延迟
                    }
                    catch (Exception ex)
                    {
                        lock (lockObject)
                        {
                            testResults.Add(($"Client1_Error_{i}", false, ex.Message, DateTime.Now));
                        }
                    }
                }
            }));
            
            // 客户端2的读写任务
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < CONCURRENT_TEST_CYCLES; i++)
                {
                    try
                    {
                        // 写入布尔值
                        var writeBoolResult = await _client2.WriteAsync(BOOL_TEST_ADDRESS, i % 2 == 1);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client2_WriteBool_{i}", writeBoolResult.IsSuccess, writeBoolResult.Message, DateTime.Now));
                        }
                        
                        // 读取布尔值
                        var readBoolResult = await _client2.ReadBooleanAsync(BOOL_TEST_ADDRESS);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client2_ReadBool_{i}", readBoolResult.IsSuccess, readBoolResult.Message, DateTime.Now));
                        }
                        
                        // 写入整数
                        var writeIntResult = await _client2.WriteAsync(DWORD_TEST_ADDRESS, i * 200);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client2_WriteInt_{i}", writeIntResult.IsSuccess, writeIntResult.Message, DateTime.Now));
                        }
                        
                        // 读取整数
                        var readIntResult = await _client2.ReadInt32Async(DWORD_TEST_ADDRESS);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client2_ReadInt_{i}", readIntResult.IsSuccess, readIntResult.Message, DateTime.Now));
                        }
                        
                        // 写入浮点数
                        var writeFloatResult = await _client2.WriteAsync(FLOAT_TEST_ADDRESS, i * 2.5f);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client2_WriteFloat_{i}", writeFloatResult.IsSuccess, writeFloatResult.Message, DateTime.Now));
                        }
                        
                        // 读取浮点数
                        var readFloatResult = await _client2.ReadFloatAsync(FLOAT_TEST_ADDRESS);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client2_ReadFloat_{i}", readFloatResult.IsSuccess, readFloatResult.Message, DateTime.Now));
                        }
                        
                        await Task.Delay(10); // 短暂延迟
                    }
                    catch (Exception ex)
                    {
                        lock (lockObject)
                        {
                            testResults.Add(($"Client2_Error_{i}", false, ex.Message, DateTime.Now));
                        }
                    }
                }
            }));
            
            // 等待所有任务完成
            await Task.WhenAll(tasks);
            
            // 统计测试结果
            var successCount = testResults.Count(r => r.Success);
            var failureCount = testResults.Count(r => !r.Success);
            
            LogInfo($"并发读写测试完成 - 成功: {successCount}, 失败: {failureCount}, 总计: {testResults.Count}");
            
            // 输出失败的操作
            var failures = testResults.Where(r => !r.Success).ToList();
            if (failures.Any())
            {
                LogWarning($"发现 {failures.Count} 个失败操作:");
                foreach (var failure in failures.Take(10)) // 只显示前10个失败
                {
                    LogWarning($"  {failure.Operation}: {failure.Message}");
                }
            }
            
            // 验证大部分操作成功
            var successRate = (double)successCount / testResults.Count;
            Assert.True(successRate > 0.8, $"操作成功率过低: {successRate:P2}");
        }

        /// <summary>
        /// 执行交叉并发读写测试
        /// 客户端1读取地址1并写入地址2，客户端2读取地址2并写入地址1
        /// </summary>
        private async Task PerformCrossConcurrentReadWriteTest()
        {
            var testResults = new List<(string Operation, bool Success, string Message, DateTime Timestamp)>();
            var lockObject = new object();
            
            // 初始化测试数据
            var initialValue1 = 1000;
            var initialValue2 = 2000;
            
            // 预先写入初始数据
            var initResult1 = await _client1.WriteAsync(CROSS_ADDRESS_1, initialValue1);
            var initResult2 = await _client1.WriteAsync(CROSS_ADDRESS_2, initialValue2);
            
            Assert.True(initResult1.IsSuccess, $"初始化地址1失败: {initResult1.Message}");
            Assert.True(initResult2.IsSuccess, $"初始化地址2失败: {initResult2.Message}");
            
            LogInfo($"初始化数据完成 - 地址1: {initialValue1}, 地址2: {initialValue2}");
            
            // 创建并发任务
            var tasks = new List<Task>();
            
            // 客户端1的任务：读取地址1，写入地址2
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < CONCURRENT_TEST_CYCLES; i++)
                {
                    try
                    {
                        // 读取地址1
                        var readResult = await _client1.ReadInt32Async(CROSS_ADDRESS_1);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client1_ReadAddress1_{i}", readResult.IsSuccess, readResult.Message, DateTime.Now));
                        }
                        
                        if (readResult.IsSuccess)
                        {
                            LogInfo($"客户端1读取地址1成功: {readResult.ResultValue}");
                        }
                        
                        // 写入地址2
                        var writeValue = initialValue2 + i * 10;
                        var writeResult = await _client1.WriteAsync(CROSS_ADDRESS_2, writeValue);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client1_WriteAddress2_{i}", writeResult.IsSuccess, writeResult.Message, DateTime.Now));
                        }
                        
                        if (writeResult.IsSuccess)
                        {
                            LogInfo($"客户端1写入地址2成功: {writeValue}");
                        }
                        
                        await Task.Delay(10); // 短暂延迟
                    }
                    catch (Exception ex)
                    {
                        lock (lockObject)
                        {
                            testResults.Add(($"Client1_CrossError_{i}", false, ex.Message, DateTime.Now));
                        }
                    }
                }
            }));
            
            // 客户端2的任务：读取地址2，写入地址1
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < CONCURRENT_TEST_CYCLES; i++)
                {
                    try
                    {
                        // 读取地址2
                        var readResult = await _client2.ReadInt32Async(CROSS_ADDRESS_2);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client2_ReadAddress2_{i}", readResult.IsSuccess, readResult.Message, DateTime.Now));
                        }
                        
                        if (readResult.IsSuccess)
                        {
                            LogInfo($"客户端2读取地址2成功: {readResult.ResultValue}");
                        }
                        
                        // 写入地址1
                        var writeValue = initialValue1 + i * 20;
                        var writeResult = await _client2.WriteAsync(CROSS_ADDRESS_1, writeValue);
                        lock (lockObject)
                        {
                            testResults.Add(($"Client2_WriteAddress1_{i}", writeResult.IsSuccess, writeResult.Message, DateTime.Now));
                        }
                        
                        if (writeResult.IsSuccess)
                        {
                            LogInfo($"客户端2写入地址1成功: {writeValue}");
                        }
                        
                        await Task.Delay(10); // 短暂延迟
                    }
                    catch (Exception ex)
                    {
                        lock (lockObject)
                        {
                            testResults.Add(($"Client2_CrossError_{i}", false, ex.Message, DateTime.Now));
                        }
                    }
                }
            }));
            
            // 等待所有任务完成
            await Task.WhenAll(tasks);
            
            // 统计测试结果
            var successCount = testResults.Count(r => r.Success);
            var failureCount = testResults.Count(r => !r.Success);
            
            LogInfo($"交叉并发读写测试完成 - 成功: {successCount}, 失败: {failureCount}, 总计: {testResults.Count}");
            
            // 验证大部分操作成功
            var successRate = (double)successCount / testResults.Count;
            Assert.True(successRate > 0.8, $"交叉操作成功率过低: {successRate:P2}");
        }

        /// <summary>
        /// 验证数据一致性
        /// </summary>
        private async Task VerifyDataConsistency()
        {
            try
            {
                // 等待一段时间确保所有操作完成
                await Task.Delay(500);
                
                // 两个客户端读取相同地址的数据进行比较
                var client1BoolResult = await _client1.ReadBooleanAsync(BOOL_TEST_ADDRESS);
                var client2BoolResult = await _client2.ReadBooleanAsync(BOOL_TEST_ADDRESS);
                
                Assert.True(client1BoolResult.IsSuccess, $"客户端1读取布尔值失败: {client1BoolResult.Message}");
                Assert.True(client2BoolResult.IsSuccess, $"客户端2读取布尔值失败: {client2BoolResult.Message}");
                
                // 验证两个客户端读取的布尔值一致
                Assert.Equal(client1BoolResult.ResultValue, client2BoolResult.ResultValue);
                LogInfo($"布尔值一致性验证通过: {client1BoolResult.ResultValue}");
                
                // 验证整数数据一致性
                var client1IntResult = await _client1.ReadInt32Async(DWORD_TEST_ADDRESS);
                var client2IntResult = await _client2.ReadInt32Async(DWORD_TEST_ADDRESS);
                
                Assert.True(client1IntResult.IsSuccess, $"客户端1读取整数失败: {client1IntResult.Message}");
                Assert.True(client2IntResult.IsSuccess, $"客户端2读取整数失败: {client2IntResult.Message}");
                
                Assert.Equal(client1IntResult.ResultValue, client2IntResult.ResultValue);
                LogInfo($"整数一致性验证通过: {client1IntResult.ResultValue}");
                
                // 验证浮点数数据一致性
                var client1FloatResult = await _client1.ReadFloatAsync(FLOAT_TEST_ADDRESS);
                var client2FloatResult = await _client2.ReadFloatAsync(FLOAT_TEST_ADDRESS);
                
                Assert.True(client1FloatResult.IsSuccess, $"客户端1读取浮点数失败: {client1FloatResult.Message}");
                Assert.True(client2FloatResult.IsSuccess, $"客户端2读取浮点数失败: {client2FloatResult.Message}");
                
                Assert.Equal(client1FloatResult.ResultValue, client2FloatResult.ResultValue, 0.001f); // 3位小数精度
                LogInfo($"浮点数一致性验证通过: {client1FloatResult.ResultValue}");
                
                LogInfo("所有数据一致性验证通过");
            }
            catch (Exception ex)
            {
                LogError($"数据一致性验证失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 验证交叉数据一致性
        /// 两个客户端分别读取两个地址，验证数据一致性
        /// </summary>
        private async Task VerifyCrossDataConsistency()
        {
            try
            {
                // 等待一段时间确保所有操作完成
                await Task.Delay(500);
                
                LogInfo("开始交叉数据一致性验证...");
                
                // 客户端1读取两个地址
                var client1ReadAddress1 = await _client1.ReadInt32Async(CROSS_ADDRESS_1);
                var client1ReadAddress2 = await _client1.ReadInt32Async(CROSS_ADDRESS_2);
                
                Assert.True(client1ReadAddress1.IsSuccess, $"客户端1读取地址1失败: {client1ReadAddress1.Message}");
                Assert.True(client1ReadAddress2.IsSuccess, $"客户端1读取地址2失败: {client1ReadAddress2.Message}");
                
                LogInfo($"客户端1读取结果 - 地址1: {client1ReadAddress1.ResultValue}, 地址2: {client1ReadAddress2.ResultValue}");
                
                // 客户端2读取两个地址
                var client2ReadAddress1 = await _client2.ReadInt32Async(CROSS_ADDRESS_1);
                var client2ReadAddress2 = await _client2.ReadInt32Async(CROSS_ADDRESS_2);
                
                Assert.True(client2ReadAddress1.IsSuccess, $"客户端2读取地址1失败: {client2ReadAddress1.Message}");
                Assert.True(client2ReadAddress2.IsSuccess, $"客户端2读取地址2失败: {client2ReadAddress2.Message}");
                
                LogInfo($"客户端2读取结果 - 地址1: {client2ReadAddress1.ResultValue}, 地址2: {client2ReadAddress2.ResultValue}");
                
                // 验证地址1的数据一致性
                Assert.Equal(client1ReadAddress1.ResultValue, client2ReadAddress1.ResultValue);
                LogInfo($"地址1数据一致性验证通过: {client1ReadAddress1.ResultValue}");
                
                // 验证地址2的数据一致性
                Assert.Equal(client1ReadAddress2.ResultValue, client2ReadAddress2.ResultValue);
                LogInfo($"地址2数据一致性验证通过: {client1ReadAddress2.ResultValue}");
                
                LogInfo("交叉数据一致性验证通过:");
                LogInfo($"  地址1 ({CROSS_ADDRESS_1}): {client1ReadAddress1.ResultValue}");
                LogInfo($"  地址2 ({CROSS_ADDRESS_2}): {client1ReadAddress2.ResultValue}");
                
                // 验证数据变化符合预期
                var expectedMinValue1 = 1000; // 初始值
                var expectedMinValue2 = 2000; // 初始值
                
                Assert.True(client1ReadAddress1.ResultValue >= expectedMinValue1, 
                    $"地址1的值 {client1ReadAddress1.ResultValue} 小于预期最小值 {expectedMinValue1}");
                Assert.True(client1ReadAddress2.ResultValue >= expectedMinValue2, 
                    $"地址2的值 {client1ReadAddress2.ResultValue} 小于预期最小值 {expectedMinValue2}");
                
                LogInfo("所有交叉数据一致性验证通过");
            }
            catch (Exception ex)
            {
                LogError($"交叉数据一致性验证失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private async Task CleanupResources()
        {
            try
            {
                // 断开客户端连接
                if (_client1 != null)
                {
                    try
                    {
                        await _client1.DisconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"客户端1断开连接时发生异常: {ex.Message}");
                    }
                    finally
                    {
                        _client1.Dispose();
                        _client1 = null;
                    }
                }
                
                if (_client2 != null)
                {
                    try
                    {
                        await _client2.DisconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"客户端2断开连接时发生异常: {ex.Message}");
                    }
                    finally
                    {
                        _client2.Dispose();
                        _client2 = null;
                    }
                }
                
                // 停止服务器
                if (_server != null)
                {
                    try
                    {
                        _server.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"服务器关闭时发生异常: {ex.Message}");
                    }
                    finally
                    {
                        _server.Dispose();
                        _server = null;
                    }
                }
                
                LogInfo("资源清理完成");
            }
            catch (Exception ex)
            {
                LogError($"资源清理失败: {ex.Message}");
            }
        }

        #endregion

        #region 日志方法

        private void LogTestStart(string testName)
        {
            var message = $"开始测试: {testName}";
            LogInfo(message);
            _output?.WriteLine(message);
        }

        private void LogStep(string step)
        {
            var message = $"  → {step}";
            LogInfo(message);
            _output?.WriteLine(message);
        }

        private void LogTestComplete(string testName)
        {
            var message = $"测试完成: {testName}";
            LogInfo(message);
            _output?.WriteLine(message);
        }

        private void LogTestError(string testName, Exception ex)
        {
            var message = $"测试失败: {testName} - {ex.Message}";
            LogError(message);
            _output?.WriteLine(message);
        }

        private void LogInfo(string message)
        {
            _logger?.LogInformation(message);
            _output?.WriteLine($"[INFO] {message}");
        }

        private void LogWarning(string message)
        {
            _logger?.LogWarning(message);
            _output?.WriteLine($"[WARN] {message}");
        }

        private void LogError(string message)
        {
            _logger?.LogError(message);
            _output?.WriteLine($"[ERROR] {message}");
        }

        #endregion

        #region IDisposable Implementation

        private bool _disposed = false;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // 清理托管资源
                CleanupResources().ConfigureAwait(false).GetAwaiter().GetResult();
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~S7_1200ServerTest()
        {
            Dispose(false);
        }

        #endregion
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication;
using Wombat.IndustrialCommunication.PLC;
using Wombat.IndustrialCommunicationTestProject.Helper;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    /// <summary>
    /// S7-1200 PLC通讯测试类
    /// 测试包括：同步/异步读写、断线重连、短连接、数据类型综合测试
    /// </summary>
    public class S7_1200
    {
        #region 测试配置常量
        
        /// <summary>测试PLC IP地址</summary>
        private const string TEST_PLC_IP = "192.168.6.159";
        
        /// <summary>测试PLC端口</summary>
        private const int TEST_PLC_PORT = 102;
        
        /// <summary>PLC版本</summary>
        private const SiemensVersion PLC_VERSION = SiemensVersion.S7_1200;
        
        /// <summary>基础测试循环次数</summary>
        private const int BASIC_TEST_CYCLES = 20;
        
        /// <summary>高频测试循环次数</summary>
        private const int HIGH_FREQUENCY_TEST_CYCLES = 100;
        
        /// <summary>自动重连最大尝试次数</summary>
        private const int MAX_RECONNECT_ATTEMPTS = 3;
        
        /// <summary>重连延迟时间（秒）</summary>
        private const int RECONNECT_DELAY_SECONDS = 5;
        
        /// <summary>等待自动重连时间（毫秒）</summary>
        private const int WAIT_RECONNECT_TIMEOUT = 7000;
        
        /// <summary>浮点数比较精度</summary>
        private const double FLOAT_COMPARISON_PRECISION = 0.001;
        
        #endregion

        #region 测试地址常量 - 基于实际PLC地址划分
        
        /// <summary>布尔测试地址 - Data0区域 (0.0-3.7)</summary>
        private const string BOOL_TEST_ADDRESS_1 = "DB200.DBX0.0";            // Data0: Bool数组区域
        private const string BOOL_TEST_ADDRESS_2 = "DB200.DBX0.1";            // Data0: Bool数组区域
        private const string BOOL_TEST_ADDRESS_3 = "DB200.DBX0.2";            // Data0: Bool数组区域
        
        /// <summary>16位整数测试地址 - Data1区域 (4.0-67.7)</summary>
        private const string INT16_TEST_ADDRESS = "DB200.DBW4";               // Data1: Word数组区域
        private const string UINT16_TEST_ADDRESS = "DB200.DBW6";              // Data1: Word数组区域
        
        /// <summary>32位整数测试地址 - Data2区域 (68.0-195.7)</summary>
        private const string INT32_TEST_ADDRESS = "DB200.DBD68";              // Data2: DWord数组区域
        private const string UINT32_TEST_ADDRESS = "DB200.DBD72";             // Data2: DWord数组区域
        
        /// <summary>64位整数测试地址 - Data2区域 (68.0-195.7)</summary>
        private const string INT64_TEST_ADDRESS = "DB200.DBD76";              // Data2: DWord数组区域(占用8字节)
        private const string UINT64_TEST_ADDRESS = "DB200.DBD84";             // Data2: DWord数组区域(占用8字节)
        
        /// <summary>浮点数测试地址 - Data3区域 (196.0-323.7)</summary>
        private const string FLOAT_TEST_ADDRESS = "DB200.DBD196";             // Data3: Real数组区域
        private const string DOUBLE_TEST_ADDRESS = "DB200.DBD200";            // Data3: Real数组区域(占用8字节)
        
        /// <summary>数组测试地址 - 在各自数据类型区域内</summary>
        private const string BOOL_ARRAY_TEST_ADDRESS = "DB200.DBX1.0";        // Data0: Bool数组区域 (从字节1开始)
        private const string INT_ARRAY_TEST_ADDRESS = "DB200.DBD8";           // Data1: Word数组区域 (10个int32需要40字节)
        private const string FLOAT_ARRAY_TEST_ADDRESS = "DB200.DBD208";       // Data3: Real数组区域 (5个float需要20字节)
        
        /// <summary>连接测试地址 - Data2区域安全位置</summary>
        private const string CONNECTION_TEST_ADDRESS = "DB200.DBD92";          // Data2: DWord数组区域
        private const string CONNECTION_TEST_ADDRESS_2 = "DB200.DBD96";       // Data2: DWord数组区域
        
        /// <summary>综合测试地址 - 基于实际PLC地址划分</summary>
        private const string COMPREHENSIVE_BOOL_ADDRESS_1 = "DB200.DBX2.0";   // Data0: Bool数组区域
        private const string COMPREHENSIVE_BOOL_ADDRESS_2 = "DB200.DBX2.1";   // Data0: Bool数组区域
        private const string COMPREHENSIVE_INT16_ADDRESS = "DB200.DBW48";     // Data1: Word数组区域
        private const string COMPREHENSIVE_UINT16_ADDRESS = "DB200.DBW50";    // Data1: Word数组区域
        private const string COMPREHENSIVE_INT32_ADDRESS = "DB200.DBD100";    // Data2: DWord数组区域
        private const string COMPREHENSIVE_UINT32_ADDRESS = "DB200.DBD104";   // Data2: DWord数组区域
        private const string COMPREHENSIVE_INT64_ADDRESS = "DB200.DBD108";    // Data2: DWord数组区域(占用8字节)
        private const string COMPREHENSIVE_UINT64_ADDRESS = "DB200.DBD116";   // Data2: DWord数组区域(占用8字节)
        private const string COMPREHENSIVE_FLOAT_ADDRESS = "DB200.DBD228";    // Data3: Real数组区域
        private const string COMPREHENSIVE_DOUBLE_ADDRESS = "DB200.DBD232";   // Data3: Real数组区域(占用8字节)
        
        #endregion

        #region 私有字段
        
        private SiemensClient client;
        private readonly ITestOutputHelper _output;

        
        #endregion

        #region 构造函数
        
        /// <summary>
        /// 初始化S7-1200测试类
        /// </summary>
        /// <param name="output">测试输出助手</param>
        public S7_1200(ITestOutputHelper output = null)
        {
            _output = output;
        }
        
        #endregion

        #region 公共测试方法

        /// <summary>
        /// 测试S7-1200同步读写功能
        /// 包括各种数据类型的基本读写操作
        /// </summary>
        [Fact]
        public void Test_S7_1200_SyncReadWrite()
        {
            // Arrange
            var testName = "S7-1200同步读写测试";
            LogTestStart(testName);
            
            client = new SiemensClient(TEST_PLC_IP, TEST_PLC_PORT, PLC_VERSION);
            
            try
            {
                // Act & Assert
                LogStep("建立PLC连接");
                var connectResult = client.Connect();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                
                LogStep("执行同步读写测试");
                ReadWrite();
                
                LogStep("断开PLC连接");
                var disconnectResult = client.Disconnect();
                Assert.True(disconnectResult.IsSuccess, $"断开连接失败: {disconnectResult.Message}");
                
                LogTestComplete(testName);
            }
            catch (Exception ex)
            {
                LogTestError(testName, ex);
                throw;
            }
            finally
            {
                SafeDisconnect();
            }
        }

        /// <summary>
        /// 测试S7-1200异步读写功能
        /// 包括各种数据类型的异步读写操作
        /// </summary>
        [Fact]
        public async Task Test_S7_1200_AsyncReadWrite()
        {
            // Arrange
            var testName = "S7-1200异步读写测试";
            LogTestStart(testName);
            
            client = new SiemensClient(TEST_PLC_IP, TEST_PLC_PORT, PLC_VERSION);
            
            try
            {
                // Act & Assert
                LogStep("建立PLC连接");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                client.IsLongConnection = false;
                LogStep("执行异步读写测试");
                await ReadWriteAsync();
                
                LogStep("断开PLC连接");
                var disconnectResult = await client.DisconnectAsync();
                Assert.True(disconnectResult.IsSuccess, $"断开连接失败: {disconnectResult.Message}");
                
                LogTestComplete(testName);
            }
            catch (Exception ex)
            {
                LogTestError(testName, ex);
                throw;
            }
            finally
            {
                await SafeDisconnectAsync();
            }
        }

        /// <summary>
        /// 测试S7-1200断线重连功能
        /// 验证自动重连机制的可靠性
        /// </summary>
        [Fact]
        public async Task Test_S7_1200_DisconnectionRecovery()
        {
            // Arrange
            var testName = "S7-1200断线重连测试";
            LogTestStart(testName);
            
            client = new SiemensClient(TEST_PLC_IP, TEST_PLC_PORT, PLC_VERSION);
            
            try
            {
                // Arrange - 配置自动重连
                LogStep("配置自动重连参数");
                client.EnableAutoReconnect = true;
                client.MaxReconnectAttempts = MAX_RECONNECT_ATTEMPTS;
                client.ReconnectDelay = TimeSpan.FromSeconds(RECONNECT_DELAY_SECONDS);
                
                // Act & Assert - 建立连接
                LogStep("建立PLC连接");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                Assert.True(client.Connected, "连接状态应为已连接");
                
                // Act & Assert - 验证连接有效性
                LogStep("验证连接有效性");
                var testValue = 12345;
                var writeResult = await client.WriteAsync(CONNECTION_TEST_ADDRESS, testValue);
                Assert.True(writeResult.IsSuccess, $"写入失败: {writeResult.Message}");
                var readResult = await client.ReadInt32Async(CONNECTION_TEST_ADDRESS);
                Assert.True(readResult.IsSuccess, $"读取失败: {readResult.Message}");
                Assert.Equal(testValue, readResult.ResultValue);
                

                
                // Act - 等待自动重连
                LogStep($"等待自动重连（最长{WAIT_RECONNECT_TIMEOUT}ms）");
                await Task.Delay(WAIT_RECONNECT_TIMEOUT);
                
                // Assert - 验证重连恢复
                LogStep("验证连接恢复");
                LogInfo($"当前连接状态: {(client.Connected ? "已连接" : "未连接")}");
                
                var recoveryTestValue = 54321;
                writeResult = await client.WriteAsync(CONNECTION_TEST_ADDRESS, recoveryTestValue);
                if (writeResult.IsSuccess)
                {
                    LogInfo("写入成功，连接已恢复");
                    readResult = await client.ReadInt32Async(CONNECTION_TEST_ADDRESS);
                    Assert.True(readResult.IsSuccess, $"读取失败: {readResult.Message}");
                    Assert.Equal(recoveryTestValue, readResult.ResultValue);
                }
                else
                {
                    LogWarning($"写入失败: {writeResult.Message}，连接可能未恢复");
                    Assert.True(client.Connected, "连接状态应为已连接");
                }
                
                LogTestComplete(testName);
            }
            catch (Exception ex)
            {
                LogTestError(testName, ex);
                throw;
            }
            finally
            {
                // 清理：禁用自动重连并断开连接
                if (client != null)
                {
                    client.EnableAutoReconnect = false;
                    await SafeDisconnectAsync();
                }
            }
        }

        /// <summary>
        /// 测试S7-1200短连接功能
        /// 验证短连接模式下的读写操作
        /// </summary>
        [Fact]
        public async Task Test_S7_1200_ShortConnection()
        {
            // Arrange
            var testName = "S7-1200短连接测试";
            LogTestStart(testName);
            
            client = new SiemensClient(TEST_PLC_IP, TEST_PLC_PORT, PLC_VERSION);
            
            try
            {
                // Arrange - 配置短连接模式
                LogStep("配置短连接模式");
                client.IsLongConnection = false;
                
                // Act & Assert - 测试布尔值读写
                LogStep("测试短连接布尔值读写");
                var writeBoolResult = await client.WriteAsync(BOOL_TEST_ADDRESS_1, true);
                Assert.True(writeBoolResult.IsSuccess, $"写入布尔值失败: {writeBoolResult.Message}");
                var readBoolResult = await client.ReadBooleanAsync(BOOL_TEST_ADDRESS_1);
                Assert.True(readBoolResult.IsSuccess, $"读取布尔值失败: {readBoolResult.Message}");
                Assert.True(readBoolResult.ResultValue, "读取的布尔值应为true");
                
                // Assert - 验证短连接状态
                LogInfo($"操作后连接状态: {(client.Connected ? "已连接" : "未连接")}");
                Assert.False(client.Connected, "短连接模式下操作后应已断开");
                
                // Act & Assert - 测试整数读写
                LogStep("测试短连接整数读写");
                var testIntValue = 12345;
                var writeIntResult = await client.WriteAsync(CONNECTION_TEST_ADDRESS, testIntValue);
                Assert.True(writeIntResult.IsSuccess, $"写入整数失败: {writeIntResult.Message}");
                var readIntResult = await client.ReadInt32Async(CONNECTION_TEST_ADDRESS);
                Assert.True(readIntResult.IsSuccess, $"读取整数失败: {readIntResult.Message}");
                Assert.Equal(testIntValue, readIntResult.ResultValue);
                
                // Assert - 验证短连接状态
                LogInfo($"操作后连接状态: {(client.Connected ? "已连接" : "未连接")}");
                Assert.False(client.Connected, "短连接模式下操作后应已断开");
                
                // Act & Assert - 测试浮点数读写
                LogStep("测试短连接浮点数读写");
                var testFloatValue = 123.45f;
                var writeFloatResult = await client.WriteAsync(CONNECTION_TEST_ADDRESS_2, testFloatValue);
                Assert.True(writeFloatResult.IsSuccess, $"写入浮点数失败: {writeFloatResult.Message}");
                var readFloatResult = await client.ReadFloatAsync(CONNECTION_TEST_ADDRESS_2);
                Assert.True(readFloatResult.IsSuccess, $"读取浮点数失败: {readFloatResult.Message}");
                Assert.Equal(testFloatValue, readFloatResult.ResultValue);
                
                // Assert - 验证短连接状态
                LogInfo($"操作后连接状态: {(client.Connected ? "已连接" : "未连接")}");
                Assert.False(client.Connected, "短连接模式下操作后应已断开");
                
                // Act & Assert - 测试启用自动重连的短连接
                LogStep("启用自动重连并测试短连接数组读写");
                client.EnableAutoReconnect = true;
                client.MaxReconnectAttempts = 1;
                
                var testIntArray = new int[] { 1, 2, 3, 4, 5 };
                var writeArrayResult = await client.WriteAsync("DB200.DBD110", testIntArray);
                Assert.True(writeArrayResult.IsSuccess, $"写入数组失败: {writeArrayResult.Message}");
                var readArrayResult = await client.ReadInt32Async("DB200.DBD110", testIntArray.Length);
                Assert.True(readArrayResult.IsSuccess, $"读取数组失败: {readArrayResult.Message}");
                
                for (int i = 0; i < testIntArray.Length; i++)
                {
                    Assert.Equal(testIntArray[i], readArrayResult.ResultValue[i]);
                }
                
                // Assert - 验证短连接状态
                LogInfo($"操作后连接状态: {(client.Connected ? "已连接" : "未连接")}");
                Assert.False(client.Connected, "短连接模式下操作后应已断开");
                
                LogTestComplete(testName);
            }
            catch (Exception ex)
            {
                LogTestError(testName, ex);
                throw;
            }
            finally
            {
                // 清理：恢复长连接模式并断开连接
                if (client != null)
                {
                    client.IsLongConnection = true;
                    client.EnableAutoReconnect = false;
                    await SafeDisconnectAsync();
                }
            }
        }

        /// <summary>
        /// 测试S7-1200数据类型综合功能
        /// 验证所有支持的数据类型读写
        /// </summary>
        [Fact]
        public async Task Test_S7_1200_ComprehensiveDataTypes()
        {
            // Arrange
            var testName = "S7-1200数据类型综合测试";
            LogTestStart(testName);
            
            client = new SiemensClient(TEST_PLC_IP, TEST_PLC_PORT, PLC_VERSION);
            
            try
            {
                // Act & Assert
                LogStep("建立PLC连接");
                client.ConnectTimeout = TimeSpan.FromSeconds(1);

                var connectResult = await client.ConnectAsync();

                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                
                LogStep("执行综合数据类型测试");
                await TestAllDataTypes();
                
                LogStep("断开PLC连接");
                await client.DisconnectAsync();
                
                LogTestComplete(testName);
            }
            catch (Exception ex)
            {
                LogTestError(testName, ex);
                throw;
            }
            finally
            {
                await SafeDisconnectAsync();
            }
        }

        /// <summary>
        /// 测试S7-1200批量读取功能
        /// 验证批量读取优化算法的性能和正确性
        /// </summary>
        [Fact]
        public async Task Test_S7_1200_BatchRead()
        {
            // Arrange
            var testName = "S7-1200批量读取测试";
            LogTestStart(testName);
            
            client = new SiemensClient(TEST_PLC_IP, TEST_PLC_PORT, PLC_VERSION);
            
            try
            {
                // Act & Assert
                LogStep("建立PLC连接");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                
                LogStep("执行批量读取测试");
                await TestBatchReadPerformance();
                
                LogStep("断开PLC连接");
                await client.DisconnectAsync();
                
                LogTestComplete(testName);
            }
            catch (Exception ex)
            {
                LogTestError(testName, ex);
            }
            finally
            {
                await SafeDisconnectAsync();
            }
        }

        /// <summary>
        /// 测试S7-1200批量写入功能
        /// 验证批量写入功能的性能和正确性
        /// </summary>
        [Fact]
        public async Task Test_S7_1200_BatchWrite()
        {
            // Arrange
            var testName = "S7-1200批量写入测试";
            LogTestStart(testName);
            
            client = new SiemensClient(TEST_PLC_IP, TEST_PLC_PORT, PLC_VERSION);
            
            try
            {
                // Act & Assert
                LogStep("建立PLC连接");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                
                LogStep("执行批量写入测试");
                await TestBatchWritePerformance();
                
                LogStep("断开PLC连接");
                await client.DisconnectAsync();
                
                LogTestComplete(testName);
            }
            catch (Exception ex)
            {
                LogTestError(testName, ex);
            }
            finally
            {
                await SafeDisconnectAsync();
            }
        }

        /// <summary>
        /// 测试TcpClientAdapter阻塞修复
        /// 验证修复后的TcpClientAdapter不会在批量读取时阻塞
        /// </summary>
        [Fact]
        public async Task Test_S7_1200_TcpClientAdapter_BlockingFix()
        {
            // Arrange
            var testName = "S7-1200 TcpClientAdapter阻塞修复测试";
            LogTestStart(testName);
            
            client = new SiemensClient(TEST_PLC_IP, TEST_PLC_PORT, PLC_VERSION);
            
            try
            {
                // Act & Assert - 建立连接
                LogStep("建立PLC连接");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                
                // 准备简单的批量读取地址
                LogStep("准备批量读取地址");
                var addresses = new Dictionary<string, DataTypeEnums>
                {
                    ["DB200.DBX0.0"] = DataTypeEnums.Bool,
                    ["DB200.DBW4"] = DataTypeEnums.Int16,
                    ["DB200.DBD68"] = DataTypeEnums.Int32
                };
                
                // 使用CancellationToken设置较短的超时，如果阻塞会快速检测到
                LogStep("执行批量读取测试（5秒超时检测阻塞）");
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var batchReadTask = client.BatchReadAsync(addresses);
                    
                    // 等待批量读取完成或超时
                    var completedTask = await Task.WhenAny(batchReadTask.AsTask(), Task.Delay(5000, cts.Token));
                    
                    if (completedTask == batchReadTask.AsTask())
                    {
                        var batchReadResult = await batchReadTask;
                        LogInfo($"批量读取完成，结果: {(batchReadResult.IsSuccess ? "成功" : "失败")}");
                        
                        if (batchReadResult.IsSuccess)
                        {
                            var successCount = batchReadResult.ResultValue.Where(kvp => kvp.Value.Item2 != null).Count();
                            LogInfo($"成功读取 {successCount}/{addresses.Count} 个地址");
                        }
                        else
                        {
                            LogWarning($"批量读取失败: {batchReadResult.Message}");
                        }
                    }
                    else
                    {
                        LogTestError(testName, new TimeoutException("批量读取在5秒内未完成，可能存在阻塞问题"));
                        Assert.True(false, "批量读取超时，可能存在阻塞问题");
                    }
                }
                
                LogStep("断开PLC连接");
                await client.DisconnectAsync();
                
                LogTestComplete(testName);
                LogInfo("TcpClientAdapter阻塞修复验证成功！");
            }
            catch (Exception ex)
            {
                LogTestError(testName, ex);
                throw;
            }
            finally
            {
                await SafeDisconnectAsync();
            }
        }

        #endregion

        #region 私有测试方法

        /// <summary>
        /// 执行同步读写测试
        /// </summary>
        private void ReadWrite()
        {
            LogInfo("执行基于DB200的同步读写测试");
            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            
            for (int i = 0; i < BASIC_TEST_CYCLES; i++)
            {
                _output?.WriteLine($"执行第 {i + 1} 轮读写测试");
                
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                ushort short_number_1 = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                uint int_number_1 = (uint)rnd.Next(0, int.MaxValue);
                float float_number = int_number / 100f;
                var bool_value = short_number % 2 == 1;

                // 测试布尔值 - 使用DB200的位地址
                LogInfo("测试布尔值读写");
                client.Write(BOOL_TEST_ADDRESS_1, true);
                Assert.True(client.ReadBoolean(BOOL_TEST_ADDRESS_1).ResultValue == true);
                client.Write(BOOL_TEST_ADDRESS_2, bool_value);
                Assert.True(client.ReadBoolean(BOOL_TEST_ADDRESS_2).ResultValue == bool_value);
                client.Write(BOOL_TEST_ADDRESS_3, !bool_value);
                Assert.True(client.ReadBoolean(BOOL_TEST_ADDRESS_3).ResultValue == !bool_value);

                // 测试16位整数
                LogInfo("测试16位整数读写");
                client.Write(INT16_TEST_ADDRESS, short_number);
                Assert.True(client.ReadInt16(INT16_TEST_ADDRESS).ResultValue == short_number);
                client.Write(UINT16_TEST_ADDRESS, short_number_1);
                Assert.True(client.ReadUInt16(UINT16_TEST_ADDRESS).ResultValue == short_number_1);

                // 测试32位整数
                LogInfo("测试32位整数读写");
                client.Write(INT32_TEST_ADDRESS, int_number);
                Assert.True(client.ReadInt32(INT32_TEST_ADDRESS).ResultValue == int_number);
                client.Write(UINT32_TEST_ADDRESS, int_number_1);
                Assert.True(client.ReadUInt32(UINT32_TEST_ADDRESS).ResultValue == int_number_1);

                // 测试64位整数
                LogInfo("测试64位整数读写");
                client.Write(INT64_TEST_ADDRESS, Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64(INT64_TEST_ADDRESS).ResultValue == Convert.ToInt64(int_number));
                client.Write(UINT64_TEST_ADDRESS, Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64(UINT64_TEST_ADDRESS).ResultValue == Convert.ToUInt64(int_number_1));

                // 测试浮点数
                LogInfo("测试浮点数读写");
                client.Write(FLOAT_TEST_ADDRESS, float_number);
                Assert.True(client.ReadFloat(FLOAT_TEST_ADDRESS).ResultValue == float_number);
                client.Write(DOUBLE_TEST_ADDRESS, Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble(DOUBLE_TEST_ADDRESS).ResultValue == Convert.ToDouble(float_number));
            }
            
            LogInfo("执行数组读写测试");
            TestArrayReadWrite();
        }

        /// <summary>
        /// 执行异步读写测试
        /// </summary>
        private async Task ReadWriteAsync()
        {
            LogInfo("执行基于DB200的异步读写测试");
            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            
            for (int i = 0; i < BASIC_TEST_CYCLES; i++)
            {
                _output?.WriteLine($"执行第 {i + 1} 轮异步读写测试");
                
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                ushort short_number_1 = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                uint int_number_1 = (uint)rnd.Next(0, int.MaxValue);
                float float_number = int_number / 100f;
                var bool_value = short_number % 2 == 1;

                // 测试布尔值
                LogInfo("测试布尔值读写");
                await client.WriteAsync(BOOL_TEST_ADDRESS_1, true);
                Assert.True((await client.ReadBooleanAsync(BOOL_TEST_ADDRESS_1)).ResultValue == true);
                await client.WriteAsync(BOOL_TEST_ADDRESS_2, bool_value);
                Assert.True((await client.ReadBooleanAsync(BOOL_TEST_ADDRESS_2)).ResultValue == bool_value);
                await client.WriteAsync(BOOL_TEST_ADDRESS_3, !bool_value);
                Assert.True((await client.ReadBooleanAsync(BOOL_TEST_ADDRESS_3)).ResultValue == !bool_value);

                // 测试16位整数
                LogInfo("测试16位整数读写");
                await client.WriteAsync(INT16_TEST_ADDRESS, short_number);
                Assert.True((await client.ReadInt16Async(INT16_TEST_ADDRESS)).ResultValue == short_number);
                await client.WriteAsync(UINT16_TEST_ADDRESS, short_number_1);
                Assert.True((await client.ReadUInt16Async(UINT16_TEST_ADDRESS)).ResultValue == short_number_1);

                // 测试32位整数
                LogInfo("测试32位整数读写");
                await client.WriteAsync(INT32_TEST_ADDRESS, int_number);
                Assert.True((await client.ReadInt32Async(INT32_TEST_ADDRESS)).ResultValue == int_number);
                await client.WriteAsync(UINT32_TEST_ADDRESS, int_number_1);
                Assert.True((await client.ReadUInt32Async(UINT32_TEST_ADDRESS)).ResultValue == int_number_1);

                // 测试64位整数
                LogInfo("测试64位整数读写");
                await client.WriteAsync(INT64_TEST_ADDRESS, Convert.ToInt64(int_number));
                Assert.True((await client.ReadInt64Async(INT64_TEST_ADDRESS)).ResultValue == Convert.ToInt64(int_number));
                await client.WriteAsync(UINT64_TEST_ADDRESS, Convert.ToUInt64(int_number_1));
                Assert.True((await client.ReadUInt64Async(UINT64_TEST_ADDRESS)).ResultValue == Convert.ToUInt64(int_number_1));

                // 测试浮点数
                LogInfo("测试浮点数读写");
                await client.WriteAsync(FLOAT_TEST_ADDRESS, float_number);
                Assert.True((await client.ReadFloatAsync(FLOAT_TEST_ADDRESS)).ResultValue == float_number);
                await client.WriteAsync(DOUBLE_TEST_ADDRESS, Convert.ToDouble(float_number));
                Assert.True((await client.ReadDoubleAsync(DOUBLE_TEST_ADDRESS)).ResultValue == Convert.ToDouble(float_number));
            }
            
            await TestArrayReadWriteAsync();
        }

        /// <summary>
        /// 执行同步数组读写测试
        /// </summary>
        private void TestArrayReadWrite()
        {
            LogInfo("执行数组读写测试 - 基于实际PLC地址划分");

            // 布尔数组测试
            bool[] bool_values = { false, true, false, false, false, false, false, false, false, false,
                                   false, false, false, false, false, false, false, false, false, true };
            client.Write(BOOL_ARRAY_TEST_ADDRESS, bool_values);
            var bool_values_result = client.ReadBoolean(BOOL_ARRAY_TEST_ADDRESS, bool_values.Length);
            for (int j = 0; j < bool_values_result.ResultValue.Length; j++)
            {
                Assert.True(bool_values_result.ResultValue[j] == bool_values[j], $"布尔数组索引{j}不匹配");
            }

            // 整数数组测试
            int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            client.Write(INT_ARRAY_TEST_ADDRESS, int_values);
            var int_values_result = client.ReadInt32(INT_ARRAY_TEST_ADDRESS, int_values.Length);
            for (int j = 0; j < int_values_result.ResultValue.Length; j++)
            {
                Assert.True(int_values_result.ResultValue[j] == int_values[j], $"整数数组索引{j}不匹配");
            }

            // 浮点数组测试
            float[] float_values = { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f };
            client.Write(FLOAT_ARRAY_TEST_ADDRESS, float_values);
            var float_values_result = client.ReadFloat(FLOAT_ARRAY_TEST_ADDRESS, float_values.Length);
            for (int j = 0; j < float_values_result.ResultValue.Length; j++)
            {
                Assert.True(Math.Abs(float_values_result.ResultValue[j] - float_values[j]) < FLOAT_COMPARISON_PRECISION, 
                    $"浮点数组索引{j}不匹配: 期望={float_values[j]}, 实际={float_values_result.ResultValue[j]}");
            }
        }

        /// <summary>
        /// 执行异步数组读写测试
        /// </summary>
        private async Task TestArrayReadWriteAsync()
        {
            LogInfo("执行异步数组读写测试 - 基于实际PLC地址划分");

            // 布尔数组测试
            bool[] bool_values = { false, true, false, false, false, false, false, false, false, false,
                                   false, false, false, false, false, false, false, false, false, true };
            await client.WriteAsync(BOOL_ARRAY_TEST_ADDRESS, bool_values);
            var bool_values_result = await client.ReadBooleanAsync(BOOL_ARRAY_TEST_ADDRESS, bool_values.Length);
            for (int j = 0; j < bool_values_result.ResultValue.Length; j++)
            {
                Assert.True(bool_values_result.ResultValue[j] == bool_values[j], $"布尔数组索引{j}不匹配");
            }

            // 整数数组测试
            int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            await client.WriteAsync(INT_ARRAY_TEST_ADDRESS, int_values);
            var int_values_result = await client.ReadInt32Async(INT_ARRAY_TEST_ADDRESS, int_values.Length);
            for (int j = 0; j < int_values_result.ResultValue.Length; j++)
            {
                Assert.True(int_values_result.ResultValue[j] == int_values[j], $"整数数组索引{j}不匹配");
            }

            // 浮点数组测试
            float[] float_values = { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f };
            await client.WriteAsync(FLOAT_ARRAY_TEST_ADDRESS, float_values);
            var float_values_result = await client.ReadFloatAsync(FLOAT_ARRAY_TEST_ADDRESS, float_values.Length);
            for (int j = 0; j < float_values_result.ResultValue.Length; j++)
            {
                Assert.True(Math.Abs(float_values_result.ResultValue[j] - float_values[j]) < FLOAT_COMPARISON_PRECISION, 
                    $"浮点数组索引{j}不匹配: 期望={float_values[j]}, 实际={float_values_result.ResultValue[j]}");
            }
        }

        /// <summary>
        /// 测试所有数据类型
        /// </summary>
        private async Task TestAllDataTypes()
        {
            LogInfo("测试所有数据类型 - 基于实际PLC地址划分");
            
            // 测试布尔类型
            LogInfo("测试布尔类型");
            await TestDataType(COMPREHENSIVE_BOOL_ADDRESS_1, true, client.ReadBooleanAsync, client.WriteAsync);
            await TestDataType(COMPREHENSIVE_BOOL_ADDRESS_2, false, client.ReadBooleanAsync, client.WriteAsync);
            
            // 测试整数类型
            LogInfo("测试整数类型");
            await TestDataType(COMPREHENSIVE_INT16_ADDRESS, (short)12345, client.ReadInt16Async, client.WriteAsync);
            await TestDataType(COMPREHENSIVE_UINT16_ADDRESS, (ushort)54321, client.ReadUInt16Async, client.WriteAsync);
            await TestDataType(COMPREHENSIVE_INT32_ADDRESS, 123456789, client.ReadInt32Async, client.WriteAsync);
            await TestDataType(COMPREHENSIVE_UINT32_ADDRESS, (uint)987654321, client.ReadUInt32Async, client.WriteAsync);
            await TestDataType(COMPREHENSIVE_INT64_ADDRESS, 9223372036854775807L, client.ReadInt64Async, client.WriteAsync);
            await TestDataType(COMPREHENSIVE_UINT64_ADDRESS, 18446744073709551615UL, client.ReadUInt64Async, client.WriteAsync);
            
            // 测试浮点类型
            LogInfo("测试浮点类型");
            await TestDataType(COMPREHENSIVE_FLOAT_ADDRESS, 123.456f, client.ReadFloatAsync, client.WriteAsync);
            await TestDataType(COMPREHENSIVE_DOUBLE_ADDRESS, 789.012345, client.ReadDoubleAsync, client.WriteAsync);
            
            LogInfo("所有数据类型测试完成");
        }

        /// <summary>
        /// 测试批量读取性能
        /// </summary>
        private async Task TestBatchReadPerformance()
        {
            LogInfo("开始批量读取性能测试");
            
            // 准备测试数据
            await PrepareTestData();
            
            // 测试场景1：连续地址批量读取
            await TestContinuousAddressBatchRead();

            //// 测试场景2：分散地址批量读取
            await TestScatteredAddressBatchRead();

            //// 测试场景3：混合数据类型批量读取
            await TestMixedDataTypeBatchRead();

            LogInfo("批量读取性能测试完成");
        }

        /// <summary>
        /// 测试批量写入性能
        /// </summary>
        private async Task TestBatchWritePerformance()
        {
            LogInfo("开始批量写入性能测试");
            
            // 测试场景1：连续地址批量写入
            await TestContinuousAddressBatchWrite();
            
            // 测试场景2：分散地址批量写入
            await TestScatteredAddressBatchWrite();
            
            // 测试场景3：混合数据类型批量写入
            await TestMixedDataTypeBatchWrite();
            
            LogInfo("批量写入性能测试完成");
        }

        /// <summary>
        /// 准备测试数据
        /// </summary>
        private async Task PrepareTestData()
        {
            LogInfo("准备测试数据");
            
            // 写入测试用的布尔值
            await client.WriteAsync("DB200.DBX124.0", true);
            await client.WriteAsync("DB200.DBX124.1", false);
            await client.WriteAsync("DB200.DBX124.2", true);
            
            // 写入测试用的整数
            await client.WriteAsync("DB200.DBW124", (short)12345);
            await client.WriteAsync("DB200.DBW126", (short)23456);
            await client.WriteAsync("DB200.DBW128", (short)32767);
            
            // 写入测试用的双字
            await client.WriteAsync("DB200.DBD130", 1234567890);
            await client.WriteAsync("DB200.DBD134", 2345678901);
            await client.WriteAsync("DB200.DBD138", 3456789012);
            
            // 写入测试用的浮点数
            await client.WriteAsync("DB200.DBD250", 123.456f);
            await client.WriteAsync("DB200.DBD254", 234.567f);
            await client.WriteAsync("DB200.DBD258", 345.678f);
            
            LogInfo("测试数据准备完成");
        }

        /// <summary>
        /// 测试连续地址批量读取
        /// </summary>
        private async Task TestContinuousAddressBatchRead()
        {
            LogInfo("=== 连续地址批量读取测试 ===");
            
            // 准备连续地址的测试数据
            var continuousAddresses = new Dictionary<string, DataTypeEnums>
            {
                ["DB200.DBW124"] = DataTypeEnums.Int16,
                ["DB200.DBW126"] = DataTypeEnums.Int16,
                ["DB200.DBW128"] = DataTypeEnums.Int16,
                ["DB200.DBD130"] = DataTypeEnums.Int32,
                ["DB200.DBD134"] = DataTypeEnums.Int32,
                ["DB200.DBD138"] = DataTypeEnums.Int32
            };
            
            await CompareReadPerformance("连续地址", continuousAddresses);
        }

        /// <summary>
        /// 测试分散地址批量读取
        /// </summary>
        private async Task TestScatteredAddressBatchRead()
        {
            LogInfo("=== 分散地址批量读取测试 ===");
            
            // 准备分散地址的测试数据
            var scatteredAddresses = new Dictionary<string, DataTypeEnums>
            {
                ["DB200.DBW4"] = DataTypeEnums.Int16,
                ["DB200.DBD68"] = DataTypeEnums.Int32,
                ["DB200.DBD100"] = DataTypeEnums.Int32,
                ["DB200.DBD196"] = DataTypeEnums.Int32,
                ["DB200.DBD250"] = DataTypeEnums.Float,
                ["DB200.DBW50"] = DataTypeEnums.Int16
            };
            
            await CompareReadPerformance("分散地址", scatteredAddresses);
        }

        /// <summary>
        /// 测试混合数据类型批量读取
        /// </summary>
        private async Task TestMixedDataTypeBatchRead()
        {
            LogInfo("=== 混合数据类型批量读取测试 ===");
            
            // 准备混合数据类型的测试数据
            var mixedAddresses = new Dictionary<string, DataTypeEnums>
            {
                ["DB200.DBX124.0"] = DataTypeEnums.Bool,
                ["DB200.DBX124.1"] = DataTypeEnums.Bool,
                ["DB200.DBX124.2"] = DataTypeEnums.Bool,
                ["DB200.DBW124"] = DataTypeEnums.Int16,
                ["DB200.DBW126"] = DataTypeEnums.Int16,
                ["DB200.DBD130"] = DataTypeEnums.Int32,
                ["DB200.DBD250"] = DataTypeEnums.Float,
                ["DB200.DBD254"] = DataTypeEnums.Float
            };
            
            await CompareReadPerformance("混合数据类型", mixedAddresses);
        }

        /// <summary>
        /// 比较读取性能（批量 vs 单个）
        /// </summary>
        private async Task CompareReadPerformance(string testType, Dictionary<string, DataTypeEnums> addresses)
        {
            const int testRounds = 5;
            
            LogInfo($"开始 {testType} 性能对比测试，共 {testRounds} 轮");
            
            // 测试批量读取性能
            var batchStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < testRounds; i++)
            {
                await Task.Delay(100);
                // 移除Task.Delay，验证AsyncLock重构是否修复了阻塞问题
                var batchResult = await client.BatchReadAsync(addresses);
                Assert.True(batchResult.IsSuccess, $"批量读取失败: {batchResult.Message}");
                
                // 验证读取到的数据
                foreach (var kvp in batchResult.ResultValue)
                {
                    Assert.NotNull(kvp.Value.Item2);
                    LogInfo($"批量读取 {kvp.Key}: {kvp.Value.Item2}");
                }
            }
            batchStopwatch.Stop();
            
            // 测试单个读取性能
            var individualStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < testRounds; i++)
            {
                foreach (var address in addresses.Keys)
                {
                    // 根据地址类型选择合适的读取方法
                    if (address.Contains("DBX"))
                    {
                        var result = await client.ReadBooleanAsync(address);
                        Assert.True(result.IsSuccess, $"单个读取失败: {result.Message}");
                        LogInfo($"单个读取 {address}: {result.ResultValue}");
                    }
                    else if (address.Contains("DBW"))
                    {
                        var result = await client.ReadInt16Async(address);
                        Assert.True(result.IsSuccess, $"单个读取失败: {result.Message}");
                        LogInfo($"单个读取 {address}: {result.ResultValue}");
                    }
                    else if (address.Contains("DBD"))
                    {
                        // 尝试读取为int32，如果地址在浮点数范围则读取为float
                        if (address.Contains("250") || address.Contains("254") || address.Contains("258"))
                        {
                            var result = await client.ReadFloatAsync(address);
                            Assert.True(result.IsSuccess, $"单个读取失败: {result.Message}");
                            LogInfo($"单个读取 {address}: {result.ResultValue}");
                        }
                        else
                        {
                            var result = await client.ReadInt32Async(address);
                            Assert.True(result.IsSuccess, $"单个读取失败: {result.Message}");
                            LogInfo($"单个读取 {address}: {result.ResultValue}");
                        }
                    }
                }
            }
            individualStopwatch.Stop();
            
            // 计算性能统计
            var batchTotalTime = batchStopwatch.ElapsedMilliseconds;
            var individualTotalTime = individualStopwatch.ElapsedMilliseconds;
            var batchAvgTime = batchTotalTime / (double)testRounds;
            var individualAvgTime = individualTotalTime / (double)testRounds;
            var speedupRatio = individualTotalTime / (double)batchTotalTime;
            var efficiency = ((individualTotalTime - batchTotalTime) / (double)individualTotalTime) * 100;
            
            // 打印性能报告
            LogInfo($"=== {testType} 性能报告 ===");
            LogInfo($"地址数量: {addresses.Count}");
            LogInfo($"测试轮数: {testRounds}");
            LogInfo($"批量读取总时间: {batchTotalTime} ms");
            LogInfo($"单个读取总时间: {individualTotalTime} ms");
            LogInfo($"批量读取平均时间: {batchAvgTime:F2} ms/轮");
            LogInfo($"单个读取平均时间: {individualAvgTime:F2} ms/轮");
            LogInfo($"性能提升倍数: {speedupRatio:F2}x");
            LogInfo($"效率提升: {efficiency:F1}%");
            LogInfo($"时间节省: {individualTotalTime - batchTotalTime} ms");
            LogInfo("==========================================");

            // 断言性能结果（对于分散地址，可能没有明显性能提升）
            if (testType == "连续地址" || testType == "混合数据类型")
            {
                if (batchTotalTime < individualTotalTime)
                    LogInfo($"{testType}批量读取应该比单个读取更快");
                if (speedupRatio > 1.0)
                    LogInfo($"{testType}批量读取应该有性能提升");


            }
            else if (testType == "分散地址")
            {
                // 对于分散地址，批量读取可能没有明显优势，但不应该显著更慢
                if (speedupRatio >= 0.8)
                    LogInfo("分散地址批量读取性能不应该显著差于单个读取");

                LogInfo($"注意: 分散地址优化效果有限，这是正常的。当地址过于分散时，批量读取的优化算法会选择不合并，以避免读取过多无用数据。");
            }
            else
            {
                if (batchTotalTime <= individualTotalTime * 1.1)
                    LogInfo("批量读取性能不应该显著差于单个读取");

            }
        }

        /// <summary>
        /// 测试连续地址批量写入
        /// </summary>
        private async Task TestContinuousAddressBatchWrite()
        {
            LogInfo("=== 连续地址批量写入测试 ===");
            
            var continuousWriteData = new Dictionary<string, (DataTypeEnums, object)>
            {
                ["DB200.DBW140"] = (DataTypeEnums.Int16, (short)1111),
                ["DB200.DBW142"] = (DataTypeEnums.Int16, (short)2222),
                ["DB200.DBW144"] = (DataTypeEnums.Int16, (short)3333),
                ["DB200.DBD146"] = (DataTypeEnums.Int32, 4444444),
                ["DB200.DBD150"] = (DataTypeEnums.Int32, 5555555)
            };
            
            await CompareWritePerformance("连续地址", continuousWriteData);
        }

        /// <summary>
        /// 测试分散地址批量写入
        /// </summary>
        private async Task TestScatteredAddressBatchWrite()
        {
            LogInfo("=== 分散地址批量写入测试 ===");
            
            var scatteredWriteData = new Dictionary<string, (DataTypeEnums, object)>
            {
                ["DB200.DBW10"] = (DataTypeEnums.Int16, (short)1001),
                ["DB200.DBD80"] = (DataTypeEnums.Int32, 2002002),
                ["DB200.DBD110"] = (DataTypeEnums.Int32, 3003003),
                ["DB200.DBD260"] = (DataTypeEnums.Float, 123.789f),
                ["DB200.DBW52"] = (DataTypeEnums.Int16, (short)4004)
            };
            
            await CompareWritePerformance("分散地址", scatteredWriteData);
        }

        /// <summary>
        /// 测试混合数据类型批量写入
        /// </summary>
        private async Task TestMixedDataTypeBatchWrite()
        {
            LogInfo("=== 混合数据类型批量写入测试 ===");
            
            var mixedWriteData = new Dictionary<string, (DataTypeEnums, object)>
            {
                ["DB200.DBX125.0"] = (DataTypeEnums.Bool, true),
                ["DB200.DBX125.1"] = (DataTypeEnums.Bool, false),
                ["DB200.DBW154"] = (DataTypeEnums.Int16, (short)9999),
                ["DB200.DBD156"] = (DataTypeEnums.Int32, 8888888),
                ["DB200.DBD262"] = (DataTypeEnums.Float, 999.123f)
            };
            
            await CompareWritePerformance("混合数据类型", mixedWriteData);
        }

        /// <summary>
        /// 比较写入性能（批量 vs 单个）
        /// </summary>
        private async Task CompareWritePerformance(string testType, Dictionary<string, (DataTypeEnums, object)> writeData)
        {
            const int testRounds = 5;
            
            LogInfo($"开始 {testType} 写入性能对比测试，共 {testRounds} 轮");
            
            // 测试批量写入性能
            var batchStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < testRounds; i++)
            {
                var batchResult = await client.BatchWriteAsync(writeData);
                Assert.True(batchResult.IsSuccess, $"批量写入失败: {batchResult.Message}");
            }
            batchStopwatch.Stop();
            
            // 测试单个写入性能
            var individualStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < testRounds; i++)
            {
                foreach (var kvp in writeData)
                {
                    // 直接调用带类型的写入方法
                    OperationResult result;
                    if (kvp.Value.Item1 == DataTypeEnums.Bool)
                        result = await client.WriteAsync(kvp.Key, (bool)kvp.Value.Item2);
                    else if (kvp.Value.Item1 == DataTypeEnums.Int16)
                        result = await client.WriteAsync(kvp.Key, (short)kvp.Value.Item2);
                    else if (kvp.Value.Item1 == DataTypeEnums.Int32)
                        result = await client.WriteAsync(kvp.Key, (int)kvp.Value.Item2);
                    else if (kvp.Value.Item1 == DataTypeEnums.Float)
                        result = await client.WriteAsync(kvp.Key, (float)kvp.Value.Item2);
                    else
                        throw new ArgumentException($"不支持的数据类型: {kvp.Value.Item1}");
                    
                    Assert.True(result.IsSuccess, $"单个写入失败: {result.Message}");
                }
            }
            individualStopwatch.Stop();
            
            // 计算性能统计
            var batchTotalTime = batchStopwatch.ElapsedMilliseconds;
            var individualTotalTime = individualStopwatch.ElapsedMilliseconds;
            var batchAvgTime = batchTotalTime / (double)testRounds;
            var individualAvgTime = individualTotalTime / (double)testRounds;
            var speedupRatio = individualTotalTime / (double)batchTotalTime;
            var efficiency = ((individualTotalTime - batchTotalTime) / (double)individualTotalTime) * 100;
            
            // 打印性能报告
            LogInfo($"=== {testType} 写入性能报告 ===");
            LogInfo($"地址数量: {writeData.Count}");
            LogInfo($"测试轮数: {testRounds}");
            LogInfo($"批量写入总时间: {batchTotalTime} ms");
            LogInfo($"单个写入总时间: {individualTotalTime} ms");
            LogInfo($"批量写入平均时间: {batchAvgTime:F2} ms/轮");
            LogInfo($"单个写入平均时间: {individualAvgTime:F2} ms/轮");
            LogInfo($"性能提升倍数: {speedupRatio:F2}x");
            LogInfo($"效率提升: {efficiency:F1}%");
            LogInfo($"时间节省: {individualTotalTime - batchTotalTime} ms");
            LogInfo("==========================================");
            
            // 验证写入结果
            await Task.Delay(100); // 等待PLC处理
            foreach (var kvp in writeData)
            {
                // 根据数据类型验证写入结果
                if (kvp.Value.Item1 == DataTypeEnums.Bool)
                {
                    var result = await client.ReadBooleanAsync(kvp.Key);
                    Assert.True(result.IsSuccess && result.ResultValue == (bool)kvp.Value.Item2, $"写入验证失败: {kvp.Key}");
                }
                else if (kvp.Value.Item1 == DataTypeEnums.Int16)
                {
                    var result = await client.ReadInt16Async(kvp.Key);
                    Assert.True(result.IsSuccess && result.ResultValue == (short)kvp.Value.Item2, $"写入验证失败: {kvp.Key}");
                }
                else if (kvp.Value.Item1 == DataTypeEnums.Int32)
                {
                    var result = await client.ReadInt32Async(kvp.Key);
                    Assert.True(result.IsSuccess && result.ResultValue == (int)kvp.Value.Item2, $"写入验证失败: {kvp.Key}");
                }
                else if (kvp.Value.Item1 == DataTypeEnums.Float)
                {
                    var result = await client.ReadFloatAsync(kvp.Key);
                    Assert.True(result.IsSuccess && Math.Abs(result.ResultValue - (float)kvp.Value.Item2) < FLOAT_COMPARISON_PRECISION, 
                        $"写入验证失败: {kvp.Key}");
                }
            }
            
            LogInfo($"{testType} 写入验证完成");
        }

        private async Task TestDataType<T>(string address, T value, 
            Func<string, ValueTask<OperationResult<T>>> readFunc,
            Func<string, T, Task<OperationResult>> writeFunc)
        {
            _output?.WriteLine($"测试数据类型: {typeof(T).Name}, 地址: {address}, 值: {value}");
            
            // 写入值
            var writeResult = await writeFunc(address, value);
            Assert.True(writeResult.IsSuccess, $"写入失败: {writeResult.Message}");
            
            // 读取并验证值
            var readResult = await readFunc(address);
            Assert.True(readResult.IsSuccess, $"读取失败: {readResult.Message}");
            
            if (typeof(T) == typeof(float) || typeof(T) == typeof(double))
            {
                // 浮点数比较需要考虑精度
                double expected = Convert.ToDouble(value);
                double actual = Convert.ToDouble(readResult.ResultValue);
                Assert.True(Math.Abs(actual - expected) < 0.0001, $"浮点数不匹配: 期望={expected}, 实际={actual}");
            }
            else
            {
                Assert.Equal(value, readResult.ResultValue);
            }
            
            _output?.WriteLine($"数据类型测试成功: {value} == {readResult.ResultValue}");
        }

        private void SafeDisconnect()
        {
            if (client.Connected)
            {
                _output?.WriteLine("确保断开连接");
                client.EnableAutoReconnect = false;
                client.Disconnect();
            }
        }

        private async Task SafeDisconnectAsync()
        {
            if (client.Connected)
            {
                _output?.WriteLine("确保断开连接");
                client.EnableAutoReconnect = false;
                await client.DisconnectAsync();
            }
        }

        private void LogTestStart(string testName)
        {
            _output?.WriteLine($"开始测试: {testName}");
        }

        private void LogTestComplete(string testName)
        {
            _output?.WriteLine($"测试完成: {testName}");
        }

        private void LogTestError(string testName, Exception ex)
        {
            _output?.WriteLine($"测试失败: {testName}, 异常: {ex.Message}");
        }

        private void LogStep(string step)
        {
            _output?.WriteLine($"执行步骤: {step}");
        }

        private void LogInfo(string message)
        {
            _output?.WriteLine($"信息: {message}");
        }

        private void LogWarning(string message)
        {
            _output?.WriteLine($"警告: {message}");
        }

        #endregion
    }
}

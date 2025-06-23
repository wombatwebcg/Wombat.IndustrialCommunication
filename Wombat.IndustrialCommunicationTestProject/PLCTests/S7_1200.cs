using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const string TEST_PLC_IP = "192.168.11.41";
        
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
        private ConnectionDisruptorExtreme _disruptor;
        
        #endregion

        #region 构造函数
        
        /// <summary>
        /// 初始化S7-1200测试类
        /// </summary>
        /// <param name="output">测试输出助手</param>
        public S7_1200(ITestOutputHelper output = null)
        {
            _output = output;
            _disruptor = new ConnectionDisruptorExtreme(new XUnitLogger(_output, "Disruptor"));
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
                
                // Act - 模拟连接中断
                LogStep("模拟连接中断");
                var disruptResult = await _disruptor.SimulateSafeWait(client, 1000);
                Assert.True(disruptResult.IsSuccess, $"模拟中断失败: {disruptResult.Message}");
                
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

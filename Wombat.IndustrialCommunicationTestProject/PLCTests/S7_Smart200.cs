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
using System.Linq;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    /// <summary>
    /// S7-200 Smart PLC通讯测试类
    /// 测试包括：同步/异步读写、断线重连、短连接、模拟服务器、压力测试
    /// </summary>
    public class S7_Smart200
    {
        #region 测试配置常量
        
        /// <summary>测试PLC IP地址</summary>
        private const string TEST_PLC_IP = "192.168.11.51";
        
        /// <summary>测试PLC端口</summary>
        private const int TEST_PLC_PORT = 102;
        
        /// <summary>PLC版本</summary>
        private const SiemensVersion PLC_VERSION = SiemensVersion.S7_200Smart;
        
        /// <summary>基础测试循环次数</summary>
        private const int BASIC_TEST_CYCLES = 50;
        
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
        
        /// <summary>模拟服务器IP地址</summary>
        private const string MOCK_SERVER_IP = "127.0.0.1";
        
        /// <summary>模拟服务器端口</summary>
        private const int MOCK_SERVER_PORT = 1102;
        
        #endregion

        #region 测试地址常量
        
        /// <summary>布尔测试地址</summary>
        private const string BOOL_TEST_ADDRESS_1 = "Q1.3";
        private const string BOOL_TEST_ADDRESS_2 = "Q1.4";
        private const string BOOL_TEST_ADDRESS_3 = "Q1.5";
        
        /// <summary>基础数据测试地址 - 使用双字地址支持32位数据</summary>
        private const string BASIC_DATA_ADDRESS = "VD700";
        private const string STRING_DATA_ADDRESS = "V1000";
        
        /// <summary>连接测试地址 - 使用双字地址</summary>
        private const string CONNECTION_TEST_ADDRESS = "VD700";
        
        /// <summary>不同数据类型的测试地址</summary>
        private const string BYTE_TEST_ADDRESS = "V700";      // 字节地址（用于布尔数组）
        private const string WORD_TEST_ADDRESS = "VW700";      // 字地址（16位）
        private const string DWORD_TEST_ADDRESS = "VD700";     // 双字地址（32位）
        private const string BOOL_BIT_TEST_ADDRESS = "V700.0"; // 位地址
        
        #endregion

        #region 私有字段
        
        private SiemensClient client;
        private readonly ITestOutputHelper _output;

        
        #endregion

        #region 构造函数
        
        /// <summary>
        /// 初始化S7-200 Smart测试类
        /// </summary>
        /// <param name="output">测试输出助手</param>
        public S7_Smart200(ITestOutputHelper output = null)
        {
            _output = output;

        }
        
        #endregion

        #region 公共测试方法

        /// <summary>
        /// 测试S7-200 Smart同步读写功能
        /// 包括各种数据类型的基本读写操作
        /// </summary>
        [Fact]
        public void Test_Smart200_SyncReadWrite()
        {
            // Arrange
            var testName = "S7-200 Smart同步读写测试";
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
        /// 测试S7-200 Smart异步读写功能
        /// 包括各种数据类型的异步读写操作
        /// </summary>
        [Fact]
        public async Task Test_Smart200_AsyncReadWrite()
        {
            // Arrange
            var testName = "S7-200 Smart异步读写测试";
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
        /// 测试S7-200 Smart断线重连功能
        /// 验证自动重连机制的可靠性
        /// </summary>
        [Fact]
        public async Task Test_Smart200_DisconnectionRecovery()
        {
            // Arrange
            var testName = "S7-200 Smart断线重连测试";
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
                short testValue = 12345;
                var writeResult = await client.WriteAsync(WORD_TEST_ADDRESS, testValue);
                Assert.True(writeResult.IsSuccess, $"写入失败: {writeResult.Message}");
                var readResult = await client.ReadInt16Async(WORD_TEST_ADDRESS);
                Assert.True(readResult.IsSuccess, $"读取失败: {readResult.Message}");
                Assert.Equal(testValue, readResult.ResultValue);
                

                
                // Act - 等待自动重连
                LogStep($"等待自动重连（最长{WAIT_RECONNECT_TIMEOUT}ms）");
                await Task.Delay(WAIT_RECONNECT_TIMEOUT);
                
                // Assert - 验证重连恢复
                LogStep("验证连接恢复");
                LogInfo($"当前连接状态: {(client.Connected ? "已连接" : "未连接")}");
                
                short recoveryTestValue = 22222;
                writeResult = await client.WriteAsync(WORD_TEST_ADDRESS, recoveryTestValue);
                if (writeResult.IsSuccess)
                {
                    LogInfo("写入成功，连接已恢复");
                    readResult = await client.ReadInt16Async(WORD_TEST_ADDRESS);
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
        /// 测试S7-200 Smart短连接功能
        /// 验证短连接模式下的读写操作
        /// </summary>
        [Fact]
        public async Task Test_Smart200_ShortConnection()
        {
            // Arrange
            var testName = "S7-200 Smart短连接测试";
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
                short testIntValue = 12345;
                var writeIntResult = await client.WriteAsync(WORD_TEST_ADDRESS, testIntValue);
                Assert.True(writeIntResult.IsSuccess, $"写入整数失败: {writeIntResult.Message}");
                var readIntResult = await client.ReadInt16Async(WORD_TEST_ADDRESS);
                Assert.True(readIntResult.IsSuccess, $"读取整数失败: {readIntResult.Message}");
                Assert.Equal(testIntValue, readIntResult.ResultValue);
                
                // Assert - 验证短连接状态
                LogInfo($"操作后连接状态: {(client.Connected ? "已连接" : "未连接")}");
                Assert.False(client.Connected, "短连接模式下操作后应已断开");
                
                // Act & Assert - 测试浮点数读写
                LogStep("测试短连接浮点数读写");
                var testFloatValue = 123.45f;
                var writeFloatResult = await client.WriteAsync(DWORD_TEST_ADDRESS, testFloatValue);
                Assert.True(writeFloatResult.IsSuccess, $"写入浮点数失败: {writeFloatResult.Message}");
                var readFloatResult = await client.ReadFloatAsync(DWORD_TEST_ADDRESS);
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
                var writeArrayResult = await client.WriteAsync(DWORD_TEST_ADDRESS, testIntArray);
                Assert.True(writeArrayResult.IsSuccess, $"写入数组失败: {writeArrayResult.Message}");
                var readArrayResult = await client.ReadInt32Async(DWORD_TEST_ADDRESS, testIntArray.Length);
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
        /// 测试S7-200 Smart批量读取功能
        /// 验证批量读取优化算法的性能和正确性
        /// </summary>
        [Fact]
        public async Task Test_Smart200_BatchRead()
        {
            // Arrange
            var testName = "S7-200 Smart批量读取测试";
            LogTestStart(testName);
            
            client = new SiemensClient(TEST_PLC_IP, TEST_PLC_PORT, PLC_VERSION);
            
            try
            {
                // Act & Assert
                LogStep("建立PLC连接");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                
                LogStep("执行S7-200 Smart批量读取测试");
                await TestSmart200BatchReadPerformance();
                
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
        /// 测试S7-200 Smart批量写入功能
        /// 验证批量写入功能的性能和正确性
        /// </summary>
        [Fact]
        public async Task Test_Smart200_BatchWrite()
        {
            // Arrange
            var testName = "S7-200 Smart批量写入测试";
            LogTestStart(testName);
            
            client = new SiemensClient(TEST_PLC_IP, TEST_PLC_PORT, PLC_VERSION);
            
            try
            {
                // Act & Assert
                LogStep("建立PLC连接");
                var connectResult = await client.ConnectAsync();
                Assert.True(connectResult.IsSuccess, $"连接失败: {connectResult.Message}");
                
                LogStep("执行S7-200 Smart批量写入测试");
                await TestSmart200BatchWritePerformance();
                
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
            LogInfo("执行基于VD700和Q区的同步读写测试");
            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            for (int i = 0; i < BASIC_TEST_CYCLES; i++)
            {
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                ushort short_number_1 = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);

                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                uint int_number_1 = (uint)rnd.Next(0, int.MaxValue);

                float float_number = int_number / 100;
                var bool_value = short_number % 2 == 1;

                string value_string = "BennyZhao";


                // 测试布尔值读写
                LogInfo("测试布尔值读写");
                client.Write(BOOL_TEST_ADDRESS_1, true);
                Assert.True(client.ReadBoolean(BOOL_TEST_ADDRESS_1).ResultValue == true, "布尔值应为true");
                client.Write(BOOL_TEST_ADDRESS_2, bool_value);
                Assert.True(client.ReadBoolean(BOOL_TEST_ADDRESS_2).ResultValue == bool_value, $"布尔值应为{bool_value}");
                client.Write(BOOL_TEST_ADDRESS_3, !bool_value);
                Assert.True(client.ReadBoolean(BOOL_TEST_ADDRESS_3).ResultValue == !bool_value, $"布尔值应为{!bool_value}");

                // 测试16位整数读写 - 使用字地址
                client.Write(WORD_TEST_ADDRESS, short_number);
                Assert.True(client.ReadInt16(WORD_TEST_ADDRESS).ResultValue == short_number);
                client.Write(WORD_TEST_ADDRESS, short_number_1);
                Assert.True(client.ReadUInt16(WORD_TEST_ADDRESS).ResultValue == short_number_1);

                // 测试32位整数读写 - 使用双字地址
                client.Write(DWORD_TEST_ADDRESS, int_number);
                Assert.True(client.ReadInt32(DWORD_TEST_ADDRESS).ResultValue == int_number);
                client.Write(DWORD_TEST_ADDRESS, int_number_1);
                Assert.True(client.ReadUInt32(DWORD_TEST_ADDRESS).ResultValue == int_number_1);

                // 测试64位整数读写 - 使用双字地址（占用8字节）
                client.Write(DWORD_TEST_ADDRESS, Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64(DWORD_TEST_ADDRESS).ResultValue == Convert.ToInt64(int_number));
                client.Write(DWORD_TEST_ADDRESS, Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64(DWORD_TEST_ADDRESS).ResultValue == Convert.ToUInt64(int_number_1));

                // 测试浮点数读写 - 使用双字地址
                client.Write(DWORD_TEST_ADDRESS, float_number);
                Assert.True(client.ReadFloat(DWORD_TEST_ADDRESS).ResultValue == float_number);
                client.Write(DWORD_TEST_ADDRESS, Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble(DWORD_TEST_ADDRESS).ResultValue == Convert.ToDouble(float_number));

                //var rrr =  client.Write("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).ResultValue == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                client.Write(BYTE_TEST_ADDRESS, bool_values);
                
                var bool_values_result = client.ReadBoolean(BYTE_TEST_ADDRESS, bool_values.Length);
                for (int j = 0; j < bool_values_result.ResultValue.Length; j++)
                {
                    Assert.True(bool_values_result.ResultValue[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write(WORD_TEST_ADDRESS, short_values);
                var short_values_result = client.ReadInt16(WORD_TEST_ADDRESS, short_values.Length);
                for (int j = 0; j < short_values_result.ResultValue.Length; j++)
                {
                    Assert.True(short_values_result.ResultValue[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write(WORD_TEST_ADDRESS, ushort_values);
                var ushort_values_result = client.ReadInt16(WORD_TEST_ADDRESS, ushort_values.Length);
                for (int j = 0; j < ushort_values_result.ResultValue.Length; j++)
                {
                    Assert.True(ushort_values_result.ResultValue[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write(DWORD_TEST_ADDRESS, int_values);
                var int_values_result = client.ReadInt32(DWORD_TEST_ADDRESS, int_values.Length);
                for (int j = 0; j < int_values_result.ResultValue.Length; j++)
                {
                    Assert.True(int_values_result.ResultValue[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write(DWORD_TEST_ADDRESS, uint_values);
                var uint_values_result = client.ReadUInt32(DWORD_TEST_ADDRESS, uint_values.Length);
                for (int j = 0; j < uint_values_result.ResultValue.Length; j++)
                {
                    Assert.True(uint_values_result.ResultValue[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write(DWORD_TEST_ADDRESS, long_values);
                var long_values_result = client.ReadInt64(DWORD_TEST_ADDRESS, long_values.Length);
                for (long j = 0; j < long_values_result.ResultValue.Length; j++)
                {
                    Assert.True(long_values_result.ResultValue[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write(DWORD_TEST_ADDRESS, ulong_values);
                var ulong_values_result = client.ReadUInt64(DWORD_TEST_ADDRESS, ulong_values.Length);
                for (int j = 0; j < ulong_values_result.ResultValue.Length; j++)
                {
                    Assert.True(ulong_values_result.ResultValue[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write(DWORD_TEST_ADDRESS, float_values);
                var float_values_result = client.ReadFloat(DWORD_TEST_ADDRESS, float_values.Length);
                for (int j = 0; j < float_values_result.ResultValue.Length; j++)
                {
                    Assert.True(float_values_result.ResultValue[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write(DWORD_TEST_ADDRESS, double_values);
                var double_values_result = client.ReadDouble(DWORD_TEST_ADDRESS, double_values.Length);
                for (int j = 0; j < double_values_result.ResultValue.Length; j++)
                {
                    Assert.True(double_values_result.ResultValue[j] == double_values[j]);

                }



            }

        }


        private async Task ReadWriteAsync()
        {

            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            for (int i = 0; i < 50; i++)
            {
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                ushort short_number_1 = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);

                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                uint int_number_1 = (uint)rnd.Next(0, int.MaxValue);

                float float_number = int_number / 100;
                var bool_value = short_number % 2 == 1;

                string value_string = "BennyZhao";

                await client.WriteAsync("Q1.3", true);
                Assert.True(client.ReadBooleanAsync("Q1.3").Result.ResultValue == true);
                await client.WriteAsync("Q1.4", bool_value);
                Assert.True(client.ReadBooleanAsync("Q1.4").Result.ResultValue == bool_value);
                await client.WriteAsync("Q1.5", !bool_value);
                Assert.True(client.ReadBooleanAsync("Q1.5").Result.ResultValue == !bool_value);

                // 测试16位整数读写 - 使用字地址
                var ssss = await client.WriteAsync(WORD_TEST_ADDRESS, short_number);
                var tttt = client.ReadInt16Async(WORD_TEST_ADDRESS);
                Assert.True(client.ReadInt16Async(WORD_TEST_ADDRESS).Result.ResultValue == short_number);
                await client.WriteAsync(WORD_TEST_ADDRESS, short_number_1);
                Assert.True(client.ReadUInt16Async(WORD_TEST_ADDRESS).Result.ResultValue == short_number_1);

                // 测试32位整数读写 - 使用双字地址
                await client.WriteAsync(DWORD_TEST_ADDRESS, int_number);
                Assert.True(client.ReadInt32Async(DWORD_TEST_ADDRESS).Result.ResultValue == int_number);
                await client.WriteAsync(DWORD_TEST_ADDRESS, int_number_1);
                Assert.True(client.ReadUInt32Async(DWORD_TEST_ADDRESS).Result.ResultValue == int_number_1);

                // 测试64位整数读写 - 使用双字地址（占用8字节）
                await client.WriteAsync(DWORD_TEST_ADDRESS, Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64Async(DWORD_TEST_ADDRESS).Result.ResultValue == Convert.ToInt64(int_number));
                await client.WriteAsync(DWORD_TEST_ADDRESS, Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64Async(DWORD_TEST_ADDRESS).Result.ResultValue == Convert.ToUInt64(int_number_1));

                // 测试浮点数读写 - 使用双字地址
                await client.WriteAsync(DWORD_TEST_ADDRESS, float_number);
                Assert.True(client.ReadFloatAsync(DWORD_TEST_ADDRESS).Result.ResultValue == float_number);
                await client.WriteAsync(DWORD_TEST_ADDRESS, Convert.ToDouble(float_number));
                Assert.True(client.ReadDoubleAsync(DWORD_TEST_ADDRESS).Result.ResultValue == Convert.ToDouble(float_number));

                //var rrr =  await client.WriteAsync("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).ResultValue == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                await client.WriteAsync(BYTE_TEST_ADDRESS, bool_values);
                var bool_values_result = client.ReadBooleanAsync(BYTE_TEST_ADDRESS, bool_values.Length);
                for (int j = 0; j < bool_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(bool_values_result.Result.ResultValue[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync(WORD_TEST_ADDRESS, short_values);
                var short_values_result = client.ReadInt16Async(WORD_TEST_ADDRESS, short_values.Length);
                for (int j = 0; j < short_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(short_values_result.Result.ResultValue[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync(WORD_TEST_ADDRESS, ushort_values);
                var ushort_values_result = client.ReadInt16Async(WORD_TEST_ADDRESS, ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(ushort_values_result.Result.ResultValue[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync(DWORD_TEST_ADDRESS, int_values);
                var int_values_result = client.ReadInt32Async(DWORD_TEST_ADDRESS, int_values.Length);
                for (int j = 0; j < int_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(int_values_result.Result.ResultValue[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync(DWORD_TEST_ADDRESS, uint_values);
                var uint_values_result = client.ReadUInt32Async(DWORD_TEST_ADDRESS, uint_values.Length);
                for (int j = 0; j < uint_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(uint_values_result.Result.ResultValue[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync(DWORD_TEST_ADDRESS, long_values);
                var long_values_result = client.ReadInt64Async(DWORD_TEST_ADDRESS, long_values.Length);
                for (long j = 0; j < long_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(long_values_result.Result.ResultValue[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync(DWORD_TEST_ADDRESS, ulong_values);
                var ulong_values_result = client.ReadUInt64Async(DWORD_TEST_ADDRESS, ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(ulong_values_result.Result.ResultValue[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync(DWORD_TEST_ADDRESS, float_values);
                var float_values_result = client.ReadFloatAsync(DWORD_TEST_ADDRESS, float_values.Length);
                for (int j = 0; j < float_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(float_values_result.Result.ResultValue[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync(DWORD_TEST_ADDRESS, double_values);
                var double_values_result = client.ReadDoubleAsync(DWORD_TEST_ADDRESS, double_values.Length);
                for (int j = 0; j < double_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(double_values_result.Result.ResultValue[j] == double_values[j]);

                }



            }

        }

        /// <summary>
        /// 测试S7-200 Smart批量读取性能
        /// </summary>
        private async Task TestSmart200BatchReadPerformance()
        {
            LogInfo("开始S7-200 Smart批量读取性能测试");
            
            // 准备测试数据
            await PrepareSmart200TestData();
            
            // 测试场景1：V区连续地址批量读取
            await TestSmart200ContinuousAddressBatchRead();
            
            // 测试场景2：V区分散地址批量读取
            await TestSmart200ScatteredAddressBatchRead();
            
            // 测试场景3：Q区和V区混合批量读取
            await TestSmart200MixedAreaBatchRead();
            
            // 测试场景4：V区布尔量批量读取
            await TestSmart200VBoolBatchRead();
            
            LogInfo("S7-200 Smart批量读取性能测试完成");
        }

        /// <summary>
        /// 测试S7-200 Smart批量写入功能
        /// 验证批量写入功能的性能和正确性
        /// </summary>
        private async Task TestSmart200BatchWritePerformance()
        {
            LogInfo("开始S7-200 Smart批量写入性能测试");
            
            // 测试场景1：V区连续地址批量写入
            await TestSmart200ContinuousAddressBatchWrite();
            
            // 测试场景2：V区分散地址批量写入
            await TestSmart200ScatteredAddressBatchWrite();
            
            // 测试场景3：Q区和V区混合批量写入
            await TestSmart200MixedAreaBatchWrite();
            
            // 测试场景4：V区布尔量批量写入
            await TestSmart200VBoolBatchWrite();
            
            LogInfo("S7-200 Smart批量写入性能测试完成");
        }

        /// <summary>
        /// 准备S7-200 Smart测试数据
        /// </summary>
        private async Task PrepareSmart200TestData()
        {
            LogInfo("准备S7-200 Smart测试数据");
            
            // 准备Q区布尔值数据 - 使用Q1.0到Q12.7 (96个布尔值)
            LogInfo("准备Q区布尔值数据");
            for (int i = 0; i < 96; i++)
            {
                var boolValue = i % 2 == 0;
                await client.WriteAsync($"Q{i / 8 + 1}.{i % 8}", boolValue);
            }
            
            // 准备V区布尔值数据 - 使用V0.0到V11.7 (96个布尔值)
            LogInfo("准备V区布尔值数据");
            for (int i = 0; i < 96; i++)
            {
                var boolValue = i % 3 == 0; // 使用不同的模式与Q区区分
                await client.WriteAsync($"V{i / 8}.{i % 8}", boolValue);
            }
            
            // 准备V区Word数据 - 使用VW700到VW898 (100个Word)
            LogInfo("准备V区Word数据");
            for (int i = 0; i < 100; i++)
            {
                var wordValue = (short)(1000 + i * 10);
                await client.WriteAsync($"VW{700 + i * 2}", wordValue);
            }
            
            // 准备V区DWord数据 - 使用VD900到VD1296 (100个DWord)
            LogInfo("准备V区DWord数据");
            for (int i = 0; i < 100; i++)
            {
                var dwordValue = 1000000 + i * 10000;
                await client.WriteAsync($"VD{900 + i * 4}", dwordValue);
            }
            
            // 准备V区Real数据 - 使用VD1300到VD1696 (100个Real)
            LogInfo("准备V区Real数据");
            for (int i = 0; i < 100; i++)
            {
                var realValue = 100.0f + i * 1.5f;
                await client.WriteAsync($"VD{1300 + i * 4}", realValue);
            }
            
            // 准备分散地址测试数据 - VW1000到VW1098 (间隔2字节)
            LogInfo("准备分散Word地址数据");
            for (int i = 0; i < 50; i++)
            {
                var wordValue = (short)(2000 + i * 20);
                await client.WriteAsync($"VW{1000 + i * 2}", wordValue);
            }
            
            // 准备分散地址测试数据 - VD1100到VD1296 (间隔4字节)
            LogInfo("准备分散DWord地址数据");
            for (int i = 0; i < 50; i++)
            {
                var dwordValue = 2000000 + i * 20000;
                await client.WriteAsync($"VD{1100 + i * 4}", dwordValue);
            }
            
            LogInfo("S7-200 Smart测试数据准备完成");
        }

        /// <summary>
        /// 测试V区连续地址批量读取
        /// </summary>
        private async Task TestSmart200ContinuousAddressBatchRead()
        {
            LogInfo("=== V区连续地址批量读取测试 (100个连续Word地址) ===");
            
            // 准备连续V区地址的测试数据 - 使用100个连续的Word地址
            var continuousAddresses = new Dictionary<string, object>();
            for (int i = 0; i < 100; i++)
            {
                continuousAddresses[$"VW{700 + i * 2}"] = null;
            }
            
            LogInfo($"准备测试 {continuousAddresses.Count} 个连续Word地址");
            LogInfo($"地址范围: VW700 到 VW898 (跨度200字节)");
            
            await CompareSmart200ReadPerformance("V区连续地址(100个Word)", continuousAddresses);
        }

        /// <summary>
        /// 测试V区分散地址批量读取
        /// </summary>
        private async Task TestSmart200ScatteredAddressBatchRead()
        {
            LogInfo("=== V区分散地址批量读取测试 (100个分散地址) ===");
            
            // 准备分散V区地址的测试数据 - 使用100个分散的地址，进一步优化间隔
            var scatteredAddresses = new Dictionary<string, object>();
            
            // 添加50个分散的Word地址 - 使用2字节间隔，提高合并效率
            for (int i = 0; i < 50; i++)
            {
                scatteredAddresses[$"VW{1000 + i * 2}"] = null;
            }
            
            // 添加50个分散的DWord地址 - 使用4字节间隔，提高合并效率
            for (int i = 0; i < 50; i++)
            {
                scatteredAddresses[$"VD{1100 + i * 4}"] = null;
            }
            
            LogInfo($"准备测试 {scatteredAddresses.Count} 个分散地址");
            LogInfo($"分布: 50个Word地址(间隔2字节) + 50个DWord地址(间隔4字节)");
            LogInfo($"地址范围: VW1000-VW1098, VD1100-VD1296");
            
            await CompareSmart200ReadPerformance("V区分散地址(100个混合)", scatteredAddresses);
        }

        /// <summary>
        /// 测试Q区和V区混合批量读取
        /// </summary>
        private async Task TestSmart200MixedAreaBatchRead()
        {
            LogInfo("=== Q区和V区混合批量读取测试 (120个混合类型地址) ===");
            
            // 准备混合区域的测试数据 - 使用120个混合类型地址
            var mixedAddresses = new Dictionary<string, object>();
            
            // Q区：24个布尔地址 (Q1.0到Q3.7)
            for (int i = 0; i < 24; i++)
            {
                mixedAddresses[$"Q{i / 8 + 1}.{i % 8}"] = null;
            }
            
            // V区：24个布尔地址 (V0.0到V2.7)
            for (int i = 0; i < 24; i++)
            {
                mixedAddresses[$"V{i / 8}.{i % 8}"] = null;
            }
            
            // V区：24个Word地址
            for (int i = 0; i < 24; i++)
            {
                mixedAddresses[$"VW{700 + i * 2}"] = null;
            }
            
            // V区：24个DWord地址
            for (int i = 0; i < 24; i++)
            {
                mixedAddresses[$"VD{900 + i * 4}"] = null;
            }
            
            // V区：24个Real地址
            for (int i = 0; i < 24; i++)
            {
                mixedAddresses[$"VD{1300 + i * 4}"] = null;
            }
            
            LogInfo($"准备测试 {mixedAddresses.Count} 个混合类型地址");
            LogInfo($"分布: Q区(24个Bool) + V区(24个Bool) + V区(24个Word) + V区(24个DWord) + V区(24个Real)");
            
            await CompareSmart200ReadPerformance("Q区和V区混合(120个混合)", mixedAddresses);
        }

        /// <summary>
        /// 测试V区布尔量批量读取
        /// </summary>
        private async Task TestSmart200VBoolBatchRead()
        {
            LogInfo("=== V区布尔量批量读取测试 (96个V区布尔地址) ===");
            
            // 准备V区布尔量地址的测试数据 - 使用96个V区布尔地址
            var vBoolAddresses = new Dictionary<string, object>();
            
            // V区：96个布尔地址 (V0.0到V11.7)
            for (int i = 0; i < 96; i++)
            {
                vBoolAddresses[$"V{i / 8}.{i % 8}"] = null;
            }
            
            LogInfo($"准备测试 {vBoolAddresses.Count} 个V区布尔地址");
            LogInfo($"地址范围: V0.0 到 V11.7 (96个布尔值)");
            
            await CompareSmart200ReadPerformance("V区布尔量(96个Bool)", vBoolAddresses);
        }

        /// <summary>
        /// 比较S7-200 Smart读取性能（批量 vs 单个）
        /// </summary>
        private async Task CompareSmart200ReadPerformance(string testType, Dictionary<string, object> addresses)
        {
            const int testRounds = 10;
            
            LogInfo($"开始 {testType} 性能对比测试，共 {testRounds} 轮");
            
            // 转换地址字典为新的格式
            var newAddresses = new Dictionary<string, DataTypeEnums>();
            foreach (var kvp in addresses)
            {
                // 根据地址类型推断数据类型
                if (kvp.Key.StartsWith("Q") && kvp.Key.Contains("."))
                {
                    newAddresses[kvp.Key] = DataTypeEnums.Bool;
                }
                else if (kvp.Key.StartsWith("V") && kvp.Key.Contains("."))
                {
                    newAddresses[kvp.Key] = DataTypeEnums.Bool;
                }
                else if (kvp.Key.StartsWith("VW"))
                {
                    newAddresses[kvp.Key] = DataTypeEnums.Int16;
                }
                else if (kvp.Key.StartsWith("VD"))
                {
                    newAddresses[kvp.Key] = DataTypeEnums.Int32;
                }
                else
                {
                    newAddresses[kvp.Key] = DataTypeEnums.Int32; // 默认类型
                }
            }
            
            // 测试批量读取性能
            var batchStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < testRounds; i++)
            {
                var batchResult = await client.BatchReadAsync(newAddresses);
                Assert.True(batchResult.IsSuccess, $"批量读取失败: {batchResult.Message}");
                
                // 验证读取到的数据
                foreach (var kvp in batchResult.ResultValue)
                {
                    Assert.NotNull(kvp.Value.Item2);
                    //LogInfo($"批量读取 {kvp.Key}: {kvp.Value.Item2}");
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
                    if (address.StartsWith("Q") && address.Contains("."))
                    {
                        var result = await client.ReadBooleanAsync(address);
                        Assert.True(result.IsSuccess, $"单个读取失败: {result.Message}");
                        //LogInfo($"单个读取 {address}: {result.ResultValue}");
                    }
                    else if (address.StartsWith("V") && address.Contains("."))
                    {
                        var result = await client.ReadBooleanAsync(address);
                        Assert.True(result.IsSuccess, $"单个读取失败: {result.Message}");
                        //LogInfo($"单个读取 {address}: {result.ResultValue}");
                    }
                    else if (address.StartsWith("VW"))
                    {
                        var result = await client.ReadInt16Async(address);
                        Assert.True(result.IsSuccess, $"单个读取失败: {result.Message}");
                        //LogInfo($"单个读取 {address}: {result.ResultValue}");
                    }
                    else if (address.StartsWith("VD"))
                    {
                        var result = await client.ReadInt32Async(address);
                        Assert.True(result.IsSuccess, $"单个读取失败: {result.Message}");
                       // LogInfo($"单个读取 {address}: {result.ResultValue}");
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
            
            // 断言性能提升
            if (batchTotalTime < individualTotalTime)
            {
                LogInfo("✓ 批量读取性能优于单个读取");
            }
            else
            {
                LogWarning("⚠ 批量读取性能可能未达到预期优化效果");
            }
            
            // 数据一致性验证：对比批量读取和单个读取的值是否相等
            LogInfo("开始数据一致性验证...");
            var batchResultForValidation = await client.BatchReadAsync(newAddresses);
            Assert.True(batchResultForValidation.IsSuccess, $"数据一致性验证：批量读取失败: {batchResultForValidation.Message}");
            
            var validationErrors = new List<string>();
            
            foreach (var address in addresses.Keys)
            {
                object individualValue = null;
                object batchValue = null;
                
                // 获取单个读取的值
                if (address.StartsWith("Q") && address.Contains("."))
                {
                    var result = await client.ReadBooleanAsync(address);
                    Assert.True(result.IsSuccess, $"数据一致性验证：单个读取失败: {result.Message}");
                    individualValue = result.ResultValue;
                }
                else if (address.StartsWith("V") && address.Contains("."))
                {
                    var result = await client.ReadBooleanAsync(address);
                    Assert.True(result.IsSuccess, $"数据一致性验证：单个读取失败: {result.Message}");
                    individualValue = result.ResultValue;
                }
                else if (address.StartsWith("VW"))
                {
                    var result = await client.ReadInt16Async(address);
                    Assert.True(result.IsSuccess, $"数据一致性验证：单个读取失败: {result.Message}");
                    individualValue = result.ResultValue;
                }
                else if (address.StartsWith("VD"))
                {
                    var result = await client.ReadInt32Async(address);
                    Assert.True(result.IsSuccess, $"数据一致性验证：单个读取失败: {result.Message}");
                    individualValue = result.ResultValue;
                }
                
                // 获取批量读取的值
                if (batchResultForValidation.ResultValue.ContainsKey(address))
                {
                    batchValue = batchResultForValidation.ResultValue[address].Item2;
                }
                
                // 比较值是否相等
                if (individualValue == null || batchValue == null)
                {
                    validationErrors.Add($"地址 {address}: 值为null (单个={individualValue}, 批量={batchValue})");
                }
                else if (!individualValue.Equals(batchValue))
                {
                    validationErrors.Add($"地址 {address}: 值不匹配 (单个={individualValue}, 批量={batchValue})");
                }
            }
            
            // 报告验证结果
            if (validationErrors.Count == 0)
            {
                LogInfo($"✓ 数据一致性验证通过：{addresses.Count} 个地址的值完全匹配");
            }
            else
            {
                LogWarning($"⚠ 数据一致性验证失败：发现 {validationErrors.Count} 个不匹配");
                foreach (var error in validationErrors.Take(10)) // 只显示前10个错误
                {
                    LogWarning($"  {error}");
                }
                if (validationErrors.Count > 10)
                {
                    LogWarning($"  ... 还有 {validationErrors.Count - 10} 个错误未显示");
                }
                Assert.True(false, $"数据一致性验证失败：批量读取与单个读取的值不匹配");
            }
        }

        /// <summary>
        /// 测试V区连续地址批量写入
        /// </summary>
        private async Task TestSmart200ContinuousAddressBatchWrite()
        {
            LogInfo("=== V区连续地址批量写入测试 (100个连续Word地址) ===");
            
            // 使用100个连续的Word地址进行批量写入测试
            var continuousWriteData = new Dictionary<string, object>();
            for (int i = 0; i < 100; i++)
            {
                continuousWriteData[$"VW{2000 + i * 2}"] = (short)(1000 + i * 10);
            }
            
            LogInfo($"准备测试 {continuousWriteData.Count} 个连续Word地址的批量写入");
            LogInfo($"地址范围: VW2000 到 VW2198");
            
            await CompareSmart200WritePerformance("V区连续地址(100个Word)", continuousWriteData);
        }

        /// <summary>
        /// 测试V区分散地址批量写入
        /// </summary>
        private async Task TestSmart200ScatteredAddressBatchWrite()
        {
            LogInfo("=== V区分散地址批量写入测试 (100个分散地址) ===");
            
            // 使用100个分散的地址进行批量写入测试
            var scatteredWriteData = new Dictionary<string, object>();
            
            // 50个分散的Word地址
            for (int i = 0; i < 50; i++)
            {
                scatteredWriteData[$"VW{2500 + i * 10}"] = (short)(2000 + i * 10);
            }
            
            // 50个分散的DWord地址
            for (int i = 0; i < 50; i++)
            {
                scatteredWriteData[$"VD{3000 + i * 10}"] = (int)(3000000 + i * 10000);
            }
            
            LogInfo($"准备测试 {scatteredWriteData.Count} 个分散地址的批量写入");
            LogInfo($"分布: 50个Word地址 + 50个DWord地址");
            
            await CompareSmart200WritePerformance("V区分散地址(100个混合)", scatteredWriteData);
        }

        /// <summary>
        /// 测试Q区和V区混合批量写入
        /// </summary>
        private async Task TestSmart200MixedAreaBatchWrite()
        {
            LogInfo("=== Q区和V区混合批量写入测试 (120个混合类型地址) ===");
            
            // 使用120个混合类型地址进行批量写入测试
            var mixedWriteData = new Dictionary<string, object>();
            
            // Q区：24个布尔地址 (Q5.0到Q7.7，避免与读取测试冲突)
            for (int i = 0; i < 24; i++)
            {
                mixedWriteData[$"Q{i / 8 + 5}.{i % 8}"] = (i % 2 == 0);
            }
            
            // V区：24个布尔地址 (V20.0到V22.7，避免与读取测试冲突)
            for (int i = 0; i < 24; i++)
            {
                mixedWriteData[$"V{i / 8 + 20}.{i % 8}"] = (i % 3 == 0);
            }
            
            // V区：24个Word地址
            for (int i = 0; i < 24; i++)
            {
                mixedWriteData[$"VW{3500 + i * 2}"] = (short)(4000 + i * 10);
            }
            
            // V区：24个DWord地址
            for (int i = 0; i < 24; i++)
            {
                mixedWriteData[$"VD{3600 + i * 4}"] = (int)(5000000 + i * 10000);
            }
            
            // V区：24个Real地址
            for (int i = 0; i < 24; i++)
            {
                mixedWriteData[$"VD{3720 + i * 4}"] = (float)(600.0f + i * 1.5f);
            }
            
            LogInfo($"准备测试 {mixedWriteData.Count} 个混合类型地址的批量写入");
            LogInfo($"分布: Q区(24个Bool) + V区(24个Bool) + V区(24个Word) + V区(24个DWord) + V区(24个Real)");
            
            await CompareSmart200WritePerformance("Q区和V区混合(120个混合)", mixedWriteData);
        }

        /// <summary>
        /// 测试V区布尔量批量写入
        /// </summary>
        private async Task TestSmart200VBoolBatchWrite()
        {
            LogInfo("=== V区布尔量批量写入测试 (96个V区布尔地址) ===");
            
            // 使用96个V区布尔地址进行批量写入测试
            var vBoolWriteData = new Dictionary<string, object>();
            
            // V区：96个布尔地址 (V30.0到V41.7，避免与其他测试冲突)
            for (int i = 0; i < 96; i++)
            {
                vBoolWriteData[$"V{i / 8 + 30}.{i % 8}"] = (i % 4 == 0);
            }
            
            LogInfo($"准备测试 {vBoolWriteData.Count} 个V区布尔地址的批量写入");
            LogInfo($"地址范围: V30.0 到 V41.7 (96个布尔值)");
            
            await CompareSmart200WritePerformance("V区布尔量(96个Bool)", vBoolWriteData);
        }

        /// <summary>
        /// 比较S7-200 Smart写入性能（批量 vs 单个）
        /// </summary>
        private async Task CompareSmart200WritePerformance(string testType, Dictionary<string, object> writeData)
        {
            const int testRounds = 10;
            
            LogInfo($"开始 {testType} 写入性能对比测试，共 {testRounds} 轮");
            
            // 转换写入数据为新的格式
            var newWriteData = new Dictionary<string, (DataTypeEnums, object)>();
            foreach (var kvp in writeData)
            {
                DataTypeEnums dataType;
                if (kvp.Value is bool)
                {
                    dataType = DataTypeEnums.Bool;
                }
                else if (kvp.Value is short)
                {
                    dataType = DataTypeEnums.Int16;
                }
                else if (kvp.Value is int)
                {
                    dataType = DataTypeEnums.Int32;
                }
                else if (kvp.Value is float)
                {
                    dataType = DataTypeEnums.Float;
                }
                else
                {
                    dataType = DataTypeEnums.Int32; // 默认类型
                }
                newWriteData[kvp.Key] = (dataType, kvp.Value);
            }
            
            // 测试批量写入性能
            var batchStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < testRounds; i++)
            {
                var batchResult = await client.BatchWriteAsync(newWriteData);
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
                    if (kvp.Value is bool boolVal)
                        result = await client.WriteAsync(kvp.Key, boolVal);
                    else if (kvp.Value is short shortVal)
                        result = await client.WriteAsync(kvp.Key, shortVal);
                    else if (kvp.Value is int intVal)
                        result = await client.WriteAsync(kvp.Key, intVal);
                    else if (kvp.Value is float floatVal)
                        result = await client.WriteAsync(kvp.Key, floatVal);
                    else
                        throw new ArgumentException($"不支持的数据类型: {kvp.Value.GetType()}");
                    
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
                if (kvp.Value is bool boolValue)
                {
                    var result = await client.ReadBooleanAsync(kvp.Key);
                    Assert.True(result.IsSuccess && result.ResultValue == boolValue, $"写入验证失败: {kvp.Key}");
                }
                else if (kvp.Value is short shortValue)
                {
                    var result = await client.ReadInt16Async(kvp.Key);
                    Assert.True(result.IsSuccess && result.ResultValue == shortValue, $"写入验证失败: {kvp.Key}");
                }
                else if (kvp.Value is int intValue)
                {
                    var result = await client.ReadInt32Async(kvp.Key);
                    Assert.True(result.IsSuccess && result.ResultValue == intValue, $"写入验证失败: {kvp.Key}");
                }
                else if (kvp.Value is float floatValue)
                {
                    var result = await client.ReadFloatAsync(kvp.Key);
                    Assert.True(result.IsSuccess && Math.Abs(result.ResultValue - floatValue) < FLOAT_COMPARISON_PRECISION, 
                        $"写入验证失败: {kvp.Key}");
                }
            }
            
            LogInfo($"{testType} 写入验证完成");
        }

        private void SafeDisconnect()
        {
            if (client != null)
            {
                client.EnableAutoReconnect = false;
                client.Disconnect();
            }
        }

        private async Task SafeDisconnectAsync()
        {
            if (client != null)
            {
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

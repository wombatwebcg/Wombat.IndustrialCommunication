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
        
        /// <summary>基础数据测试地址</summary>
        private const string BASIC_DATA_ADDRESS = "V700";
        private const string STRING_DATA_ADDRESS = "V1000";
        
        /// <summary>连接测试地址</summary>
        private const string CONNECTION_TEST_ADDRESS = "V700";
        
        #endregion

        #region 私有字段
        
        private SiemensClient client;
        private readonly ITestOutputHelper _output;
        private ConnectionDisruptorExtreme _disruptor;
        
        #endregion

        #region 构造函数
        
        /// <summary>
        /// 初始化S7-200 Smart测试类
        /// </summary>
        /// <param name="output">测试输出助手</param>
        public S7_Smart200(ITestOutputHelper output = null)
        {
            _output = output;
            _disruptor = new ConnectionDisruptorExtreme(new XUnitLogger(_output, "Disruptor"));
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
                var writeFloatResult = await client.WriteAsync(CONNECTION_TEST_ADDRESS, testFloatValue);
                Assert.True(writeFloatResult.IsSuccess, $"写入浮点数失败: {writeFloatResult.Message}");
                var readFloatResult = await client.ReadFloatAsync(CONNECTION_TEST_ADDRESS);
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
                var writeArrayResult = await client.WriteAsync(CONNECTION_TEST_ADDRESS, testIntArray);
                Assert.True(writeArrayResult.IsSuccess, $"写入数组失败: {writeArrayResult.Message}");
                var readArrayResult = await client.ReadInt32Async(CONNECTION_TEST_ADDRESS, testIntArray.Length);
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

        #endregion

        #region 私有测试方法

        /// <summary>
        /// 执行同步读写测试
        /// </summary>
        private void ReadWrite()
        {
            LogInfo("执行基于V700和Q区的同步读写测试");
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

                client.Write("V700", short_number);
                Assert.True(client.ReadInt16("V700").ResultValue == short_number);
                client.Write("V700", short_number_1);
                Assert.True(client.ReadUInt16("V700").ResultValue == short_number_1);

                client.Write("V700", int_number);
                Assert.True(client.ReadInt32("V700").ResultValue == int_number);
                client.Write("V700", int_number_1);
                Assert.True(client.ReadUInt32("V700").ResultValue == int_number_1);

                client.Write("V700", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64("V700").ResultValue == Convert.ToInt64(int_number));
                client.Write("V700", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64("V700").ResultValue == Convert.ToUInt64(int_number_1));

                client.Write("V700", float_number);
                Assert.True(client.ReadFloat("V700").ResultValue == float_number);
                client.Write("V700", Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble("V700").ResultValue == Convert.ToDouble(float_number));

                //var rrr =  client.Write("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).ResultValue == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                client.Write("V700", bool_values);
                var bool_values_result = client.ReadBoolean("V700", bool_values.Length);
                for (int j = 0; j < bool_values_result.ResultValue.Length; j++)
                {
                    Assert.True(bool_values_result.ResultValue[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", short_values);
                var short_values_result = client.ReadInt16("V700", short_values.Length);
                for (int j = 0; j < short_values_result.ResultValue.Length; j++)
                {
                    Assert.True(short_values_result.ResultValue[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", ushort_values);
                var ushort_values_result = client.ReadInt16("V700", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.ResultValue.Length; j++)
                {
                    Assert.True(ushort_values_result.ResultValue[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", int_values);
                var int_values_result = client.ReadInt32("V700", int_values.Length);
                for (int j = 0; j < int_values_result.ResultValue.Length; j++)
                {
                    Assert.True(int_values_result.ResultValue[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", uint_values);
                var uint_values_result = client.ReadUInt32("V700", uint_values.Length);
                for (int j = 0; j < uint_values_result.ResultValue.Length; j++)
                {
                    Assert.True(uint_values_result.ResultValue[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", long_values);
                var long_values_result = client.ReadInt64("V700", long_values.Length);
                for (long j = 0; j < long_values_result.ResultValue.Length; j++)
                {
                    Assert.True(long_values_result.ResultValue[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", ulong_values);
                var ulong_values_result = client.ReadUInt64("V700", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.ResultValue.Length; j++)
                {
                    Assert.True(ulong_values_result.ResultValue[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", float_values);
                var float_values_result = client.ReadFloat("V700", float_values.Length);
                for (int j = 0; j < float_values_result.ResultValue.Length; j++)
                {
                    Assert.True(float_values_result.ResultValue[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", double_values);
                var double_values_result = client.ReadDouble("V700", double_values.Length);
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

                var ssss = await client.WriteAsync("V700", short_number);
                var tttt = client.ReadInt16Async("V700");
                Assert.True(client.ReadInt16Async("V700").Result.ResultValue == short_number);
                await client.WriteAsync("V700", short_number_1);
                Assert.True(client.ReadUInt16Async("V700").Result.ResultValue == short_number_1);

                await client.WriteAsync("V700", int_number);
                Assert.True(client.ReadInt32Async("V700").Result.ResultValue == int_number);
                await client.WriteAsync("V700", int_number_1);
                Assert.True(client.ReadUInt32Async("V700").Result.ResultValue == int_number_1);

                await client.WriteAsync("V700", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64Async("V700").Result.ResultValue == Convert.ToInt64(int_number));
                await client.WriteAsync("V700", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64Async("V700").Result.ResultValue == Convert.ToUInt64(int_number_1));

                await client.WriteAsync("V700", float_number);
                Assert.True(client.ReadFloatAsync("V700").Result.ResultValue == float_number);
                await client.WriteAsync("V700", Convert.ToDouble(float_number));
                Assert.True(client.ReadDoubleAsync("V700").Result.ResultValue == Convert.ToDouble(float_number));

                //var rrr =  await client.WriteAsync("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).ResultValue == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                await client.WriteAsync("V700", bool_values);
                var bool_values_result = client.ReadBooleanAsync("V700", bool_values.Length);
                for (int j = 0; j < bool_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(bool_values_result.Result.ResultValue[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", short_values);
                var short_values_result = client.ReadInt16Async("V700", short_values.Length);
                for (int j = 0; j < short_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(short_values_result.Result.ResultValue[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", ushort_values);
                var ushort_values_result = client.ReadInt16Async("V700", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(ushort_values_result.Result.ResultValue[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", int_values);
                var int_values_result = client.ReadInt32Async("V700", int_values.Length);
                for (int j = 0; j < int_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(int_values_result.Result.ResultValue[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", uint_values);
                var uint_values_result = client.ReadUInt32Async("V700", uint_values.Length);
                for (int j = 0; j < uint_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(uint_values_result.Result.ResultValue[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", long_values);
                var long_values_result = client.ReadInt64Async("V700", long_values.Length);
                for (long j = 0; j < long_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(long_values_result.Result.ResultValue[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", ulong_values);
                var ulong_values_result = client.ReadUInt64Async("V700", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(ulong_values_result.Result.ResultValue[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", float_values);
                var float_values_result = client.ReadFloatAsync("V700", float_values.Length);
                for (int j = 0; j < float_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(float_values_result.Result.ResultValue[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", double_values);
                var double_values_result = client.ReadDoubleAsync("V700", double_values.Length);
                for (int j = 0; j < double_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(double_values_result.Result.ResultValue[j] == double_values[j]);

                }



            }

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

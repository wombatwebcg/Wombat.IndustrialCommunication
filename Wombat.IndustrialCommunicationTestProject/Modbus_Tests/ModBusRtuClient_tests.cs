using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunicationTestProject.Helper;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTest.Modbus
{
    public class ModbusRTUClient_tests
    {
        private ModbusRTUClient client;
        byte stationNumber = 1;//站号
        private readonly ITestOutputHelper _output;
        
        public ModbusRTUClient_tests(ITestOutputHelper output = null)
        {
            _output = output;
            //client = new ModbusRTUClient("COM3", 9600, 8, StopBits.One, Parity.None);
            client = new ModbusRTUClient("COM1", 9600, 8, StopBits.One, Parity.None);
            
            // 配置自动重连参数，用于测试
            client.EnableAutoReconnect = true;
            client.MaxReconnectAttempts = 3;
            client.ReconnectDelay = TimeSpan.FromSeconds(1);
        }

        [Fact]
        public void 短连接单读写()
        {
            client.IsLongConnection = false;
            singleReadWrite();

        }

        [Fact]
        public void 长连接单读写()
        {
            client.IsLongConnection = true;
            client.Connect();
            singleReadWrite();
            client.Disconnect();
        }

        [Fact]
        public async Task 短连接批量写异步()
        {
            client.IsLongConnection = false;
            await multipleReadWrite();

        }
        [Fact]
        public async Task 长连接批量读写异步()
        {
            client.Connect();
            await multipleReadWrite();
            client.Disconnect();
        }

        [Fact]
        public async Task 长连接模式下的短线重连测试()
        {
            // 1. 配置为长连接模式
            client.IsLongConnection = true;
            client.EnableAutoReconnect = true;
            await client.ConnectAsync();
            
            try
            {
                // 2. 先执行一次正常读写确认连接工作正常
                _output?.WriteLine("执行初始读写操作验证连接");
                Random rnd = new Random((int)Stopwatch.GetTimestamp());
                short testValue = (short)rnd.Next(short.MinValue, short.MaxValue);
                
                // 写入测试值
                var writeResult = await client.WriteAsync("1;16;0", testValue);
                Assert.True(writeResult.IsSuccess, $"初始写入失败: {writeResult.Message}");
                
                // 读取并验证测试值
                var readResult = await client.ReadInt16Async("1;3;0");
                Assert.True(readResult.IsSuccess, $"初始读取失败: {readResult.Message}");
                Assert.Equal(testValue, readResult.ResultValue);
                
                // 3. 模拟串口中断
                _output?.WriteLine("模拟串口中断");
                var disruptor = new ConnectionDisruptor();
                var disruptResult = await disruptor.SimulateConnectionDisruption(client);
                Assert.True(disruptResult.IsSuccess, $"模拟中断失败: {disruptResult.Message}");
                
                // 4. 等待一段时间，让自动重连有机会启动
                _output?.WriteLine("等待3秒让自动重连启动");
                await Task.Delay(3000);
                
                // 5. 尝试读取数据，这应该触发自动重连
                _output?.WriteLine("尝试读取数据，这应该触发自动重连");
                short newTestValue = (short)(testValue + 1);
                
                // 尝试写入新的测试值
                var reconnectWriteResult = await client.WriteAsync("1;16;0", newTestValue);
                Assert.True(reconnectWriteResult.IsSuccess, $"重连后写入失败: {reconnectWriteResult.Message}");
                
                // 读取并验证新的测试值
                var reconnectReadResult = await client.ReadInt16Async("1;3;0");
                Assert.True(reconnectReadResult.IsSuccess, $"重连后读取失败: {reconnectReadResult.Message}");
                Assert.Equal(newTestValue, reconnectReadResult.ResultValue);
                
                _output?.WriteLine("自动重连测试成功");
            }
            finally
            {
                // 6. 清理：断开连接
                await client.DisconnectAsync();
            }
        }
        
        [Fact]
        public async Task 长连接模式下的手动重连测试()
        {
            // 1. 配置为长连接模式但禁用自动重连
            client.IsLongConnection = true;
            client.EnableAutoReconnect = false;
            await client.ConnectAsync();
            
            try
            {
                // 2. 先执行一次正常读写确认连接工作正常
                _output?.WriteLine("执行初始读写操作验证连接");
                Random rnd = new Random((int)Stopwatch.GetTimestamp());
                short testValue = (short)rnd.Next(short.MinValue, short.MaxValue);
                
                // 写入测试值
                var writeResult = await client.WriteAsync("1;16;0", testValue);
                Assert.True(writeResult.IsSuccess, $"初始写入失败: {writeResult.Message}");
                
                // 读取并验证测试值
                var readResult = await client.ReadInt16Async("1;3;0");
                Assert.True(readResult.IsSuccess, $"初始读取失败: {readResult.Message}");
                Assert.Equal(testValue, readResult.ResultValue);
                
                // 3. 模拟串口中断
                _output?.WriteLine("模拟串口中断");
                var disruptor = new ConnectionDisruptor();
                var disruptResult = await disruptor.SimulateConnectionDisruption(client);
                Assert.True(disruptResult.IsSuccess, $"模拟中断失败: {disruptResult.Message}");
                
                // 4. 验证连接已断开
                Assert.False(client.Connected, "客户端应处于断开状态");
                
                // 5. 尝试读取数据，应该失败，因为连接断开且禁用了自动重连
                _output?.WriteLine("尝试读取数据，预期将失败");
                var failReadResult = await client.ReadInt16Async("1;3;0");
                Assert.False(failReadResult.IsSuccess, "禁用自动重连时，断线后的读取应该失败");
                
                // 6. 手动重连
                _output?.WriteLine("执行手动重连");
                var reconnectResult = await client.ConnectAsync();
                Assert.True(reconnectResult.IsSuccess, $"手动重连失败: {reconnectResult.Message}");
                
                // 7. 重连后执行读写操作
                _output?.WriteLine("重连后执行读写操作");
                short newTestValue = (short)(testValue + 1);
                
                // 尝试写入新的测试值
                var reconnectWriteResult = await client.WriteAsync("1;16;0", newTestValue);
                Assert.True(reconnectWriteResult.IsSuccess, $"重连后写入失败: {reconnectWriteResult.Message}");
                
                // 读取并验证新的测试值
                var reconnectReadResult = await client.ReadInt16Async("1;3;0");
                Assert.True(reconnectReadResult.IsSuccess, $"重连后读取失败: {reconnectReadResult.Message}");
                Assert.Equal(newTestValue, reconnectReadResult.ResultValue);
                
                _output?.WriteLine("手动重连测试成功");
            }
            finally
            {
                // 8. 清理：断开连接
                await client.DisconnectAsync();
            }
        }

        [Fact]
        public async Task 短连接模式下的连接中断恢复测试()
        {
            // 1. 配置为短连接模式
            client.IsLongConnection = false;
            
            // 2. 确保客户端未连接
            if (client.Connected)
            {
                await client.DisconnectAsync();
            }
            
            try
            {
                // 3. 执行初始操作，这会建立一个临时连接
                _output?.WriteLine("执行初始读写操作");
                Random rnd = new Random((int)Stopwatch.GetTimestamp());
                short testValue = (short)rnd.Next(short.MinValue, short.MaxValue);
                
                // 写入测试值
                var writeResult = await client.WriteAsync("1;16;0", testValue);
                Assert.True(writeResult.IsSuccess, $"初始写入失败: {writeResult.Message}");
                
                // 读取并验证测试值
                var readResult = await client.ReadInt16Async("1;3;0");
                Assert.True(readResult.IsSuccess, $"初始读取失败: {readResult.Message}");
                Assert.Equal(testValue, readResult.ResultValue);
                
                // 4. 在短连接模式下，每次操作后连接应该自动关闭
                Assert.False(client.Connected, "短连接模式下，操作完成后客户端应处于断开状态");
                
                // 5. 尝试第二次操作，这应该建立一个新的临时连接
                _output?.WriteLine("执行第二次读写操作");
                short newTestValue = (short)(testValue + 1);
                
                // 写入新的测试值
                var secondWriteResult = await client.WriteAsync("1;16;0", newTestValue);
                Assert.True(secondWriteResult.IsSuccess, $"第二次写入失败: {secondWriteResult.Message}");
                
                // 读取并验证新的测试值
                var secondReadResult = await client.ReadInt16Async("1;3;0");
                Assert.True(secondReadResult.IsSuccess, $"第二次读取失败: {secondReadResult.Message}");
                Assert.Equal(newTestValue, secondReadResult.ResultValue);
                
                // 6. 操作完成后连接应再次自动关闭
                Assert.False(client.Connected, "短连接模式下，第二次操作完成后客户端应处于断开状态");
                
                _output?.WriteLine("短连接模式测试成功");
            }
            finally
            {
                // 7. 确保连接断开
                if (client.Connected)
                {
                    await client.DisconnectAsync();
                }
            }
        }

        [Fact]
        public async Task 短连接模式下的连续操作中断测试()
        {
            // 1. 配置为短连接模式
            client.IsLongConnection = false;
            // 配置短连接重连参数，确保即使在短连接模式下也能进行有限次数的重试
            client.ShortConnectionReconnectAttempts = 1;
            
            try
            {
                // 2. 执行初始操作，这会建立一个临时连接
                _output?.WriteLine("执行初始读写操作");
                Random rnd = new Random((int)Stopwatch.GetTimestamp());
                short testValue = (short)rnd.Next(short.MinValue, short.MaxValue);
                
                // 写入测试值
                var writeResult = await client.WriteAsync("1;16;0", testValue);
                Assert.True(writeResult.IsSuccess, $"初始写入失败: {writeResult.Message}");
                
                // 读取并验证测试值
                var readResult = await client.ReadInt16Async("1;3;0");
                Assert.True(readResult.IsSuccess, $"初始读取失败: {readResult.Message}");
                Assert.Equal(testValue, readResult.ResultValue);
                
                // 3. 模拟串口临时不可用（例如，设备断电或串口拔出）
                _output?.WriteLine("模拟串口临时不可用");
                
                // 这里我们不直接断开客户端连接，因为在短连接模式下它已经断开了
                // 相反，我们等待一段时间，模拟串口不可用的情况
                await Task.Delay(3000);
                
                // 4. 尝试在串口恢复后执行操作
                _output?.WriteLine("在串口恢复后执行读写操作");
                short newTestValue = (short)(testValue + 1);
                
                // 写入新的测试值
                var recoveryWriteResult = await client.WriteAsync("1;16;0", newTestValue);
                Assert.True(recoveryWriteResult.IsSuccess, $"恢复后写入失败: {recoveryWriteResult.Message}");
                
                // 读取并验证新的测试值
                var recoveryReadResult = await client.ReadInt16Async("1;3;0");
                Assert.True(recoveryReadResult.IsSuccess, $"恢复后读取失败: {recoveryReadResult.Message}");
                Assert.Equal(newTestValue, recoveryReadResult.ResultValue);
                
                _output?.WriteLine("短连接模式中断恢复测试成功");
            }
            finally
            {
                // 5. 确保连接断开
                if (client.Connected)
                {
                    await client.DisconnectAsync();
                }
            }
        }

        private void singleReadWrite()
        {
            Random rnd = new Random((int)Stopwatch.GetTimestamp());

            for (int i = 0; i < 10; i++)
            {
                #region 生产随机数
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                ushort ushort_number = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                uint uint_number = (uint)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
                long long_number = rnd.Next(int.MinValue, int.MaxValue);
                ulong ulong_number = (ulong)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
                float float_number = (float)(rnd.Next(int.MinValue, int.MaxValue) / 110.0);
                double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100.0;
                bool coil = int_number % 2 == 0;
                #endregion
                //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
                var w1 = client.Write(
                ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                {
                    StationNumber = 1,
                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteSingleRegister,
                    Address = 0
                }), short_number);
                var w2 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                {
                    StationNumber = 1,
                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteSingleRegister,
                    Address = 4
                }), ushort_number);
                var w3 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                {
                    StationNumber = 1,
                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                    Address = 8
                }), int_number);
                var w4 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                {
                    StationNumber = 1,
                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                    Address = 12
                }), uint_number);
                var w5 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                {
                    StationNumber = 1,
                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                    Address = 16
                }), long_number);
                var w6 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                {
                    StationNumber = 1,
                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                    Address = 20
                }), ulong_number);
                var w7 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                {
                    StationNumber = 1,
                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                    Address = 24
                }), float_number);
                var w8 = client.Write("1;16;28", double_number);
                var w19 = client.Write("1;5;0", coil);


                //写入可能有一定的延时，500毫秒后检验

                //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
                var read_short_number = client.ReadInt16($"1;3;0");
                Assert.True(read_short_number.ResultValue == short_number);
                Assert.True(client.ReadUInt16("1;0x3;4").ResultValue == ushort_number);
                Assert.True(client.ReadInt32("1;0x3;8").ResultValue == int_number);
                Assert.True(client.ReadUInt32("1;0x3;12").ResultValue == uint_number);
                Assert.True(client.ReadInt64("1;0x3;16").ResultValue == long_number);
                Assert.True(client.ReadUInt64("1;0x3;20").ResultValue == ulong_number);
                Assert.True(client.ReadFloat("1;0x3;24").ResultValue == float_number);
                Assert.True(client.ReadDouble("1;0x3;28").ResultValue == double_number);
                Assert.True(client.ReadBoolean("1;0x1;0").ResultValue == coil);

                //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).ResultValue == orderCode);
            }

        }


        private async Task multipleReadWrite()
        {
            bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
            await client.WriteAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
            {
                StationNumber = 1,
                FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleCoils,
                Address = 0
            }), bool_values);
            var bool_values_result = await client.ReadBooleanAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
            {
                StationNumber = 1,
                FunctionCode = (byte)ModbusStandardFunctionCode.ReadCoils,
                Address = 0
            }), bool_values.Length);
            for (int j = 0; j < bool_values_result.ResultValue.Length; j++)
            {
                Assert.True(bool_values_result.ResultValue[j] == bool_values[j]);

            }

            short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            await client.WriteAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
            {
                StationNumber = 1,
                FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                Address = 0
            }), short_values);
            var short_values_result = await client.ReadInt16Async(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
            {
                StationNumber = 1,
                FunctionCode = (byte)ModbusStandardFunctionCode.ReadHoldingRegisters,
                Address = 0
            }), short_values.Length);
            for (int j = 0; j < short_values_result.ResultValue.Length; j++)
            {
                Assert.True(short_values_result.ResultValue[j] == short_values[j]);

            }

            ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            await client.WriteAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister, Address = 0 }), ushort_values);
            var ushort_values_result = await client.ReadInt16Async(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
            {
                StationNumber = 1,
                FunctionCode = (byte)ModbusStandardFunctionCode.ReadHoldingRegisters,
                Address = 0
            }), ushort_values.Length);
            for (int j = 0; j < ushort_values_result.ResultValue.Length; j++)
            {
                Assert.True(ushort_values_result.ResultValue[j] == ushort_values[j]);

            }

            int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            await client.WriteAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister, Address = 0 }), int_values);
            var int_values_result = await client.ReadInt32Async(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.ReadHoldingRegisters, Address = 0 }), int_values.Length);
            for (int j = 0; j < int_values_result.ResultValue.Length; j++)
            {
                Assert.True(int_values_result.ResultValue[j] == int_values[j]);

            }

            uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            await client.WriteAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister, Address = 0 }), uint_values);
            var uint_values_result = await client.ReadUInt32Async(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.ReadHoldingRegisters, Address = 0 }), uint_values.Length);
            for (int j = 0; j < uint_values_result.ResultValue.Length; j++)
            {
                Assert.True(uint_values_result.ResultValue[j] == uint_values[j]);

            }

            long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            await client.WriteAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister, Address = 0 }), long_values);
            var long_values_result = await client.ReadInt64Async(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.ReadHoldingRegisters, Address = 0 }), long_values.Length);
            for (long j = 0; j < long_values_result.ResultValue.Length; j++)
            {
                Assert.True(long_values_result.ResultValue[j] == long_values[j]);

            }

            ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            await client.WriteAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister, Address = 0 }), ulong_values);
            var ulong_values_result = await client.ReadUInt64Async(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.ReadHoldingRegisters, Address = 0 }), ulong_values.Length);
            for (int j = 0; j < ulong_values_result.ResultValue.Length; j++)
            {
                Assert.True(ulong_values_result.ResultValue[j] == ulong_values[j]);

            }

            float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            await client.WriteAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister, Address = 0 }), float_values);
            var float_values_result = await client.ReadFloatAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.ReadHoldingRegisters, Address = 0 }), float_values.Length);
            for (int j = 0; j < float_values_result.ResultValue.Length; j++)
            {
                Assert.True(float_values_result.ResultValue[j] == float_values[j]);

            }
            double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            await client.WriteAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister, Address = 0 }), double_values);
            var double_values_result = await client.ReadDoubleAsync(ModbusAddressParser.ParseModbusAddress(new ModbusHeader() 
            { StationNumber = 1, FunctionCode = (byte)ModbusStandardFunctionCode.ReadHoldingRegisters, Address = 0 }), double_values.Length);
            for (int j = 0; j < double_values_result.ResultValue.Length; j++)
            {
                Assert.True(double_values_result.ResultValue[j] == double_values[j]);

            }

        }

        //[Fact]
        //public void 批量读取()
        //{
        //    var list = new List<ModbusInput>();
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 2,
        //        DataType = DataTypeEnums.Int16,
        //        FunctionCode = 3,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 2,
        //        DataType = DataTypeEnums.Int16,
        //        FunctionCode = 4,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 5,
        //        DataType = DataTypeEnums.Int16,
        //        FunctionCode = 3,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 199,
        //        DataType = DataTypeEnums.Int16,
        //        FunctionCode = 3,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 200,
        //        DataType = DataTypeEnums.Bool,
        //        FunctionCode = 2,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 201,
        //        DataType = DataTypeEnums.Bool,
        //        FunctionCode = 2,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 202,
        //        DataType = DataTypeEnums.Bool,
        //        FunctionCode = 2,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 203,
        //        DataType = DataTypeEnums.Bool,
        //        FunctionCode = 2,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 204,
        //        DataType = DataTypeEnums.Bool,
        //        FunctionCode = 2,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 205,
        //        DataType = DataTypeEnums.Bool,
        //        FunctionCode = 2,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 206,
        //        DataType = DataTypeEnums.Bool,
        //        FunctionCode = 2,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 207,
        //        DataType = DataTypeEnums.Bool,
        //        FunctionCode = 2,
        //        StationNumber = 1
        //    });
        //    list.Add(new ModbusInput()
        //    {
        //        Address = 208,
        //        DataType = DataTypeEnums.Bool,
        //        FunctionCode = 2,
        //        StationNumber = 1
        //    });
        //    var result = client.BatchRead(list);
        //}
    }
}

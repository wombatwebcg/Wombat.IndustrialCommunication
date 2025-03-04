using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;

using Xunit;

namespace Wombat.IndustrialCommunicationTest.Modbus
{
    public class ModbusRTUClient_tests
    {
        private ModbusRTUClient client;
        byte stationNumber = 1;//站号
        public ModbusRTUClient_tests()
        {
            //client = new ModbusRTUClient("COM3", 9600, 8, StopBits.One, Parity.None);
            client = new ModbusRTUClient("COM1", 9600, 8, StopBits.One, Parity.None);
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

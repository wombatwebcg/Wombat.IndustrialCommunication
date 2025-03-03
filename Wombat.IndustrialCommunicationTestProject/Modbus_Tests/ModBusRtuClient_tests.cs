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
        public void 短连接自动开关()
        {
            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            client.IsLongConnection = false;
            singleReadWrite();

        }

        [Fact]
        public async Task 长连接主动开关()
        {
            client.IsLongConnection = true;
          var oop=  client.Connect();

            Random rnd = new Random((int)Stopwatch.GetTimestamp());

            Task task1 = Task.Run(() =>
            {
                singleReadWrite();
                Task.Delay(1000).Wait();

            });
            Task.WaitAll(task1);
            client.Disconnect();
        }
        [Fact]
        public async Task 长连接主动开关异步()
        {
            var oop = client.Connect();
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
                ////写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
                //var w1 = client.Write(
                //ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                //{
                //    StationNumber = 1,
                //    FunctionCode = (byte)ModbusStandardFunctionCode.WriteSingleRegister,
                //    Address = 0
                //}), short_number);
                //var w2 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                //{
                //    StationNumber = 1,
                //    FunctionCode = (byte)ModbusStandardFunctionCode.WriteSingleRegister,
                //    Address = 4
                //}), ushort_number);
                //var w3 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                //{
                //    StationNumber = 1,
                //    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                //    Address = 8
                //}), int_number);
                //var w4 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                //{
                //    StationNumber = 1,
                //    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                //    Address = 12
                //}), uint_number);
                //var w5 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                //{
                //    StationNumber = 1,
                //    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                //    Address = 16
                //}), long_number);
                //var w6 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                //{
                //    StationNumber = 1,
                //    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                //    Address = 20
                //}), ulong_number);
                //var w7 = client.Write(ModbusAddressParser.ParseModbusAddress(new ModbusHeader()
                //{
                //    StationNumber = 1,
                //    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
                //    Address = 24
                //}), float_number);
                //var w8 = client.Write("1;16;28", double_number);
                Thread.Sleep(100);

                var w19 = client.Write("1;5;0", true);
                var w29 = client.Write("1;5;0", false);


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

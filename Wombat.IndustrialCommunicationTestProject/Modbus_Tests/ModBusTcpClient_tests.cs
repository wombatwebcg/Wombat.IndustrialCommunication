using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.Modbus
{
    //public class ModbusTcpClient_tests
    //{
    //    ModbusTcpClient client;
    //    byte stationNumber = 1;//站号
    //    public ModbusTcpClient_tests()
    //    {
    //        var ip = IPAddress.Parse("127.0.0.1");
    //        client = new ModbusTcpClient(new IPEndPoint(ip, 502));
    //    }

    //    public bool ShortToBit(int value, int index)
    //    {
    //        var binaryArray = DataTypeExtensions.IntToBinaryArray(value, 16);
    //        var length = binaryArray.Length - 16;
    //        return binaryArray[length + index].ToString() == "1";
    //    }

    //    [Fact]
    //    public async Task 短连接自动开关()
    //    {
    //        client.IsLongLivedConnection = false;
    //        Random rnd = new Random((int)Stopwatch.GetTimestamp());

    //        Task task1 = Task.Run(() =>
    //        {
    //            for (int i = 0; i < 10; i++)
    //            {
    //                #region 生产随机数
    //                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
    //                ushort ushort_number = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
    //                int int_number = rnd.Next(int.MinValue, int.MaxValue);
    //                uint uint_number = (uint)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                long long_number = rnd.Next(int.MinValue, int.MaxValue);
    //                ulong ulong_number = (ulong)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                float float_number = rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                bool coil = int_number % 2 == 0;
    //                #endregion
    //                //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //                var w1 = client.Write(
    //                ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteSingleRegister,
    //                    Address =0
    //                }), short_number);
    //                var w2 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteSingleRegister,
    //                    Address =4
    //                }), ushort_number);
    //                var w3 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =8
    //                }), int_number);
    //                var w4 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =12
    //                }), uint_number);
    //                var w5 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =16
    //                }), long_number);
    //                var w6 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =20
    //                }), ulong_number);
    //                var w7 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =24
    //                }), float_number);
    //                var w8 = client.Write("1;0x10;28", double_number);



    //                //写入可能有一定的延时，500毫秒后检验

    //                //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //                var read_short_number = client.ReadInt16($"1;3;0");
    //                Assert.True(read_short_number.Value == short_number);
    //                Assert.True(client.ReadUInt16("1;0x3;4").Value == ushort_number);
    //                Assert.True(client.ReadInt32("1;0x3;8").Value == int_number);
    //                Assert.True(client.ReadUInt32("1;0x3;12").Value == uint_number);
    //                Assert.True(client.ReadInt64("1;0x3;16").Value == long_number);
    //                Assert.True(client.ReadUInt64("1;0x3;20").Value == ulong_number);
    //                Assert.True(client.ReadFloat("1;0x3;24").Value == float_number);

    //                //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).Value == orderCode);
    //            }
    //            Task.Delay(1000).Wait();

    //        });
    //        Task task2 = Task.Run(() =>
    //        {
    //            for (int i = 0; i < 10; i++)
    //            {
    //                #region 生产随机数
    //                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
    //                ushort ushort_number = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
    //                int int_number = rnd.Next(int.MinValue, int.MaxValue);
    //                uint uint_number = (uint)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                long long_number = rnd.Next(int.MinValue, int.MaxValue);
    //                ulong ulong_number = (ulong)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                float float_number = rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                bool coil = int_number % 2 == 0;
    //                #endregion
    //                //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //                var w1 = client.Write("1;5;10", coil);
    //                var w2 = client.Write("1;5;11", coil);
    //                var w3 = client.Write("1;5;12", coil);
    //                var w4 = client.Write("1;5;13", coil);
    //                var w5 = client.Write("1;5;14", coil);



    //                //写入可能有一定的延时，500毫秒后检验

    //                //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)

    //                Assert.True(client.ReadBoolean("1;1;10").Value == coil);
    //                Assert.True(client.ReadBoolean("1;1;11").Value == coil);
    //                Assert.True(client.ReadBoolean("1;1;12").Value == coil);
    //                Assert.True(client.ReadBoolean("1;1;13").Value == coil);
    //                Assert.True(client.ReadBoolean("1;1;14").Value == coil);
    //                //Assert.True(client.ReadDouble("28").Value == double_number);
    //                //Assert.True(client.ReadBoolean("32").Value == coil);

    //                //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).Value == orderCode);
    //            }

    //        });
    //        Task.WaitAll(task1, task2);
    //    }

    //    [Fact]
    //    public async Task 长连接主动开关()
    //    {
    //        client.Connect();

    //        Random rnd = new Random((int)Stopwatch.GetTimestamp());
    //        //for (int i = 0; i < 10; i++)
    //        //{
    //        //    #region 生产随机数
    //        //    short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
    //        //    ushort ushort_number = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
    //        //    int int_number = rnd.Next(int.MinValue, int.MaxValue);
    //        //    uint uint_number = (uint)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //        //    long long_number = rnd.Next(int.MinValue, int.MaxValue);
    //        //    ulong ulong_number = (ulong)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //        //    float float_number = rnd.Next(int.MinValue, int.MaxValue) / 100;
    //        //    double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100;
    //        //    bool coil = int_number % 2 == 0;
    //        //    #endregion
    //        //    client.SetStationNumber(1);
    //        //    //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //        //    var w1 = client.Write("0", short_number);
    //        //    var w2 = client.Write("4", ushort_number);
    //        //    var w3 = client.Write("8", int_number);
    //        //    var w4 = client.Write("12", uint_number);
    //        //    var w5 = client.Write("16", long_number);
    //        //    var w6 = client.Write("20", ulong_number);
    //        //    var w7 = client.Write("24", float_number);
    //        //    var w8 = client.Write("28", double_number);

    //        //    var oo1 = client.Write("32", coil);

    //        //    var read_short_number3 = client.ReadInt16("0");

    //        //    //写入可能有一定的延时，500毫秒后检验
    //        //    await Task.Delay(1000);

    //        //    //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //        //    var read_short_number = client.ReadInt16("0");
    //        //    Assert.True(read_short_number.Value == short_number);
    //        //    Assert.True(client.ReadUInt16("4").Value == ushort_number);
    //        //    Assert.True(client.ReadInt32("8").Value == int_number);
    //        //    Assert.True(client.ReadUInt32("12").Value == uint_number);
    //        //    Assert.True(client.ReadInt64("16").Value == long_number);
    //        //    Assert.True(client.ReadUInt64("20").Value == ulong_number);
    //        //    Assert.True(client.ReadFloat("24").Value == float_number);
    //        //    //Assert.True(client.ReadDouble("28").Value == double_number);
    //        //    //Assert.True(client.ReadBoolean("32").Value == coil);

    //        //    //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).Value == orderCode);
    //        //}

    //        while(true)
    //        {
    //            var read_short_number33 = client.ReadInt16($"1;3;0");
    //            Debug.Write($"{read_short_number33.TimeConsuming}"+"\r\n");
    //        }

    //        Task task1 =  Task.Run(() =>
    //        {
    //            for (int i = 0; i < 10; i++)
    //            {
    //                #region 生产随机数
    //                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
    //                ushort ushort_number = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
    //                int int_number = rnd.Next(int.MinValue, int.MaxValue);
    //                uint uint_number = (uint)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                long long_number = rnd.Next(int.MinValue, int.MaxValue);
    //                ulong ulong_number = (ulong)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                float float_number = rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                bool coil = int_number % 2 == 0;
    //                #endregion
    //                //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //                var w1 = client.Write(
    //                ModbusAddressParser.ParseModbusHeader(new ModbusHeader() { 
    //                StationNumber = 1,
    //                FunctionCode = (byte)ModbusStandardFunctionCode.WriteSingleRegister,
    //                Address =0}), short_number);
    //                var w2 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteSingleRegister,
    //                    Address =4
    //                }), ushort_number);
    //                var w3 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =8
    //                }), int_number);
    //                var w4 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =12
    //                }), uint_number);
    //                var w5 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =16
    //                }), long_number);
    //                var w6 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =20
    //                }), ulong_number);
    //                var w7 = client.Write(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =24
    //                }), float_number);
    //                var w8 = client.Write("1;0x10;28", double_number);



    //                //写入可能有一定的延时，500毫秒后检验

    //                //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //                var read_short_number = client.ReadInt16($"1;3;0");
    //                Assert.True(read_short_number.Value == short_number);
    //                Assert.True(client.ReadUInt16("1;0x3;4").Value == ushort_number);
    //                Assert.True(client.ReadInt32("1;0x3;8").Value == int_number);
    //                Assert.True(client.ReadUInt32("1;0x3;12").Value == uint_number);
    //                Assert.True(client.ReadInt64("1;0x3;16").Value == long_number);
    //                Assert.True(client.ReadUInt64("1;0x3;20").Value == ulong_number);
    //                Assert.True(client.ReadFloat("1;0x3;24").Value == float_number);

    //                //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).Value == orderCode);
    //            }
    //            Task.Delay(1000).Wait();

    //        });
    //        Task task2 = Task.Run(() =>
    //        {
    //            for (int i = 0; i < 10; i++)
    //            {
    //                #region 生产随机数
    //                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
    //                ushort ushort_number = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
    //                int int_number = rnd.Next(int.MinValue, int.MaxValue);
    //                uint uint_number = (uint)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                long long_number = rnd.Next(int.MinValue, int.MaxValue);
    //                ulong ulong_number = (ulong)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                float float_number = rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                bool coil = int_number % 2 == 0;
    //                #endregion
    //                //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //                var w1 = client.Write("1;5;10", coil);
    //                var w2 = client.Write("1;5;11", coil);
    //                var w3 = client.Write("1;5;12", coil);
    //                var w4 = client.Write("1;5;13", coil);
    //                var w5 = client.Write("1;5;14", coil);



    //                //写入可能有一定的延时，500毫秒后检验

    //                //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)

    //                Assert.True(client.ReadBoolean("1;1;10").Value == coil);
    //                Assert.True(client.ReadBoolean("1;1;11").Value == coil);
    //                Assert.True(client.ReadBoolean("1;1;12").Value == coil);
    //                Assert.True(client.ReadBoolean("1;1;13").Value == coil);
    //                Assert.True(client.ReadBoolean("1;1;14").Value == coil);
    //                //Assert.True(client.ReadDouble("28").Value == double_number);
    //                //Assert.True(client.ReadBoolean("32").Value == coil);

    //                //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).Value == orderCode);
    //            }

    //        });
    //        Task.WaitAll(task1, task2);
    //        client.Disconnect();
    //    }


    //    [Fact]
    //    public async Task 长连接主动开关异步()
    //    {
    //        var oop = client.Connect();

    //        Random rnd = new Random((int)Stopwatch.GetTimestamp());
    //        //for (int i = 0; i < 10; i++)
    //        //{
    //        //    #region 生产随机数
    //        //    short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
    //        //    ushort ushort_number = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
    //        //    int int_number = rnd.Next(int.MinValue, int.MaxValue);
    //        //    uint uint_number = (uint)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //        //    long long_number = rnd.Next(int.MinValue, int.MaxValue);
    //        //    ulong ulong_number = (ulong)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //        //    float float_number = rnd.Next(int.MinValue, int.MaxValue) / 100;
    //        //    double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100;
    //        //    bool coil = int_number % 2 == 0;
    //        //    #endregion
    //        //    client.SetStationNumber(1);
    //        //    //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //        //    var w1 = client.Write("0", short_number);
    //        //    var w2 = client.Write("4", ushort_number);
    //        //    var w3 = client.Write("8", int_number);
    //        //    var w4 = client.Write("12", uint_number);
    //        //    var w5 = client.Write("16", long_number);
    //        //    var w6 = client.Write("20", ulong_number);
    //        //    var w7 = client.Write("24", float_number);
    //        //    var w8 = client.Write("28", double_number);

    //        //    var oo1 = client.Write("32", coil);

    //        //    var read_short_number3 = client.ReadInt16("0");

    //        //    //写入可能有一定的延时，500毫秒后检验
    //        //    await Task.Delay(1000);

    //        //    //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //        //    var read_short_number = client.ReadInt16("0");
    //        //    Assert.True(read_short_number.Value == short_number);
    //        //    Assert.True(client.ReadUInt16("4").Value == ushort_number);
    //        //    Assert.True(client.ReadInt32("8").Value == int_number);
    //        //    Assert.True(client.ReadUInt32("12").Value == uint_number);
    //        //    Assert.True(client.ReadInt64("16").Value == long_number);
    //        //    Assert.True(client.ReadUInt64("20").Value == ulong_number);
    //        //    Assert.True(client.ReadFloat("24").Value == float_number);
    //        //    //Assert.True(client.ReadDouble("28").Value == double_number);
    //        //    //Assert.True(client.ReadBoolean("32").Value == coil);

    //        //    //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).Value == orderCode);
    //        //}

    //        await Task.Run(async () =>
    //        {
    //            for (int i = 0; i < 10; i++)
    //            {
    //                #region 生产随机数
    //                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
    //                ushort ushort_number = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
    //                int int_number = rnd.Next(int.MinValue, int.MaxValue);
    //                uint uint_number = (uint)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                long long_number = rnd.Next(int.MinValue, int.MaxValue);
    //                ulong ulong_number = (ulong)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                float float_number = rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                bool coil = int_number % 2 == 0;
    //                #endregion
    //                //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //                var w1 =await client.WriteAsync(
    //                ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteSingleRegister,
    //                    Address =0
    //                }), short_number);
    //                var w2 =await client.WriteAsync(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteSingleRegister,
    //                    Address =4
    //                }), ushort_number);
    //                var w3 =await client.WriteAsync(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =8
    //                }), int_number);
    //                var w4 =await client.WriteAsync(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =12
    //                }), uint_number);
    //                var w5 =await client.WriteAsync(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =16
    //                }), long_number);
    //                var w6 =await client.WriteAsync(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =20
    //                }), ulong_number);
    //                var w7 =await client.WriteAsync(ModbusAddressParser.ParseModbusHeader(new ModbusHeader()
    //                {
    //                    StationNumber = 1,
    //                    FunctionCode = (byte)ModbusStandardFunctionCode.WriteMultipleRegister,
    //                    Address =24
    //                }), float_number);
    //                var w8 =await client.WriteAsync("1;0x10;28", double_number);



    //                //写入可能有一定的延时，500毫秒后检验

    //                //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //                var read_short_number = await client.ReadInt16Async($"1;3;0");
    //                Assert.True(read_short_number.Value == short_number);
    //                Assert.True((await client.ReadUInt16Async("1;0x3;4")).Value == ushort_number);
    //                Assert.True((await client.ReadInt32Async("1;0x3;8")).Value == int_number);
    //                Assert.True((await client.ReadUInt32Async("1;0x3;12")).Value == uint_number);
    //                Assert.True((await client.ReadInt64Async("1;0x3;16")).Value == long_number);
    //                Assert.True((await client.ReadUInt64Async("1;0x3;20")).Value == ulong_number);
    //                Assert.True((await client.ReadFloatAsync("1;0x3;24")).Value == float_number);

    //                //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).Value == orderCode);
    //            }
    //            Task.Delay(1000).Wait();

    //        });
    //        await Task.Run(async () =>
    //        {
    //            for (int i = 0; i < 10; i++)
    //            {
    //                #region 生产随机数
    //                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
    //                ushort ushort_number = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);
    //                int int_number = rnd.Next(int.MinValue, int.MaxValue);
    //                uint uint_number = (uint)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                long long_number = rnd.Next(int.MinValue, int.MaxValue);
    //                ulong ulong_number = (ulong)Math.Abs(rnd.Next(int.MinValue, int.MaxValue));
    //                float float_number = rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100;
    //                bool coil = int_number % 2 == 0;
    //                #endregion
    //                //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
    //                var w1 =await client.WriteAsync("1;5;10", !coil);
    //                var w2 =await client.WriteAsync("1;5;11", coil);
    //                var w3 =await client.WriteAsync("1;5;12", !coil);
    //                var w4 =await client.WriteAsync("1;5;13", coil);
    //                var w5 =await client.WriteAsync("1;5;14", coil);



    //                //写入可能有一定的延时，500毫秒后检验

    //                //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)

    //                Assert.True((await client.ReadBooleanAsync("1;1;10")).Value == !coil);
    //                Assert.True((await client.ReadBooleanAsync("1;1;11")).Value == coil);
    //                Assert.True((await client.ReadBooleanAsync("1;1;12")).Value == !coil);
    //                Assert.True((await client.ReadBooleanAsync("1;1;13")).Value == coil);
    //                Assert.True((await client.ReadBooleanAsync("1;1;14")).Value == coil);
    //                //Assert.True(client.ReadDouble("28").Value == double_number);
    //                //Assert.True(client.ReadBoolean("32").Value == coil);

    //                //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).Value == orderCode);
    //            }
    //            Task.Delay(1000).Wait();

    //        });
    //       //await Task.WaitAll(task1, task2);
    //        client.Disconnect();
    //    }


    //    [Fact]
    //    public void 批量读取()
    //    {
    //        client.Connect();
    //        client.WarningLog = (msg, ex) =>
    //        {
    //            string aa = msg;
    //        };

    //        var list = new List<ModbusInput>();
    //        list.Add(new ModbusInput()
    //        {
    //            Address = 2,
    //            DataType = DataTypeEnums.Int16,
    //            FunctionCode = 3,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address = 2,
    //            DataType = DataTypeEnums.Int16,
    //            FunctionCode = 3,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address = 5,
    //            DataType = DataTypeEnums.Int16,
    //            FunctionCode = 3,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address = 19,
    //            DataType = DataTypeEnums.Int16,
    //            FunctionCode = 3,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address = 20,
    //            DataType = DataTypeEnums.Bool,
    //            FunctionCode = (byte)ModbusStandardFunctionCode.ReadCoils,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address = 21,
    //            DataType = DataTypeEnums.Bool,
    //            FunctionCode = (byte)ModbusStandardFunctionCode.ReadCoils,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address = 22,
    //            DataType = DataTypeEnums.Bool,
    //            FunctionCode = (byte)ModbusStandardFunctionCode.ReadCoils,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address =23,
    //            DataType = DataTypeEnums.Bool,
    //            FunctionCode = (byte)ModbusStandardFunctionCode.ReadCoils,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address =24,
    //            DataType = DataTypeEnums.Bool,
    //            FunctionCode = (byte)ModbusStandardFunctionCode.ReadCoils,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address =25,
    //            DataType = DataTypeEnums.Bool,
    //            FunctionCode = (byte)ModbusStandardFunctionCode.ReadCoils,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address =26,
    //            DataType = DataTypeEnums.Bool,
    //            FunctionCode = (byte)ModbusStandardFunctionCode.ReadCoils,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address =27,
    //            DataType = DataTypeEnums.Bool,
    //            FunctionCode = (byte)ModbusStandardFunctionCode.ReadCoils,
    //            StationNumber = 1
    //        });
    //        list.Add(new ModbusInput()
    //        {
    //            Address =28,
    //            DataType = DataTypeEnums.Bool,
    //            FunctionCode = (byte)ModbusStandardFunctionCode.ReadCoils,
    //            StationNumber = 1
    //        });
    //        var result = client.BatchRead(list);
    //    }
    //}
}

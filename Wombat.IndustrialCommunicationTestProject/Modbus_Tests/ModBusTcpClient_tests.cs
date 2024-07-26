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
    public class ModbusTcpClient_tests
    {
        ModbusTcpClient client;
        byte stationNumber = 1;//站号
        public ModbusTcpClient_tests()
        {
            var ip = IPAddress.Parse("127.0.0.1");
            client = new ModbusTcpClient(new IPEndPoint(ip, 502));
        }

        public bool ShortToBit(int value, int index)
        {
            var binaryArray = DataTypeExtensions.IntToBinaryArray(value, 16);
            var length = binaryArray.Length - 16;
            return binaryArray[length + index].ToString() == "1";
        }

        [Fact]
        public async Task 短连接自动开关()
        {
            client.IsLongLivedConnection = false;
           //var aa1 = client.ReadUInt16Bit("0.1").Value;

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
                float float_number = rnd.Next(int.MinValue, int.MaxValue) / 100;
                double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100;
                bool coil = int_number % 2 == 0;
                #endregion
                client.SetStationNumber(1);
               //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
               var w1 = client.SetWriteSingleRegister().Write("0", short_number);
                var w2 = client.SetWriteSingleRegister().Write("4", ushort_number);
                var w3 = client.SetWriteMultipleRegister().Write("8", int_number);
                var w4 = client.SetWriteMultipleRegister().Write("12", uint_number);
                var w5 = client.SetWriteMultipleRegister().Write("16", long_number);
                var w6 = client.SetWriteMultipleRegister().Write("20", ulong_number);
                var w7 = client.SetWriteMultipleRegister().Write("24", float_number);
                var w8 = client.SetWriteMultipleRegister().Write("28", double_number);

               var oo1= client.SetWriteSingleCoil().Write("32", coil);


                //写入可能有一定的延时，500毫秒后检验
                await Task.Delay(1000);

                //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
                var read_short_number = client.SetReadHoldingRegisters().ReadInt16("0");
                Assert.True(read_short_number.Value == short_number);
                Assert.True(client.SetReadHoldingRegisters().ReadUInt16("4").Value == ushort_number);
                Assert.True(client.SetReadHoldingRegisters().ReadInt32("8").Value == int_number);
                Assert.True(client.SetReadHoldingRegisters().ReadUInt32("12").Value == uint_number);
                Assert.True(client.SetReadHoldingRegisters().ReadInt64("16").Value == long_number);
                Assert.True(client.SetReadHoldingRegisters().ReadUInt64("20").Value == ulong_number);
                Assert.True(client.SetReadHoldingRegisters().ReadFloat("24").Value == float_number);
                //Assert.True(client.ReadDouble("28").Value == double_number);
                Assert.True(client.SetReadCoils().ReadBoolean("32").Value == coil);

                //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).Value == orderCode);
            }
        }

        [Fact]
        public async Task 长连接主动开关()
        {
            client.Connect();

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
                float float_number = rnd.Next(int.MinValue, int.MaxValue) / 100;
                double double_number = (double)rnd.Next(int.MinValue, int.MaxValue) / 100;
                bool coil = int_number % 2 == 0;
                #endregion
                client.SetStationNumber(1);
                //写入地址:0 值为:short_number 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
                var w1 = client.SetWriteSingleRegister().Write("0", short_number);
                var w2 = client.SetWriteSingleRegister().Write("4", ushort_number);
                var w3 = client.SetWriteMultipleRegister().Write("8", int_number);
                var w4 = client.SetWriteMultipleRegister().Write("12", uint_number);
                var w5 = client.SetWriteMultipleRegister().Write("16", long_number);
                var w6 = client.SetWriteMultipleRegister().Write("20", ulong_number);
                var w7 = client.SetWriteMultipleRegister().Write("24", float_number);
                var w8 = client.SetWriteMultipleRegister().Write("28", double_number);

                var oo1 = client.SetWriteSingleCoil().Write("32", coil);


                //写入可能有一定的延时，500毫秒后检验
                await Task.Delay(1000);

                //读取地址:0 站号:stationNumber 功能码:默认16(也可以自己传入对应的功能码)
                var read_short_number = client.SetReadHoldingRegisters().ReadInt16("0");
                Assert.True(read_short_number.Value == short_number);
                Assert.True(client.SetReadHoldingRegisters().ReadUInt16("4").Value == ushort_number);
                Assert.True(client.SetReadHoldingRegisters().ReadInt32("8").Value == int_number);
                Assert.True(client.SetReadHoldingRegisters().ReadUInt32("12").Value == uint_number);
                Assert.True(client.SetReadHoldingRegisters().ReadInt64("16").Value == long_number);
                Assert.True(client.SetReadHoldingRegisters().ReadUInt64("20").Value == ulong_number);
                Assert.True(client.SetReadHoldingRegisters().ReadFloat("24").Value == float_number);
                //Assert.True(client.ReadDouble("28").Value == double_number);
                Assert.True(client.SetReadCoils().ReadBoolean("32").Value == coil);

                //Assert.True(client.ReadString("100", stationNumber, readLength: (ushort)orderCode.Length).Value == orderCode);
            }
            client.Disconnect();
        }

        [Fact]
        public void 批量读取()
        {
            var result1 = client.ReadInt16("12");

            client.WarningLog = (msg, ex) =>
            {
                string aa = msg;
            };

            var list = new List<ModbusInput>();
            list.Add(new ModbusInput()
            {
                Address = "2",
                DataType = DataTypeEnum.Int16,
                FunctionCode = 3,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "2",
                DataType = DataTypeEnum.Int16,
                FunctionCode = 4,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "5",
                DataType = DataTypeEnum.Int16,
                FunctionCode = 3,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "199",
                DataType = DataTypeEnum.Int16,
                FunctionCode = 3,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "200",
                DataType = DataTypeEnum.Bool,
                FunctionCode = 2,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "201",
                DataType = DataTypeEnum.Bool,
                FunctionCode = 2,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "202",
                DataType = DataTypeEnum.Bool,
                FunctionCode = 2,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "203",
                DataType = DataTypeEnum.Bool,
                FunctionCode = 2,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "204",
                DataType = DataTypeEnum.Bool,
                FunctionCode = 2,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "205",
                DataType = DataTypeEnum.Bool,
                FunctionCode = 2,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "206",
                DataType = DataTypeEnum.Bool,
                FunctionCode = 2,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "207",
                DataType = DataTypeEnum.Bool,
                FunctionCode = 2,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                Address = "208",
                DataType = DataTypeEnum.Bool,
                FunctionCode = 2,
                StationNumber = 1
            });
            var result = client.BatchRead(list);
        }
    }
}

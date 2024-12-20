﻿
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

using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    public class SiemensClient_Smart200_Tests
    {
        private IEthernetClient client;
        public SiemensClient_Smart200_Tests()
        {
          var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)//CategoryName以System开头的所有日志输出级别为Warning
                    .AddFilter<ConsoleLoggerProvider>("Wombat.Socket.TestTcpSocketServer", LogLevel.Debug)
                    .AddConsole();//在loggerFactory中添加 ConsoleProvider
            });
            //var ip = IPAddress.Parse("192.168.1.180");
            //var port = int.Parse("102");
            var ip = IPAddress.Parse("192.168.2.70");//20.205.243.166
            var  port = 102;
            client = new SiemensClient(SiemensVersion.S7_200Smart, new IPEndPoint(ip, port));
            client.ReceiveTimeout = TimeSpan.FromSeconds(10);
            client.SendTimeout = TimeSpan.FromSeconds(10);
            client.UseLogger(loggerFactory.CreateLogger<SiemensClient>());
        }

        [Fact]
        public void 短连接自动开关()
        {



            client.IsLongLivedConnection = false;

            ReadWrite();
        }

        [Fact]
        public void 长连接主动开关()
        {
            client.IsLongLivedConnection = true;

            var tt =   client.Connect();
            ReadWrite();
            client?.Disconnect();
        }

        private void ReadWrite()
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


                var ssss2111 = client.Write("VW620", 10);
                var ssss21113 = client.Write("VW622", 10);

                var ssss2 = client.Write("Q1.3", true);
                var ssss3 = client.Write("db1.dbx0.0", true);

                Assert.True(client.ReadBoolean("Q1.3").Value == true);
                client.Write("Q1.4", bool_value);
                Assert.True(client.ReadBoolean("Q1.4").Value == bool_value);
                client.Write("Q1.5", !bool_value);
                Assert.True(client.ReadBoolean("Q1.5").Value == !bool_value);

                client.Write("V700", short_number);
                Assert.True(client.ReadInt16("V700").Value == short_number);
                client.Write("V700", short_number_1);
                Assert.True(client.ReadUInt16("V700").Value == short_number_1);

                client.Write("V700", int_number);
                Assert.True(client.ReadInt32("V700").Value == int_number);
                client.Write("V700", int_number_1);
                Assert.True(client.ReadUInt32("V700").Value == int_number_1);

                client.Write("V700", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64("V700").Value == Convert.ToInt64(int_number));
                client.Write("V700", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64("V700").Value == Convert.ToUInt64(int_number_1));

                client.Write("V700", float_number);
                Assert.True(client.ReadFloat("V700").Value == float_number);
                client.Write("V700", Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble("V700").Value == Convert.ToDouble(float_number));

                //var rrr =  client.Write("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).Value == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                client.Write("V700", bool_values);
                var bool_values_result = client.ReadBoolean("V700", bool_values.Length);
                for (int j = 0; j < bool_values_result.Value.Length; j++)
                {
                    Assert.True(bool_values_result.Value[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", short_values);
                var short_values_result = client.ReadInt16("V700", short_values.Length);
                for (int j = 0; j < short_values_result.Value.Length; j++)
                {
                    Assert.True(short_values_result.Value[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", ushort_values);
                var ushort_values_result = client.ReadInt16("V700", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Value.Length; j++)
                {
                    Assert.True(ushort_values_result.Value[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", int_values);
                var int_values_result = client.ReadInt32("V700", int_values.Length);
                for (int j = 0; j < int_values_result.Value.Length; j++)
                {
                    Assert.True(int_values_result.Value[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", uint_values);
                var uint_values_result = client.ReadUInt32("V700", uint_values.Length);
                for (int j = 0; j < uint_values_result.Value.Length; j++)
                {
                    Assert.True(uint_values_result.Value[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", long_values);
                var long_values_result = client.ReadInt64("V700", long_values.Length);
                for (long j = 0; j < long_values_result.Value.Length; j++)
                {
                    Assert.True(long_values_result.Value[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", ulong_values);
                var ulong_values_result = client.ReadUInt64("V700", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Value.Length; j++)
                {
                    Assert.True(ulong_values_result.Value[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", float_values);
                var float_values_result = client.ReadFloat("V700", float_values.Length);
                for (int j = 0; j < float_values_result.Value.Length; j++)
                {
                    Assert.True(float_values_result.Value[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", double_values);
                var double_values_result = client.ReadDouble("V700", double_values.Length);
                for (int j = 0; j < double_values_result.Value.Length; j++)
                {
                    Assert.True(double_values_result.Value[j] == double_values[j]);

                }



            }

        }

        [Fact]
        public void 长连接主动开关Async()
        {
            client.IsLongLivedConnection = true;

            var tt = client.Connect();
            var ssss1 = client.Write("Q1.3", true);
            var ssss2 = client.Write("Q1.3", true);
            var ssss3= client.Write("Q1.3", true);
            var ssss4 = client.Write("Q1.3", true);

            var ssssss1 = client.WriteAsync("Q1.3", true).Result;
            var ssssss2 = client.WriteAsync("Q1.3", true).Result;
            var ssssss3 = client.WriteAsync("Q1.3", true).Result;
            var ssssss4 = client.WriteAsync("Q1.3", true).Result;

            ReadWriteAsync().Wait();
            client?.Disconnect();
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
                Assert.True(client.ReadBooleanAsync("Q1.3").Result.Value == true);
                await client.WriteAsync("Q1.4", bool_value);
                Assert.True(client.ReadBooleanAsync("Q1.4").Result.Value == bool_value);
                await client.WriteAsync("Q1.5", !bool_value);
                Assert.True(client.ReadBooleanAsync("Q1.5").Result.Value == !bool_value);

                var ssss = await client.WriteAsync("V700", short_number);
                var tttt = client.ReadInt16Async("V700");
                Assert.True(client.ReadInt16Async("V700").Result.Value == short_number);
                await client.WriteAsync("V700", short_number_1);
                Assert.True(client.ReadUInt16Async("V700").Result.Value == short_number_1);

                await client.WriteAsync("V700", int_number);
                Assert.True(client.ReadInt32Async("V700").Result.Value == int_number);
                await client.WriteAsync("V700", int_number_1);
                Assert.True(client.ReadUInt32Async("V700").Result.Value == int_number_1);

                await client.WriteAsync("V700", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64Async("V700").Result.Value == Convert.ToInt64(int_number));
                await client.WriteAsync("V700", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64Async("V700").Result.Value == Convert.ToUInt64(int_number_1));

                await client.WriteAsync("V700", float_number);
                Assert.True(client.ReadFloatAsync("V700").Result.Value == float_number);
                await client.WriteAsync("V700", Convert.ToDouble(float_number));
                Assert.True(client.ReadDoubleAsync("V700").Result.Value == Convert.ToDouble(float_number));

                //var rrr =  await client.WriteAsync("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).Value == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                await client.WriteAsync("V700", bool_values);
                var bool_values_result = client.ReadBooleanAsync("V700", bool_values.Length);
                for (int j = 0; j < bool_values_result.Result.Value.Length; j++)
                {
                    Assert.True(bool_values_result.Result.Value[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", short_values);
                var short_values_result = client.ReadInt16Async("V700", short_values.Length);
                for (int j = 0; j < short_values_result.Result.Value.Length; j++)
                {
                    Assert.True(short_values_result.Result.Value[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", ushort_values);
                var ushort_values_result = client.ReadInt16Async("V700", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Result.Value.Length; j++)
                {
                    Assert.True(ushort_values_result.Result.Value[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", int_values);
                var int_values_result = client.ReadInt32Async("V700", int_values.Length);
                for (int j = 0; j < int_values_result.Result.Value.Length; j++)
                {
                    Assert.True(int_values_result.Result.Value[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", uint_values);
                var uint_values_result = client.ReadUInt32Async("V700", uint_values.Length);
                for (int j = 0; j < uint_values_result.Result.Value.Length; j++)
                {
                    Assert.True(uint_values_result.Result.Value[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", long_values);
                var long_values_result = client.ReadInt64Async("V700", long_values.Length);
                for (long j = 0; j < long_values_result.Result.Value.Length; j++)
                {
                    Assert.True(long_values_result.Result.Value[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", ulong_values);
                var ulong_values_result = client.ReadUInt64Async("V700", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Result.Value.Length; j++)
                {
                    Assert.True(ulong_values_result.Result.Value[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", float_values);
                var float_values_result = client.ReadFloatAsync("V700", float_values.Length);
                for (int j = 0; j < float_values_result.Result.Value.Length; j++)
                {
                    Assert.True(float_values_result.Result.Value[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", double_values);
                var double_values_result = client.ReadDoubleAsync("V700", double_values.Length);
                for (int j = 0; j < double_values_result.Result.Value.Length; j++)
                {
                    Assert.True(double_values_result.Result.Value[j] == double_values[j]);

                }



            }

        }

        [Fact]
        public void 批量读写()
        {
          var sss =  client.Connect();

            //client.WarningLog = (msg, ex) =>
            //{
            //    string aa = msg;
            //};

            var re = new Random(DateTime.Now.Second);

            var number0 = re.Next(0, 255) % 2 == 1;
            var number1 = re.Next(0, 255) % 2 == 1;
            var number2 = re.Next(0, 255) % 2 == 1;
            var number3 = re.Next(0, 255) % 2 == 1;
            var number4 = re.Next(0, 255) % 2 == 1;
            var number5 = re.Next(0, 255) % 2 == 1;
            var number6 = re.Next(0, 255) % 2 == 1;
            var number7 = re.Next(0, 255) % 2 == 1;
            byte byte1 = (byte)re.Next(0, 255);
            byte byte2 = (byte)re.Next(0, 255);
            byte byte3 = (byte)re.Next(0, 255);
            float V2642 = (float)re.Next(0, 255);
            float V2646 = (float)re.Next(0, 255);
            float V2650 = (float)re.Next(0, 255);

            Dictionary<string, object> writeAddresses = new Dictionary<string, object>();
            writeAddresses.Add("V2634.0", number0);
            writeAddresses.Add("V2634.1", number1);
            writeAddresses.Add("V2634.2", number2);
            writeAddresses.Add("V2634.3", number3);
            writeAddresses.Add("V2634.4", number4);
            writeAddresses.Add("V2634.5", number5);
            writeAddresses.Add("V2634.6", number6);
            writeAddresses.Add("V2634.7", number7);
            writeAddresses.Add("V2642", V2642);
            writeAddresses.Add("V2646", V2646);
            writeAddresses.Add("V2650", V2650);
            writeAddresses.Add("V2654", V2650);
            writeAddresses.Add("V2658", V2650);
            writeAddresses.Add("V2662", V2650);
            writeAddresses.Add("V2666", V2650);
            writeAddresses.Add("V2670", V2650);
            writeAddresses.Add("V2674", V2650);
            writeAddresses.Add("V1650", byte1);
            writeAddresses.Add("V1651", byte2);
            writeAddresses.Add("V1652", byte3);
           var w1 =  client.BatchWrite(writeAddresses);

            Dictionary<string, DataTypeEnums> readAddresses = new Dictionary<string, DataTypeEnums>();
            readAddresses.Add("V2634.0", DataTypeEnums.Bool);
            readAddresses.Add("V2634.1", DataTypeEnums.Bool);
            readAddresses.Add("V2634.2", DataTypeEnums.Bool);
            readAddresses.Add("V2634.3", DataTypeEnums.Bool);
            readAddresses.Add("V2634.4", DataTypeEnums.Bool);
            readAddresses.Add("V2634.5", DataTypeEnums.Bool);
            readAddresses.Add("V2634.6", DataTypeEnums.Bool);
            readAddresses.Add("V2634.7", DataTypeEnums.Bool);
            readAddresses.Add("V2642", DataTypeEnums.Float);
            readAddresses.Add("V2646", DataTypeEnums.Float);
            readAddresses.Add("V2650", DataTypeEnums.Float);
            readAddresses.Add("V2654", DataTypeEnums.Float);
            readAddresses.Add("V2658", DataTypeEnums.Float);
            readAddresses.Add("V2662", DataTypeEnums.Float);
            readAddresses.Add("V2666", DataTypeEnums.Float);
            readAddresses.Add("V2670", DataTypeEnums.Float);
            readAddresses.Add("V2674", DataTypeEnums.Float);
            readAddresses.Add("V1650", DataTypeEnums.Byte);
            readAddresses.Add("V1651", DataTypeEnums.Byte);
            readAddresses.Add("V1652", DataTypeEnums.Byte);

            var result = client.BatchRead(readAddresses);

            Assert.True(Convert.ToBoolean(result.Value["V2634.0"]) == number0);
            Assert.True(Convert.ToBoolean(result.Value["V2634.1"]) == number1);
            Assert.True(Convert.ToBoolean(result.Value["V2634.2"]) == number2);
            Assert.True(Convert.ToBoolean(result.Value["V2634.3"]) == number3);
            Assert.True(Convert.ToBoolean(result.Value["V2634.4"]) == number4);
            Assert.True(Convert.ToBoolean(result.Value["V2634.5"]) == number5);
            Assert.True(Convert.ToBoolean(result.Value["V2634.6"]) == number6);
            Assert.True(Convert.ToBoolean(result.Value["V2634.7"]) == number7);
            Assert.True(Convert.ToSingle(result.Value["V2642"]) == V2642);
            Assert.True(Convert.ToSingle(result.Value["V2646"]) == V2646);
            Assert.True(Convert.ToSingle(result.Value["V2650"]) == V2650);
            Assert.True(Convert.ToSingle(result.Value["V2654"]) == V2650);
            Assert.True(Convert.ToSingle(result.Value["V2658"]) == V2650);
            Assert.True(Convert.ToSingle(result.Value["V2662"]) == V2650);
            Assert.True(Convert.ToSingle(result.Value["V2666"]) == V2650);
            Assert.True(Convert.ToSingle(result.Value["V2670"]) == V2650);
            Assert.True(Convert.ToSingle(result.Value["V2674"]) == V2650);
            Assert.True(Convert.ToByte(result.Value["V1650"]) == byte1);
            Assert.True(Convert.ToByte(result.Value["V1651"]) == byte2);
            Assert.True(Convert.ToByte(result.Value["V1652"]) == byte3);
            client?.Disconnect();
        }

        [Fact]
        public void test()
        {
            //string address = "I1.1";
            //ushort readNumber = 20;
            //test2(address, readNumber);

            //TODO 最多只能批量读取 19个？
            Dictionary<string, DataTypeEnums> addresses = new Dictionary<string, DataTypeEnums>();

            //addresses.Add("V1000", DataTypeEnums.Float);
            //addresses.Add("I0.0", DataTypeEnums.Bool);
            //addresses.Add("V4109", DataTypeEnums.Byte);
            //addresses.Add("V1004", DataTypeEnums.Float);

            //addresses.Add("V1000", DataTypeEnums.Float);
            //addresses.Add("V1004", DataTypeEnums.Float);
            //addresses.Add("V1008", DataTypeEnums.Float);
            //addresses.Add("V1012", DataTypeEnums.Float);
            //addresses.Add("V1016", DataTypeEnums.Float);
            //addresses.Add("V1020", DataTypeEnums.Float);
            //addresses.Add("V1024", DataTypeEnums.Float);
            //addresses.Add("V1032", DataTypeEnums.Float);
            //addresses.Add("V1036", DataTypeEnums.Float);
            //addresses.Add("V1040", DataTypeEnums.Float);
            //addresses.Add("V1044", DataTypeEnums.Float);
            //addresses.Add("V1048", DataTypeEnums.Float);
            //addresses.Add("V1052", DataTypeEnums.Float);
            //addresses.Add("V1230", DataTypeEnums.Float);
            //addresses.Add("V1234", DataTypeEnums.Float);
            //addresses.Add("V1238", DataTypeEnums.Float);
            //addresses.Add("V1242", DataTypeEnums.Float);
            //addresses.Add("V1246", DataTypeEnums.Float);
            //addresses.Add("V1250", DataTypeEnums.Float);

            //addresses.Add("V1254", DataTypeEnums.Float);
            //addresses.Add("V1258", DataTypeEnums.Float);


            //addresses.Add("V1012", DataTypeEnums.Float);
            //addresses.Add("V1076 ", DataTypeEnums.UInt32);
            //addresses.Add("V5056 ", DataTypeEnums.Float);
            //addresses.Add("V5232 ", DataTypeEnums.Float);         

            //addresses.Add("I0.0 ", DataTypeEnums.Bool);
            //addresses.Add("I0.1 ", DataTypeEnums.Bool);
            //addresses.Add("I0.2 ", DataTypeEnums.Bool);
            //addresses.Add("I0.3 ", DataTypeEnums.Bool);
            //addresses.Add("I0.4 ", DataTypeEnums.Bool);
            //addresses.Add("I0.5 ", DataTypeEnums.Bool);
            //addresses.Add("I0.6 ", DataTypeEnums.Bool);
            //addresses.Add("I0.7 ", DataTypeEnums.Bool);

            //addresses.Add("I1.0 ", DataTypeEnums.Bool);
            //addresses.Add("I1.1 ", DataTypeEnums.Bool);
            //addresses.Add("I1.2 ", DataTypeEnums.Bool);
            //addresses.Add("I1.3 ", DataTypeEnums.Bool);
            //addresses.Add("I1.4 ", DataTypeEnums.Bool);
            //addresses.Add("I1.5 ", DataTypeEnums.Bool);
            //addresses.Add("I1.6 ", DataTypeEnums.Bool);
            //addresses.Add("I1.7 ", DataTypeEnums.Bool);


            //client.Write("DB4.0", (float)6);
            //client.Write("DB4.12", (float)9);
            //client.Write("DB1.410.0", false);
            //client.Write("DB1.410.0", true);

            var result = client.BatchRead(addresses);

            Dictionary<string, object> newAddresses = new Dictionary<string, object>();
            newAddresses.Add("DB4.24", (float)1);
            newAddresses.Add("DB4.0", (float)2);
            newAddresses.Add("DB1.434.0", true);
            newAddresses.Add("DB1.482.0", true);
            newAddresses.Add("DB4.12", (float)3);
            newAddresses.Add("DB1.410.0", true);
            var result1 = client.BatchWrite(newAddresses);

            var r3 = client.Write("DB1.482.0", false);
            var result2 = client.Write("DB1.434.0", false);
            client.Write("DB1.434.0", true);
        }

        [Fact]
        public void 定时更新()
        {
            //client.ReadInt16("V700", (value,isSucceed,err) =>
            //{
            //    Debug.WriteLine($"V700:{value}  err:{err}");
            //});

            //client.ReadInt16("V102", (value, isSucceed, err) =>
            //{
            //    Debug.WriteLine($"V102:{value}  err:{err}");
            //});

            //Thread.Sleep(1000 * 30);
        }
    }
}

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
    public class SiemensClient_1200_Tests
    {
        private IEthernetClient client;
        private IEthernetClient client2;

        public SiemensClient_1200_Tests()
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
            var ip = IPAddress.Parse("192.168.2.41");//20.205.243.166
            var  port = 102;

            client = new SiemensClient(SiemensVersion.S7_1200, new IPEndPoint(ip, port));
            client.UseLogger(loggerFactory.CreateLogger<SiemensClient>());


            var ip2 = IPAddress.Parse("192.168.2.30");//20.205.243.166
            client2 = new SiemensClient(SiemensVersion.S7_200Smart, new IPEndPoint(ip2, port));
            client2.UseLogger(loggerFactory.CreateLogger<SiemensClient>());

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
            client2.IsLongLivedConnection = true;

            var tt =   client.Connect();
            var cc = client2.Connect();

            ReadWrite();
            client?.Disconnect();
        }

        private async void ReadWrite()
        {

            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            for (int i = 0; i < 100; i++)
            {
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                ushort short_number_1 = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);

                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                uint int_number_1 = (uint)rnd.Next(0, int.MaxValue);

                float float_number = int_number / 100;
                var bool_value = short_number % 2 == 1;

                string value_string = "BennyZhao";



                var tttt6662 =await client.ReadByteAsync("DB50.DBW0", 1000);
                //var sss =await  client2.WriteAsync("VB0", tttt6662.Value);

                var ssss2 = client.Write("Q0.3", true);
                Assert.True(client.ReadBoolean("Q0.3").Value == true);
                client.Write("Q0.4", bool_value);
                Assert.True(client.ReadBoolean("Q0.4").Value == bool_value);
                client.Write("Q0.5", !bool_value);
                Assert.True(client.ReadBoolean("Q0.5").Value == !bool_value);

                var ssss = client.Write("DB50.DBW0", short_number);
                var tttt = client.ReadInt16("DB50.DBW0");
                Assert.True(client.ReadInt16("DB50.DBW0").Value == short_number);
                client.Write("DB50.DBW0", short_number_1);
                var tttt2 = client.ReadUInt16("DB50.DBW0").Value;
                Assert.True(client.ReadUInt16("DB50.DBW0").Value == short_number_1);

                client.Write("DB50.DBD0", int_number);
                Assert.True(client.ReadInt32("DB50.DBD0").Value == int_number);
                client.Write("DB50.DBD0", int_number_1);
                Assert.True(client.ReadUInt32("DB50.DBD0").Value == int_number_1);

                client.Write("DB50.DBD0", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64("DB50.DBD0").Value == Convert.ToInt64(int_number));
                client.Write("DB50.DBD0", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64("DB50.DBD0").Value == Convert.ToUInt64(int_number_1));

                client.Write("DB50.DBD0", float_number);
                Assert.True(client.ReadFloat("DB50.DBD0").Value == float_number);
                client.Write("DB50.DBD0", Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble("DB50.DBD0").Value == Convert.ToDouble(float_number));

                //var rrr =  client.Write("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).Value == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                client.Write("DB50", bool_values);
                var bool_values_result = client.ReadBoolean("DB50", bool_values.Length);
                for (int j = 0; j < bool_values_result.Value.Length; j++)
                {
                    Assert.True(bool_values_result.Value[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("DB50", short_values);
                var short_values_result = client.ReadInt16("DB50", short_values.Length);
                for (int j = 0; j < short_values_result.Value.Length; j++)
                {
                    Assert.True(short_values_result.Value[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("DB50", ushort_values);
                var ushort_values_result = client.ReadInt16("DB50", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Value.Length; j++)
                {
                    Assert.True(ushort_values_result.Value[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("DB50", int_values);
                var int_values_result = client.ReadInt32("DB50", int_values.Length);
                for (int j = 0; j < int_values_result.Value.Length; j++)
                {
                    Assert.True(int_values_result.Value[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("DB50", uint_values);
                var uint_values_result = client.ReadUInt32("DB50", uint_values.Length);
                for (int j = 0; j < uint_values_result.Value.Length; j++)
                {
                    Assert.True(uint_values_result.Value[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("DB50", long_values);
                var long_values_result = client.ReadInt64("DB50", long_values.Length);
                for (long j = 0; j < long_values_result.Value.Length; j++)
                {
                    Assert.True(long_values_result.Value[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("DB50", ulong_values);
                var ulong_values_result = client.ReadUInt64("DB50", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Value.Length; j++)
                {
                    Assert.True(ulong_values_result.Value[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("DB50", float_values);
                var float_values_result = client.ReadFloat("DB50", float_values.Length);
                for (int j = 0; j < float_values_result.Value.Length; j++)
                {
                    Assert.True(float_values_result.Value[j] == float_values[j]);

                }
                double[] double_values = { 1.2222, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("DB50", double_values);
                var double_values_result = client.ReadDouble("DB50", double_values.Length);
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

                var ssss = await client.WriteAsync("DB50", short_number);
                var tttt = client.ReadInt16Async("DB50");
                Assert.True(client.ReadInt16Async("DB50").Result.Value == short_number);
                await client.WriteAsync("DB50", short_number_1);
                Assert.True(client.ReadUInt16Async("DB50").Result.Value == short_number_1);

                await client.WriteAsync("DB50", int_number);
                Assert.True(client.ReadInt32Async("DB50").Result.Value == int_number);
                await client.WriteAsync("DB50", int_number_1);
                Assert.True(client.ReadUInt32Async("DB50").Result.Value == int_number_1);

                await client.WriteAsync("DB50", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64Async("DB50").Result.Value == Convert.ToInt64(int_number));
                await client.WriteAsync("DB50", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64Async("DB50").Result.Value == Convert.ToUInt64(int_number_1));

                await client.WriteAsync("DB50", float_number);
                Assert.True(client.ReadFloatAsync("DB50").Result.Value == float_number);
                await client.WriteAsync("DB50", Convert.ToDouble(float_number));
                Assert.True(client.ReadDoubleAsync("DB50").Result.Value == Convert.ToDouble(float_number));

                //var rrr =  await client.WriteAsync("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).Value == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                await client.WriteAsync("DB50", bool_values);
                var bool_values_result = client.ReadBooleanAsync("DB50", bool_values.Length);
                for (int j = 0; j < bool_values_result.Result.Value.Length; j++)
                {
                    Assert.True(bool_values_result.Result.Value[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("DB50", short_values);
                var short_values_result = client.ReadInt16Async("DB50", short_values.Length);
                for (int j = 0; j < short_values_result.Result.Value.Length; j++)
                {
                    Assert.True(short_values_result.Result.Value[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("DB50", ushort_values);
                var ushort_values_result = client.ReadInt16Async("DB50", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Result.Value.Length; j++)
                {
                    Assert.True(ushort_values_result.Result.Value[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("DB50", int_values);
                var int_values_result = client.ReadInt32Async("DB50", int_values.Length);
                for (int j = 0; j < int_values_result.Result.Value.Length; j++)
                {
                    Assert.True(int_values_result.Result.Value[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("DB50", uint_values);
                var uint_values_result = client.ReadUInt32Async("DB50", uint_values.Length);
                for (int j = 0; j < uint_values_result.Result.Value.Length; j++)
                {
                    Assert.True(uint_values_result.Result.Value[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("DB50", long_values);
                var long_values_result = client.ReadInt64Async("DB50", long_values.Length);
                for (long j = 0; j < long_values_result.Result.Value.Length; j++)
                {
                    Assert.True(long_values_result.Result.Value[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("DB50", ulong_values);
                var ulong_values_result = client.ReadUInt64Async("DB50", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Result.Value.Length; j++)
                {
                    Assert.True(ulong_values_result.Result.Value[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("DB50", float_values);
                var float_values_result = client.ReadFloatAsync("DB50", float_values.Length);
                for (int j = 0; j < float_values_result.Result.Value.Length; j++)
                {
                    Assert.True(float_values_result.Result.Value[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("DB50", double_values);
                var double_values_result = client.ReadDoubleAsync("DB50", double_values.Length);
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
            var V2642 = (float)re.Next(0, 255);
            var V2646 = (float)re.Next(0, 255);
            var V2650 = (float)re.Next(0, 255);

            Dictionary<string, object> writeAddresses = new Dictionary<string, object>();
            writeAddresses.Add("DB23.DBX0.0", number0);
            writeAddresses.Add("DB23.DBX0.1", number1);
            writeAddresses.Add("DB23.DBX0.2", number2);
            writeAddresses.Add("DB23.DBX0.3", number3);
            writeAddresses.Add("DB23.DBX0.4", number4);
            writeAddresses.Add("DB23.DBX1.0", number5);
            writeAddresses.Add("DB23.DBX2.0", number6);
            writeAddresses.Add("DB23.DBX3.0", number7);
            writeAddresses.Add("DB23.DBB4", byte1);
            writeAddresses.Add("DB23.DBB5", byte2);
            writeAddresses.Add("DB23.DBB6", byte3);
            writeAddresses.Add("DB23.DBD10", V2642);
            writeAddresses.Add("DB23.DBD14", V2646);
            writeAddresses.Add("DB23.DBD18", V2650);
            writeAddresses.Add("DB23.DBD30", V2642);
            writeAddresses.Add("DB23.DBD34", V2646);
            writeAddresses.Add("DB23.DBD38", V2650);
            writeAddresses.Add("DB23.DBD50", V2642);
            writeAddresses.Add("DB23.DBD54", V2646);
            writeAddresses.Add("DB23.DBD58", V2650);
            writeAddresses.Add("DB23.DBD70", V2642);
            writeAddresses.Add("DB23.DBD74", V2646);
            writeAddresses.Add("DB23.DBD78", V2650);
            writeAddresses.Add("DB23.DBD90", V2642);
            writeAddresses.Add("DB23.DBD94", V2646);
            writeAddresses.Add("DB23.DBD98", V2650);

           var w= client.BatchWrite(writeAddresses);

            Dictionary<string, DataTypeEnums> readAddresses = new Dictionary<string, DataTypeEnums>();

            readAddresses.Add("DB23.DBX0.0", DataTypeEnums.Bool);
            readAddresses.Add("DB23.DBX0.1", DataTypeEnums.Bool);
            readAddresses.Add("DB23.DBX0.2", DataTypeEnums.Bool);
            readAddresses.Add("DB23.DBX0.3", DataTypeEnums.Bool);
            readAddresses.Add("DB23.DBX0.4", DataTypeEnums.Bool);
            readAddresses.Add("DB23.DBX1.0", DataTypeEnums.Bool);
            readAddresses.Add("DB23.DBX2.0", DataTypeEnums.Bool);
            readAddresses.Add("DB23.DBX3.0", DataTypeEnums.Bool);
            readAddresses.Add("DB23.DBB4", DataTypeEnums.Byte);
            readAddresses.Add("DB23.DBB5", DataTypeEnums.Byte);
            readAddresses.Add("DB23.DBB6", DataTypeEnums.Byte);
            readAddresses.Add("DB23.DBD10", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD14", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD18", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD30", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD34", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD38", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD50", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD54", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD58", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD70", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD74", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD78", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD90", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD94", DataTypeEnums.Float);
            readAddresses.Add("DB23.DBD98", DataTypeEnums.Float);

            var result = client.BatchRead(readAddresses);

            Assert.True(Convert.ToBoolean(result.Value["DB23.DBX0.0"]) == number0);
            Assert.True(Convert.ToBoolean(result.Value["DB23.DBX0.1"]) == number1);
            Assert.True(Convert.ToBoolean(result.Value["DB23.DBX0.2"]) == number2);
            Assert.True(Convert.ToBoolean(result.Value["DB23.DBX0.3"]) == number3);
            Assert.True(Convert.ToBoolean(result.Value["DB23.DBX0.4"]) == number4);
            Assert.True(Convert.ToBoolean(result.Value["DB23.DBX1.0"]) == number5);
            Assert.True(Convert.ToBoolean(result.Value["DB23.DBX2.0"]) == number6);
            Assert.True(Convert.ToBoolean(result.Value["DB23.DBX3.0"]) == number7);
            Assert.True(Convert.ToByte(result.Value["DB23.DBB4"]) == byte1);
            Assert.True(Convert.ToByte(result.Value["DB23.DBB5"]) == byte2);
            Assert.True(Convert.ToByte(result.Value["DB23.DBB6"]) == byte3);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD10"]) == V2642);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD14"]) == V2646);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD18"]) == V2650);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD30"]) == V2642);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD34"]) == V2646);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD38"]) == V2650);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD50"]) == V2642);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD54"]) == V2646);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD58"]) == V2650);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD70"]) == V2642);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD74"]) == V2646);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD78"]) == V2650);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD90"]) == V2642);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD94"]) == V2646);
            Assert.True(Convert.ToSingle(result.Value["DB23.DBD98"]) == V2650);

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
            //client.ReadInt16("DB50", (value,isSucceed,err) =>
            //{
            //    Debug.WriteLine($"DB50:{value}  err:{err}");
            //});

            //client.ReadInt16("V102", (value, isSucceed, err) =>
            //{
            //    Debug.WriteLine($"V102:{value}  err:{err}");
            //});

            //Thread.Sleep(1000 * 30);
        }
    }
}


using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;
using Wombat.Infrastructure;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    public class SiemensClient_1200_Tests
    {
        private IEthernetClient client;
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
            var ip = IPAddress.Parse("192.168.2.12");//20.205.243.166
            var  port = 102;
            client = new SiemensClient(SiemensVersion.S7_1200, new IPEndPoint(ip, port));
            client.UseLogger(loggerFactory.CreateLogger<SiemensClient>());
        }

        [Fact]
        public void 短连接自动开关()
        {



            client.IsUseLongConnect = false;

            ReadWrite();
        }

        [Fact]
        public void 长连接主动开关()
        {
            client.IsUseLongConnect = true;

            var tt =   client.Connect();
            ReadWrite();
            client?.Disconnect();
        }

        private void ReadWrite()
        {

            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            for (int i = 0; i < 1000; i++)
            {
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                ushort short_number_1 = (ushort)rnd.Next(ushort.MinValue, ushort.MaxValue);

                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                uint int_number_1 = (uint)rnd.Next(0, int.MaxValue);

                float float_number = int_number / 100;
                var bool_value = short_number % 2 == 1;

                string value_string = "BennyZhao";

               var ssss2 = client.Write("Q1.3", true);
                Assert.True(client.ReadBoolean("Q1.3").Value == true);
                client.Write("Q1.4", bool_value);
                Assert.True(client.ReadBoolean("Q1.4").Value == bool_value);
                client.Write("Q1.5", !bool_value);
                Assert.True(client.ReadBoolean("Q1.5").Value == !bool_value);

                var ssss = client.Write("DB50", short_number);
                var tttt = client.ReadInt16("DB50");
                Assert.True(client.ReadInt16("DB50").Value == short_number);
                client.Write("DB50", short_number_1);
                var tttt2 = client.ReadUInt16("DB50").Value;
                Assert.True(client.ReadUInt16("DB50").Value == short_number_1);

                client.Write("DB50", int_number);
                Assert.True(client.ReadInt32("DB50").Value == int_number);
                client.Write("DB50", int_number_1);
                Assert.True(client.ReadUInt32("DB50").Value == int_number_1);

                client.Write("DB50", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64("DB50").Value == Convert.ToInt64(int_number));
                client.Write("DB50", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64("DB50").Value == Convert.ToUInt64(int_number_1));

                client.Write("DB50", float_number);
                Assert.True(client.ReadFloat("DB50").Value == float_number);
                client.Write("DB50", Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble("DB50").Value == Convert.ToDouble(float_number));

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
            client.IsUseLongConnect = true;

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
            client.BatchWrite(writeAddresses);

            Dictionary<string, DataTypeEnum> readAddresses = new Dictionary<string, DataTypeEnum>();
            readAddresses.Add("V2634.0", DataTypeEnum.Bool);
            readAddresses.Add("V2634.1", DataTypeEnum.Bool);
            readAddresses.Add("V2634.2", DataTypeEnum.Bool);
            readAddresses.Add("V2634.3", DataTypeEnum.Bool);
            readAddresses.Add("V2634.4", DataTypeEnum.Bool);
            readAddresses.Add("V2634.5", DataTypeEnum.Bool);
            readAddresses.Add("V2634.6", DataTypeEnum.Bool);
            readAddresses.Add("V2634.7", DataTypeEnum.Bool);
            readAddresses.Add("V2642", DataTypeEnum.Float);
            readAddresses.Add("V2646", DataTypeEnum.Float);
            readAddresses.Add("V2650", DataTypeEnum.Float);
            readAddresses.Add("V2654", DataTypeEnum.Float);
            readAddresses.Add("V2658", DataTypeEnum.Float);
            readAddresses.Add("V2662", DataTypeEnum.Float);
            readAddresses.Add("V2666", DataTypeEnum.Float);
            readAddresses.Add("V2670", DataTypeEnum.Float);
            readAddresses.Add("V2674", DataTypeEnum.Float);
            readAddresses.Add("V1650", DataTypeEnum.Byte);
            readAddresses.Add("V1651", DataTypeEnum.Byte);
            readAddresses.Add("V1652", DataTypeEnum.Byte);

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
            Dictionary<string, DataTypeEnum> addresses = new Dictionary<string, DataTypeEnum>();

            //addresses.Add("V1000", DataTypeEnum.Float);
            //addresses.Add("I0.0", DataTypeEnum.Bool);
            //addresses.Add("V4109", DataTypeEnum.Byte);
            //addresses.Add("V1004", DataTypeEnum.Float);

            //addresses.Add("V1000", DataTypeEnum.Float);
            //addresses.Add("V1004", DataTypeEnum.Float);
            //addresses.Add("V1008", DataTypeEnum.Float);
            //addresses.Add("V1012", DataTypeEnum.Float);
            //addresses.Add("V1016", DataTypeEnum.Float);
            //addresses.Add("V1020", DataTypeEnum.Float);
            //addresses.Add("V1024", DataTypeEnum.Float);
            //addresses.Add("V1032", DataTypeEnum.Float);
            //addresses.Add("V1036", DataTypeEnum.Float);
            //addresses.Add("V1040", DataTypeEnum.Float);
            //addresses.Add("V1044", DataTypeEnum.Float);
            //addresses.Add("V1048", DataTypeEnum.Float);
            //addresses.Add("V1052", DataTypeEnum.Float);
            //addresses.Add("V1230", DataTypeEnum.Float);
            //addresses.Add("V1234", DataTypeEnum.Float);
            //addresses.Add("V1238", DataTypeEnum.Float);
            //addresses.Add("V1242", DataTypeEnum.Float);
            //addresses.Add("V1246", DataTypeEnum.Float);
            //addresses.Add("V1250", DataTypeEnum.Float);

            //addresses.Add("V1254", DataTypeEnum.Float);
            //addresses.Add("V1258", DataTypeEnum.Float);


            //addresses.Add("V1012", DataTypeEnum.Float);
            //addresses.Add("V1076 ", DataTypeEnum.UInt32);
            //addresses.Add("V5056 ", DataTypeEnum.Float);
            //addresses.Add("V5232 ", DataTypeEnum.Float);         

            //addresses.Add("I0.0 ", DataTypeEnum.Bool);
            //addresses.Add("I0.1 ", DataTypeEnum.Bool);
            //addresses.Add("I0.2 ", DataTypeEnum.Bool);
            //addresses.Add("I0.3 ", DataTypeEnum.Bool);
            //addresses.Add("I0.4 ", DataTypeEnum.Bool);
            //addresses.Add("I0.5 ", DataTypeEnum.Bool);
            //addresses.Add("I0.6 ", DataTypeEnum.Bool);
            //addresses.Add("I0.7 ", DataTypeEnum.Bool);

            //addresses.Add("I1.0 ", DataTypeEnum.Bool);
            //addresses.Add("I1.1 ", DataTypeEnum.Bool);
            //addresses.Add("I1.2 ", DataTypeEnum.Bool);
            //addresses.Add("I1.3 ", DataTypeEnum.Bool);
            //addresses.Add("I1.4 ", DataTypeEnum.Bool);
            //addresses.Add("I1.5 ", DataTypeEnum.Bool);
            //addresses.Add("I1.6 ", DataTypeEnum.Bool);
            //addresses.Add("I1.7 ", DataTypeEnum.Bool);


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

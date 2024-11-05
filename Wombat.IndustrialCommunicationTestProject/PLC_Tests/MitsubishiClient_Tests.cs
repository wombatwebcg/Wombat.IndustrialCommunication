using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;

using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    public class MitsubishiClient_Tests
    {
        private MitsubishiClient client;
        string ip = "192.168.0.171";
        //string ip = "192.168.1.180";

        public MitsubishiClient_Tests()
        {
        }

        [Theory]
        [InlineData(MitsubishiVersion.Qna_3E, 5000)]
        //[InlineData(MitsubishiVersion.A_1E, 6001)]
        public void 短连接自动开关(MitsubishiVersion version, int port)
        {
            client = new MitsubishiClient(version, ip, port);
            client.IsLongLivedConnection = false;
            ReadWrite();
        }

        [Theory]
        [InlineData(MitsubishiVersion.Qna_3E, 5000)]
        //[InlineData(MitsubishiVersion.A_1E, 6001)]
        public void 长连接主动开关(MitsubishiVersion version, int port)
        {
            client = new MitsubishiClient(version, ip, port);

            client.Connect();
            client.IsLongLivedConnection = true;
         var sss =   client.Disconnect();

            ReadWrite();

            client?.Disconnect();
        }

        private void ReadWrite()
        {
            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            for (int i = 0; i < 1000; i++)
            {
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                float float_number = int_number / 100;
                var bool_value = short_number % 2 == 1;

                //client.Write("Y100", true);
                //Assert.True(client.ReadBoolean("Y100").Value == true);
                client.Write("M900", false);
                var tttttttt = client.Write("M900", true);
                var sss = client.ReadBoolean("M900");
                Assert.True(client.ReadBoolean("M900").Value == true);
                client.Write("M901", bool_value);
                Assert.True(client.ReadBoolean("M901").Value == bool_value);
                client.Write("M902", bool_value);
                Assert.True(client.ReadBoolean("M902").Value == bool_value);
                client.Write("M903", !bool_value);
                Assert.True(client.ReadBoolean("M903").Value == !bool_value);
                client.Write("M904", bool_value);
                Assert.True(client.ReadBoolean("M904").Value == bool_value);
                //client.Write("L100", !bool_value);
                //Assert.True(client.ReadBoolean("L100").Value == !bool_value);
                //client.Write("F100", bool_value);
                //Assert.True(client.ReadBoolean("F100").Value == bool_value);
                //client.Write("V100", !bool_value);
                //Assert.True(client.ReadBoolean("V100").Value == !bool_value);
                //client.Write("B100", bool_value);
                //Assert.True(client.ReadBoolean("B100").Value == bool_value);
                //client.Write("S100", bool_value);
                //Assert.True(client.ReadBoolean("S100").Value == bool_value);

                client.Write("D600", short_number);
                Assert.True(client.ReadInt16("D600").Value == short_number);

                client.Write("D600", int_number);
                Assert.True(client.ReadInt32("D600").Value == int_number);

                client.Write("D600", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64("D600").Value == Convert.ToInt64(int_number));

                client.Write("D600", float_number);
                Assert.True(client.ReadFloat("D600").Value == float_number);

                client.Write("D600", Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble("D600").Value == Convert.ToDouble(float_number));

                bool[] bool_values = { true, false,true, true, true, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false,false };

                var sss1 = client.Write("M900", bool_values);
                var bool_values_result = client.ReadBoolean("M900", bool_values.Length);
                for (int j = 0; j < bool_values_result.Value.Length; j++)
                {
                    Assert.True(bool_values_result.Value[j] == bool_values[j]);

                }

                short[] short_values = { 10000, 20000, 30003, 30004, 30005, 30006, 30007, 30008, 30009, 30010 };
                client.Write("D300", short_values);
                var short_values_result = client.ReadInt16("D300", short_values.Length);
                for (int j = 0; j < short_values_result.Value.Length; j++)
                {
                    Assert.True(short_values_result.Value[j] == short_values[j]);

                }

                ushort[] ushort_values = { 10000, 20000, 30003, 30004, 30005, 30006, 30007, 30008, 30009, 30010 };
                client.Write("D300", ushort_values);
                var ushort_values_result = client.ReadInt16("D300", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Value.Length; j++)
                {
                    Assert.True(ushort_values_result.Value[j] == ushort_values[j]);

                }

                int[] int_values = { 100000000,- 2000000, 30000000, -40000000, 50000000, 60000000, 70000000, 80000000, 90000000, 100000000 };
               var r4= client.Write("D300", int_values);
                var int_values_result = client.ReadInt32("D300", int_values.Length);
                for (int j = 0; j < int_values_result.Value.Length; j++)
                {
                    Assert.True(int_values_result.Value[j] == int_values[j]);

                }

                uint[] uint_values = { 100000, 200000, 300000, 400000, 500000, 600000, 700000, 800000, 900000, 1000000 };
                client.Write("D300", uint_values);
                var uint_values_result = client.ReadUInt32("D300", uint_values.Length);
                for (int j = 0; j < uint_values_result.Value.Length; j++)
                {
                    Assert.True(uint_values_result.Value[j] == uint_values[j]);

                }

                long[] long_values = { 100000000, 200000000, 300000000, 400000000, 500000000, 600000000, 7000000, 80000000, 900000000, 1000000000 };
                client.Write("D300", long_values);
                var long_values_result = client.ReadInt64("D300", long_values.Length);
                for (long j = 0; j < long_values_result.Value.Length; j++)
                {
                    Assert.True(long_values_result.Value[j] == long_values[j]);

                }

                ulong[] ulong_values = { 100000000, 200000000, 300000000, 400000000, 500000000, 600000000, 7000000, 80000000, 900000000, 1000000000 };
                client.Write("D300", ulong_values);
                var ulong_values_result = client.ReadUInt64("D300", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Value.Length; j++)
                {
                    Assert.True(ulong_values_result.Value[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("D300", float_values);
                var float_values_result = client.ReadFloat("D300", float_values.Length);
                for (int j = 0; j < float_values_result.Value.Length; j++)
                {
                    Assert.True(float_values_result.Value[j] == float_values[j]);

                }
                double[] double_values = { 1.1, 2.1, 3.1, 4.1, 5.1, 6.1, 7.1, 8.1, 9.1, 10.1 };
                client.Write("D300", double_values);
                var double_values_result = client.ReadDouble("D300", double_values.Length);
                for (int j = 0; j < double_values_result.Value.Length; j++)
                {
                    Assert.True(double_values_result.Value[j] == double_values[j]);

                }
            }
        }

        [Theory]
        [InlineData(MitsubishiVersion.Qna_3E, 5000)]
        //[InlineData(MitsubishiVersion.A_1E, 6001)]
        public async void 长连接主动开关Async(MitsubishiVersion version, int port)
        {
            client = new MitsubishiClient(version, ip, port);

            await client.ConnectAsync();
            client.IsLongLivedConnection = true;

            ReadWriteAsync().Wait();

            client?.Disconnect();
        }

        private async Task ReadWriteAsync()
        {
            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            for (int i = 0; i < 10; i++)
            {
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                float float_number = int_number / 100;
                var bool_value = short_number % 2 == 1;

                //client.WriteAsync("Y100", true);
                //Assert.True(client.ReadBoolean("Y100").Value == true);
                var tttttttt =await client.WriteAsync("M900", false);
                 await client.WriteAsync("M900", true);

                var sss =   await client.ReadBooleanAsync("M900");
                Assert.True(client.ReadBooleanAsync("M900").Result.Value == true);
                await client.WriteAsync("M901", bool_value);
                Assert.True(client.ReadBooleanAsync("M901").Result.Value == bool_value);
                await client.WriteAsync("M902", bool_value);
                Assert.True(client.ReadBooleanAsync("M902").Result.Value == bool_value);
                await client.WriteAsync("M903", !bool_value);
                Assert.True(client.ReadBooleanAsync("M903").Result.Value == !bool_value);
                await client.WriteAsync("M904", bool_value);
                Assert.True(client.ReadBooleanAsync("M904").Result.Value == bool_value);
                //client.WriteAsync("L100", !bool_value);
                //Assert.True(client.ReadBoolean("L100").Value == !bool_value);
                //client.WriteAsync("F100", bool_value);
                //Assert.True(client.ReadBoolean("F100").Value == bool_value);
                //client.WriteAsync("V100", !bool_value);
                //Assert.True(client.ReadBoolean("V100").Value == !bool_value);
                //client.WriteAsync("B100", bool_value);
                //Assert.True(client.ReadBoolean("B100").Value == bool_value);
                //client.WriteAsync("S100", bool_value);
                //Assert.True(client.ReadBoolean("S100").Value == bool_value);

                await client.WriteAsync("D600", short_number);
                Assert.True(client.ReadInt16Async("D600").Result.Value == short_number);

                await client.WriteAsync("D600", int_number);
                Assert.True(client.ReadInt32Async("D600").Result.Value == int_number);

                await client.WriteAsync("D600", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64Async("D600").Result.Value == Convert.ToInt64(int_number));

                await client.WriteAsync("D600", float_number);
                Assert.True(client.ReadFloatAsync("D600").Result.Value == float_number);

                await client.WriteAsync("D600", Convert.ToDouble(float_number));
                Assert.True(client.ReadDoubleAsync("D600").Result.Value == Convert.ToDouble(float_number));

                bool[] bool_values = { true, false,true, true, true, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false,true };

                var sss1 = await client.WriteAsync("M900", bool_values);
                var bool_values_result = await client.ReadBooleanAsync("M900", bool_values.Length);
                for (int j = 0; j < bool_values_result.Value.Length; j++)
                {
                    Assert.True(bool_values_result.Value[j] == bool_values[j]);

                }

                short[] short_values = { 10000, -20000, 30003, -30004, 30005, 30006, 30007, -30008, -30009, 30010 };
                await client.WriteAsync("D300", short_values);
                var short_values_result = await client.ReadInt16Async("D300", short_values.Length);
                for (int j = 0; j < short_values_result.Value.Length; j++)
                {
                    Assert.True(short_values_result.Value[j] == short_values[j]);

                }

                ushort[] ushort_values = { 10000, 20000, 30003, 30004, 30005, 30006, 30007, 30008, 30009, 30010 };
                await client.WriteAsync("D300", ushort_values);
                var ushort_values_result = await client.ReadInt16Async("D300", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Value.Length; j++)
                {
                    Assert.True(ushort_values_result.Value[j] == ushort_values[j]);

                }

                int[] int_values = { 10000000, -20000000, 30000000, 40000000, 50000000, 60000000, -70000000, 80000000, -90000000, 100000000 };
                await client.WriteAsync("D300", int_values);
                var int_values_result = await client.ReadInt32Async("D300", int_values.Length);
                for (int j = 0; j < int_values_result.Value.Length; j++)
                {
                    Assert.True(int_values_result.Value[j] == int_values[j]);

                }

                uint[] uint_values = { 100000, 200000, 300000, 400000, 500000, 600000, 700000, 800000, 900000, 1000000 };
                await client.WriteAsync("D300", uint_values);
                var uint_values_result = await client.ReadUInt32Async("D300", uint_values.Length);
                for (int j = 0; j < uint_values_result.Value.Length; j++)
                {
                    Assert.True(uint_values_result.Value[j] == uint_values[j]);

                }

                long[] long_values = { 10000000000, 20000000000, 30000000000, 400000000000, 500000000000, 600000000, 70000000000, 80000000000, 900000000000, 1000000000000 };
                await client.WriteAsync("D300", long_values);
                var long_values_result = await client.ReadInt64Async("D300", long_values.Length);
                for (long j = 0; j < long_values_result.Value.Length; j++)
                {
                    Assert.True(long_values_result.Value[j] == long_values[j]);

                }

                ulong[] ulong_values = { 100000000, 200000000, 300000000, 400000000, 500000000, 600000000, 7000000, 80000000, 900000000, 1000000000 };
                await client.WriteAsync("D300", ulong_values);
                var ulong_values_result = await client.ReadUInt64Async("D300", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Value.Length; j++)
                {
                    Assert.True(ulong_values_result.Value[j] == ulong_values[j]);

                }

                float[] float_values = { 1.1f, 2.1f, 3.1f, 4.1f, 5.1f, 6.1f, 7.1f, 8.1f, 9.1f, 10.1f };
                await client.WriteAsync("D300", float_values);
                var float_values_result =await client.ReadFloatAsync("D300", float_values.Length);
                for (int j = 0; j < float_values_result.Value.Length; j++)
                {
                    Assert.True(float_values_result.Value[j] == float_values[j]);

                }
                double[] double_values = { 1.1111, 2.111, 3.11111, 4.1111, 5.1, 6.1, 7.1, 8.1, 9.1, 10.11111 };
                await client.WriteAsync("D300", double_values);
                var double_values_result = await client.ReadDoubleAsync("D300", double_values.Length);
                for (int j = 0; j < double_values_result.Value.Length; j++)
                {
                    Assert.True(double_values_result.Value[j] == double_values[j]);

                }
            }
        }


        [Theory]
        [InlineData(MitsubishiVersion.Qna_3E, 8000)]
        //[InlineData(MitsubishiVersion.Qna_3E, 8001)]
        public void 批量读写(MitsubishiVersion version, int port)
        {
            client = new MitsubishiClient(version, ip, port);

            client.Connect();

            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            short short_number1 = (short)rnd.Next(short.MinValue, short.MaxValue);
            short short_number2 = (short)rnd.Next(short.MinValue, short.MaxValue);
            short short_number3 = (short)rnd.Next(short.MinValue, short.MaxValue);
            short short_number4 = (short)rnd.Next(short.MinValue, short.MaxValue);
            short short_number5 = (short)rnd.Next(short.MinValue, short.MaxValue);
            var bool_value = short_number1 % 2 == 1;

            client.Write("M100", !bool_value);
            client.Write("M101", !bool_value);
            client.Write("M102", bool_value);
            client.Write("M103", !bool_value);
            client.Write("M104", bool_value);

            var result = client.ReadBoolean("M100", 5);
            //foreach (var item in result.Value)
            //{
            //    if (item.Key == "M100" || item.Key == "M101" || item.Key == "M103")
            //    {
            //        Assert.True(item.Value == !bool_value);
            //    }
            //    else
            //    {
            //        Assert.True(item.Value == bool_value);
            //    }
            //}

            client.Write("D300", short_number1);
            client.Write("D301", short_number2);
            client.Write("D302", short_number3);
            client.Write("D303", short_number4);
            client.Write("D304", short_number5);
            //var tt2t = client.ReadInt16("D300");
            Assert.True(client.ReadInt16("D300").Value == short_number1);
            Assert.True(client.ReadInt16("D301").Value == short_number2);
            Assert.True(client.ReadInt16("D302").Value == short_number3);
            Assert.True(client.ReadInt16("D303").Value == short_number4);
            Assert.True(client.ReadInt16("D304").Value == short_number5);

            client?.Disconnect();
        }

        [Theory]
        [InlineData(MitsubishiVersion.Qna_3E, 8000)]
        public void 批量读取(MitsubishiVersion version, int port)
        {
            client = new MitsubishiClient(version, ip, port);

            Dictionary<string, DataTypeEnums> readAddresses = new Dictionary<string, DataTypeEnums>();
            //readAddresses.Add("V2634.0", DataTypeEnums.Bool);
            //readAddresses.Add("V2634.1", DataTypeEnums.Bool);
            //readAddresses.Add("V2634.2", DataTypeEnums.Bool);
            //readAddresses.Add("V2634.3", DataTypeEnums.Bool);
            //readAddresses.Add("V2634.4", DataTypeEnums.Bool);
            //readAddresses.Add("V2634.5", DataTypeEnums.Bool);
            //readAddresses.Add("V2634.6", DataTypeEnums.Bool);
            //readAddresses.Add("V2634.7", DataTypeEnums.Bool);
            //readAddresses.Add("V2642", DataTypeEnums.Float);
            //readAddresses.Add("V2646", DataTypeEnums.Float);
            //readAddresses.Add("V2650", DataTypeEnums.Float);
            readAddresses.Add("D100", DataTypeEnums.Float);
            readAddresses.Add("D102", DataTypeEnums.Float);
            readAddresses.Add("D104", DataTypeEnums.Float);
            readAddresses.Add("D263", DataTypeEnums.Int16);
            readAddresses.Add("D265", DataTypeEnums.Int16);
            //readAddresses.Add("V2670", DataTypeEnums.Float);
            //readAddresses.Add("V2674", DataTypeEnums.Float);
            //readAddresses.Add("V1650", DataTypeEnums.Byte);
            //readAddresses.Add("V1651", DataTypeEnums.Byte);
            //readAddresses.Add("V1652", DataTypeEnums.Byte);

            var result = client.BatchRead(readAddresses);
        }
    }
}

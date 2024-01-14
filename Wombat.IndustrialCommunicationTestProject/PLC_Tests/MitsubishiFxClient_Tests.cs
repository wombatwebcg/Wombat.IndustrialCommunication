using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;
using Xunit;

namespace Wombat.IndustrialCommunicationTest.PLCTests
{
    public class MitsubishiFxClient_Tests
    {
        private MitsubishiFxSerialClient client;
        string ip = "192.168.0.171";
        //string ip = "192.168.1.180";

        public MitsubishiFxClient_Tests()
        {
        }

        [Theory]
        [InlineData("Com14")]
        public void 短连接自动开关(string portName)
        {
            client = new MitsubishiFxSerialClient(portName);
            client.IsUseLongConnect = false;

            //var sss = client.Write("d0", (short)0);
            ReadWrite();
        }
        [Theory]
        [InlineData("Com14")]
        public void 长连接主动开关(string portName)
        {
            client = new MitsubishiFxSerialClient(portName);

            client.Connect();
            client.IsUseLongConnect = true;

            ReadWrite();

            client?.Disconnect();
        }

        private void ReadWrite()
        {
            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            for (int i = 0; i < 10; i++)
            {
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                float float_number = int_number / 100;
                var bool_value = short_number % 2 == 1;

                //client.Write("Y100", true);
                //Assert.True(client.ReadBoolean("Y100").Value == true);
                //client.Write("M90", false);
                //var tttttttt = client.Write("M90", true);
                //var sss = client.ReadBoolean("M90");
                //Assert.True(client.ReadBoolean("M90").Value == true);
                client.Write("M91", bool_value);
                Assert.True(client.ReadBoolean("M91").Value == bool_value);
                client.Write("M92", bool_value);
                Assert.True(client.ReadBoolean("M92").Value == bool_value);
                client.Write("M93", !bool_value);
                Assert.True(client.ReadBoolean("M93").Value == !bool_value);
                client.Write("M94", bool_value);
                //Assert.True(client.ReadBoolean("M94").Value == bool_value);
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


                client.ReadInt16("D60");
                client.Write("D60", short_number);
                Assert.True(client.ReadInt16("D60").Value == short_number);

                client.Write("D60", int_number);
                Assert.True(client.ReadInt32("D60").Value == int_number);

                client.Write("D60", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64("D60").Value == Convert.ToInt64(int_number));

                client.Write("D60", float_number);
                Assert.True(client.ReadFloat("D60").Value == float_number);

                client.Write("D60", Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble("D60").Value == Convert.ToDouble(float_number));

                //var sss1222 = client.Write("Y0", new bool[4] { false,false,false,false});
                client.Write("M90", new bool []{ false, false, false, false, false, false, false, false, false });


                bool[] bool_values = { true, true,true, true,true,true,true, true, true };

                var sss1 = client.Write("M90", bool_values);
                var bool_values_result = client.ReadBoolean("Y0", bool_values.Length);
                for (int j = 0; j < bool_values_result.Value.Length; j++)
                {
                    Assert.True(bool_values_result.Value[j] == bool_values[j]);

                }

                short[] short_values = { 10000, 20000, 30003, 30004};
                client.Write("D300", short_values);
                var short_values_result = client.ReadInt16("D300", short_values.Length);
                for (int j = 0; j < short_values_result.Value.Length; j++)
                {
                    Assert.True(short_values_result.Value[j] == short_values[j]);

                }

                ushort[] ushort_values = { 10000, 20000, 30003, 30004,  };
                client.Write("D300", ushort_values);
                var ushort_values_result = client.ReadInt16("D300", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Value.Length; j++)
                {
                    Assert.True(ushort_values_result.Value[j] == ushort_values[j]);

                }

                int[] int_values = { 100000000, -2000000, 30000000};
                var r4 = client.Write("D300", int_values);
                var int_values_result = client.ReadInt32("D300", int_values.Length);
                for (int j = 0; j < int_values_result.Value.Length; j++)
                {
                    Assert.True(int_values_result.Value[j] == int_values[j]);

                }

                uint[] uint_values = { 100000, 200000, 300000};
                client.Write("D300", uint_values);
                var uint_values_result = client.ReadUInt32("D300", uint_values.Length);
                for (int j = 0; j < uint_values_result.Value.Length; j++)
                {
                    Assert.True(uint_values_result.Value[j] == uint_values[j]);

                }

                long[] long_values = { 100000000};
                client.Write("D300", long_values);
                var long_values_result = client.ReadInt64("D300", long_values.Length);
                for (long j = 0; j < long_values_result.Value.Length; j++)
                {
                    Assert.True(long_values_result.Value[j] == long_values[j]);

                }

                ulong[] ulong_values = { 100000000};
                client.Write("D300", ulong_values);
                var ulong_values_result = client.ReadUInt64("D300", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Value.Length; j++)
                {
                    Assert.True(ulong_values_result.Value[j] == ulong_values[j]);

                }

                float[] float_values = { 1, };
                client.Write("D300", float_values);
                var float_values_result = client.ReadFloat("D300", float_values.Length);
                for (int j = 0; j < float_values_result.Value.Length; j++)
                {
                    Assert.True(float_values_result.Value[j] == float_values[j]);

                }
                double[] double_values = { 1.1,};
                client.Write("D300", double_values);
                var double_values_result = client.ReadDouble("D300", double_values.Length);
                for (int j = 0; j < double_values_result.Value.Length; j++)
                {
                    Assert.True(double_values_result.Value[j] == double_values[j]);

                }
            }
        }

        [Theory]
        [InlineData("Com14")]

        public async void 长连接主动开关Async(string portName)
        {
            client = new MitsubishiFxSerialClient(portName);

            await client.ConnectAsync();
            client.IsUseLongConnect = true;

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
                var tttttttt = await client.WriteAsync("Y0", false);
                await client.WriteAsync("Y0", true);

                var sss = await client.ReadBooleanAsync("M90");
                Assert.True(client.ReadBooleanAsync("M90").Result.Value == true);
                await client.WriteAsync("M91", bool_value);
                Assert.True(client.ReadBooleanAsync("M91").Result.Value == bool_value);
                await client.WriteAsync("M92", bool_value);
                Assert.True(client.ReadBooleanAsync("M92").Result.Value == bool_value);
                //await client.WriteAsync("M93", !bool_value);
                //Assert.True(client.ReadBooleanAsync("M93").Result.Value == !bool_value);
                //await client.WriteAsync("M94", bool_value);
                //Assert.True(client.ReadBooleanAsync("M94").Result.Value == bool_value);
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

                await client.WriteAsync("D60", short_number);
                Assert.True(client.ReadInt16Async("D60").Result.Value == short_number);

                await client.WriteAsync("D60", int_number);
                Assert.True(client.ReadInt32Async("D60").Result.Value == int_number);

                await client.WriteAsync("D60", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64Async("D60").Result.Value == Convert.ToInt64(int_number));

                await client.WriteAsync("D60", float_number);
                Assert.True(client.ReadFloatAsync("D60").Result.Value == float_number);

                await client.WriteAsync("D60", Convert.ToDouble(float_number));
                Assert.True(client.ReadDoubleAsync("D60").Result.Value == Convert.ToDouble(float_number));

                bool[] bool_values = { true, false,true, true, true, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false,true };

                var sss1 = await client.WriteAsync("M90", bool_values);
                var bool_values_result = await client.ReadBooleanAsync("M90", bool_values.Length);
                for (int j = 0; j < bool_values_result.Value.Length; j++)
                {
                    Assert.True(bool_values_result.Value[j] == bool_values[j]);

                }

                short[] short_values = { 10000, -20000, 30003, -30004, 30005 };
                await client.WriteAsync("D300", short_values);
                var short_values_result = await client.ReadInt16Async("D300", short_values.Length);
                for (int j = 0; j < short_values_result.Value.Length; j++)
                {
                    Assert.True(short_values_result.Value[j] == short_values[j]);

                }

                ushort[] ushort_values = { 10000, 20000, 30003, 30004, 30005};
                await client.WriteAsync("D300", ushort_values);
                var ushort_values_result = await client.ReadInt16Async("D300", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Value.Length; j++)
                {
                    Assert.True(ushort_values_result.Value[j] == ushort_values[j]);

                }

                int[] int_values = { 10000000, -20000000, 30000000,};
                await client.WriteAsync("D300", int_values);
                var int_values_result = await client.ReadInt32Async("D300", int_values.Length);
                for (int j = 0; j < int_values_result.Value.Length; j++)
                {
                    Assert.True(int_values_result.Value[j] == int_values[j]);

                }

                uint[] uint_values = { 100000, 200000, 300000};
                await client.WriteAsync("D300", uint_values);
                var uint_values_result = await client.ReadUInt32Async("D300", uint_values.Length);
                for (int j = 0; j < uint_values_result.Value.Length; j++)
                {
                    Assert.True(uint_values_result.Value[j] == uint_values[j]);

                }

                long[] long_values = { 10000000000,};
                await client.WriteAsync("D300", long_values);
                var long_values_result = await client.ReadInt64Async("D300", long_values.Length);
                for (long j = 0; j < long_values_result.Value.Length; j++)
                {
                    Assert.True(long_values_result.Value[j] == long_values[j]);

                }

                ulong[] ulong_values = { 100000000};
                await client.WriteAsync("D300", ulong_values);
                var ulong_values_result = await client.ReadUInt64Async("D300", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Value.Length; j++)
                {
                    Assert.True(ulong_values_result.Value[j] == ulong_values[j]);

                }

                float[] float_values = { 1.1f};
                await client.WriteAsync("D300", float_values);
                var float_values_result = await client.ReadFloatAsync("D300", float_values.Length);
                for (int j = 0; j < float_values_result.Value.Length; j++)
                {
                    Assert.True(float_values_result.Value[j] == float_values[j]);

                }
                double[] double_values = { 1.1111 };
                await client.WriteAsync("D300", double_values);
                var double_values_result = await client.ReadDoubleAsync("D300", double_values.Length);
                for (int j = 0; j < double_values_result.Value.Length; j++)
                {
                    Assert.True(double_values_result.Value[j] == double_values[j]);

                }
            }
        }


        public void 批量读写(string portName)
        {
            client = new MitsubishiFxSerialClient(portName);

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

        public void 批量读取(string portName)
        {
            client = new MitsubishiFxSerialClient(portName);

            Dictionary<string, DataTypeEnum> readAddresses = new Dictionary<string, DataTypeEnum>();
            //readAddresses.Add("V2634.0", DataTypeEnum.Bool);
            //readAddresses.Add("V2634.1", DataTypeEnum.Bool);
            //readAddresses.Add("V2634.2", DataTypeEnum.Bool);
            //readAddresses.Add("V2634.3", DataTypeEnum.Bool);
            //readAddresses.Add("V2634.4", DataTypeEnum.Bool);
            //readAddresses.Add("V2634.5", DataTypeEnum.Bool);
            //readAddresses.Add("V2634.6", DataTypeEnum.Bool);
            //readAddresses.Add("V2634.7", DataTypeEnum.Bool);
            //readAddresses.Add("V2642", DataTypeEnum.Float);
            //readAddresses.Add("V2646", DataTypeEnum.Float);
            //readAddresses.Add("V2650", DataTypeEnum.Float);
            readAddresses.Add("D100", DataTypeEnum.Float);
            readAddresses.Add("D102", DataTypeEnum.Float);
            readAddresses.Add("D104", DataTypeEnum.Float);
            readAddresses.Add("D263", DataTypeEnum.Int16);
            readAddresses.Add("D265", DataTypeEnum.Int16);
            //readAddresses.Add("V2670", DataTypeEnum.Float);
            //readAddresses.Add("V2674", DataTypeEnum.Float);
            //readAddresses.Add("V1650", DataTypeEnum.Byte);
            //readAddresses.Add("V1651", DataTypeEnum.Byte);
            //readAddresses.Add("V1652", DataTypeEnum.Byte);

            var result = client.BatchRead(readAddresses);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.PLC;
using Xunit;


namespace Wombat.IndustrialCommunicationTest.PLCTests
{
   public class InovanceClient_Test
    {
        private InovanceClient client;

        string ip = "192.168.0.171";

        public InovanceClient_Test()
        {

        }

        [Fact]
        //[InlineData(MitsubishiVersion.A_1E, 6001)]
        public void 短连接自动开关()
        {

            client = new InovanceClient(ip,502);
            client.IsLongLivedConnection = false;
            ReadWrite();
        }
        [Fact]

        public void 长连接自动开关()
        {
            client = new InovanceClient(ip, 502);
            client.IsLongLivedConnection = true;
            client.Connect();
            client.ConnectTimeout = TimeSpan.FromMilliseconds(500);

            ReadWrite();

            //var t1 = Task.Run(() =>
            //{
            //    ReadWrite();
            //});
           // var t2 = Task.Run(() =>
           // {
           //     ReadWrite2();
           // });
           //while(!t1.IsCompleted|!t2.IsCompleted)
           // {

           // }
            client.Disconnect();

        }

        [Fact]
        public async void 长连接自动开关Async()
        {
            client = new InovanceClient(ip, 502);
            client.IsLongLivedConnection = true;
            await client.ConnectAsync();
            client.ConnectTimeout = TimeSpan.FromMilliseconds(500);

            ReadWrite();

            //var t1 = Task.Run(() =>
            //{
            //    ReadWrite();
            //});
            // var t2 = Task.Run(() =>
            // {
            //     ReadWrite2();
            // });
            //while(!t1.IsCompleted|!t2.IsCompleted)
            // {

            // }
            client.Disconnect();

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
                //client.Write("M900", true);
                //var sss = client.ReadCoil("M900");
                //Assert.True(client.ReadCoil("M900").Value == true);
                //client.Write("M901", bool_value);
                //Assert.True(client.ReadCoil("M901").Value == bool_value);
                //client.Write("M902", bool_value);
                //Assert.True(client.ReadCoil("M902").Value == bool_value);
                //client.Write("M903", !bool_value);
                //Assert.True(client.ReadCoil("M903").Value == !bool_value);
                //client.Write("M904", bool_value);
                //Assert.True(client.ReadCoil("M904").Value == bool_value);
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

                //var sss555 = client.Write("D200", short_number);
                //Assert.True(client.ReadInt16("D200").Value == short_number);
                //var sss5556 = client.Write("D200", (ushort)Math.Abs(short_number));
                //var sssssssss2 = client.ReadUInt16("D200");

                //Assert.True(client.ReadUInt16("D200").Value == (ushort)Math.Abs(short_number));

                client.Write("D200", int_number);
                //client.Write("D200",200);
                //var ss2s = client.ReadInt32("D200");
                Assert.True(client.ReadInt32("D200").Value == int_number);

                client.Write("D200", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64("D200").Value == Convert.ToInt64(int_number));

                client.Write("D200", float_number);
                Assert.True(client.ReadFloat("D200").Value == float_number);

                client.Write("D200", Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble("D200").Value == Convert.ToDouble(float_number));

                //bool[] bool_values = { false, true, false, false, true, false, false, false, false, false
                //        , false, false, false,false,false,false,false,false,false, true };

                //var sss1 = client.Write("M900", bool_values);
                //var bool_values_result = client.ReadCoil("M900", bool_values.Length);
                //for (int j = 0; j < bool_values_result.Value.Length; j++)
                //{
                //    Assert.True(bool_values_result.Value[j] == bool_values[j]);

                //}

                bool[] bool_values = { true, true, false, false, true, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };

                var sss1 = client.Write("M900", bool_values);
                ////var bool_values_result = client.ReadCoil("M900", bool_values.Length);
                //for (int j = 0; j < bool_values_result.Value.Length; j++)
                //{
                //    Assert.True(bool_values_result.Value[j] == bool_values[j]);

                //}

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

                int[] int_values = { 100000, 200000, 300000, 400000, 500000, 600000, 700000, 800000, 900000, 1000000 };
                client.Write("D300", int_values);
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


        private async Task ReadWriteAsync()
        {



            Random rnd = new Random((int)Stopwatch.GetTimestamp());
            for (int i = 0; i < 1000; i++)
            {
                short short_number = (short)rnd.Next(short.MinValue, short.MaxValue);
                int int_number = rnd.Next(int.MinValue, int.MaxValue);
                float float_number = int_number / 100;
                var bool_value = short_number % 2 == 1;

                //await client.WriteAsync("Y100", true);
                //Assert.True(client.ReadBoolean("Y100").Value == true);
                //await client.WriteAsync("M900", true);
                //var sss = client.ReadCoil("M900");
                //Assert.True(client.ReadCoil("M900").Value == true);
                //await client.WriteAsync("M901", bool_value);
                //Assert.True(client.ReadCoil("M901").Value == bool_value);
                //await client.WriteAsync("M902", bool_value);
                //Assert.True(client.ReadCoil("M902").Value == bool_value);
                //await client.WriteAsync("M903", !bool_value);
                //Assert.True(client.ReadCoil("M903").Value == !bool_value);
                //await client.WriteAsync("M904", bool_value);
                //Assert.True(client.ReadCoil("M904").Value == bool_value);
                //await client.WriteAsync("L100", !bool_value);
                //Assert.True(client.ReadBoolean("L100").Value == !bool_value);
                //await client.WriteAsync("F100", bool_value);
                //Assert.True(client.ReadBoolean("F100").Value == bool_value);
                //await client.WriteAsync("V100", !bool_value);
                //Assert.True(client.ReadBoolean("V100").Value == !bool_value);
                //await client.WriteAsync("B100", bool_value);
                //Assert.True(client.ReadBoolean("B100").Value == bool_value);
                //await client.WriteAsync("S100", bool_value);
                //Assert.True(client.ReadBoolean("S100").Value == bool_value);

                //var sss555 = await client.WriteAsync("D200", short_number);
                //Assert.True(client.ReadInt16("D200").Value == short_number);
                //var sss5556 = await client.WriteAsync("D200", (ushort)Math.Abs(short_number));
                //var sssssssss2 = client.ReadUInt16("D200");

                //Assert.True(client.ReadUInt16("D200").Value == (ushort)Math.Abs(short_number));

                await client.WriteAsync("D200", int_number);
                //await client.WriteAsync("D200",200);
                //var ss2s = client.ReadInt32("D200");
                Assert.True(client.ReadInt32("D200").Value == int_number);

                await client.WriteAsync("D200", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64("D200").Value == Convert.ToInt64(int_number));

                await client.WriteAsync("D200", float_number);
                Assert.True(client.ReadFloat("D200").Value == float_number);

                await client.WriteAsync("D200", Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble("D200").Value == Convert.ToDouble(float_number));

                //bool[] bool_values = { false, true, false, false, true, false, false, false, false, false
                //        , false, false, false,false,false,false,false,false,false, true };

                //var sss1 = await client.WriteAsync("M900", bool_values);
                //var bool_values_result = client.ReadCoil("M900", bool_values.Length);
                //for (int j = 0; j < bool_values_result.Value.Length; j++)
                //{
                //    Assert.True(bool_values_result.Value[j] == bool_values[j]);

                //}

                //bool[] bool_values = { true, true, false, false, true, false, false, false, false, false
                //        , false, false, false,false,false,false,false,false,false, true };

                //var sss1 = await client.WriteAsync("M900", bool_values);
                //var bool_values_result = client.ReadCoil("M900", bool_values.Length);
                //for (int j = 0; j < bool_values_result.Value.Length; j++)
                //{
                //    Assert.True(bool_values_result.Value[j] == bool_values[j]);

                //}

                short[] short_values = { 10000, 20000, 30003, 30004, 30005, 30006, 30007, 30008, 30009, 30010 };
                await client.WriteAsync("D300", short_values);
                var short_values_result = client.ReadInt16("D300", short_values.Length);
                for (int j = 0; j < short_values_result.Value.Length; j++)
                {
                    Assert.True(short_values_result.Value[j] == short_values[j]);

                }

                ushort[] ushort_values = { 10000, 20000, 30003, 30004, 30005, 30006, 30007, 30008, 30009, 30010 };
                await client.WriteAsync("D300", ushort_values);
                var ushort_values_result = client.ReadInt16("D300", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Value.Length; j++)
                {
                    Assert.True(ushort_values_result.Value[j] == ushort_values[j]);

                }

                int[] int_values = { 100000, 200000, 300000, 400000, 500000, 600000, 700000, 800000, 900000, 1000000 };
                await client.WriteAsync("D300", int_values);
                var int_values_result = client.ReadInt32("D300", int_values.Length);
                for (int j = 0; j < int_values_result.Value.Length; j++)
                {
                    Assert.True(int_values_result.Value[j] == int_values[j]);

                }

                uint[] uint_values = { 100000, 200000, 300000, 400000, 500000, 600000, 700000, 800000, 900000, 1000000 };
                await client.WriteAsync("D300", uint_values);
                var uint_values_result = client.ReadUInt32("D300", uint_values.Length);
                for (int j = 0; j < uint_values_result.Value.Length; j++)
                {
                    Assert.True(uint_values_result.Value[j] == uint_values[j]);

                }

                long[] long_values = { 100000000, 200000000, 300000000, 400000000, 500000000, 600000000, 7000000, 80000000, 900000000, 1000000000 };
                await client.WriteAsync("D300", long_values);
                var long_values_result = client.ReadInt64("D300", long_values.Length);
                for (long j = 0; j < long_values_result.Value.Length; j++)
                {
                    Assert.True(long_values_result.Value[j] == long_values[j]);

                }

                ulong[] ulong_values = { 100000000, 200000000, 300000000, 400000000, 500000000, 600000000, 7000000, 80000000, 900000000, 1000000000 };
                await client.WriteAsync("D300", ulong_values);
                var ulong_values_result = client.ReadUInt64("D300", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Value.Length; j++)
                {
                    Assert.True(ulong_values_result.Value[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("D300", float_values);
                var float_values_result = client.ReadFloat("D300", float_values.Length);
                for (int j = 0; j < float_values_result.Value.Length; j++)
                {
                    Assert.True(float_values_result.Value[j] == float_values[j]);

                }
                double[] double_values = { 1.1, 2.1, 3.1, 4.1, 5.1, 6.1, 7.1, 8.1, 9.1, 10.1 };
                await client.WriteAsync("D300", double_values);
                var double_values_result = client.ReadDouble("D300", double_values.Length);
                for (int j = 0; j < double_values_result.Value.Length; j++)
                {
                    Assert.True(double_values_result.Value[j] == double_values[j]);

                }
            }
        }

    }
}

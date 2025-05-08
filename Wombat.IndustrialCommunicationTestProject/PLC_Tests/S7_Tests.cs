
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
    public class S7_Tests
    {
        private SiemensClient client;
        public S7_Tests()
        {

        }
        [Fact]
        public void Smart200读写测试()
        {

            client = new SiemensClient("192.168.11.51", 102, SiemensVersion.S7_200Smart);
            client.Connect();
            ReadWrite();
            client.Disconnect();

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


                var ssss2 = client.Write("Q1.3", true);
                var sss1 = client.ReadBoolean("Q1.3");
                Assert.True(client.ReadBoolean("Q1.3").ResultValue == true);
                client.Write("Q1.4", bool_value);
                Assert.True(client.ReadBoolean("Q1.4").ResultValue == bool_value);
                client.Write("Q1.5", !bool_value);
                Assert.True(client.ReadBoolean("Q1.5").ResultValue == !bool_value);

                client.Write("V700", short_number);
                Assert.True(client.ReadInt16("V700").ResultValue == short_number);
                client.Write("V700", short_number_1);
                Assert.True(client.ReadUInt16("V700").ResultValue == short_number_1);

                client.Write("V700", int_number);
                Assert.True(client.ReadInt32("V700").ResultValue == int_number);
                client.Write("V700", int_number_1);
                Assert.True(client.ReadUInt32("V700").ResultValue == int_number_1);

                client.Write("V700", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64("V700").ResultValue == Convert.ToInt64(int_number));
                client.Write("V700", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64("V700").ResultValue == Convert.ToUInt64(int_number_1));

                client.Write("V700", float_number);
                Assert.True(client.ReadFloat("V700").ResultValue == float_number);
                client.Write("V700", Convert.ToDouble(float_number));
                Assert.True(client.ReadDouble("V700").ResultValue == Convert.ToDouble(float_number));

                //var rrr =  client.Write("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).ResultValue == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                client.Write("V700", bool_values);
                var bool_values_result = client.ReadBoolean("V700", bool_values.Length);
                for (int j = 0; j < bool_values_result.ResultValue.Length; j++)
                {
                    Assert.True(bool_values_result.ResultValue[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", short_values);
                var short_values_result = client.ReadInt16("V700", short_values.Length);
                for (int j = 0; j < short_values_result.ResultValue.Length; j++)
                {
                    Assert.True(short_values_result.ResultValue[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", ushort_values);
                var ushort_values_result = client.ReadInt16("V700", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.ResultValue.Length; j++)
                {
                    Assert.True(ushort_values_result.ResultValue[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", int_values);
                var int_values_result = client.ReadInt32("V700", int_values.Length);
                for (int j = 0; j < int_values_result.ResultValue.Length; j++)
                {
                    Assert.True(int_values_result.ResultValue[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", uint_values);
                var uint_values_result = client.ReadUInt32("V700", uint_values.Length);
                for (int j = 0; j < uint_values_result.ResultValue.Length; j++)
                {
                    Assert.True(uint_values_result.ResultValue[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", long_values);
                var long_values_result = client.ReadInt64("V700", long_values.Length);
                for (long j = 0; j < long_values_result.ResultValue.Length; j++)
                {
                    Assert.True(long_values_result.ResultValue[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", ulong_values);
                var ulong_values_result = client.ReadUInt64("V700", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.ResultValue.Length; j++)
                {
                    Assert.True(ulong_values_result.ResultValue[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", float_values);
                var float_values_result = client.ReadFloat("V700", float_values.Length);
                for (int j = 0; j < float_values_result.ResultValue.Length; j++)
                {
                    Assert.True(float_values_result.ResultValue[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                client.Write("V700", double_values);
                var double_values_result = client.ReadDouble("V700", double_values.Length);
                for (int j = 0; j < double_values_result.ResultValue.Length; j++)
                {
                    Assert.True(double_values_result.ResultValue[j] == double_values[j]);

                }



            }

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
                Assert.True(client.ReadBooleanAsync("Q1.3").Result.ResultValue == true);
                await client.WriteAsync("Q1.4", bool_value);
                Assert.True(client.ReadBooleanAsync("Q1.4").Result.ResultValue == bool_value);
                await client.WriteAsync("Q1.5", !bool_value);
                Assert.True(client.ReadBooleanAsync("Q1.5").Result.ResultValue == !bool_value);

                var ssss = await client.WriteAsync("V700", short_number);
                var tttt = client.ReadInt16Async("V700");
                Assert.True(client.ReadInt16Async("V700").Result.ResultValue == short_number);
                await client.WriteAsync("V700", short_number_1);
                Assert.True(client.ReadUInt16Async("V700").Result.ResultValue == short_number_1);

                await client.WriteAsync("V700", int_number);
                Assert.True(client.ReadInt32Async("V700").Result.ResultValue == int_number);
                await client.WriteAsync("V700", int_number_1);
                Assert.True(client.ReadUInt32Async("V700").Result.ResultValue == int_number_1);

                await client.WriteAsync("V700", Convert.ToInt64(int_number));
                Assert.True(client.ReadInt64Async("V700").Result.ResultValue == Convert.ToInt64(int_number));
                await client.WriteAsync("V700", Convert.ToUInt64(int_number_1));
                Assert.True(client.ReadUInt64Async("V700").Result.ResultValue == Convert.ToUInt64(int_number_1));

                await client.WriteAsync("V700", float_number);
                Assert.True(client.ReadFloatAsync("V700").Result.ResultValue == float_number);
                await client.WriteAsync("V700", Convert.ToDouble(float_number));
                Assert.True(client.ReadDoubleAsync("V700").Result.ResultValue == Convert.ToDouble(float_number));

                //var rrr =  await client.WriteAsync("V1000", value_string);
                //  var ttttt = client.ReadString("V1000", value_string.Length);
                //  Assert.True(client.ReadString("V1000", value_string.Length).ResultValue == value_string);

                bool[] bool_values = { false, true, false, false, false, false, false, false, false, false
                        , false, false, false,false,false,false,false,false,false, true };
                await client.WriteAsync("V700", bool_values);
                var bool_values_result = client.ReadBooleanAsync("V700", bool_values.Length);
                for (int j = 0; j < bool_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(bool_values_result.Result.ResultValue[j] == bool_values[j]);

                }

                short[] short_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", short_values);
                var short_values_result = client.ReadInt16Async("V700", short_values.Length);
                for (int j = 0; j < short_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(short_values_result.Result.ResultValue[j] == short_values[j]);

                }

                ushort[] ushort_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", ushort_values);
                var ushort_values_result = client.ReadInt16Async("V700", ushort_values.Length);
                for (int j = 0; j < ushort_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(ushort_values_result.Result.ResultValue[j] == ushort_values[j]);

                }

                int[] int_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", int_values);
                var int_values_result = client.ReadInt32Async("V700", int_values.Length);
                for (int j = 0; j < int_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(int_values_result.Result.ResultValue[j] == int_values[j]);

                }

                uint[] uint_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", uint_values);
                var uint_values_result = client.ReadUInt32Async("V700", uint_values.Length);
                for (int j = 0; j < uint_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(uint_values_result.Result.ResultValue[j] == uint_values[j]);

                }

                long[] long_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", long_values);
                var long_values_result = client.ReadInt64Async("V700", long_values.Length);
                for (long j = 0; j < long_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(long_values_result.Result.ResultValue[j] == long_values[j]);

                }

                ulong[] ulong_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", ulong_values);
                var ulong_values_result = client.ReadUInt64Async("V700", ulong_values.Length);
                for (int j = 0; j < ulong_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(ulong_values_result.Result.ResultValue[j] == ulong_values[j]);

                }

                float[] float_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", float_values);
                var float_values_result = client.ReadFloatAsync("V700", float_values.Length);
                for (int j = 0; j < float_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(float_values_result.Result.ResultValue[j] == float_values[j]);

                }
                double[] double_values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                await client.WriteAsync("V700", double_values);
                var double_values_result = client.ReadDoubleAsync("V700", double_values.Length);
                for (int j = 0; j < double_values_result.Result.ResultValue.Length; j++)
                {
                    Assert.True(double_values_result.Result.ResultValue[j] == double_values[j]);

                }



            }

        }



    }
}

using System.Collections.Generic;
using System.IO.Ports;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;

using Xunit;

namespace Wombat.IndustrialCommunicationTest.Modbus
{
    public class ModbusRtuClient_tests
    {
        private ModbusRtuClient client;
        byte stationNumber = 1;//站号
        public ModbusRtuClient_tests()
        {
            //client = new ModbusRtuClient("COM3", 9600, 8, StopBits.One, Parity.None);
            client = new ModbusRtuClient("COM6", 9600, 8);
        }

        [Fact]
        public void  短连接自动开关()
        {
           var tttt  =  client.Connect();
            client.SetFunctionCode(3);
            short Number = 33;
            client.Write("4", Number);
            Assert.True(client.ReadInt16("4").Value == Number);

            Number = 34;
            client.Write("4", Number);
            Assert.True(client.ReadInt16("4").Value == Number);

            Number = 1;
            client.Write("12", Number);
            Assert.True(client.ReadInt16("12").Value == 1);

            Number = 0;
            client.Write("12", Number);
            Assert.True(client.ReadInt16("12").Value == 0);

            int numberInt32 = -12;
            client.Write("4", numberInt32);
            Assert.True(client.ReadInt32("4").Value == numberInt32);

            float numberFloat = 112;
            client.Write("4", numberFloat);
            Assert.True(client.ReadFloat("4").Value == numberFloat);

            double numberDouble = 32;
            client.Write("4", numberDouble);
            Assert.True(client.ReadDouble("4").Value == numberDouble);
        }

        [Fact]
        public void 长连接主动开关()
        {
            client.IsLongLivedConnection = true;

            client.Connect();

            short Number = 33;
            client.Write("4", Number);
            Assert.True(client.ReadInt16("4").Value == Number);

            Number = 34;
            client.Write("4", Number);
            Assert.True(client.ReadInt16("4").Value == Number);

            Number = 1;
            client.Write("12", Number);
            Assert.True(client.ReadInt16("12").Value == 1);

            Number = 0;
            client.Write("12", Number);
            Assert.True(client.ReadInt16("12").Value == 0);

            int numberInt32 = -12;
            client.Write("4", numberInt32);
            Assert.True(client.ReadInt32("4").Value == numberInt32);

            float numberFloat = 112;
            client.Write("4", numberFloat);
            Assert.True(client.ReadFloat("4").Value == numberFloat);

            double numberDouble = 32;
            client.Write("4", numberDouble);
            Assert.True(client.ReadDouble("4").Value == numberDouble);

            client.Disconnect();
        }

        [Fact]
        public void 批量读取()
        {
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

﻿using System.Collections.Generic;
using System.IO.Ports;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;

using Xunit;

namespace Wombat.IndustrialCommunicationTest.Modbus
{
    public class ModbusAsciiClient_tests
    {
        private ModbusAsciiClient client;
        byte stationNumber = 1;//站号
        public ModbusAsciiClient_tests()
        {
            client = new ModbusAsciiClient("COM3", 9600, 8, StopBits.One, Parity.None);
        }

        [Fact]
        public void 批量读取()
        {
            var list = new List<ModbusInput>();
            list.Add(new ModbusInput()
            {
                RegisterAddress = "2",
                DataType = DataTypeEnum.Int16,
                FunctionCode = 3,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                RegisterAddress = "2",
                DataType = DataTypeEnum.Int16,
                FunctionCode = 4,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                RegisterAddress = "5",
                DataType = DataTypeEnum.Int16,
                FunctionCode = 3,
                StationNumber = 1
            });
            list.Add(new ModbusInput()
            {
                RegisterAddress = "199",
                DataType = DataTypeEnum.Int16,
                FunctionCode = 3,
                StationNumber = 1
            });
            var result = client.BatchRead(list);
        }

        [Fact]
        public async Task 短连接自动开关()
        {
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
        public async Task 长连接主动开关()
        {
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
    }
}

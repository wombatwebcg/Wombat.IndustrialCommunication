using System;
using System.Collections.Generic;
using System.Text;

namespace Wombat.IndustrialCommunication.Modbus
{
    public static class ModbusExtensions
    {
        public static IModbusClient SetStationNumber(this IModbusClient modbusClient,byte stationNumber)
        {
            modbusClient.StationNumber = stationNumber;
            return modbusClient;
        }

        public static IModbusClient SetFunctionCode(this IModbusClient modbusClient, byte functionCode)
        {
            modbusClient.FunctionCode =functionCode;
            return modbusClient;
        }

        public static IModbusClient SetReadCoils(this IModbusClient modbusClient)
        {
            modbusClient.FunctionCode = 1;
            return modbusClient;
        }

        public static IModbusClient SetReadDiscreteInputs(this IModbusClient modbusClient)
        {
            modbusClient.FunctionCode = 2;
            return modbusClient;
        }

        public static IModbusClient SetReadHoldingRegisters(this IModbusClient modbusClient)
        {
            modbusClient.FunctionCode = 3;
            return modbusClient;
        }

        public static IModbusClient SetReadInputRegisters(this IModbusClient modbusClient)
        {
            modbusClient.FunctionCode = 4;
            return modbusClient;
        }

        public static IModbusClient SetWriteSingleCoil(this IModbusClient modbusClient)
        {
            modbusClient.FunctionCode = 5;
            return modbusClient;
        }

        public static IModbusClient SetWriteSingleRegister(this IModbusClient modbusClient)
        {
            modbusClient.FunctionCode = 6;
            return modbusClient;
        }

        public static IModbusClient SetWriteMultipleCoils(this IModbusClient modbusClient)
        {
            modbusClient.FunctionCode = 0xF;
            return modbusClient;
        }

        public static IModbusClient SetWriteMultipleRegister(this IModbusClient modbusClient)
        {
            modbusClient.FunctionCode = 0x10;
            return modbusClient;
        }


    }
}

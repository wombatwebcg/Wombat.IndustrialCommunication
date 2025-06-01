using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus.Data
{
    public class DataStore
    {
        private readonly object _syncRoot = new object();

        public DataStore()
        {
            // 默认构造函数
        }

        internal DataStore(ushort coilDiscretes, ushort inputDiscretes, ushort holdingRegisters, ushort inputRegisters)
        {
            CoilDiscretes = new MemoryLite<bool>(new bool[coilDiscretes], 0, coilDiscretes);
            InputDiscretes = new MemoryLite<bool>(new bool[inputDiscretes], 0, inputDiscretes);
            HoldingRegisters = new MemoryLite<int>(new int[holdingRegisters], 0, holdingRegisters);
            InputRegisters = new MemoryLite<int>(new int[inputRegisters], 0, inputRegisters);
        }

        public event EventHandler<DataStoreEventArgs> DataStoreWrittenTo;
        public event EventHandler<DataStoreEventArgs> DataStoreReadFrom;

        public MemoryLite<bool> CoilDiscretes { get; set; }
        public MemoryLite<bool> InputDiscretes { get; set; }
        public MemoryLite<int> HoldingRegisters { get; set; }
        public MemoryLite<int> InputRegisters { get; set; }

        public object SyncRoot
        {
            get { return _syncRoot; }
        }

        // 帮助方法，用于引发事件
        private static void RaiseEvent<T>(EventHandler<T> handler, object sender, T args) where T : EventArgs
        {
            if (handler != null)
            {
                handler(sender, args);
            }
        }

        internal static DiscreteCollection ReadCoils(DataStore dataStore, ushort startAddress, ushort count)
        {
            return ReadDiscretes(dataStore, dataStore.CoilDiscretes, startAddress, count, ModbusDataType.Coil);
        }

        internal static DiscreteCollection ReadInputs(DataStore dataStore, ushort startAddress, ushort count)
        {
            return ReadDiscretes(dataStore, dataStore.InputDiscretes, startAddress, count, ModbusDataType.Input);
        }

        internal static RegisterCollection ReadHoldingRegisters(DataStore dataStore, ushort startAddress, ushort count)
        {
            return ReadRegisters(dataStore, dataStore.HoldingRegisters, startAddress, count, ModbusDataType.HoldingRegister);
        }

        internal static RegisterCollection ReadInputRegisters(DataStore dataStore, ushort startAddress, ushort count)
        {
            return ReadRegisters(dataStore, dataStore.InputRegisters, startAddress, count, ModbusDataType.InputRegister);
        }

        private static DiscreteCollection ReadDiscretes(
            DataStore dataStore, 
            MemoryLite<bool> source, 
            ushort startAddress, 
            ushort count, 
            ModbusDataType dataType)
        {
            int startIndex = startAddress + 1;

            if (startIndex < 0 || source.Size < startIndex + count)
                throw new InvalidModbusRequestException(2);

            var discretes = new bool[count];
            lock (dataStore.SyncRoot)
            {
                for (int i = 0; i < count; i++)
                {
                    discretes[i] = source[startIndex + i];
                }
            }

            var result = new DiscreteCollection(discretes);
            RaiseEvent(dataStore.DataStoreReadFrom, dataStore, 
                DataStoreEventArgs.CreateDataStoreEventArgs(startAddress, dataType, discretes));

            return result;
        }

        private static RegisterCollection ReadRegisters(
            DataStore dataStore, 
            MemoryLite<int> source, 
            ushort startAddress, 
            ushort count, 
            ModbusDataType dataType)
        {
            int startIndex = startAddress + 1;

            if (startIndex < 0 || source.Size < startIndex + count)
                throw new InvalidModbusRequestException(2);

            var registers = new ushort[count];
            lock (dataStore.SyncRoot)
            {
                for (int i = 0; i < count; i++)
                {
                    registers[i] = (ushort)source[startIndex + i];
                }
            }

            var result = new RegisterCollection(registers);
            RaiseEvent(dataStore.DataStoreReadFrom, dataStore, 
                DataStoreEventArgs.CreateDataStoreEventArgs(startAddress, dataType, registers));

            return result;
        }

        internal static void WriteCoils(DataStore dataStore, ushort startAddress, DiscreteCollection values)
        {
            WriteDiscretes(dataStore, dataStore.CoilDiscretes, startAddress, values, ModbusDataType.Coil);
        }

        internal static void WriteInputs(DataStore dataStore, ushort startAddress, DiscreteCollection values)
        {
            WriteDiscretes(dataStore, dataStore.InputDiscretes, startAddress, values, ModbusDataType.Input);
        }

        internal static void WriteHoldingRegisters(DataStore dataStore, ushort startAddress, RegisterCollection values)
        {
            WriteRegisters(dataStore, dataStore.HoldingRegisters, startAddress, values, ModbusDataType.HoldingRegister);
        }

        internal static void WriteInputRegisters(DataStore dataStore, ushort startAddress, RegisterCollection values)
        {
            WriteRegisters(dataStore, dataStore.InputRegisters, startAddress, values, ModbusDataType.InputRegister);
        }

        private static void WriteDiscretes(
            DataStore dataStore, 
            MemoryLite<bool> destination, 
            ushort startAddress, 
            DiscreteCollection values, 
            ModbusDataType dataType)
        {
            int startIndex = startAddress + 1;

            if (startIndex < 0 || destination.Size < startIndex + values.Count)
                throw new InvalidModbusRequestException(2);

            lock (dataStore.SyncRoot)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    destination[startIndex + i] = values[i];
                }
            }

            RaiseEvent(dataStore.DataStoreWrittenTo, dataStore, 
                DataStoreEventArgs.CreateDataStoreEventArgs(startAddress, dataType, values));
        }

        private static void WriteRegisters(
            DataStore dataStore, 
            MemoryLite<int> destination, 
            ushort startAddress, 
            RegisterCollection values, 
            ModbusDataType dataType)
        {
            int startIndex = startAddress + 1;

            if (startIndex < 0 || destination.Size < startIndex + values.Count)
                throw new InvalidModbusRequestException(2);

            lock (dataStore.SyncRoot)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    destination[startIndex + i] = values[i];
                }
            }

            RaiseEvent(dataStore.DataStoreWrittenTo, dataStore, 
                DataStoreEventArgs.CreateDataStoreEventArgs(startAddress, dataType, values));
        }
    }
}

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
            CoilDiscretes = new ModbusDataCollection<bool> { ModbusDataType = ModbusDataType.Coil };
            InputDiscretes = new ModbusDataCollection<bool> { ModbusDataType = ModbusDataType.Input };
            HoldingRegisters = new ModbusDataCollection<ushort> { ModbusDataType = ModbusDataType.HoldingRegister };
            InputRegisters = new ModbusDataCollection<ushort> { ModbusDataType = ModbusDataType.InputRegister };
        }

        internal DataStore(IList<bool> coilDiscretes, IList<bool> inputDiscretes, IList<ushort> holdingRegisters, IList<ushort> inputRegisters)
        {
            CoilDiscretes = new ModbusDataCollection<bool>(coilDiscretes) { ModbusDataType = ModbusDataType.Coil };
            InputDiscretes = new ModbusDataCollection<bool>(inputDiscretes) { ModbusDataType = ModbusDataType.Input };
            HoldingRegisters = new ModbusDataCollection<ushort>(holdingRegisters) { ModbusDataType = ModbusDataType.HoldingRegister };
            InputRegisters = new ModbusDataCollection<ushort>(inputRegisters) { ModbusDataType = ModbusDataType.InputRegister };
        }

        public event EventHandler<DataStoreEventArgs> DataStoreWrittenTo;

        public event EventHandler<DataStoreEventArgs> DataStoreReadFrom;

        public ModbusDataCollection<bool> CoilDiscretes { get; private set; }

        public ModbusDataCollection<bool> InputDiscretes { get; private set; }

        public ModbusDataCollection<ushort> HoldingRegisters { get; private set; }

        public ModbusDataCollection<ushort> InputRegisters { get; private set; }

        public object SyncRoot
        {
            get { return _syncRoot; }
        }

        internal static T ReadData<T, U>(DataStore dataStore, ModbusDataCollection<U> dataSource, ushort startAddress,
ushort count, object syncRoot) where T : Collection<U>, new()
        {
            int startIndex = startAddress + 1;

            if (startIndex < 0 || dataSource.Count < startIndex + count)
                throw new InvalidModbusRequestException(2);

            U[] dataToRetrieve;
            lock (syncRoot)
                dataToRetrieve = dataSource.Slice(startIndex, count).ToArray();

            T result = new T();
            for (int i = 0; i < count; i++)
                result.Add(dataToRetrieve[i]);

            dataStore.DataStoreReadFrom.Raise(dataStore,
                DataStoreEventArgs.CreateDataStoreEventArgs(startAddress, dataSource.ModbusDataType, result));

            return result;
        }

        internal static void WriteData<TData>(DataStore dataStore, Span<TData> items,
ModbusDataCollection<TData> destination, ushort startAddress, object syncRoot)
        {
            //int startIndex = startAddress + 1;

            //if (startIndex < 0 || destination.Count < startIndex + items.Count())
            //    throw new InvalidModbusRequestException(2);

            //lock (syncRoot)
            //    Update(items, destination, startIndex);

            //dataStore.DataStoreWrittenTo.Raise(dataStore,
            //    DataStoreEventArgs.CreateDataStoreEventArgs(startAddress, destination.ModbusDataType, items));
        }

        internal static void Update<T>(IEnumerable<T> items, Span<T> destination, int startIndex)
        {
            //if (startIndex < 0 || destination.Count < startIndex + items.Count())
            //    throw new InvalidModbusRequestException(2);

            //int index = startIndex;
            //foreach (T item in items)
            //{
            //    destination[index] = item;
            //    ++index;
            //}
        }
    }
}

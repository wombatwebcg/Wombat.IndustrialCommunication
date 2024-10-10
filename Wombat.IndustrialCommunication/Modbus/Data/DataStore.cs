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
            //CoilDiscretes = new  MemoryLite<bool> { };
            //InputDiscretes = new  Memory<bool> { };
            //HoldingRegisters = new  Memory<int> {  };
            //InputRegisters = new Memory<int> { };
        }

        internal DataStore(ushort coilDiscretes, ushort inputDiscretes, ushort holdingRegisters, ushort inputRegisters)
        {
            CoilDiscretes = new MemoryLite<bool>( new bool[coilDiscretes],0,coilDiscretes) {  };
            InputDiscretes = new MemoryLite<bool>(new bool[inputDiscretes],0, inputDiscretes) { };
            HoldingRegisters = new MemoryLite<int>(new int[holdingRegisters],0, holdingRegisters) {};
            InputRegisters = new MemoryLite<int>(new int[inputRegisters],0, inputRegisters) { } ;
        }

        public event EventHandler<DataStoreEventArgs> DataStoreWrittenTo;

        public event EventHandler<DataStoreEventArgs> DataStoreReadFrom;

        public  MemoryLite<bool> CoilDiscretes { get; set; }

        public MemoryLite<bool> InputDiscretes { get; set; }

        public MemoryLite<int> HoldingRegisters { get;set; }

        public MemoryLite<int> InputRegisters { get; set; }

        public object SyncRoot
        {
            get { return _syncRoot; }
        }

//        internal static T ReadData<T, U>(DataStore dataStore,  Memory<U> dataSource, ushort startAddress,
//ushort count, object syncRoot) where T : Collection<U>, new()
//        {
//            int startIndex = startAddress + 1;

//            if (startIndex < 0 || dataSource.Count < startIndex + count)
//                throw new InvalidModbusRequestException(2);

//            U[] dataToRetrieve;
//            lock (syncRoot)
//                dataToRetrieve = dataSource.Slice(startIndex, count).ToArray();

//            T result = new T();
//            for (int i = 0; i < count; i++)
//                result.Add(dataToRetrieve[i]);

//            dataStore.DataStoreReadFrom.Raise(dataStore,
//                DataStoreEventArgs.CreateDataStoreEventArgs(startAddress, dataSource.ModbusDataType, result));

//            return result;
//        }

//        internal static void WriteData<TData>(DataStore dataStore, Span<TData> items,
// Memory<TData> destination, ushort startAddress, object syncRoot)
//        {
//            //int startIndex = startAddress + 1;

//            //if (startIndex < 0 || destination.Count < startIndex + items.Count())
//            //    throw new InvalidModbusRequestException(2);

//            //lock (syncRoot)
//            //    Update(items, destination, startIndex);

//            //dataStore.DataStoreWrittenTo.Raise(dataStore,
//            //    DataStoreEventArgs.CreateDataStoreEventArgs(startAddress, destination.ModbusDataType, items));
//        }

//        internal static void Update<T>(IEnumerable<T> items, Span<T> destination, int startIndex)
//        {
//            //if (startIndex < 0 || destination.Count < startIndex + items.Count())
//            //    throw new InvalidModbusRequestException(2);

//            //int index = startIndex;
//            //foreach (T item in items)
//            //{
//            //    destination[index] = item;
//            //    ++index;
//            //}
//        }
    }
}

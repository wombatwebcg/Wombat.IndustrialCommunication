using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Wombat.IndustrialCommunication.Modbus.Data
{


    public class DataStoreEventArgs : EventArgs
    {
        private DataStoreEventArgs(ushort startAddress, ModbusDataType modbusDataType)
        {
            this.StartAddress = startAddress;
            this.ModbusDataType = modbusDataType;
        }

        public ModbusDataType ModbusDataType { get; private set; }

        public ushort StartAddress { get; private set; }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public DiscriminatedUnion<ReadOnlyCollection<bool>, ReadOnlyCollection<ushort>> Data { get; private set; }

        internal static DataStoreEventArgs CreateDataStoreEventArgs<T>(ushort startAddress,
            ModbusDataType modbusDataType, IEnumerable<T> data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            DataStoreEventArgs eventArgs;

            if (typeof(T) == typeof(bool))
            {
                var a = new ReadOnlyCollection<bool>(data.Cast<bool>().ToArray());
                eventArgs = new DataStoreEventArgs(startAddress, modbusDataType)
                {
                    Data = DiscriminatedUnion<ReadOnlyCollection<bool>, ReadOnlyCollection<ushort>>.CreateA(a)
                };
            }
            else if (typeof(T) == typeof(ushort))
            {
                var b = new ReadOnlyCollection<ushort>(data.Cast<ushort>().ToArray());
                eventArgs = new DataStoreEventArgs(startAddress, modbusDataType)
                {
                    Data = DiscriminatedUnion<ReadOnlyCollection<bool>, ReadOnlyCollection<ushort>>.CreateB(b)
                };
            }
            else
            {
                throw new ArgumentException("Generic type T should be of type bool or ushort");
            }

            return eventArgs;
        }
    }
}

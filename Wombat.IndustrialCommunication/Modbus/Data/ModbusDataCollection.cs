using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication.Modbus.Data
{


    public class ModbusDataCollection<T>  : MemoryLite<T>
    {
        private bool _allowZeroElement = true;


        public ModbusDataCollection(int capacity):base(capacity)
        {
          
        }


        public ModbusDataCollection(T[] data, int start, int length, int numberOfSegments = 4) :base(data,start,length,numberOfSegments)
        {

        }


        internal ModbusDataType ModbusDataType { get; set; }

        private static IList<T> AddDefault(IList<T> data)
        {
            data.Insert(0, default(T));
            return data;
        }

    }
}

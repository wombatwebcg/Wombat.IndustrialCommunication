using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Wombat.IndustrialCommunication.Modbus.Data
{


    public class ModbusDataCollection<TData> :Collection<TData>
    {
        private bool _allowZeroElement = true;

        public ModbusDataCollection(int capacity)
        {

        }

        public ModbusDataCollection(params TData[] data): this((IList<TData>)data)
        {
        }

        public ModbusDataCollection(IList<TData> data): base(AddDefault(data.IsReadOnly ? new List<TData>(data) : data))
        {
            _allowZeroElement = false;
        }

        internal ModbusDataType ModbusDataType { get; set; }

        private static IList<TData> AddDefault(IList<TData> data)
        {
            data.Insert(0, default(TData));
            return data;
        }

        protected override void InsertItem(int index, TData item)
        {

            base.InsertItem(index, item);
        }

        protected override  void SetItem(int index, TData item)
        {
           base.SetItem(index, item);
        }

        protected override void RemoveItem(int index)
        {

            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            _allowZeroElement = true;
            base.ClearItems();
            _allowZeroElement = false;
        }
    }
}

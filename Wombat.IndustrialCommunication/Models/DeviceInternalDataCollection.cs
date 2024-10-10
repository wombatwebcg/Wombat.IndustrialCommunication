using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Xml.Linq;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication
{
    public class DeviceInternalDataCollection<T> : Collection<T>
        where T : IDeviceInternalData, new()
    {
        public DeviceInternalDataCollection(int capacity) 
        {
            for (int i = 0; i < capacity; i++)
            {
                this.Insert(i,new T() { });

            }
        }

        public DeviceInternalDataCollection(params T[] data) : this((IList<T>)data)
        {
        }

        public DeviceInternalDataCollection(IList<T> data) : base(data.IsReadOnly ? new List<T>(data) : data)
        {

        }

        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index) 
        { 
            base.RemoveItem(index); 
        }

        protected override void ClearItems()
        {
            base.ClearItems();  
        }

        protected override void SetItem(int index, T item)
        {
            base.SetItem(index, item);  
        }
    }
}

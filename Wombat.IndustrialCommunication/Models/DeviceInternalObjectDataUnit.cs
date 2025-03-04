using System;
using System.ComponentModel;
using System.Reactive.Subjects;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication
{

    public class DeviceInternalObjectDataUnit : IDeviceInternalData, INotifyPropertyChanged
    {
        public ISubject<DeviceInternalObjectDataUnit> Subject=>_subject;


        private object _dataValue;
        private ISubject<DeviceInternalObjectDataUnit> _subject;

        public DeviceInternalObjectDataUnit()
        {
            _subject = new Subject<DeviceInternalObjectDataUnit>();
        }

        public string Name { get; set; }

        public int Index { get; set; }

        public DataTypeEnums DataTypeEnum { get; set; }


        volatile bool _isUpdate = false;

        public object DataValue
        {
            get => _dataValue;
            set
            {
                if (!Equals(_dataValue, value) && !_isUpdate)
                {
                    _dataValue = value;
                    _isUpdate = true;
                    OnPropertyChanged(nameof(DataValue));
                    NotifyObservers();
                    _isUpdate = false;
                }
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void NotifyObservers()
        {
            _subject.OnNext(this);
        }
    }
}

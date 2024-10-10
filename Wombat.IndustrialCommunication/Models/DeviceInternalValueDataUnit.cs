using System;
using System.ComponentModel;
using System.Reactive.Subjects;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Models;

namespace Wombat.IndustrialCommunication
{

    public class DeviceInternalValueDataUnit<T> : IDeviceInternalData, INotifyPropertyChanged where T : struct
    {
        public ISubject<DeviceInternalValueDataUnit<T>> Subject=>_subject;


        private T _dataValue;
        private ISubject<DeviceInternalValueDataUnit<T>> _subject;

        public DeviceInternalValueDataUnit()
        {
            _subject = new Subject<DeviceInternalValueDataUnit<T>>();
        }

        public string Name { get; set; }
        public int Index { get; set; }

        volatile bool _isUpdate = false;
        public T DataValue
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

        // 通知观察者
        private void NotifyObservers()
        {
            _subject.OnNext(this);
        }
    }
}

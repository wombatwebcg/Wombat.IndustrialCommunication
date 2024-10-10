using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{

    public delegate Task DeviceIOValueEventHandler<T>(DeviceInternalValueDataUnit<T> device) where T : struct;


    public class DeviceIOValueObserver<T> where T : struct
    {
        // 定义事件，供外部订阅
        public event DeviceIOValueEventHandler<T> DeviceIOValueTaskCompleted;
        public event DeviceIOValueEventHandler<T> DeviceIOValueChanged;


        public DeviceIOValueObserver(IObservable<DeviceInternalValueDataUnit<T>> observable)
        {
            // 订阅数据流
            observable
                .Where(device => device != null)
                .DistinctUntilChanged()
                .ObserveOn(TaskPoolScheduler.Default)
                .Subscribe(async device =>
                {
                    await HandleDeviceUpdateAsync(device);
                });
        }

        // 处理设备更新的异步方法
        private async Task HandleDeviceUpdateAsync(DeviceInternalValueDataUnit<T> device)
        {

            // 触发异步硬件操作运行事件
            if (DeviceIOValueChanged != null)
            {
                await DeviceIOValueChanged(device);
            }

            // 触发异步硬件操作完成事件
            if (DeviceIOValueTaskCompleted != null)
            {
                await DeviceIOValueTaskCompleted(device);
            }
        }


    }
}

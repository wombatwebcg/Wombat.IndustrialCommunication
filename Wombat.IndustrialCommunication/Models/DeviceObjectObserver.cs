using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    public delegate Task DeviceObjectEventHandler(DeviceInternalObjectDataUnit device);



    public class DeviceObjectObserver
    {
        // 定义事件，供外部订阅
        public event DeviceObjectEventHandler DeviceObjectTaskCompleted;
        public event DeviceObjectEventHandler DeviceObjectChanged;

        public DeviceObjectObserver(IObservable<DeviceInternalObjectDataUnit> observable)
        {
            // 订阅数据流
            observable
                .Where(device => device != null) // 可以在这里添加过滤条件
                .Subscribe(async device =>
                {
                    await HandleDeviceUpdateAsync(device);
                });
        }

        // 处理设备更新的异步方法
        private async Task HandleDeviceUpdateAsync(DeviceInternalObjectDataUnit device)
        {

            // 触发异步硬件操作运行事件
            if (DeviceObjectChanged != null)
            {
                await DeviceObjectChanged(device);
            }

            // 触发异步硬件操作完成事件
            if (DeviceObjectTaskCompleted != null)
            {
                await DeviceObjectTaskCompleted(device);
            }
        }


    }
}

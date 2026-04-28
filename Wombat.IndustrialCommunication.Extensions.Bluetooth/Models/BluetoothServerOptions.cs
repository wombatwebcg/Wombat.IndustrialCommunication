using System;
using Wombat.IndustrialCommunication;

namespace Wombat.IndustrialCommunication.Extensions.Bluetooth.Models
{
    /// <summary>
    /// 本机蓝牙服务端配置。
    /// </summary>
    public class BluetoothServerOptions
    {
        public string ServiceId { get; set; }
        public string WriteCharacteristicId { get; set; }
        public string NotifyCharacteristicId { get; set; }
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(3);
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(3);

        public OperationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ServiceId))
            {
                return OperationResult.CreateFailedResult("ServiceId 不能为空");
            }

            if (string.IsNullOrWhiteSpace(WriteCharacteristicId))
            {
                return OperationResult.CreateFailedResult("WriteCharacteristicId 不能为空");
            }

            if (string.IsNullOrWhiteSpace(NotifyCharacteristicId))
            {
                return OperationResult.CreateFailedResult("NotifyCharacteristicId 不能为空");
            }

            if (ConnectTimeout <= TimeSpan.Zero || ReceiveTimeout <= TimeSpan.Zero || SendTimeout <= TimeSpan.Zero)
            {
                return OperationResult.CreateFailedResult("超时参数必须大于 0");
            }

            return OperationResult.CreateSuccessResult();
        }
    }
}

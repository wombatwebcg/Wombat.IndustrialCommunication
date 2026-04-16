using System;
using Wombat.IndustrialCommunication;

namespace Wombat.IndustrialCommunication.Extensions.Bluetooth.Models
{
    public class BluetoothConnectionOptions
    {
        public string DeviceId { get; set; }
        public string ServiceId { get; set; }
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(3);
        public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(3);
        public int ReadChunkSize { get; set; } = 256;
        public int WriteChunkSize { get; set; } = 256;

        public OperationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(DeviceId))
            {
                return OperationResult.CreateFailedResult("DeviceId 不能为空");
            }

            if (ConnectTimeout <= TimeSpan.Zero || ReceiveTimeout <= TimeSpan.Zero || SendTimeout <= TimeSpan.Zero)
            {
                return OperationResult.CreateFailedResult("超时参数必须大于 0");
            }

            if (ReadChunkSize <= 0 || WriteChunkSize <= 0)
            {
                return OperationResult.CreateFailedResult("分片大小必须大于 0");
            }

            return OperationResult.CreateSuccessResult();
        }
    }
}

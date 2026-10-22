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

        /// <summary>
        /// Modbus RTU 从站地址（1-247）。
        /// </summary>
        public int SlaveId { get; set; } = 1;

        /// <summary>
        /// 允许的最大并发连接数。预留字段，多会话（R5）启用前不产生行为。
        /// </summary>
        public int MaxConnections { get; set; } = 1;

        /// <summary>
        /// BLE 单包有效载荷上限（字节）。预留字段，MTU 分片（R4）启用前不产生行为。
        /// </summary>
        public int Mtu { get; set; } = 247;

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

            if (SlaveId < 1 || SlaveId > 247)
            {
                return OperationResult.CreateFailedResult("SlaveId 必须在 1 到 247 之间");
            }

            if (MaxConnections < 1)
            {
                return OperationResult.CreateFailedResult("MaxConnections 必须大于等于 1");
            }

            if (Mtu < 23)
            {
                return OperationResult.CreateFailedResult("Mtu 必须大于等于 23");
            }

            return OperationResult.CreateSuccessResult();
        }
    }
}

using System;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Modbus;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Models;

namespace Wombat.IndustrialCommunication.Extensions.Bluetooth.Factory
{
    /// <summary>
    /// 蓝牙服务端工厂：按选项创建并配置 Modbus RTU 蓝牙从站。
    /// </summary>
    public static class BluetoothServerFactory
    {
        /// <summary>
        /// 创建 Modbus RTU 蓝牙从站。
        /// </summary>
        /// <param name="channel">服务端蓝牙通道（由宿主实现，如实现 <see cref="IBluetoothServerChannel"/> 则可被识别为外设角色）。</param>
        /// <param name="options">服务端选项；为 null 时使用从站默认值（SlaveId=1）与通道既有超时。</param>
        /// <param name="logger">可选日志记录器。</param>
        public static ModbusRtuBluetoothServer CreateModbusRtuServer(
            IBluetoothChannel channel,
            BluetoothServerOptions options = null,
            ILogger logger = null)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            if (options != null)
            {
                var validateResult = options.Validate();
                if (!validateResult.IsSuccess)
                {
                    throw new ArgumentException(validateResult.Message, nameof(options));
                }

                channel.ConnectTimeout = options.ConnectTimeout;
                channel.ReceiveTimeout = options.ReceiveTimeout;
                channel.SendTimeout = options.SendTimeout;
            }

            var server = new ModbusRtuBluetoothServer(channel);
            if (options != null)
            {
                server.SlaveId = (byte)options.SlaveId;
                server.ConnectTimeout = options.ConnectTimeout;
                server.ReceiveTimeout = options.ReceiveTimeout;
                server.SendTimeout = options.SendTimeout;
            }

            if (logger != null)
            {
                server.UseLogger(logger);
            }

            return server;
        }
    }
}

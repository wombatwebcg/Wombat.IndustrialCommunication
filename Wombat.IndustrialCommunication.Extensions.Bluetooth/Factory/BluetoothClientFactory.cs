using System;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Modbus;
using Wombat.IndustrialCommunication.Extensions.Bluetooth.Models;

namespace Wombat.IndustrialCommunication.Extensions.Bluetooth.Factory
{
    public static class BluetoothClientFactory
    {
        public static ModbusRtuBluetoothClient CreateModbusRtuClient(IBluetoothChannel channel, BluetoothConnectionOptions options = null)
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

            var client = new ModbusRtuBluetoothClient(channel);
            if (options != null)
            {
                client.ConnectTimeout = options.ConnectTimeout;
                client.ReceiveTimeout = options.ReceiveTimeout;
                client.SendTimeout = options.SendTimeout;
            }

            return client;
        }
    }
}

using System;
using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.Gateway
{
    /// <summary>
    /// 网关设备工厂实现类，封装IDeviceFactory接口
    /// </summary>
    public class GatewayDeviceFactory : IGatewayDeviceFactory
    {
        private readonly IDeviceFactory _deviceFactory;
        private readonly ILogger _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="deviceFactory">设备工厂实例</param>
        /// <param name="logger">日志记录器</param>
        public GatewayDeviceFactory(IDeviceFactory deviceFactory, ILogger logger = null)
        {
            _deviceFactory = deviceFactory ?? throw new ArgumentNullException(nameof(deviceFactory));
            _logger = logger;
        }

        /// <summary>
        /// 创建Modbus TCP设备
        /// </summary>
        public IGatewayDevice CreateModbusTcpDevice(string ip, int port = 502)
        {
            _logger?.LogDebug("创建Modbus TCP设备：{IP}:{Port}", ip, port);
            var client = _deviceFactory.CreateModbusTcpClient(ip, port);
            return new GatewayDevice(client);
        }

        /// <summary>
        /// 创建Modbus RTU设备
        /// </summary>
        public IGatewayDevice CreateModbusRtuDevice(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None)
        {
            _logger?.LogDebug("创建Modbus RTU设备：{PortName}, {BaudRate}, {DataBits}, {StopBits}, {Parity}, {Handshake}", 
                portName, baudRate, dataBits, stopBits, parity, handshake);
            var client = _deviceFactory.CreateModbusRtuClient(portName, baudRate, dataBits, stopBits, parity, handshake);
            return new GatewayDevice(client);
        }

        /// <summary>
        /// 创建西门子S7设备
        /// </summary>
        public IGatewayDevice CreateSiemensDevice(string ip, int port, SiemensVersion siemensVersion, byte slot = 0, byte rack = 0)
        {
            _logger?.LogDebug("创建西门子S7设备：{IP}:{Port}, {SiemensVersion}, Slot:{Slot}, Rack:{Rack}", 
                ip, port, siemensVersion, slot, rack);
            var client = _deviceFactory.CreateSiemensClient(ip, port, siemensVersion, slot, rack);
            return new GatewayDevice(client);
        }

        /// <summary>
        /// 创建带连接池的Modbus TCP设备
        /// </summary>
        public IGatewayDevice CreateModbusTcpDeviceWithPool(int connectionPoolSize, string ip, int port = 502)
        {
            _logger?.LogDebug("创建带连接池的Modbus TCP设备：连接池大小:{PoolSize}, {IP}:{Port}", 
                connectionPoolSize, ip, port);
            var pool = _deviceFactory.CreateModbusTcpClientPool(connectionPoolSize, ip, port);
            return new GatewayDevice(pool);
        }

        /// <summary>
        /// 创建带连接池的Modbus RTU设备
        /// </summary>
        public IGatewayDevice CreateModbusRtuDeviceWithPool(int connectionPoolSize, string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None)
        {
            _logger?.LogDebug("创建带连接池的Modbus RTU设备：连接池大小:{PoolSize}, {PortName}, {BaudRate}, {DataBits}, {StopBits}, {Parity}, {Handshake}", 
                connectionPoolSize, portName, baudRate, dataBits, stopBits, parity, handshake);
            var pool = _deviceFactory.CreateModbusRtuClientPool(connectionPoolSize, portName, baudRate, dataBits, stopBits, parity, handshake);
            return new GatewayDevice(pool);
        }

        /// <summary>
        /// 创建带连接池的西门子S7设备
        /// </summary>
        public IGatewayDevice CreateSiemensDeviceWithPool(int connectionPoolSize, string ip, int port, SiemensVersion siemensVersion, byte slot = 0, byte rack = 0)
        {
            _logger?.LogDebug("创建带连接池的西门子S7设备：连接池大小:{PoolSize}, {IP}:{Port}, {SiemensVersion}, Slot:{Slot}, Rack:{Rack}", 
                connectionPoolSize, ip, port, siemensVersion, slot, rack);
            var pool = _deviceFactory.CreateSiemensClientPool(connectionPoolSize, ip, port, siemensVersion, slot, rack);
            return new GatewayDevice(pool);
        }
    }
} 
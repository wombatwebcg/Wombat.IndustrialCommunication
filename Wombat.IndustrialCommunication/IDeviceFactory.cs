using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 设备工厂接口，用于创建各种工业通信设备客户端
    /// </summary>
    public interface IDeviceFactory
    {
        /// <summary>
        /// 创建Modbus TCP客户端
        /// </summary>
        /// <param name="ip">设备IP地址</param>
        /// <param name="port">设备端口号，默认502</param>
        /// <returns>Modbus TCP客户端实例</returns>
        IDeviceClient CreateModbusTcpClient(string ip, int port = 502);

        /// <summary>
        /// 创建Modbus RTU客户端
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率，默认9600</param>
        /// <param name="dataBits">数据位，默认8</param>
        /// <param name="stopBits">停止位，默认1</param>
        /// <param name="parity">校验位，默认None</param>
        /// <param name="handshake">握手协议，默认None</param>
        /// <returns>Modbus RTU客户端实例</returns>
        IDeviceClient CreateModbusRtuClient(string portName, int baudRate = 9600, int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, System.IO.Ports.Handshake handshake = System.IO.Ports.Handshake.None);

        /// <summary>
        /// 创建西门子S7客户端
        /// </summary>
        /// <param name="ip">设备IP地址</param>
        /// <param name="port">设备端口号</param>
        /// <param name="siemensVersion">西门子PLC版本</param>
        /// <param name="slot">槽号，默认0</param>
        /// <param name="rack">机架号，默认0</param>
        /// <returns>西门子S7客户端实例</returns>
        IDeviceClient CreateSiemensClient(string ip, int port, SiemensVersion siemensVersion, byte slot = 0, byte rack = 0);
        
        /// <summary>
        /// 创建Modbus TCP服务器
        /// </summary>
        /// <param name="ip">服务器IP地址，默认0.0.0.0（所有可用IP）</param>
        /// <param name="port">服务器端口号，默认502</param>
        /// <returns>Modbus TCP服务器实例</returns>
        IDeviceServer CreateModbusTcpServer(string ip = "0.0.0.0", int port = 502);
        
        /// <summary>
        /// 创建Modbus RTU服务器
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率，默认9600</param>
        /// <param name="dataBits">数据位，默认8</param>
        /// <param name="stopBits">停止位，默认1</param>
        /// <param name="parity">校验位，默认None</param>
        /// <param name="handshake">握手协议，默认None</param>
        /// <returns>Modbus RTU服务器实例</returns>
        IDeviceServer CreateModbusRtuServer(string portName, int baudRate = 9600, int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, System.IO.Ports.Handshake handshake = System.IO.Ports.Handshake.None);
        
        /// <summary>
        /// 创建西门子S7服务器
        /// </summary>
        /// <param name="ip">服务器IP地址，默认0.0.0.0（所有可用IP）</param>
        /// <param name="port">服务器端口号，默认102</param>
        /// <param name="siemensVersion">西门子PLC版本</param>
        /// <returns>西门子S7服务器实例</returns>
        IDeviceServer CreateSiemensServer(string ip = "0.0.0.0", int port = 102, SiemensVersion siemensVersion = SiemensVersion.S7_1200);
        
        /// <summary>
        /// 创建带连接池的Modbus TCP客户端
        /// </summary>
        /// <param name="connectionPoolSize">连接池大小，默认10</param>
        /// <param name="ip">设备IP地址</param>
        /// <param name="port">设备端口号，默认502</param>
        /// <returns>Modbus TCP客户端连接池</returns>
        ConnectionPoolManager<IDeviceClient> CreateModbusTcpClientPool(int connectionPoolSize, string ip, int port = 502);
        
        /// <summary>
        /// 创建带连接池的Modbus RTU客户端
        /// </summary>
        /// <param name="connectionPoolSize">连接池大小，默认10</param>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率，默认9600</param>
        /// <param name="dataBits">数据位，默认8</param>
        /// <param name="stopBits">停止位，默认1</param>
        /// <param name="parity">校验位，默认None</param>
        /// <param name="handshake">握手协议，默认None</param>
        /// <returns>Modbus RTU客户端连接池</returns>
        ConnectionPoolManager<IDeviceClient> CreateModbusRtuClientPool(int connectionPoolSize, string portName, int baudRate = 9600, int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, System.IO.Ports.Handshake handshake = System.IO.Ports.Handshake.None);
        
        /// <summary>
        /// 创建带连接池的西门子S7客户端
        /// </summary>
        /// <param name="connectionPoolSize">连接池大小，默认10</param>
        /// <param name="ip">设备IP地址</param>
        /// <param name="port">设备端口号</param>
        /// <param name="siemensVersion">西门子PLC版本</param>
        /// <param name="slot">槽号，默认0</param>
        /// <param name="rack">机架号，默认0</param>
        /// <returns>西门子S7客户端连接池</returns>
        ConnectionPoolManager<IDeviceClient> CreateSiemensClientPool(int connectionPoolSize, string ip, int port, SiemensVersion siemensVersion, byte slot = 0, byte rack = 0);
    }

    /// <summary>
    /// 设备工厂实现类
    /// </summary>
    public class DeviceFactory : IDeviceFactory
    {
        public IDeviceClient CreateModbusTcpClient(string ip, int port = 502)
        {
            return new Modbus.ModbusTcpClient(ip, port);
        }

        public IDeviceClient CreateModbusRtuClient(string portName, int baudRate = 9600, int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, System.IO.Ports.Handshake handshake = System.IO.Ports.Handshake.None)
        {
            return new Modbus.ModbusRTUClient(portName, baudRate, dataBits, stopBits, parity, handshake);
        }

        public IDeviceClient CreateSiemensClient(string ip, int port, SiemensVersion siemensVersion, byte slot = 0, byte rack = 0)
        {
            return new PLC.SiemensClient(ip, port, siemensVersion, slot, rack);
        }
        
        public IDeviceServer CreateModbusTcpServer(string ip = "0.0.0.0", int port = 502)
        {
            return new Modbus.ModbusTcpServer(ip, port);
        }
        
        public IDeviceServer CreateModbusRtuServer(string portName, int baudRate = 9600, int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, System.IO.Ports.Handshake handshake = System.IO.Ports.Handshake.None)
        {
            return new Modbus.ModbusRtuServer(portName, baudRate, dataBits, stopBits, parity, handshake);
        }
        
        public IDeviceServer CreateSiemensServer(string ip = "0.0.0.0", int port = 102, SiemensVersion siemensVersion = SiemensVersion.S7_1200)
        {
            var server = new PLC.S7TcpServer(ip, port);
            ((PLC.S7TcpServerBase)server).SiemensVersion = siemensVersion;
            return server;
        }
        
        public ConnectionPoolManager<IDeviceClient> CreateModbusTcpClientPool(int connectionPoolSize, string ip, int port = 502)
        {
            // 创建连接池配置
            var config = new ConnectionPoolConfig { MaxPoolSize = connectionPoolSize };
            
            // 创建连接工厂方法
            Func<string, IDeviceClient> connectionFactory = (connectionId) => new Modbus.ModbusTcpClient(ip, port);
            
            return new ConnectionPoolManager<IDeviceClient>(connectionFactory, config);
        }
        
        public ConnectionPoolManager<IDeviceClient> CreateModbusRtuClientPool(int connectionPoolSize, string portName, int baudRate = 9600, int dataBits = 8, System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One, System.IO.Ports.Parity parity = System.IO.Ports.Parity.None, System.IO.Ports.Handshake handshake = System.IO.Ports.Handshake.None)
        {
            // 创建连接池配置
            var config = new ConnectionPoolConfig { MaxPoolSize = connectionPoolSize };
            
            // 创建连接工厂方法
            Func<string, IDeviceClient> connectionFactory = (connectionId) => 
                new Modbus.ModbusRTUClient(portName, baudRate, dataBits, stopBits, parity, handshake);
            
            return new ConnectionPoolManager<IDeviceClient>(connectionFactory, config);
        }
        
        public ConnectionPoolManager<IDeviceClient> CreateSiemensClientPool(int connectionPoolSize, string ip, int port, SiemensVersion siemensVersion, byte slot = 0, byte rack = 0)
        {
            // 创建连接池配置
            var config = new ConnectionPoolConfig { MaxPoolSize = connectionPoolSize };
            
            // 创建连接工厂方法
            Func<string, IDeviceClient> connectionFactory = (connectionId) => 
                new PLC.SiemensClient(ip, port, siemensVersion, slot, rack);
            
            return new ConnectionPoolManager<IDeviceClient>(connectionFactory, config);
        }
    }
}

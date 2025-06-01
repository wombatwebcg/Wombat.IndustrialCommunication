using System;
using System.IO.Ports;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.Gateway
{
    /// <summary>
    /// 网关设备工厂接口，用于创建各种工业通信设备的网关访问实例
    /// </summary>
    public interface IGatewayDeviceFactory
    {
        /// <summary>
        /// 创建Modbus TCP设备
        /// </summary>
        /// <param name="ip">设备IP地址</param>
        /// <param name="port">设备端口号，默认502</param>
        /// <returns>网关设备实例</returns>
        IGatewayDevice CreateModbusTcpDevice(string ip, int port = 502);
        
        /// <summary>
        /// 创建Modbus RTU设备
        /// </summary>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率，默认9600</param>
        /// <param name="dataBits">数据位，默认8</param>
        /// <param name="stopBits">停止位，默认1</param>
        /// <param name="parity">校验位，默认None</param>
        /// <param name="handshake">握手协议，默认None</param>
        /// <returns>网关设备实例</returns>
        IGatewayDevice CreateModbusRtuDevice(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None);
        
        /// <summary>
        /// 创建西门子S7设备
        /// </summary>
        /// <param name="ip">设备IP地址</param>
        /// <param name="port">设备端口号</param>
        /// <param name="siemensVersion">西门子PLC版本</param>
        /// <param name="slot">槽号，默认0</param>
        /// <param name="rack">机架号，默认0</param>
        /// <returns>网关设备实例</returns>
        IGatewayDevice CreateSiemensDevice(string ip, int port, SiemensVersion siemensVersion, byte slot = 0, byte rack = 0);
        
        /// <summary>
        /// 创建带连接池的Modbus TCP设备
        /// </summary>
        /// <param name="connectionPoolSize">连接池大小，默认10</param>
        /// <param name="ip">设备IP地址</param>
        /// <param name="port">设备端口号，默认502</param>
        /// <returns>网关设备实例</returns>
        IGatewayDevice CreateModbusTcpDeviceWithPool(int connectionPoolSize, string ip, int port = 502);
        
        /// <summary>
        /// 创建带连接池的Modbus RTU设备
        /// </summary>
        /// <param name="connectionPoolSize">连接池大小，默认10</param>
        /// <param name="portName">串口名称</param>
        /// <param name="baudRate">波特率，默认9600</param>
        /// <param name="dataBits">数据位，默认8</param>
        /// <param name="stopBits">停止位，默认1</param>
        /// <param name="parity">校验位，默认None</param>
        /// <param name="handshake">握手协议，默认None</param>
        /// <returns>网关设备实例</returns>
        IGatewayDevice CreateModbusRtuDeviceWithPool(int connectionPoolSize, string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None);
        
        /// <summary>
        /// 创建带连接池的西门子S7设备
        /// </summary>
        /// <param name="connectionPoolSize">连接池大小，默认10</param>
        /// <param name="ip">设备IP地址</param>
        /// <param name="port">设备端口号</param>
        /// <param name="siemensVersion">西门子PLC版本</param>
        /// <param name="slot">槽号，默认0</param>
        /// <param name="rack">机架号，默认0</param>
        /// <returns>网关设备实例</returns>
        IGatewayDevice CreateSiemensDeviceWithPool(int connectionPoolSize, string ip, int port, SiemensVersion siemensVersion, byte slot = 0, byte rack = 0);
    }
} 
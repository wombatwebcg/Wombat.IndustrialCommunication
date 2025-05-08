using System;
using System.Collections.Generic;
using System.Text;
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
    }
}

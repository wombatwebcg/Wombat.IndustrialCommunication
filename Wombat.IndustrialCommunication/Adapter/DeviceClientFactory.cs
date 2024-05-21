using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication.Adapter
{
    public class DeviceClientFactory
    {
        /// <summary>
        /// 创建以太网类型的设备连接
        /// </summary>
        /// <param name="deviceVersion">设备类型</param>
        /// <param name="ip">ip地址</param>
        /// <param name="port">端口号</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="format">大小端设置</param>
        /// <returns></returns>
        public static IPLCEthernetClient CreatePLCEthernetDevice(EthernetDeviceVersion deviceVersion, string ip, int port, int timeout = 1500, EndianFormat format = EndianFormat.ABCD,bool isReverse = false)
        {
            IPLCEthernetClient iotClientCommon;
            switch (deviceVersion)
            { 
                case EthernetDeviceVersion.Siemens_S7_200:
                    iotClientCommon = new SiemensClient(SiemensVersion.S7_200, ip, port);
                    break;
                case EthernetDeviceVersion.Siemens_S7_200Smart:
                    iotClientCommon = new SiemensClient(SiemensVersion.S7_200Smart, ip, port);
                    break;
                case EthernetDeviceVersion.Siemens_S7_300:
                    iotClientCommon = new SiemensClient(SiemensVersion.S7_300, ip, port);
                    break;
                case EthernetDeviceVersion.Siemens_S7_400:
                    iotClientCommon = new SiemensClient(SiemensVersion.S7_400, ip, port);
                    break;
                case EthernetDeviceVersion.Siemens_S7_1200:
                    iotClientCommon = new SiemensClient(SiemensVersion.S7_1200, ip, port);
                    break;
                case EthernetDeviceVersion.Siemens_S7_1500:
                    iotClientCommon = new SiemensClient(SiemensVersion.S7_1500, ip, port);
                    break;
                case EthernetDeviceVersion.OmronFins:
                    iotClientCommon = new OmronFinsClient(ip, port);
                    break;
                case EthernetDeviceVersion.AllenBradley:
                    iotClientCommon = new AllenBradleyClient(ip, port);
                    break;
                case EthernetDeviceVersion.Mitsubishi_A_1E:
                    iotClientCommon = new MitsubishiClient(MitsubishiVersion.A_1E, ip, port);
                    break;
                case EthernetDeviceVersion.Mitsubishi_Qna_3E:
                    iotClientCommon = new MitsubishiClient(MitsubishiVersion.Qna_3E, ip, port);
                    break;
                default:
                    throw new Exception($"类型[{deviceVersion}]暂未实现");
            }

            iotClientCommon.Timeout = TimeSpan.FromMilliseconds(timeout);
            iotClientCommon.Timeout = TimeSpan.FromMilliseconds(timeout);
            //iotClientCommon.DataFormat = format;
            //iotClientCommon.IsReverse = isReverse;
            return iotClientCommon;
        }






        /// <summary>
        /// 创建以太网类型的设备连接
        /// </summary>
        /// <param name="deviceVersion">设备类型</param>
        /// <param name="ip">ip地址</param>
        /// <param name="port">端口号</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="format">大小端设置</param>
        /// <returns></returns>
        public static IModbusEthernetClient CreateModbusEthernetDevice(EthernetDeviceVersion deviceVersion, string ip, int port, int timeout = 1500, EndianFormat format = EndianFormat.ABCD,bool Isreve = false)
        {
            IModbusEthernetClient iotClientCommon;
            switch (deviceVersion)

            {
                case EthernetDeviceVersion.ModbusTcp:
                    iotClientCommon = new ModbusTcpClient(ip, port);
                    break;
                case EthernetDeviceVersion.ModbusRtuOverTcp:
                    iotClientCommon = new ModbusRtuOverTcpClient(ip, port);
                    break;
                default:
                    throw new Exception($"类型[{deviceVersion}]暂未实现");
            }

            iotClientCommon.Timeout = TimeSpan.FromMilliseconds(timeout);
            iotClientCommon.DataFormat = format;
            //iotClientCommon.DataFormat = format;
            //iotClientCommon.IsReverse = isReverse;
            return iotClientCommon;
        }






        /// <summary>
        /// 创建串口类型的设备连接
        /// </summary>
        /// <param name="deviceVersion">设备类型</param>
        /// <param name="portName">COM端口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">奇偶校验</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="format">大小端设置</param>
        /// <returns></returns>
        public static IPLCSerialPortClient CreatePLCSerialPortClient(SerialDeviceVersion deviceVersion, string portName, int baudRate, int dataBits, StopBits stopBits, Parity parity, int timeout = 1500, EndianFormat format = EndianFormat.ABCD, bool Isreve = false)
        {
            IPLCSerialPortClient iotClientCommon;
            switch (deviceVersion)
            {
                case SerialDeviceVersion.MitsubishiFxSerial:
                    iotClientCommon = new MitsubishiFxSerialClient(portName, baudRate, dataBits, stopBits, parity);
                    break;
                default:
                    throw new Exception($"类型[{deviceVersion}]暂未实现");
            }
            iotClientCommon.Timeout = TimeSpan.FromMilliseconds(timeout);
            iotClientCommon.DataFormat = format;
            //iotClientCommon.DataFormat = format;
            //iotClientCommon.IsReverse = isReverse;
            return iotClientCommon;
        }

        /// <summary>
        /// 创建串口类型的设备连接
        /// </summary>
        /// <param name="deviceVersion">设备类型</param>
        /// <param name="portName">COM端口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">奇偶校验</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="format">大小端设置</param>
        /// <returns></returns>
        public static IModbusSerialPortClient CreateModbusSerialPortClient(SerialDeviceVersion deviceVersion, string portName, int baudRate, int dataBits, StopBits stopBits, Parity parity, int timeout = 1500, EndianFormat format = EndianFormat.ABCD, bool Isrreve = false)
        {
            IModbusSerialPortClient iotClientCommon;
            switch (deviceVersion)
            {
                case SerialDeviceVersion.ModbusRtu:
                    iotClientCommon = new ModbusRtuClient(portName, baudRate, dataBits, stopBits, parity);
                    break;
                case SerialDeviceVersion.ModBusAscii:
                    iotClientCommon = new ModbusAsciiClient(portName, baudRate, dataBits, stopBits, parity);
                    break;
                default:
                    throw new Exception($"类型[{deviceVersion}]暂未实现");
            }
            iotClientCommon.Timeout = TimeSpan.FromMilliseconds(timeout);
            iotClientCommon.DataFormat = format;
            //iotClientCommon.DataFormat = format;
            //iotClientCommon.IsReverse = isReverse;
            return iotClientCommon;
        }

    }
}

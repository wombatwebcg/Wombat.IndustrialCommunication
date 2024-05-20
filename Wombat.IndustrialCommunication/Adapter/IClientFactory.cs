﻿//using System;
//using System.Collections.Generic;
//using System.Text;
//using Wombat.Extensions.DataTypeExtensions;
//using Wombat.IndustrialCommunication.PLC;

//namespace Wombat.IndustrialCommunication.Adapter
//{
//   public class IClientFactory
//    {
//        /// <summary>
//        /// 创建以太网类型的设备连接
//        /// </summary>
//        /// <param name="deviceVersion">设备类型</param>
//        /// <param name="ip">ip地址</param>
//        /// <param name="port">端口号</param>
//        /// <param name="timeout">超时时间</param>
//        /// <param name="format">大小端设置</param>
//        /// <returns></returns>
//        public static IEthernetClient CreatePLCEthernetDevice(EthernetDeviceVersion deviceVersion, string ip, int port, int timeout = 1500, EndianFormat format = EndianFormat.ABCD)
//        {
//            IEthernetClient iotClientCommon;
//            switch (deviceVersion)
//            {
//                case EthernetDeviceVersion.ModbusTcp:
//                    iotClientCommon = new ModbusTcpCommunication("ModbusTcp", ip, port, timeout, format);
//                    break;
//                case EthernetDeviceVersion.ModbusRtuOverTcp:
//                    iotClientCommon = new ModbusTcpCommunication("ModbusRtuOverTcp", ip, port, timeout, format);
//                    break;
//                case EthernetDeviceVersion.Siemens_S7_200:
//                    iotClientCommon = new SiemensClientCommunication(SiemensVersion.S7_200, ip, port, timeout: timeout);
//                    break;
//                case EthernetDeviceVersion.Siemens_S7_200Smart:
//                    iotClientCommon = new SiemensClientCommunication(SiemensVersion.S7_200Smart, ip, port, timeout: timeout);
//                    break;
//                case EthernetDeviceVersion.Siemens_S7_300:
//                    iotClientCommon = new SiemensClientCommunication(SiemensVersion.S7_300, ip, port, timeout: timeout);
//                    break;
//                case EthernetDeviceVersion.Siemens_S7_400:
//                    iotClientCommon = new SiemensClientCommunication(SiemensVersion.S7_400, ip, port, timeout: timeout);
//                    break;
//                case EthernetDeviceVersion.Siemens_S7_1200:
//                    iotClientCommon = new SiemensClientCommunication(SiemensVersion.S7_1200, ip, port, timeout: timeout);
//                    break;
//                case EthernetDeviceVersion.Siemens_S7_1500:
//                    iotClientCommon = new SiemensClientCommunication(SiemensVersion.S7_1500, ip, port, timeout: timeout);
//                    break;
//                case EthernetDeviceVersion.OmronFins:
//                    iotClientCommon = new OmronFinsClientCommunication(ip, port, timeout);
//                    break;
//                case EthernetDeviceVersion.AllenBradley:
//                    iotClientCommon = new AllenBradleyClientCommunication(ip, port, timeout);
//                    break;
//                case EthernetDeviceVersion.Mitsubishi_A_1E:
//                    iotClientCommon = new MitsubishiClientCommunication(MitsubishiVersion.A_1E, ip, port, timeout);
//                    break;
//                case EthernetDeviceVersion.Mitsubishi_Qna_3E:
//                    iotClientCommon = new MitsubishiClientCommunication(MitsubishiVersion.Qna_3E, ip, port, timeout);
//                    break;
//                default:
//                    throw new Exception($"类型[{deviceVersion}]暂未实现");
//            }
//            return iotClientCommon;
//        }

//        /// <summary>
//        /// 创建串口类型的设备连接
//        /// </summary>
//        /// <param name="deviceVersion">设备类型</param>
//        /// <param name="portName">COM端口名称</param>
//        /// <param name="baudRate">波特率</param>
//        /// <param name="dataBits">数据位</param>
//        /// <param name="stopBits">停止位</param>
//        /// <param name="parity">奇偶校验</param>
//        /// <param name="timeout">超时时间（毫秒）</param>
//        /// <param name="format">大小端设置</param>
//        /// <returns></returns>
//        public static IIoTClientCommon CreateClient2SerialDevice(SerialDeviceVersion deviceVersion, string portName, int baudRate, int dataBits, StopBits stopBits, Parity parity, int timeout = 1500, EndianFormat format = EndianFormat.ABCD)
//        {
//            IIoTClientCommon iotClientCommon;
//            switch (deviceVersion)
//            {
//                case SerialDeviceVersion.ModbusRtu:
//                    iotClientCommon = new ModbusRtuCommunication("ModbusRtu", portName, baudRate, dataBits, stopBits, parity, timeout, format);
//                    break;
//                case SerialDeviceVersion.ModBusAscii:
//                    iotClientCommon = new ModbusRtuCommunication("ModbusAscii", portName, baudRate, dataBits, stopBits, parity, timeout, format);
//                    break;
//                default:
//                    throw new Exception($"类型[{deviceVersion}]暂未实现");
//            }
//            return iotClientCommon;
//        }

//    }
//}

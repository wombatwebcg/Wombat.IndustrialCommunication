using Microsoft.Extensions.Logging;
using System;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// ModbusRtu协议客户端
    /// </summary>
    public class ModbusRtuClient : ModbusSerialPortBase
    {


        public ModbusRtuClient() : base()
        {
            this.DataFormat = EndianFormat.ABCD;

        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="portName">COM端口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">奇偶校验</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="DataFormat">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        public ModbusRtuClient(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None)
            : base(portName,baudRate,dataBits,stopBits,parity,handshake)
        {
           this.DataFormat = EndianFormat.ABCD;

        }

        public override string Version => "ModbusRtuClient";
    }
}

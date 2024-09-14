using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 信捷PLC对应客户端(未测试)
    /// </summary>
    public class XinjieClient : PLCByModbusDtuBase
    {
        public XinjieClient(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None) : base(portName, baudRate, dataBits, stopBits, parity, handshake)
        {

        }

        //public override string TranPLCAddress(string address)
        //{
        //    string head = address.Substring(0, 1).ToLower();
        //    if (!ushort.TryParse(head, out ushort tempAddress))
        //    {
        //        throw new Exception("地址不正确") ;
        //    }

        //    switch (head)
        //    {
        //        case "m":
        //            if (tempAddress < 8000)
        //            {
        //                return tempAddress.ToString();
        //            }
        //            else
        //            {
        //                return (tempAddress+0x5000).ToString();

        //            }
        //        case "x":
        //            return (tempAddress + 0x4000).ToString();
        //        case "y":
        //            return (tempAddress + 0x4800).ToString();
        //        case "s":
        //            return (tempAddress + 0x5000).ToString();
        //        case "t":
        //            return (tempAddress + 0x6400).ToString();
        //        case "c":
        //            return (tempAddress + 0x6C00).ToString();
        //        case "d":
        //            if (tempAddress < 8000)
        //            {
        //                return tempAddress.ToString();
        //            }
        //            else
        //            {
        //                return (tempAddress + 0x4000).ToString();

        //            }
        //        default:
        //            throw new Exception("没有对应写的地址");


        //    }

        //}



    }
}

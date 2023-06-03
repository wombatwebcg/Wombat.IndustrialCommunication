using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Wombat.IndustrialCommunication.Modbus;


namespace Wombat.IndustrialCommunication.PLC
{
    public class InovanceClient : PLCByModbusTcpBase
    {

        public InovanceClient() : base()
        {
            DataFormat = EndianFormat.BADC;
        }

        public InovanceClient(IPEndPoint ipAndPoint) : base(ipAndPoint)
        {
            DataFormat = EndianFormat.BADC;
        }

        public InovanceClient(string ip, int port) : base(ip, port)
        {
            DataFormat = EndianFormat.BADC;

        }


        public override string TranPLCAddress(string address)
        {
            string head = address.Substring(0, 1);
            string  newAddress = string.Empty;
            if (!ushort.TryParse(address.Substring(1), out ushort tempAddress))
            {
                return newAddress;
            }
            head = head.ToLower();
            switch (head)
            {
                case "m":
                    if (tempAddress < 7680)
                    {
                        newAddress = tempAddress.ToString();
                    }
                    else
                    {
                        newAddress = (tempAddress + 0x1F40).ToString();

                    }
                    return newAddress;
                case "x":
                    newAddress = (tempAddress + 0xF800).ToString();
                    return newAddress;
                case "y":
                    newAddress = (tempAddress + 0xFC00).ToString();
                    return newAddress;
                case "s":
                    newAddress = (tempAddress + 0xE000).ToString();
                    return newAddress;
                case "d":
                    newAddress = tempAddress.ToString();
                    return newAddress;
                case "sd":
                    newAddress = (tempAddress + 0x2400).ToString();
                    return newAddress;
                case "r":
                    newAddress = (tempAddress + 0x3000).ToString();
                    return newAddress;
                case "t":
                    newAddress = (tempAddress + 0xF000).ToString();
                    return newAddress;
                case "c":
                    if (tempAddress < 200)
                    {
                        newAddress = (tempAddress + 0xF400).ToString();
                    }
                    else
                    {
                        newAddress = (tempAddress + 0xF700).ToString();

                    }
                    return newAddress;

            }

            return newAddress;
        }
        private static bool TranCoilAddress(string address, out string newAddress)
        {
            string head = address.Substring(0, 1);
            newAddress = string.Empty;
            if (!ushort.TryParse(address.Substring(1), out ushort tempAddress))
            {
                return false;
            }
            head = head.ToLower();
            switch (head)
            {
                case "m":
                    if (tempAddress < 7680)
                    {
                        newAddress = tempAddress.ToString();
                    }
                    else
                    {
                        newAddress = (tempAddress + 0x1F40).ToString();

                    }
                    return true;
                case "x":
                    newAddress =(tempAddress + 0xF800).ToString();
                    return true;
                case "y":
                    newAddress = (tempAddress + 0xFC00).ToString();
                    return true;
                case "s":
                    newAddress = (tempAddress + 0xE000).ToString();
                    return true;
                case "t":
                    newAddress = (tempAddress + 0xF000).ToString();
                    return true;
                case "c":
                    newAddress = (tempAddress + 0xF400).ToString();
                    return true;

            }

            return false;

        }


        private static bool TranRegisterAddress(string address, out string newAddress)
        {
            string head = address.Substring(0, 1);
            newAddress = string.Empty;
            if (!ushort.TryParse(address.Substring(1), out ushort tempAddress))
            {
                return false;
            }
            head = head.ToLower();
            switch (head)
            {
                case "m":
                    if (tempAddress < 7680)
                    {
                        newAddress = tempAddress.ToString();
                    }
                    else
                    {
                        newAddress = (tempAddress + 0x1F40).ToString();

                    }
                    return true;
                case "x":
                    newAddress = (tempAddress + 0xF800).ToString();
                    return true;
                case "y":
                    newAddress = (tempAddress + 0xFC00).ToString();
                    return true;
                case "s":
                    newAddress = (tempAddress + 0xE000).ToString();
                    return true;
                case "d":
                    newAddress = tempAddress.ToString();
                    return true;
                case "sd":
                    newAddress = (tempAddress + 0x2400).ToString();
                    return true;
                case "r":
                    newAddress = (tempAddress + 0x3000).ToString();
                    return true;
                case "t":
                    newAddress = (tempAddress + 0xF000).ToString();
                    return true;
                case "c":
                    if (tempAddress < 200)
                    {
                        newAddress = (tempAddress + 0xF400).ToString();
                    }
                    else
                    {
                        newAddress = (tempAddress + 0xF700).ToString();

                    }
                    return true;

            }

            return false;

        }


    }
}

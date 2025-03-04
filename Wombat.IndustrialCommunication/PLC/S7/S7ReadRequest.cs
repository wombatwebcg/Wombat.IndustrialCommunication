using System;
using System.Collections.Generic;
using System.Text;
using Wombat.IndustrialCommunication.Enums;

namespace Wombat.IndustrialCommunication.PLC
{
    public class S7ReadRequest :  IDeviceReadWriteMessage
    {
        public S7ReadRequest(string address, int length, bool isBit)
        {
            RegisterAddress = address;
            RegisterCount = length;
            var siemensAddress = ConvertSiemensAddress(address);
            siemensAddress.IsBit = isBit;
            siemensAddress.ReadWriteLength = length;
            ProtocolMessageFrame = GetReadCommand(siemensAddress);

        }

        public byte[] ProtocolMessageFrame { get; set; }

        public int ProtocolDataNumber { get; set; }

        public string RegisterAddress { get; set; }

        public int RegisterCount { get; set; }

        public int ProtocolResponseLength { get; set; } = SiemensConstant.InitHeadLength;


        public void Initialize(byte[] frame)
        {
            throw new NotImplementedException();
        }

        protected byte[] GetReadCommand(SiemensAddress[] datas)
        {
            //byte type, int beginAddress, ushort dbAddress, ushort length, bool isBit
            byte[] command = new byte[19 + datas.Length * 12];
            command[0] = 0x03;
            command[1] = 0x00;//[0][1]固定报文头
            command[2] = (byte)(command.Length / 256);
            command[3] = (byte)(command.Length % 256);//[2][3]整个读取请求长度为0x1F= 31 
            command[4] = 0x02; // 固定 -> Fixed
            command[5] = 0xF0; // 固定 -> Fixed
            command[6] = 0x80;//COTP
            command[7] = 0x32;//协议ID
            command[8] = 0x01;//1  客户端发送命令 3 服务器回复命令
            command[9] = 0x00;
            command[10] = 0x00;//[4]-[10]固定6个字节
            command[11] = 0x00;
            command[12] = 0x01;//[11][12]两个字节，标识序列号，回复报文相同位置和这个完全一样；范围是0~65535
            command[13] = (byte)((command.Length - 17) / 256);
            command[14] = (byte)((command.Length - 17) % 256); //parameter length（减17是因为从[17]到最后属于parameter）
            command[15] = 0x00;
            command[16] = 0x00;//data length
            command[17] = 0x04;//04读 05写
            command[18] = (byte)datas.Length;//读取数据块个数
            for (int i = 0; i < datas.Length; i++)
            {
                var data = datas[i];
                bool isBit = data.IsBit;
                int realLength = data.ReadWriteLength;
                if (data.IsBit)
                {
                    realLength = (int)(Math.Ceiling(realLength / 8.0));
                }
                if(data.IsBit&& data.ReadWriteLength>1)
                {
                    isBit = false;
                }
                command[19 + i * 12] = 0x12;//variable specification
                command[20 + i * 12] = 0x0A;//Length of following address specification
                command[21 + i * 12] = 0x10;//Syntax Id: S7ANY 
                command[22 + i * 12] = isBit ? (byte)0x01 : (byte)0x02;//Toport size: BYTE 
                command[23 + i * 12] = (byte)(realLength / 256);
                command[24 + i * 12] = (byte)(realLength % 256);//[23][24]两个字节,访问数据的个数，以byte为单位；
                command[25 + i * 12] = (byte)(data.DbBlock / 256);
                command[26 + i * 12] = (byte)(data.DbBlock % 256);//[25][26]DB块的编号
                command[27 + i * 12] = data.TypeCode;//访问数据块的类型
                command[28 + i * 12] = (byte)(data.BeginAddress / 256 / 256 % 256);
                command[29 + i * 12] = (byte)(data.BeginAddress / 256 % 256);
                command[30 + i * 12] = (byte)(data.BeginAddress % 256);//[28][29][30]访问DB块的偏移量
            }
            return command;
        }

        protected byte[] GetReadCommand(SiemensAddress data)
        {
            return GetReadCommand(new SiemensAddress[] { data });
        }


        private SiemensAddress ConvertSiemensAddress(string address, int offest = 0)
        {
            try
            {
                //转换成大写
                address = address.ToUpper();
                var addressInfo = new SiemensAddress()
                {
                    Address = address,
                    DbBlock = 0,
                };
                switch (address[0])
                {
                    case 'I':
                        addressInfo.TypeCode = 0x81;
                        break;
                    case 'Q':
                        addressInfo.TypeCode = 0x82;
                        break;
                    case 'M':
                        addressInfo.TypeCode = 0x83;
                        break;
                    case 'D':
                        addressInfo.TypeCode = 0x84;
                        string[] adds = address.Split('.');
                        if (address[1] == 'B')
                            addressInfo.DbBlock = Convert.ToUInt16(adds[0].Substring(2));
                        else
                            addressInfo.DbBlock = Convert.ToUInt16(adds[0].Substring(1));
                        //TODO 
                        //addressInfo.BeginAddress = GetBeingAddress(address.Substring(address.IndexOf('.') + 1));
                        break;
                    case 'T':
                        addressInfo.TypeCode = 0x1D;
                        break;
                    case 'C':
                        addressInfo.TypeCode = 0x1C;
                        break;
                    case 'V':
                        addressInfo.TypeCode = 0x84;
                        addressInfo.DbBlock = 1;
                        break;
                }

                //if (address[0] != 'D' && address[1] != 'B')
                //    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1));

                //DB块
                if (address[0] == 'D' && address[1] == 'B')
                {
                    //DB1.0.0、DB1.4（非PLC地址）
                    var indexOfpoint = address.IndexOf('.') + 1;
                    if (address[indexOfpoint] >= '0' && address[indexOfpoint] <= '9')
                        addressInfo.BeginAddress = S7CommonMethods.GetBeingAddress(address.Substring(indexOfpoint), offest);
                    //DB1.DBX0.0、DB1.DBD4（标准PLC地址）
                    else
                        addressInfo.BeginAddress = S7CommonMethods.GetBeingAddress(address.Substring(address.IndexOf('.') + 4), offest);
                }
                //非DB块
                else
                {
                    //I0.0、V1004的情况（非PLC地址）
                    if (address[1] >= '0' && address[1] <= '9')
                        addressInfo.BeginAddress = S7CommonMethods.GetBeingAddress(address.Substring(1), offest);
                    //VB1004的情况（标准PLC地址）
                    else
                        addressInfo.BeginAddress = S7CommonMethods.GetBeingAddress(address.Substring(2), offest);
                }
                return addressInfo;
            }
            catch (Exception ex)
            {
                throw new Exception($"地址[{address}]解析异常，ConvertArg Message:{ex.Message}");
            }
        }



    }
}

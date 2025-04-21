using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wombat.IndustrialCommunication.Enums;

namespace Wombat.IndustrialCommunication.PLC
{
    public class S7WriteRequest : IDeviceReadWriteMessage
    {
        public S7WriteRequest(string address,int offest, byte[] writeData, bool isBit)
        {
            RegisterAddress = address;
            var siemensWriteAddress= S7CommonMethods.ConvertWriteArg(address, offest, writeData, isBit);
            ProtocolMessageFrame = GetWriteCommand(siemensWriteAddress);

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


        /// <summary>
        /// 获取写指令
        /// </summary>
        /// <param name="write"></param>
        /// <returns></returns>
        protected byte[] GetWriteCommand(SiemensWriteAddress write)
        {
            return GetWriteCommand(new SiemensWriteAddress[] { write });
        }

        /// <summary>
        /// 获取写指令
        /// </summary>
        /// <param name="writes"></param>
        /// <returns></returns>
        protected byte[] GetWriteCommand(SiemensWriteAddress[] writes)
        {
            //（如果不是最后一个 WriteData.Length == 1 ，则需要填充一个空数据）
            var writeDataLength = writes.Sum(t => t.WriteData.Length == 1 ? 2 : t.WriteData.Length);
            if (writes[writes.Length - 1].WriteData.Length == 1) writeDataLength--;

            //前19个固定的、16为Item长度、writes.Length为Imte的个数
            byte[] command = new byte[19 + writes.Length * 16 + writeDataLength];

            command[0] = 0x03;
            command[1] = 0x00;//[0][1]固定报文头
            command[2] = (byte)((command.Length) / 256);
            command[3] = (byte)((command.Length) % 256);//[2][3]整个读取请求长度
            command[4] = 0x02; // 固定 -> Fixed
            command[5] = 0xF0; // 固定 -> Fixed
            command[6] = 0x80; // 固定 -> Fixed
            command[7] = 0x32;//protocol Id
            command[8] = 0x01;//1  客户端发送命令 3 服务器回复命令 Job
            command[9] = 0x00;
            command[10] = 0x00;//[9][10] redundancy identification (冗余的识别)
            command[11] = 0x00;
            command[12] = 0x01;//[11]-[12]protocol data unit reference
            command[13] = (byte)((12 * writes.Length + 2) / 256);
            command[14] = (byte)((12 * writes.Length + 2) % 256);//Parameter length
            command[15] = (byte)((writeDataLength + 4 * writes.Length) / 256);
            command[16] = (byte)((writeDataLength + 4 * writes.Length) % 256);//[15][16] Data length

            //Parameter
            command[17] = 0x05;//04读 05写 Function Write
            command[18] = (byte)writes.Length;//写入数据块个数 Item count
            //Item[]
            for (int i = 0; i < writes.Length; i++)
            {
                var write = writes[i];
                if (write.IsBit & (write.WriteData.Length > 1 | write.WriteData[0] >= 2))
                {
                    write.IsBit = false;
                }


                var typeCode = write.TypeCode;
                var beginAddress = write.BeginAddress;
                var dbBlock = write.DbBlock;
                var writeData = write.WriteData;

                command[19 + i * 12] = 0x12;
                command[20 + i * 12] = 0x0A;
                command[21 + i * 12] = 0x10;//[19]-[21]固定
                command[22 + i * 12] = write.IsBit ? (byte)0x01 : (byte)0x02;//写入方式，1是按位，2是按字
                command[23 + i * 12] = (byte)(writeData.Length / 256);
                command[24 + i * 12] = (byte)(writeData.Length % 256);//写入数据个数
                command[25 + i * 12] = (byte)(dbBlock / 256);
                command[26 + i * 12] = (byte)(dbBlock % 256);//DB块的编号
                command[27 + i * 12] = typeCode;
                command[28 + i * 12] = (byte)(beginAddress / 256 / 256 % 256); ;
                command[29 + i * 12] = (byte)(beginAddress / 256 % 256);
                command[30 + i * 12] = (byte)(beginAddress % 256);//[28][29][30]访问DB块的偏移量      

            }
            var index = 18 + writes.Length * 12;
            //Data
            for (int i = 0; i < writes.Length; i++)
            {
                var write = writes[i];
                bool isBit = write.IsBit;
                if (write.WriteData.Length > 1 || write.WriteData[0] >= 2)
                {
                    isBit = false;
                }
                var writeData = write.WriteData;
                var coefficient = isBit ? 1 : 8;

                command[1 + index] = 0x00;
                command[2 + index] = isBit ? (byte)0x03 : (byte)0x04;// 03bit（位）04 byte(字节)
                command[3 + index] = (byte)(writeData.Length * coefficient / 256);
                command[4 + index] = (byte)(writeData.Length * coefficient % 256);//按位计算出的长度

                if (write.WriteData.Length == 1)
                {
                    if (isBit)
                        command[5 + index] = writeData[0] == 0x01 ? (byte)0x01 : (byte)0x00; //True or False 
                    else command[5 + index] = writeData[0];

                    if (i >= writes.Length - 1)
                        index += (4 + 1);
                    else index += (4 + 2); // fill byte  （如果不是最后一个bit，则需要填充一个空数据）
                }
                else
                {
                    writeData.CopyTo(command, 5 + index);
                    index += (4 + writeData.Length);
                }
            }
            return command;
        }

    }
}

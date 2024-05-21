using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Wombat.IndustrialCommunication.Modbus;


namespace Wombat.IndustrialCommunication.PLC
{
    public  abstract class PLCByModbusTcpBase : ModbusTcpClient
    {
        protected PLCByModbusTcpBase() : base()
        {
        }



        protected PLCByModbusTcpBase(IPEndPoint ipAndPoint) : base(ipAndPoint)
        {
        }

        protected PLCByModbusTcpBase(string ip, int port) : base(ip, port)
        {

        }
        #region  Read 读取


        //public new  OperationResult<byte[]> Read(string address, int readLength = 1, byte stationNumber = 1, byte functionCode = 3)
        //{
        //    return base.Read(address, readLength, stationNumber, functionCode);
        //}


        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<short> ReadInt16(string address, byte stationNumber = 1, byte functionCode = 3)
          => base.ReadInt16(address, stationNumber, functionCode);

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<short[]> ReadInt16(string address, int readLength, byte stationNumber = 1, byte functionCode = 3)
          => base.ReadInt16(address, readLength, stationNumber, functionCode);


        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>

        public new OperationResult<ushort> ReadUInt16(string address, byte stationNumber = 1, byte functionCode = 3)
          => base.ReadUInt16(address,stationNumber, functionCode);






        public new OperationResult<ushort[]> ReadUInt16(string address, int readLength, byte stationNumber = 1, byte functionCode = 3)
          => base.ReadUInt16(address, readLength, stationNumber, functionCode);

        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        public new OperationResult<ushort> ReadUInt16Bit(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true)
            => base.ReadUInt16Bit(address,stationNumber,functionCode,left);



        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        public new OperationResult<short> ReadInt16Bit(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true)
          => base.ReadInt16Bit(address, stationNumber, functionCode, left);


        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<int> ReadInt32(string address, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadInt32(address, stationNumber, functionCode);

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<int[]> ReadInt32(string address, int readLength, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadInt32(address, readLength, stationNumber, functionCode);

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<uint> ReadUInt32(string address, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadUInt32(address, stationNumber, functionCode);

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<uint[]> ReadUInt32(string address, int readLength, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadUInt32(address, readLength, stationNumber, functionCode);

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<long> ReadInt64(string address, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadInt64(address, stationNumber, functionCode);

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<long[]> ReadInt64(string address, int readLength, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadInt64(address,readLength ,stationNumber, functionCode);


        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<ulong> ReadUInt64(string address, byte stationNumber = 1, byte functionCode = 3)
            =>base.ReadUInt64(address, stationNumber, functionCode);

        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<ulong[]> ReadUInt64(string address, int readLength, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadUInt64(address,readLength, stationNumber, functionCode);

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<float> ReadFloat(string address, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadFloat(address, stationNumber, functionCode);

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<float[]> ReadFloat(string address, int readLength, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadFloat(address,readLength,stationNumber, functionCode);


        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<double> ReadDouble(string address, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadDouble(address, stationNumber, functionCode);

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<double[]> ReadDouble(string address, int readLength, byte stationNumber = 1, byte functionCode = 3)
            => base.ReadDouble(address,readLength ,stationNumber, functionCode);




        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<bool> ReadCoil(string address, byte stationNumber = 1, byte functionCode = 1)
            => base.ReadCoil(address, stationNumber, functionCode);

        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<bool[]> ReadCoil(string address, int readLength, byte stationNumber = 1, byte functionCode = 1)
            => base.ReadCoil(address,readLength ,stationNumber, functionCode);



        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public new OperationResult<bool> ReadDiscrete(string address, byte stationNumber = 1, byte functionCode = 2)
            => base.ReadDiscrete(address, stationNumber, functionCode);

        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public new OperationResult<bool[]> ReadDiscrete(string address, int readLength, byte stationNumber = 1, byte functionCode = 2)
            => base.ReadDiscrete(address,readLength ,stationNumber, functionCode);



        #endregion

        #region Write 写入

        public override OperationResult Write(string address, bool value, byte stationNumber = 1, byte functionCode = 5)
        {
            return base.Write(address, value, stationNumber, functionCode);   
        }

        public override OperationResult Write(string address, bool[] value, byte stationNumber = 1, byte functionCode = 15)
        {
            return base.Write(address, value, stationNumber, functionCode);
        }

        public override OperationResult Write(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16)
        {
            return base.Write(address, values, stationNumber, functionCode);  
        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, short value, byte stationNumber = 1, byte functionCode = 6)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, short[] value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ushort value, byte stationNumber = 1, byte functionCode = 6)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ushort[] value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, int value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, int[] value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, uint value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, uint[] value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, long value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, long[] value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ulong value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ulong[] value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, float value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, float[] value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, double value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, double[] value, byte stationNumber = 1, byte functionCode = 16)
            => base.Write(address, value, stationNumber, functionCode);


        #endregion


    }
}

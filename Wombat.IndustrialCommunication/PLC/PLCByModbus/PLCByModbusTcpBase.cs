using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Wombat.IndustrialCommunication.Modbus;
using Wombat.IndustrialCommunication.Models;
using Wombat.Infrastructure;

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


        //public new  OperationResult<byte[]> Read(string address, int readLength = 1, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
        //{
        //    return base.Read(address, readLength, stationNumber, functionCode, isPlcAddress);
        //}


        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<short> ReadInt16(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
          => base.ReadInt16(address, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<short[]> ReadInt16(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
          => base.ReadInt16(address, readLength, stationNumber, functionCode, isPlcAddress);


        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>

        public new OperationResult<ushort> ReadUInt16(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
          => base.ReadUInt16(address,stationNumber, functionCode, isPlcAddress);






        public new OperationResult<ushort[]> ReadUInt16(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
          => base.ReadUInt16(address, readLength, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        public new OperationResult<ushort> ReadUInt16Bit(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true, bool isPlcAddress = true)
            => base.ReadUInt16Bit(address,stationNumber,functionCode,left,isPlcAddress);



        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        public new OperationResult<short> ReadInt16Bit(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true, bool isPlcAddress = true)
          => base.ReadInt16Bit(address, stationNumber, functionCode, left, isPlcAddress);


        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<int> ReadInt32(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadInt32(address, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<int[]> ReadInt32(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadInt32(address, readLength, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<uint> ReadUInt32(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadUInt32(address, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<uint[]> ReadUInt32(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadUInt32(address, readLength, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<long> ReadInt64(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadInt64(address, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<long[]> ReadInt64(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadInt64(address,readLength ,stationNumber, functionCode, isPlcAddress);


        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<ulong> ReadUInt64(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            =>base.ReadUInt64(address, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<ulong[]> ReadUInt64(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadUInt64(address,readLength, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<float> ReadFloat(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadFloat(address, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<float[]> ReadFloat(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadFloat(address,readLength,stationNumber, functionCode, isPlcAddress);


        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<double> ReadDouble(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadDouble(address, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<double[]> ReadDouble(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = true)
            => base.ReadDouble(address,readLength ,stationNumber, functionCode, isPlcAddress);




        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<bool> ReadCoil(string address, byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = true)
            => base.ReadCoil(address, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<bool[]> ReadCoil(string address, int readLength, byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = true)
            => base.ReadCoil(address,readLength ,stationNumber, functionCode, isPlcAddress);



        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public new OperationResult<bool> ReadDiscrete(string address, byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = true)
            => base.ReadDiscrete(address, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public new OperationResult<bool[]> ReadDiscrete(string address, int readLength, byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = true)
            => base.ReadDiscrete(address,readLength ,stationNumber, functionCode, isPlcAddress);



        #endregion

        #region Write 写入

        public override OperationResult Write(string address, bool value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = true)
        {
            return base.Write(address, value, stationNumber, functionCode, isPlcAddress);   
        }

        public override OperationResult Write(string address, bool[] value, byte stationNumber = 1, byte functionCode = 15, bool isPlcAddress = true)
        {
            return base.Write(address, value, stationNumber, functionCode, isPlcAddress);
        }

        public override OperationResult Write(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
        {
            return base.Write(address, values, stationNumber, functionCode, isPlcAddress);  
        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, short value, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, short[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ushort value, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ushort[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, int value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, int[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, uint value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, uint[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, long value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, long[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ulong value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ulong[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, float value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, float[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, double value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, double[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = true)
            => base.Write(address, value, stationNumber, functionCode, isPlcAddress);


        #endregion


    }
}

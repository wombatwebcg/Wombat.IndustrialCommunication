﻿using System.IO.Ports;
using Wombat.IndustrialCommunication.Modbus;

namespace Wombat.IndustrialCommunication.PLC
{
    public abstract  class PLCByModbusDtuBase: ModbusRtuClient
    {
        protected PLCByModbusDtuBase(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None) : base(portName, baudRate, dataBits, stopBits, parity, handshake)
        {
        }

        #region  Read 读取


        //public new  OperationResult<byte[]> Read(string address, int readLength = 1, byte stationNumber = 1, byte functionCode = 3)
        //{
        //    return base.Read(address, readLength);
        //}


        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<short> ReadInt16(string address)
        {
            return this.ReadInt16(address);
        }

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<short[]> ReadInt16(string address, int readLength)
        {
            return this.ReadInt16(address, readLength);

        }
        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <returns></returns>

        public new OperationResult<ushort> ReadUInt16(string address)
        {
            return this.ReadUInt16(address);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="readLength"></param>
        /// <returns></returns>
        public new OperationResult<ushort[]> ReadUInt16(string address, int readLength)
        {
            return this.ReadUInt16(address, readLength);
        }

        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        public new OperationResult<ushort> ReadUInt16Bit(string address, bool left = true)
        {
            return  ReadUInt16Bit(address, left);

        }

        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        public new OperationResult<short> ReadInt16Bit(string address, bool left = true)
        {
            return ReadInt16Bit(address, left);

        }


        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<int> ReadInt32(string address)
        {
            return this.ReadInt32(address);
        }

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<int[]> ReadInt32(string address, int readLength)
        {
            return this.ReadInt32(address, readLength);
        }

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<uint> ReadUInt32(string address)
        {
            return this.ReadUInt32(address);
        }


        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <returns></returns>
        public new OperationResult<uint[]> ReadUInt32(string address, int readLength)
        {
            return this.ReadUInt32(address, readLength);
        }

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<long> ReadInt64(string address)
        {
            return this.ReadInt64(address);

        }

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<long[]> ReadInt64(string address, int readLength)
        {
            return this.ReadInt64(address, readLength);

        }


        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<ulong> ReadUInt64(string address)
        {
            return this.ReadUInt64(address);
        }

        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<ulong[]> ReadUInt64(string address, int readLength)
        {
            return this.ReadUInt64(address, readLength);
        }

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<float> ReadFloat(string address)
        {
            return this.ReadFloat(address);
        }

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<float[]> ReadFloat(string address, int readLength)
        {
            return this.ReadFloat(address, readLength);
        }


        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<double> ReadDouble(string address)
        {
            return this.ReadDouble(address); ;
        }


        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<double[]> ReadDouble(string address, int readLength)
        {
            return this.ReadDouble(address, readLength);
        }




        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<bool> ReadBoolean(string address)
        {
            return this.ReadBoolean(address);
        }

        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public new OperationResult<bool[]> ReadBoolean(string address, int readLength)
        {
            return this.ReadBoolean(address, readLength);
        }





        #endregion

        #region Write 写入

        public override OperationResult Write(string address, bool value)
        {
            return this.Write(address, value);
        }

        public override OperationResult Write(string address, bool[] value)
        {
            return this.Write(address, value);
        }

        public override OperationResult Write(string address, byte[] values)
        {
            return this.Write(address, values);
        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, short value)
        {
            return this.Write(address, value);
        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, short[] value)
        {
            return this.Write(address, value);
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ushort value)
        {
            return this.Write(address, value);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ushort[] value)
        {
            return this.Write(address, value);
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, int value)
        {
            return this.Write(address, value);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, int[] value)
        {
            return this.Write(address, value);
        }



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, uint value)
        {
            return this.Write(address, value);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, uint[] value)
        {
            return this.Write(address, value);
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, long value)
        {
            return this.Write(address, value);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, long[] value)
        {
            return this.Write(address, value);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ulong value)
        {
            return this.Write(address, value);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, ulong[] value)
        {
            return this.Write(address, value);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, float value)
        {
            return this.Write(address, value);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, float[] value)
        {
            return this.Write(address, value);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, double value)
        {
            return this.Write(address, value);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public new OperationResult Write(string address, double[] value)
        {
            return this.Write(address, value);
        }


        #endregion

    }
}

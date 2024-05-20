using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// 
    /// </summary>
    public interface IModbusReadWrite
    {

        #region  Read 读取
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="readLength">读取长度</param>
        /// <param name="setEndian">设置构造函数中的大小端</param>
        /// <returns></returns>
        OperationResult<byte[]> Read(string address,int readLength, byte stationNumber, byte functionCode, bool isPlcAddress = false);

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<short> ReadInt16(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<short[]> ReadInt16(string address,int length ,byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        OperationResult<short> ReadInt16Bit(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true, bool isPlcAddress = false);

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        OperationResult<short> ReadInt16(string beginAddress, string address, byte[] values);

        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<ushort[]> ReadUInt16(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<ushort> ReadUInt16(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        OperationResult<ushort> ReadUInt16(string beginAddress, string address, byte[] values);

        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        OperationResult<ushort> ReadUInt16Bit(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true, bool isPlcAddress = false);

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<int> ReadInt32(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<int[]> ReadInt32(string address,int length ,byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        OperationResult<int> ReadInt32(string beginAddress, string address, byte[] values);

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<uint> ReadUInt32(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<uint[]> ReadUInt32(string address,int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);



        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        OperationResult<uint> ReadUInt32(string beginAddress, string address, byte[] values);

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<long> ReadInt64(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<long[]> ReadInt64(string address,int length ,byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        OperationResult<long> ReadInt64(string beginAddress, string address, byte[] values);

        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<ulong> ReadUInt64(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<ulong[]> ReadUInt64(string address,int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        OperationResult<ulong> ReadUInt64(string beginAddress, string address, byte[] values);

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<float> ReadFloat(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<float[]> ReadFloat(string address,int length ,byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        OperationResult<float> ReadFloat(string beginAddress, string address, byte[] values);

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<double> ReadDouble(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<double[]> ReadDouble(string address,int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        OperationResult<double> ReadDouble(string beginAddress, string address, byte[] values);

        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<bool> ReadCoil(string address, byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = false);

        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        OperationResult<bool[]> ReadCoil(string address,int length ,byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = false);


        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        OperationResult<bool> ReadCoil(string beginAddress, string address, byte[] values);

        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        OperationResult<bool> ReadDiscrete(string address, byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = false);

        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        OperationResult<bool[]> ReadDiscrete(string address,int length ,byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = false);



        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        OperationResult<bool> ReadDiscrete(string beginAddress, string address, byte[] values);

        /// <summary>
        /// 分批读取（批量读取，内部进行批量计算读取）
        /// </summary>
        /// <param name="addresses"></param>
        /// <param name="retryCount">如果读取异常，重试次数</param>
        /// <returns></returns>
        OperationResult<List<ModbusOutput>> BatchRead(List<ModbusInput> addresses, uint retryCount = 1);
        #endregion

        #region  ReadAsync 读取
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="readLength">读取长度</param>
        /// <param name="setEndian">设置构造函数中的大小端</param>
        /// <returns></returns>
        Task<OperationResult<byte[]>> ReadAsync(string address, int readLength, byte stationNumber, byte functionCode, bool isPlcAddress = false);

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<short>> ReadInt16Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<short[]>> ReadInt16Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        Task<OperationResult<short>> ReadInt16BitAsync(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true, bool isPlcAddress = false);


        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<ushort[]>> ReadUInt16Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<ushort>> ReadUInt16Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);



        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        Task<OperationResult<ushort>> ReadUInt16BitAsync(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true, bool isPlcAddress = false);

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<int>> ReadInt32Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<int[]>> ReadInt32Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<uint>> ReadUInt32Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<uint[]>> ReadUInt32Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);



        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<long>> ReadInt64Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<long[]>> ReadInt64Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<ulong>> ReadUInt64Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<ulong[]>> ReadUInt64Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);



        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<float>> ReadFloatAsync(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);


        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<float[]>> ReadFloatAsync(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);



        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<double>> ReadDoubleAsync(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<double[]>> ReadDoubleAsync(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);



        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<bool>> ReadCoilAsync(string address, byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = false);

        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        Task<OperationResult<bool[]>> ReadCoilAsync(string address, int length, byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = false);


        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        Task<OperationResult<bool>> ReadDiscreteAsync(string address, byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = false);

        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        Task<OperationResult<bool[]>> ReadDiscreteAsync(string address, int length, byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = false);




        /// <summary>
        /// 分批读取（批量读取，内部进行批量计算读取）
        /// </summary>
        /// <param name="addresses"></param>
        /// <param name="retryCount">如果读取异常，重试次数</param>
        /// <returns></returns>
        Task<OperationResult<List<ModbusOutput>>> BatchReadAsync(List<ModbusInput> addresses, uint retryCount = 1);
        #endregion

        #region Write 写入
        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        OperationResult Write(string address, bool value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = false);

        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        OperationResult Write(string address, bool[] value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        OperationResult Write(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        //OperationResult Write(string address, byte value, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false);



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, short value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, short[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, ushort value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, ushort[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, int value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, int[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, uint value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, uint[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, long value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, long[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, ulong value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, ulong[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, float value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, float[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, double value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        OperationResult Write(string address, double[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        #endregion

        #region WriteAsync 写入
        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        Task<OperationResult> WriteAsync(string address, bool value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = false);

        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        Task<OperationResult> WriteAsync(string address, bool[] value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        //Task<OperationResult> WriteAsync(string address, byte value, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false);



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, short value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, short[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, ushort value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, ushort[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, int value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, int[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, uint value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, uint[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, long value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, long[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, ulong value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, ulong[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, float value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, float[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, double value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        Task<OperationResult> WriteAsync(string address, double[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        #endregion

    }
}

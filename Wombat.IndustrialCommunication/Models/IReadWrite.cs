using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;

namespace Wombat.IndustrialCommunication
{
    public interface IReadWrite
    {
        #region Read 

        /// <summary>
        /// 分批读取
        /// </summary>
        /// <param name="addresses">地址集合</param>
        /// <param name="batchNumber">批量读取数量</param>
        /// <returns></returns>
        OperationResult<Dictionary<string,(DataTypeEnums,object)>> BatchRead(Dictionary<string, DataTypeEnums> addresses);



        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<byte> ReadByte(string address);

        /// <summary>
        /// 读取Byte
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperationResult<byte[]> ReadByte(string address, int length);


        /// <summary>
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<bool> ReadBoolean(string address);

        /// <summary>
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<bool[]> ReadBoolean(string address, int length);


        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<ushort> ReadUInt16(string address);

        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<ushort[]> ReadUInt16(string address, int length);

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<short> ReadInt16(string address);



        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<short[]> ReadInt16(string address, int length);


        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<uint> ReadUInt32(string address);

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<uint[]> ReadUInt32(string address, int length);


        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<int> ReadInt32(string address);


        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<int[]> ReadInt32(string address, int length);


        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<ulong> ReadUInt64(string address);


        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<ulong[]> ReadUInt64(string address, int length);


        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<long> ReadInt64(string address);


        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<long[]> ReadInt64(string address, int length);


        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<float> ReadFloat(string address);



        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<float[]> ReadFloat(string address, int length);


        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<double> ReadDouble(string address);

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<double[]> ReadDouble(string address, int length);


        /// <summary>
        /// 读取String
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        OperationResult<string> ReadString(string address, int length);


        OperationResult<object>Read(DataTypeEnums dataTypeEnum,string address);


        OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address,int length);


        #endregion

        #region ReadAsync 


        /// <summary>
        /// 异步分批读取
        /// </summary>
        /// <param name="addresses">地址集合</param>
        /// <param name="batchNumber">批量读取数量</param>
        /// <returns></returns>
        /// 
        ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses);




        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<byte>> ReadByteAsync(string address);

        /// <summary>
        /// 读取Byte
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        ValueTask<OperationResult<byte[]>> ReadByteAsync(string address, int length);


        /// <summary>
        /// 异步读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<bool>> ReadBooleanAsync(string address);

        /// <summary>
        /// 异步读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<bool[]>> ReadBooleanAsync(string address, int length);


        /// <summary>
        /// 异步读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<ushort>> ReadUInt16Async(string address);

        /// <summary>
        /// 异步读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<ushort[]>> ReadUInt16Async(string address, int length);

        /// <summary>
        /// 异步读取Int16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<short>> ReadInt16Async(string address);


        /// <summary>
        /// 异步读取Int16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<short[]>> ReadInt16Async(string address, int length);


        /// <summary>
        /// 异步读取UInt32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<uint>> ReadUInt32Async(string address);

        /// <summary>
        /// 异步读取UInt32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<uint[]>> ReadUInt32Async(string address, int length);


        /// <summary>
        /// 异步读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<int>> ReadInt32Async(string address);


        /// <summary>
        /// 异步读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<int[]>> ReadInt32Async(string address, int length);


        /// <summary>
        /// 异步读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<ulong>> ReadUInt64Async(string address);


        /// <summary>
        /// 异步读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<ulong[]>> ReadUInt64Async(string address, int length);


        /// <summary>
        /// 异步读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<long>> ReadInt64Async(string address);


        /// <summary>
        /// 异步读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<long[]>> ReadInt64Async(string address, int length);


        /// <summary>
        /// 异步读取Float
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<float>> ReadFloatAsync(string address);


        /// <summary>
        /// 异步读取Float
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<float[]>> ReadFloatAsync(string address, int length);


        /// <summary>
        /// 异步读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<double>> ReadDoubleAsync(string address);

        /// <summary>
        /// 异步读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<double[]>> ReadDoubleAsync(string address, int length);


        /// <summary>
        /// 异步读取String
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        ValueTask<OperationResult<string>> ReadStringAsync(string address, int length);



        ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address);

        ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address, int length);

        #endregion

        #region Write
        /// <summary>
        /// 分批写入 
        /// </summary>
        /// <param name="addresses">地址集合</param>
        /// <param name="batchNumber">批量读取数量</param>
        /// <returns></returns>

        OperationResult BatchWrite(Dictionary<string, (DataTypeEnums,object)> addresses);



        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, byte[] value);

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, bool value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, bool[] value);




        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, byte value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, ushort value);



        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, ushort[] value);

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, short value);

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, short[] value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, uint value);

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, uint[] value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, int value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, int[] value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, ulong value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, ulong[] value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, long value);

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, long[] value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, float value);



        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, float[] value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, double value);

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, double[] value);


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, string value);

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="type">数据类型</param>
        /// <returns></returns>
        OperationResult Write(DataTypeEnums dataTypeEnum, string address, object value);


        OperationResult Write(DataTypeEnums dataTypeEnum, string address, object[] value);


        #endregion

        #region WriteAsync


        /// <summary>
        /// 异步分批写入 
        /// </summary>
        /// <param name="addresses">地址集合</param>
        /// <param name="batchNumber">批量读取数量</param>
        /// <returns></returns>
        ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses);


        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="isBit">是否位</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, byte[] value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, bool value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, bool[] value);




        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, byte value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, ushort value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, ushort[] value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, short value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, short[] value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, uint value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, uint[] value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, int value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, int[] value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, ulong value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, ulong[] value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, long value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, long[] value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, float value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, float[] value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, double value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, double[] value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, string value);

        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="type">数据类型</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object value);


        Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object[] value);



        #endregion


    }
}

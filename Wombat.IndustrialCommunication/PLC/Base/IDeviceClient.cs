using System.Collections.Generic;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication;
using Wombat.ObjectConversionExtention;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// IIoTClient 接口
    /// </summary>
    public interface IDeviceClient: IBaseModel
    {
        /// <summary>
        /// 版本
        /// </summary>
        string Version { get; }



        /// <summary>
        /// 发送报文，并获取响应报文
        /// </summary>
        /// <param name="command">发送命令</param>
        /// <returns></returns>
         OperationResult<byte[]> SendPackageSingle(byte[] command);

        #region Read 

        /// <summary>
        /// 分批读取
        /// </summary>
        /// <param name="addresses">地址集合</param>
        /// <param name="batchNumber">批量读取数量</param>
        /// <returns></returns>
        OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnum> addresses);




        /// <summary>
        /// 读取Byte
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        OperationResult<byte[]> Read(string address, int length, bool isBit = false);


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
        OperationResult<bool[]> ReadBoolean(string address,int length);


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
        OperationResult<short[]> ReadInt16(string address,int length);


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
        OperationResult<uint[]> ReadUInt32(string address,int length);


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
        OperationResult<ulong[]> ReadUInt64(string address,int length);


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
        OperationResult<long[]> ReadInt64(string address,int length);


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

        #endregion

        #region ReadAsync 


        /// <summary>
        /// 异步分批读取
        /// </summary>
        /// <param name="addresses">地址集合</param>
        /// <param name="batchNumber">批量读取数量</param>
        /// <returns></returns>
        Task<OperationResult<Dictionary<string, object>>> BatchReadAsync(Dictionary<string, DataTypeEnum> addresses);


        /// <summary>
        /// 异步读取Byte
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false);


        /// <summary>
        /// 异步读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<bool>> ReadBooleanAsync(string address);

        /// <summary>
        /// 异步读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<bool[]>> ReadBooleanAsync(string address, int length);


        /// <summary>
        /// 异步读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<ushort>> ReadUInt16Async(string address);

        /// <summary>
        /// 异步读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<ushort[]>> ReadUInt16Async(string address, int length);

        /// <summary>
        /// 异步读取Int16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<short>> ReadInt16Async(string address);


        /// <summary>
        /// 异步读取Int16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<short[]>> ReadInt16Async(string address, int length);


        /// <summary>
        /// 异步读取UInt32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<uint>> ReadUInt32Async(string address);

        /// <summary>
        /// 异步读取UInt32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<uint[]>> ReadUInt32Async(string address, int length);


        /// <summary>
        /// 异步读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<int>> ReadInt32Async(string address);


        /// <summary>
        /// 异步读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<int[]>> ReadInt32Async(string address, int length);


        /// <summary>
        /// 异步读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<ulong>> ReadUInt64Async(string address);


        /// <summary>
        /// 异步读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<ulong[]>> ReadUInt64Async(string address, int length);


        /// <summary>
        /// 异步读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<long>> ReadInt64Async(string address);


        /// <summary>
        /// 异步读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<long[]>> ReadInt64Async(string address, int length);


        /// <summary>
        /// 异步读取Float
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<float>> ReadFloatAsync(string address);


        /// <summary>
        /// 异步读取Float
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<float[]>> ReadFloatAsync(string address, int length);


        /// <summary>
        /// 异步读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<double>> ReadDoubleAsync(string address);

        /// <summary>
        /// 异步读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<double[]>> ReadDoubleAsync(string address, int length);


        /// <summary>
        /// 异步读取String
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        Task<OperationResult<string>> ReadStringAsync(string address, int length);

        #endregion

        #region Write
        /// <summary>
        /// 分批写入 
        /// </summary>
        /// <param name="addresses">地址集合</param>
        /// <param name="batchNumber">批量读取数量</param>
        /// <returns></returns>

        OperationResult BatchWrite(Dictionary<string, object> addresses);



        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        OperationResult Write(string address, byte[] value,bool isBit);

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
        OperationResult Write(string address, sbyte value);

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
        OperationResult Write(string address, object value, DataTypeEnum type);


        #endregion

        #region WriteAsync


        /// <summary>
        /// 异步分批写入 
        /// </summary>
        /// <param name="addresses">地址集合</param>
        /// <param name="batchNumber">批量读取数量</param>
        /// <returns></returns>
        Task<OperationResult> BatchWriteAsync(Dictionary<string, object> addresses);


        /// <summary>
        /// 异步写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="isBit">是否位</param>
        /// <returns></returns>
        Task<OperationResult> WriteAsync(string address, byte[] value, bool isBit);

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
        Task<OperationResult> WriteAsync(string address, sbyte value);

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
        Task<OperationResult> WriteAsync(string address, object value, DataTypeEnum type);

        #endregion


    }
}

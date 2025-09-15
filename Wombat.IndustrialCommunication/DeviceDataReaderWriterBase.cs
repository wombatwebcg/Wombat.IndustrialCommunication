using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// 设备数据读写基类
    /// </summary>
    public abstract class DeviceDataReaderWriterBase : IDisposable, IReadWrite
    {
        private DeviceMessageTransport _transport;

        internal DeviceDataReaderWriterBase(DeviceMessageTransport transport)
        {
            _transport = transport;
        }

        /// <summary>
        /// 数据格式
        /// </summary>
        public EndianFormat DataFormat { get; set; } = EndianFormat.ABCD;

        /// <summary>
        /// 是否反转
        /// </summary>
        public bool IsReverse { get; set; } = false;

        /// <summary>
        /// 传输对象
        /// </summary>
        public DeviceMessageTransport Transport => _transport;

        /// <summary>
        /// 版本信息
        /// </summary>
        public abstract string Version { get; }

        #region Read

        /// <summary>
        /// 批量读取数据
        /// </summary>
        public virtual OperationResult<Dictionary<string, (DataTypeEnums, object)>> BatchRead(Dictionary<string, DataTypeEnums> addresses)
        {
            return Task.Run(async () => await BatchReadAsync(addresses)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        internal virtual OperationResult<byte[]> Read(string address, int length, DataTypeEnums dataType, bool isBit = false)
        {
            return Task.Run(async () => await ReadAsync(address, length, dataType, isBit)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 读取单个字节
        /// </summary>
        public OperationResult<byte> ReadByte(string address)
        {
            return Task.Run(async () => await ReadByteAsync(address)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 读取多个字节
        /// </summary>
        public OperationResult<byte[]> ReadByte(string address, int length)
        {
            return Task.Run(async () => await ReadByteAsync(address, length)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 读取布尔值
        /// </summary>
        public virtual OperationResult<bool> ReadBoolean(string address)
        {
            return Task.Run(async () => await ReadBooleanAsync(address)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 读取布尔数组
        /// </summary>
        public virtual OperationResult<bool[]> ReadBoolean(string address, int length)
        {
            return Task.Run(async () => await ReadBooleanAsync(address, length)).GetAwaiter().GetResult();
        }

        public OperationResult<short> ReadInt16(string address)
        {
            return Task.Run(async () => await ReadInt16Async(address)).GetAwaiter().GetResult();
        }

        public OperationResult<short[]> ReadInt16(string address, int length)
        {
            return Task.Run(async () => await ReadInt16Async(address, length)).GetAwaiter().GetResult();
        }

        public OperationResult<ushort> ReadUInt16(string address)
        {
            return Task.Run(async () => await ReadUInt16Async(address)).GetAwaiter().GetResult();
        }

        public OperationResult<ushort[]> ReadUInt16(string address, int length)
        {
            return Task.Run(async () => await ReadUInt16Async(address, length)).GetAwaiter().GetResult();
        }

        public OperationResult<int> ReadInt32(string address)
        {
            return Task.Run(async () => await ReadInt32Async(address)).GetAwaiter().GetResult();
        }

        public OperationResult<int[]> ReadInt32(string address, int length)
        {
            return Task.Run(async () => await ReadInt32Async(address, length)).GetAwaiter().GetResult();
        }

        public OperationResult<uint> ReadUInt32(string address)
        {
            return Task.Run(async () => await ReadUInt32Async(address)).GetAwaiter().GetResult();
        }

        public OperationResult<uint[]> ReadUInt32(string address, int length)
        {
            return Task.Run(async () => await ReadUInt32Async(address, length)).GetAwaiter().GetResult();
        }

        public OperationResult<long> ReadInt64(string address)
        {
            return Task.Run(async () => await ReadInt64Async(address)).GetAwaiter().GetResult();
        }

        public OperationResult<long[]> ReadInt64(string address, int length)
        {
            return Task.Run(async () => await ReadInt64Async(address, length)).GetAwaiter().GetResult();
        }

        public OperationResult<ulong> ReadUInt64(string address)
        {
            return Task.Run(async () => await ReadUInt64Async(address)).GetAwaiter().GetResult();
        }

        public OperationResult<ulong[]> ReadUInt64(string address, int length)
        {
            return Task.Run(async () => await ReadUInt64Async(address, length)).GetAwaiter().GetResult();
        }

        public OperationResult<float> ReadFloat(string address)
        {
            return Task.Run(async () => await ReadFloatAsync(address)).GetAwaiter().GetResult();
        }

        public OperationResult<float[]> ReadFloat(string address, int length)
        {
            return Task.Run(async () => await ReadFloatAsync(address, length)).GetAwaiter().GetResult();
        }

        public OperationResult<double> ReadDouble(string address)
        {
            return Task.Run(async () => await ReadDoubleAsync(address)).GetAwaiter().GetResult();
        }

        public OperationResult<double[]> ReadDouble(string address, int length)
        {
            return Task.Run(async () => await ReadDoubleAsync(address, length)).GetAwaiter().GetResult();
        }

        public OperationResult<string> ReadString(string address, int length)
        {
            return Task.Run(async () => await ReadStringAsync(address, length)).GetAwaiter().GetResult();
        }

        #endregion

        #region ReadAsync

        /// <summary>
        /// 异步批量读取数据
        /// </summary>
        public virtual ValueTask<OperationResult<Dictionary<string, (DataTypeEnums, object)>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 异步读取数据
        /// </summary>
        internal abstract ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, DataTypeEnums dataType,bool isBit = false);

        /// <summary>
        /// 异步读取单个字节
        /// </summary>
        public async ValueTask<OperationResult<byte>> ReadByteAsync(string address)
        {
            var result = await ReadByteAsync(address, 1);
            if (result.IsSuccess)
            {
                return new OperationResult<byte> { IsSuccess = true, ResultValue = result.ResultValue[0] };
            }
            return new OperationResult<byte> { IsSuccess = false, Message = result.Message };
        }

        /// <summary>
        /// 异步读取多个字节
        /// </summary>
        public async ValueTask<OperationResult<byte[]>> ReadByteAsync(string address, int length)
        {
            return await ReadAsync(address, length, DataTypeEnums.Byte, false);
        }

        /// <summary>
        /// 异步读取布尔值
        /// </summary>
        public virtual async ValueTask<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            var result = await ReadBooleanAsync(address, 1);
            if (result.IsSuccess)
            {
                return new OperationResult<bool> { IsSuccess = true, ResultValue = result.ResultValue[0] };
            }
            return new OperationResult<bool> { IsSuccess = false, Message = result.Message };
        }

        /// <summary>
        /// 异步读取布尔数组
        /// </summary>
        public virtual async ValueTask<OperationResult<bool[]>> ReadBooleanAsync(string address, int length)
        {
            var readResult = await ReadAsync(address, length, DataTypeEnums.Bool, isBit: true);
            if (!readResult.IsSuccess)
            {
                return new OperationResult<bool[]> { IsSuccess = false, Message = readResult.Message };
            }
            return new OperationResult<bool[]> 
            { 
                IsSuccess = true, 
                ResultValue = readResult.ResultValue.ToBool(0, length, false) 
            };
        }

        public OperationResult<bool> ReadBoolean(int startAddressOffest, int addressIndex, byte[] values)
        {
            try
            {
                var interval = addressIndex - startAddressOffest;
                var byteArry = values.Skip(interval * 1).Take(1).ToArray();
                return new OperationResult<bool>
                {
                    ResultValue = BitConverter.ToBoolean(byteArry, 0)
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<bool>
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public async ValueTask<OperationResult<short>> ReadInt16Async(string address)
        {
            var result = await ReadInt16Async(address, 1);
            if (result.IsSuccess)
            {
                return new OperationResult<short> { IsSuccess = true, ResultValue = result.ResultValue[0] };
            }
            return new OperationResult<short> { IsSuccess = false, Message = result.Message };
        }

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public async ValueTask<OperationResult<short[]>> ReadInt16Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 2 * length, DataTypeEnums.Int16);
            if (!readResult.IsSuccess)
            {
                return new OperationResult<short[]> { IsSuccess = false, Message = readResult.Message };
            }
            return new OperationResult<short[]> 
            { 
                IsSuccess = true, 
                ResultValue = readResult.ResultValue.ToInt16(0, length, IsReverse) 
            };
        }

        public OperationResult<short> ReadInt16(int startAddressOffest, int addressIndex, byte[] values)
        {
            try
            {
                var interval = addressIndex - startAddressOffest;
                var byteArry = values.Skip(interval * 2).Take(2).Reverse().ToArray();
                return new OperationResult<short>
                {
                    ResultValue = BitConverter.ToInt16(byteArry, 0)
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<short>
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<ushort>> ReadUInt16Async(string address)
        {
            var result = await ReadUInt16Async(address, 1);
            if (result.IsSuccess)
            {
                return new OperationResult<ushort> { IsSuccess = true, ResultValue = result.ResultValue[0] };
            }
            return new OperationResult<ushort> { IsSuccess = false, Message = result.Message };
        }

        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<ushort[]>> ReadUInt16Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 2 * length, DataTypeEnums.UInt16);
            if (!readResult.IsSuccess)
            {
                return new OperationResult<ushort[]> { IsSuccess = false, Message = readResult.Message };
            }
            return new OperationResult<ushort[]> 
            { 
                IsSuccess = true, 
                ResultValue = readResult.ResultValue.ToUInt16(0, length, IsReverse) 
            };
        }

        public OperationResult<ushort> ReadUInt16(int startAddressOffest, int addressIndex, byte[] values)
        {
            try
            {
                var interval = addressIndex - startAddressOffest;
                var byteArry = values.Skip(interval * 2).Take(2).Reverse().ToArray();
                return new OperationResult<ushort>
                {
                    ResultValue = BitConverter.ToUInt16(byteArry, 0)
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<ushort>
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<int>> ReadInt32Async(string address)
        {
            var result = await ReadInt32Async(address, 1);
            if (result.IsSuccess)
            {
                return new OperationResult<int> { IsSuccess = true, ResultValue = result.ResultValue[0] };
            }
            return new OperationResult<int> { IsSuccess = false, Message = result.Message };
        }

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<int[]>> ReadInt32Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 4 * length, DataTypeEnums.Int32);
            if (!readResult.IsSuccess)
            {
                return new OperationResult<int[]> { IsSuccess = false, Message = readResult.Message };
            }
            return new OperationResult<int[]> 
            { 
                IsSuccess = true, 
                ResultValue = readResult.ResultValue.ToInt32(0, length, DataFormat) 
            };
        }

        public OperationResult<int> ReadInt32(int startAddressOffest, int addressIndex, byte[] values)
        {
            try
            {
                var interval = (addressIndex - startAddressOffest) / 2;
                var offset = (addressIndex - startAddressOffest) % 2 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 2 + offset).Take(2 * 2).ToArray();
                return new OperationResult<int>
                {
                    ResultValue = byteArry.ToInt32(0, DataFormat)
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<int>
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<uint>> ReadUInt32Async(string address)
        {
            var result = await ReadUInt32Async(address, 1);
            if (result.IsSuccess)
            {
                return new OperationResult<uint> { IsSuccess = true, ResultValue = result.ResultValue[0] };
            }
            return new OperationResult<uint> { IsSuccess = false, Message = result.Message };
        }

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<uint[]>> ReadUInt32Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 4 * length, DataTypeEnums.UInt32);
            if (!readResult.IsSuccess)
            {
                return new OperationResult<uint[]> { IsSuccess = false, Message = readResult.Message };
            }
            return new OperationResult<uint[]> 
            { 
                IsSuccess = true, 
                ResultValue = readResult.ResultValue.ToUInt32(0, length, DataFormat) 
            };
        }

        public OperationResult<uint> ReadUInt32(int startAddressOffest, int addressIndex, byte[] values)
        {
            try
            {
                var interval = (addressIndex - startAddressOffest) / 2;
                var offset = (addressIndex - startAddressOffest) % 2 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 2 + offset).Take(2 * 2).ToArray();
                return new OperationResult<uint>
                {
                    ResultValue = byteArry.ToUInt32(0, DataFormat)
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<uint>
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<long>> ReadInt64Async(string address)
        {
            var result = await ReadInt64Async(address, 1);
            if (result.IsSuccess)
            {
                return new OperationResult<long> { IsSuccess = true, ResultValue = result.ResultValue[0] };
            }
            return new OperationResult<long> { IsSuccess = false, Message = result.Message };
        }

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<long[]>> ReadInt64Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 8 * length, DataTypeEnums.Int64);
            if (!readResult.IsSuccess)
            {
                return new OperationResult<long[]> { IsSuccess = false, Message = readResult.Message };
            }
            return new OperationResult<long[]> 
            { 
                IsSuccess = true, 
                ResultValue = readResult.ResultValue.ToInt64(0, length, DataFormat) 
            };
        }

        public OperationResult<long> ReadInt64(int startAddressOffest, int addressIndex, byte[] values)
        {
            try
            {
                var interval = (addressIndex - startAddressOffest) / 4;
                var offset = (addressIndex - startAddressOffest) % 4 * 2;
                var byteArry = values.Skip(interval * 2 * 4 + offset).Take(2 * 4).ToArray();
                return new OperationResult<long>
                {
                    ResultValue = byteArry.ToInt64(0, DataFormat)
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<long>
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<ulong>> ReadUInt64Async(string address)
        {
            var result = await ReadUInt64Async(address, 1);
            if (result.IsSuccess)
            {
                return new OperationResult<ulong> { IsSuccess = true, ResultValue = result.ResultValue[0] };
            }
            return new OperationResult<ulong> { IsSuccess = false, Message = result.Message };
        }

        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<ulong[]>> ReadUInt64Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 8 * length, DataTypeEnums.UInt64);
            if (!readResult.IsSuccess)
            {
                return new OperationResult<ulong[]> { IsSuccess = false, Message = readResult.Message };
            }
            return new OperationResult<ulong[]> 
            { 
                IsSuccess = true, 
                ResultValue = readResult.ResultValue.ToUInt64(0, length, DataFormat) 
            };
        }

        public OperationResult<ulong> ReadUInt64(int startAddressOffest, int addressIndex, byte[] values)
        {
            try
            {
                var interval = (addressIndex - startAddressOffest) / 4;
                var offset = (addressIndex - startAddressOffest) % 4 * 2;
                var byteArry = values.Skip(interval * 2 * 4 + offset).Take(2 * 4).ToArray();
                return new OperationResult<ulong>
                {
                    ResultValue = byteArry.ToUInt64(0, DataFormat)
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<ulong>
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<float>> ReadFloatAsync(string address)
        {
            var result = await ReadFloatAsync(address, 1);
            if (result.IsSuccess)
            {
                return new OperationResult<float> { IsSuccess = true, ResultValue = result.ResultValue[0] };
            }
            return new OperationResult<float> { IsSuccess = false, Message = result.Message };
        }

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<float[]>> ReadFloatAsync(string address, int length)
        {
            var readResult = await ReadAsync(address, 4 * length, DataTypeEnums.Float);
            if (!readResult.IsSuccess)
            {
                return new OperationResult<float[]> { IsSuccess = false, Message = readResult.Message };
            }
            return new OperationResult<float[]> 
            { 
                IsSuccess = true, 
                ResultValue = readResult.ResultValue.ToFloat(0, length, DataFormat) 
            };
        }

        public OperationResult<float> ReadFloat(int beginaddressIndex, int addressIndex, byte[] values)
        {
            try
            {
                var interval = (addressIndex - beginaddressIndex) / 2;
                var offset = (addressIndex - beginaddressIndex) % 2 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 2 + offset).Take(2 * 2).ToArray();
                return new OperationResult<float>
                {
                    ResultValue = byteArry.ToFloat(0, DataFormat)
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<float>
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<double>> ReadDoubleAsync(string address)
        {
            var result = await ReadDoubleAsync(address, 1);
            if (result.IsSuccess)
            {
                return new OperationResult<double> { IsSuccess = true, ResultValue = result.ResultValue[0] };
            }
            return new OperationResult<double> { IsSuccess = false, Message = result.Message };
        }

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<double[]>> ReadDoubleAsync(string address, int length)
        {
            var readResult = await ReadAsync(address, 8 * length, DataTypeEnums.Double);
            if (!readResult.IsSuccess)
            {
                return new OperationResult<double[]> { IsSuccess = false, Message = readResult.Message };
            }
            return new OperationResult<double[]> 
            { 
                IsSuccess = true, 
                ResultValue = readResult.ResultValue.ToDouble(0, length, DataFormat) 
            };
        }

        public OperationResult<double> ReadDouble(int beginaddressIndex, int addressIndex, byte[] values)
        {
            try
            {
                var interval = (addressIndex - beginaddressIndex) / 4;
                var offset = (addressIndex - beginaddressIndex) % 4 * 2;
                var byteArry = values.Skip(interval * 2 * 4 + offset).Take(2 * 4).ToArray();
                return new OperationResult<double>
                {
                    ResultValue = byteArry.ToDouble(0,DataFormat)
                };
            }
            catch (Exception ex)
            {
                return new OperationResult<double>
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }

        public async ValueTask<OperationResult<string>> ReadStringAsync(string address, int length)
        {
            var readResult = await ReadAsync(address, 4 * length, DataTypeEnums.String);
            if (!readResult.IsSuccess)
            {
                return new OperationResult<string> { IsSuccess = false, Message = readResult.Message };
            }
            return new OperationResult<string> 
            { 
                IsSuccess = true, 
                ResultValue = readResult.ResultValue.ToString(0, length, encoding: Encoding.ASCII) 
            };
        }

        #endregion

        #region Write

        public virtual OperationResult BatchWrite(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            return Task.Run(async () => await BatchWriteAsync(addresses)).GetAwaiter().GetResult();
        }

        internal virtual OperationResult Write(string address, byte[] data, DataTypeEnums dataType, bool isBit = false)
        {
            return Task.Run(async () => await WriteAsync(address, data,dataType, isBit)).GetAwaiter().GetResult();
        }   

        public virtual OperationResult Write(string address, byte[] value)
        {
            return Task.Run(async () => await WriteAsync(address, value, DataTypeEnums.Byte, false)).GetAwaiter().GetResult();
        }
        
        public virtual OperationResult Write(string address, byte value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public virtual OperationResult Write(string address, bool value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }
        
        public virtual OperationResult Write(string address, bool[] value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, short value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, short[] value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, ushort value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, ushort[] value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, int value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, int[] value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, uint value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, uint[] value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, long value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, long[] value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, ulong value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, ulong[] value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, float value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, float[] value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, double value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, double[] value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        public OperationResult Write(string address, string value)
        {
            return Task.Run(async () => await WriteAsync(address, value)).GetAwaiter().GetResult();
        }

        #endregion

        #region WriteAsync

        public virtual ValueTask<OperationResult> BatchWriteAsync(Dictionary<string, (DataTypeEnums, object)> addresses)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="data">值</param>
        /// <param name="isBit">值</param>
        /// <returns></returns>
        internal abstract Task<OperationResult> WriteAsync(string address, byte[] data,DataTypeEnums dataType, bool isBit = false);

        public virtual async Task<OperationResult> WriteAsync(string address, byte[] value) => await WriteAsync(address, value, DataTypeEnums.Byte, false);

        public virtual async Task<OperationResult> WriteAsync(string address, byte value)
        {
            return await WriteAsync(address, new byte[1] { value });
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async virtual Task<OperationResult> WriteAsync(string address, bool value)
        {
            return await WriteAsync(address, value ? new byte[] { 0x01 } : new byte[] { 0x00 }, DataTypeEnums.Bool, true);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public virtual async Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            return await WriteAsync(address, value.ToBytes(), DataTypeEnums.Bool, true);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, short value)
        {
            return await WriteAsync(address, value.ToByte(IsReverse), DataTypeEnums.Int16);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, short[] value)
        {
            return await WriteAsync(address, value.ToByte(IsReverse), DataTypeEnums.Int16);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, ushort value)
        {
            return await WriteAsync(address, value.ToByte(IsReverse), DataTypeEnums.UInt16);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, ushort[] value)
        {
            return await WriteAsync(address, value.ToByte(IsReverse), DataTypeEnums.UInt16);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, int value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.Int32);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, int[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.Int32);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, uint value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.UInt32);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, uint[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.UInt32);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, long value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.Int64);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, long[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.Int64);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, ulong value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.UInt64);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, ulong[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.UInt64);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, float value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.Float);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, float[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.Float);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, double value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.Double);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, double[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat), DataTypeEnums.Double);
        }

        public async Task<OperationResult> WriteAsync(string address, string value)
        {
            return await WriteAsync(address, value.ToByte(Encoding.ASCII), DataTypeEnums.String);
        }

        // 类型化写入方法别名
        public async Task<OperationResult> WriteBooleanAsync(string address, bool value)
        {
            return await WriteAsync(address, value);
        }

        public async Task<OperationResult> WriteUInt16Async(string address, ushort value)
        {
            return await WriteAsync(address, value);
        }

        public async Task<OperationResult> WriteInt16Async(string address, short value)
        {
            return await WriteAsync(address, value);
        }

        public async Task<OperationResult> WriteUInt32Async(string address, uint value)
        {
            return await WriteAsync(address, value);
        }

        public async Task<OperationResult> WriteInt32Async(string address, int value)
        {
            return await WriteAsync(address, value);
        }

        public async Task<OperationResult> WriteFloatAsync(string address, float value)
        {
            return await WriteAsync(address, value);
        }

        public async Task<OperationResult> WriteDoubleAsync(string address, double value)
        {
            return await WriteAsync(address, value);
        }

        #endregion

        #region object类型操作
        /// <summary>
        /// 读取对象类型数据
        /// </summary>
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address)
        {
            try
            {
                switch (dataTypeEnum)
                {
                    case DataTypeEnums.None:
                        return new OperationResult<object> { IsSuccess = false, Message = "数据类型为null" };
                    case DataTypeEnums.Bool:
                        var boolResult = ReadBoolean(address);
                        return new OperationResult<object> { IsSuccess = boolResult.IsSuccess, ResultValue = boolResult.IsSuccess ? (object)boolResult.ResultValue : null, Message = boolResult.Message };
                    case DataTypeEnums.Byte:
                        var byteResult = ReadByte(address);
                        return new OperationResult<object> { IsSuccess = byteResult.IsSuccess, ResultValue = byteResult.IsSuccess ? (object)byteResult.ResultValue : null, Message = byteResult.Message };
                    case DataTypeEnums.Int16:
                        var int16Result = ReadInt16(address);
                        return new OperationResult<object> { IsSuccess = int16Result.IsSuccess, ResultValue = int16Result.IsSuccess ? (object)int16Result.ResultValue : null, Message = int16Result.Message };
                    case DataTypeEnums.UInt16:
                        var uint16Result = ReadUInt16(address);
                        return new OperationResult<object> { IsSuccess = uint16Result.IsSuccess, ResultValue = uint16Result.IsSuccess ? (object)uint16Result.ResultValue : null, Message = uint16Result.Message };
                    case DataTypeEnums.Int32:
                        var int32Result = ReadInt32(address);
                        return new OperationResult<object> { IsSuccess = int32Result.IsSuccess, ResultValue = int32Result.IsSuccess ? (object)int32Result.ResultValue : null, Message = int32Result.Message };
                    case DataTypeEnums.UInt32:
                        var uint32Result = ReadUInt32(address);
                        return new OperationResult<object> { IsSuccess = uint32Result.IsSuccess, ResultValue = uint32Result.IsSuccess ? (object)uint32Result.ResultValue : null, Message = uint32Result.Message };
                    case DataTypeEnums.Int64:
                        var int64Result = ReadInt64(address);
                        return new OperationResult<object> { IsSuccess = int64Result.IsSuccess, ResultValue = int64Result.IsSuccess ? (object)int64Result.ResultValue : null, Message = int64Result.Message };
                    case DataTypeEnums.UInt64:
                        var uint64Result = ReadUInt64(address);
                        return new OperationResult<object> { IsSuccess = uint64Result.IsSuccess, ResultValue = uint64Result.IsSuccess ? (object)uint64Result.ResultValue : null, Message = uint64Result.Message };
                    case DataTypeEnums.Float:
                        var floatResult = ReadFloat(address);
                        return new OperationResult<object> { IsSuccess = floatResult.IsSuccess, ResultValue = floatResult.IsSuccess ? (object)floatResult.ResultValue : null, Message = floatResult.Message };
                    case DataTypeEnums.Double:
                        var doubleResult = ReadDouble(address);
                        return new OperationResult<object> { IsSuccess = doubleResult.IsSuccess, ResultValue = doubleResult.IsSuccess ? (object)doubleResult.ResultValue : null, Message = doubleResult.Message };
                    case DataTypeEnums.String:
                        return new OperationResult<object> { IsSuccess = false, Message = "string泛型读取没有实现" };
                    default:
                        return new OperationResult<object> { IsSuccess = false, Message = "未知的数据类型" };
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<object> { IsSuccess = false, Message = $"类型转换错误: {ex.Message}" };
            }
        }

        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address, int length)
        {
            try
            {
                switch (dataTypeEnum)
                {
                    case DataTypeEnums.None:
                        return new OperationResult<object> { IsSuccess = false, Message = "数据类型为null" };
                    case DataTypeEnums.Bool:
                        var boolResult = ReadBoolean(address, length);
                        return new OperationResult<object> { IsSuccess = boolResult.IsSuccess, ResultValue = boolResult.IsSuccess ? (object)boolResult.ResultValue : null, Message = boolResult.Message };
                    case DataTypeEnums.Byte:
                        var byteResult = ReadByte(address, length);
                        return new OperationResult<object> { IsSuccess = byteResult.IsSuccess, ResultValue = byteResult.IsSuccess ? (object)byteResult.ResultValue : null, Message = byteResult.Message };
                    case DataTypeEnums.Int16:
                        var int16Result = ReadInt16(address, length);
                        return new OperationResult<object> { IsSuccess = int16Result.IsSuccess, ResultValue = int16Result.IsSuccess ? (object)int16Result.ResultValue : null, Message = int16Result.Message };
                    case DataTypeEnums.UInt16:
                        var uint16Result = ReadUInt16(address, length);
                        return new OperationResult<object> { IsSuccess = uint16Result.IsSuccess, ResultValue = uint16Result.IsSuccess ? (object)uint16Result.ResultValue : null, Message = uint16Result.Message };
                    case DataTypeEnums.Int32:
                        var int32Result = ReadInt32(address, length);
                        return new OperationResult<object> { IsSuccess = int32Result.IsSuccess, ResultValue = int32Result.IsSuccess ? (object)int32Result.ResultValue : null, Message = int32Result.Message };
                    case DataTypeEnums.UInt32:
                        var uint32Result = ReadUInt32(address, length);
                        return new OperationResult<object> { IsSuccess = uint32Result.IsSuccess, ResultValue = uint32Result.IsSuccess ? (object)uint32Result.ResultValue : null, Message = uint32Result.Message };
                    case DataTypeEnums.Int64:
                        var int64Result = ReadInt64(address, length);
                        return new OperationResult<object> { IsSuccess = int64Result.IsSuccess, ResultValue = int64Result.IsSuccess ? (object)int64Result.ResultValue : null, Message = int64Result.Message };
                    case DataTypeEnums.UInt64:
                        var uint64Result = ReadUInt64(address, length);
                        return new OperationResult<object> { IsSuccess = uint64Result.IsSuccess, ResultValue = uint64Result.IsSuccess ? (object)uint64Result.ResultValue : null, Message = uint64Result.Message };
                    case DataTypeEnums.Float:
                        var floatResult = ReadFloat(address, length);
                        return new OperationResult<object> { IsSuccess = floatResult.IsSuccess, ResultValue = floatResult.IsSuccess ? (object)floatResult.ResultValue : null, Message = floatResult.Message };
                    case DataTypeEnums.Double:
                        var doubleResult = ReadDouble(address, length);
                        return new OperationResult<object> { IsSuccess = doubleResult.IsSuccess, ResultValue = doubleResult.IsSuccess ? (object)doubleResult.ResultValue : null, Message = doubleResult.Message };
                    case DataTypeEnums.String:
                        return new OperationResult<object> { IsSuccess = false, Message = "string泛型读取没有实现" };
                    default:
                        return new OperationResult<object> { IsSuccess = false, Message = "未知的数据类型" };
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<object> { IsSuccess = false, Message = $"类型转换错误: {ex.Message}" };
            }
        }

        public async ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address)
        {
            try
            {
                switch (dataTypeEnum)
                {
                    case DataTypeEnums.None:
                        return new OperationResult<object> { IsSuccess = false, Message = "数据类型为null" };
                    case DataTypeEnums.Bool:
                        var boolResult = await ReadBooleanAsync(address);
                        return new OperationResult<object> { IsSuccess = boolResult.IsSuccess, ResultValue = boolResult.IsSuccess ? (object)boolResult.ResultValue : null, Message = boolResult.Message };
                    case DataTypeEnums.Byte:
                        var byteResult = await ReadByteAsync(address);
                        return new OperationResult<object> { IsSuccess = byteResult.IsSuccess, ResultValue = byteResult.IsSuccess ? (object)byteResult.ResultValue : null, Message = byteResult.Message };
                    case DataTypeEnums.Int16:
                        var int16Result = await ReadInt16Async(address);
                        return new OperationResult<object> { IsSuccess = int16Result.IsSuccess, ResultValue = int16Result.IsSuccess ? (object)int16Result.ResultValue : null, Message = int16Result.Message };
                    case DataTypeEnums.UInt16:
                        var uint16Result = await ReadUInt16Async(address);
                        return new OperationResult<object> { IsSuccess = uint16Result.IsSuccess, ResultValue = uint16Result.IsSuccess ? (object)uint16Result.ResultValue : null, Message = uint16Result.Message };
                    case DataTypeEnums.Int32:
                        var int32Result = await ReadInt32Async(address);
                        return new OperationResult<object> { IsSuccess = int32Result.IsSuccess, ResultValue = int32Result.IsSuccess ? (object)int32Result.ResultValue : null, Message = int32Result.Message };
                    case DataTypeEnums.UInt32:
                        var uint32Result = await ReadUInt32Async(address);
                        return new OperationResult<object> { IsSuccess = uint32Result.IsSuccess, ResultValue = uint32Result.IsSuccess ? (object)uint32Result.ResultValue : null, Message = uint32Result.Message };
                    case DataTypeEnums.Int64:
                        var int64Result = await ReadInt64Async(address);
                        return new OperationResult<object> { IsSuccess = int64Result.IsSuccess, ResultValue = int64Result.IsSuccess ? (object)int64Result.ResultValue : null, Message = int64Result.Message };
                    case DataTypeEnums.UInt64:
                        var uint64Result = await ReadUInt64Async(address);
                        return new OperationResult<object> { IsSuccess = uint64Result.IsSuccess, ResultValue = uint64Result.IsSuccess ? (object)uint64Result.ResultValue : null, Message = uint64Result.Message };
                    case DataTypeEnums.Float:
                        var floatResult = await ReadFloatAsync(address);
                        return new OperationResult<object> { IsSuccess = floatResult.IsSuccess, ResultValue = floatResult.IsSuccess ? (object)floatResult.ResultValue : null, Message = floatResult.Message };
                    case DataTypeEnums.Double:
                        var doubleResult = await ReadDoubleAsync(address);
                        return new OperationResult<object> { IsSuccess = doubleResult.IsSuccess, ResultValue = doubleResult.IsSuccess ? (object)doubleResult.ResultValue : null, Message = doubleResult.Message };
                    case DataTypeEnums.String:
                        return new OperationResult<object> { IsSuccess = false, Message = "string泛型读取没有实现" };
                    default:
                        return new OperationResult<object> { IsSuccess = false, Message = "未知的数据类型" };
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<object> { IsSuccess = false, Message = $"类型转换错误: {ex.Message}" };
            }
        }

        public async ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address, int length)
        {
            try
            {
                switch (dataTypeEnum)
                {
                    case DataTypeEnums.None:
                        return new OperationResult<object> { IsSuccess = false, Message = "数据类型为null" };
                    case DataTypeEnums.Bool:
                        var boolResult = await ReadBooleanAsync(address, length);
                        return new OperationResult<object> { IsSuccess = boolResult.IsSuccess, ResultValue = boolResult.IsSuccess ? (object)boolResult.ResultValue : null, Message = boolResult.Message };
                    case DataTypeEnums.Byte:
                        var byteResult = await ReadByteAsync(address, length);
                        return new OperationResult<object> { IsSuccess = byteResult.IsSuccess, ResultValue = byteResult.IsSuccess ? (object)byteResult.ResultValue : null, Message = byteResult.Message };
                    case DataTypeEnums.Int16:
                        var int16Result = await ReadInt16Async(address, length);
                        return new OperationResult<object> { IsSuccess = int16Result.IsSuccess, ResultValue = int16Result.IsSuccess ? (object)int16Result.ResultValue : null, Message = int16Result.Message };
                    case DataTypeEnums.UInt16:
                        var uint16Result = await ReadUInt16Async(address, length);
                        return new OperationResult<object> { IsSuccess = uint16Result.IsSuccess, ResultValue = uint16Result.IsSuccess ? (object)uint16Result.ResultValue : null, Message = uint16Result.Message };
                    case DataTypeEnums.Int32:
                        var int32Result = await ReadInt32Async(address, length);
                        return new OperationResult<object> { IsSuccess = int32Result.IsSuccess, ResultValue = int32Result.IsSuccess ? (object)int32Result.ResultValue : null, Message = int32Result.Message };
                    case DataTypeEnums.UInt32:
                        var uint32Result = await ReadUInt32Async(address, length);
                        return new OperationResult<object> { IsSuccess = uint32Result.IsSuccess, ResultValue = uint32Result.IsSuccess ? (object)uint32Result.ResultValue : null, Message = uint32Result.Message };
                    case DataTypeEnums.Int64:
                        var int64Result = await ReadInt64Async(address, length);
                        return new OperationResult<object> { IsSuccess = int64Result.IsSuccess, ResultValue = int64Result.IsSuccess ? (object)int64Result.ResultValue : null, Message = int64Result.Message };
                    case DataTypeEnums.UInt64:
                        var uint64Result = await ReadUInt64Async(address, length);
                        return new OperationResult<object> { IsSuccess = uint64Result.IsSuccess, ResultValue = uint64Result.IsSuccess ? (object)uint64Result.ResultValue : null, Message = uint64Result.Message };
                    case DataTypeEnums.Float:
                        var floatResult = await ReadFloatAsync(address, length);
                        return new OperationResult<object> { IsSuccess = floatResult.IsSuccess, ResultValue = floatResult.IsSuccess ? (object)floatResult.ResultValue : null, Message = floatResult.Message };
                    case DataTypeEnums.Double:
                        var doubleResult = await ReadDoubleAsync(address, length);
                        return new OperationResult<object> { IsSuccess = doubleResult.IsSuccess, ResultValue = doubleResult.IsSuccess ? (object)doubleResult.ResultValue : null, Message = doubleResult.Message };
                    case DataTypeEnums.String:
                        return new OperationResult<object> { IsSuccess = false, Message = "string泛型读取没有实现" };
                    default:
                        return new OperationResult<object> { IsSuccess = false, Message = "未知的数据类型" };
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<object> { IsSuccess = false, Message = $"类型转换错误: {ex.Message}" };
            }
        }

        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object value)
        {
            try
            {
                switch (dataTypeEnum)
                {
                    case DataTypeEnums.None:
                        return new OperationResult { IsSuccess = false, Message = "数据类型为null" };
                    case DataTypeEnums.Bool:
                        return Write(address, (bool)value.ToString().ConvertFromStringToObject(dataTypeEnum));
                    case DataTypeEnums.Byte:
                        return Write(address, (byte)value.ToString().ConvertFromStringToObject(dataTypeEnum));
                    case DataTypeEnums.Int16:
                        return Write(address, (short)value.ToString().ConvertFromStringToObject(dataTypeEnum));
                    case DataTypeEnums.UInt16:
                        return Write(address, (ushort)value.ToString().ConvertFromStringToObject(dataTypeEnum));
                    case DataTypeEnums.Int32:
                        return Write(address, (int)value.ToString().ConvertFromStringToObject(dataTypeEnum));
                    case DataTypeEnums.UInt32:
                        return Write(address, (uint)value.ToString().ConvertFromStringToObject(dataTypeEnum));
                    case DataTypeEnums.Int64:
                        return Write(address, (long)value.ToString().ConvertFromStringToObject(dataTypeEnum));
                    case DataTypeEnums.UInt64:
                        return Write(address, (ulong)value.ToString().ConvertFromStringToObject(dataTypeEnum));
                    case DataTypeEnums.Float:
                        return Write(address, (float)value.ToString().ConvertFromStringToObject(dataTypeEnum));
                    case DataTypeEnums.Double:
                        return Write(address, (double)value.ToString().ConvertFromStringToObject(dataTypeEnum));
                    case DataTypeEnums.String:
                        return new OperationResult { IsSuccess = false, Message = "string写入未实现" };
                    default:
                        return new OperationResult { IsSuccess = false, Message = "未知的数据类型" };
                }
            }
            catch (Exception ex)
            {
                return new OperationResult { IsSuccess = false, Message = $"类型转换错误: {ex.Message}" };
            }
        }

        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object[] values)
        {
            try
            {
                switch (dataTypeEnum)
                {
                    case DataTypeEnums.None:
                        return new OperationResult { IsSuccess = false, Message = "数据类型为null" };
                    case DataTypeEnums.Bool:
                        return Write(address, values.Cast<bool>().ToArray());
                    case DataTypeEnums.Byte:
                        return Write(address, values.Cast<byte>().ToArray());
                    case DataTypeEnums.Int16:
                        return Write(address, values.Cast<short>().ToArray());
                    case DataTypeEnums.UInt16:
                        return Write(address, values.Cast<ushort>().ToArray());
                    case DataTypeEnums.Int32:
                        return Write(address, values.Cast<int>().ToArray());
                    case DataTypeEnums.UInt32:
                        return Write(address, values.Cast<uint>().ToArray());
                    case DataTypeEnums.Int64:
                        return Write(address, values.Cast<long>().ToArray());
                    case DataTypeEnums.UInt64:
                        return Write(address, values.Cast<ulong>().ToArray());
                    case DataTypeEnums.Float:
                        return Write(address, values.Cast<float>().ToArray());
                    case DataTypeEnums.Double:
                        return Write(address, values.Cast<double>().ToArray());
                    case DataTypeEnums.String:
                        return new OperationResult { IsSuccess = false, Message = "string写入未实现" };
                    default:
                        return new OperationResult { IsSuccess = false, Message = "未知的数据类型" };
                }
            }
            catch (Exception ex)
            {
                return new OperationResult { IsSuccess = false, Message = $"类型转换错误: {ex.Message}" };
            }
        }

        public async Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object value)
        {
            try
            {
                switch (dataTypeEnum)
                {
                    case DataTypeEnums.None:
                        return new OperationResult { IsSuccess = false, Message = "数据类型为null" };
                    case DataTypeEnums.Bool:
                        return await WriteAsync(address, (bool)(value.ToString().ConvertFromStringToObject(dataTypeEnum)));
                    case DataTypeEnums.Byte:
                        return await WriteAsync(address, (byte)(value.ToString().ConvertFromStringToObject(dataTypeEnum)));
                    case DataTypeEnums.Int16:
                        return await WriteAsync(address, (short)(value.ToString().ConvertFromStringToObject(dataTypeEnum)));
                    case DataTypeEnums.UInt16:
                        return await WriteAsync(address, (ushort)(value.ToString().ConvertFromStringToObject(dataTypeEnum)));
                    case DataTypeEnums.Int32:
                        return await WriteAsync(address, (int)(value.ToString().ConvertFromStringToObject(dataTypeEnum)));
                    case DataTypeEnums.UInt32:
                        return await WriteAsync(address, (uint)(value.ToString().ConvertFromStringToObject(dataTypeEnum)));
                    case DataTypeEnums.Int64:
                        return await WriteAsync(address, (long)(value.ToString().ConvertFromStringToObject(dataTypeEnum)));
                    case DataTypeEnums.UInt64:
                        return await WriteAsync(address, (ulong)(value.ToString().ConvertFromStringToObject(dataTypeEnum)));
                    case DataTypeEnums.Float:
                        return await WriteAsync(address, (float)(value.ToString().ConvertFromStringToObject(dataTypeEnum)));
                    case DataTypeEnums.Double:
                        return await WriteAsync(address, (double)(value.ToString().ConvertFromStringToObject(dataTypeEnum)));
                    case DataTypeEnums.String:
                        return new OperationResult { IsSuccess = false, Message = "string写入未实现" };
                    default:
                        return new OperationResult { IsSuccess = false, Message = "未知的数据类型" };
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateFailedResult(ex.Message);
            }
        }

        public async Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object[] values)
        {
            switch (dataTypeEnum)
            {
                case DataTypeEnums.None:
                    return new OperationResult { IsSuccess = false, Message = "数据类型为null" };
                case DataTypeEnums.Bool:
                    return await WriteAsync(address, values.Cast<bool>().ToArray());
                case DataTypeEnums.Byte:
                    return await WriteAsync(address, values.Cast<byte>().ToArray());
                case DataTypeEnums.Int16:
                    return await WriteAsync(address, values.Cast<short>().ToArray());
                case DataTypeEnums.UInt16:
                    return await WriteAsync(address, values.Cast<ushort>().ToArray());
                case DataTypeEnums.Int32:
                    return await WriteAsync(address, values.Cast<int>().ToArray());
                case DataTypeEnums.UInt32:
                    return await WriteAsync(address, values.Cast<uint>().ToArray());
                case DataTypeEnums.Int64:
                    return await WriteAsync(address, values.Cast<long>().ToArray());
                case DataTypeEnums.UInt64:
                    return await WriteAsync(address, values.Cast<ulong>().ToArray());
                case DataTypeEnums.Float:
                    return await WriteAsync(address, values.Cast<float>().ToArray());
                case DataTypeEnums.Double:
                    return await WriteAsync(address, values.Cast<double>().ToArray());
                case DataTypeEnums.String:
                    return new OperationResult { IsSuccess = false, Message = "string写入未实现" };
                default:
                    return new OperationResult { IsSuccess = false, Message = "未知的数据类型" };
            }
        }

        #endregion

        private bool disposedValue;

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // 释放托管资源
                }

                // 释放非托管资源
                disposedValue = true;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 根据寄存器类型和操作类型获取功能码
        /// </summary>
        /// <param name="registerType">寄存器类型</param>
        /// <param name="dataType">数据类型</param>
        /// <param name="isWrite">是否为写操作</param>
        /// <param name="dataLength">数据长度（字节数），用于判断是否写多个寄存器/线圈</param>
        /// <returns>功能码</returns>
        private static byte GetFunctionCodeByRegisterType(byte registerType, DataTypeEnums dataType, bool isWrite, int dataLength = 1)
        {
            if (isWrite)
            {
                switch (registerType)
                {
                    case 0x01:  // 线圈
                        // 根据数据长度判断写单个还是多个线圈
                        return dataLength > 1 ? (byte)0x0F : (byte)0x05;
                    case 0x02:  // 离散输入 - 不支持写操作
                        throw new ArgumentException("离散输入寄存器不支持写操作");
                    case 0x03:  // 保持寄存器
                        switch (dataType)
                        {
                            case DataTypeEnums.Int16:
                            case DataTypeEnums.UInt16:
                                return 0x06;  // 写单个寄存器
                            case DataTypeEnums.Int32:
                            case DataTypeEnums.UInt32:
                            case DataTypeEnums.Float:
                            case DataTypeEnums.Int64:
                            case DataTypeEnums.UInt64:
                            case DataTypeEnums.Double:
                                return 0x10;  // 写多个寄存器
                            default:
                                // 根据数据长度判断：大于2字节使用写多个寄存器
                                return dataLength > 2 ? (byte)0x10 : (byte)0x06;
                        }
                    case 0x04:  // 输入寄存器 - 不支持写操作
                        throw new ArgumentException("输入寄存器不支持写操作");
                    default:
                        return dataLength > 2 ? (byte)0x10 : (byte)0x06;  // 默认根据长度判断
                }
            }
            else
            {
                return registerType;  // 读操作直接使用寄存器类型作为功能码
            }
        }

        /// <summary>
        /// 根据数据类型和操作类型自动判断功能码
        /// </summary>
        /// <param name="dataType">数据类型</param>
        /// <param name="isWrite">是否为写操作</param>
        /// <param name="registerType">寄存器类型（可选，用于逻辑地址格式）</param>
        /// <param name="dataLength">数据长度（字节数）</param>
        /// <returns>功能码</returns>
        private static byte GetAutoFunctionCode(DataTypeEnums dataType, bool isWrite, byte registerType = 0x03, int dataLength = 1)
        {
            if (isWrite)
            {
                switch (registerType)
                {
                    case 0x01:  // 线圈
                        return dataLength > 1 ? (byte)0x0F : (byte)0x05;
                    case 0x02:  // 离散输入 - 不支持写操作
                        throw new ArgumentException("离散输入寄存器不支持写操作");
                    case 0x03:  // 保持寄存器
                        switch (dataType)
                        {
                            case DataTypeEnums.Int16:
                            case DataTypeEnums.UInt16:
                                return 0x06;  // 写单个寄存器
                            case DataTypeEnums.Int32:
                            case DataTypeEnums.UInt32:
                            case DataTypeEnums.Float:
                            case DataTypeEnums.Int64:
                            case DataTypeEnums.UInt64:
                            case DataTypeEnums.Double:
                                return 0x10;  // 写多个寄存器
                            default:
                                return dataLength > 2 ? (byte)0x10 : (byte)0x06;
                        }
                    case 0x04:  // 输入寄存器 - 不支持写操作
                        throw new ArgumentException("输入寄存器不支持写操作");
                    default:
                        // 如果没有指定寄存器类型，根据数据类型默认判断
                        switch (dataType)
                        {
                            case DataTypeEnums.Bool:
                                return dataLength > 1 ? (byte)0x0F : (byte)0x05;
                            case DataTypeEnums.Int32:
                            case DataTypeEnums.UInt32:
                            case DataTypeEnums.Float:
                            case DataTypeEnums.Int64:
                            case DataTypeEnums.UInt64:
                            case DataTypeEnums.Double:
                                return 0x10;  // 多字节数据使用写多个寄存器
                            default:
                                return dataLength > 2 ? (byte)0x10 : (byte)0x06;
                        }
                }
            }
            else
            {
                switch (registerType)
                {
                    case 0x01:  // 线圈
                        return 0x01;  // 读线圈
                    case 0x02:  // 离散输入
                        return 0x02;  // 读离散输入
                    case 0x03:  // 保持寄存器
                        return 0x03;  // 读保持寄存器
                    case 0x04:  // 输入寄存器
                        return 0x04;  // 读输入寄存器
                    default:
                        // 如果没有指定寄存器类型，根据数据类型默认判断
                        switch (dataType)
                        {
                            case DataTypeEnums.Bool:
                                return 0x01;  // 读线圈
                            default:
                                return 0x03;  // 默认读保持寄存器
                        }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication
{
    public abstract class DeviceServer : ServerBase, IServer, IReadWrite
    {
        #region Read


        public virtual OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnums> addresses)
        {
            throw new NotImplementedException();
        }

        public virtual ValueTask<OperationResult<Dictionary<string, object>>> BatchReadAsync(Dictionary<string, DataTypeEnums> addresses)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>
        /// <returns></returns>
        internal abstract OperationResult<byte[]> Read(string address, int length, bool isBit = false);


        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>

        /// <returns></returns>
        internal abstract ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false);




        public OperationResult<byte> ReadByte(string address)
        {
            var result = ReadByte(address, 1);
            if (result.IsSuccess)
                return new OperationResult<byte>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<byte>(result).Complete();
        }

        public OperationResult<byte[]> ReadByte(string address, int length)
        {
            return Read(address, length, false);
        }

        public async ValueTask<OperationResult<byte>> ReadByteAsync(string address)
        {
            var result = await ReadByteAsync(address, 1);
            if (result.IsSuccess)
                return new OperationResult<byte>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<byte>(result).Complete();
        }



        public async ValueTask<OperationResult<byte[]>> ReadByteAsync(string address, int length)
        {
            return await ReadAsync(address, length, false);
        }

        /// <summary>
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public virtual OperationResult<bool> ReadBoolean(string address)
        {
            var result = ReadBoolean(address, 1);
            if (result.IsSuccess)
                return new OperationResult<bool>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<bool>(result).Complete();
        }


        public virtual async ValueTask<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            var result = await ReadBooleanAsync(address, 1);
            if (result.IsSuccess)
                return new OperationResult<bool>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<bool>(result).Complete();
        }


        /// <summary>
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public virtual OperationResult<bool[]> ReadBoolean(string address, int length)
        {
            //int reallength = (int)Math.Ceiling(length*1.0 /8);
            var readResult = Read(address, length, isBit: true);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, length, IsReverse);
            return result.Complete();
        }

        public virtual async ValueTask<OperationResult<bool[]>> ReadBooleanAsync(string address, int length)
        {
            //int reallength = (int)Math.Ceiling(length*1.0 /8);
            var readResult = await ReadAsync(address, length, isBit: true);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, length, IsReverse);
            return result.Complete();
        }


        public OperationResult<bool> ReadBoolean(int startAddressOffest, int addressIndex, byte[] values)
        {
            try
            {
                var interval = addressIndex - startAddressOffest;
                var byteArry = values.Skip(interval * 1).Take(1).ToArray();
                return new OperationResult<bool>
                {
                    Value = BitConverter.ToBoolean(byteArry, 0)
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
        public OperationResult<short> ReadInt16(string address)
        {
            var result = ReadInt16(address, 1);
            if (result.IsSuccess)
                return new OperationResult<short>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<short>(result).Complete();
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
                return new OperationResult<short>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<short>(result).Complete();
        }


        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public OperationResult<short[]> ReadInt16(string address, int length)
        {
            var readResult = Read(address, 2 * length);
            var result = new OperationResult<short[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt16(0, length, IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public async ValueTask<OperationResult<short[]>> ReadInt16Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 2 * length);
            var result = new OperationResult<short[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt16(0, length, IsReverse);
            return result.Complete();
        }


        public OperationResult<short> ReadInt16(int startAddressOffest, int addressIndex, byte[] values)
        {
            try
            {
                var interval = addressIndex - startAddressOffest;
                var byteArry = values.Skip(interval * 2).Take(2).Reverse().ToArray();
                return new OperationResult<short>
                {
                    Value = BitConverter.ToInt16(byteArry, 0)
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
        public OperationResult<ushort> ReadUInt16(string address)
        {
            var result = ReadUInt16(address, 1);
            if (result.IsSuccess)
                return new OperationResult<ushort>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<ushort>(result).Complete();
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
                return new OperationResult<ushort>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<ushort>(result).Complete();
        }


        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public OperationResult<ushort[]> ReadUInt16(string address, int length)
        {
            var readResult = Read(address, 2 * length);
            var result = new OperationResult<ushort[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt16(0, length, IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<ushort[]>> ReadUInt16Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 2 * length);
            var result = new OperationResult<ushort[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt16(0, length, IsReverse);
            return result.Complete();
        }


        public OperationResult<ushort> ReadUInt16(int startAddressOffest, int addressIndex, byte[] values)
        {
            try
            {
                var interval = addressIndex - startAddressOffest;
                var byteArry = values.Skip(interval * 2).Take(2).Reverse().ToArray();
                return new OperationResult<ushort>
                {
                    Value = BitConverter.ToUInt16(byteArry, 0)
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
        public OperationResult<int> ReadInt32(string address)
        {
            var result = ReadInt32(address, 1);
            if (result.IsSuccess)
                return new OperationResult<int>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<int>(result).Complete();

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
                return new OperationResult<int>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<int>(result).Complete();

        }



        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public OperationResult<int[]> ReadInt32(string address, int length)
        {
            var readResult = Read(address, 4 * length);
            var result = new OperationResult<int[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt32(0, length, DataFormat, IsReverse);
            return result.Complete();
        }


        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<int[]>> ReadInt32Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 4 * length);
            var result = new OperationResult<int[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt32(0, length, DataFormat, IsReverse);
            return result.Complete();
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
                    Value = byteArry.ToInt32(0, DataFormat)
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
        public OperationResult<uint> ReadUInt32(string address)
        {
            var result = ReadUInt32(address, 1);
            if (result.IsSuccess)
                return new OperationResult<uint>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<uint>(result).Complete();
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
                return new OperationResult<uint>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<uint>(result).Complete();
        }


        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public OperationResult<uint[]> ReadUInt32(string address, int length)
        {
            var readResult = Read(address, 4 * length);
            var result = new OperationResult<uint[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt32(0, length, DataFormat, IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<uint[]>> ReadUInt32Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 4 * length);
            var result = new OperationResult<uint[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt32(0, length, DataFormat, IsReverse);
            return result.Complete();
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
                    Value = byteArry.ToUInt32(0, DataFormat)
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
        public OperationResult<long> ReadInt64(string address)
        {
            var result = ReadInt64(address, 1);
            if (result.IsSuccess)
                return new OperationResult<long>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<long>(result).Complete();
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
                return new OperationResult<long>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<long>(result).Complete();
        }



        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public OperationResult<long[]> ReadInt64(string address, int length)
        {
            var readResult = Read(address, 8 * length);
            var result = new OperationResult<long[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt64(0, length, DataFormat, IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<long[]>> ReadInt64Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 8 * length);
            var result = new OperationResult<long[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt64(0, length, DataFormat, IsReverse);
            return result.Complete();
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
                    Value = byteArry.ToInt64(0, DataFormat)
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
        public OperationResult<ulong> ReadUInt64(string address)
        {
            var result = ReadUInt64(address, 1);
            if (result.IsSuccess)
                return new OperationResult<ulong>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<ulong>(result).Complete();
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
                return new OperationResult<ulong>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<ulong>(result).Complete();
        }


        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public OperationResult<ulong[]> ReadUInt64(string address, int length)
        {
            var readResult = Read(address, 8 * length);
            var result = new OperationResult<ulong[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt64(0, length, DataFormat, IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<ulong[]>> ReadUInt64Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 8 * length);
            var result = new OperationResult<ulong[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt64(0, length, DataFormat, IsReverse);
            return result.Complete();
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
                    Value = byteArry.ToUInt64(0, DataFormat)
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
        public OperationResult<float> ReadFloat(string address)
        {
            var result = ReadFloat(address, 1);
            if (result.IsSuccess)
                return new OperationResult<float>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<float>(result).Complete();
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
                return new OperationResult<float>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<float>(result).Complete();
        }


        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public OperationResult<float[]> ReadFloat(string address, int length)
        {
            var readResult = Read(address, 4 * length);
            var result = new OperationResult<float[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToFloat(0, length, DataFormat, IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<float[]>> ReadFloatAsync(string address, int length)
        {
            var readResult = await ReadAsync(address, 4 * length);
            var result = new OperationResult<float[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToFloat(0, length, DataFormat, IsReverse);
            return result.Complete();
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
                    Value = byteArry.ToFloat(0, DataFormat)
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
        public OperationResult<double> ReadDouble(string address)
        {
            var result = ReadDouble(address, 1);
            if (result.IsSuccess)
                return new OperationResult<double>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<double>(result).Complete();
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
                return new OperationResult<double>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<double>(result).Complete();
        }



        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public OperationResult<double[]> ReadDouble(string address, int length)
        {
            var readResult = Read(address, 8 * length);
            var result = new OperationResult<double[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToDouble(0, length, DataFormat, IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async ValueTask<OperationResult<double[]>> ReadDoubleAsync(string address, int length)
        {
            var readResult = await ReadAsync(address, 8 * length);
            var result = new OperationResult<double[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToDouble(0, length, DataFormat, IsReverse);
            return result.Complete();
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
                    Value = byteArry.ToDouble(0, DataFormat, IsReverse)
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

        public OperationResult<string> ReadString(string address, int length)
        {
            var readResult = Read(address, 4 * length);
            var result = new OperationResult<string>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToString(0, length, encoding: Encoding.ASCII);
            return result.Complete();
        }

        public async ValueTask<OperationResult<string>> ReadStringAsync(string address, int length)
        {
            var readResult = await ReadAsync(address, 4 * length);
            var result = new OperationResult<string>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToString(0, length, encoding: Encoding.ASCII);
            return result.Complete();
        }



        #endregion

        #region Write

        public virtual OperationResult BatchWrite(Dictionary<string, object> addresses)
        {
            throw new NotImplementedException();

        }

        public Task<OperationResult> BatchWriteAsync(Dictionary<string, object> addresses)
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
        internal abstract OperationResult Write(string address, byte[] data, bool isBit = false);

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="data">值</param>
        /// <param name="isBit">值</param>
        /// <returns></returns>
        internal abstract Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false);





        public virtual OperationResult Write(string address, byte[] value) => Write(address, value, false);



        public virtual async Task<OperationResult> WriteAsync(string address, byte[] value) => await WriteAsync(address, value, false);


        public virtual OperationResult Write(string address, byte value)
        {
            return Write(address, new byte[1] { value });
        }

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
        public virtual OperationResult Write(string address, bool value)
        {
            return Write(address, value ? new byte[] { 0x01 } : new byte[] { 0x00 }, true);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async virtual Task<OperationResult> WriteAsync(string address, bool value)
        {
            return await WriteAsync(address, value ? new byte[] { 0x01 } : new byte[] { 0x00 }, true);
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public virtual OperationResult Write(string address, bool[] value)
        {
            return Write(address, value.ToByte(), true);
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public virtual async Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            return await WriteAsync(address, value.ToByte(), true);
        }



        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, short value)
        {
            return Write(address, value.ToByte(IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, short value)
        {
            return await WriteAsync(address, value.ToByte(IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, short[] value)
        {
            return Write(address, value.ToByte(IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, short[] value)
        {
            return await WriteAsync(address, value.ToByte(IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, ushort value)
        {
            return Write(address, value.ToByte(IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, ushort value)
        {
            return await WriteAsync(address, value.ToByte(IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, ushort[] value)
        {
            return Write(address, value.ToByte(IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, ushort[] value)
        {
            return await WriteAsync(address, value.ToByte(IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, int value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, int value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, int[] value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, int[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, uint value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, uint value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, uint[] value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, uint[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, long value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, long value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, long[] value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, long[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, ulong value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, ulong value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }



        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, ulong[] value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, ulong[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, float value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, float value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, float[] value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, float[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, double value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, double value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, double[] value)
        {
            return Write(address, value.ToByte(DataFormat, reverse: IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, double[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, reverse: IsReverse));
        }



        public OperationResult Write(string address, string value)
        {
            return Write(address, value.ToByte(Encoding.ASCII));
        }

        public async Task<OperationResult> WriteAsync(string address, string value)
        {
            return await WriteAsync(address, value.ToByte(Encoding.ASCII));
        }





        #endregion

        #region object类型操作
        public OperationResult<object> Read(DataTypeEnums dataTypeEnum, string address)
        {
            switch (dataTypeEnum)
            {
                case DataTypeEnums.None:
                    return OperationResult.CreateFailedResult<object>("数据类型为null");
                case DataTypeEnums.Bool:
                    return ReadBoolean(address).ToObject();
                case DataTypeEnums.Byte:
                    return ReadByte(address).ToObject();
                case DataTypeEnums.Int16:
                    return ReadInt16(address).ToObject();
                case DataTypeEnums.UInt16:
                    return ReadUInt16(address).ToObject();
                case DataTypeEnums.Int32:
                    return ReadInt32(address).ToObject();
                case DataTypeEnums.UInt32:
                    return ReadUInt32(address).ToObject();
                case DataTypeEnums.Int64:
                    return ReadInt64(address).ToObject();
                case DataTypeEnums.UInt64:
                    return ReadUInt64(address).ToObject();
                case DataTypeEnums.Float:
                    return ReadFloat(address).ToObject();
                case DataTypeEnums.Double:
                    return ReadDouble(address).ToObject();
                case DataTypeEnums.String:
                    return OperationResult.CreateFailedResult<object>("string泛型读取没有实现");
                default:
                    return OperationResult.CreateFailedResult<object>();
            }
        }


        public OperationResult<object[]> Read(DataTypeEnums dataTypeEnum, string address, int length)
        {
            switch (dataTypeEnum)
            {
                case DataTypeEnums.None:
                    return OperationResult.CreateFailedResult<object[]>("数据类型为null");
                case DataTypeEnums.Bool:
                    return ReadBoolean(address, length).ToObject();
                case DataTypeEnums.Byte:
                    return ReadByte(address, length).ToObject();
                case DataTypeEnums.Int16:
                    return ReadInt16(address, length).ToObject();
                case DataTypeEnums.UInt16:
                    return ReadUInt16(address, length).ToObject();
                case DataTypeEnums.Int32:
                    return ReadInt32(address, length).ToObject();
                case DataTypeEnums.UInt32:
                    return ReadUInt32(address, length).ToObject();
                case DataTypeEnums.Int64:
                    return ReadInt64(address, length).ToObject();
                case DataTypeEnums.UInt64:
                    return ReadUInt64(address, length).ToObject();
                case DataTypeEnums.Float:
                    return ReadFloat(address, length).ToObject();
                case DataTypeEnums.Double:
                    return ReadDouble(address, length).ToObject();
                case DataTypeEnums.String:
                    return OperationResult.CreateFailedResult<object[]>("string泛型读取没有实现");
                default:
                    return OperationResult.CreateFailedResult<object[]>();
            }
        }


        public async ValueTask<OperationResult<object>> ReadAsync(DataTypeEnums dataTypeEnum, string address)
        {
            switch (dataTypeEnum)
            {
                case DataTypeEnums.None:
                    return await Task.FromResult(OperationResult.CreateFailedResult<object>("数据类型为null"));
                case DataTypeEnums.Bool:
                    return (await ReadBooleanAsync(address)).ToObject();
                case DataTypeEnums.Byte:
                    return (await ReadByteAsync(address)).ToObject();
                case DataTypeEnums.Int16:
                    return (await ReadInt16Async(address)).ToObject();
                case DataTypeEnums.UInt16:
                    return (await ReadUInt16Async(address)).ToObject();
                case DataTypeEnums.Int32:
                    return (await ReadInt32Async(address)).ToObject();
                case DataTypeEnums.UInt32:
                    return (await ReadUInt32Async(address)).ToObject();
                case DataTypeEnums.Int64:
                    return (await ReadInt64Async(address)).ToObject();
                case DataTypeEnums.UInt64:
                    return (await ReadUInt64Async(address)).ToObject();
                case DataTypeEnums.Float:
                    return (await ReadFloatAsync(address)).ToObject();
                case DataTypeEnums.Double:
                    return (await ReadDoubleAsync(address)).ToObject();
                case DataTypeEnums.String:
                    return await Task.FromResult(OperationResult.CreateFailedResult<object>("string泛型读取没有实现"));
                default:
                    return await Task.FromResult(OperationResult.CreateFailedResult<object>());
            }
        }


        public async ValueTask<OperationResult<object[]>> ReadAsync(DataTypeEnums dataTypeEnum, string address, int length)
        {
            switch (dataTypeEnum)
            {
                case DataTypeEnums.None:
                    return await Task.FromResult(OperationResult.CreateFailedResult<object[]>("数据类型为null"));
                case DataTypeEnums.Bool:
                    return (await ReadBooleanAsync(address, length)).ToObject();
                case DataTypeEnums.Byte:
                    return (await ReadByteAsync(address, length)).ToObject();
                case DataTypeEnums.Int16:
                    return (await ReadInt16Async(address, length)).ToObject();
                case DataTypeEnums.UInt16:
                    return (await ReadUInt16Async(address, length)).ToObject();
                case DataTypeEnums.Int32:
                    return (await ReadInt32Async(address, length)).ToObject();
                case DataTypeEnums.UInt32:
                    return (await ReadUInt32Async(address, length)).ToObject();
                case DataTypeEnums.Int64:
                    return (await ReadInt64Async(address, length)).ToObject();
                case DataTypeEnums.UInt64:
                    return (await ReadUInt64Async(address, length)).ToObject();
                case DataTypeEnums.Float:
                    return (await ReadFloatAsync(address, length)).ToObject();
                case DataTypeEnums.Double:
                    return (await ReadDoubleAsync(address, length)).ToObject();
                case DataTypeEnums.String:
                    return await Task.FromResult(OperationResult.CreateFailedResult<object[]>("string泛型读取没有实现"));
                default:
                    return await Task.FromResult(OperationResult.CreateFailedResult<object[]>());
            }
        }


        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object value)
        {
            switch (dataTypeEnum)
            {
                case DataTypeEnums.None:
                    return OperationResult.CreateFailedResult("数据类型为null");
                case DataTypeEnums.Bool:
                    return Write(address, (bool)value);
                case DataTypeEnums.Byte:
                    return Write(address, (byte)value);
                case DataTypeEnums.Int16:
                    return Write(address, (short)value);
                case DataTypeEnums.UInt16:
                    return Write(address, (int)value);
                case DataTypeEnums.Int32:
                    return Write(address, (long)value);
                case DataTypeEnums.UInt32:
                    return Write(address, (ushort)value);
                case DataTypeEnums.Int64:
                    return Write(address, (uint)value);
                case DataTypeEnums.UInt64:
                    return Write(address, (ulong)value);
                case DataTypeEnums.Float:
                    return Write(address, (float)value);
                case DataTypeEnums.Double:
                    return Write(address, (double)value);
                case DataTypeEnums.String:
                    return OperationResult.CreateFailedResult("string写入未实现");
                default:
                    return OperationResult.CreateFailedResult("数据类型为null");




            }

        }


        public OperationResult Write(DataTypeEnums dataTypeEnum, string address, object[] values)
        {
            switch (dataTypeEnum)
            {
                case DataTypeEnums.None:
                    return OperationResult.CreateFailedResult("数据类型为null");
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
                    return OperationResult.CreateFailedResult("string写入未实现");
                default:
                    return OperationResult.CreateFailedResult("数据类型为null");




            }
        }



        public async Task<OperationResult> WriteAsync(DataTypeEnums dataTypeEnum, string address, object value)
        {
            try
            {
                switch (dataTypeEnum)
                {
                    case DataTypeEnums.None:
                        return OperationResult.CreateFailedResult("数据类型为null");
                    case DataTypeEnums.Bool:
                        return await WriteAsync(address, (bool)value);
                    case DataTypeEnums.Byte:
                        return await WriteAsync(address, (byte)value);
                    case DataTypeEnums.Int16:
                        return await WriteAsync(address, (short)value);
                    case DataTypeEnums.UInt16:
                        return await WriteAsync(address, (int)value);
                    case DataTypeEnums.Int32:
                        return await WriteAsync(address, (long)value);
                    case DataTypeEnums.UInt32:
                        return await WriteAsync(address, (ushort)value);
                    case DataTypeEnums.Int64:
                        return await WriteAsync(address, (uint)value);
                    case DataTypeEnums.UInt64:
                        return await WriteAsync(address, (ulong)value);
                    case DataTypeEnums.Float:
                        return await WriteAsync(address, (float)value);
                    case DataTypeEnums.Double:
                        return await WriteAsync(address, (double)value);
                    case DataTypeEnums.String:
                        return OperationResult.CreateFailedResult("string写入未实现");
                    default:
                        return OperationResult.CreateFailedResult("数据类型为null");

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
                    return OperationResult.CreateFailedResult("数据类型为null");
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
                    return OperationResult.CreateFailedResult("string写入未实现");
                default:
                    return OperationResult.CreateFailedResult("数据类型为null");




            }
        }


        #endregion

    }
}

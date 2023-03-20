using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wombat.IndustrialCommunication.Models;
using Wombat.Infrastructure;

namespace Wombat.IndustrialCommunication.Modbus
{
    public abstract class ModbusBase : BaseModel, IModbusClient
    {




        #region  Read 读取
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="readLength">读取长度</param>
        /// <returns></returns>
        public abstract  OperationResult<byte[]> Read(string address, int readLength = 1, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<short> ReadInt16(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result = ReadInt16(address: address, readLength: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<short>(result) { Value = result.Value[0] }.EndTime();
            else
                return new OperationResult<short>(result).EndTime();
        }

        public OperationResult<short[]> ReadInt16(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, readLength: readLength, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<short[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.TransInt16(0, readLength,IsReverse);
            return result.EndTime();
        }



        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        public OperationResult<short> ReadInt16Bit(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true, bool isPlcAddress = false)
        {
            string[] adds = address.Split('.');
            var readResult = Read(adds[0].Trim(), stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            var result = new OperationResult<short>(readResult);
            if (result.IsSuccess)
            {
                result.Value = BitConverter.ToInt16(readResult.Value, 0);
                if (adds.Length >= 2)
                {
                    var index = int.Parse(adds[1].Trim());
                    var binaryArray = result.Value.TransByte().TransBool(0, 16);
                    if (left)
                    {
                        var length = binaryArray.Length - 16;
                        result.Value = short.Parse(binaryArray[length + index].ToString());
                    }
                    else
                        result.Value = short.Parse(binaryArray[binaryArray.Length - 1 - index].ToString());
                }
            }
            return result.EndTime();
        }

        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<ushort> ReadUInt16(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result = ReadUInt16(address: address, readLength: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<ushort>(result) { Value = result.Value[0] }.EndTime();
            else
                return new OperationResult<ushort>(result).EndTime();
        }

        public OperationResult<ushort[]> ReadUInt16(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, readLength: readLength, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<ushort[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.TransUInt16(0, readLength, IsReverse);
            return result.EndTime();
        }

        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        public OperationResult<ushort> ReadUInt16Bit(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true, bool isPlcAddress = false)
        {
            string[] adds = address.Split('.');
            var readResult = Read(adds[0].Trim(), stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            var result = new OperationResult<ushort>(readResult);
            if (result.IsSuccess)
            {
                result.Value = BitConverter.ToUInt16(readResult.Value, 0);
                if (adds.Length >= 2)
                {
                    var index = int.Parse(adds[1].Trim());
                    var binaryArray = DataConvert.IntToBinaryArray(result.Value, 16);
                    if (left)
                    {
                        var length = binaryArray.Length - 16;
                        result.Value = ushort.Parse(binaryArray[length + index].ToString());
                    }
                    else
                        result.Value = ushort.Parse(binaryArray[binaryArray.Length - 1 - index].ToString());
                }
            }
            return result.EndTime();
        }

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<int> ReadInt32(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result = ReadInt32(address: address, readLength: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<int>(result) { Value = result.Value[0] }.EndTime();
            else
                return new OperationResult<int>(result).EndTime();
        }

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<int[]> ReadInt32(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, (ushort)(2*readLength), stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<int[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.TransInt32(0,length: readLength,format:DataFormat,reverse:IsReverse);
            return result.EndTime();
        }


        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<uint> ReadUInt32(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result = ReadUInt32(address: address, readLength: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<uint>(result) { Value = result.Value[0] }.EndTime();
            else
                return new OperationResult<uint>(result).EndTime();
        }

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<uint[]> ReadUInt32(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, 2* readLength, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<uint[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.TransUInt32(0,length: readLength, format: DataFormat, reverse: IsReverse);
            return result.EndTime();
        }

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<long> ReadInt64(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result = ReadInt64(address: address, readLength: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<long>(result) { Value = result.Value[0] }.EndTime();
            else
                return new OperationResult<long>(result).EndTime();
        }

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<long[]> ReadInt64(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, 4*readLength, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<long[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.TransInt64(0,readLength, format: DataFormat, reverse: IsReverse);
            return result.EndTime();
        }


        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<ulong> ReadUInt64(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result = ReadUInt64(address: address, readLength: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<ulong>(result) { Value = result.Value[0] }.EndTime();
            else
                return new OperationResult<ulong>(result).EndTime();
        }
        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<ulong[]> ReadUInt64(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, 4 * readLength, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<ulong[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.TransUInt64(0, readLength, format: DataFormat, reverse: IsReverse);
            return result.EndTime();
        }

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<float> ReadFloat(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result = ReadFloat(address: address, readLength: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<float>(result) { Value = result.Value[0] }.EndTime();
            else
                return new OperationResult<float>(result).EndTime();
        }

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<float[]> ReadFloat(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, 2*readLength, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<float[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.TransFloat(0,readLength ,format: DataFormat, reverse: IsReverse);
            return result.EndTime();
        }


        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<double> ReadDouble(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result = ReadDouble(address: address, readLength: 1, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<double>(result) { Value = result.Value[0] }.EndTime();
            else
                return new OperationResult<double>(result).EndTime();
        }

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<double[]> ReadDouble(string address, int readLength, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, 4*readLength, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<double[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.TransDouble(0,readLength, format: DataFormat, reverse: IsReverse);
            return result.EndTime();
        }




        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<bool> ReadCoil(string address, byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            var result = new OperationResult<bool>(readResult);
            if (result.IsSuccess)
                result.Value = BitConverter.ToBoolean(readResult.Value, 0);
            return result.EndTime();
        }

        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<bool[]> ReadCoil(string address, int readLength, byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = false)
        {
            var readResult = Read(address: address,readLength: readLength, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.TransBool(0,readLength);
            return result.EndTime();
        }



        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public OperationResult<bool> ReadDiscrete(string address, byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            var result = new OperationResult<bool>(readResult);
            if (result.IsSuccess)
                result.Value = BitConverter.ToBoolean(readResult.Value, 0);
            return result.EndTime();
        }

        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public OperationResult<bool[]> ReadDiscrete(string address, int readLength, byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.TransBool(0, readLength);
            return result.EndTime();
        }



        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<short> ReadInt16(string beginAddress, string address, byte[] values)
        {
            if (!int.TryParse(address?.Trim(), out int addressInt) || !int.TryParse(beginAddress?.Trim(), out int beginAddressInt))
                throw new Exception($"只能是数字，参数address：{address}  beginAddress：{beginAddress}");
            try
            {
                var interval = addressInt - beginAddressInt;
                var byteArry = values.Skip(interval * 2).Take(2).ToArray();
                return new OperationResult<short>
                {
                    Value = byteArry.TransInt16(0,IsReverse)
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



        public OperationResult<short> ReadInt16(int beginAddress, int address, byte[] values)
        {
            return ReadInt16(beginAddress.ToString(), address.ToString(), values);
        }

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<ushort> ReadUInt16(string beginAddress, string address, byte[] values)
        {
            if (!int.TryParse(address?.Trim(), out int addressInt) || !int.TryParse(beginAddress?.Trim(), out int beginAddressInt))
                throw new Exception($"只能是数字，参数address：{address}  beginAddress：{beginAddress}");
            try
            {
                var interval = addressInt - beginAddressInt;
                var byteArry = values.Skip(interval * 2).Take(2).Reverse().ToArray();
                return new OperationResult<ushort>
                {
                    Value = byteArry.TransUInt16(0,IsReverse)
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

        public OperationResult<ushort> ReadUInt16(int beginAddress, int address, byte[] values)
        {
            return ReadUInt16(beginAddress.ToString(), address.ToString(), values);
        }

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<int> ReadInt32(string beginAddress, string address, byte[] values)
        {
            if (!int.TryParse(address?.Trim(), out int addressInt) || !int.TryParse(beginAddress?.Trim(), out int beginAddressInt))
                throw new Exception($"只能是数字，参数address：{address}  beginAddress：{beginAddress}");
            try
            {
                var interval = (addressInt - beginAddressInt) / 2;
                var offset = (addressInt - beginAddressInt) % 2 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 2 + offset).Take(2 * 2).ToArray();
                return new OperationResult<int>
                {
                    Value = byteArry.TransInt32(0,DataFormat,IsReverse)
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

        public OperationResult<int> ReadInt32(int beginAddress, int address, byte[] values)
        {
            return ReadInt32(beginAddress.ToString(), address.ToString(), values);
        }

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<uint> ReadUInt32(string beginAddress, string address, byte[] values)
        {
            if (!int.TryParse(address?.Trim(), out int addressInt) || !int.TryParse(beginAddress?.Trim(), out int beginAddressInt))
                throw new Exception($"只能是数字，参数address：{address}  beginAddress：{beginAddress}");
            try
            {
                var interval = (addressInt - beginAddressInt) / 2;
                var offset = (addressInt - beginAddressInt) % 2 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 2 + offset).Take(2 * 2).ToArray();
                return new OperationResult<uint>
                {
                    Value = byteArry.TransUInt32(0, DataFormat, IsReverse)
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

        public OperationResult<uint> ReadUInt32(int beginAddress, int address, byte[] values)
        {
            return ReadUInt32(beginAddress.ToString(), address.ToString(), values);
        }

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<long> ReadInt64(string beginAddress, string address, byte[] values)
        {
            if (!int.TryParse(address?.Trim(), out int addressInt) || !int.TryParse(beginAddress?.Trim(), out int beginAddressInt))
                throw new Exception($"只能是数字，参数address：{address}  beginAddress：{beginAddress}");
            try
            {
                var interval = (addressInt - beginAddressInt) / 4;
                var offset = (addressInt - beginAddressInt) % 4 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 4 + offset).Take(2 * 4).ToArray();
                return new OperationResult<long>
                {
                    Value = byteArry.TransInt64(0, DataFormat, IsReverse)
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

        public OperationResult<long> ReadInt64(int beginAddress, int address, byte[] values)
        {
            return ReadInt64(beginAddress.ToString(), address.ToString(), values);
        }

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<ulong> ReadUInt64(string beginAddress, string address, byte[] values)
        {
            if (!int.TryParse(address?.Trim(), out int addressInt) || !int.TryParse(beginAddress?.Trim(), out int beginAddressInt))
                throw new Exception($"只能是数字，参数address：{address}  beginAddress：{beginAddress}");
            try
            {
                var interval = (addressInt - beginAddressInt) / 4;
                var offset = (addressInt - beginAddressInt) % 4 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 4 + offset).Take(2 * 4).ToArray();
                return new OperationResult<ulong>
                {
                    Value = byteArry.TransUInt64(0, DataFormat, IsReverse)
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

        public OperationResult<ulong> ReadUInt64(int beginAddress, int address, byte[] values)
        {
            return ReadUInt64(beginAddress.ToString(), address.ToString(), values);
        }

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<float> ReadFloat(string beginAddress, string address, byte[] values)
        {
            if (!int.TryParse(address?.Trim(), out int addressInt) || !int.TryParse(beginAddress?.Trim(), out int beginAddressInt))
                throw new Exception($"只能是数字，参数address：{address}  beginAddress：{beginAddress}");
            try
            {
                var interval = (addressInt - beginAddressInt) / 2;
                var offset = (addressInt - beginAddressInt) % 2 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 2 + offset).Take(2 * 2).ToArray();
                return new OperationResult<float>
                {
                    Value = byteArry.TransFloat(0, DataFormat, IsReverse)
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

        public OperationResult<float> ReadFloat(int beginAddress, int address, byte[] values)
        {
            return ReadFloat(beginAddress.ToString(), address.ToString(), values);
        }

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<double> ReadDouble(string beginAddress, string address, byte[] values)
        {
            if (!int.TryParse(address?.Trim(), out int addressInt) || !int.TryParse(beginAddress?.Trim(), out int beginAddressInt))
                throw new Exception($"只能是数字，参数address：{address}  beginAddress：{beginAddress}");
            try
            {
                var interval = (addressInt - beginAddressInt) / 4;
                var offset = (addressInt - beginAddressInt) % 4 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 4 + offset).Take(2 * 4).ToArray();
                return new OperationResult<double>
                {
                    Value = byteArry.TransDouble(0, DataFormat, IsReverse)
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

        public OperationResult<double> ReadDouble(int beginAddress, int address, byte[] values)
        {
            return ReadDouble(beginAddress.ToString(), address.ToString(), values);
        }

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<bool> ReadCoil(string beginAddress, string address, byte[] values)
        {
            if (!int.TryParse(address?.Trim(), out int addressInt) || !int.TryParse(beginAddress?.Trim(), out int beginAddressInt))
                throw new Exception($"只能是数字，参数address：{address}  beginAddress：{beginAddress}");
            try
            {
                var interval = addressInt - beginAddressInt;
                var index = (interval + 1) % 8 == 0 ? (interval + 1) / 8 : (interval + 1) / 8 + 1;
                var binaryArray = Convert.ToInt32(values[index - 1]).IntToBinaryArray().ToArray().Reverse().ToArray();
                var isBit = false;
                if ((index - 1) * 8 + binaryArray.Length > interval)
                    isBit = binaryArray[interval - (index - 1) * 8].ToString() == 1.ToString();
                return new OperationResult<bool>()
                {
                    Value = isBit
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

        public OperationResult<bool> ReadCoil(int beginAddress, int address, byte[] values)
        {
            return ReadCoil(beginAddress.ToString(), address.ToString(), values);
        }

        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<bool> ReadDiscrete(string beginAddress, string address, byte[] values)
        {
            if (!int.TryParse(address?.Trim(), out int addressInt) || !int.TryParse(beginAddress?.Trim(), out int beginAddressInt))
                throw new Exception($"只能是数字，参数address：{address}  beginAddress：{beginAddress}");
            try
            {
                var interval = addressInt - beginAddressInt;
                var index = (interval + 1) % 8 == 0 ? (interval + 1) / 8 : (interval + 1) / 8 + 1;
                var binaryArray = Convert.ToInt32(values[index - 1]).IntToBinaryArray().ToArray().Reverse().ToArray();
                var isBit = false;
                if ((index - 1) * 8 + binaryArray.Length > interval)
                    isBit = binaryArray[interval - (index - 1) * 8].ToString() == 1.ToString();
                return new OperationResult<bool>()
                {
                    Value = isBit
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

        public OperationResult<bool> ReadDiscrete(int beginAddress, int address, byte[] values)
        {
            return ReadDiscrete(beginAddress.ToString(), address.ToString(), values);
        }

        /// <summary>
        /// 分批读取（批量读取，内部进行批量计算读取）
        /// </summary>
        /// <param name="addresses"></param>
        /// <returns></returns>
        private OperationResult<List<ModbusOutput>> BatchRead(List<ModbusInput> addresses)
        {
            var result = new OperationResult<List<ModbusOutput>>();
            result.Value = new List<ModbusOutput>();
            var functionCodes = addresses.Select(t => t.FunctionCode).Distinct();
            foreach (var functionCode in functionCodes)
            {
                var stationNumbers = addresses.Where(t => t.FunctionCode == functionCode).Select(t => t.StationNumber).Distinct();
                foreach (var stationNumber in stationNumbers)
                {
                    var addressList = addresses.Where(t => t.FunctionCode == functionCode && t.StationNumber == stationNumber)
                        .DistinctBy(t => t.Address)
                        .ToDictionary(t => t.Address, t => t.DataType);
                    var tempOperationResult = BatchRead(addressList, stationNumber, functionCode);
                    if (tempOperationResult.IsSuccess)
                    {
                        foreach (var item in tempOperationResult.Value)
                        {
                            result.Value.Add(new ModbusOutput()
                            {
                                Address = item.Key,
                                FunctionCode = functionCode,
                                StationNumber = stationNumber,
                                Value = item.Value
                            });
                        }
                    }
                    else
                    {
                        result.SetInfo(tempOperationResult);
                    }
                    result.Requst = tempOperationResult.Requst;
                    result.Response = tempOperationResult.Response;
                }
            }
            return result.EndTime();
        }

        private OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnum> addressList, byte stationNumber, byte functionCode)
        {
            var result = new OperationResult<Dictionary<string, object>>();
            result.Value = new Dictionary<string, object>();

            var addresses = addressList.Select(t => new KeyValuePair<int, DataTypeEnum>(int.Parse(t.Key), t.Value)).ToList();

            var minAddress = addresses.Select(t => t.Key).Min();
            var maxAddress = addresses.Select(t => t.Key).Max();
            while (maxAddress >= minAddress)
            {
                int readLength = 121;//125 - 4 = 121

                var tempAddress = addresses.Where(t => t.Key >= minAddress && t.Key <= minAddress + readLength).ToList();
                //如果范围内没有数据。按正确逻辑不存在这种情况。
                if (!tempAddress.Any())
                {
                    minAddress = minAddress + readLength;
                    continue;
                }

                var tempMax = tempAddress.OrderByDescending(t => t.Key).FirstOrDefault();
                switch (tempMax.Value)
                {
                    case DataTypeEnum.Bool:
                    case DataTypeEnum.Byte:
                    case DataTypeEnum.Int16:
                    case DataTypeEnum.UInt16:
                        readLength = tempMax.Key + 1 - minAddress;
                        break;
                    case DataTypeEnum.Int32:
                    case DataTypeEnum.UInt32:
                    case DataTypeEnum.Float:
                        readLength = tempMax.Key + 2 - minAddress;
                        break;
                    case DataTypeEnum.Int64:
                    case DataTypeEnum.UInt64:
                    case DataTypeEnum.Double:
                        readLength = tempMax.Key + 4 - minAddress;
                        break;
                    default:
                        throw new Exception("Message BatchRead 未定义类型 -1");
                }

                var tempOperationResult = Read(minAddress.ToString(), Convert.ToUInt16(readLength), stationNumber: stationNumber, functionCode: functionCode);

                result.Requst = tempOperationResult.Requst;
                result.Response = tempOperationResult.Response;
                if (!tempOperationResult.IsSuccess)
                {
                    result.IsSuccess = tempOperationResult.IsSuccess;
                    result.Exception = tempOperationResult.Exception;
                    result.ErrorCode = tempOperationResult.ErrorCode;
                    result.Message = $"读取 地址:{minAddress} 站号:{stationNumber} 功能码:{functionCode} 失败。{tempOperationResult.Message}";
                    result.AddMessage2List();
                    return result.EndTime();
                }

                var rValue = tempOperationResult.Value.Reverse().ToArray();
                foreach (var item in tempAddress)
                {
                    object tempVaue = null;

                    switch (item.Value)
                    {
                        case DataTypeEnum.Bool:
                            tempVaue = ReadCoil(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnum.Byte:
                            throw new Exception("Message BatchRead 未定义类型 -2");
                        case DataTypeEnum.Int16:
                            tempVaue = ReadInt16(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnum.UInt16:
                            tempVaue = ReadUInt16(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnum.Int32:
                            tempVaue = ReadInt32(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnum.UInt32:
                            tempVaue = ReadUInt32(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnum.Int64:
                            tempVaue = ReadInt64(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnum.UInt64:
                            tempVaue = ReadUInt64(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnum.Float:
                            tempVaue = ReadFloat(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnum.Double:
                            tempVaue = ReadDouble(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        default:
                            throw new Exception("Message BatchRead 未定义类型 -3");
                    }

                    result.Value.Add(item.Key.ToString(), tempVaue);
                }
                minAddress = minAddress + readLength;

                if (addresses.Any(t => t.Key >= minAddress))
                    minAddress = addresses.Where(t => t.Key >= minAddress).OrderBy(t => t.Key).FirstOrDefault().Key;
                else
                    return result.EndTime();
            }
            return result.EndTime();
        }

        /// <summary>
        /// 分批读取
        /// </summary>
        /// <param name="addresses"></param>
        /// <param name="retryCount">如果读取异常，重试次数</param>
        /// <returns></returns>
        public OperationResult<List<ModbusOutput>> BatchRead(List<ModbusInput> addresses, uint retryCount = 1)
        {
            var result = BatchRead(addresses);
            for (int i = 0; i < retryCount; i++)
            {
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    result = BatchRead(addresses);
                }
                else
                    break;
            }
            return result;
        }


        #endregion

        #region Write 写入
        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        public abstract OperationResult Write(string address, bool value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = false);


        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        public abstract OperationResult Write(string address, bool[] value, byte stationNumber = 1, byte functionCode = 15, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public abstract OperationResult Write(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        //public abstract OperationResult Write(string address, byte values, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false);



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public virtual OperationResult Write(string address, byte value, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false)
        {
            return Write(address,new byte[1] { value }, stationNumber, functionCode, isPlcAddress);
        }



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, short value, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false)
        {
            var values = value.TransByte(IsReverse);
            return Write(address, values, stationNumber, functionCode,isPlcAddress);
        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, short[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(IsReverse);
            return Write(address, values, stationNumber, functionCode,isPlcAddress);
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, ushort value, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false)
        {
            var values = value.TransByte(IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, ushort[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, int value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat,IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, int[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat,IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, uint value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, uint[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, long value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, long[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, ulong value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, ulong[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, float value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, float[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, double value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public OperationResult Write(string address, double[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.TransByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }


        #endregion

    }
}

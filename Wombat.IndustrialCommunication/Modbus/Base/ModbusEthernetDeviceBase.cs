using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;


namespace Wombat.IndustrialCommunication.Modbus
{
    public abstract class ModbusEthernetDeviceBase : EthernetDeviceBase, IModbusReadWrite
    {

        #region  Read 读取
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="length">读取长度</param>
        /// <returns></returns>
        public abstract  OperationResult<byte[]> Read(string address, int length = 1, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<short> ReadInt16(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result = ReadInt16(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<short>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<short>(result).Complete();
        }

        public OperationResult<short[]> ReadInt16(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, length: length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<short[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt16(0, length,IsReverse);
            return result.Complete();
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
                    var binaryArray = result.Value.ToByte().ToBool(0, 16);
                    if (left)
                    {
                        var length = binaryArray.Length - 16;
                        result.Value = short.Parse(binaryArray[length + index].ToString());
                    }
                    else
                        result.Value = short.Parse(binaryArray[binaryArray.Length - 1 - index].ToString());
                }
            }
            return result.Complete();
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
            var result = ReadUInt16(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<ushort>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<ushort>(result).Complete();
        }

        public OperationResult<ushort[]> ReadUInt16(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, length: length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<ushort[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt16(0, length,IsReverse);
            return result.Complete();
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
                    var binaryArray = DataTypeExtensions.IntToBinaryArray(result.Value, 16);
                    if (left)
                    {
                        var length = binaryArray.Length - 16;
                        result.Value = ushort.Parse(binaryArray[length + index].ToString());
                    }
                    else
                        result.Value = ushort.Parse(binaryArray[binaryArray.Length - 1 - index].ToString());
                }
            }
            return result.Complete();
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
            var result = ReadInt32(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<int>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<int>(result).Complete();
        }

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<int[]> ReadInt32(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, (ushort)(2*length), stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<int[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt32(0,length: length,format:DataFormat, IsReverse);
            return result.Complete();
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
            var result = ReadUInt32(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<uint>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<uint>(result).Complete();
        }

        /// <summary>
        /// 读取UInt32
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<uint[]> ReadUInt32(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, 2* length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<uint[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt32(0,length: length, format: DataFormat, IsReverse);
            return result.Complete();
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
            var result = ReadInt64(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<long>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<long>(result).Complete();
        }

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<long[]> ReadInt64(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, 4*length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<long[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt64(0,length, format: DataFormat, IsReverse);
            return result.Complete();
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
            var result = ReadUInt64(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<ulong>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<ulong>(result).Complete();
        }
        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<ulong[]> ReadUInt64(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, 4 * length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<ulong[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt64(0, length, format: DataFormat, IsReverse);
            return result.Complete();
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
            var result = ReadFloat(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode,isPlcAddress:isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<float>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<float>(result).Complete();
        }

        /// <summary>
        /// 读取Float
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<float[]> ReadFloat(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, 2*length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<float[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToFloat(0,length ,format: DataFormat, IsReverse);
            return result.Complete();
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
            var result = ReadDouble(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<double>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<double>(result).Complete();
        }

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<double[]> ReadDouble(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, 4*length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<double[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToDouble(0,length, format: DataFormat, IsReverse);
            return result.Complete();
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
            return result.Complete();
        }

        /// <summary>
        /// 读取线圈
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public OperationResult<bool[]> ReadCoil(string address, int length, byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = false)
        {
            var readResult = Read(address: address,length: length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0,length, IsReverse);
            return result.Complete();
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
            return result.Complete();
        }

        /// <summary>
        /// 读取离散
        /// </summary>
        /// <param name="address"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public OperationResult<bool[]> ReadDiscrete(string address, int length, byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = false)
        {
            var readResult = Read(address: address, length: length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, length, IsReverse);
            return result.Complete();
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
                    Value = byteArry.ToInt16(0)
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
                    Value = byteArry.ToUInt16(0)
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
                    Value = byteArry.ToInt32(0,DataFormat)
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
                    Value = byteArry.ToDouble(0, DataFormat)
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
                    result.Requsts = tempOperationResult.Requsts;
                    result.Responses = tempOperationResult.Responses;
                }
            }
            return result.Complete();
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
                int length = 121;//125 - 4 = 121

                var tempAddress = addresses.Where(t => t.Key >= minAddress && t.Key <= minAddress + length).ToList();
                //如果范围内没有数据。按正确逻辑不存在这种情况。
                if (!tempAddress.Any())
                {
                    minAddress = minAddress + length;
                    continue;
                }

                var tempMax = tempAddress.OrderByDescending(t => t.Key).FirstOrDefault();
                switch (tempMax.Value)
                {
                    case DataTypeEnum.Bool:
                    case DataTypeEnum.Byte:
                    case DataTypeEnum.Int16:
                    case DataTypeEnum.UInt16:
                        length = tempMax.Key + 1 - minAddress;
                        break;
                    case DataTypeEnum.Int32:
                    case DataTypeEnum.UInt32:
                    case DataTypeEnum.Float:
                        length = tempMax.Key + 2 - minAddress;
                        break;
                    case DataTypeEnum.Int64:
                    case DataTypeEnum.UInt64:
                    case DataTypeEnum.Double:
                        length = tempMax.Key + 4 - minAddress;
                        break;
                    default:
                        throw new Exception("Message BatchRead 未定义类型 -1");
                }

                var tempOperationResult = Read(minAddress.ToString(), Convert.ToUInt16(length), stationNumber: stationNumber, functionCode: functionCode);

                result.Requsts = tempOperationResult.Requsts;
                result.Responses = tempOperationResult.Responses;
                if (!tempOperationResult.IsSuccess)
                {
                    result.IsSuccess = tempOperationResult.IsSuccess;
                    result.Exception = tempOperationResult.Exception;
                    result.ErrorCode = tempOperationResult.ErrorCode;
                    result.Message = $"读取 地址:{minAddress} 站号:{stationNumber} 功能码:{functionCode} 失败。{tempOperationResult.Message}";
                    return result.Complete();
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
                minAddress = minAddress + length;

                if (addresses.Any(t => t.Key >= minAddress))
                    minAddress = addresses.Where(t => t.Key >= minAddress).OrderBy(t => t.Key).FirstOrDefault().Key;
                else
                    return result.Complete();
            }
            return result.Complete();
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

        #region ReadAsync
        public abstract Task<OperationResult<byte[]>> ReadAsync(string address, int length = 1, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false);

        public async Task<OperationResult<short>> ReadInt16Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result =await ReadInt16Async(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<short>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<short>(result).Complete();
        }

        public async Task<OperationResult<short[]>> ReadInt16Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, length: length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<short[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt16(0, length, IsReverse);
            return result.Complete();
        }

        public async Task<OperationResult<short>> ReadInt16BitAsync(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true, bool isPlcAddress = false)
        {
            string[] adds = address.Split('.');
            var readResult =await ReadAsync(adds[0].Trim(), stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<short>(readResult);
            if (result.IsSuccess)
            {
                result.Value = BitConverter.ToInt16(readResult.Value, 0);
                if (adds.Length >= 2)
                {
                    var index = int.Parse(adds[1].Trim());
                    var binaryArray = result.Value.ToByte().ToBool(0, 16);
                    if (left)
                    {
                        var length = binaryArray.Length - 16;
                        result.Value = short.Parse(binaryArray[length + index].ToString());
                    }
                    else
                        result.Value = short.Parse(binaryArray[binaryArray.Length - 1 - index].ToString());
                }
            }
            return result.Complete();
        }

        public async Task<OperationResult<ushort[]>> ReadUInt16Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, length: length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<ushort[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt16(0, length, IsReverse);
            return result.Complete();
        }

        public async Task<OperationResult<ushort>> ReadUInt16Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result =await ReadUInt16Async(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<ushort>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<ushort>(result).Complete();
        }

        public async Task<OperationResult<ushort>> ReadUInt16BitAsync(string address, byte stationNumber = 1, byte functionCode = 3, bool left = true, bool isPlcAddress = false)
        {
            string[] adds = address.Split('.');
            var readResult =await ReadAsync(adds[0].Trim(), stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<ushort>(readResult);
            if (result.IsSuccess)
            {
                result.Value = BitConverter.ToUInt16(readResult.Value, 0);
                if (adds.Length >= 2)
                {
                    var index = int.Parse(adds[1].Trim());
                    var binaryArray = DataTypeExtensions.IntToBinaryArray(result.Value, 16);
                    if (left)
                    {
                        var length = binaryArray.Length - 16;
                        result.Value = ushort.Parse(binaryArray[length + index].ToString());
                    }
                    else
                        result.Value = ushort.Parse(binaryArray[binaryArray.Length - 1 - index].ToString());
                }
            }
            return result.Complete();
        }

        public async Task<OperationResult<int>> ReadInt32Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result =await ReadInt32Async(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<int>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<int>(result).Complete();
        }

        public async Task<OperationResult<int[]>> ReadInt32Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, (ushort)(2 * length), stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<int[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt32(0, length: length, format: DataFormat, IsReverse);
            return result.Complete();
        }

        public async Task<OperationResult<uint>> ReadUInt32Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result = await ReadUInt32Async(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<uint>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<uint>(result).Complete();
        }

        public async Task<OperationResult<uint[]>> ReadUInt32Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, 2 * length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<uint[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt32(0, length: length, format: DataFormat, IsReverse);
            return result.Complete();
        }

        public async Task<OperationResult<long>> ReadInt64Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result =await ReadInt64Async(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<long>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<long>(result).Complete();
        }

        public async Task<OperationResult<long[]>> ReadInt64Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, 4 * length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<long[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt64(0, length, format: DataFormat, IsReverse);
            return result.Complete();
        }


        public async Task<OperationResult<ulong>> ReadUInt64Async(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result =await ReadUInt64Async(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<ulong>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<ulong>(result).Complete();
        }

        public async Task<OperationResult<ulong[]>> ReadUInt64Async(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, 4 * length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<ulong[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt64(0, length, format: DataFormat, IsReverse);
            return result.Complete();
        }


        public async Task<OperationResult<float>> ReadFloatAsync(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result =await ReadFloatAsync(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<float>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<float>(result).Complete();
        }

        public async Task<OperationResult<float[]>> ReadFloatAsync(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, 2 * length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<float[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToFloat(0, length, format: DataFormat, IsReverse);
            return result.Complete();
        }


        public async Task<OperationResult<double>> ReadDoubleAsync(string address, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var result =await ReadDoubleAsync(address: address, length: 1, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            if (result.IsSuccess)
                return new OperationResult<double>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<double>(result).Complete();
        }

        public async Task<OperationResult<double[]>> ReadDoubleAsync(string address, int length, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, 4 * length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<double[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToDouble(0, length, format: DataFormat, IsReverse);
            return result.Complete();
        }


        public async Task<OperationResult<bool>> ReadCoilAsync(string address, byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<bool>(readResult);
            if (result.IsSuccess)
                result.Value = BitConverter.ToBoolean(readResult.Value, 0);
            return result.Complete();
        }

        public async Task<OperationResult<bool[]>> ReadCoilAsync(string address, int length, byte stationNumber = 1, byte functionCode = 1, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, length: length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, length, IsReverse);
            return result.Complete();
        }


        public async Task<OperationResult<bool>> ReadDiscreteAsync(string address, byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = false)
        {
            var readResult =await ReadAsync(address: address, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<bool>(readResult);
            if (result.IsSuccess)
                result.Value = BitConverter.ToBoolean(readResult.Value, 0);
            return result.Complete();
        }

        public async Task<OperationResult<bool[]>> ReadDiscreteAsync(string address, int length, byte stationNumber = 1, byte functionCode = 2, bool isPlcAddress = false)
        {
            var readResult = await ReadAsync(address: address, length: length, stationNumber: stationNumber, functionCode: functionCode, isPlcAddress: isPlcAddress);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, length, IsReverse);
            return result.Complete();
        }

        public Task<OperationResult<List<ModbusOutput>>> BatchReadAsync(List<ModbusInput> addresses, uint retryCount = 1)
        {
            throw new NotImplementedException();
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
            return Write(address,new byte[2] { 0x00,value }, stationNumber, functionCode, isPlcAddress);
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
            var values = value.ToByte(IsReverse);
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
            var values = value.ToByte(IsReverse);
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
            var values = value.ToByte(IsReverse);
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
            var values = value.ToByte(IsReverse);
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
            var values = value.ToByte(DataFormat, IsReverse);
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
            var values = value.ToByte(DataFormat, IsReverse);
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
            var values = value.ToByte(DataFormat, IsReverse);
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
            var values = value.ToByte(DataFormat, IsReverse);
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
            var values = value.ToByte(DataFormat,IsReverse);
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
            var values = value.ToByte(DataFormat,IsReverse);
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
            var values = value.ToByte(DataFormat, IsReverse);
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
            var values = value.ToByte(DataFormat, IsReverse);
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
            var values = value.ToByte(DataFormat, IsReverse);
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
            var values = value.ToByte(DataFormat, IsReverse);
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
            var values = value.ToByte(DataFormat, IsReverse);
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
            var values = value.ToByte(DataFormat, IsReverse);
            return Write(address, values, stationNumber, functionCode, isPlcAddress);
        }


        #endregion

        #region WriteAsync 写入
        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        public abstract Task<OperationResult> WriteAsync(string address, bool value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = false);


        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        public abstract Task<OperationResult> WriteAsync(string address, bool[] value, byte stationNumber = 1, byte functionCode = 15, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public abstract  Task<OperationResult> WriteAsync(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false);

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        //public abstract async Task<OperationResult> WriteAsync(string address, byte values, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false);



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public virtual async Task<OperationResult> WriteAsync(string address, byte value, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false)
        {
            return await WriteAsync(address, new byte[2] { 0x00, value }, stationNumber, functionCode, isPlcAddress);
        }



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, short value, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false)
        {
            var values = value.ToByte(IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, short[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, ushort value, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false)
        {
            var values = value.ToByte(IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, ushort[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, int value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, int[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, uint value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, uint[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, long value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, long[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, ulong value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, ulong[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, float value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }
        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, float[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, double value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public async Task<OperationResult> WriteAsync(string address, double[] value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            var values = value.ToByte(DataFormat, IsReverse);
            return await WriteAsync(address, values, stationNumber, functionCode, isPlcAddress);
        }


        #endregion



    }
}

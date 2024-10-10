using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    public abstract class ModbusClient : DeviceClient, IDeviceClient
    {
        internal AsyncLock _lock = new AsyncLock();

        #region 从内存中提取读取值


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


        /// <summary>
        /// 从批量读取的数据字节提取对应的地址数据
        /// </summary>
        /// <param name="beginAddress">批量读取的起始地址</param>
        /// <param name="address">读取地址</param>
        /// <param name="values">批量读取的值</param>
        /// <returns></returns>
        public OperationResult<bool> ReadBoolean(string beginAddress, string address, byte[] values)
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



        #endregion

    }
}

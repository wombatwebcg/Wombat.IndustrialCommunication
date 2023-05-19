using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wombat.Infrastructure;
using Wombat.ObjectConversionExtention;

namespace Wombat.IndustrialCommunication.PLC
{
   public abstract class IEthernetClientBase: BaseModel, IEthernetClient
    {
        public  IPEndPoint IpEndPoint { get; set; }

        public abstract  string Version { get; }


        /// <summary>
        /// 分批缓冲区大小
        /// </summary>
        protected const int BufferSize = 4096;



        /// <summary>
        /// Socket读取
        /// </summary>
        /// <param name="socket">socket</param>
        /// <param name="receiveCount">读取长度</param>          
        /// <returns></returns>
        public virtual OperationResult<byte[]> SocketRead(Socket socket, int receiveCount)
        {
            var result = new OperationResult<byte[]>();
            if (receiveCount < 0)
            {
                result.IsSuccess = false;
                result.Message = $"读取长度[receiveCount]为{receiveCount}";
                
                return result;
            }

            byte[] receiveBytes = new byte[receiveCount];
            int receiveFinish = 0;
            while (receiveFinish < receiveCount)
            {
                // 分批读取
                int receiveLength = (receiveCount - receiveFinish) >= BufferSize ? BufferSize : (receiveCount - receiveFinish);
                try
                {
                    var readLeng = socket.Receive(receiveBytes, receiveFinish, receiveLength, SocketFlags.None);
                    if (readLeng == 0)
                    {
                        socket?.SafeClose();
                        result.IsSuccess = false;
                        result.Message = $"连接被断开";
                        
                        return result;
                    }
                    receiveFinish += readLeng;
                }
                catch (SocketException ex)
                {
                    socket?.SafeClose();
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                        result.Message = $"连接超时：{ex.Message}";
                    else
                        result.Message = $"连接被断开，{ex.Message}";
                    result.IsSuccess = false;
                    
                    result.Exception = ex;
                    return result;
                }
            }
            result.Value = receiveBytes;
            return result.Complete();
        }


        /// <summary>
        /// 发送报文，并获取响应报文（如果网络异常，会自动进行一次重试）
        /// TODO 重试机制应改成用户主动设置
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override OperationResult<byte[]> SendPackageReliable(byte[] command)
        {
            try
            {
                var result = SendPackageSingle(command);
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    var conentOperationResult = Connect();
                    if (!conentOperationResult.IsSuccess)
                    {
                        return new OperationResult<byte[]>(conentOperationResult);

                    }
                    else
                    {
                        result = SendPackageSingle(command); ;
                        return result.Complete();
                    }
                }
                else
                {
                    return result.Complete();

                }
            }
            catch (Exception ex)
            {
                try
                {
                    WarningLog?.Invoke(ex.Message, ex);
                    //如果出现异常，则进行一次重试                
                    var conentOperationResult = Connect();
                    if (!conentOperationResult.IsSuccess)
                    {
                        return new OperationResult<byte[]>(conentOperationResult);
                    }
                    else
                    {
                      var  result = SendPackageSingle(command); ;
                        return result.Complete();
                    }
                }
                catch (Exception ex2)
                {
                    var result = new OperationResult<byte[]>();
                    result.IsSuccess = false;
                    result.Message = ex2.Message;                   
                    return result.Complete();
                }
            }
        }




        /// <summary>
        /// 发送报文，并获取响应报文（如果网络异常，会自动进行一次重试）
        /// TODO 重试机制应改成用户主动设置
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override async Task<OperationResult<byte[]>> SendPackageReliableAsync(byte[] command)
        {
            try
            {
                var result = await SendPackageSingleAsync(command);
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    var conentOperationResult = Connect();
                    if (!conentOperationResult.IsSuccess)
                    {
                        return new OperationResult<byte[]>(conentOperationResult);

                    }
                    else
                    {
                        result =await SendPackageSingleAsync(command); ;
                        return result.Complete();
                    }
                }
                else
                {
                    return result.Complete();

                }
            }
            catch (Exception ex)
            {
                try
                {
                    WarningLog?.Invoke(ex.Message, ex);
                    //如果出现异常，则进行一次重试                
                    var conentOperationResult = Connect();
                    if (!conentOperationResult.IsSuccess)
                    {
                        return new OperationResult<byte[]>(conentOperationResult);
                    }
                    else
                    {
                        var result = await SendPackageSingleAsync(command); 
                        return result.Complete();
                    }
                }
                catch (Exception ex2)
                {
                    var result = new OperationResult<byte[]>();
                    result.IsSuccess = false;
                    result.Message = ex2.Message;
                    return result.Complete();
                }
            }
        }


        #region Read


        public virtual OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnum> addresses)
        {
            throw new NotImplementedException();
        }

        public virtual Task<OperationResult<Dictionary<string, object>>> BatchReadAsync(Dictionary<string, DataTypeEnum> addresses)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>
        /// <param name="setEndian">返回值是否设置大小端</param>
        /// <returns></returns>
        public abstract OperationResult<byte[]> Read(string address, int length, bool isBit = false);


        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>
        /// <param name="setEndian">返回值是否设置大小端</param>
        /// <returns></returns>
        public  abstract Task<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false);


        /// <summary>
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public virtual OperationResult<bool> ReadBoolean(string address)
        {
            var result = ReadBoolean(address, 1);
            if(result.IsSuccess)
                return new OperationResult<bool>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<bool>(result).Complete();
        }


        public virtual async Task<OperationResult<bool>> ReadBooleanAsync(string address)
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
        public virtual OperationResult<bool[]> ReadBoolean(string address,int length)
        {
            //int reallength = (int)Math.Ceiling(length*1.0 /8);
            var readResult = Read(address, length, isBit: true);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, length, IsReverse);
            return result.Complete();
        }

        public virtual async Task<OperationResult<bool[]>> ReadBooleanAsync(string address, int length)
        {
            //int reallength = (int)Math.Ceiling(length*1.0 /8);
           var readResult = await ReadAsync(address, length, isBit: true);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, length, IsReverse);
            return result.Complete();
        }

        public OperationResult<bool> ReadBoolean(int startAddressInt, int addressInt, byte[] values)
        {
            try
            {
                var interval = addressInt - startAddressInt;
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
        public async Task<OperationResult<short>> ReadInt16Async(string address)
        {
            var result =await ReadInt16Async(address, 1);
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
        public OperationResult<short[]> ReadInt16(string address,int length)
        {
            var readResult = Read(address, 2*length);
            var result = new OperationResult<short[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt16(0,length ,IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取Int16
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public async Task<OperationResult<short[]>> ReadInt16Async(string address, int length)
        {
            var readResult =await ReadAsync(address, 2 * length);
            var result = new OperationResult<short[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt16(0, length, IsReverse);
            return result.Complete();
        }


        public OperationResult<short> ReadInt16(int startAddressInt, int addressInt, byte[] values)
        {
            try
            {
                var interval = addressInt - startAddressInt;
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
            if(result.IsSuccess)
                return new OperationResult<ushort>(result) { Value = result.Value[0] }.Complete();
            else
                return new OperationResult<ushort>(result).Complete();
        }

        /// <summary>
        /// 读取UInt16
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async Task<OperationResult<ushort>> ReadUInt16Async(string address)
        {
            var result =await ReadUInt16Async(address, 1);
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
        public async Task<OperationResult<ushort[]>> ReadUInt16Async(string address, int length)
        {
            var readResult =await ReadAsync(address, 2 * length);
            var result = new OperationResult<ushort[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt16(0, length, IsReverse);
            return result.Complete();
        }


        public OperationResult<ushort> ReadUInt16(int startAddressInt, int addressInt, byte[] values)
        {
            try
            {
                var interval = addressInt - startAddressInt;
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
                return new OperationResult<int>(result) {Value = result.Value[0] }.Complete();
            else
                return new OperationResult<int>(result).Complete();

        }

        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async Task<OperationResult<int>> ReadInt32Async(string address)
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
        public OperationResult<int[]> ReadInt32(string address,int length)
        {
            var readResult = Read(address, 4*length);
            var result = new OperationResult<int[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt32(0,length ,DataFormat, IsReverse);
            return result.Complete();
        }


        /// <summary>
        /// 读取Int32
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async Task<OperationResult<int[]>> ReadInt32Async(string address, int length)
        {
            var readResult =await ReadAsync(address, 4 * length);
            var result = new OperationResult<int[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt32(0, length, DataFormat, IsReverse);
            return result.Complete();
        }

        public OperationResult<int> ReadInt32(int startAddressInt, int addressInt, byte[] values)
        {
            try
            {
                var interval = (addressInt - startAddressInt) / 2;
                var offset = (addressInt - startAddressInt) % 2 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 2 + offset).Take(2 * 2).ToArray();
                return new OperationResult<int>
                {
                    Value = byteArry.ToInt32(0, DataFormat, IsReverse)
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
        public async Task<OperationResult<uint>> ReadUInt32Async(string address)
        {
            var result =await ReadUInt32Async(address, 1);
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
        public OperationResult<uint[]> ReadUInt32(string address,int length)
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
        public async Task<OperationResult<uint[]>> ReadUInt32Async(string address, int length)
        {
            var readResult =await ReadAsync(address, 4 * length);
            var result = new OperationResult<uint[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt32(0, length, DataFormat, IsReverse);
            return result.Complete();
        }


        public OperationResult<uint> ReadUInt32(int startAddressInt, int addressInt, byte[] values)
        {
            try
            {
                var interval = (addressInt - startAddressInt) / 2;
                var offset = (addressInt - startAddressInt) % 2 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 2 + offset).Take(2 * 2).ToArray();
                return new OperationResult<uint>
                {
                    Value = byteArry.ToUInt32(0,  DataFormat, IsReverse)
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
        public async Task<OperationResult<long>> ReadInt64Async(string address)
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
        public OperationResult<long[]> ReadInt64(string address,int length)
        {
            var readResult = Read(address, 8*length);
            var result = new OperationResult<long[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt64(0,length ,DataFormat, IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取Int64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async Task<OperationResult<long[]>> ReadInt64Async(string address, int length)
        {
            var readResult =await ReadAsync(address, 8 * length);
            var result = new OperationResult<long[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToInt64(0, length, DataFormat, IsReverse);
            return result.Complete();
        }


        public OperationResult<long> ReadInt64(int startAddressInt, int addressInt, byte[] values)
        {
            try
            {
                var interval = (addressInt - startAddressInt) / 4;
                var offset = (addressInt - startAddressInt) % 4 * 2;
                var byteArry = values.Skip(interval * 2 * 4 + offset).Take(2 * 4).ToArray();
                return new OperationResult<long>
                {
                    Value = byteArry.ToInt64(0, DataFormat, IsReverse)
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
        public async Task<OperationResult<ulong>> ReadUInt64Async(string address)
        {
            var result =await ReadUInt64Async(address, 1);
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
        public OperationResult<ulong[]> ReadUInt64(string address,int length)
        {
            var readResult = Read(address, 8*length);
            var result = new OperationResult<ulong[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt64(0,length ,DataFormat, IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取UInt64
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async Task<OperationResult<ulong[]>> ReadUInt64Async(string address, int length)
        {
            var readResult = await ReadAsync(address, 8 * length);
            var result = new OperationResult<ulong[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToUInt64(0, length, DataFormat, IsReverse);
            return result.Complete();
        }


        public OperationResult<ulong> ReadUInt64(int startAddressInt, int addressInt, byte[] values)
        {
            try
            {
                var interval = (addressInt - startAddressInt) / 4;
                var offset = (addressInt - startAddressInt) % 4 * 2;
                var byteArry = values.Skip(interval * 2 * 4 + offset).Take(2 * 4).ToArray();
                return new OperationResult<ulong>
                {
                    Value = byteArry.ToUInt64(0, DataFormat, IsReverse)
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
        public async  Task<OperationResult<float>> ReadFloatAsync(string address)
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
        public OperationResult<float[]> ReadFloat(string address,int length)
        {
            var readResult = Read(address, 4*length);
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
        public async Task<OperationResult<float[]>> ReadFloatAsync(string address, int length)
        {
            var readResult =await ReadAsync(address, 4 * length);
            var result = new OperationResult<float[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToFloat(0, length, DataFormat, IsReverse);
            return result.Complete();
        }

        public OperationResult<float> ReadFloat(int beginAddressInt, int addressInt, byte[] values)
        {
            try
            {
                var interval = (addressInt - beginAddressInt) / 2;
                var offset = (addressInt - beginAddressInt) % 2 * 2;//取余 乘以2（每个地址16位，占两个字节）
                var byteArry = values.Skip(interval * 2 * 2 + offset).Take(2 * 2).ToArray();
                return new OperationResult<float>
                {
                    Value = byteArry.ToFloat(0,DataFormat,IsReverse)
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
        public async Task<OperationResult<double>> ReadDoubleAsync(string address)
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
        public OperationResult<double[]> ReadDouble(string address,int length)
        {
            var readResult = Read(address, 8*length);
            var result = new OperationResult<double[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToDouble(0,length, DataFormat, IsReverse);
            return result.Complete();
        }

        /// <summary>
        /// 读取Double
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public async Task<OperationResult<double[]>> ReadDoubleAsync(string address, int length)
        {
            var readResult =await ReadAsync(address, 8 * length);
            var result = new OperationResult<double[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToDouble(0, length, DataFormat, IsReverse);
            return result.Complete();
        }


        public OperationResult<double> ReadDouble(int beginAddressInt, int addressInt, byte[] values)
        {
            try
            {
                var interval = (addressInt - beginAddressInt) / 4;
                var offset = (addressInt - beginAddressInt) % 4 * 2;
                var byteArry = values.Skip(interval * 2 * 4 + offset).Take(2 * 4).ToArray();
                return new OperationResult<double>
                {
                    Value = byteArry.ToDouble(0,DataFormat,IsReverse)
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
                result.Value = readResult.Value.ToString(0, length, encoding:Encoding.ASCII);
            return result.Complete();
        }

        public async Task<OperationResult<string>> ReadStringAsync(string address, int length)
        {
            var readResult =await ReadAsync(address, 4 * length);
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
        public abstract OperationResult Write(string address, byte[] data, bool isBit = false);

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="data">值</param>
        /// <param name="isBit">值</param>
        /// <returns></returns>
        public abstract Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false);


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
            return Write(address, value.ToByte(),true);
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
        public OperationResult Write(string address, sbyte value)
        {
            throw new NotImplementedException();
        }

        public Task<OperationResult> WriteAsync(string address, sbyte value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, short value)
        {
            return Write(address, value.ToByte(IsReverse) );
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
        public  OperationResult Write(string address, int value)
        {
            return Write(address, value.ToByte(DataFormat,IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, int value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, int[] value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, int[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, uint value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, uint value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, uint[] value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, uint[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, long value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, long value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, long[] value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, long[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, ulong value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, ulong value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }



        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, ulong[] value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, ulong[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, float value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, float value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, float[] value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, float[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, double value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, double value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public OperationResult Write(string address, double[] value)
        {
            return Write(address, value.ToByte(DataFormat, IsReverse));
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, double[] value)
        {
            return await WriteAsync(address, value.ToByte(DataFormat, IsReverse));
        }



        public OperationResult Write(string address, string value)
        {
            return Write(address, value.ToByte(Encoding.ASCII, DataFormat, IsReverse));
        }

        public async Task<OperationResult> WriteAsync(string address, string value)
        {
            return await WriteAsync(address, value.ToByte(Encoding.ASCII, DataFormat, IsReverse));
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="type">数据类型</param>
        /// <returns></returns>
        public OperationResult Write(string address, object value, DataTypeEnum type)
        {
            var result = new OperationResult() { IsSuccess = false };
            switch (type)
            {
                case DataTypeEnum.Bool:
                    result = Write(address, Convert.ToBoolean(value));
                    break;
                case DataTypeEnum.Byte:
                    result = Write(address, Convert.ToByte(value));
                    break;
                case DataTypeEnum.Int16:
                    result = Write(address, Convert.ToInt16(value));
                    break;
                case DataTypeEnum.UInt16:
                    result = Write(address, Convert.ToUInt16(value));
                    break;
                case DataTypeEnum.Int32:
                    result = Write(address, Convert.ToInt32(value));
                    break;
                case DataTypeEnum.UInt32:
                    result = Write(address, Convert.ToUInt32(value));
                    break;
                case DataTypeEnum.Int64:
                    result = Write(address, Convert.ToInt64(value));
                    break;
                case DataTypeEnum.UInt64:
                    result = Write(address, Convert.ToUInt64(value));
                    break;
                case DataTypeEnum.Float:
                    result = Write(address, Convert.ToSingle(value));
                    break;
                case DataTypeEnum.Double:
                    result = Write(address, Convert.ToDouble(value));
                    break;
            }
            return result;
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <param name="type">数据类型</param>
        /// <returns></returns>
        public async Task<OperationResult> WriteAsync(string address, object value, DataTypeEnum type)
        {
            var result = new OperationResult() { IsSuccess = false };
            switch (type)
            {
                case DataTypeEnum.Bool:
                    result = await WriteAsync(address, Convert.ToBoolean(value));
                    break;
                case DataTypeEnum.Byte:
                    result = await WriteAsync(address, Convert.ToByte(value));
                    break;
                case DataTypeEnum.Int16:
                    result = await WriteAsync(address, Convert.ToInt16(value));
                    break;
                case DataTypeEnum.UInt16:
                    result = await WriteAsync(address, Convert.ToUInt16(value));
                    break;
                case DataTypeEnum.Int32:
                    result = await WriteAsync(address, Convert.ToInt32(value));
                    break;
                case DataTypeEnum.UInt32:
                    result = await WriteAsync(address, Convert.ToUInt32(value));
                    break;
                case DataTypeEnum.Int64:
                    result = await WriteAsync(address, Convert.ToInt64(value));
                    break;
                case DataTypeEnum.UInt64:
                    result = await WriteAsync(address, Convert.ToUInt64(value));
                    break;
                case DataTypeEnum.Float:
                    result = await WriteAsync(address, Convert.ToSingle(value));
                    break;
                case DataTypeEnum.Double:
                    result = await WriteAsync(address, Convert.ToDouble(value));
                    break;
            }
            return result;
        }






        #endregion

    }
}

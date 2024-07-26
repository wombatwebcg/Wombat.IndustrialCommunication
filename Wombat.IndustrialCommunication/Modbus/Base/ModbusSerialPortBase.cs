using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;


using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// ModbusRtu协议客户端
    /// </summary>
    public abstract class ModbusSerialPortBase : SerialPortClientBase, IModbusSerialPortClient
    {


        public override bool Connected => _serialPort == null ? false : _serialPort.IsOpen;

        internal byte _stationNumber;
        public byte StationNumber
        {
            get => _stationNumber;
            set
            {
                using (_lock.Lock())
                {

                    _stationNumber = value;
                }
            }
        }

        internal byte _functionCode { get; set; }
        public byte FunctionCode
        {
            get => _functionCode;
            set
            {
                using (_lock.Lock())
                {
                    _functionCode = value;
                }
            }
        }

        internal AsyncLock _lock =  new AsyncLock();

        /// <summary>
        /// 是否自动打开关闭
        /// </summary>
        protected bool _isAutoOpen = true;


        public ModbusSerialPortBase()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="portName">COM端口名称</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">奇偶校验</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="DataFormat">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        public ModbusSerialPortBase(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None):this()
        {
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            Handshake = handshake;
            Parity = parity;
            StopBits = stopBits;

        }



        #region  Read 读取
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
        /// <param name="readLength">读取长度</param>
        /// <param name="byteFormatting"></param>
        /// <returns></returns>
        internal override OperationResult<byte[]> Read(string address, int readLength = 1,bool isBit = false)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult<byte[]>();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{StationNumber} 功能码:{FunctionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    //获取命令（组装报文）
                    byte[] command = GetReadCommand(address, StationNumber, FunctionCode, (ushort)readLength);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));

                    //发送命令并获取响应报文
                    var sendResult = InterpretMessageData(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;
                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(FunctionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }

                    byte[] resultData = new byte[responsePackage.Length - 2 - 3];
                    Array.Copy(responsePackage, 3, resultData, 0, resultData.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                    //4 获取响应报文数据（字节数组形式）                
                    result.Value = resultData.ToArray();
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection) Disconnect();
                }
                return result.Complete();
            }
        }

        public virtual OperationResult<byte[]> Read(byte[] command)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult<byte[]>();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));

                    //发送命令并获取响应报文
                    var sendResult = InterpretMessageData(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;
                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }

                    byte[] resultData = new byte[responsePackage.Length - 2 - 3];
                    Array.Copy(responsePackage, 3, resultData, 0, resultData.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                    //4 获取响应报文数据（字节数组形式）                
                    result.Value = resultData.ToArray();
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection) Disconnect();
                }
                return result.Complete();
            }
        }


        internal  override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int readLength = 1, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult<byte[]>();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult =await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{StationNumber} 功能码:{FunctionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    //获取命令（组装报文）
                    byte[] command = GetReadCommand(address, StationNumber, FunctionCode, (ushort)readLength);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));

                    //发送命令并获取响应报文
                    var sendResult =await InterpretMessageDataAsync(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;
                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(FunctionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }

                    byte[] resultData = new byte[responsePackage.Length - 2 - 3];
                    Array.Copy(responsePackage, 3, resultData, 0, resultData.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                    //4 获取响应报文数据（字节数组形式）                
                    result.Value = resultData.ToArray();
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection)await DisconnectAsync();
                }
                return result.Complete();
            }
        }


        public virtual async Task<OperationResult<byte[]>> ReadAsync(byte[] command)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult<byte[]>();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    //获取命令（组装报文）
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));

                    //发送命令并获取响应报文
                    var sendResult = await InterpretMessageDataAsync(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;
                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        return result.Complete();
                    }

                    byte[] resultData = new byte[responsePackage.Length - 2 - 3];
                    Array.Copy(responsePackage, 3, resultData, 0, resultData.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                    //4 获取响应报文数据（字节数组形式）                
                    result.Value = resultData.ToArray();
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection) await DisconnectAsync();
                }
                return result.Complete();
            }
        }

        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        public OperationResult<short> ReadInt16Bit(string address, bool left = true)
        {
            string[] adds = address.Split('.');
            var readResult = Read(adds[0].Trim());
            var result = new OperationResult<short>(readResult);
            if (result.IsSuccess)
            {
                result.Value = BitConverter.ToInt16(readResult.Value, 0);
                if (adds.Length >= 2)
                {
                    var index = int.Parse(adds[1].Trim());
                    var binaryArray = result.Value.ToByte().ToBool(0, 16, reverse: IsReverse);
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
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="left">按位取值从左边开始取</param>
        /// <returns></returns>
        public OperationResult<ushort> ReadUInt16Bit(string address, bool left = true)
        {
            string[] adds = address.Split('.');
            var readResult = Read(adds[0].Trim());
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


        public async Task<OperationResult<short>> ReadInt16BitAsync(string address, bool left = true)
        {
            string[] adds = address.Split('.');
            var readResult = await ReadAsync(adds[0].Trim());
            var result = new OperationResult<short>(readResult);
            if (result.IsSuccess)
            {
                result.Value = BitConverter.ToInt16(readResult.Value, 0);
                if (adds.Length >= 2)
                {
                    var index = int.Parse(adds[1].Trim());
                    var binaryArray = result.Value.ToByte().ToBool(0, 16, reverse: IsReverse);
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

        public async Task<OperationResult<ushort>> ReadUInt16BitAsync(string address, bool left = true)
        {
            string[] adds = address.Split('.');
            var readResult = await ReadAsync(adds[0].Trim());
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

        /// <summary>
        /// 分批读取（批量读取，内部进行批量计算读取）
        /// </summary>
        /// <param name="addresses"></param>
        /// <returns></returns>
        private OperationResult<List<ModbusOutput>> BatchReadBase(List<ModbusInput> addresses)
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
                    var tempOperationResult = BatchReadBase(addressList, stationNumber, functionCode);
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

        private OperationResult<Dictionary<string, object>> BatchReadBase(Dictionary<string, DataTypeEnum> addressList, byte stationNumber, byte functionCode)
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
                StationNumber = stationNumber;
                FunctionCode = functionCode;
                var tempOperationResult = Read(minAddress.ToString(), length);

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
                            tempVaue = ReadBoolean(minAddress.ToString(), item.Key.ToString(), rValue).Value;
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
            var result = BatchReadBase(addresses);
            for (int i = 0; i < retryCount; i++)
            {
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    result = BatchReadBase(addresses);
                }
                else
                    break;
            }
            return result;
        }






        /// <summary>
        /// 分批读取（批量读取，内部进行批量计算读取）
        /// </summary>
        /// <param name="addresses"></param>
        /// <returns></returns>
        private async Task<OperationResult<List<ModbusOutput>>> BatchReadBaseAsync(List<ModbusInput> addresses)
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
                    var tempOperationResult = await BatchReadBaseAsync(addressList, stationNumber, functionCode);
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

        private async Task<OperationResult<Dictionary<string, object>>> BatchReadBaseAsync(Dictionary<string, DataTypeEnum> addressList, byte stationNumber, byte functionCode)
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
                StationNumber = stationNumber;
                FunctionCode = functionCode;
                var tempOperationResult = await ReadAsync(minAddress.ToString(), Convert.ToUInt16(length));

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
                            tempVaue = ReadBoolean(minAddress.ToString(), item.Key.ToString(), rValue).Value;
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


        public async Task<OperationResult<List<ModbusOutput>>> BatchReadAsync(List<ModbusInput> addresses, uint retryCount = 1)
        {
            var result = await BatchReadBaseAsync(addresses);
            for (int i = 0; i < retryCount; i++)
            {
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    result = BatchReadBase(addresses);
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
        public override OperationResult Write(string address, bool value)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{StationNumber} 功能码:{FunctionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCoilCommand(address, value, StationNumber, FunctionCode);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                    //发送命令并获取响应报文
                    var sendResult = InterpretMessageData(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;

                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(FunctionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                        //return result.Complete();
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Buffer.BlockCopy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection) Disconnect();
                }
                return result.Complete();
            }
        }


        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        public override async Task<OperationResult> WriteAsync(string address, bool value)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult =await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{StationNumber} 功能码:{FunctionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCoilCommand(address, value, StationNumber, FunctionCode);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                    //发送命令并获取响应报文
                    var sendResult =await InterpretMessageDataAsync(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;

                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(FunctionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Buffer.BlockCopy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection)await DisconnectAsync();
                }
                return result.Complete();
            }
        }

        public override OperationResult Write(string address, bool[] value)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{StationNumber} 功能码:{FunctionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCoilCommand(address, value, StationNumber, FunctionCode);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                    //发送命令并获取响应报文
                    var sendResult = InterpretMessageData(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;

                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(FunctionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Buffer.BlockCopy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection) Disconnect();
                }
                return result.Complete();
            }
        }

        public override async Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult =await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{StationNumber} 功能码:{FunctionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCoilCommand(address, value, StationNumber, FunctionCode);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                    //发送命令并获取响应报文
                    var sendResult =await InterpretMessageDataAsync(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;

                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(FunctionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Buffer.BlockCopy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection)await DisconnectAsync();
                }
                return result.Complete();
            }
        }

        public virtual async Task<OperationResult> WriteAsync(byte[] command)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                    //发送命令并获取响应报文
                    var sendResult = await InterpretMessageDataAsync(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;

                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        return result.Complete();
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Buffer.BlockCopy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection) await DisconnectAsync();
                }
                return result.Complete();
            }
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="StationNumber"></param>
        /// <param name="FunctionCode"></param>
        /// <returns></returns>
        //public override OperationResult Write(string address, byte value, byte StationNumber = 1, byte FunctionCode = 16)
        //{
        //    _advancedHybirdLock.Enter();
        //    var result = new OperationResult();
        //    if (!_serialPortBase?.IsConnect ?? true)
        //    {
        //        var connectResult = Connect();
        //        if (!connectResult.IsSuccess)
        //        {
        //            connectResult.Message = $"读取 地址:{address} 站号:{StationNumber} 功能码:{FunctionCode} 失败。{ connectResult.Message}";
        //            _advancedHybirdLock.Leave();
        //            return result.SetInfo(connectResult);
        //        }
        //    }
        //    try
        //    {
        //        var command = GetWriteCommand(address, value, StationNumber, FunctionCode);

        //        var commandCRC16 = CRC16Helper.GetCRC16(command);
        //        result.Requst = string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
        //        var sendResult = SendPackageReliable(commandCRC16);
        //        if (!sendResult.IsSuccess)
        //        {
        //            _advancedHybirdLock.Leave();
        //            return result.SetInfo(sendResult).Complete();
        //        }
        //        var responsePackage = sendResult.Value;
        //        if (!responsePackage.Any())
        //        {
        //            result.IsSuccess = false;
        //            result.Message = "响应结果为空";
        //            _advancedHybirdLock.Leave();
        //            return result.Complete();
        //        }
        //        else if (!CRC16Helper.CheckCRC16(responsePackage))
        //        {
        //            result.IsSuccess = false;
        //            result.Message = "响应结果CRC16Helper验证失败";
        //            //return result.Complete();
        //        }
        //        else if (ModbusHelper.VerifyFunctionCode(FunctionCode, responsePackage[1]))
        //        {
        //            result.IsSuccess = false;
        //            result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
        //        }
        //        byte[] resultBuffer = new byte[responsePackage.Length - 2];
        //        Array.Copy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
        //        result.Response = string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
        //    }
        //    catch (Exception ex)
        //    {
        //        result.IsSuccess = false;
        //        result.Message = ex.Message;
        //    }
        //    finally
        //    {
        //        if (!IsLongLivedConnection) Disconnect();
        //    }
        //    _advancedHybirdLock.Leave();
        //    return result.Complete();
        //}



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="StationNumber"></param>
        /// <param name="FunctionCode"></param>
        /// <returns></returns>
        internal override OperationResult Write(string address, byte[] values,bool isBit)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{StationNumber} 功能码:{FunctionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCommand(address, values, StationNumber, FunctionCode);

                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                    var sendResult = InterpretMessageData(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;
                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(FunctionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Array.Copy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection) Disconnect();
                }
                return result.Complete();
            }
        }


        public virtual OperationResult Write(byte[] command)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {

                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                    var sendResult = InterpretMessageData(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;
                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Array.Copy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection) Disconnect();
                }
                return result.Complete();
            }
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="StationNumber"></param>
        /// <param name="FunctionCode"></param>
        /// <returns></returns>
        internal override async Task<OperationResult> WriteAsync(string address, byte[] values,bool isBit)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult =await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{StationNumber} 功能码:{FunctionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCommand(address, values, StationNumber, FunctionCode);

                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                    var sendResult =await InterpretMessageDataAsync(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;
                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(FunctionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Array.Copy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsLongLivedConnection)await DisconnectAsync();
                }
                return result.Complete();
            }
        }



        #endregion

        #region 获取命令

        public virtual string TranPLCAddress(string address)
        {
            return address;
        }


        /// <summary>
        /// 获取读取命令
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
        /// <param name="length">读取长度</param>
        /// <returns></returns>
        public byte[] GetReadCommand(string address, byte StationNumber, byte FunctionCode, ushort length)
        {
            address = TranPLCAddress(address);
            var readAddress = ushort.Parse(address?.Trim());
            byte[] buffer = new byte[6];
            buffer[0] = StationNumber;  //站号
            buffer[1] = FunctionCode;   //功能码
            buffer[2] = BitConverter.GetBytes(readAddress)[1];
            buffer[3] = BitConverter.GetBytes(readAddress)[0];//寄存器地址
            buffer[4] = BitConverter.GetBytes(length)[1];
            buffer[5] = BitConverter.GetBytes(length)[0];//表示request 寄存器的长度(寄存器个数)
            return buffer;
        }



        /// <summary>
        /// 获取写入命令
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="values"></param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCommand(string address, byte value, byte StationNumber, byte FunctionCode)
        {

            address = TranPLCAddress(address);
            var readAddress = ushort.Parse(address?.Trim());
            byte[] buffer = new byte[6];
            buffer[0] = StationNumber;//站号
            buffer[1] = FunctionCode; //功能码
            buffer[2] = BitConverter.GetBytes(readAddress)[1];
            buffer[3] = BitConverter.GetBytes(readAddress)[0];//寄存器地址
            buffer[4] = value;    
            buffer[5] = 0;
            return buffer;
        }




        /// <summary>
        /// 获取写入命令
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="values"></param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCommand(string address, byte[] values, byte StationNumber, byte FunctionCode)
        {
            address = TranPLCAddress(address);
            var readAddress = ushort.Parse(address?.Trim());
            if (values.Length > 2)
            {
                byte[] buffer = new byte[7 + values.Length];
                buffer[0] = StationNumber; //站号
                buffer[1] = FunctionCode;  //功能码
                buffer[2] = BitConverter.GetBytes(readAddress)[1];
                buffer[3] = BitConverter.GetBytes(readAddress)[0];//寄存器地址
                buffer[4] = (byte)(values.Length / 2 / 256);
                buffer[5] = (byte)(values.Length / 2 % 256);//写寄存器数量(除2是两个字节一个寄存器，寄存器16位。除以256是byte最大存储255。)              
                buffer[6] = (byte)(values.Length);          //写字节的个数
                values.CopyTo(buffer, 7);                   //把目标值附加到数组后面
                return buffer;
            }
            else
            {

                byte[] buffer = new byte[6];
                buffer[0] = StationNumber;//站号
                buffer[1] = FunctionCode; //功能码
                buffer[2] = BitConverter.GetBytes(readAddress)[1];
                buffer[3] = BitConverter.GetBytes(readAddress)[0];//寄存器地址
                buffer[4] = values[0];
                buffer[5] = values[1];
                return buffer;

            }
        }

        /// <summary>
        /// 获取线圈写入命令
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value"></param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
        /// <returns></returns>
        public  byte[] GetWriteCoilCommand(string address, bool value, byte StationNumber, byte FunctionCode)
        {
            address = TranPLCAddress(address);
            var readAddress = ushort.Parse(address?.Trim());
            byte[] buffer = new byte[6];
            buffer[0] = StationNumber;//站号
            buffer[1] = FunctionCode; //功能码
            buffer[2] = BitConverter.GetBytes(readAddress)[1];
            buffer[3] = BitConverter.GetBytes(readAddress)[0];//寄存器地址
            buffer[4] = (byte)(value ? 0xFF : 0x00);     //此处只可以是FF表示闭合00表示断开，其他数值非法
            buffer[5] = 0x00;
            return buffer;
        }


        /// <summary>
        /// 获取线圈写入命令
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="values"></param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCoilCommand(string address, bool[] values, byte StationNumber, byte FunctionCode, byte[] check = null)
        {
            address = TranPLCAddress(address);
            var writeAddress = ushort.Parse(address?.Trim());
            int length = (values.Length + 1) / 2;
            byte[] newValue = values.ToByte();

            byte[] buffer = new byte[7 + newValue.Length];
            buffer[0] = StationNumber; //站号
            buffer[1] = FunctionCode;  //功能码
            buffer[2] = BitConverter.GetBytes(writeAddress)[1];
            buffer[3] = BitConverter.GetBytes(writeAddress)[0];//寄存器地址
            buffer[4] = (byte)(values.Length / 2 / 256);
            buffer[5] = (byte)(values.Length / 2 % 256);//写寄存器数量(除2是两个字节一个寄存器，寄存器16位。除以256是byte最大存储255。)              
            buffer[6] = (byte)(newValue.Length);          //写字节的个数
            newValue.CopyTo(buffer, 7);                   //把目标值附加到数组后面
            return buffer;

        }


        #endregion



    }
}

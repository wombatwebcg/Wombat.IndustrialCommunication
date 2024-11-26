using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// ModbusRtu协议客户端
    /// </summary>
    public abstract class ModbusClientSerialPortBase : ModbusClient
    {


        public string PortName { get; set; }
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public StopBits StopBits { get; set; } = StopBits.One;
        public Parity Parity { get; set; } = Parity.None;
        public Handshake Handshake { get; set; } = Handshake.None;


        /// <summary>
        /// 串行端口对象
        /// </summary>
        protected internal SerialPort _serialPort;


        /// <summary>
        /// 获取设备上的COM端口集合
        /// </summary>
        /// <returns></returns>
        public static string[] GetPortNames()
        {
            return SerialPort.GetPortNames();
        }

        public override bool Connected => _serialPort == null ? false : _serialPort.IsOpen;



        /// <summary>
        /// 连接
        /// </summary>
        /// <returns></returns>
        internal override OperationResult DoConnect()
        {
            _serialPort?.Close();
            _serialPort = new SerialPort();
            _serialPort.PortName = PortName ?? throw new ArgumentNullException(nameof(PortName));
            _serialPort.BaudRate = BaudRate;
            _serialPort.Parity = Parity;
            _serialPort.DataBits = DataBits;
            _serialPort.StopBits = StopBits;
            _serialPort.Handshake = Handshake;


            _serialPort.WriteTimeout = (int)ConnectTimeout.TotalMilliseconds;
            _serialPort.ReadTimeout = (int)ConnectTimeout.TotalMilliseconds;


            var result = new OperationResult();
            try
            {
                _serialPort.Open();

            }
            catch (Exception ex)
            {
                if (_serialPort?.IsOpen ?? false) _serialPort?.Close();
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.ErrorCode = 408;
                result.Exception = ex;
            }
            return result.Complete();
        }

        /// <summary>
        /// 连接
        /// </summary>
        /// <returns></returns>
        internal override async Task<OperationResult> DoConnectAsync()
        {

            _serialPort?.Close();
            _serialPort = new SerialPort();
            _serialPort.PortName = PortName ?? throw new ArgumentNullException(nameof(PortName));
            _serialPort.BaudRate = BaudRate;
            _serialPort.Parity = Parity;
            _serialPort.DataBits = DataBits;
            _serialPort.StopBits = StopBits;
            _serialPort.Handshake = Handshake;

            _serialPort.WriteTimeout = (int)ConnectTimeout.TotalMilliseconds;
            _serialPort.ReadTimeout = (int)ConnectTimeout.TotalMilliseconds;


            var result = new OperationResult();
            try
            {
                await Task.Run(async () =>
                {
                    _serialPort.Open();

                });

            }
            catch (Exception ex)
            {
                if (_serialPort?.IsOpen ?? false) _serialPort?.Close();
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.ErrorCode = 408;
                result.Exception = ex;
            }
            return result.Complete();
        }


        /// <summary>
        /// 关闭连接
        /// </summary>
        /// <returns></returns>
        internal override async Task<OperationResult> DoDisconnectAsync()
        {
            var result = new OperationResult();
            try
            {
                await Task.Run(async () =>
                {
                    _serialPort.Close();

                });

            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return result;
        }


        /// <summary>
        /// 关闭连接
        /// </summary>
        /// <returns></returns>
        internal override OperationResult DoDisconnect()
        {
            var result = new OperationResult();
            try
            {
                _serialPort.Close();
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            return result;
        }



        public ModbusClientSerialPortBase()
        {
            _serialPort = new SerialPort();
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
        public ModbusClientSerialPortBase(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None) : this()
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
        /// <param name="modbusHeader.StationNumber">站号</param>
        /// <param name="modbusHeader.FunctionCode">功能码</param>
        /// <param name="readLength">读取长度</param>
        /// <param name="byteFormatting"></param>
        /// <returns></returns>
        internal override OperationResult<byte[]> Read(string address, int readLength = 1, bool isBit = false)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusHeader(address, out var modbusHeader))
                {
                    try
                    {

                        if (!Connected && !IsLongLivedConnection)
                        {
                            var connectResult = Connect();
                            if (!connectResult.IsSuccess)
                            {
                                connectResult.Message = $"读取地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{connectResult.Message}";
                                return result.SetInfo(connectResult);
                            }
                        }
                        //获取命令（组装报文）
                        byte[] command = GetReadCommand(modbusHeader.Address, modbusHeader.StationNumber, modbusHeader.FunctionCode, (ushort)readLength);
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
                        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, responsePackage[1]))
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
                }
                else
                {
                    result = OperationResult.CreateFailedResult<byte[]>("modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址");
                }

                return result.Complete();
            }
        }

        public virtual OperationResult<byte[]> Read(byte[] command)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult<byte[]>();

                try
                {

                    if (!Connected && !IsLongLivedConnection)
                    {
                        var connectResult = Connect();
                        if (!connectResult.IsSuccess)
                        {
                            return result.SetInfo(connectResult);
                        }
                    }
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


        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int readLength = 1, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {

                var result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusHeader(address, out var modbusHeader))
                {
                    try
                    {

                        if (!Connected && !IsLongLivedConnection)
                        {
                            var connectResult = await ConnectAsync();
                            if (!connectResult.IsSuccess)
                            {
                                connectResult.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{connectResult.Message}";
                                return result.SetInfo(connectResult);
                            }
                        }
                        //获取命令（组装报文）
                        byte[] command = GetReadCommand(modbusHeader.Address, modbusHeader.StationNumber, modbusHeader.FunctionCode, (ushort)readLength);
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
                        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, responsePackage[1]))
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
                        if (!IsLongLivedConnection) await DisconnectAsync();
                    }
                }
                else
                {
                    result = OperationResult.CreateFailedResult<byte[]>("modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址");

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

        private OperationResult<Dictionary<ushort, object>> BatchReadBase(Dictionary<ushort, DataTypeEnums> addressList, byte stationNumber, byte functionCode)
        {
            var result = new OperationResult<Dictionary<ushort, object>>();
            result.Value = new Dictionary<ushort, object>();

            var addresses = addressList.Select(t => new KeyValuePair<ushort, DataTypeEnums>(t.Key, t.Value)).ToList();

            var minAddress = addresses.Select(t => t.Key).Min();
            var maxAddress = addresses.Select(t => t.Key).Max();
            while (maxAddress >= minAddress)
            {
                int length = 121;//125 - 4 = 121

                var tempAddress = addresses.Where(t => t.Key >= minAddress && t.Key <= minAddress + length).ToList();
                //如果范围内没有数据。按正确逻辑不存在这种情况。
                if (!tempAddress.Any())
                {
                    minAddress = (ushort)(minAddress + length);
                    continue;
                }

                var tempMax = tempAddress.OrderByDescending(t => t.Key).FirstOrDefault();
                switch (tempMax.Value)
                {
                    case DataTypeEnums.Bool:
                    case DataTypeEnums.Byte:
                    case DataTypeEnums.Int16:
                    case DataTypeEnums.UInt16:
                        length = tempMax.Key + 1 - minAddress;
                        break;
                    case DataTypeEnums.Int32:
                    case DataTypeEnums.UInt32:
                    case DataTypeEnums.Float:
                        length = tempMax.Key + 2 - minAddress;
                        break;
                    case DataTypeEnums.Int64:
                    case DataTypeEnums.UInt64:
                    case DataTypeEnums.Double:
                        length = tempMax.Key + 4 - minAddress;
                        break;
                    default:
                        throw new Exception("Message BatchRead 未定义类型 -1");
                }
                if (!ModbusAddressParser.TryParseModbusHeader(new ModbusHeader()
                {
                    Address = minAddress,
                    FunctionCode = functionCode,
                    StationNumber = stationNumber
                }, out string splicingAddress))
                {
                    result.Message = "modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址";
                    result.IsSuccess = true;
                    return result.Complete();

                }


                var tempOperationResult = Read(splicingAddress, length);

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

                var rValue = tempOperationResult.Value.ToArray();
                foreach (var item in tempAddress)
                {
                    object tempVaue = null;

                    switch (item.Value)
                    {
                        case DataTypeEnums.Bool:
                            tempVaue = ReadBoolean(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.Byte:
                            throw new Exception("Message BatchRead 未定义类型 -2");
                        case DataTypeEnums.Int16:
                            tempVaue = ReadInt16(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.UInt16:
                            tempVaue = ReadUInt16(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.Int32:
                            tempVaue = ReadInt32(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.UInt32:
                            tempVaue = ReadUInt32(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.Int64:
                            tempVaue = ReadInt64(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.UInt64:
                            tempVaue = ReadUInt64(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.Float:
                            tempVaue = ReadFloat(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.Double:
                            tempVaue = ReadDouble(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        default:
                            throw new Exception("Message BatchRead 未定义类型 -3");
                    }

                    result.Value.Add(item.Key, tempVaue);
                }
                minAddress = (ushort)(minAddress + length);

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

        private async Task<OperationResult<Dictionary<ushort, object>>> BatchReadBaseAsync(Dictionary<ushort, DataTypeEnums> addressList, byte stationNumber, byte functionCode)
        {
            var result = new OperationResult<Dictionary<ushort, object>>();
            result.Value = new Dictionary<ushort, object>();

            var addresses = addressList.Select(t => new KeyValuePair<ushort, DataTypeEnums>(t.Key, t.Value)).ToList();

            var minAddress = addresses.Select(t => t.Key).Min();
            var maxAddress = addresses.Select(t => t.Key).Max();
            while (maxAddress >= minAddress)
            {
                int length = 121;//125 - 4 = 121

                var tempAddress = addresses.Where(t => t.Key >= minAddress && t.Key <= minAddress + length).ToList();
                //如果范围内没有数据。按正确逻辑不存在这种情况。
                if (!tempAddress.Any())
                {
                    minAddress = (ushort)(minAddress + length);
                    continue;
                }

                var tempMax = tempAddress.OrderByDescending(t => t.Key).FirstOrDefault();
                switch (tempMax.Value)
                {
                    case DataTypeEnums.Bool:
                    case DataTypeEnums.Byte:
                    case DataTypeEnums.Int16:
                    case DataTypeEnums.UInt16:
                        length = tempMax.Key + 1 - minAddress;
                        break;
                    case DataTypeEnums.Int32:
                    case DataTypeEnums.UInt32:
                    case DataTypeEnums.Float:
                        length = tempMax.Key + 2 - minAddress;
                        break;
                    case DataTypeEnums.Int64:
                    case DataTypeEnums.UInt64:
                    case DataTypeEnums.Double:
                        length = tempMax.Key + 4 - minAddress;
                        break;
                    default:
                        throw new Exception("Message BatchRead 未定义类型 -1");
                }
                if (!ModbusAddressParser.TryParseModbusHeader(new ModbusHeader()
                {
                    Address = minAddress,
                    FunctionCode = functionCode,
                    StationNumber = stationNumber
                }, out string splicingAddress))
                {
                    result.Message = "modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址";
                    result.IsSuccess = true;
                    return result.Complete();

                }

                var tempOperationResult = await ReadAsync(splicingAddress, Convert.ToUInt16(length));

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

                var rValue = tempOperationResult.Value.ToArray();
                foreach (var item in tempAddress)
                {
                    object tempVaue = null;

                    switch (item.Value)
                    {
                        case DataTypeEnums.Bool:
                            tempVaue = ReadBoolean(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.Byte:
                            throw new Exception("Message BatchRead 未定义类型 -2");
                        case DataTypeEnums.Int16:
                            tempVaue = ReadInt16(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.UInt16:
                            tempVaue = ReadUInt16(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.Int32:
                            tempVaue = ReadInt32(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.UInt32:
                            tempVaue = ReadUInt32(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.Int64:
                            tempVaue = ReadInt64(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.UInt64:
                            tempVaue = ReadUInt64(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.Float:
                            tempVaue = ReadFloat(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        case DataTypeEnums.Double:
                            tempVaue = ReadDouble(minAddress.ToString(), item.Key.ToString(), rValue).Value;
                            break;
                        default:
                            throw new Exception("Message BatchRead 未定义类型 -3");
                    }

                    result.Value.Add(item.Key, tempVaue);
                }
                minAddress = (ushort)(minAddress + length);

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
                if (ModbusAddressParser.TryParseModbusHeader(address, out var modbusHeader))
                {
                    try
                    {
                        if (!Connected && !IsLongLivedConnection)
                        {
                            var connectResult = Connect();
                            if (!connectResult.IsSuccess)
                            {
                                connectResult.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{connectResult.Message}";
                                return result.SetInfo(connectResult);
                            }
                        }
                        var command = GetWriteCoilCommand(modbusHeader.Address, value, modbusHeader.StationNumber, modbusHeader.FunctionCode);
                        var commandCRC16 = CRC16Helper.GetCRC16(command);
                        result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                        //发送命令并获取响应报文
                        var sendResult = InterpretMessageData(commandCRC16);
                        if (!sendResult.IsSuccess)
                        {
                            if (!IsLongLivedConnection) Disconnect();
                            return result.SetInfo(sendResult).Complete();
                        }
                        var responsePackage = sendResult.Value;

                        if (!responsePackage.Any())
                        {
                            result.IsSuccess = false;
                            result.Message = "响应结果为空";
                            if (!IsLongLivedConnection) Disconnect();
                            return result.Complete();
                        }
                        else if (!CRC16Helper.CheckCRC16(responsePackage))
                        {
                            result.IsSuccess = false;
                            result.Message = "响应结果CRC16Helper验证失败";
                            if (!IsLongLivedConnection) Disconnect();
                            return result.Complete();
                        }
                        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, responsePackage[1]))
                        {
                            result.IsSuccess = false;
                            result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
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
                        if (!IsLongLivedConnection) Disconnect();
                    }
                }
                else
                {
                    result = OperationResult.CreateFailedResult<byte[]>("modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址");

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
                if (ModbusAddressParser.TryParseModbusHeader(address, out var modbusHeader))
                {
                    try
                    {
                        if (!Connected && !IsLongLivedConnection)
                        {
                            var connectResult = await ConnectAsync();
                            if (!connectResult.IsSuccess)
                            {
                                connectResult.Message = $"读取地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{connectResult.Message}";
                                return result.SetInfo(connectResult);
                            }
                        }
                        var command = GetWriteCoilCommand(modbusHeader.Address, value, modbusHeader.StationNumber, modbusHeader.FunctionCode);
                        var commandCRC16 = CRC16Helper.GetCRC16(command);
                        result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                        //发送命令并获取响应报文
                        var sendResult = await InterpretMessageDataAsync(commandCRC16);
                        if (!sendResult.IsSuccess)
                        {
                            if (!IsLongLivedConnection) await DisconnectAsync();
                            return result.SetInfo(sendResult).Complete();
                        }
                        var responsePackage = sendResult.Value;

                        if (!responsePackage.Any())
                        {
                            result.IsSuccess = false;
                            result.Message = "响应结果为空";
                            if (!IsLongLivedConnection) await DisconnectAsync();
                            return result.Complete();
                        }
                        else if (!CRC16Helper.CheckCRC16(responsePackage))
                        {
                            result.IsSuccess = false;
                            result.Message = "响应结果CRC16Helper验证失败";
                            if (!IsLongLivedConnection) await DisconnectAsync();
                            return result.Complete();
                        }
                        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, responsePackage[1]))
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
                        if (!IsLongLivedConnection) await DisconnectAsync();
                    }
                }
                else
                {
                    result = OperationResult.CreateFailedResult<byte[]>("modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址");

                }
                return result.Complete();
            }
        }

        public override OperationResult Write(string address, bool[] value)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult();
                if (ModbusAddressParser.TryParseModbusHeader(address, out var modbusHeader))
                {
                    try
                    {
                    if (!Connected && !IsLongLivedConnection)
                    {
                        var connectResult = Connect();
                        if (!connectResult.IsSuccess)
                        {
                            connectResult.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{ connectResult.Message}";
                            return result.SetInfo(connectResult);
                        }
                    }
                    var command = GetWriteCoilCommand(modbusHeader.Address, value, modbusHeader.StationNumber, modbusHeader.FunctionCode);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                    //发送命令并获取响应报文
                    var sendResult = InterpretMessageData(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        if (!IsLongLivedConnection) Disconnect();
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;

                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        if (!IsLongLivedConnection) Disconnect();
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, responsePackage[1]))
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
                }
                else
                {
                    result = OperationResult.CreateFailedResult<byte[]>("modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址");

                }

                return result.Complete();
            }
        }

        public override async Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            using (await _lock.LockAsync())

            {
                var result = new OperationResult();
                if (ModbusAddressParser.TryParseModbusHeader(address, out var modbusHeader))
                {
                    try
                    {
                    if (!Connected && !IsLongLivedConnection)
                    {
                        var connectResult = await ConnectAsync();
                        if (!connectResult.IsSuccess)
                        {
                            connectResult.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{ connectResult.Message}";
                            return result.SetInfo(connectResult);
                        }
                    }
                    var command = GetWriteCoilCommand(modbusHeader.Address, value, modbusHeader.StationNumber, modbusHeader.FunctionCode);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
                    //发送命令并获取响应报文
                    var sendResult = await InterpretMessageDataAsync(commandCRC16);
                    if (!sendResult.IsSuccess)
                    {
                        if (!IsLongLivedConnection) await DisconnectAsync();
                        return result.SetInfo(sendResult).Complete();
                    }
                    var responsePackage = sendResult.Value;

                    if (!responsePackage.Any())
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果为空";
                        if (!IsLongLivedConnection) await DisconnectAsync();
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, responsePackage[1]))
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
                    if (!IsLongLivedConnection) await DisconnectAsync();
                }
                }
                else
                {
                    result = OperationResult.CreateFailedResult<byte[]>("modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址");

                }

                return result.Complete();
            }
        }

        public virtual async Task<OperationResult> WriteAsync(byte[] command)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                try
                {
                if (!Connected && !IsLongLivedConnection)
                {
                    var connectResult = await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        return result.SetInfo(connectResult);
                    }
                }
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
        /// <param name="modbusHeader.StationNumber"></param>
        /// <param name="modbusHeader.FunctionCode"></param>
        /// <returns></returns>
        //public override OperationResult Write(string address, byte value, byte modbusHeader.StationNumber = 1, byte modbusHeader.FunctionCode = 16)
        //{
        //    _advancedHybirdLock.Enter();
        //    var result = new OperationResult();
        //    if (!_serialPortBase?.IsConnect ?? true)
        //    {
        //        var connectResult = Connect();
        //        if (!connectResult.IsSuccess)
        //        {
        //            connectResult.Message = $"读取 地址:{address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{ connectResult.Message}";
        //            _advancedHybirdLock.Leave();
        //            return result.SetInfo(connectResult);
        //        }
        //    }
        //    try
        //    {
        //        var command = GetWriteCommand(modbusHeader.RegisterAddress, value, modbusHeader.StationNumber, modbusHeader.FunctionCode);

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
        //        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, responsePackage[1]))
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
        /// <param name="modbusHeader.StationNumber"></param>
        /// <param name="modbusHeader.FunctionCode"></param>
        /// <returns></returns>
        internal override OperationResult Write(string address, byte[] values, bool isBit)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult();
                if (ModbusAddressParser.TryParseModbusHeader(address, out var modbusHeader))
                {
                    try
                    {
                    if (!Connected && !IsLongLivedConnection)
                    {
                        var connectResult = Connect();
                        if (!connectResult.IsSuccess)
                        {
                            connectResult.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{ connectResult.Message}";
                            return result.SetInfo(connectResult);
                        }
                    }
                    var command = GetWriteCommand(modbusHeader.Address, values, modbusHeader.StationNumber, modbusHeader.FunctionCode);

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
                        if (!IsLongLivedConnection) Disconnect();
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        if (!IsLongLivedConnection) Disconnect();
                        return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, responsePackage[1]))
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
                }
                else
                {
                    result = OperationResult.CreateFailedResult<byte[]>("modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址");

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
                        if (!IsLongLivedConnection) Disconnect();
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        if (!IsLongLivedConnection) Disconnect();
                        return result.Complete();
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
        /// <param name="modbusHeader.StationNumber"></param>
        /// <param name="modbusHeader.FunctionCode"></param>
        /// <returns></returns>
        internal override async Task<OperationResult> WriteAsync(string address, byte[] values, bool isBit)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                if (ModbusAddressParser.TryParseModbusHeader(address, out var modbusHeader))
                {
                    try
                    {
                    if (!Connected && !IsLongLivedConnection)
                    {
                        var connectResult = await ConnectAsync();
                        if (!connectResult.IsSuccess)
                        {
                            connectResult.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{ connectResult.Message}";
                            return result.SetInfo(connectResult);
                        }
                    }

                    var command = GetWriteCommand(modbusHeader.Address, values, modbusHeader.StationNumber, modbusHeader.FunctionCode);

                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts.Add(string.Join(" ", commandCRC16.Select(t => t.ToString("X2"))));
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
                        if (!IsLongLivedConnection) await DisconnectAsync();
                        return result.Complete();
                    }
                    else if (!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        if (!IsLongLivedConnection) await DisconnectAsync();
                        return result.Complete();
                    }
                    else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, responsePackage[1]))
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
                    if (!IsLongLivedConnection) await DisconnectAsync();
                }
                }
                else
                {
                    result = OperationResult.CreateFailedResult<byte[]>("modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址");

                }

                return result.Complete();
            }
        }



        #endregion

        #region 获取命令

        public virtual ushort TranPLCAddress(ushort address)
        {
            return address;
        }


        /// <summary>
        /// 获取读取命令
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="modbusHeader.StationNumber">站号</param>
        /// <param name="modbusHeader.FunctionCode">功能码</param>
        /// <param name="length">读取长度</param>
        /// <returns></returns>
        public byte[] GetReadCommand(ushort address, byte stationNumber, byte functionCode, ushort length)
        {
            address = TranPLCAddress(address);
            byte[] buffer = new byte[6];
            buffer[0] = stationNumber;  //站号
            buffer[1] = functionCode;   //功能码
            buffer[2] = BitConverter.GetBytes(address)[1];
            buffer[3] = BitConverter.GetBytes(address)[0];//寄存器地址
            buffer[4] = BitConverter.GetBytes(length)[1];
            buffer[5] = BitConverter.GetBytes(length)[0];//表示request 寄存器的长度(寄存器个数)
            return buffer;
        }



        /// <summary>
        /// 获取写入命令
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="values"></param>
        /// <param name="modbusHeader.StationNumber">站号</param>
        /// <param name="modbusHeader.FunctionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCommand(ushort address, byte value, byte stationNumber, byte functionCode)
        {

            address = TranPLCAddress(address);
            byte[] buffer = new byte[6];
            buffer[0] = stationNumber;//站号
            buffer[1] = functionCode; //功能码
            buffer[2] = BitConverter.GetBytes(address)[1];
            buffer[3] = BitConverter.GetBytes(address)[0];//寄存器地址
            buffer[4] = value;
            buffer[5] = 0;
            return buffer;
        }




        /// <summary>
        /// 获取写入命令
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="values"></param>
        /// <param name="modbusHeader.StationNumber">站号</param>
        /// <param name="modbusHeader.FunctionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCommand(ushort address, byte[] values, byte stationNumber, byte functionCode)
        {
            address = TranPLCAddress(address);
            if (values.Length > 2)
            {
                byte[] buffer = new byte[7 + values.Length];
                buffer[0] = stationNumber; //站号
                buffer[1] = functionCode;  //功能码
                buffer[2] = BitConverter.GetBytes(address)[1];
                buffer[3] = BitConverter.GetBytes(address)[0];//寄存器地址
                buffer[4] = (byte)(values.Length / 2 / 256);
                buffer[5] = (byte)(values.Length / 2 % 256);//写寄存器数量(除2是两个字节一个寄存器，寄存器16位。除以256是byte最大存储255。)              
                buffer[6] = (byte)(values.Length);          //写字节的个数
                values.CopyTo(buffer, 7);                   //把目标值附加到数组后面
                return buffer;
            }
            else
            {

                byte[] buffer = new byte[6];
                buffer[0] = stationNumber;//站号
                buffer[1] = functionCode; //功能码
                buffer[2] = BitConverter.GetBytes(address)[1];
                buffer[3] = BitConverter.GetBytes(address)[0];//寄存器地址
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
        /// <param name="modbusHeader.StationNumber">站号</param>
        /// <param name="modbusHeader.FunctionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCoilCommand(ushort address, bool value, byte stationNumber, byte functionCode)
        {
            address = TranPLCAddress(address);
            byte[] buffer = new byte[6];
            buffer[0] = stationNumber;//站号
            buffer[1] = functionCode; //功能码
            buffer[2] = BitConverter.GetBytes(address)[1];
            buffer[3] = BitConverter.GetBytes(address)[0];//寄存器地址
            buffer[4] = (byte)(value ? 0xFF : 0x00);     //此处只可以是FF表示闭合00表示断开，其他数值非法
            buffer[5] = 0x00;
            return buffer;
        }


        /// <summary>
        /// 获取线圈写入命令
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="values"></param>
        /// <param name="modbusHeader.StationNumber">站号</param>
        /// <param name="modbusHeader.FunctionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCoilCommand(ushort address, bool[] values, byte stationNumber, byte functionCode, byte[] check = null)
        {
            address = TranPLCAddress(address);
            int length = (values.Length + 1) / 2;
            byte[] newValue = values.ToByte();

            byte[] buffer = new byte[7 + newValue.Length];
            buffer[0] = stationNumber; //站号
            buffer[1] = functionCode;  //功能码
            buffer[2] = BitConverter.GetBytes(address)[1];
            buffer[3] = BitConverter.GetBytes(address)[0];//寄存器地址
            buffer[4] = (byte)(values.Length / 2 / 256);
            buffer[5] = (byte)(values.Length / 2 % 256);//写寄存器数量(除2是两个字节一个寄存器，寄存器16位。除以256是byte最大存储255。)              
            buffer[6] = (byte)(newValue.Length);          //写字节的个数
            newValue.CopyTo(buffer, 7);                   //把目标值附加到数组后面
            return buffer;

        }


        #endregion


        #region 基本读写
        /// <summary>
        /// 读取串口
        /// </summary>
        /// <param name="socket">socket</param>
        /// <param name="receiveCount">读取长度</param>          
        /// <returns></returns>
        public virtual OperationResult<byte[]> ReadBuffer()
        {
            OperationResult<byte[]> result = new OperationResult<byte[]>();
            DateTime beginTime = DateTime.Now;
            var tempBufferLength = _serialPort.BytesToRead;
            //在(没有取到数据或BytesToRead在继续读取)且没有超时的情况，延时处理
            while ((_serialPort.BytesToRead == 0 || tempBufferLength != _serialPort.BytesToRead) && DateTime.Now - beginTime <= TimeSpan.FromMilliseconds(ReceiveTimeout.TotalMilliseconds))
            {
                tempBufferLength = _serialPort.BytesToRead;
                //延时处理
                Thread.Sleep(WaiteInterval);
            }
            byte[] buffer = new byte[_serialPort.BytesToRead];
            result.Value = new byte[buffer.Length];
            var receiveFinish = 0;
            while (receiveFinish < buffer.Length)
            {
                var readLeng = _serialPort.Read(buffer, receiveFinish, buffer.Length);
                if (readLeng == 0)
                {
                    result.Value = null;
                    return result.Complete();
                }
                result.Value = new byte[readLeng];
                Array.Copy(buffer, receiveFinish, result.Value, receiveFinish, readLeng);
                receiveFinish += readLeng;

            }

            string printSend = $"{_serialPort.PortName} send:";
            for (int i = 0; i < buffer.Length; i++)
            {
                printSend = printSend + " " + buffer[i].ToString("X").PadLeft(2, '0'); ;

            }
            Logger?.LogDebug(printSend);

            return result.Complete();
        }


        /// <summary>
        /// 读取串口
        /// </summary>
        /// <param name="socket">socket</param>
        /// <param name="receiveCount">读取长度</param>          
        /// <returns></returns>
        public virtual async ValueTask<OperationResult<byte[]>> ReadBufferAsync()
        {
            OperationResult<byte[]> result = new OperationResult<byte[]>();
            DateTime beginTime = DateTime.Now;
            var tempBufferLength = _serialPort.BytesToRead;
            //在(没有取到数据或BytesToRead在继续读取)且没有超时的情况，延时处理
            while ((_serialPort.BytesToRead == 0 || tempBufferLength != _serialPort.BytesToRead) && DateTime.Now - beginTime <= TimeSpan.FromMilliseconds(ReceiveTimeout.TotalMilliseconds))
            {
                tempBufferLength = _serialPort.BytesToRead;
                //延时处理
                await Task.Delay(WaiteInterval);
            }
            byte[] buffer = new byte[_serialPort.BytesToRead];
            result.Value = new byte[buffer.Length];

            var receiveFinish = 0;
            while (receiveFinish < buffer.Length)
            {
                var readLeng = await _serialPort.BaseStream.ReadAsync(buffer, receiveFinish, buffer.Length);
                if (readLeng == 0)
                {
                    result.Value = null;
                    return result.Complete();
                }
                Array.Copy(buffer, receiveFinish, result.Value, receiveFinish, readLeng);
                receiveFinish += readLeng;
            }

            string printSend = $"{_serialPort.PortName} send:";
            for (int i = 0; i < buffer.Length; i++)
            {
                printSend = printSend + " " + buffer[i].ToString("X").PadLeft(2, '0'); ;

            }
            Logger?.LogDebug(printSend);

            return result.Complete();
        }

        internal override OperationResult<byte[]> ExchangingMessages(byte[] command)
        {
            OperationResult<byte[]> sendPackage()
            {
                //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
                _serialPort.Write(command, 0, command.Length);
                string printSend = $"{_serialPort.PortName} send:";
                for (int i = 0; i < command.Length; i++)
                {
                    printSend = printSend + " " + command[i].ToString("X").PadLeft(2, '0'); ;

                }
                Logger?.LogDebug(printSend);


                //获取响应报文
                return ReadBuffer();
            }

            OperationResult<byte[]> result = new OperationResult<byte[]>();
            try
            {
                result = sendPackage();
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    result = result.SetInfo(sendPackage());
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                WarningLog?.Invoke(ex.Message, ex);
            }
            return result;
        }

        internal override async ValueTask<OperationResult<byte[]>> ExchangingMessagesAsync(byte[] command)
        {
            async ValueTask<OperationResult<byte[]>> sendPackage()
            {
                //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
                await _serialPort.BaseStream.WriteAsync(command, 0, command.Length);
                string printSend = $"{_serialPort.PortName} send:";
                for (int i = 0; i < command.Length; i++)
                {
                    printSend = printSend + " " + command[i].ToString("X").PadLeft(2, '0'); ;

                }
                Logger?.LogDebug(printSend);

                //获取响应报文
                return await ReadBufferAsync();

            }

            OperationResult<byte[]> result = new OperationResult<byte[]>();
            try
            {
                result = await sendPackage();
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    result = result.SetInfo(await sendPackage());
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                WarningLog?.Invoke(ex.Message, ex);
                //如果出现异常，则进行一次重试
                //重新打开连接
            }
            return result;
        }

        /// <summary>
        /// 发送报文，并获取响应报文（如果网络异常，会自动进行一次重试）
        /// TODO 重试机制应改成用户主动设置
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal override OperationResult<byte[]> InterpretMessageData(byte[] command)
        {
            var result = new OperationResult<byte[]>();
            try
            {
                result = ExchangingMessages(command);
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    result.Message = "设备响应异常";
                    return result.Complete();
                }
                else
                {
                    return result.Complete();

                }
            }
            catch (Exception ex)
            {
                WarningLog?.Invoke(ex.Message, ex);
                result.IsSuccess = false;
                result.Message = ex.Message;
                return result.Complete();

            }
        }




        /// <summary>
        /// 发送报文，并获取响应报文（如果网络异常，会自动进行一次重试）
        /// TODO 重试机制应改成用户主动设置
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal override async ValueTask<OperationResult<byte[]>> InterpretMessageDataAsync(byte[] command)
        {
            var result = new OperationResult<byte[]>();

            try
            {
                result = await ExchangingMessagesAsync(command);
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    result.Message = "设备响应异常";
                }
                return result.Complete();
            }
            catch (Exception ex)
            {
                WarningLog?.Invoke(ex.Message, ex);
                result.IsSuccess = false;
                result.Message = ex.Message;
                return result.Complete();

            }
        }


        /// <summary>
        /// 丢弃来自串行驱动程序的接收缓冲区的数据
        /// </summary>
        public void DiscardInBuffer() => _serialPort?.DiscardInBuffer();

        /// <summary>
        /// 丢弃来自串行驱动程序的传输缓冲区的数据
        /// </summary>
        public void DiscardOutBuffer() => _serialPort?.DiscardOutBuffer();

        #endregion
    }
}

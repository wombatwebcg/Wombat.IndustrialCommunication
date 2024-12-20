﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;

using Wombat.Network.Sockets;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Socket基类
    /// </summary>
    public abstract class ModbusClientEthernetBase : ModbusClient, IEthernetClient
    {
        public IPEndPoint IpEndPoint { get; set; }
        public string ClientName { get; set; }

        protected internal SocketClientBase _socket;
        public override bool Connected => _socket == null ? false : _socket.Connected;





        /// <summary>
        /// 
        /// </summary>
        /// <param name="ipAndPoint"></param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="format">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        /// <param name="plcAddresses">PLC地址</param>
        public ModbusClientEthernetBase()
        {
            _socket = new SocketClientBase();
            _socket.SocketConfiguration.ConnectTimeout = ConnectTimeout;
            _socket.SocketConfiguration.ReceiveTimeout = SendTimeout;
            _socket.SocketConfiguration.SendTimeout = ReceiveTimeout;


        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="ipAndPoint"></param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="format">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        /// <param name="plcAddresses">PLC地址</param>
        public ModbusClientEthernetBase(IPEndPoint ipAndPoint) : this()
        {
            IpEndPoint = ipAndPoint;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="format">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        public ModbusClientEthernetBase(string ip, int port) : this()
        {
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            IpEndPoint = new IPEndPoint(address, port);
        }




        internal override OperationResult DoConnect()
        {
            var result = new OperationResult();
            _socket?.Close(); ;
            try
            {
                _socket?.Connect(IpEndPoint);
            }
            catch (Exception ex)
            {
                _socket?.Close();
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.ErrorCode = 408;
                result.Exception = ex;
            }
            return result.Complete();
        }

        internal override async Task<OperationResult> DoConnectAsync()
        {
            _socket?.CloseAsync();
            var result = new OperationResult();
            try
            {
                await _socket?.ConnectAsync(IpEndPoint);
            }
            catch (Exception ex)
            {
                await _socket?.CloseAsync(); ;
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.ErrorCode = 408;
                result.Exception = ex;
            }
            return result.Complete();
        }

        internal override OperationResult DoDisconnect()
        {
            OperationResult result = new OperationResult();
            try
            {
                _socket?.Close();
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.Exception = ex;
                return result;
            }

        }



        internal override async Task<OperationResult> DoDisconnectAsync()
        {
            OperationResult result = new OperationResult();
            try
            {
                await _socket?.CloseAsync();
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.Exception = ex;
                return result;
            }

        }



        /// <summary>
        /// 发送报文，并获取响应报文（建议使用SendPackageReliable，如果异常会自动重试一次）
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal override OperationResult<byte[]> ExchangingMessages(byte[] command)
        {
            //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
            OperationResult<byte[]> result = new OperationResult<byte[]>();
            try
            {
                _socket.Send(command);
                var socketReadResult = ReadBuffer(8);
                if (!socketReadResult.IsSuccess)
                    return socketReadResult;
                var headPackage = socketReadResult.Value;
                int length = headPackage[4] * 256 + headPackage[5] - 2;
                socketReadResult = ReadBuffer(length);
                if (!socketReadResult.IsSuccess)
                    return socketReadResult;
                var dataPackage = socketReadResult.Value;

                result.Value = headPackage.Concat(dataPackage).ToArray();
                return result.Complete();
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;

                return result.Complete();
            }
        }


        /// <summary>
        /// 发送报文，并获取响应报文（建议使用SendPackageReliable，如果异常会自动重试一次）
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal override async ValueTask<OperationResult<byte[]>> ExchangingMessagesAsync(byte[] command)
        {
            //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
            OperationResult<byte[]> result = new OperationResult<byte[]>();
            try
            {
                await _socket.SendAsync(command);
                var socketReadResult = ReadBuffer(8);
                if (!socketReadResult.IsSuccess)
                    return socketReadResult;
                var headPackage = socketReadResult.Value;
                int length = headPackage[4] * 256 + headPackage[5] - 2;
                socketReadResult = await ReadBufferAsync(length);
                if (!socketReadResult.IsSuccess)
                    return socketReadResult;
                var dataPackage = socketReadResult.Value;

                result.Value = headPackage.Concat(dataPackage).ToArray();
                return result.Complete();
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;

                return result.Complete();
            }
        }


        #region Read 读取
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
        /// <param name="readLength">读取长度</param>
        /// <param name="byteFormatting">大小端转换</param>
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
                                connectResult.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{connectResult.Message}";
                                return result.SetInfo(connectResult);
                            }
                        }
                        var chenkHead = GetCheckHead(modbusHeader.FunctionCode);
                        //1 获取命令（组装报文）
                        byte[] command = GetReadCommand(modbusHeader.Address, modbusHeader.StationNumber, modbusHeader.FunctionCode, (ushort)readLength, chenkHead);
                        result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));

                        //获取响应报文
                        var sendResult = InterpretMessageData(command);
                        if (!sendResult.IsSuccess)
                        {
                            sendResult.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{sendResult.Message}";
                            if (!IsLongLivedConnection) Disconnect();
                            return result.SetInfo(sendResult).Complete();
                        }
                        var dataPackage = sendResult.Value;
                        byte[] resultBuffer = new byte[dataPackage.Length - 9];
                        Array.Copy(dataPackage, 9, resultBuffer, 0, resultBuffer.Length);
                        result.Responses.Add(string.Join(" ", dataPackage.Select(t => t.ToString("X2"))));
                        //4 获取响应报文数据（字节数组形式）             
                        result.Value = resultBuffer.ToArray();

                        if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                        {
                            result.IsSuccess = false;
                            result.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。响应结果校验失败";
                        }
                        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, dataPackage[7]))
                        {
                            result.IsSuccess = false;
                            result.Message = ModbusHelper.ErrMsg(dataPackage[8]);
                        }
                    }
                    catch (SocketException ex)
                    {
                        result.IsSuccess = false;
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            result.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。连接超时";
                        }
                        else
                        {
                            result.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{ex.Message}";
                        }
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
        /// 读取数据
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
        /// <param name="readLength">读取长度</param>
        /// <param name="byteFormatting">大小端转换</param>
        /// <returns></returns>
        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int readLength = 1, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult<byte[]>();
                if (ModbusAddressParser.TryParseModbusHeader(address,out var modbusHeader))
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
                        var chenkHead = GetCheckHead(modbusHeader.FunctionCode);
                        //1 获取命令（组装报文）
                        byte[] command = GetReadCommand(modbusHeader.Address, modbusHeader.StationNumber, modbusHeader.FunctionCode, (ushort)readLength, chenkHead);
                        result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));
                        //获取响应报文
                        var sendResult = await InterpretMessageDataAsync(command);
                        if (!sendResult.IsSuccess)
                        {
                            sendResult.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{sendResult.Message}";
                            if (!IsLongLivedConnection) await DisconnectAsync();
                            return result.SetInfo(sendResult).Complete();
                        }
                        var dataPackage = sendResult.Value;
                        byte[] resultBuffer = new byte[dataPackage.Length - 9];
                        Array.Copy(dataPackage, 9, resultBuffer, 0, resultBuffer.Length);
                        result.Responses.Add(string.Join(" ", dataPackage.Select(t => t.ToString("X2"))));
                        //4 获取响应报文数据（字节数组形式）             
                        result.Value = resultBuffer.ToArray();

                        if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                        {
                            result.IsSuccess = false;
                            result.Message = $"读取 地址:{modbusHeader.Address} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。响应结果校验失败";
                        }
                        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, dataPackage[7]))
                        {
                            result.IsSuccess = false;
                            result.Message = ModbusHelper.ErrMsg(dataPackage[8]);
                        }
                    }
                    catch (SocketException ex)
                    {
                        result.IsSuccess = false;
                        //if (ex.SocketErrorCode == SocketError.TimedOut)
                        //{
                        //    result.Message = $"读取 地址:{modbusHeader.RegisterAddress} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。连接超时";
                        //}
                        //else
                        //{
                        //    result.Message = $"读取 地址:{modbusHeader.RegisterAddress} 站号:{modbusHeader.StationNumber} 功能码:{modbusHeader.FunctionCode} 失败。{ ex.Message}";
                        //}
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



        /// <summary>
        /// 按位的方式读取
        /// </summary>
        /// <param name="address">寄存器地址:如1.00 ... 1.14、1.15</param>
        /// <param name="modbusHeader.StationNumber">站号</param>
        /// <param name="modbusHeader.FunctionCode">功能码</param>
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
        /// <param name="modbusHeader.StationNumber">站号</param>
        /// <param name="modbusHeader.FunctionCode">功能码</param>
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
                    result.Requsts.Add(tempOperationResult.Requsts.FirstOrDefault());
                    result.Responses.Add(tempOperationResult.Responses.FirstOrDefault());
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
                    result.Message="modbus地址格式错误,参考格式为:1;3;0,表示1号站，3号功能码，0地址";
                    result.IsSuccess =true;
                    return result.Complete();

                }

                var tempOperationResult = Read(splicingAddress, length);

                result.Requsts.Add(tempOperationResult.Requsts.FirstOrDefault());
                result.Responses.Add(tempOperationResult.Responses.FirstOrDefault());
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
                    result.Requsts.Add(tempOperationResult.Requsts.FirstOrDefault());
                    result.Responses.Add(tempOperationResult.Responses.FirstOrDefault());
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

                result.Requsts.Add(tempOperationResult.Requsts.FirstOrDefault());
                result.Responses.Add(tempOperationResult.Responses.FirstOrDefault());
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
                    result = await BatchReadBaseAsync(addresses);
                }
                else
                    break;
            }
            return result;
        }

        #endregion

        #region 写入
        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address">写入地址</param>
        /// <param name="value"></param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
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
                                return result.SetInfo(connectResult);
                            }
                        }
                        var chenkHead = GetCheckHead(modbusHeader.FunctionCode);
                        var command = GetWriteCoilCommand(modbusHeader.Address, value, modbusHeader.StationNumber, modbusHeader.FunctionCode, chenkHead);
                        result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));
                        var sendResult = InterpretMessageData(command);
                        if (!sendResult.IsSuccess)
                        {
                            if (!IsLongLivedConnection) Disconnect();
                            return result.SetInfo(sendResult).Complete();
                        }
                        var dataPackage = sendResult.Value;
                        result.Responses.Add(string.Join(" ", dataPackage.Select(t => t.ToString("X2"))));
                        if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                        {
                            result.IsSuccess = false;
                            result.Message = "响应结果校验失败";
                        }
                        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, dataPackage[7]))
                        {
                            result.IsSuccess = false;
                            result.Message = ModbusHelper.ErrMsg(dataPackage[8]);
                        }
                    }
                    catch (SocketException ex)
                    {
                        result.IsSuccess = false;
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            result.Message = "连接超时";
                        }
                        else
                        {
                            result.Message = ex.Message;
                        }
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
        /// <param name="address">写入地址</param>
        /// <param name="value"></param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
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
                                return result.SetInfo(connectResult);
                            }
                        }
                        var chenkHead = GetCheckHead(modbusHeader.FunctionCode);
                        var command = GetWriteCoilCommand(modbusHeader.Address, value, modbusHeader.StationNumber, modbusHeader.FunctionCode, chenkHead);
                        result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));
                        var sendResult = await InterpretMessageDataAsync(command);
                        if (!sendResult.IsSuccess)
                        {
                            if (!IsLongLivedConnection) await DisconnectAsync();
                            return result.SetInfo(sendResult).Complete();
                        }
                        var dataPackage = sendResult.Value;
                        result.Responses.Add(string.Join(" ", dataPackage.Select(t => t.ToString("X2"))));
                        if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                        {
                            result.IsSuccess = false;
                            result.Message = "响应结果校验失败";
                        }
                        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, dataPackage[7]))
                        {
                            result.IsSuccess = false;
                            result.Message = ModbusHelper.ErrMsg(dataPackage[8]);
                        }
                    }
                    catch (SocketException ex)
                    {
                        result.IsSuccess = false;
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            result.Message = "连接超时";
                        }
                        else
                        {
                            result.Message = ex.Message;
                        }
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
                                return result.SetInfo(connectResult);

                            }
                        }
                        var chenkHead = GetCheckHead(modbusHeader.FunctionCode);
                        var command = GetWriteCoilCommand(modbusHeader.Address, value, modbusHeader.StationNumber, modbusHeader.FunctionCode, chenkHead);
                        result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));
                        var sendResult = InterpretMessageData(command);
                        if (!sendResult.IsSuccess)
                        {
                            if (!IsLongLivedConnection) Disconnect();
                            return result.SetInfo(sendResult).Complete();
                        }
                        var dataPackage = sendResult.Value;
                        result.Responses.Add(string.Join(" ", dataPackage.Select(t => t.ToString("X2"))));
                        if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                        {
                            result.IsSuccess = false;
                            result.Message = "响应结果校验失败";
                        }
                        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, dataPackage[7]))
                        {
                            result.IsSuccess = false;
                            result.Message = ModbusHelper.ErrMsg(dataPackage[8]);
                        }
                    }
                    catch (SocketException ex)
                    {
                        result.IsSuccess = false;
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            result.Message = "连接超时";
                        }
                        else
                        {
                            result.Message = ex.Message;
                        }
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
                                return result.SetInfo(connectResult);
                            }
                        }
                        var chenkHead = GetCheckHead(modbusHeader.FunctionCode);
                        var command = GetWriteCoilCommand(modbusHeader.Address, value, modbusHeader.StationNumber, modbusHeader.FunctionCode, chenkHead);
                        result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));
                        var sendResult = await InterpretMessageDataAsync(command);
                        if (!sendResult.IsSuccess)
                        {
                            if (!IsLongLivedConnection) await DisconnectAsync();
                            return result.SetInfo(sendResult).Complete();
                        }
                        var dataPackage = sendResult.Value;
                        result.Responses.Add(string.Join(" ", dataPackage.Select(t => t.ToString("X2"))));
                        if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                        {
                            result.IsSuccess = false;
                            result.Message = "响应结果校验失败";
                        }
                        else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, dataPackage[7]))
                        {
                            result.IsSuccess = false;
                            result.Message = ModbusHelper.ErrMsg(dataPackage[8]);
                        }
                    }
                    catch (SocketException ex)
                    {
                        result.IsSuccess = false;
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            result.Message = "连接超时";
                            await _socket?.CloseAsync();
                        }
                        else
                        {
                            result.Message = ex.Message;
                        }
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


        internal override OperationResult Write(string address, byte[] data, bool isBit = false)
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
                            return result.SetInfo(connectResult);
                        }
                    }
                    var chenkHead = GetCheckHead(modbusHeader.FunctionCode);
                    var command = GetWriteCommand(modbusHeader.Address, data, modbusHeader.StationNumber, modbusHeader.FunctionCode, chenkHead);
                    result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));
                    var sendResult = InterpretMessageData(command);
                    if (!sendResult.IsSuccess)
                    {
                        if (!IsLongLivedConnection) Disconnect();
                        return result.SetInfo(sendResult).Complete();
                    }
                    var dataPackage = sendResult.Value;
                    result.Responses.Add(string.Join(" ", dataPackage.Select(t => t.ToString("X2"))));

                    if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果校验失败";
                    }
                    else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, dataPackage[7]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(dataPackage[8]);
                    }
                }
                catch (SocketException ex)
                {
                    result.IsSuccess = false;
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        result.Message = "连接超时";
                    }
                    else
                    {
                        result.Message = ex.Message;
                    }
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

        internal async override Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
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
                            return result.SetInfo(connectResult);
                        }
                    }
                    var chenkHead = GetCheckHead(modbusHeader.FunctionCode);
                    var command = GetWriteCommand(modbusHeader.Address, data, modbusHeader.StationNumber, modbusHeader.FunctionCode, chenkHead);
                    result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));
                    var sendResult = await InterpretMessageDataAsync(command);
                    if (!sendResult.IsSuccess)
                    {
                        if (!IsLongLivedConnection) await DisconnectAsync();
                        return result.SetInfo(sendResult).Complete();
                    }
                    var dataPackage = sendResult.Value;
                    result.Responses.Add(string.Join(" ", dataPackage.Select(t => t.ToString("X2"))));
                    if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果校验失败";
                    }
                    else if (ModbusHelper.VerifyFunctionCode(modbusHeader.FunctionCode, dataPackage[7]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(dataPackage[8]);
                    }
                }
                catch (SocketException ex)
                {
                    result.IsSuccess = false;
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        result.Message = "连接超时";
                    }
                    else
                    {
                        result.Message = ex.Message;
                    }
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

        /// <summary>
        /// 获取随机校验头
        /// </summary>
        /// <returns></returns>
        private byte[] GetCheckHead(int seed)
        {
            var random = new Random(DateTime.Now.Millisecond + seed);
            return new byte[] { (byte)random.Next(255), (byte)random.Next(255) };
        }

        public virtual ushort TranPLCAddress(ushort address)
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
        public byte[] GetReadCommand(ushort address, byte StationNumber, byte FunctionCode, ushort length, byte[] check = null)
        {
            //if (_isPlcAddress) { address = TranPLCAddress(address); }
            address = TranPLCAddress(address);
            byte[] buffer = new byte[12];
            buffer[0] = check?[0] ?? 0x19;
            buffer[1] = check?[1] ?? 0xB2;//Client发出的检验信息
            buffer[2] = 0x00;
            buffer[3] = 0x00;//表示tcp/ip 的协议的Modbus的协议
            buffer[4] = 0x00;
            buffer[5] = 0x06;//表示的是该字节以后的字节长度

            buffer[6] = StationNumber;  //站号
            buffer[7] = FunctionCode;   //功能码
            buffer[8] = BitConverter.GetBytes(address)[1];
            buffer[9] = BitConverter.GetBytes(address)[0];//寄存器地址
            buffer[10] = BitConverter.GetBytes(length)[1];
            buffer[11] = BitConverter.GetBytes(length)[0];//表示request 寄存器的长度(寄存器个数)
            return buffer;
        }

        /// <summary>
        /// 获取写入命令
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="values">批量读取的值</param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
        /// <returns></returns>
        //public byte[] GetWriteOneCommand(string address, byte[] values, byte StationNumber, byte FunctionCode, byte[] check = null)
        //{
        //    if (_isPlcAddress) { address = TranPLCAddress(address); }
        //    var writeAddress = ushort.Parse(address?.Trim());
        //    byte[] buffer = new byte[12];
        //    buffer[0] = check?[0] ?? 0x19;
        //    buffer[1] = check?[1] ?? 0xB2;//Client发出的检验信息     
        //    buffer[4] = 0x00;
        //    buffer[5] = 0x06;//表示的是该字节以后的字节长度

        //    buffer[6] = StationNumber;//站号
        //    buffer[7] = FunctionCode; //功能码
        //    buffer[8] = BitConverter.GetBytes(writeAddress)[1];
        //    buffer[9] = BitConverter.GetBytes(writeAddress)[0];//寄存器地址
        //    buffer[10] = values[0];     //此处只可以是FF表示闭合00表示断开，其他数值非法
        //    buffer[11] = values[1];
        //    return buffer;
        //}


        /// <summary>
        /// 获取写入命令
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="values">批量读取的值</param>
        /// <param name="StationNumber">站号</param>
        /// <param name="FunctionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCommand(ushort address, byte[] values, byte StationNumber, byte FunctionCode, byte[] check = null)
        {
            address = TranPLCAddress(address);

            if (values.Length > 2)
            {
                byte[] buffer = new byte[13 + values.Length];
                buffer[0] = check?[0] ?? 0x19;
                buffer[1] = check?[1] ?? 0xB2;//检验信息，用来验证response是否串数据了           
                buffer[4] = BitConverter.GetBytes(7 + values.Length)[1];
                buffer[5] = BitConverter.GetBytes(7 + values.Length)[0];//表示的是header handle后面还有多长的字节

                buffer[6] = StationNumber; //站号
                buffer[7] = FunctionCode;  //功能码
                buffer[8] = BitConverter.GetBytes(address)[1];
                buffer[9] = BitConverter.GetBytes(address)[0];//寄存器地址
                buffer[10] = (byte)(values.Length / 2 / 256);
                buffer[11] = (byte)(values.Length / 2 % 256);//写寄存器数量(除2是两个字节一个寄存器，寄存器16位。除以256是byte最大存储255。)              
                buffer[12] = (byte)(values.Length);          //写字节的个数
                values.CopyTo(buffer, 13);                   //把目标值附加到数组后面
                return buffer;

            }
            else
            {
                byte[] buffer = new byte[12];
                buffer[0] = check?[0] ?? 0x19;
                buffer[1] = check?[1] ?? 0xB2;//Client发出的检验信息     
                buffer[4] = 0x00;
                buffer[5] = 0x06;//表示的是该字节以后的字节长度

                buffer[6] = StationNumber;//站号
                buffer[7] = FunctionCode; //功能码
                buffer[8] = BitConverter.GetBytes(address)[1];
                buffer[9] = BitConverter.GetBytes(address)[0];//寄存器地址
                buffer[10] = values[0];     //此处只可以是FF表示闭合00表示断开，其他数值非法
                buffer[11] = values[1];
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
        public byte[] GetWriteCoilCommand(ushort address, bool value, byte StationNumber, byte FunctionCode, byte[] check = null)
        {
            address = TranPLCAddress(address);
            byte[] buffer = new byte[12];
            buffer[0] = check?[0] ?? 0x19;
            buffer[1] = check?[1] ?? 0xB2;//Client发出的检验信息     
            buffer[4] = 0x00;
            buffer[5] = 0x06;//表示的是该字节以后的字节长度

            buffer[6] = StationNumber;//站号
            buffer[7] = FunctionCode; //功能码
            buffer[8] = BitConverter.GetBytes(address)[1];
            buffer[9] = BitConverter.GetBytes(address)[0];//寄存器地址
            buffer[10] = (byte)(value ? 0xFF : 0x00);     //此处只可以是FF表示闭合00表示断开，其他数值非法
            buffer[11] = 0x00;
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
        public byte[] GetWriteCoilCommand(ushort address, bool[] values, byte StationNumber, byte FunctionCode, byte[] check = null)
        {
            address = TranPLCAddress(address);
            int length = (values.Length + 1) / 2;
            byte[] newValue = values.ToByte();

            byte[] buffer = new byte[13 + newValue.Length];
            buffer[0] = check?[0] ?? 0x19;
            buffer[1] = check?[1] ?? 0xB2;//检验信息，用来验证response是否串数据了           
            buffer[4] = BitConverter.GetBytes(7 + newValue.Length)[1];
            buffer[5] = BitConverter.GetBytes(7 + newValue.Length)[0];//表示的是header handle后面还有多长的字节

            buffer[6] = StationNumber; //站号
            buffer[7] = FunctionCode;  //功能码
            buffer[8] = BitConverter.GetBytes(address)[1];
            buffer[9] = BitConverter.GetBytes(address)[0];//寄存器地址
            buffer[10] = (byte)(values.Length / 256);
            buffer[11] = (byte)(values.Length % 256);//写寄存器数量(除2是两个字节一个寄存器，寄存器16位。除以256是byte最大存储255。)              
            buffer[12] = (byte)(newValue.Length);          //写字节的个数
            newValue.CopyTo(buffer, 13);                   //把目标值附加到数组后面
            return buffer;
        }


        #endregion

        #region 收发命令
        /// <summary>
        /// Socket读取
        /// </summary>
        /// <param name="socket">socket</param>
        /// <param name="receiveCount">读取长度</param>          
        /// <returns></returns>
        internal virtual OperationResult<byte[]> ReadBuffer(int receiveCount)
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
                int receiveLength = (receiveCount - receiveFinish) >= _socket.SocketConfiguration.ReceiveBufferSize ? _socket.SocketConfiguration.ReceiveBufferSize : (receiveCount - receiveFinish);
                try
                {
                    var readLeng = _socket.Receive(receiveBytes, receiveFinish, receiveLength);
                    if (readLeng == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = $"连接被断开";
                        return result;
                    }
                    receiveFinish += readLeng;
                }
                catch (SocketException ex)
                {
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
        /// Socket读取
        /// </summary>
        /// <param name="socket">socket</param>
        /// <param name="receiveCount">读取长度</param>          
        /// <returns></returns>
        internal virtual async ValueTask<OperationResult<byte[]>> ReadBufferAsync(int receiveCount)
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
                int receiveLength = (receiveCount - receiveFinish) >= _socket.SocketConfiguration.ReceiveBufferSize ? _socket.SocketConfiguration.ReceiveBufferSize : (receiveCount - receiveFinish);
                try
                {
                    var readLeng = await _socket.ReceiveAsync(receiveBytes, receiveFinish, receiveLength);
                    if (readLeng == 0)
                    {
                        result.IsSuccess = false;
                        result.Message = $"连接被断开";

                        return result;
                    }
                    receiveFinish += readLeng;
                }
                catch (SocketException ex)
                {
                    _socket?.Close();
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
        #endregion

    }
}

using Microsoft.Extensions.Logging;
using System;
using System.IO.Ports;
using System.Linq;
using Wombat.Infrastructure;
using Wombat.Core;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// ModbusRtu协议客户端
    /// </summary>
    public abstract class ModbusSerialPortBase : ModbusSerialPortDeviceBase
    {


        public override bool Connected => _serialPort == null ? false : _serialPort.IsOpen;


        private AsyncLock _lock;

        /// <summary>
        /// 是否自动打开关闭
        /// </summary>
        protected bool _isAutoOpen = true;


        public ModbusSerialPortBase()
        {
            _lock = new AsyncLock();
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


        internal override OperationResult<byte[]> GetMessageContent(byte[] command)
        {
            throw new NotImplementedException();
        }

        internal override ValueTask<OperationResult<byte[]>> GetMessageContentAsync(byte[] command)
        {
            throw new NotImplementedException();    
        }

        #region  Read 读取
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="readLength">读取长度</param>
        /// <param name="byteFormatting"></param>
        /// <returns></returns>
        public override OperationResult<byte[]> Read(string address, int readLength = 1, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult<byte[]>();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    //获取命令（组装报文）
                    byte[] command = GetReadCommand(address, stationNumber, functionCode, (ushort)readLength, isPlcAddress: isPlcAddress);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));

                    //发送命令并获取响应报文
                    var sendResult = InterpretAndExtractMessageData(commandCRC16);
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
                    else if (ModbusHelper.VerifyFunctionCode(functionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }

                    byte[] resultData = new byte[responsePackage.Length - 2 - 3];
                    Array.Copy(responsePackage, 3, resultData, 0, resultData.Length);
                    result.Responses[0] = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
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
                    if (!IsUseLongConnect) Disconnect();
                }
                return result.Complete();
            }
        }


        public override async Task<OperationResult<byte[]>> ReadAsync(string address, int readLength = 1, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult<byte[]>();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult =await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    //获取命令（组装报文）
                    byte[] command = GetReadCommand(address, stationNumber, functionCode, (ushort)readLength, isPlcAddress: isPlcAddress);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));

                    //发送命令并获取响应报文
                    var sendResult =await InterpretAndExtractMessageDataAsync(commandCRC16);
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
                    else if (ModbusHelper.VerifyFunctionCode(functionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }

                    byte[] resultData = new byte[responsePackage.Length - 2 - 3];
                    Array.Copy(responsePackage, 3, resultData, 0, resultData.Length);
                    result.Responses[0] = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
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
                    if (!IsUseLongConnect)await DisconnectAsync();
                }
                return result.Complete();
            }
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
        public override OperationResult Write(string address, bool value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = false)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCoilCommand(address, value, stationNumber, functionCode, isPlcAddress: isPlcAddress);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));
                    //发送命令并获取响应报文
                    var sendResult = InterpretAndExtractMessageData(commandCRC16);
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
                    else if (ModbusHelper.VerifyFunctionCode(functionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Buffer.BlockCopy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses[0] = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsUseLongConnect) Disconnect();
                }
                return result.Complete();
            }
        }


        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        public override async Task<OperationResult> WriteAsync(string address, bool value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = false)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult =await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCoilCommand(address, value, stationNumber, functionCode, isPlcAddress: isPlcAddress);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));
                    //发送命令并获取响应报文
                    var sendResult =await InterpretAndExtractMessageDataAsync(commandCRC16);
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
                    else if (ModbusHelper.VerifyFunctionCode(functionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Buffer.BlockCopy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses[0] = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsUseLongConnect)await DisconnectAsync();
                }
                return result.Complete();
            }
        }



        public override OperationResult Write(string address, bool[] value, byte stationNumber = 1, byte functionCode = 0X0F, bool isPlcAddress = false)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCoilCommand(address, value, stationNumber, functionCode, isPlcAddress: isPlcAddress);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));
                    //发送命令并获取响应报文
                    var sendResult = InterpretAndExtractMessageData(commandCRC16);
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
                    else if (ModbusHelper.VerifyFunctionCode(functionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Buffer.BlockCopy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses[0] = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsUseLongConnect) Disconnect();
                }
                return result.Complete();
            }
        }

        public override async Task<OperationResult> WriteAsync(string address, bool[] value, byte stationNumber = 1, byte functionCode = 0X0F, bool isPlcAddress = false)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult =await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCoilCommand(address, value, stationNumber, functionCode, isPlcAddress: isPlcAddress);
                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));
                    //发送命令并获取响应报文
                    var sendResult =await InterpretAndExtractMessageDataAsync(commandCRC16);
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
                    else if (ModbusHelper.VerifyFunctionCode(functionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Buffer.BlockCopy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses[0] = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsUseLongConnect)await DisconnectAsync();
                }
                return result.Complete();
            }
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        //public override OperationResult Write(string address, byte value, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        //{
        //    _advancedHybirdLock.Enter();
        //    var result = new OperationResult();
        //    if (!_serialPortBase?.IsConnect ?? true)
        //    {
        //        var connectResult = Connect();
        //        if (!connectResult.IsSuccess)
        //        {
        //            connectResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ connectResult.Message}";
        //            _advancedHybirdLock.Leave();
        //            return result.SetInfo(connectResult);
        //        }
        //    }
        //    try
        //    {
        //        var command = GetWriteCommand(address, value, stationNumber, functionCode, isPlcAddress: isPlcAddress);

        //        var commandCRC16 = CRC16Helper.GetCRC16(command);
        //        result.Requst = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));
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
        //        else if (ModbusHelper.VerifyFunctionCode(functionCode, responsePackage[1]))
        //        {
        //            result.IsSuccess = false;
        //            result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
        //        }
        //        byte[] resultBuffer = new byte[responsePackage.Length - 2];
        //        Array.Copy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
        //        result.Response = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
        //    }
        //    catch (Exception ex)
        //    {
        //        result.IsSuccess = false;
        //        result.Message = ex.Message;
        //    }
        //    finally
        //    {
        //        if (!IsUseLongConnect) Disconnect();
        //    }
        //    _advancedHybirdLock.Leave();
        //    return result.Complete();
        //}



        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public override OperationResult Write(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCommand(address, values, stationNumber, functionCode, isPlcAddress: isPlcAddress);

                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));
                    var sendResult = InterpretAndExtractMessageData(commandCRC16);
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
                    else if (ModbusHelper.VerifyFunctionCode(functionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Array.Copy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses[0] = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsUseLongConnect) Disconnect();
                }
                return result.Complete();
            }
        }


        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public override async Task<OperationResult> WriteAsync(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult =await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ connectResult.Message}";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    var command = GetWriteCommand(address, values, stationNumber, functionCode, isPlcAddress: isPlcAddress);

                    var commandCRC16 = CRC16Helper.GetCRC16(command);
                    result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));
                    var sendResult =await InterpretAndExtractMessageDataAsync(commandCRC16);
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
                    else if (ModbusHelper.VerifyFunctionCode(functionCode, responsePackage[1]))
                    {
                        result.IsSuccess = false;
                        result.Message = ModbusHelper.ErrMsg(responsePackage[2]);
                    }
                    byte[] resultBuffer = new byte[responsePackage.Length - 2];
                    Array.Copy(responsePackage, 0, resultBuffer, 0, resultBuffer.Length);
                    result.Responses[0] = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsUseLongConnect)await DisconnectAsync();
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
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="length">读取长度</param>
        /// <returns></returns>
        public byte[] GetReadCommand(string address, byte stationNumber, byte functionCode, ushort length, bool isPlcAddress = false)
        {
            if (isPlcAddress) { address = TranPLCAddress(address); }
            var readAddress = ushort.Parse(address?.Trim());
            byte[] buffer = new byte[6];
            buffer[0] = stationNumber;  //站号
            buffer[1] = functionCode;   //功能码
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
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCommand(string address, byte value, byte stationNumber, byte functionCode, bool isPlcAddress = false)
        {

            if (isPlcAddress) { address = TranPLCAddress(address); }
            var readAddress = ushort.Parse(address?.Trim());
            byte[] buffer = new byte[6];
            buffer[0] = stationNumber;//站号
            buffer[1] = functionCode; //功能码
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
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCommand(string address, byte[] values, byte stationNumber, byte functionCode, bool isPlcAddress = false)
        {
            if (isPlcAddress) { address = TranPLCAddress(address); }
            var readAddress = ushort.Parse(address?.Trim());
            if (values.Length > 2)
            {
                byte[] buffer = new byte[7 + values.Length];
                buffer[0] = stationNumber; //站号
                buffer[1] = functionCode;  //功能码
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
                buffer[0] = stationNumber;//站号
                buffer[1] = functionCode; //功能码
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
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public  byte[] GetWriteCoilCommand(string address, bool value, byte stationNumber, byte functionCode, bool isPlcAddress = false)
        {
            if (isPlcAddress) { address = TranPLCAddress(address); }
            var readAddress = ushort.Parse(address?.Trim());
            byte[] buffer = new byte[6];
            buffer[0] = stationNumber;//站号
            buffer[1] = functionCode; //功能码
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
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCoilCommand(string address, bool[] values, byte stationNumber, byte functionCode, byte[] check = null, bool isPlcAddress = false)
        {
            if (isPlcAddress) { address = TranPLCAddress(address); }
            var writeAddress = ushort.Parse(address?.Trim());
            int length = (values.Length + 1) / 2;
            byte[] newValue = values.ToByte();

            byte[] buffer = new byte[7 + newValue.Length];
            buffer[0] = stationNumber; //站号
            buffer[1] = functionCode;  //功能码
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

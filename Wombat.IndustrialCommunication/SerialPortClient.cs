using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.Infrastructure;

namespace Wombat.IndustrialCommunication
{
    public class SerialPortClient:SerialPortDeviceBase
    {
        private AsyncLock _lock;

        public override string Version => "SerialPortClient";

        public SerialPortClient()
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
        public SerialPortClient(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None) : this()
        {
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            Handshake = handshake;
            Parity = parity;
            StopBits = stopBits;
            _lock = new AsyncLock();
        }

        public OperationResult<byte[]> MessageExchange(byte[] message, bool useCRC = false)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult<byte[]>();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"串口连接失败";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    byte[] command = message;
                    if (useCRC)
                    {
                        command = CRC16Helper.GetCRC16(message);
                        result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));
                       
                    }
                    //发送命令并获取响应报文
                    var sendResult = InterpretAndExtractMessageData(command);
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
                    else if (useCRC&!CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        return result.Complete();
                    }
                    byte[] resultData = new byte[responsePackage.Length];
                    Array.Copy(responsePackage, 0, resultData, 0, resultData.Length);
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

        public async Task<OperationResult<byte[]>> MessageExchangeAsync(byte[] message, bool useCRC = false)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult<byte[]>();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult =await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"串口连接失败";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {
                    byte[] command = message;
                    if (useCRC)
                    {
                        command = CRC16Helper.GetCRC16(message);
                        result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));

                    }
                    //发送命令并获取响应报文
                    var sendResult =await InterpretAndExtractMessageDataAsync(command);
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
                    else if (useCRC & !CRC16Helper.CheckCRC16(responsePackage))
                    {

                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }
                    byte[] resultData = new byte[responsePackage.Length];
                    Array.Copy(responsePackage, 0, resultData, 0, resultData.Length);
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


        public  OperationResult<string> MessageExchange(string message, Encoding encoding = null, bool useCRC = false)
        {
            using (_lock.Lock())
            {
                var result = new OperationResult<string>();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult =  Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"串口连接失败";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {

                    if (encoding == null) encoding = Encoding.ASCII;
                    byte[] command = encoding.GetBytes(message);
                    if (useCRC)
                    {
                        command = CRC16Helper.GetCRC16(command);
                        result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));

                    }
                    //发送命令并获取响应报文
                    var sendResult = InterpretAndExtractMessageData(command);
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
                    else if (useCRC & !CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }
                    byte[] resultData = new byte[responsePackage.Length];
                    Array.Copy(responsePackage, 0, resultData, 0, resultData.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                    //4 获取响应报文数据（字节数组形式）                
                    result.Value = resultData.ToString(0, resultData.Length, encoding);
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

        public async Task<OperationResult<string>> MessageExchangeAsync(string message, Encoding encoding = null, bool useCRC = false)
        {
            using (await _lock.LockAsync())
            {
                var result = new OperationResult<string>();
                if (!_serialPort?.IsOpen ?? true)
                {
                    var connectResult = await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"串口连接失败";
                        return result.SetInfo(connectResult);
                    }
                }
                try
                {

                    if (encoding == null) encoding = Encoding.ASCII;
                    byte[] command = encoding.GetBytes(message);
                    if (useCRC)
                    {
                        command = CRC16Helper.GetCRC16(command);
                        result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));

                    }
                    //发送命令并获取响应报文
                    var sendResult = await InterpretAndExtractMessageDataAsync(command);
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
                    else if (useCRC & !CRC16Helper.CheckCRC16(responsePackage))
                    {
                        result.IsSuccess = false;
                        result.Message = "响应结果CRC16Helper验证失败";
                        //return result.Complete();
                    }
                    byte[] resultData = new byte[responsePackage.Length];
                    Array.Copy(responsePackage, 0, resultData, 0, resultData.Length);
                    result.Responses.Add(string.Join(" ", responsePackage.Select(t => t.ToString("X2"))));
                    //4 获取响应报文数据（字节数组形式）                
                    result.Value = resultData.ToString(0,resultData.Length,encoding);
                    
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

    }
}

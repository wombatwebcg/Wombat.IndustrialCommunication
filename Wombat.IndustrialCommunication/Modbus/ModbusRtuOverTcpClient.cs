using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Wombat.IndustrialCommunication.Models;
using Wombat.Infrastructure;
using Wombat.ObjectConversionExtention;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Tcp的方式发送ModbusRtu协议报文 - 客户端
    /// </summary>
    public class ModbusRtuOverTcpClient : ModbusSocketBase
    {


        /// <summary>
        /// 是否自动打开关闭
        /// </summary>
        protected bool _isAutoOpen = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ip">ip地址</param>
        /// <param name="port">端口</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="DataFormat">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        public ModbusRtuOverTcpClient(string ip, int port) : base(ip, port)
        {
        }


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ipEndPoint">ip地址和端口</param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="DataFormat">大小端设置</param>
        public ModbusRtuOverTcpClient(IPEndPoint ipAndPoint) : base(ipAndPoint)
        {
        }

        /// <summary>
        /// 发送报文，并获取响应报文
        /// </summary>
        /// <param name="command"></param>
        /// <param name="lenght"></param>
        /// <returns></returns>
        public OperationResult<byte[]> SendPackage(byte[] command, int lenght)
        {
            OperationResult<byte[]> _SendPackage()
            {
                lock (this)
                {
                    //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
                    OperationResult<byte[]> result = new OperationResult<byte[]>();
                    //发送命令
                    Base.Send(command);
                    //获取响应报文    
                    var socketReadResult = SocketRead(Base, lenght);
                    if (!socketReadResult.IsSuccess)
                        return socketReadResult;
                    result.Value = socketReadResult.Value;
                    return result.Complete();
                }
            }

            try
            {
                var result = _SendPackage();
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                        return new OperationResult<byte[]>(connectResult);

                    return _SendPackage();
                }
                else
                    return result;
            }
            catch (Exception ex)
            {
                WarningLog?.Invoke(ex.Message, ex);
                //如果出现异常，则进行一次重试
                //重新打开连接
                var connectResult = Connect();
                if (!connectResult.IsSuccess)
                    return new OperationResult<byte[]>(connectResult);

                return _SendPackage();
            }
        }



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
            if (_isAutoOpen) Connect();

            var result = new OperationResult<byte[]>();
            try
            {
                //获取命令（组装报文）
                byte[] command = GetReadCommand(address, stationNumber, functionCode, (ushort)readLength);
                var commandCRC16 = CRC16Helper.GetCRC16(command);
                result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));

                //发送命令并获取响应报文
                int readLenght;
                if (functionCode == 1 || functionCode == 2)
                    readLenght = 5 + (int)Math.Ceiling((float)readLength / 8);
                else
                    readLenght = 5 + readLength * 2;
                var sendResult = SendPackage(commandCRC16, readLenght);
                if (!sendResult.IsSuccess)
                    return sendResult;
                var responsePackage = sendResult.Value;
                //var responsePackage = SendPackage(commandCRC16, readLenght);
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
                if (_isAutoOpen) Dispose();
            }
            return result.Complete();
        }

        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        public override OperationResult Write(string address, bool value, byte stationNumber = 1, byte functionCode = 5,bool isPlcAddress = false)
        {
            if (!IsConnect) Connect();
            var result = new OperationResult();
            try
            {
                var command = GetWriteCoilCommand(address, value, stationNumber, functionCode);
                var commandCRC16 = CRC16Helper.GetCRC16(command);
                result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));
                //发送命令并获取响应报文
                //var responsePackage = SendPackage(commandCRC16, 8);
                var sendResult = SendPackage(commandCRC16, 8);
                if (!sendResult.IsSuccess)
                    return sendResult;
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
                if (IsConnect) Dispose();
            }
            return result.Complete();
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        public override OperationResult Write(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16, bool byteFormatting = true)
        {
            if (!IsConnect) Connect();

            var result = new OperationResult();
            try
            {
                var command = GetWriteCommand(address, values, stationNumber, functionCode);

                var commandCRC16 = CRC16Helper.GetCRC16(command);
                result.Requsts[0] = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));
                //var responsePackage = SendPackage(commandCRC16, 8);
                var sendResult = SendPackage(commandCRC16, 8);
                if (!sendResult.IsSuccess)
                    return sendResult;
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
                result.Responses[0] = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            finally
            {
                if (IsConnect) Dispose();
            }
            return result.Complete();
        }


        public override Task<OperationResult<byte[]>> SendPackageSingleAsync(byte[] command)
        {
            throw new NotImplementedException();
        }

        public override Task<OperationResult<byte[]>> SendPackageReliableAsync(byte[] command)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address"></param>
        /// <param name="values"></param>
        /// <param name="stationNumber"></param>
        /// <param name="functionCode"></param>
        /// <returns></returns>
        //public override OperationResult WriteOne(string address, byte[] values, byte stationNumber = 1, byte functionCode = 6, bool byteFormatting = true)
        //{
        //    if (!IsConnect) Connect();

        //    var result = new OperationResult();
        //    try
        //    {
        //        var command = GetWriteCommand(address, values, stationNumber, functionCode);

        //        var commandCRC16 = CRC16Helper.GetCRC16(command);
        //        result.Requst = string.Join(" ", commandCRC16.Select(t => t.ToString("X2")));
        //        //var responsePackage = SendPackage(commandCRC16, 8);
        //        var sendResult = SendPackage(commandCRC16, 8);
        //        if (!sendResult.IsSuccess)
        //            return sendResult;
        //        var responsePackage = sendResult.Value;

        //        if (!responsePackage.Any())
        //        {
        //            result.IsSuccess = false;
        //            result.Message = "响应结果为空";
        //            return result.Complete();
        //        }
        //        else if (!CRC16Helper.CheckCRC16(responsePackage))
        //        {
        //            result.IsSuccess = false;
        //            result.Message = "响应结果CRC16Helper验证失败";
        //            //return result.Complete();
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
        //        if (IsConnect) Dispose();
        //    }
        //    return result.Complete();
        //}


    }


}

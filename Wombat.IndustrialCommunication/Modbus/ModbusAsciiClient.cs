﻿using System;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Infrastructure;
using Wombat.ObjectConversionExtention;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// ModbusAscii
    /// </summary>
    public class ModbusAsciiClient : ModbusSerialBase
    {
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
        public ModbusAsciiClient(string portName, int baudRate, int dataBits, StopBits stopBits, Parity parity)
            : base(portName, baudRate, dataBits, stopBits, parity)
        {
        }

        #region  Read 读取
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="readLength">读取长度</param>
        /// <returns></returns>
        public override OperationResult<byte[]> Read(string address, int readLength = 1, byte stationNumber = 1, byte functionCode = 3,bool isPlcAddress = false)
        {
            if (!IsConnect) Connect();

            var result = new OperationResult<byte[]>();
            try
            {
                //获取命令（组装报文）
                byte[] command = GetReadCommand(address, stationNumber, functionCode,(ushort) readLength, isPlcAddress: isPlcAddress);
                var commandLRC = DataConvert.ByteArrayToAsciiArray(LRCHelper.GetLRC(command));

                var finalCommand = new byte[commandLRC.Length + 3];
                Buffer.BlockCopy(commandLRC, 0, finalCommand, 1, commandLRC.Length);
                finalCommand[0] = 0x3A;
                finalCommand[finalCommand.Length - 2] = 0x0D;
                finalCommand[finalCommand.Length - 1] = 0x0A;

                result.Requsts[0] = string.Join(" ", finalCommand.Select(t => t.ToString("X2")));

                //发送命令并获取响应报文
                var sendResult = SendPackageReliable(finalCommand);
                if (!sendResult.IsSuccess)
                    return result.SetInfo(sendResult).Complete();
                var responsePackage = sendResult.Value;

                if (!responsePackage.Any())
                {
                    result.IsSuccess = false;
                    result.Message = "响应结果为空";
                    return result.Complete();
                }

                byte[] resultLRC = new byte[responsePackage.Length - 3];
                Array.Copy(responsePackage, 1, resultLRC, 0, resultLRC.Length);
                var resultByte = DataConvert.AsciiArrayToByteArray(resultLRC);
                if (!LRCHelper.CheckLRC(resultByte))
                {
                    result.IsSuccess = false;
                    result.Message = "响应结果LRC验证失败";
                    //return result.Complete();
                }
                var resultData = new byte[resultByte[2]];
                Buffer.BlockCopy(resultByte, 3, resultData, 0, resultData.Length);
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
                if (IsConnect) Dispose();
            }
            return result.Complete();
        }

        public override Task<OperationResult<byte[]>> SendPackageReliableAsync(byte[] command)
        {
            throw new NotImplementedException();
        }

        public override Task<OperationResult<byte[]>> SendPackageSingleAsync(byte[] command)
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
        public override OperationResult Write(string address, bool value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = false)
        {
            if (!IsConnect) Connect();
            var result = new OperationResult();
            try
            {
                var command = GetWriteCoilCommand(address, value, stationNumber, functionCode, isPlcAddress: isPlcAddress);

                var commandAscii = DataConvert.ByteArrayToAsciiArray(LRCHelper.GetLRC(command));
                var finalCommand = new byte[commandAscii.Length + 3];
                Buffer.BlockCopy(commandAscii, 0, finalCommand, 1, commandAscii.Length);
                finalCommand[0] = 0x3A;
                finalCommand[finalCommand.Length - 2] = 0x0D;
                finalCommand[finalCommand.Length - 1] = 0x0A;

                result.Requsts[0] = string.Join(" ", finalCommand.Select(t => t.ToString("X2")));
                //发送命令并获取响应报文
                var sendResult = SendPackageReliable(finalCommand);
                if (!sendResult.IsSuccess)
                    return result.SetInfo(sendResult).Complete();
                var responsePackage = sendResult.Value;
                if (!responsePackage.Any())
                {
                    result.IsSuccess = false;
                    result.Message = "响应结果为空";
                    return result.Complete();
                }

                byte[] resultLRC = new byte[responsePackage.Length - 3];
                Array.Copy(responsePackage, 1, resultLRC, 0, resultLRC.Length);
                var resultByte = DataConvert.AsciiArrayToByteArray(resultLRC);
                if (!LRCHelper.CheckLRC(resultByte))
                {
                    result.IsSuccess = false;
                    result.Message = "响应结果LRC验证失败";
                    //return result.Complete();
                }

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
        public override OperationResult Write(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16,bool isPlcAddress = false)
        {
            if (!IsConnect) Connect();

            var result = new OperationResult();
            try
            {
                var command = GetWriteCommand(address, values, stationNumber, functionCode, isPlcAddress: isPlcAddress);

                var commandAscii = DataConvert.ByteArrayToAsciiArray(LRCHelper.GetLRC(command));
                var finalCommand = new byte[commandAscii.Length + 3];
                Buffer.BlockCopy(commandAscii, 0, finalCommand, 1, commandAscii.Length);
                finalCommand[0] = 0x3A;
                finalCommand[finalCommand.Length - 2] = 0x0D;
                finalCommand[finalCommand.Length - 1] = 0x0A;

                result.Requsts[0] = string.Join(" ", finalCommand.Select(t => t.ToString("X2")));
                var sendResult = SendPackageReliable(finalCommand);
                if (!sendResult.IsSuccess)
                    return result.SetInfo(sendResult).Complete();
                var responsePackage = sendResult.Value;
                if (!responsePackage.Any())
                {
                    result.IsSuccess = false;
                    result.Message = "响应结果为空";
                    return result.Complete();
                }

                byte[] resultLRC = new byte[responsePackage.Length - 3];
                Array.Copy(responsePackage, 1, resultLRC, 0, resultLRC.Length);
                var resultByte = DataConvert.AsciiArrayToByteArray(resultLRC);
                if (!LRCHelper.CheckLRC(resultByte))
                {
                    result.IsSuccess = false;
                    result.Message = "响应结果LRC验证失败";
                    //return result.Complete();
                }

                result.Responses[0] = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
            }
            finally
            {
                if (!IsConnect) Dispose();
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
        //public override OperationResult WriteOne(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16, bool isPlcAddress = false)
        //{
        //    if (!IsConnect) Connect();

        //    var result = new OperationResult();
        //    try
        //    {
        //        var command = GetWriteCommand(address, values, stationNumber, functionCode, isPlcAddress: isPlcAddress);

        //        var commandAscii = DataConvert.ByteArrayToAsciiArray(LRCHelper.GetLRC(command));
        //        var finalCommand = new byte[commandAscii.Length + 3];
        //        Buffer.BlockCopy(commandAscii, 0, finalCommand, 1, commandAscii.Length);
        //        finalCommand[0] = 0x3A;
        //        finalCommand[finalCommand.Length - 2] = 0x0D;
        //        finalCommand[finalCommand.Length - 1] = 0x0A;

        //        result.Requst = string.Join(" ", finalCommand.Select(t => t.ToString("X2")));
        //        var sendResult = SendPackageReliable(finalCommand);
        //        if (!sendResult.IsSuccess)
        //            return result.SetInfo(sendResult).Complete();
        //        var responsePackage = sendResult.Value;
        //        if (!responsePackage.Any())
        //        {
        //            result.IsSuccess = false;
        //            result.Message = "响应结果为空";
        //            return result.Complete();
        //        }

        //        byte[] resultLRC = new byte[responsePackage.Length - 3];
        //        Array.Copy(responsePackage, 1, resultLRC, 0, resultLRC.Length);
        //        var resultByte = DataConvert.AsciiArrayToByteArray(resultLRC);
        //        if (!LRCHelper.CheckLRC(resultByte))
        //        {
        //            result.IsSuccess = false;
        //            result.Message = "响应结果LRC验证失败";
        //            //return result.Complete();
        //        }

        //        result.Response = string.Join(" ", responsePackage.Select(t => t.ToString("X2")));
        //    }
        //    catch (Exception ex)
        //    {
        //        result.IsSuccess = false;
        //        result.Message = ex.Message;
        //    }
        //    finally
        //    {
        //        if (!IsConnect) Dispose();
        //    }
        //    return result.Complete();
        //}


        #endregion
    }
}

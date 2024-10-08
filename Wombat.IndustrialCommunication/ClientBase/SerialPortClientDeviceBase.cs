﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Wombat.Network.Sockets;
using Microsoft.Extensions.Logging;


namespace Wombat.IndustrialCommunication
{
   public abstract class SerialPortClientDeviceBase : DeviceClient, ISerialPortClient
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public StopBits StopBits { get; set; } = StopBits.One;
        public Parity Parity { get; set; } = Parity.None;
        public Handshake Handshake { get; set; } = Handshake.None;

        private AsyncLock _lock = new AsyncLock();

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
            while ((_serialPort.BytesToRead == 0 || tempBufferLength != _serialPort.BytesToRead) && DateTime.Now - beginTime <= TimeSpan.FromMilliseconds(ConnectTimeout.TotalMilliseconds))
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
            while ((_serialPort.BytesToRead == 0 || tempBufferLength != _serialPort.BytesToRead) && DateTime.Now - beginTime <= TimeSpan.FromMilliseconds(ConnectTimeout.TotalMilliseconds))
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
                var readLeng =await _serialPort.BaseStream.ReadAsync(buffer, receiveFinish, buffer.Length);
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
                using (_lock.Lock())
                {
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
            }

            OperationResult<byte[]> result = new OperationResult<byte[]>();
            try
            {
                result = sendPackage();
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                        return new OperationResult<byte[]>(connectResult);

                    result = result.SetInfo(sendPackage());
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

        internal override async ValueTask<OperationResult<byte[]>> ExchangingMessagesAsync(byte[] command)
        {
            async ValueTask<OperationResult<byte[]>> sendPackage()
            {
                //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
                using (await _lock.LockAsync())
                {
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
            }

            OperationResult<byte[]> result = new OperationResult<byte[]>();
            try
            {
                result = await sendPackage();
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                        return new OperationResult<byte[]>(connectResult);

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

    }
}

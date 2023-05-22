using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Infrastructure;
using Wombat.Core;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// SerialPort基类
    /// </summary>
    public  class SerialPortBase : ClientBase
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
        private SerialPort _serialPort;

        public SerialPortBase()
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
        public SerialPortBase(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None)
        {
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            Handshake = handshake;
            Parity = parity;
            StopBits = stopBits;

        }


        /// <summary>
        /// 获取设备上的COM端口集合
        /// </summary>
        /// <returns></returns>
        public static string[] GetPortNames()
        {
            return SerialPort.GetPortNames();
        }

        public override bool Connected =>_serialPort==null?false:_serialPort.IsOpen;



        /// <summary>
        /// 连接
        /// </summary>
        /// <returns></returns>
        internal override OperationResult DoConnect()
        {
            if (DeviceInterfaceHelper.CheckSerialPort(PortName))
            {
                _serialPort?.Close();
            }
            _serialPort = new SerialPort();
            _serialPort.PortName = PortName ?? throw new ArgumentNullException(nameof(PortName));
            _serialPort.BaudRate = BaudRate;
            _serialPort.Parity = Parity;
            _serialPort.DataBits = DataBits;
            _serialPort.StopBits = StopBits;
            _serialPort.Handshake = Handshake;

            _serialPort.WriteTimeout = (int)Timeout.TotalMilliseconds;
            _serialPort.ReadTimeout = (int)Timeout.TotalMilliseconds;


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
        /// 读取
        /// </summary>
        /// <param name="serialPort"></param>
        /// <returns></returns>
        protected OperationResult<byte[]> SerialPortRead(SerialPort serialPort)
        {
            OperationResult<byte[]> result = new OperationResult<byte[]>();
            DateTime beginTime = DateTime.Now;
            var tempBufferLength = serialPort.BytesToRead;
            //在(没有取到数据或BytesToRead在继续读取)且没有超时的情况，延时处理
            while ((serialPort.BytesToRead == 0 || tempBufferLength != serialPort.BytesToRead) && DateTime.Now - beginTime <= TimeSpan.FromMilliseconds(Timeout.TotalMilliseconds))
            {
                tempBufferLength = serialPort.BytesToRead;
                //延时处理
                Thread.Sleep(WaiteInterval);
            }
            byte[] buffer = new byte[serialPort.BytesToRead];
            var receiveFinish = 0;
            while (receiveFinish < buffer.Length)
            {
                var readLeng = serialPort.Read(buffer, receiveFinish, buffer.Length);
                if (readLeng == 0)
                {
                    result.Value = null;
                    return result.Complete();
                }
                receiveFinish += readLeng;
            }
            result.Value = buffer;
            if (Logger.MinimumLevel<=LogEventLevel.Debug)
            {
                string printSend = $"{_serialPort.PortName} receive:";
                for (int i = 0; i < buffer.Length; i++)
                {
                    printSend = printSend + " " + buffer[i].ToString("X").PadLeft(2, '0'); ;

                }
                Logger?.Debug(printSend);
            }

            return result.Complete();
        }


        #region 发送报文，并获取响应报文



        /// <summary>
        /// 发送报文，并获取响应报文（建议使用SendPackageReliable，如果异常会自动重试一次）
        /// </summary>
        /// <param name="command">发送命令</param>
        /// <returns></returns>
       internal override OperationResult<byte[]> GetMessageContent(byte[] command)
        {
            OperationResult<byte[]> _sendPackage()
            {
                //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
                lock (this)
                {
                    //发送命令
                    _serialPort.Write(command, 0, command.Length);
                    if (Logger.MinimumLevel <= LogEventLevel.Debug)
                    {
                        string printSend = $"{_serialPort.PortName} send:";
                        for (int i = 0; i < command.Length; i++)
                        {
                            printSend = printSend + " " + command[i].ToString("X").PadLeft(2, '0'); ;
                        }
                        Logger?.Debug(printSend);
                    }

                    //获取响应报文
                    return SerialPortRead(_serialPort);
                }
            }

            OperationResult<byte[]> result = new OperationResult<byte[]>();
            try
            {
                 result = _sendPackage();
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                        return new OperationResult<byte[]>(connectResult);

                    result = result.SetInfo(_sendPackage());
                }
            }
            catch (Exception ex)
            {
                WarningLog?.Invoke(ex.Message, ex);
                //如果出现异常，则进行一次重试
                //重新打开连接
            }
            return result;

        }


        int _OperationReTryTimes = 0;
        /// <summary>
        /// 发送报文，并获取响应报文
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
       internal override OperationResult<byte[]> InterpretAndExtractMessageData(byte[] command)
        {
            OperationResult<byte[]> result = new OperationResult<byte[]>();

            try
            {
                result = GetMessageContent(command);
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    if (_OperationReTryTimes < OperationReTryTimes)
                    {
                        _OperationReTryTimes++;
                        InterpretAndExtractMessageData(command);
                    }
                    else
                    {
                        _OperationReTryTimes = 0;
                        return result;

                    }
                }
                else
                {
                    _OperationReTryTimes = 0;
                    return result;
                }
            }
            catch (Exception ex)
            {
                WarningLog?.Invoke(ex.Message, ex);
                //如果出现异常，则进行一次重试
                //重新打开连接
                result = GetMessageContent(command);
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    if (_OperationReTryTimes < OperationReTryTimes)
                    {
                        _OperationReTryTimes++;
                         InterpretAndExtractMessageData(command);
                    }
                    else
                    {
                        _OperationReTryTimes = 0;
                        return result;

                    }
                }
                else
                {
                    _OperationReTryTimes = 0;
                    return result;
                }
            }
            return result;

        }


        public virtual OperationResult<string> SendPackageReliable(string command, Encoding encoding)
        {
           var result = InterpretAndExtractMessageData(encoding.GetBytes(command));
            if(result.IsSuccess)
            {
                return new OperationResult<string>(result)
                {
                    Value = encoding.GetString(result.Value)
                };
            }
            else
            {
                return new OperationResult<string>(result);
 

            }
        }

        internal override ValueTask<OperationResult<byte[]>> InterpretAndExtractMessageDataAsync(byte[] command)
        {
            throw new NotImplementedException();
        }

        internal override ValueTask<OperationResult<byte[]>> GetMessageContentAsync(byte[] command)
        {
            throw new NotImplementedException();
        }

        internal override Task<OperationResult> DoConnectAsync()
        {
            throw new NotImplementedException();
        }

        internal override Task<OperationResult> DoDisconnectAsync()
        {
            throw new NotImplementedException();
        }


        #endregion

    }
}

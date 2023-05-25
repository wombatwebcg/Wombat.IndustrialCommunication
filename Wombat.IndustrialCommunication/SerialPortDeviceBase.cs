using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wombat.Infrastructure;
using Wombat.Network.Sockets;
using Wombat.ObjectConversionExtention;

namespace Wombat.IndustrialCommunication
{
   public abstract class SerialPortDeviceBase : ClientBase
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

            _serialPort.WriteTimeout = (int)Timeout.TotalMilliseconds;
            _serialPort.ReadTimeout = (int)Timeout.TotalMilliseconds;


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
                int receiveLength = (receiveCount - receiveFinish) >= _serialPort.ReadBufferSize ? _serialPort.ReadBufferSize : (receiveCount - receiveFinish);
                try
                {
                    var readLeng = _serialPort.Read(receiveBytes, receiveFinish, receiveLength);
                    if (readLeng == 0)
                    {
                        _serialPort.Close();
                        result.IsSuccess = false;
                        result.Message = $"连接被断开";

                        return result;
                    }
                    receiveFinish += readLeng;
                }
                catch (SocketException ex)
                {
                    _serialPort?.Close();
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
                int receiveLength = (receiveCount - receiveFinish) >= _serialPort.ReadBufferSize ? _serialPort.ReadBufferSize : (receiveCount - receiveFinish);
                try
                {
                   
                   var readLeng = await _serialPort.BaseStream.ReadAsync(receiveBytes, receiveFinish, receiveLength);
                    if (readLeng == 0)
                    {
                        _serialPort.Close();
                        result.IsSuccess = false;
                        result.Message = $"连接被断开";

                        return result;
                    }
                    receiveFinish += readLeng;
                }
                catch (SocketException ex)
                {
                    _serialPort?.Close();
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
        internal override OperationResult<byte[]> InterpretAndExtractMessageData(byte[] command)
        {
            try
            {
                var result = GetMessageContent(command);
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
                        result = GetMessageContent(command); ;
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
                      var  result = GetMessageContent(command); ;
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
        internal override async ValueTask<OperationResult<byte[]>> InterpretAndExtractMessageDataAsync(byte[] command)
        {
            try
            {
                var result = await GetMessageContentAsync(command);
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    var conentOperationResult =await ConnectAsync();
                    if (!conentOperationResult.IsSuccess)
                    {
                        return new OperationResult<byte[]>(conentOperationResult);

                    }
                    else
                    {
                        result =await GetMessageContentAsync(command); ;
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
                        var result = await GetMessageContentAsync(command); 
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
    }
}

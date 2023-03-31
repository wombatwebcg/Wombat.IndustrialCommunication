using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Wombat.Infrastructure;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// Socket基类
    /// </summary>
    public abstract class SocketBase : BaseModel
    {
        /// <summary>
        /// 分批缓冲区大小
        /// </summary>
        protected const int BufferSize = 4096;

        /// <summary>
        /// Socket实例
        /// </summary>
        protected Socket _socket;


        private IPEndPoint _ipEndPoint;

        public override bool IsConnect => _socket == null ? false : _socket.Connected;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ipAndPoint"></param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="format">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        /// <param name="plcAddresses">PLC地址</param>
        public SocketBase(IPEndPoint ipAndPoint)
        {
            _ipEndPoint = ipAndPoint;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="format">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        public SocketBase(string ip, int port)
        {
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            _ipEndPoint = new IPEndPoint(address, port);
       }

        protected override OperationResult DoConnect()
        {
            var result = new OperationResult();
            _socket?.SafeClose();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                //超时时间设置
                _socket.ReceiveTimeout = (int)Timeout.TotalMilliseconds;
                _socket.SendTimeout = (int)Timeout.TotalMilliseconds;

                //连接
                //socket.Connect(ipEndPoint);
                IAsyncResult connectResult = _socket.BeginConnect(_ipEndPoint, null, null);
                //阻塞当前线程           
                if (!connectResult.AsyncWaitHandle.WaitOne(Timeout))
                    throw new TimeoutException("连接超时");
                _socket.EndConnect(connectResult);
            }
            catch (Exception ex)
            {
                _socket?.SafeClose();
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.ErrorCode = 408;
                result.Exception = ex;
            }
            return result.EndTime();

        }

        protected override OperationResult DoDisconnect()
        {

            OperationResult result = new OperationResult();
            try
            {
                _socket?.SafeClose();
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
        /// Socket读取
        /// </summary>
        /// <param name="socket">socket</param>
        /// <param name="receiveCount">读取长度</param>          
        /// <returns></returns>
        protected OperationResult<byte[]> SocketRead(Socket socket, int receiveCount)
        {
            var result = new OperationResult<byte[]>();
            if (receiveCount < 0)
            {
                result.IsSuccess = false;
                result.Message = $"读取长度[receiveCount]为{receiveCount}";
                result.AddMessage2List();
                return result;
            }

            byte[] receiveBytes = new byte[receiveCount];
            int receiveFinish = 0;
            while (receiveFinish < receiveCount)
            {
                // 分批读取
                int receiveLength = (receiveCount - receiveFinish) >= BufferSize ? BufferSize : (receiveCount - receiveFinish);
                try
                {
                    var readLeng = socket.Receive(receiveBytes, receiveFinish, receiveLength, SocketFlags.None);
                    if (readLeng == 0)
                    {
                        socket?.SafeClose();
                        result.IsSuccess = false;
                        result.Message = $"连接被断开";
                        result.AddMessage2List();
                        return result;
                    }
                    receiveFinish += readLeng;
                }
                catch (SocketException ex)
                {
                    socket?.SafeClose();
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                        result.Message = $"连接超时：{ex.Message}";
                    else
                        result.Message = $"连接被断开，{ex.Message}";
                    result.IsSuccess = false;
                    result.AddMessage2List();
                    result.Exception = ex;
                    return result;
                }
                Thread.Sleep(WaiteInterval);

            }
            result.Value = receiveBytes;
            return result.EndTime();
        }

        /// <summary>
        /// 发送报文，并获取响应报文（建议使用SendPackageReliable，如果异常会自动重试一次）
        /// </summary>
        /// <param name="command">发送命令</param>
        /// <returns></returns>
        public abstract override OperationResult<byte[]> SendPackageSingle(byte[] command);

        /// <summary>
        /// 发送报文，并获取响应报文（如果网络异常，会自动进行一次重试）
        /// TODO 重试机制应改成用户主动设置
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override OperationResult<byte[]> SendPackageReliable(byte[] command)
        {
            try
            {
                var result = SendPackageSingle(command);
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    var conentOperationResult = Connect();
                    if (!conentOperationResult.IsSuccess)
                        return new OperationResult<byte[]>(conentOperationResult);

                    return SendPackageSingle(command);
                }
                else
                    return result;
            }
            catch (Exception ex)
            {
                try
                {
                    WarningLog?.Invoke(ex.Message, ex);
                    //如果出现异常，则进行一次重试                
                    var conentOperationResult = Connect();
                    if (!conentOperationResult.IsSuccess)
                        return new OperationResult<byte[]>(conentOperationResult);

                    return SendPackageSingle(command);
                }
                catch (Exception ex2)
                {
                    OperationResult<byte[]> result = new OperationResult<byte[]>();
                    result.IsSuccess = false;
                    result.Message = ex2.Message;
                    result.AddMessage2List();
                    return result.EndTime();
                }
            }
        }


    }
}

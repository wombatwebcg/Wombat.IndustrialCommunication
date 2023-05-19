using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Wombat.Infrastructure;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Socket基类
    /// </summary>
    public abstract class ModbusSocketBase : ModbusBase
    {
        /// <summary>
        /// 分批缓冲区大小
        /// </summary>
        protected const int BufferSize = 4096;


        public Socket Base => _socket;
        /// <summary>
        /// Socket实例
        /// </summary>
        protected Socket _socket;

        private AdvancedHybirdLock _advancedHybirdLock;

        public IPEndPoint IpEndPoint { get; set; }

        public override bool IsConnect => _socket == null ? false : _socket.Connected;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ipAndPoint"></param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="format">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        /// <param name="plcAddresses">PLC地址</param>
        public ModbusSocketBase()
        {
            _advancedHybirdLock = new AdvancedHybirdLock();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="ipAndPoint"></param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="format">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        /// <param name="plcAddresses">PLC地址</param>
        public ModbusSocketBase(IPEndPoint ipAndPoint)
        {
            IpEndPoint = ipAndPoint;
            _advancedHybirdLock = new AdvancedHybirdLock();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <param name="format">大小端设置</param>
        /// <param name="plcAddresses">PLC地址</param>
        public ModbusSocketBase(string ip, int port)
        {
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            IpEndPoint = new IPEndPoint(address, port);
            _advancedHybirdLock = new AdvancedHybirdLock();
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
                IAsyncResult connectResult = _socket.BeginConnect(IpEndPoint, null, null);
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
            return result.Complete();

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
                        socket?. SafeClose();
                        result.IsSuccess = false;
                        result.Message = $"连接被断开";
                        
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
                    
                    result.Exception = ex;
                    return result;
                }
            }
            result.Value = receiveBytes;
            return result.Complete();
        }

        /// <summary>
        /// 发送报文，并获取响应报文（建议使用SendPackageReliable，如果异常会自动重试一次）
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public override OperationResult<byte[]> SendPackageSingle(byte[] command)
        {
            //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
            lock (this)
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                try
                {
                    _socket.Send(command);
                    var socketReadResult = SocketRead(_socket, 8);
                    if (!socketReadResult.IsSuccess)
                        return socketReadResult;
                    var headPackage = socketReadResult.Value;
                    int length = headPackage[4] * 256 + headPackage[5] - 2;
                    socketReadResult = SocketRead(_socket, length);
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
        }

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
                    OperationResult<byte[]> result = new OperationResult<byte[]>
                    {
                        IsSuccess = false,
                        Message = ex2.Message
                    };
                    
                    return result.Complete();
                }
            }
        }

        #region Read 读取
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">寄存器起始地址</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="readLength">读取长度</param>
        /// <param name="byteFormatting">大小端转换</param>
        /// <returns></returns>
        public override OperationResult<byte[]> Read(string address, int readLength = 1, byte stationNumber = 1, byte functionCode = 3, bool isPlcAddress = false)
        {
            _advancedHybirdLock.Enter();
            var result = new OperationResult<byte[]>();

            if (!_socket?.Connected ?? true)
            {
                var connectResult = Connect();
                if (!connectResult.IsSuccess)
                {
                    connectResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ connectResult.Message}";
                    _advancedHybirdLock.Leave();
                    return result.SetInfo(connectResult);
                }
            }
            try
            {
                var chenkHead = GetCheckHead(functionCode);
                //1 获取命令（组装报文）
                byte[] command = GetReadCommand(address, stationNumber, functionCode, (ushort)readLength, chenkHead,isPlcAddress);
                result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                //获取响应报文
                var sendResult = SendPackageReliable(command);
                if (!sendResult.IsSuccess)
                {
                    sendResult.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ sendResult.Message}";
                    _advancedHybirdLock.Leave();
                    return result.SetInfo(sendResult).Complete();
                }
                var dataPackage = sendResult.Value;
                byte[] resultBuffer = new byte[dataPackage.Length - 9];
                Array.Copy(dataPackage, 9, resultBuffer, 0, resultBuffer.Length);
                result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                //4 获取响应报文数据（字节数组形式）             
                result.Value = resultBuffer.ToArray();

                if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                {
                    result.IsSuccess = false;
                    result.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。响应结果校验失败";
                    _socket?.SafeClose();
                }
                else if (ModbusHelper.VerifyFunctionCode(functionCode, dataPackage[7]))
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
                    result.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。连接超时";
                    _socket?.SafeClose();
                }
                else
                {
                    result.Message = $"读取 地址:{address} 站号:{stationNumber} 功能码:{functionCode} 失败。{ ex.Message}";
                }
            }
            finally
            {
                if (!IsUseLongConnect) Disconnect();
            }
            _advancedHybirdLock.Leave();
            return result.Complete();
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="address">写入地址</param>
        /// <param name="values">写入字节数组</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="byteFormatting">大小端设置</param>
        /// <returns></returns>
        //public override OperationResult WriteOne(string address, byte[] values, byte stationNumber = 1, byte functionCode = 6, bool isPlcAddress = false)
        //{
        //    _advancedHybirdLock.Enter();
        //    var result = new OperationResult();
        //    if (!_socket?.Connected ?? true)
        //    {
        //        var connectResult = Connect();
        //        if (!connectResult.IsSuccess)
        //        {
        //            _advancedHybirdLock.Leave();
        //            return result.SetInfo(connectResult);
        //        }
        //    }
        //    try
        //    {
        //        var chenkHead = GetCheckHead(functionCode);
        //        var command = GetWriteOneCommand(address, values, stationNumber, functionCode, chenkHead, isPlcAddress);
        //        result.Requst = string.Join(" ", command.Select(t => t.ToString("X2")));
        //        var sendResult = SendPackageReliable(command);
        //        if (!sendResult.IsSuccess)
        //        {
        //            _advancedHybirdLock.Leave();
        //            return result.SetInfo(sendResult).Complete();
        //        }
        //        var dataPackage = sendResult.Value;
        //        result.Response = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
        //        if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
        //        {
        //            result.IsSuccess = false;
        //            result.Message = "响应结果校验失败";
        //            _socket?.SafeClose();
        //        }
        //        else if (ModbusHelper.VerifyFunctionCode(functionCode, dataPackage[7]))
        //        {
        //            result.IsSuccess = false;
        //            result.Message = ModbusHelper.ErrMsg(dataPackage[8]);
        //        }
        //    }
        //    catch (SocketException ex)
        //    {
        //        result.IsSuccess = false;
        //        if (ex.SocketErrorCode == SocketError.TimedOut)
        //        {
        //            result.Message = "连接超时";
        //            _socket?.SafeClose();
        //        }
        //        else
        //        {
        //            result.Message = ex.Message;
        //        }
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
        /// <param name="address">写入地址</param>
        /// <param name="values">写入字节数组</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <param name="byteFormatting">大小端设置</param>
        /// <returns></returns>
        public override OperationResult Write(string address, byte[] values, byte stationNumber = 1, byte functionCode = 16,bool isPlcAddress = false)
        {
            _advancedHybirdLock.Enter();
            var result = new OperationResult();
            if (!_socket?.Connected ?? true)
            {
                var connectResult = Connect();
                if (!connectResult.IsSuccess)
                {
                    _advancedHybirdLock.Leave();
                    return result.SetInfo(connectResult);
                }
            }
            try
            {
                var chenkHead = GetCheckHead(functionCode);
                var command = GetWriteCommand(address, values, stationNumber, functionCode, chenkHead,isPlcAddress);
                result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                var sendResult = SendPackageReliable(command);
                if (!sendResult.IsSuccess)
                {
                    _advancedHybirdLock.Leave();
                    return result.SetInfo(sendResult).Complete();
                }
                var dataPackage = sendResult.Value;
                result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                {
                    result.IsSuccess = false;
                    result.Message = "响应结果校验失败";
                    _socket?.SafeClose();
                }
                else if (ModbusHelper.VerifyFunctionCode(functionCode, dataPackage[7]))
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
                    _socket?.SafeClose();
                }
                else
                {
                    result.Message = ex.Message;
                }
            }
            finally
            {
                if (!IsUseLongConnect) Disconnect();
            }
            _advancedHybirdLock.Leave();
            return result.Complete();
        }
        /// <summary>
        /// 线圈写入
        /// </summary>
        /// <param name="address">写入地址</param>
        /// <param name="value"></param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        public override OperationResult Write(string address, bool value, byte stationNumber = 1, byte functionCode = 5, bool isPlcAddress = false)
        {
            _advancedHybirdLock.Enter();
            var result = new OperationResult();
            if (!_socket?.Connected ?? true)
            {
                var connectResult = Connect();
                if (!connectResult.IsSuccess)
                {
                    _advancedHybirdLock.Leave();
                    return result.SetInfo(connectResult);
                }
            }
            try
            {
                var chenkHead = GetCheckHead(functionCode);
                var command = GetWriteCoilCommand(address, value, stationNumber, functionCode, chenkHead, isPlcAddress);
                result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                var sendResult = SendPackageReliable(command);
                if (!sendResult.IsSuccess)
                {
                    _advancedHybirdLock.Leave();
                    return result.SetInfo(sendResult).Complete();
                }
                var dataPackage = sendResult.Value;
                result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                {
                    result.IsSuccess = false;
                    result.Message = "响应结果校验失败";
                    _socket?.SafeClose();
                }
                else if (ModbusHelper.VerifyFunctionCode(functionCode, dataPackage[7]))
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
                    _socket?.SafeClose();
                }
                else
                {
                    result.Message = ex.Message;
                }
            }
            finally
            {
                if (!IsUseLongConnect) Disconnect();
            }
            _advancedHybirdLock.Leave();
            return result.Complete();
        }

        public override OperationResult Write(string address, bool[] value, byte stationNumber = 1, byte functionCode = 15, bool isPlcAddress = false)
        {
            _advancedHybirdLock.Enter();
            var result = new OperationResult();
            if (!_socket?.Connected ?? true)
            {
                var connectResult = Connect();
                if (!connectResult.IsSuccess)
                {
                    _advancedHybirdLock.Leave();
                    return result.SetInfo(connectResult);
                }
            }
            try
            {
                var chenkHead = GetCheckHead(functionCode);
                var command = GetWriteCoilCommand(address, value, stationNumber, functionCode, chenkHead, isPlcAddress);
                result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                var sendResult = SendPackageReliable(command);
                if (!sendResult.IsSuccess)
                {
                    _advancedHybirdLock.Leave();
                    return result.SetInfo(sendResult).Complete();
                }
                var dataPackage = sendResult.Value;
                result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                if (chenkHead[0] != dataPackage[0] || chenkHead[1] != dataPackage[1])
                {
                    result.IsSuccess = false;
                    result.Message = "响应结果校验失败";
                    _socket?.SafeClose();
                }
                else if (ModbusHelper.VerifyFunctionCode(functionCode, dataPackage[7]))
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
                    _socket?.SafeClose();
                }
                else
                {
                    result.Message = ex.Message;
                }
            }
            finally
            {
                if (!IsUseLongConnect) Disconnect();
            }
            _advancedHybirdLock.Leave();
            return result.Complete();
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
        public byte[] GetReadCommand(string address, byte stationNumber, byte functionCode, ushort length, byte[] check = null, bool isPlcAddress = false)
        {
            if (isPlcAddress) { address = TranPLCAddress(address); }
            var readAddress = ushort.Parse(address?.Trim());
            byte[] buffer = new byte[12];
            buffer[0] = check?[0] ?? 0x19;
            buffer[1] = check?[1] ?? 0xB2;//Client发出的检验信息
            buffer[2] = 0x00;
            buffer[3] = 0x00;//表示tcp/ip 的协议的Modbus的协议
            buffer[4] = 0x00;
            buffer[5] = 0x06;//表示的是该字节以后的字节长度

            buffer[6] = stationNumber;  //站号
            buffer[7] = functionCode;   //功能码
            buffer[8] = BitConverter.GetBytes(readAddress)[1];
            buffer[9] = BitConverter.GetBytes(readAddress)[0];//寄存器地址
            buffer[10] = BitConverter.GetBytes(length)[1];
            buffer[11] = BitConverter.GetBytes(length)[0];//表示request 寄存器的长度(寄存器个数)
            return buffer;
        }

        /// <summary>
        /// 获取写入命令
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="values">批量读取的值</param>
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        //public byte[] GetWriteOneCommand(string address, byte[] values, byte stationNumber, byte functionCode, byte[] check = null, bool isPlcAddress = false)
        //{
        //    if (isPlcAddress) { address = TranPLCAddress(address); }
        //    var writeAddress = ushort.Parse(address?.Trim());
        //    byte[] buffer = new byte[12];
        //    buffer[0] = check?[0] ?? 0x19;
        //    buffer[1] = check?[1] ?? 0xB2;//Client发出的检验信息     
        //    buffer[4] = 0x00;
        //    buffer[5] = 0x06;//表示的是该字节以后的字节长度

        //    buffer[6] = stationNumber;//站号
        //    buffer[7] = functionCode; //功能码
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
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCommand(string address, byte[] values, byte stationNumber, byte functionCode, byte[] check = null, bool isPlcAddress = false)
        {
            if (isPlcAddress) { address = TranPLCAddress(address); }
            var writeAddress = ushort.Parse(address?.Trim());

            if (values.Length > 2)
            {
                byte[] buffer = new byte[13 + values.Length];
                buffer[0] = check?[0] ?? 0x19;
                buffer[1] = check?[1] ?? 0xB2;//检验信息，用来验证response是否串数据了           
                buffer[4] = BitConverter.GetBytes(7 + values.Length)[1];
                buffer[5] = BitConverter.GetBytes(7 + values.Length)[0];//表示的是header handle后面还有多长的字节

                buffer[6] = stationNumber; //站号
                buffer[7] = functionCode;  //功能码
                buffer[8] = BitConverter.GetBytes(writeAddress)[1];
                buffer[9] = BitConverter.GetBytes(writeAddress)[0];//寄存器地址
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

                buffer[6] = stationNumber;//站号
                buffer[7] = functionCode; //功能码
                buffer[8] = BitConverter.GetBytes(writeAddress)[1];
                buffer[9] = BitConverter.GetBytes(writeAddress)[0];//寄存器地址
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
        /// <param name="stationNumber">站号</param>
        /// <param name="functionCode">功能码</param>
        /// <returns></returns>
        public byte[] GetWriteCoilCommand(string address, bool value, byte stationNumber, byte functionCode, byte[] check = null, bool isPlcAddress = false)
        {
            if (isPlcAddress) { address = TranPLCAddress(address); }
            var writeAddress = ushort.Parse(address?.Trim());
            byte[] buffer = new byte[12];
            buffer[0] = check?[0] ?? 0x19;
            buffer[1] = check?[1] ?? 0xB2;//Client发出的检验信息     
            buffer[4] = 0x00;
            buffer[5] = 0x06;//表示的是该字节以后的字节长度

            buffer[6] = stationNumber;//站号
            buffer[7] = functionCode; //功能码
            buffer[8] = BitConverter.GetBytes(writeAddress)[1];
            buffer[9] = BitConverter.GetBytes(writeAddress)[0];//寄存器地址
            buffer[10] = (byte)(value ? 0xFF : 0x00);     //此处只可以是FF表示闭合00表示断开，其他数值非法
            buffer[11] = 0x00;
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

            byte[] buffer = new byte[13 + newValue.Length];
            buffer[0] = check?[0] ?? 0x19;
            buffer[1] = check?[1] ?? 0xB2;//检验信息，用来验证response是否串数据了           
            buffer[4] = BitConverter.GetBytes(7 + newValue.Length)[1];
            buffer[5] = BitConverter.GetBytes(7 + newValue.Length)[0];//表示的是header handle后面还有多长的字节

            buffer[6] = stationNumber; //站号
            buffer[7] = functionCode;  //功能码
            buffer[8] = BitConverter.GetBytes(writeAddress)[1];
            buffer[9] = BitConverter.GetBytes(writeAddress)[0];//寄存器地址
            buffer[10] = (byte)(values.Length  / 256);
            buffer[11] = (byte)(values.Length  % 256);//写寄存器数量(除2是两个字节一个寄存器，寄存器16位。除以256是byte最大存储255。)              
            buffer[12] = (byte)(newValue.Length);          //写字节的个数
            newValue.CopyTo(buffer, 13);                   //把目标值附加到数组后面
            return buffer;
        }


        #endregion      

    }
}

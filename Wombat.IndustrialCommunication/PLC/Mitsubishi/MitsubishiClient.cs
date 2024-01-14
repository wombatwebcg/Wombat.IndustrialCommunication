using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Wombat.Infrastructure;
using System.Threading.Tasks;

using Wombat.Network.Sockets;
using Wombat.Extensions.DataTypeExtensions;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 三菱plc客户端
    /// </summary>
    public class MitsubishiClient : PLCEthernetBase
    {



        /// <summary>
        /// 版本
        /// </summary>
        public override string Version => _version.ToString();

        private MitsubishiVersion _version;


        private AsyncLock _lock;


        /// <summary>
        /// 是否是连接的
        /// </summary>
        /// 
        public override bool Connected => _socket == null ? false : _socket.Connected;


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="version"></param>
        /// <param name="ipAndPoint"></param>
        public MitsubishiClient(MitsubishiVersion version)
        {
            this._version = version;
            _lock = new AsyncLock();
            IsReverse = false;
            DataFormat = EndianFormat.DCBA;

        }


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="version">三菱型号版本</param>
        /// <param name="ip">ip地址</param>
        /// <param name="port">端口</param>
        /// <param name="timeout">超时时间</param>
        public MitsubishiClient(MitsubishiVersion version, string ip, int port)
        {
            this._version = version;
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            DataFormat = EndianFormat.DCBA;
            IpEndPoint = new IPEndPoint(address, port);
            IsReverse = false;
            _lock = new AsyncLock();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="version"></param>
        /// <param name="ipAndPoint"></param>
        public MitsubishiClient(MitsubishiVersion version ,IPEndPoint ipAndPoint)
        {
            this._version = version;
            IpEndPoint = ipAndPoint;
            _lock = new AsyncLock();
            IsReverse = false;
            DataFormat = EndianFormat.DCBA;

        }

        /// <summary>
        /// 打开连接（如果已经是连接状态会先关闭再打开）
        /// </summary>
        /// <returns></returns>
        internal override OperationResult DoConnect()
        {
            var result = new OperationResult();
            _socket?.Close();
            _socket = new SocketClientBase();
            try
            {
                //超时时间设置
                _socket.SocketConfiguration.ReceiveTimeout = Timeout;
                _socket.SocketConfiguration.SendTimeout = Timeout;
                _socket.SocketConfiguration.ReceiveBufferSize = 1024;
                _socket.SocketConfiguration.SendBufferSize = 1024;
                _socket.Connect(IpEndPoint);
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
            var result = new OperationResult();
            _socket?.CloseAsync();
            _socket = new SocketClientBase();
            try
            {
                //超时时间设置
                _socket.SocketConfiguration.ReceiveTimeout = Timeout;
                _socket.SocketConfiguration.SendTimeout = Timeout;
                _socket.SocketConfiguration.ReceiveBufferSize = 1024;
                _socket.SocketConfiguration.SendBufferSize = 1024;
                await _socket.ConnectAsync(IpEndPoint);
            }
            catch (Exception ex)
            {
                await _socket?.CloseAsync();
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
                return result.Complete();
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.Exception = ex;
                return result.Complete();
            }

        }

        internal override async Task<OperationResult> DoDisconnectAsync()
        {
            OperationResult result = new OperationResult();
            try
            {
                await _socket?.CloseAsync();
                return result.Complete();
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.Exception = ex;
                return result.Complete();
            }
        }


        #region 发送报文，并获取响应报文
        /// <summary>
        /// 发送报文，并获取响应报文（建议使用SendPackageReliable，如果异常会自动重试一次）
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal override OperationResult<byte[]> GetMessageContent(byte[] command)
        {
            //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
            OperationResult<byte[]> result = new OperationResult<byte[]>();
            try
            {
                _socket.Send(command);
                var socketReadResult = ReadBuffer(9);
                if (!socketReadResult.IsSuccess)
                    return socketReadResult;
                var headPackage = socketReadResult.Value;

                //其后内容的总长度
                var contentLength = BitConverter.ToUInt16(headPackage, 7);
                socketReadResult = ReadBuffer(contentLength);
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

        internal override async ValueTask<OperationResult<byte[]>> GetMessageContentAsync(byte[] command)
        {
            OperationResult<byte[]> result = new OperationResult<byte[]>();
            try
            {
               await _socket.SendAsync(command);
                var socketReadResult =await ReadBufferAsync(9);
                if (!socketReadResult.IsSuccess)
                    return socketReadResult;
                var headPackage = socketReadResult.Value;

                //其后内容的总长度
                var contentLength = BitConverter.ToUInt16(headPackage, 7);
                socketReadResult = await ReadBufferAsync(contentLength);
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
        /// 发送报文，并获取响应报文
        /// </summary>
        /// <param name="command"></param>
        /// <param name="receiveCount"></param>
        /// <returns></returns>
        public OperationResult<byte[]> SendPackage(byte[] command, int receiveCount)
        {

            OperationResult<byte[]> _sendPackage()
            {
                //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
                lock (this)
                {
                    OperationResult<byte[]> result = new OperationResult<byte[]>();
                    _socket.Send(command);
                    var socketReadResult = ReadBuffer(receiveCount);
                    if (!socketReadResult.IsSuccess)
                        return socketReadResult;
                    var dataPackage = socketReadResult.Value;

                    result.Value = dataPackage.ToArray();
                    return result.Complete();
                }
            }

            try
            {
                var result = _sendPackage();
                if (!result.IsSuccess)
                {
                    WarningLog?.Invoke(result.Message, result.Exception);
                    //如果出现异常，则进行一次重试         
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                        return new OperationResult<byte[]>(connectResult);

                    return _sendPackage();
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

                return _sendPackage();
            }
        }
        #endregion



        /// <summary>
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public override OperationResult<bool> ReadBoolean(string address)
        {
            var readResut = Read(address, 1, isBit: true);
            var result = new OperationResult<bool>(readResut);
            if (result.IsSuccess)
                result.Value = (readResut.Value[0] & 0b00010000) != 0;
            return result.Complete();
        }

        public override async ValueTask<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            var readResut = await ReadAsync(address, 1, isBit: true);
            var result = new OperationResult<bool>(readResut);
            if (result.IsSuccess)
                result.Value = (readResut.Value[0] & 0b00010000) != 0;
            return result.Complete();
        }


        /// <summary>
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public override OperationResult<bool[]> ReadBoolean(string address, int length)
        {
            var readResult = Read(address, Convert.ToUInt16(length), isBit: true);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
            {
                result.Value = new bool[length];
                for (ushort i = 0; i < length; i++)
                {
                    var index = i / 2;
                    var isoffset = i % 2 == 0;
                    if (isoffset)
                        result.Value[i] = (readResult.Value[index] & 0b00010000) != 0;
                    else
                        result.Value[i] = (readResult.Value[index] & 0b00000001) != 0;
                }

            }
            return result.Complete();
        }


        public override async ValueTask<OperationResult<bool[]>> ReadBooleanAsync(string address, int length)
        {
            var readResult =await ReadAsync(address, Convert.ToUInt16(length), isBit: true);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
            {
                result.Value = new bool[length];
                for (ushort i = 0; i < length; i++)
                {
                    var index = i / 2;
                    var isoffset = i % 2 == 0;
                    if (isoffset)
                        result.Value[i] = (readResult.Value[index] & 0b00010000) != 0;
                    else
                        result.Value[i] = (readResult.Value[index] & 0b00000001) != 0;
                }

            }
            return result.Complete();
        }







        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>
        /// <returns></returns>
        public override OperationResult<byte[]> Read(string address, int length, bool isBit = false)
        {
            using (_lock.Lock())
            {
                if (!_socket?.Connected ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取{address}失败，{ connectResult.Message}";
                        return new OperationResult<byte[]>(connectResult).Complete();
                    }

                }
                var result = new OperationResult<byte[]>();
                try
                {
                    //发送读取信息
                    MitsubishiMCAddress arg = null;
                    byte[] command = null;

                    switch (_version)
                    {
                        case MitsubishiVersion.A_1E:
                            arg = ConvertArg_A_1E(address);
                            command = GetReadCommand_A_1E(arg.BeginAddress, arg.TypeCode, (ushort)length, isBit);
                            break;
                        case MitsubishiVersion.Qna_3E:
                            arg = ConvertArg_Qna_3E(address);
                            command = GetReadCommand_Qna_3E(arg.BeginAddress, arg.TypeCode, (ushort)length, isBit);
                            break;
                    }
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));

                    OperationResult<byte[]> sendResult = new OperationResult<byte[]>();
                    switch (_version)
                    {
                        case MitsubishiVersion.A_1E:
                            var lenght = command[10] + command[11] * 256;
                            if (isBit)
                                sendResult = SendPackage(command, (int)Math.Ceiling(lenght * 0.5) + 2);
                            else
                                sendResult = SendPackage(command, lenght * 2 + 2);
                            break;
                        case MitsubishiVersion.Qna_3E:
                            sendResult = InterpretAndExtractMessageData(command);
                            break;
                    }
                    if (!sendResult.IsSuccess)
                    {
                        return sendResult;

                    }

                    byte[] dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));

                    var bufferLength = length;
                    byte[] responseValue = null;

                    switch (_version)
                    {
                        case MitsubishiVersion.A_1E:
                            responseValue = new byte[dataPackage.Length - 2];
                            Array.Copy(dataPackage, 2, responseValue, 0, responseValue.Length);
                            break;
                        case MitsubishiVersion.Qna_3E:

                            if (isBit)
                            {
                                bufferLength = (ushort)Math.Ceiling(bufferLength * 0.5);
                            }
                            responseValue = new byte[bufferLength];
                            Array.Copy(dataPackage, dataPackage.Length - bufferLength, responseValue, 0, bufferLength);
                            break;
                    }

                    result.Value = responseValue;
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
                    _socket?.Close();
                }
                finally
                {
                    if (!IsUseLongConnect) Disconnect();
                }
               return result.Complete();
            }
        }

        public override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                if (!_socket?.Connected ?? true)
                {
                    var connectResult = await ConnectAsync();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"读取{address}失败，{ connectResult.Message}";
                        return new OperationResult<byte[]>(connectResult).Complete();
                    }

                }
                var result = new OperationResult<byte[]>();
                try
                {
                    //发送读取信息
                    MitsubishiMCAddress arg = null;
                    byte[] command = null;

                    switch (_version)
                    {
                        case MitsubishiVersion.A_1E:
                            arg = ConvertArg_A_1E(address);
                            command = GetReadCommand_A_1E(arg.BeginAddress, arg.TypeCode, (ushort)length, isBit);
                            break;
                        case MitsubishiVersion.Qna_3E:
                            arg = ConvertArg_Qna_3E(address);
                            command = GetReadCommand_Qna_3E(arg.BeginAddress, arg.TypeCode, (ushort)length, isBit);
                            break;
                    }
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));

                    OperationResult<byte[]> sendResult = new OperationResult<byte[]>();
                    switch (_version)
                    {
                        case MitsubishiVersion.A_1E:
                            var lenght = command[10] + command[11] * 256;
                            if (isBit)
                                sendResult = SendPackage(command, (int)Math.Ceiling(lenght * 0.5) + 2);
                            else
                                sendResult = SendPackage(command, lenght * 2 + 2);
                            break;
                        case MitsubishiVersion.Qna_3E:
                            sendResult = await InterpretAndExtractMessageDataAsync(command);
                            break;
                    }
                    if (!sendResult.IsSuccess)
                    {
                        return sendResult;

                    }

                    byte[] dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));

                    var bufferLength = length;
                    byte[] responseValue = null;

                    switch (_version)
                    {
                        case MitsubishiVersion.A_1E:
                            responseValue = new byte[dataPackage.Length - 2];
                            Array.Copy(dataPackage, 2, responseValue, 0, responseValue.Length);
                            break;
                        case MitsubishiVersion.Qna_3E:
                            if (isBit)
                            {
                                bufferLength = (ushort)Math.Ceiling(bufferLength * 0.5);
                            }
                            responseValue = new byte[bufferLength];
                            Array.Copy(dataPackage, dataPackage.Length - bufferLength, responseValue, 0, bufferLength);
                            break;
                    }

                    result.Value = responseValue;
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
                    await _socket?.CloseAsync();
                }
                finally
                {
                    if (!IsUseLongConnect)await DisconnectAsync();

                }
                return result.Complete();

            }
        }



        /// <summary>
        /// 向PLC中位软元件写入bool数组，返回值说明，比如你写入M100,values[0]对应M100
        /// </summary>
        /// <param name="address">要写入的数据地址</param>
        /// <param name="value">要写入的实际数据，长度为8的倍数</param>
        /// <example>
        /// 详细请查看<see cref="Write(string, bool[])"/>方法的示例
        /// </example>
        /// <returns>返回写入结果</returns>
        public override OperationResult Write(string address, bool value)
        {
            return this.Write(address, new bool[] { value });
        }

        public override async Task<OperationResult> WriteAsync(string address, bool value)
        {
            return await this.WriteAsync(address, new bool[] { value });
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public override async Task<OperationResult> WriteAsync(string address, bool[] value)
        {
            return await WriteAsync(address, ToBoolArrayToByteData(value), true);
        }


        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public override OperationResult Write(string address, bool[] value)
        {
            return Write(address, ToBoolArrayToByteData(value), true);
        }



        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address"></param>
        /// <param name="data"></param>
        /// <param name="isBit"></param>
        /// <returns></returns>
        public override OperationResult Write(string address, byte[] data, bool isBit = false)
        {
            using (_lock.Lock())
            {
                if (!_socket?.Connected ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        return connectResult.Complete();
                    }
                }
                OperationResult result = new OperationResult();
                try
                {
                    //发送写入信息
                    MitsubishiMCAddress arg = null;
                    byte[] command = null;
                    switch (_version)
                    {
                        case MitsubishiVersion.A_1E:
                            arg = ConvertArg_A_1E(address);
                            command = GetWriteCommand_A_1E(arg.BeginAddress, arg.TypeCode, data, isBit);
                            break;
                        case MitsubishiVersion.Qna_3E:
                            arg = ConvertArg_Qna_3E(address);
                            command = GetWriteCommand_Qna_3E(arg.BeginAddress, arg.TypeCode, data, isBit);
                            break;
                    }
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));

                    OperationResult<byte[]> sendResult = new OperationResult<byte[]>();
                    switch (_version)
                    {
                        case MitsubishiVersion.A_1E:
                            sendResult = SendPackage(command, 2);
                            break;
                        case MitsubishiVersion.Qna_3E:
                            sendResult = InterpretAndExtractMessageData(command);
                            break;
                    }
                    if (!sendResult.IsSuccess)
                    {
                        return sendResult;
                    }
                    byte[] dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
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
                        result.Exception = ex;
                    }
                    _socket?.Close();
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                    result.Exception = ex;
                    _socket?.Close();
                }
                finally
                {
                    if (!IsUseLongConnect) Disconnect();
                }
                return result.Complete();
            }
        }


        public override async Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                if (!_socket?.Connected ?? true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        return connectResult.Complete();
                    }
                }
                OperationResult result = new OperationResult();
                try
                {
                    //发送写入信息
                    MitsubishiMCAddress arg = null;
                    byte[] command = null;
                    switch (_version)
                    {
                        case MitsubishiVersion.A_1E:
                            arg = ConvertArg_A_1E(address);
                            command = GetWriteCommand_A_1E(arg.BeginAddress, arg.TypeCode, data, isBit);
                            break;
                        case MitsubishiVersion.Qna_3E:
                            arg = ConvertArg_Qna_3E(address);
                            command = GetWriteCommand_Qna_3E(arg.BeginAddress, arg.TypeCode, data, isBit);
                            break;
                    }
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));

                    OperationResult<byte[]> sendResult = new OperationResult<byte[]>(result);
                    switch (_version)
                    {
                        case MitsubishiVersion.A_1E:
                            sendResult = SendPackage(command, 2);
                            break;
                        case MitsubishiVersion.Qna_3E:
                            sendResult = await InterpretAndExtractMessageDataAsync(command);
                            break;
                    }
                    if (!sendResult.IsSuccess)
                    {
                        return sendResult;
                    }
                    byte[] dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
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
                        result.Exception = ex;
                    }
                    _socket?.Close();
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                    result.Exception = ex;
                    _socket?.Close();
                }
                finally
                {
                    if (!IsUseLongConnect) Disconnect();
                }
                return result.Complete();
            }
        }

        #region 生成报文命令
        /// <summary>
        /// 获取Qna_3E读取命令
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="typeCode"></param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>
        /// <returns></returns>
        protected byte[] GetReadCommand_Qna_3E(int beginAddress, byte[] typeCode, ushort length, bool isBit)
        {
            if (!isBit) length = (ushort)(length / 2);

            byte[] command = new byte[21];
            command[0] = 0x50;
            command[1] = 0x00; //副头部
            command[2] = 0x00; //网络编号
            command[3] = 0xFF; //PLC编号
            command[4] = 0xFF;
            command[5] = 0x03; //IO编号
            command[6] = 0x00; //模块站号
            command[7] = (byte)((command.Length - 9) % 256);
            command[8] = (byte)((command.Length - 9) / 256); // 请求数据长度
            command[9] = 0x0A;
            command[10] = 0x00; //时钟
            command[11] = 0x01;
            command[12] = 0x04;//指令（0x01 0x04读 0x01 0x14写）
            command[13] = isBit ? (byte)0x01 : (byte)0x00;//子指令（位 或 字节为单位）
            command[14] = 0x00;
            command[15] = BitConverter.GetBytes(beginAddress)[0];// 起始地址的地位
            command[16] = BitConverter.GetBytes(beginAddress)[1];
            command[17] = BitConverter.GetBytes(beginAddress)[2];
            command[18] = typeCode[0]; //数据类型
            command[19] = (byte)(length % 256);
            command[20] = (byte)(length / 256); //长度
            return command;
        }

        /// <summary>
        /// 获取A_1E读取命令
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="typeCode"></param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>
        /// <returns></returns>
        protected byte[] GetReadCommand_A_1E(int beginAddress, byte[] typeCode, ushort length, bool isBit)
        {
            if (!isBit)
                length = (ushort)(length / 2);
            byte[] command = new byte[12];
            command[0] = isBit ? (byte)0x00 : (byte)0x01;//副头部
            command[1] = 0xFF; //PLC编号
            command[2] = 0x0A;
            command[3] = 0x00;
            command[4] = BitConverter.GetBytes(beginAddress)[0]; // 
            command[5] = BitConverter.GetBytes(beginAddress)[1]; // 开始读取的地址
            command[6] = 0x00;
            command[7] = 0x00;
            command[8] = typeCode[1];
            command[9] = typeCode[0];
            command[10] = (byte)(length % 256);//长度
            command[11] = (byte)(length / 256);
            return command;
        }


        /// <summary>
        /// 将bool的组压缩成三菱格式的字节数组来表示开关量的
        /// </summary>
        /// <param name="value">原始的数据字节</param>
        /// <returns>压缩过后的数据字节</returns>
        internal static byte[] ToBoolArrayToByteData(bool[] value)
        {
            int length = (value.Length + 1) / 2;
            byte[] buffer = new byte[length];
            for (ushort i = 0; i < value.Length; i++)
            {
                var index = i / 2;
                var isoffset = i % 2 == 0;
                if (isoffset)
                    buffer[index] += (byte)(value[i] ? 0b00010000 : 0b00000000);
                else
                    buffer[index] += (byte)(value[i] ? 0b00000001 : 0b00000000);
            }
            return buffer;
        }



        /// <summary>
        /// 获取Qna_3E写入命令
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="typeCode"></param>
        /// <param name="data"></param>
        /// <param name="isBit"></param>
        /// <returns></returns>
        protected byte[] GetWriteCommand_Qna_3E(int beginAddress, byte[] typeCode, byte[] data, bool isBit)
        {
            var length = data.Length / 2;
            //var length = data.Length*2 ;

            if (isBit)
            {
                length = data.Length < 2 ? 1 : data.Length * 2;
            }

            byte[] command = new byte[21 + data.Length];
            command[0] = 0x50;
            command[1] = 0x00; //副头部
            command[2] = 0x00; //网络编号
            command[3] = 0xFF; //PLC编号
            command[4] = 0xFF;
            command[5] = 0x03; //IO编号
            command[6] = 0x00; //模块站号
            command[7] = (byte)((command.Length - 9) % 256);// 请求数据长度
            command[8] = (byte)((command.Length - 9) / 256);
            command[9] = 0x0A;
            command[10] = 0x00; //时钟
            command[11] = 0x01;
            command[12] = 0x14;//指令（0x01 0x04读 0x01 0x14写）
            command[13] = isBit ? (byte)0x01 : (byte)0x00;//子指令（位 或 字节为单位）
            command[14] = 0x00;
            command[15] = BitConverter.GetBytes(beginAddress)[0];// 起始地址的地位
            command[16] = BitConverter.GetBytes(beginAddress)[1];
            command[17] = BitConverter.GetBytes(beginAddress)[2];
            command[18] = typeCode[0];//数据类型
            command[19] = (byte)(length % 256);
            command[20] = (byte)(length / 256); //长度
            data.ToArray().CopyTo(command, 21);
            return command;
        }




        /// <summary>
        /// 获取A_1E写入命令
        /// </summary>
        /// <param name="beginAddress"></param>
        /// <param name="typeCode"></param>
        /// <param name="data"></param>
        /// <param name="isBit"></param>
        /// <returns></returns>
        protected byte[] GetWriteCommand_A_1E(int beginAddress, byte[] typeCode, byte[] data, bool isBit)
        {
            var length = data.Length / 2;
            if (isBit) length = data.Length;

            byte[] command = new byte[12 + data.Length];
            command[0] = isBit ? (byte)0x02 : (byte)0x03;     //副标题
            command[1] = 0xFF;                             // PLC号
            command[2] = 0x0A;
            command[3] = 0x00;
            command[4] = BitConverter.GetBytes(beginAddress)[0];        //
            command[5] = BitConverter.GetBytes(beginAddress)[1];        //起始地址的地位
            command[6] = 0x00;
            command[7] = 0x00;
            command[8] = typeCode[1];        //
            command[9] = typeCode[0];        //数据类型
            command[10] = (byte)(length % 256);
            command[11] = (byte)(length / 256);
            data.ToArray().CopyTo(command, 12);
            return command;
        }
        #endregion

        #region private        

        #region 地址解析
        /// <summary>
        /// Qna_3E地址解析
        /// </summary>
        /// <param name="address"></param>
        /// <param name="toUpper"></param>
        /// <returns></returns>
        private MitsubishiMCAddress ConvertArg_Qna_3E(string address, DataTypeEnum dataType = DataTypeEnum.None, bool toUpper = true)
        {
            if (toUpper) address = address.ToUpper();
            var addressInfo = new MitsubishiMCAddress()
            {
                DataTypeEnum = dataType
            };
            switch (address[0])
            {
                case 'M'://M中间继电器
                    {
                        addressInfo.TypeCode = new byte[] { 0x90 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'X':// X输入继电器
                    {
                        addressInfo.TypeCode = new byte[] { 0x9C };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 8;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'Y'://Y输出继电器
                    {
                        addressInfo.TypeCode = new byte[] { 0x9D };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 8;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'D'://D数据寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0xA8 };
                        addressInfo.BitType = 0x00;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'W'://W链接寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0xB4 };
                        addressInfo.BitType = 0x00;
                        addressInfo.Format = 16;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'L'://L锁存继电器
                    {
                        addressInfo.TypeCode = new byte[] { 0x92 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'F'://F报警器
                    {
                        addressInfo.TypeCode = new byte[] { 0x93 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'V'://V边沿继电器
                    {
                        addressInfo.TypeCode = new byte[] { 0x94 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'B'://B链接继电器
                    {
                        addressInfo.TypeCode = new byte[] { 0xA0 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 16;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'R'://R文件寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0xAF };
                        addressInfo.BitType = 0x00;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'S':
                    {
                        //累计定时器的线圈
                        if (address[1] == 'C')
                        {
                            addressInfo.TypeCode = new byte[] { 0xC6 };
                            addressInfo.BitType = 0x01;
                            addressInfo.Format = 10;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(2), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 2);
                        }
                        //累计定时器的触点
                        else if (address[1] == 'S')
                        {
                            addressInfo.TypeCode = new byte[] { 0xC7 };
                            addressInfo.BitType = 0x01;
                            addressInfo.Format = 10;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(2), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 2);
                        }
                        //累计定时器的当前值
                        else if (address[1] == 'N')
                        {
                            addressInfo.TypeCode = new byte[] { 0xC8 };
                            addressInfo.BitType = 0x00;
                            addressInfo.Format = 100;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(2), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 2);
                        }
                        // S步进继电器
                        else
                        {
                            addressInfo.TypeCode = new byte[] { 0x98 };
                            addressInfo.BitType = 0x01;
                            addressInfo.Format = 10;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 1);
                        }
                        break;
                    }
                case 'Z':
                    {
                        //文件寄存器ZR区
                        if (address[1] == 'R')
                        {
                            addressInfo.TypeCode = new byte[] { 0xB0 };
                            addressInfo.BitType = 0x00;
                            addressInfo.Format = 16;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(2), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 2);
                        }
                        //变址寄存器
                        else
                        {
                            addressInfo.TypeCode = new byte[] { 0xCC };
                            addressInfo.BitType = 0x00;
                            addressInfo.Format = 10;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 1);
                        }
                        break;
                    }
                case 'T':
                    {
                        // 定时器的当前值
                        if (address[1] == 'N')
                        {
                            addressInfo.TypeCode = new byte[] { 0xC2 };
                            addressInfo.BitType = 0x00;
                            addressInfo.Format = 10;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(2), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 2);
                        }
                        //定时器的触点
                        else if (address[1] == 'S')
                        {
                            addressInfo.TypeCode = new byte[] { 0xC1 };
                            addressInfo.BitType = 0x01;
                            addressInfo.Format = 10;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(2), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 2);
                        }
                        //定时器的线圈
                        else if (address[1] == 'C')
                        {
                            addressInfo.TypeCode = new byte[] { 0xC0 };
                            addressInfo.BitType = 0x01;
                            addressInfo.Format = 10;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(2), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 2);
                        }
                        break;
                    }
                case 'C':
                    {
                        //计数器的当前值
                        if (address[1] == 'N')
                        {
                            addressInfo.TypeCode = new byte[] { 0xC5 };
                            addressInfo.BitType = 0x00;
                            addressInfo.Format = 10;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(2), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 2);
                        }
                        //计数器的触点
                        else if (address[1] == 'S')
                        {
                            addressInfo.TypeCode = new byte[] { 0xC4 };
                            addressInfo.BitType = 0x01;
                            addressInfo.Format = 10;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(2), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 2);
                        }
                        //计数器的线圈
                        else if (address[1] == 'C')
                        {
                            addressInfo.TypeCode = new byte[] { 0xC3 };
                            addressInfo.BitType = 0x01;
                            addressInfo.Format = 10;
                            addressInfo.BeginAddress = Convert.ToInt32(address.Substring(2), addressInfo.Format);
                            addressInfo.TypeChar = address.Substring(0, 2);
                        }
                        break;
                    }
            }
            return addressInfo;
        }

        /// <summary>
        /// A_1E地址解析
        /// </summary>
        /// <param name="address"></param>
        /// <param name="toUpper"></param>
        /// <returns></returns>
        private MitsubishiMCAddress ConvertArg_A_1E(string address, bool toUpper = true)
        {
            if (toUpper) address = address.ToUpper();
            var addressInfo = new MitsubishiMCAddress();
            switch (address[0])
            {
                case 'X'://X输入寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x58, 0x20 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 8;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'Y'://Y输出寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x59, 0x20 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 8;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'M'://M中间寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x4D, 0x20 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'S'://S状态寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x53, 0x20 };
                        addressInfo.BitType = 0x01;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'D'://D数据寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x44, 0x20 };
                        addressInfo.BitType = 0x00;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
                case 'R'://R文件寄存器
                    {
                        addressInfo.TypeCode = new byte[] { 0x52, 0x20 };
                        addressInfo.BitType = 0x00;
                        addressInfo.Format = 10;
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1), addressInfo.Format);
                        addressInfo.TypeChar = address.Substring(0, 1);
                    }
                    break;
            }
            return addressInfo;
        }









        #endregion

        #region TODO
        public override OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnum> addresses)
        {
            var result = new OperationResult<Dictionary<string, object>>();
            result.Value = new Dictionary<string, object>();

            var mitsubishiMCAddresses = addresses.Select(t => ConvertArg_Qna_3E(t.Key, t.Value)).ToList();
            var typeChars = mitsubishiMCAddresses.Select(t => t.TypeChar).Distinct();
            foreach (var typeChar in typeChars)
            {
                var tempAddresses = mitsubishiMCAddresses.Where(t => t.TypeChar == typeChar).ToList();
                var minAddress = tempAddresses.Select(t => t.BeginAddress).Min();
                var maxAddress = tempAddresses.Select(t => t.BeginAddress).Max();

                while (maxAddress >= minAddress)
                {
                    int readLength = 121;//TODO 分批读取的长度

                    var tempAddress = tempAddresses.Where(t => t.BeginAddress >= minAddress && t.BeginAddress <= minAddress + readLength).ToList();
                    //如果范围内没有数据。按正确逻辑不存在这种情况。
                    if (!tempAddress.Any())
                    {
                        minAddress = minAddress + readLength;
                        continue;
                    }

                    var tempMax = tempAddress.OrderByDescending(t => t.BeginAddress).FirstOrDefault();
                    switch (tempMax.DataTypeEnum)
                    {
                        case DataTypeEnum.Bool:
                        case DataTypeEnum.Byte:
                            readLength = tempMax.BeginAddress + 1 - minAddress;
                            break;
                        case DataTypeEnum.Int16:
                        case DataTypeEnum.UInt16:
                            readLength = tempMax.BeginAddress * 2 + 2 - minAddress * 2;
                            break;
                        case DataTypeEnum.Int32:
                        case DataTypeEnum.UInt32:
                        case DataTypeEnum.Float:
                            readLength = tempMax.BeginAddress * 4 + 4 - minAddress * 4;
                            break;
                        case DataTypeEnum.Int64:
                        case DataTypeEnum.UInt64:
                        case DataTypeEnum.Double:
                            readLength = tempMax.BeginAddress + 8 - minAddress;
                            break;
                        default:
                            throw new Exception("Message BatchRead 未定义类型 -1");
                    }

                    //TODO isbit
                    //TODO 直接传入MitsubishiMCAddress
                    var tempResult = Read(typeChar + minAddress.ToString(), Convert.ToUInt16(readLength), false);

                    if (!tempResult.IsSuccess)
                    {
                        result.IsSuccess = tempResult.IsSuccess;
                        result.Exception = tempResult.Exception;
                        result.ErrorCode = tempResult.ErrorCode;
                        result.Message = tempResult.Message;
                        return result.Complete();
                    }

                    var rValue = tempResult.Value.ToArray();
                    foreach (var item in tempAddress)
                    {
                        object tempVaue = null;

                        switch (item.DataTypeEnum)
                        {
                            case DataTypeEnum.Bool:
                            //tempVaue = ReadCoil(minAddress, item.Key, rValue).Value;
                            //break;
                            case DataTypeEnum.Byte:
                                throw new Exception("Message BatchRead 未定义类型 -2");
                            case DataTypeEnum.Int16:
                                tempVaue = ReadInt16(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            //case DataTypeEnum.UInt16:
                            //    tempVaue = ReadUInt16(minAddress, item.BeginAddress, rValue).Value;
                            //    break;
                            //case DataTypeEnum.Int32:
                            //    tempVaue = ReadInt32(minAddress, item.BeginAddress, rValue).Value;
                            //    break;
                            //case DataTypeEnum.UInt32:
                            //    tempVaue = ReadUInt32(minAddress, item.BeginAddress, rValue).Value;
                            //    break;
                            //case DataTypeEnum.Int64:
                            //    tempVaue = ReadInt64(minAddress, item.BeginAddress, rValue).Value;
                            //    break;
                            //case DataTypeEnum.UInt64:
                            //    tempVaue = ReadUInt64(minAddress, item.BeginAddress, rValue).Value;
                            //    break;
                            case DataTypeEnum.Float:
                                tempVaue = ReadFloat(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            //case DataTypeEnum.Double:
                            //    tempVaue = ReadDouble(minAddress, item.BeginAddress, rValue).Value;
                            //    break;
                            default:
                                throw new Exception("Message BatchRead 未定义类型 -3");
                        }

                        result.Value.Add(item.TypeChar + item.BeginAddress.ToString(), tempVaue);
                    }
                    minAddress = minAddress + readLength;

                    if (tempAddresses.Any(t => t.BeginAddress >= minAddress))
                        minAddress = tempAddresses.Where(t => t.BeginAddress >= minAddress).OrderBy(t => t.BeginAddress).FirstOrDefault().BeginAddress;
                    //else
                    //    return result.Complete();
                }
                //return result.Complete();
            }
            return result.Complete();
        }

        public override OperationResult BatchWrite(Dictionary<string, object> addresses)
        {
            throw new NotImplementedException();
        }





        #endregion

        ///// <summary>
        ///// 获取地址的区域类型
        ///// </summary>
        ///// <param name="address"></param>
        ///// <returns></returns>
        //private string GetAddressType(string address)
        //{
        //    if (address.Length < 2)
        //        throw new Exception("address格式不正确");

        //    if ((address[1] >= 'A' && address[1] <= 'Z') ||
        //        (address[1] >= 'a' && address[1] <= 'z'))
        //        return address.Substring(0, 2);
        //    else
        //        return address.Substring(0, 1);
        //}
        #endregion
    }
}

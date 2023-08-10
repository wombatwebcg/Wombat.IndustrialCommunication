using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Wombat.Infrastructure;

using Wombat.Core;
using System.Threading.Tasks;
using Wombat.Network.Sockets;

namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 西门子客户端
    /// http://www.360doc.cn/mip/763580999.html
    /// </summary>
    public class SiemensClient : PLCEthernetBase
    {
        //protected TcpRawSocketClient _socket;
        private AsyncLock _lock; 
        /// <summary>
        /// CPU版本
        /// </summary>
        private readonly SiemensVersion version;

        /// <summary>
        /// 是否是连接的
        /// </summary>
        /// 
        public override bool Connected => _socket==null?false:_socket.Connected;

        /// <summary>
        /// 版本
        /// </summary>
        public override string Version => version.ToString();

        /// <summary>
        /// 插槽号 
        /// </summary>
        public byte Slot { get; private set; }

        /// <summary>
        /// 机架号
        /// </summary>
        public byte Rack { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="version">CPU版本</param>
        /// <param name="ipAndPoint">IP地址和端口号</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="slot">PLC的插槽号</param>
        /// <param name="rack">PLC的机架号</param>
        public SiemensClient(SiemensVersion version, byte slot = 0x00, byte rack = 0x00)
        {
            Slot = slot;
            Rack = rack;
            this.version = version;
            DataFormat = EndianFormat.DCBA;
            IsReverse = true;
            _lock = new AsyncLock();
        }



        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="version">CPU版本</param>
        /// <param name="ipAndPoint">IP地址和端口号</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="slot">PLC的插槽号</param>
        /// <param name="rack">PLC的机架号</param>
        public SiemensClient(SiemensVersion version, IPEndPoint ipAndPoint, byte slot = 0x00, byte rack = 0x00)
        {
            Slot = slot;
            Rack = rack;
            this.version = version;
            IpEndPoint = ipAndPoint;
            DataFormat = EndianFormat.DCBA;
            IsReverse = true;
            _lock = new AsyncLock();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="version">CPU版本</param>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口号</param>
        /// <param name="slot">PLC的槽号</param>
        /// <param name="rack">PLC的机架号</param>
        /// <param name="timeout">超时时间</param>
        public SiemensClient(SiemensVersion version, string ip, int port, byte slot = 0x00, byte rack = 0x00)
        {
            Slot = slot;
            Rack = rack;
            this.version = version;
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            IpEndPoint = new IPEndPoint(address, port);
            DataFormat = EndianFormat.DCBA;
            IsReverse = true;
            _lock = new AsyncLock();

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

                //连接
                //socket.Connect(IpEndPoint);
                //阻塞当前线程           

                var Command1 = SiemensConstant.Command1;
                var Command2 = SiemensConstant.Command2;

                switch (version)
                {
                    case SiemensVersion.S7_200:
                        Command1 = SiemensConstant.Command1_200;
                        Command2 = SiemensConstant.Command2_200;
                        break;
                    case SiemensVersion.S7_200Smart:
                        Command1 = SiemensConstant.Command1_200Smart;
                        Command2 = SiemensConstant.Command2_200Smart;
                        break;
                    case SiemensVersion.S7_300:
                        Command1[21] = (byte)((Rack * 0x20) + Slot); //0x02;
                        break;
                    case SiemensVersion.S7_400:
                        Command1[21] = (byte)((Rack * 0x20) + Slot); //0x03;
                        Command1[17] = 0x00;
                        break;
                    case SiemensVersion.S7_1200:
                        Command1[21] = (byte)((Rack * 0x20) + Slot); //0x00;
                        break;
                    case SiemensVersion.S7_1500:
                        Command1[21] = (byte)((Rack * 0x20) + Slot); //0x00;
                        break;
                    default:
                        Command1[18] = 0x00;
                        break;
                }

                result.Requsts[0] = string.Join(" ", Command1.Select(t => t.ToString("X2")));
                //第一次初始化指令交互
                _socket.Send(Command1);

                var socketReadResult = ReadBuffer(SiemensConstant.InitHeadLength);
                if (!socketReadResult.IsSuccess)
                {
                    return socketReadResult;
                }
                var head1 = socketReadResult.Value;


                socketReadResult = ReadBuffer(GetContentLength(head1));
                if (!socketReadResult.IsSuccess)
                {
                    return socketReadResult;
                }
                var content1 = socketReadResult.Value;

                result.Responses[0] = string.Join(" ", head1.Concat(content1).Select(t => t.ToString("X2")));

                result.Requsts[1] = string.Join(" ", Command2.Select(t => t.ToString("X2")));
                //第二次初始化指令交互
                _socket.Send(Command2);

                socketReadResult = ReadBuffer(SiemensConstant.InitHeadLength);
                if (!socketReadResult.IsSuccess)
                {
                    return socketReadResult;
                }
                var head2 = socketReadResult.Value;

                socketReadResult = ReadBuffer(GetContentLength(head2));
                if (!socketReadResult.IsSuccess)
                {
                    return socketReadResult;

                }
                var content2 = socketReadResult.Value;

                result.Responses[1] = string.Join(" ", head2.Concat(content2).Select(t => t.ToString("X2")));
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

                //连接
                //socket.Connect(IpEndPoint);
                //阻塞当前线程           

                var Command1 = SiemensConstant.Command1;
                var Command2 = SiemensConstant.Command2;

                switch (version)
                {
                    case SiemensVersion.S7_200:
                        Command1 = SiemensConstant.Command1_200;
                        Command2 = SiemensConstant.Command2_200;
                        break;
                    case SiemensVersion.S7_200Smart:
                        Command1 = SiemensConstant.Command1_200Smart;
                        Command2 = SiemensConstant.Command2_200Smart;
                        break;
                    case SiemensVersion.S7_300:
                        Command1[21] = (byte)((Rack * 0x20) + Slot); //0x02;
                        break;
                    case SiemensVersion.S7_400:
                        Command1[21] = (byte)((Rack * 0x20) + Slot); //0x03;
                        Command1[17] = 0x00;
                        break;
                    case SiemensVersion.S7_1200:
                        Command1[21] = (byte)((Rack * 0x20) + Slot); //0x00;
                        break;
                    case SiemensVersion.S7_1500:
                        Command1[21] = (byte)((Rack * 0x20) + Slot); //0x00;
                        break;
                    default:
                        Command1[18] = 0x00;
                        break;
                }

                result.Requsts[0] = string.Join(" ", Command1.Select(t => t.ToString("X2")));
                //第一次初始化指令交互
                _socket.Send(Command1);

                var socketReadResult = await ReadBufferAsync(SiemensConstant.InitHeadLength);
                if (!socketReadResult.IsSuccess)
                {
                    return socketReadResult;
                }
                var head1 = socketReadResult.Value;


                socketReadResult = await ReadBufferAsync(GetContentLength(head1));
                if (!socketReadResult.IsSuccess)
                {
                    return socketReadResult;
                }
                var content1 = socketReadResult.Value;

                result.Responses[0] = string.Join(" ", head1.Concat(content1).Select(t => t.ToString("X2")));

                result.Requsts[1] = string.Join(" ", Command2.Select(t => t.ToString("X2")));
                //第二次初始化指令交互
                _socket.Send(Command2);

                socketReadResult = await ReadBufferAsync(SiemensConstant.InitHeadLength);
                if (!socketReadResult.IsSuccess)
                {
                    return socketReadResult;
                }
                var head2 = socketReadResult.Value;

                socketReadResult = await ReadBufferAsync(GetContentLength(head2));
                if (!socketReadResult.IsSuccess)
                {
                    return socketReadResult;

                }
                var content2 = socketReadResult.Value;

                result.Responses[1] = string.Join(" ", head2.Concat(content2).Select(t => t.ToString("X2")));
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

        internal override async  Task<OperationResult> DoDisconnectAsync()
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
                var socketReadResult = ReadBuffer(SiemensConstant.InitHeadLength);
                if (!socketReadResult.IsSuccess)
                    return socketReadResult;
                var headPackage = socketReadResult.Value;

                socketReadResult = ReadBuffer(GetContentLength(headPackage));
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
        /// 发送报文，并获取响应报文（建议使用SendPackageReliable，如果异常会自动重试一次）
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal override async ValueTask<OperationResult<byte[]>> GetMessageContentAsync(byte[] command)
        {
            OperationResult<byte[]> result = new OperationResult<byte[]>();
            try
            {

                await _socket.SendAsync(command);
                var socketReadResult = await ReadBufferAsync(SiemensConstant.InitHeadLength);
                if (!socketReadResult.IsSuccess)
                    return socketReadResult;
                var headPackage = socketReadResult.Value;
               
                socketReadResult = await ReadBufferAsync(GetContentLength(headPackage));
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
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public override OperationResult<bool> ReadBoolean(string address)
        {
            var readResult = Read(address, 1, isBit: true);
            var result = new OperationResult<bool>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, 1)[0];
            return result.Complete();
        }


        /// <summary>
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public override async ValueTask<OperationResult<bool>> ReadBooleanAsync(string address)
        {
            var readResult = await ReadAsync(address, 1, isBit: true);
            var result = new OperationResult<bool>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, 1)[0];
            return result.Complete();
        }


        /// <summary>
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public override OperationResult<bool[]> ReadBoolean(string address, int length)
        {
            var readResult = Read(address, length*2, isBit: true);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, length);
            return result.Complete();
        }


        /// <summary>
        /// 读取Boolean
        /// </summary>
        /// <param name="address">地址</param>
        /// <returns></returns>
        public override async ValueTask<OperationResult<bool[]>> ReadBooleanAsync(string address, int length)
        {
            var readResult = await ReadAsync (address, length * 2, isBit: true);
            var result = new OperationResult<bool[]>(readResult);
            if (result.IsSuccess)
                result.Value = readResult.Value.ToBool(0, length);
            return result.Complete();
        }


        /// <summary>
        /// 读取字节数组
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length">读取长度</param>
        /// <param name="isBit">是否Bit类型</param>        
        /// <returns></returns>
        public override OperationResult<byte[]> Read(string address, int length, bool isBit = false)
        {
            using (_lock.Lock())
            {
                if (!Connected)
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
                    var arg = ConvertArg(address);
                    arg.ReadWriteLength = (ushort)length;
                    arg.ReadWriteBit = isBit;
                    byte[] command = GetReadCommand(arg);
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                    //发送命令 并获取响应报文
                    var sendResult = InterpretAndExtractMessageData(command);
                    if (!sendResult.IsSuccess)
                    {
                        sendResult.Message = $"读取{address}失败，{ sendResult.Message}";
                        return result.SetInfo(sendResult).Complete();
                    }
                    var dataPackage = sendResult.Value;

                    //length = dataPackage.Length - 21;

                    byte[] responseData = new byte[length];
                    Array.Copy(dataPackage, dataPackage.Length - length, responseData, 0, length);
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                    result.Value = responseData.ToArray();

                    //0x04 读 0x01 读取一个长度 //如果是批量读取，批量读取方法里面有验证
                    if (dataPackage[19] == 0x04 && dataPackage[20] == 0x01)
                    {
                        if (dataPackage[21] == 0x0A && dataPackage[22] == 0x00)
                        {
                            result.IsSuccess = false;
                            result.Message = $"读取{address}失败，请确认是否存在地址{address}";
                        }
                        else if (dataPackage[21] == 0x05 && dataPackage[22] == 0x00)
                        {
                            result.IsSuccess = false;
                            result.Message = $"读取{address}失败，请确认是否存在地址{address}";
                        }
                        else if (dataPackage[21] != 0xFF)
                        {
                            result.IsSuccess = false;
                            result.Message = $"读取{address}失败，异常代码[{21}]:{dataPackage[21]}";
                        }
                    }

                }
                catch (SocketException ex)
                {
                    result.IsSuccess = false;
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        result.Message = $"读取{address}失败，连接超时";
                    }
                    else
                    {
                        result.Message = $"读取{address}失败，{ ex.Message}";
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


        /// <summary>
        /// 读取字节数组
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length">读取长度</param>
        /// <param name="isBit">是否Bit类型</param>        
        /// <returns></returns>
        public override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            using (await _lock.LockAsync())
            {
                if (!Connected)
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
                    var arg = ConvertArg(address);
                    arg.ReadWriteLength = (ushort)length;
                    arg.ReadWriteBit = isBit;
                    byte[] command = GetReadCommand(arg);
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                    //发送命令 并获取响应报文
                    var sendResult = await InterpretAndExtractMessageDataAsync(command);
                    if (!sendResult.IsSuccess)
                    {
                        sendResult.Message = $"读取{address}失败，{ sendResult.Message}";
                        return result.SetInfo(sendResult).Complete();
                    }
                    var dataPackage = sendResult.Value;

                    //length = dataPackage.Length - 21;

                    byte[] responseData = new byte[length];
                    Array.Copy(dataPackage, dataPackage.Length - length, responseData, 0, length);
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                    result.Value = responseData.ToArray();

                    //0x04 读 0x01 读取一个长度 //如果是批量读取，批量读取方法里面有验证
                    if (dataPackage[19] == 0x04 && dataPackage[20] == 0x01)
                    {
                        if (dataPackage[21] == 0x0A && dataPackage[22] == 0x00)
                        {
                            result.IsSuccess = false;
                            result.Message = $"读取{address}失败，请确认是否存在地址{address}";
                        }
                        else if (dataPackage[21] == 0x05 && dataPackage[22] == 0x00)
                        {
                            result.IsSuccess = false;
                            result.Message = $"读取{address}失败，请确认是否存在地址{address}";
                        }
                        else if (dataPackage[21] != 0xFF)
                        {
                            result.IsSuccess = false;
                            result.Message = $"读取{address}失败，异常代码[{21}]:{dataPackage[21]}";
                        }
                    }

                }
                catch (SocketException ex)
                {
                    result.IsSuccess = false;
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        result.Message = $"读取{address}失败，连接超时";
                    }
                    else
                    {
                        result.Message = $"读取{address}失败，{ ex.Message}";
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




        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="data">值</param>
        /// <param name="isBit">值</param>
        /// <returns></returns>
        public override OperationResult Write(string address, byte[] data, bool isBit = false)
        {
            using (_lock.Lock())
            {
                if (!Connected)
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
                    //Array.Reverse(data);
                    //发送写入信息
                    var arg = ConvertWriteArg(address, data, isBit);
                    byte[] command = GetWriteCommand(arg);
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                    var sendResult = InterpretAndExtractMessageData(command);
                    if (!sendResult.IsSuccess)
                    {
                        return sendResult;
                    }

                    var dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));

                    var offset = dataPackage.Length - 1;
                    if (dataPackage[offset] == 0x0A)
                    {
                        result.IsSuccess = false;
                        result.Message = $"写入{address}失败，请确认是否存在地址{address}，异常代码[{offset}]:{dataPackage[offset]}";
                    }
                    else if (dataPackage[offset] == 0x05)
                    {
                        result.IsSuccess = false;
                        result.Message = $"写入{address}失败，请确认是否存在地址{address}，异常代码[{offset}]:{dataPackage[offset]}";
                    }
                    else if (dataPackage[offset] != 0xFF)
                    {
                        result.IsSuccess = false;
                        result.Message = $"写入{address}失败，异常代码[{offset}]:{dataPackage[offset]}";
                    }
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
                if (!Connected)
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
                    //Array.Reverse(data);
                    //发送写入信息
                    var arg = ConvertWriteArg(address, data, isBit);
                    byte[] command = GetWriteCommand(arg);
                    result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                    var sendResult = await InterpretAndExtractMessageDataAsync(command);
                    if (!sendResult.IsSuccess)
                    {
                        return sendResult;
                    }

                    var dataPackage = sendResult.Value;
                    result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));

                    var offset = dataPackage.Length - 1;
                    if (dataPackage[offset] == 0x0A)
                    {
                        result.IsSuccess = false;
                        result.Message = $"写入{address}失败，请确认是否存在地址{address}，异常代码[{offset}]:{dataPackage[offset]}";
                    }
                    else if (dataPackage[offset] == 0x05)
                    {
                        result.IsSuccess = false;
                        result.Message = $"写入{address}失败，请确认是否存在地址{address}，异常代码[{offset}]:{dataPackage[offset]}";
                    }
                    else if (dataPackage[offset] != 0xFF)
                    {
                        result.IsSuccess = false;
                        result.Message = $"写入{address}失败，异常代码[{offset}]:{dataPackage[offset]}";
                    }
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



        /// <summary>
        /// 分批读取，默认按19个地址打包读取
        /// </summary>
        /// <param name="addresses">地址集合</param>
        /// <param name="batchNumber">批量读取数量</param>
        /// <returns></returns>
        public override OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnum> addresses)
        {
            int batchNumber = 19;
            var result = new OperationResult<Dictionary<string, object>>();
            result.Value = new Dictionary<string, object>();

            var batchCount = Math.Ceiling((float)addresses.Count / batchNumber);
            for (int i = 0; i < batchCount; i++)
            {
                var tempAddresses = addresses.Skip(i * batchNumber).Take(batchNumber).ToDictionary(t => t.Key, t => t.Value);
                var tempResult = BatchReadBase(tempAddresses);
                if (!tempResult.IsSuccess)
                {
                    result.IsSuccess = false;
                    result.Message = tempResult.Message;
                    result.Exception = tempResult.Exception;
                    result.ErrorCode = tempResult.ErrorCode;
                }

                if (tempResult.Value?.Any() ?? false)
                {
                    foreach (var item in tempResult.Value)
                    {
                        result.Value.Add(item.Key, item.Value);
                    }
                }

                result.Requsts = tempResult.Requsts;
                result.Responses = tempResult.Responses;
            }
            return result.Complete();
        }

        /// <summary>
        /// 最多只能批量读取19个数据？        
        /// </summary>
        /// <param name="addresses"></param>
        /// <returns></returns>
        private OperationResult<Dictionary<string, object>> BatchReadBase(Dictionary<string, DataTypeEnum> addresses)
        {
            if (!Connected)
            {
                var connectResult = Connect();
                if (!connectResult.IsSuccess)
                {
                    return new OperationResult<Dictionary<string, object>>(connectResult);
                }
            }
            var result = new OperationResult<Dictionary<string, object>>();
            result.Value = new Dictionary<string, object>();
            try
            {
                //发送读取信息
                var args = ConvertArg(addresses);
                byte[] command = GetReadCommand(args);
                result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                //发送命令 并获取响应报文
                var sendResult = InterpretAndExtractMessageData(command);
                if (!sendResult.IsSuccess)
                    return new OperationResult<Dictionary<string, object>>(sendResult);

                var dataPackage = sendResult.Value;

                //2021.5.27注释，直接使用【var length = dataPackage.Length - 21】代替。
                //DataType类型为Bool的时候需要读取两个字节
                //var length = args.Sum(t => t.ReadWriteLength == 1 ? 2 : t.ReadWriteLength) + args.Length * 4;
                //if (args.Last().ReadWriteLength == 1) length--;//最后一个如果是 ReadWriteLength == 1  ，结果会少一个字节。

                var length = dataPackage.Length - 21;

                byte[] responseData = new byte[length];

                Array.Copy(dataPackage, dataPackage.Length - length, responseData, 0, length);

                result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));
                var cursor = 0;
                foreach (var item in args)
                {
                    object value;

                    var isSucceed = true;
                    if (responseData[cursor] == 0x0A && responseData[cursor + 1] == 0x00)
                    {
                        isSucceed = false;
                        result.Message = $"读取{item.Address}失败，请确认是否存在地址{item.Address}";

                    }
                    else if (responseData[cursor] == 0x05 && responseData[cursor + 1] == 0x00)
                    {
                        isSucceed = false;
                        result.Message = $"读取{item.Address}失败，请确认是否存在地址{item.Address}";
                    }
                    else if (responseData[cursor] != 0xFF)
                    {
                        isSucceed = false;
                        result.Message = $"读取{item.Address}失败，异常代码[{cursor}]:{responseData[cursor]}";
                    }

                    cursor += 4;

                    //如果本次读取有异常
                    if (!isSucceed)
                    {
                        result.IsSuccess = false;
                        continue;
                    }

                    var readResult = responseData.Skip(cursor).Take(item.ReadWriteLength).Reverse().ToArray();
                    cursor += item.ReadWriteLength == 1 ? 2 : item.ReadWriteLength;
                    switch (item.DataType)
                    {
                        case DataTypeEnum.Bool:
                            value = BitConverter.ToBoolean(readResult, 0) ? 1 : 0;
                            break;
                        case DataTypeEnum.Byte:
                            value = readResult[0];
                            break;
                        case DataTypeEnum.Int16:
                            value = BitConverter.ToInt16(readResult, 0);
                            break;
                        case DataTypeEnum.UInt16:
                            value = BitConverter.ToUInt16(readResult, 0);
                            break;
                        case DataTypeEnum.Int32:
                            value = BitConverter.ToInt32(readResult, 0);
                            break;
                        case DataTypeEnum.UInt32:
                            value = BitConverter.ToUInt32(readResult, 0);
                            break;
                        case DataTypeEnum.Int64:
                            value = BitConverter.ToInt64(readResult, 0);
                            break;
                        case DataTypeEnum.UInt64:
                            value = BitConverter.ToUInt64(readResult, 0);
                            break;
                        case DataTypeEnum.Float:
                            value = BitConverter.ToSingle(readResult, 0);
                            break;
                        case DataTypeEnum.Double:
                            value = BitConverter.ToDouble(readResult, 0);
                            break;
                        default:
                            throw new Exception($"未定义数据类型：{item.DataType}");
                    }
                    result.Value.Add(item.Address, value);
                }
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
                if (Connected) Dispose();
            }
            return result.Complete();
        }

        /// <summary>
        /// 批量写入
        /// TODO 可以重构后面的Write 都走BatchWrite
        /// </summary>
        /// <param name="addresses"></param>
        /// <returns></returns>
        private OperationResult BatchWriteBase(Dictionary<string, object> addresses)
        {
            if (!Connected)
            {
                var connectResult = Connect();
                if (!connectResult.IsSuccess)
                {
                    return connectResult;
                }
            }
            OperationResult result = new OperationResult();
            try
            {
                var newAddresses = new Dictionary<string, KeyValuePair<byte[], bool>>();
                foreach (var item in addresses)
                {
                    var tempData = new List<byte>();
                    switch (item.Value.GetType().Name)
                    {
                        case "Boolean":
                            tempData = (bool)item.Value ? new List<byte>() { 0x01 } : new List<byte>() { 0x00 };
                            break;
                        case "Byte":
                            tempData = new List<byte>() { (byte)item.Value };
                            break;
                        case "UInt16":
                            tempData = BitConverter.GetBytes((ushort)item.Value).ToList();
                            break;
                        case "Int16":
                            tempData = BitConverter.GetBytes((short)item.Value).ToList();
                            break;
                        case "UInt32":
                            tempData = BitConverter.GetBytes((uint)item.Value).ToList();
                            break;
                        case "Int32":
                            tempData = BitConverter.GetBytes((int)item.Value).ToList();
                            break;
                        case "UInt64":
                            tempData = BitConverter.GetBytes((ulong)item.Value).ToList();
                            break;
                        case "Int64":
                            tempData = BitConverter.GetBytes((long)item.Value).ToList();
                            break;
                        case "Single":
                            tempData = BitConverter.GetBytes((float)item.Value).ToList();
                            break;
                        case "Double":
                            tempData = BitConverter.GetBytes((double)item.Value).ToList();
                            break;
                        default:
                            throw new Exception($"暂未提供对{item.Value.GetType().Name}类型的写入操作。");
                    }
                    tempData.Reverse();
                    newAddresses.Add(item.Key, new KeyValuePair<byte[], bool>(tempData.ToArray(), item.Value.GetType().Name == "Boolean"));
                }
                var arg = ConvertWriteArg(newAddresses);
                byte[] command = GetWriteCommand(arg);
                result.Requsts[0] = string.Join(" ", command.Select(t => t.ToString("X2")));
                var sendResult = InterpretAndExtractMessageData(command);
                if (!sendResult.IsSuccess)
                    return sendResult;

                var dataPackage = sendResult.Value;
                result.Responses[0] = string.Join(" ", dataPackage.Select(t => t.ToString("X2")));

                if (dataPackage.Length == arg.Length + 21)
                {
                    for (int i = 0; i < arg.Length; i++)
                    {
                        var offset = 21 + i;
                        if (dataPackage[offset] == 0x0A)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入{arg[i].Address}失败，请确认是否存在地址{arg[i].Address}，异常代码[{offset}]:{dataPackage[offset]}";
                        }
                        else if (dataPackage[offset] == 0x05)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入{arg[i].Address}失败，请确认是否存在地址{arg[i].Address}，异常代码[{offset}]:{dataPackage[offset]}";
                        }
                        else if (dataPackage[offset] != 0xFF)
                        {
                            result.IsSuccess = false;
                            result.Message = $"写入{string.Join(",", arg.Select(t => t.Address))}失败，异常代码[{offset}]:{dataPackage[offset]}";
                        }
                    }
                }
                else
                {
                    result.IsSuccess = false;
                    result.Message = $"写入数据数量和响应结果数量不一致，写入数据：{arg.Length} 响应数量：{dataPackage.Length - 21}";
                }
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
                if (Connected) Dispose();
            }
            return result.Complete();
        }

        /// <summary>
        /// 分批写入，默认按10个地址打包读取
        /// </summary>
        /// <param name="addresses">地址集合</param>
        /// <param name="batchNumber">批量读取数量</param>
        /// <returns></returns>
        public override OperationResult BatchWrite(Dictionary<string, object> addresses)
        {
            int batchNumber = 10;
            var result = new OperationResult();
            var batchCount = Math.Ceiling((float)addresses.Count / batchNumber);
            for (int i = 0; i < batchCount; i++)
            {
                var tempAddresses = addresses.Skip(i * batchNumber).Take(batchNumber).ToDictionary(t => t.Key, t => t.Value);
                var tempResult = BatchWriteBase(tempAddresses);
                if (!tempResult.IsSuccess)
                {
                    result.IsSuccess = tempResult.IsSuccess;
                    result.Message = tempResult.Message;
                    
                }
                result.Requsts = tempResult.Requsts;
                result.Responses = tempResult.Responses;
            }
            return result.Complete();
        }

        #region ConvertArg 根据地址信息转换成通讯需要的信息
        /// <summary>
        /// 获取区域类型代码
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        private SiemensAddress ConvertArg(string address)
        {
            try
            {
                //转换成大写
                address = address.ToUpper();
                var addressInfo = new SiemensAddress()
                {
                    Address = address,
                    DbBlock = 0,
                };
                switch (address[0])
                {
                    case 'I':
                        addressInfo.TypeCode = 0x81;
                        break;
                    case 'Q':
                        addressInfo.TypeCode = 0x82;
                        break;
                    case 'M':
                        addressInfo.TypeCode = 0x83;
                        break;
                    case 'D':
                        addressInfo.TypeCode = 0x84;
                        string[] adds = address.Split('.');
                        if (address[1] == 'B')
                            addressInfo.DbBlock = Convert.ToUInt16(adds[0].Substring(2));
                        else
                            addressInfo.DbBlock = Convert.ToUInt16(adds[0].Substring(1));
                        //TODO 
                        //addressInfo.BeginAddress = GetBeingAddress(address.Substring(address.IndexOf('.') + 1));
                        break;
                    case 'T':
                        addressInfo.TypeCode = 0x1D;
                        break;
                    case 'C':
                        addressInfo.TypeCode = 0x1C;
                        break;
                    case 'V':
                        addressInfo.TypeCode = 0x84;
                        addressInfo.DbBlock = 1;
                        break;
                }

                //if (address[0] != 'D' && address[1] != 'B')
                //    addressInfo.BeginAddress = GetBeingAddress(address.Substring(1));

                //DB块
                if (address[0] == 'D' && address[1] == 'B')
                {
                    //DB1.0.0、DB1.4（非PLC地址）
                    var indexOfpoint = address.IndexOf('.') + 1;
                    if (address[indexOfpoint] >= '0' && address[indexOfpoint] <= '9')
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(indexOfpoint));
                    //DB1.DBX0.0、DB1.DBD4（标准PLC地址）
                    else
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(address.IndexOf('.') + 4));
                }
                //非DB块
                else
                {
                    //I0.0、V1004的情况（非PLC地址）
                    if (address[1] >= '0' && address[1] <= '9')
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(1));
                    //VB1004的情况（标准PLC地址）
                    else
                        addressInfo.BeginAddress = GetBeingAddress(address.Substring(2));
                }
                return addressInfo;
            }
            catch (Exception ex)
            {
                throw new Exception($"地址[{address}]解析异常，ConvertArg Message:{ex.Message}");
            }
        }

        private SiemensAddress[] ConvertArg(Dictionary<string, DataTypeEnum> addresses)
        {
            return addresses.Select(t =>
            {
                var item = ConvertArg(t.Key);
                item.DataType = t.Value;
                switch (t.Value)
                {
                    case DataTypeEnum.Bool:
                        item.ReadWriteLength = 1;
                        item.ReadWriteBit = true;
                        break;
                    case DataTypeEnum.Byte:
                        item.ReadWriteLength = 1;
                        break;
                    case DataTypeEnum.Int16:
                        item.ReadWriteLength = 2;
                        break;
                    case DataTypeEnum.UInt16:
                        item.ReadWriteLength = 2;
                        break;
                    case DataTypeEnum.Int32:
                        item.ReadWriteLength = 4;
                        break;
                    case DataTypeEnum.UInt32:
                        item.ReadWriteLength = 4;
                        break;
                    case DataTypeEnum.Int64:
                        item.ReadWriteLength = 8;
                        break;
                    case DataTypeEnum.UInt64:
                        item.ReadWriteLength = 8;
                        break;
                    case DataTypeEnum.Float:
                        item.ReadWriteLength = 4;
                        break;
                    case DataTypeEnum.Double:
                        item.ReadWriteLength = 8;
                        break;
                    default:
                        throw new Exception($"未定义数据类型：{t.Value}");
                }
                return item;
            }).ToArray();
        }

        /// <summary>
        /// 转换成写入需要的通讯信息
        /// </summary>
        /// <param name="address"></param>
        /// <param name="writeData"></param>
        /// <returns></returns>
        private SiemensWriteAddress ConvertWriteArg(string address, byte[] writeData, bool bit)
        {
            SiemensWriteAddress arg = new SiemensWriteAddress(ConvertArg(address));
            arg.WriteData = writeData;
            arg.ReadWriteBit = bit;
            return arg;
        }

        private SiemensWriteAddress[] ConvertWriteArg(Dictionary<string, KeyValuePair<byte[], bool>> addresses)
        {
            return addresses.Select(t =>
            {
                var item = new SiemensWriteAddress(ConvertArg(t.Key));
                item.WriteData = t.Value.Key;
                item.ReadWriteBit = t.Value.Value;
                return item;
            }).ToArray();
        }
        #endregion

        #region 获取指令
        /// <summary>
        /// 获取读指令
        /// </summary>      
        /// <returns></returns>
        protected byte[] GetReadCommand(SiemensAddress[] datas)
        {
            //byte type, int beginAddress, ushort dbAddress, ushort length, bool isBit
            byte[] command = new byte[19 + datas.Length * 12];
            command[0] = 0x03;
            command[1] = 0x00;//[0][1]固定报文头
            command[2] = (byte)(command.Length / 256);
            command[3] = (byte)(command.Length % 256);//[2][3]整个读取请求长度为0x1F= 31 
            command[4] = 0x02; // 固定 -> Fixed
            command[5] = 0xF0; // 固定 -> Fixed
            command[6] = 0x80;//COTP
            command[7] = 0x32;//协议ID
            command[8] = 0x01;//1  客户端发送命令 3 服务器回复命令
            command[9] = 0x00;
            command[10] = 0x00;//[4]-[10]固定6个字节
            command[11] = 0x00;
            command[12] = 0x01;//[11][12]两个字节，标识序列号，回复报文相同位置和这个完全一样；范围是0~65535
            command[13] = (byte)((command.Length - 17) / 256);
            command[14] = (byte)((command.Length - 17) % 256); //parameter length（减17是因为从[17]到最后属于parameter）
            command[15] = 0x00;
            command[16] = 0x00;//data length
            command[17] = 0x04;//04读 05写
            command[18] = (byte)datas.Length;//读取数据块个数
            for (int i = 0; i < datas.Length; i++)
            {
                var data = datas[i];
                if (data.ReadWriteBit & data.ReadWriteLength >= 2)
                {
                    data.ReadWriteBit = false;
                }
                command[19 + i * 12] = 0x12;//variable specification
                command[20 + i * 12] = 0x0A;//Length of following address specification
                command[21 + i * 12] = 0x10;//Syntax Id: S7ANY 
                command[22 + i * 12] = data.ReadWriteBit ? (byte)0x01 : (byte)0x02;//Toport size: BYTE 
                command[23 + i * 12] = (byte)(data.ReadWriteLength / 256);
                command[24 + i * 12] = (byte)(data.ReadWriteLength % 256);//[23][24]两个字节,访问数据的个数，以byte为单位；
                command[25 + i * 12] = (byte)(data.DbBlock / 256);
                command[26 + i * 12] = (byte)(data.DbBlock % 256);//[25][26]DB块的编号
                command[27 + i * 12] = data.TypeCode;//访问数据块的类型
                command[28 + i * 12] = (byte)(data.BeginAddress / 256 / 256 % 256);
                command[29 + i * 12] = (byte)(data.BeginAddress / 256 % 256);
                command[30 + i * 12] = (byte)(data.BeginAddress % 256);//[28][29][30]访问DB块的偏移量
            }
            return command;
        }

        /// <summary>
        /// 获取读指令
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected byte[] GetReadCommand(SiemensAddress data)
        {
            return GetReadCommand(new SiemensAddress[] { data });
        }

        /// <summary>
        /// 获取写指令
        /// </summary>
        /// <param name="writes"></param>
        /// <returns></returns>
        protected byte[] GetWriteCommand(SiemensWriteAddress[] writes)
        {
            //（如果不是最后一个 WriteData.Length == 1 ，则需要填充一个空数据）
            var writeDataLength = writes.Sum(t => t.WriteData.Length == 1 ? 2 : t.WriteData.Length);
            if (writes[writes.Length - 1].WriteData.Length == 1) writeDataLength--;

            //前19个固定的、16为Item长度、writes.Length为Imte的个数
            byte[] command = new byte[19 + writes.Length * 16 + writeDataLength];

            command[0] = 0x03;
            command[1] = 0x00;//[0][1]固定报文头
            command[2] = (byte)((command.Length) / 256);
            command[3] = (byte)((command.Length) % 256);//[2][3]整个读取请求长度
            command[4] = 0x02; // 固定 -> Fixed
            command[5] = 0xF0; // 固定 -> Fixed
            command[6] = 0x80; // 固定 -> Fixed
            command[7] = 0x32;//protocol Id
            command[8] = 0x01;//1  客户端发送命令 3 服务器回复命令 Job
            command[9] = 0x00;
            command[10] = 0x00;//[9][10] redundancy identification (冗余的识别)
            command[11] = 0x00;
            command[12] = 0x01;//[11]-[12]protocol data unit reference
            command[13] = (byte)((12 * writes.Length + 2) / 256);
            command[14] = (byte)((12 * writes.Length + 2) % 256);//Parameter length
            command[15] = (byte)((writeDataLength + 4 * writes.Length) / 256);
            command[16] = (byte)((writeDataLength + 4 * writes.Length) % 256);//[15][16] Data length

            //Parameter
            command[17] = 0x05;//04读 05写 Function Write
            command[18] = (byte)writes.Length;//写入数据块个数 Item count
            //Item[]
            for (int i = 0; i < writes.Length; i++)
            {
                var write = writes[i];
                if (write.ReadWriteBit & (write.WriteData.Length > 1 | write.WriteData[0] >= 2))
                {
                    write.ReadWriteBit = false;
                }


                var typeCode = write.TypeCode;
                var beginAddress = write.BeginAddress;
                var dbBlock = write.DbBlock;
                var writeData = write.WriteData;

                command[19 + i * 12] = 0x12;
                command[20 + i * 12] = 0x0A;
                command[21 + i * 12] = 0x10;//[19]-[21]固定
                command[22 + i * 12] = write.ReadWriteBit ? (byte)0x01 : (byte)0x02;//写入方式，1是按位，2是按字
                command[23 + i * 12] = (byte)(writeData.Length / 256);
                command[24 + i * 12] = (byte)(writeData.Length % 256);//写入数据个数
                command[25 + i * 12] = (byte)(dbBlock / 256);
                command[26 + i * 12] = (byte)(dbBlock % 256);//DB块的编号
                command[27 + i * 12] = typeCode;
                command[28 + i * 12] = (byte)(beginAddress / 256 / 256 % 256); ;
                command[29 + i * 12] = (byte)(beginAddress / 256 % 256);
                command[30 + i * 12] = (byte)(beginAddress % 256);//[28][29][30]访问DB块的偏移量      

            }
            var index = 18 + writes.Length * 12;
            //Data
            for (int i = 0; i < writes.Length; i++)
            {
                var write = writes[i];
                if(write.ReadWriteBit & (write.WriteData.Length >1 | write.WriteData[0]>=2))
                {
                    write.ReadWriteBit = false;
                }
                var writeData = write.WriteData;
                var coefficient = write.ReadWriteBit ? 1 : 8;

                command[1 + index] = 0x00;
                command[2 + index] = write.ReadWriteBit ? (byte)0x03 : (byte)0x04;// 03bit（位）04 byte(字节)
                command[3 + index] = (byte)(writeData.Length * coefficient / 256);
                command[4 + index] = (byte)(writeData.Length * coefficient % 256);//按位计算出的长度

                if (write.WriteData.Length == 1)
                {
                    if (write.ReadWriteBit)
                        command[5 + index] = writeData[0] == 0x01 ? (byte)0x01 : (byte)0x00; //True or False 
                    else command[5 + index] = writeData[0];

                    if (i >= writes.Length - 1)
                        index += (4 + 1);
                    else index += (4 + 2); // fill byte  （如果不是最后一个bit，则需要填充一个空数据）
                }
                else
                {
                    writeData.CopyTo(command, 5 + index);
                    index += (4 + writeData.Length);
                }
            }
            return command;
        }

        /// <summary>
        /// 获取写指令
        /// </summary>
        /// <param name="write"></param>
        /// <returns></returns>
        protected byte[] GetWriteCommand(SiemensWriteAddress write)
        {
            return GetWriteCommand(new SiemensWriteAddress[] { write });
        }

        #endregion

        #region protected

        /// <summary>
        /// 获取需要读取的长度
        /// </summary>
        /// <param name="head"></param>
        /// <returns></returns>
        protected int GetContentLength(byte[] head)
        {
            if (head?.Length >= 4)
                return head[2] * 256 + head[3] - 4;
            else
                throw new ArgumentException("请传入正确的参数");
        }

        /// <summary>
        /// 获取读取PLC地址的开始位置
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        protected int GetBeingAddress(string address)
        {
            //去掉V1025 前面的V
            //address = address.Substring(1);
            //I1.3地址的情况
            if (address.IndexOf('.') < 0)
                return int.Parse(address) * 8;
            else
            {
                string[] temp = address.Split('.');
                return Convert.ToInt32(temp[0]) * 8 + Convert.ToInt32(temp[1]);
            }
        }






        #endregion
    }
}

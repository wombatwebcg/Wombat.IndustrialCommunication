using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.Infrastructure;


namespace Wombat.IndustrialCommunication.PLC
{
    /// <summary>
    /// 欧姆龙PLC 客户端
    /// https://flat2010.github.io/2020/02/23/Omron-Fins%E5%8D%8F%E8%AE%AE/
    /// </summary>
    public class OmronFinsClient : PLCEthernetBase
    {

        /// <summary>
        /// 基础命令
        /// </summary>
        private byte[] BasicCommand = new byte[]
        {
            0x46, 0x49, 0x4E, 0x53,//Magic字段  0x46494E53 对应的ASCII码，即FINS
            0x00, 0x00, 0x00, 0x0C,//Length字段 表示其后所有字段的总长度
            0x00, 0x00, 0x00, 0x00,//Command字段 
            0x00, 0x00, 0x00, 0x00,//Error Code字段
            0x00, 0x00, 0x00, 0x0B //Client/Server Node Address字段
        };

        /// <summary>
        /// 版本
        /// </summary>
        public override string Version => "OmronFins";

        protected Socket _socket;

        /// <summary>
        /// 是否是连接的
        /// </summary>
        public override bool Connected => _socket.Connected;

        /// <summary>
        /// DA2(即Destination unit address，目标单元地址)
        /// 0x00：PC(CPU)
        /// 0xFE： SYSMAC NET Link Unit or SYSMAC LINK Unit connected to network；
        /// 0x10~0x1F：CPU总线单元 ，其值等于10 + 单元号(前端面板中配置的单元号)
        /// </summary>
        public byte UnitAddress { get; set; } = 0x00;

        /// <summary>
        /// SA1 客户端节点编号
        /// </summary>
        public byte SA1 { get; set; } = 0x0B;

        /// <summary>
        /// DA1 服务器节点编号
        /// </summary>
        private byte DA1 { get; set; } = 0x01;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeout"></param>
        /// <param name="endianFormat"></param>
        public OmronFinsClient(string ip, int port = 9600, int timeout = 1500, EndianFormat endianFormat = EndianFormat.CDAB)
        {
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            IpEndPoint = new IPEndPoint(address, port);
        }

        /// <summary>
        /// 打开连接（如果已经是连接状态会先关闭再打开）
        /// </summary>
        /// <returns></returns>
        internal override OperationResult DoConnect()
        {
            var result = new OperationResult();
            _socket?.SafeClose();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                //超时时间设置
                _socket.ReceiveTimeout = (int)Timeout.TotalMilliseconds;
                _socket.SendTimeout = (int)Timeout.TotalMilliseconds;

                //socket.Connect(IpEndPoint);
                IAsyncResult connectOperationResult = _socket.BeginConnect(IpEndPoint, null, null);
                //阻塞当前线程           
                if (!connectOperationResult.AsyncWaitHandle.WaitOne(Timeout))
                    throw new TimeoutException("连接超时");
                _socket.EndConnect(connectOperationResult);

                BasicCommand[19] = SA1;
                result.Requsts.Add(string.Join(" ", BasicCommand.Select(t => t.ToString("X2"))));
                _socket.Send(BasicCommand);

                var socketReadResult = ReadBuffer(8);
                if (!socketReadResult.IsSuccess)
                    return socketReadResult;
                var head = socketReadResult.Value;

                byte[] buffer = new byte[4];
                buffer[0] = head[7];
                buffer[1] = head[6];
                buffer[2] = head[5];
                buffer[3] = head[4];
                var length = BitConverter.ToInt32(buffer, 0);

                socketReadResult = ReadBuffer(length);
                if (!socketReadResult.IsSuccess)
                    return socketReadResult;
                var content = socketReadResult.Value;

                var headContent = head.Concat(content).ToArray();
                result.Responses.Add(string.Join(" ", headContent.Select(t => t.ToString("X2"))));
                // 服务器节点编号
                if (headContent.Length >= 24) DA1 = headContent[23];
                else DA1 = Convert.ToByte(IpEndPoint.Address.ToString().Substring(IpEndPoint.Address.ToString().LastIndexOf(".") + 1)); ;
            }
            catch (Exception ex)
            {
                _socket?.SafeClose();
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.ErrorCode = 408;
                result.Exception = ex;
            }
            return result.Complete(); ;
        }

        internal override OperationResult DoDisconnect()
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
        /// 发送报文，并获取响应报文（建议使用SendPackageReliable，如果异常会自动重试一次）
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        internal override OperationResult<byte[]> GetMessageContent(byte[] command)
        {
            //从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
            lock (this)
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();
                try
                {
                    _socket.Send(command);
                    var socketReadResult = ReadBuffer( 8);
                    if (!socketReadResult.IsSuccess)
                        return socketReadResult;
                    var head = socketReadResult.Value;

                    byte[] buffer = new byte[4];
                    buffer[0] = head[7];
                    buffer[1] = head[6];
                    buffer[2] = head[5];
                    buffer[3] = head[4];
                    //4-7是Length字段 表示其后所有字段的总长度
                    var contentLength = BitConverter.ToInt32(buffer, 0);
                    socketReadResult = ReadBuffer(contentLength);
                    if (!socketReadResult.IsSuccess)
                        return socketReadResult;
                    var dataPackage = socketReadResult.Value;

                    result.Value = head.Concat(dataPackage).ToArray();
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
        /// 读取数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>
        /// <param name="setEndian">返回值是否设置大小端</param>
        /// <returns></returns>
        internal override OperationResult<byte[]> Read(string address, int length, bool isBit = false)
        {
            if (!_socket?.Connected ?? true)
            {
                var connectOperationResult = Connect();
                if (!connectOperationResult.IsSuccess)
                {
                    return new OperationResult<byte[]>(connectOperationResult);
                }
            }
            var result = new OperationResult<byte[]>();
            try
            {
                //发送读取信息
                var arg = ConvertArg(address, isBit: isBit);
                byte[] command = GetReadCommand(arg, (ushort)length);
                result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));
                //发送命令 并获取响应报文
                var sendOperationResult = InterpretAndExtractMessageData(command);
                if (!sendOperationResult.IsSuccess)
                    return sendOperationResult;
                var dataPackage = sendOperationResult.Value;

                byte[] responseData = new byte[length];
                Array.Copy(dataPackage, dataPackage.Length - length, responseData, 0, length);
                result.Responses.Add(string.Join(" ", dataPackage.Select(t => t.ToString("X2"))));
                result.Value = responseData.ToArray();
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
                _socket?.SafeClose();
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.Exception = ex;
                _socket?.SafeClose();
            }
            finally
            {
                if (Connected) Dispose();
            }
            return result.Complete();
        }

        /// <summary>
        /// 写入数据
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="data">值</param>
        /// <param name="isBit">值</param>
        /// <returns></returns>
        internal override OperationResult Write(string address, byte[] data, bool isBit = false)
        {
            if (!_socket?.Connected ?? true)
            {
                var connectOperationResult = Connect();
                if (!connectOperationResult.IsSuccess)
                {
                    return connectOperationResult;
                }
            }
            OperationResult result = new OperationResult();
            try
            {
                //发送写入信息
                var arg = ConvertArg(address, isBit: isBit);
                byte[] command = GetWriteCommand(arg, data);
                result.Requsts.Add(string.Join(" ", command.Select(t => t.ToString("X2"))));
                var sendOperationResult = InterpretAndExtractMessageData(command);
                if (!sendOperationResult.IsSuccess)
                    return sendOperationResult;

                var dataPackage = sendOperationResult.Value;
                result.Responses.Add(string.Join(" ", dataPackage.Select(t => t.ToString("X2"))));
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
                _socket?.SafeClose();
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
                result.Exception = ex;
                _socket?.SafeClose();
            }
            finally
            {
                if (Connected) Dispose();
            }
            return result.Complete();
        }

        /// <summary>
        /// 地址信息解析
        /// </summary>
        /// <param name="address"></param>        
        /// <param name="dataType"></param> 
        /// <param name="isBit"></param> 
        /// <returns></returns>
        private OmronFinsAddress ConvertArg(string address, DataTypeEnum dataType = DataTypeEnum.None, bool isBit = false)
        {
            address = address.ToUpper();
            var addressInfo = new OmronFinsAddress()
            {
                DataTypeEnum = dataType,
                IsBit = isBit
            };
            switch (address[0])
            {
                case 'D'://DM区
                    {
                        addressInfo.BitCode = 0x02;
                        addressInfo.WordCode = 0x82;
                        addressInfo.TypeChar = address.Substring(0, 1);
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1).Split('.')[0]);
                        break;
                    }
                case 'C'://CIO区
                    {
                        addressInfo.BitCode = 0x30;
                        addressInfo.WordCode = 0xB0;
                        addressInfo.TypeChar = address.Substring(0, 1);
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1).Split('.')[0]);
                        break;
                    }
                case 'W'://WR区
                    {
                        addressInfo.BitCode = 0x31;
                        addressInfo.WordCode = 0xB1;
                        addressInfo.TypeChar = address.Substring(0, 1);
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1).Split('.')[0]);
                        break;
                    }
                case 'H'://HR区
                    {
                        addressInfo.BitCode = 0x32;
                        addressInfo.WordCode = 0xB2;
                        addressInfo.TypeChar = address.Substring(0, 1);
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1).Split('.')[0]);
                        break;
                    }
                case 'A'://AR区
                    {
                        addressInfo.BitCode = 0x33;
                        addressInfo.WordCode = 0xB3;
                        addressInfo.TypeChar = address.Substring(0, 1);
                        addressInfo.BeginAddress = Convert.ToInt32(address.Substring(1).Split('.')[0]);
                        break;
                    }
                case 'E':
                    {
                        string[] address_split = address.Split('.');
                        int block_length = Convert.ToInt32(address_split[0].Substring(1), 16);
                        if (block_length < 16)
                        {
                            addressInfo.BitCode = (byte)(0x20 + block_length);
                            addressInfo.WordCode = (byte)(0xA0 + block_length);
                        }
                        else
                        {
                            addressInfo.BitCode = (byte)(0xE0 + block_length - 16);
                            addressInfo.WordCode = (byte)(0x60 + block_length - 16);
                        }

                        if (isBit)
                        {
                            // 位操作
                            ushort address_location = ushort.Parse(address_split[1]);
                            addressInfo.BitAddress = new byte[3];
                            addressInfo.BitAddress[0] = BitConverter.GetBytes(address_location)[1];
                            addressInfo.BitAddress[1] = BitConverter.GetBytes(address_location)[0];

                            if (address_split.Length > 2)
                            {
                                addressInfo.BitAddress[2] = byte.Parse(address_split[2]);
                                if (addressInfo.BitAddress[2] > 15)
                                    //输入的位地址只能在0-15之间
                                    throw new Exception("位地址数据异常");
                            }
                        }
                        else
                        {
                            // 字操作
                            ushort address_location = ushort.Parse(address_split[1]);
                            addressInfo.BitAddress = new byte[3];
                            addressInfo.BitAddress[0] = BitConverter.GetBytes(address_location)[1];
                            addressInfo.BitAddress[1] = BitConverter.GetBytes(address_location)[0];
                        }
                        break;
                    }
                default:
                    //类型不支持
                    throw new Exception("Address解析异常");
            }

            if (address[0] != 'E')
            {
                if (isBit)
                {
                    // 位操作
                    string[] address_split = address.Substring(1).Split('.');
                    ushort address_location = ushort.Parse(address_split[0]);
                    addressInfo.BitAddress = new byte[3];
                    addressInfo.BitAddress[0] = BitConverter.GetBytes(address_location)[1];
                    addressInfo.BitAddress[1] = BitConverter.GetBytes(address_location)[0];

                    if (address_split.Length > 1)
                    {
                        addressInfo.BitAddress[2] = byte.Parse(address_split[1]);
                        if (addressInfo.BitAddress[2] > 15)
                            //输入的位地址只能在0-15之间
                            throw new Exception("位地址数据异常");
                    }
                }
                else
                {
                    // 字操作
                    ushort address_location = ushort.Parse(address.Substring(1));
                    addressInfo.BitAddress = new byte[3];
                    addressInfo.BitAddress[0] = BitConverter.GetBytes(address_location)[1];
                    addressInfo.BitAddress[1] = BitConverter.GetBytes(address_location)[0];
                }
            }

            return addressInfo;
        }

        /// <summary>
        /// 获取Read命令
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        protected byte[] GetReadCommand(OmronFinsAddress arg, ushort length)
        {
            bool isBit = arg.IsBit;

            if (!isBit) length = (ushort)(length / 2);

            byte[] command = new byte[26 + 8];

            Array.Copy(BasicCommand, 0, command, 0, 4);
            byte[] tmp = BitConverter.GetBytes(command.Length - 8);
            Array.Reverse(tmp);
            tmp.CopyTo(command, 4);
            command[11] = 0x02;

            command[16] = 0x80; //ICF 信息控制字段
            command[17] = 0x00; //RSV 保留字段
            command[18] = 0x02; //GCT 网关计数
            command[19] = 0x00; //DNA 目标网络地址 00:表示本地网络  0x01~0x7F:表示远程网络
            command[20] = DA1; //DA1 目标节点编号 0x01~0x3E:SYSMAC LINK网络中的节点号 0x01~0x7E:YSMAC NET网络中的节点号 0xFF:广播传输
            command[21] = UnitAddress; //DA2 目标单元地址
            command[22] = 0x00; //SNA 源网络地址 取值及含义同DNA字段
            command[23] = SA1; //SA1 源节点编号 取值及含义同DA1字段
            command[24] = 0x00; //SA2 源单元地址 取值及含义同DA2字段
            command[25] = 0x00; //SID Service ID 取值0x00~0xFF，产生会话的进程的唯一标识

            command[26] = 0x01;
            command[27] = 0x01; //Command Code 内存区域读取
            command[28] = isBit ? arg.BitCode : arg.WordCode;
            arg.BitAddress.CopyTo(command, 29);
            command[32] = (byte)(length / 256);
            command[33] = (byte)(length % 256);

            return command;
        }

        /// <summary>
        /// 获取Write命令
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected byte[] GetWriteCommand(OmronFinsAddress arg, byte[] value)
        {
            bool isBit = arg.IsBit;
            byte[] command = new byte[26 + 8 + value.Length];

            Array.Copy(BasicCommand, 0, command, 0, 4);
            byte[] tmp = BitConverter.GetBytes(command.Length - 8);
            Array.Reverse(tmp);
            tmp.CopyTo(command, 4);
            command[11] = 0x02;

            command[16] = 0x80; //ICF 信息控制字段
            command[17] = 0x00; //RSV 保留字段
            command[18] = 0x02; //GCT 网关计数
            command[19] = 0x00; //DNA 目标网络地址 00:表示本地网络  0x01~0x7F:表示远程网络
            command[20] = DA1; //DA1 目标节点编号 0x01~0x3E:SYSMAC LINK网络中的节点号 0x01~0x7E:YSMAC NET网络中的节点号 0xFF:广播传输
            command[21] = UnitAddress; //DA2 目标单元地址
            command[22] = 0x00; //SNA 源网络地址 取值及含义同DNA字段
            command[23] = SA1; //SA1 源节点编号 取值及含义同DA1字段
            command[24] = 0x00; //SA2 源单元地址 取值及含义同DA2字段
            command[25] = 0x00; //SID Service ID 取值0x00~0xFF，产生会话的进程的唯一标识

            command[26] = 0x01;
            command[27] = 0x02; //Command Code 内存区域写入
            command[28] = isBit ? arg.BitCode : arg.WordCode;
            arg.BitAddress.CopyTo(command, 29);
            command[32] = isBit ? (byte)(value.Length / 256) : (byte)(value.Length / 2 / 256);
            command[33] = isBit ? (byte)(value.Length % 256) : (byte)(value.Length / 2 % 256);
            value.CopyTo(command, 34);

            return command;
        }

        /// <summary>
        /// 批量读取
        /// </summary>
        /// <param name="addresses"></param>
        /// <param name="batchNumber">此参数设置无实际效果</param>
        /// <returns></returns>
        public override OperationResult<Dictionary<string, object>> BatchRead(Dictionary<string, DataTypeEnum> addresses)
        {
            var result = new OperationResult<Dictionary<string, object>>();
            result.Value = new Dictionary<string, object>();

            var omronFinsAddresses = addresses.Select(t => ConvertArg(t.Key, t.Value)).ToList();
            var typeChars = omronFinsAddresses.Select(t => t.TypeChar).Distinct();
            foreach (var typeChar in typeChars)
            {
                var tempAddresses = omronFinsAddresses.Where(t => t.TypeChar == typeChar).ToList();
                var minAddress = tempAddresses.Select(t => t.BeginAddress).Min();
                var maxAddress = tempAddresses.Select(t => t.BeginAddress).Max();

                while (maxAddress >= minAddress)
                {
                    int readLength = 121;//TODO 分批读取的长度还可以继续调大

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
                            throw new Exception("暂时不支持Bool类型批量读取");
                        case DataTypeEnum.Byte:
                            throw new Exception("暂时不支持Byte类型批量读取");
                        //readLength = tempMax.BeginAddress + 1 - minAddress;
                        //break;
                        case DataTypeEnum.Int16:
                        case DataTypeEnum.UInt16:
                            readLength = tempMax.BeginAddress * 2 + 2 - minAddress * 2;
                            break;
                        case DataTypeEnum.Int32:
                        case DataTypeEnum.UInt32:
                        case DataTypeEnum.Float:
                            readLength = tempMax.BeginAddress * 2 + 4 - minAddress * 2;
                            break;
                        case DataTypeEnum.Int64:
                        case DataTypeEnum.UInt64:
                        case DataTypeEnum.Double:
                            readLength = tempMax.BeginAddress * 2 + 8 - minAddress * 2;
                            break;
                        default:
                            throw new Exception("Message BatchRead 未定义类型 -1");
                    }

                    var tempOperationResult = Read(typeChar + minAddress.ToString(), Convert.ToUInt16(readLength), false);

                    if (!tempOperationResult.IsSuccess)
                    {
                        result.IsSuccess = tempOperationResult.IsSuccess;
                        result.Exception = tempOperationResult.Exception;
                        result.Message = tempOperationResult.Message;
                        result.ErrorCode = tempOperationResult.ErrorCode;
                        return result.Complete();
                    }

                    var rValue = tempOperationResult.Value.ToArray();
                    foreach (var item in tempAddress)
                    {
                        object tempVaue = null;

                        switch (item.DataTypeEnum)
                        {
                            case DataTypeEnum.Bool:
                                tempVaue = ReadBoolean(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            case DataTypeEnum.Byte:
                                throw new Exception("Message BatchRead 未定义类型 -2");
                            case DataTypeEnum.Int16:
                                tempVaue = ReadInt16(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            case DataTypeEnum.UInt16:
                                tempVaue = ReadUInt16(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            case DataTypeEnum.Int32:
                                tempVaue = ReadInt32(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            case DataTypeEnum.UInt32:
                                tempVaue = ReadUInt32(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            case DataTypeEnum.Int64:
                                tempVaue = ReadInt64(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            case DataTypeEnum.UInt64:
                                tempVaue = ReadUInt64(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            case DataTypeEnum.Float:
                                tempVaue = ReadFloat(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            case DataTypeEnum.Double:
                                tempVaue = ReadDouble(minAddress, item.BeginAddress, rValue).Value;
                                break;
                            default:
                                throw new Exception("Message BatchRead 未定义类型 -3");
                        }

                        result.Value.Add(item.TypeChar + item.BeginAddress.ToString(), tempVaue);
                    }
                    minAddress = minAddress + readLength / 2;

                    if (tempAddresses.Any(t => t.BeginAddress >= minAddress))
                        minAddress = tempAddresses.Where(t => t.BeginAddress >= minAddress).OrderBy(t => t.BeginAddress).FirstOrDefault().BeginAddress;
                }
            }
            return result.Complete(); ;
        }



        public override OperationResult BatchWrite(Dictionary<string, object> addresses)
        {
            throw new NotImplementedException();
        }

        internal override ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            throw new NotImplementedException();
        }

        internal override Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            throw new NotImplementedException();
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

    }
}

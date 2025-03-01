using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Wombat.Network;
using Wombat.Network.Sockets;


namespace Wombat.IndustrialCommunication
{
    //public abstract class EthernetDeviceClientBase : DeviceClient, IEthernetClient
    //{
    //    public IPEndPoint IpEndPoint { get; set; }
    //    public string ClientName { get; set; }

    //    public CommunicationResult<byte[]> _communicationResult = new CommunicationResult<byte[]>();

    //     protected internal TcpSocketClient _socket;

    //    public EthernetDeviceClientBase()
    //    {




    //    }

    //    /// <summary>
    //    /// 打开连接（如果已经是连接状态会先关闭再打开）
    //    /// </summary>
    //    /// <returns></returns>
    //    internal override OperationResult DoConnect()
    //    {
    //        var result = new OperationResult();
    //        var config = new TcpSocketClientConfiguration();
    //        config.ConnectTimeout = ConnectTimeout;
    //        config.ReceiveTimeout = ReceiveTimeout;
    //        config.SendTimeout = SendTimeout;
    //        config.FrameBuilder = new RawBufferFrameBuilder();
    //        var dispatcher = new EthernetDeviceClientEventDispatcher(_communicationResult);
    //        _socket = new TcpSocketClient(IpEndPoint, dispatcher, config);
    //        _socket?.Close().GetAwaiter().GetResult(); ;
    //        try
    //        {
    //             _socket.Connect().GetAwaiter().GetResult(); ;
    //        }
    //        catch (Exception ex)
    //        {
    //            _socket?.Close().GetAwaiter().GetResult(); ;
    //            result.IsSuccess = false;
    //            result.Message = ex.Message;
    //            result.ErrorCode = 408;
    //            result.Exception = ex;
    //        }
    //        return result.Complete();
    //    }

    //    internal override async Task<OperationResult> DoConnectAsync()
    //    {
    //        var result = new OperationResult();
    //        var config = new TcpSocketClientConfiguration();
    //        config.ConnectTimeout = ConnectTimeout;
    //        config.ReceiveTimeout = ReceiveTimeout;
    //        config.SendTimeout = SendTimeout;
    //        config.FrameBuilder = new RawBufferFrameBuilder();
    //        var dispatcher = new EthernetDeviceClientEventDispatcher(_communicationResult);
    //        _socket = new TcpSocketClient(IpEndPoint, dispatcher, config);
    //        await _socket?.Close();
    //        try
    //        {
    //            await _socket.Connect();
    //        }
    //        catch (Exception ex)
    //        {
    //            await _socket?.Close();
    //            result.IsSuccess = false;
    //            result.Message = ex.Message;
    //            result.ErrorCode = 408;
    //            result.Exception = ex;
    //        }
    //        return result.Complete();
    //    }


    //    internal override OperationResult DoDisconnect()
    //    {

    //        OperationResult result = new OperationResult();
    //        try
    //        {
    //            _socket?.Close();
    //            return result.Complete();
    //        }
    //        catch (Exception ex)
    //        {
    //            result.IsSuccess = false;
    //            result.Message = ex.Message;
    //            result.Exception = ex;
    //            return result.Complete();
    //        }

    //    }

    //    internal override async Task<OperationResult> DoDisconnectAsync()
    //    {
    //        OperationResult result = new OperationResult();
    //        try
    //        {
    //            await _socket?.Close();
    //            return result.Complete();
    //        }
    //        catch (Exception ex)
    //        {
    //            result.IsSuccess = false;
    //            result.Message = ex.Message;
    //            result.Exception = ex;
    //            return result.Complete();
    //        }
    //    }



    //    /// <summary>
    //    /// Socket读取
    //    /// </summary>
    //    /// <param name="socket">socket</param>
    //    /// <param name="receiveCount">读取长度</param>          
    //    /// <returns></returns>
    //    internal virtual OperationResult<byte[]> ReadBuffer(int receiveCount)
    //    {
    //        var result = new OperationResult<byte[]>();
    //        if (receiveCount < 0)
    //        {
    //            result.IsSuccess = false;
    //            result.Message = $"读取长度[receiveCount]为{receiveCount}";

    //            return result;
    //        }

    //        byte[] receiveBytes = new byte[receiveCount];
    //        int receiveFinish = 0;
    //        while (receiveFinish < receiveCount)
    //        {
    //            // 分批读取
    //            int receiveLength = (receiveCount - receiveFinish) >= _socket.TcpSocketClientConfiguration.ReceiveBufferSize ? _socket.TcpSocketClientConfiguration.ReceiveBufferSize : (receiveCount - receiveFinish);
    //            try
    //            {
    //                var readLeng = _socket.Receive(receiveBytes, receiveFinish, receiveLength);
    //                if (readLeng == 0)
    //                {
    //                    _socket.Close();
    //                    result.IsSuccess = false;
    //                    result.Message = $"连接被断开";

    //                    return result;
    //                }
    //                receiveFinish += readLeng;
    //            }
    //            catch (SocketException ex)
    //            {
    //                _socket?.Close();
    //                if (ex.SocketErrorCode == SocketError.TimedOut)
    //                    result.Message = $"连接超时：{ex.Message}";
    //                else
    //                    result.Message = $"连接被断开，{ex.Message}";
    //                result.IsSuccess = false;

    //                result.Exception = ex;
    //                return result;
    //            }
    //        }
    //        result.Value = receiveBytes;
    //        return result.Complete();
    //    }

    //    /// <summary>
    //    /// Socket读取
    //    /// </summary>
    //    /// <param name="socket">socket</param>
    //    /// <param name="receiveCount">读取长度</param>          
    //    /// <returns></returns>
    //    internal virtual async ValueTask<OperationResult<byte[]>> ReadBufferAsync(int receiveCount)
    //    {
    //        var result = new OperationResult<byte[]>();
    //        if (receiveCount < 0)
    //        {
    //            result.IsSuccess = false;
    //            result.Message = $"读取长度[receiveCount]为{receiveCount}";

    //            return result;
    //        }

    //        byte[] receiveBytes = new byte[receiveCount];
    //        int receiveFinish = 0;
    //        while (receiveFinish < receiveCount)
    //        {
    //            // 分批读取
    //            int receiveLength = (receiveCount - receiveFinish) >= _socket.TcpSocketClientConfiguration.ReceiveBufferSize ? _socket.TcpSocketClientConfiguration.ReceiveBufferSize : (receiveCount - receiveFinish);
    //            try
    //            {
    //                var readLeng = await _socket.ReceiveAsync(receiveBytes, receiveFinish, receiveLength);
    //                if (readLeng == 0)
    //                {
    //                    _socket.Close();
    //                    result.IsSuccess = false;
    //                    result.Message = $"连接被断开";

    //                    return result;
    //                }
    //                receiveFinish += readLeng;
    //            }
    //            catch (SocketException ex)
    //            {
    //                _socket?.Close();
    //                if (ex.SocketErrorCode == SocketError.TimedOut)
    //                    result.Message = $"连接超时：{ex.Message}";
    //                else
    //                    result.Message = $"连接被断开，{ex.Message}";
    //                result.IsSuccess = false;

    //                result.Exception = ex;
    //                return result;
    //            }
    //        }
    //        result.Value = receiveBytes;
    //        return result.Complete();
    //    }


    //    /// <summary>
    //    /// 发送报文，并获取响应报文（如果网络异常，会自动进行一次重试）
    //    /// TODO 重试机制应改成用户主动设置
    //    /// </summary>
    //    /// <param name="command"></param>
    //    /// <returns></returns>
    //    internal override OperationResult<byte[]> InterpretMessageData(byte[] command)
    //    {
    //        var result = new OperationResult<byte[]>();
    //        try
    //        {
    //            result = ExchangingMessages(command);
    //            if (!result.IsSuccess)
    //            {
    //                WarningLog?.Invoke(result.Message, result.Exception);
    //                result.Message = "设备响应异常";
    //                return result.Complete();
    //            }
    //            else
    //            {
    //                return result.Complete();

    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            WarningLog?.Invoke(ex.Message, ex);
    //            result.IsSuccess = false;
    //            result.Message = ex.Message;
    //            return result.Complete();

    //        }
    //    }




    //    /// <summary>
    //    /// 发送报文，并获取响应报文（如果网络异常，会自动进行一次重试）
    //    /// TODO 重试机制应改成用户主动设置
    //    /// </summary>
    //    /// <param name="command"></param>
    //    /// <returns></returns>
    //    internal override async ValueTask<OperationResult<byte[]>> InterpretMessageDataAsync(byte[] command)
    //    {
    //        var result = new OperationResult<byte[]>();

    //        try
    //        {
    //            result = await ExchangingMessagesAsync(command);
    //            if (!result.IsSuccess)
    //            {
    //                WarningLog?.Invoke(result.Message, result.Exception);
    //                result.Message = "设备响应异常";
    //            }
    //            return result.Complete();
    //        }
    //        catch (Exception ex)
    //        {
    //            WarningLog?.Invoke(ex.Message, ex);
    //            result.IsSuccess = false;
    //            result.Message = ex.Message;
    //            return result.Complete();

    //        }
    //    }
    //}


    //public class EthernetDeviceClientEventDispatcher : ITcpSocketClientEventDispatcher
    //{
    //    CommunicationResult<byte[]> _communicationResult;
    //    public EthernetDeviceClientEventDispatcher(CommunicationResult<byte[]> communicationResult)
    //    {
    //        _communicationResult = communicationResult;
    //    }

    //    public Task OnServerConnected(TcpSocketClient client)
    //    {
    //        return Task.CompletedTask;
    //    }

    //    public Task OnServerDataReceived(TcpSocketClient client, byte[] data, int offset, int count)
    //    {
    //        _communicationResult.Set(data);
    //        _communicationResult.Reset();
    //        return Task.CompletedTask;
    //    }

    //    public Task OnServerDisconnected(TcpSocketClient client)
    //    {
    //        return Task.CompletedTask;
    //    }
    //}
}

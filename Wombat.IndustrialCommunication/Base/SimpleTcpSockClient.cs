using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Wombat.ObjectConversionExtention;
using Microsoft.Extensions.Logging;
using System.Threading;
using Wombat.Infrastructure;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication
{
    public class SimpleTcpSockClient : SocketBase
    {
        private AdvancedHybirdLock _advancedHybirdLock;

        public int MixReceiveLength { get; set; } = 128;
        public SimpleTcpSockClient(IPEndPoint ipAndPoint) : base(ipAndPoint)
        {
            _advancedHybirdLock = new AdvancedHybirdLock();
        }

        public SimpleTcpSockClient(string ip, int port) : base(ip, port)
        {
            _advancedHybirdLock = new AdvancedHybirdLock();

        }

        public override bool IsConnect => base.IsConnect;

        public override OperationResult<byte[]> SendPackageSingle(byte[] command)
        {
            ////从发送命令到读取响应为最小单元，避免多线程执行串数据（可线程安全执行）
            //_advancedHybirdLock.Enter();
            //    OperationResult<byte[]> result = new OperationResult<byte[]>();
            //try
            //{
            //    _socket.Send(command);
            //    var socketReadResult = SocketRead(_socket, MixReceiveLength);
            //   _advancedHybirdLock.Leave();
            //    return socketReadResult;
            //}
            //catch (Exception ex)
            //{
            //    result.IsSuccess = false;
            //    result.Message = ex.Message;
            //    _advancedHybirdLock.Leave();
            //    return result.Complete();
            //}

            return default;
        }

        public override Task<OperationResult<byte[]>> SendPackageReliableAsync(byte[] command)
        {
            throw new NotImplementedException();
        }

        public override Task<OperationResult<byte[]>> SendPackageSingleAsync(byte[] command)
        {
            throw new NotImplementedException();
        }
    }
}

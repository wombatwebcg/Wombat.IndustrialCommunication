using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.Network.Sockets;

namespace Wombat.IndustrialCommunication
{
    public class TcpClientAdapter : IStreamResource
    {
        private TcpSocketClientBase _tcpSocketClientBase;

        public TcpClientAdapter(string ip,int port)
        {
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
           var ipEndPoint = new IPEndPoint(address, port);
            _tcpSocketClientBase = new TcpSocketClientBase(ipEndPoint);
        }

        public string Version => nameof(TcpClientAdapter);
        public ILogger Logger { get; set; }
        public TimeSpan WaiteInterval { get; set; }
        public bool Connected => _tcpSocketClientBase?.Connected ?? false;
        public TimeSpan ConnectTimeout
        {
            get
            {

                if (_tcpSocketClientBase != null)
                {
                    return _tcpSocketClientBase.TcpSocketClientConfiguration.ConnectTimeout;
                }
                return default;
            }
            set
            {
                if (_tcpSocketClientBase != null)
                {
                    _tcpSocketClientBase.TcpSocketClientConfiguration.ConnectTimeout = value;
                }
            }
        }
        public TimeSpan ReceiveTimeout
        {
            get
            {

                if (_tcpSocketClientBase != null)
                {
                    return _tcpSocketClientBase.TcpSocketClientConfiguration.ReceiveTimeout;
                }
                return default;
            }
            set
            {
                if (_tcpSocketClientBase != null)
                {
                    _tcpSocketClientBase.TcpSocketClientConfiguration.ReceiveTimeout = value;
                }
            }
        }
        public TimeSpan SendTimeout
        {
            get
            {

                if (_tcpSocketClientBase != null)
                {
                    return _tcpSocketClientBase.TcpSocketClientConfiguration.SendTimeout;
                }
                return default;
            }
            set
            {
                if (_tcpSocketClientBase != null)
                {
                    _tcpSocketClientBase.TcpSocketClientConfiguration.SendTimeout = value;
                }
            }
        }

        public async Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            OperationResult operation = new OperationResult();
            try
            {
                await _tcpSocketClientBase.SendAsync(buffer, offset, size, cancellationToken);
                return operation.Complete();
            }
            catch (Exception ex)
            {
                operation.Exception = ex;
                return OperationResult.CreateFailedResult(operation);
            }
        }

        public async Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            OperationResult<int> operation = new OperationResult<int>();
            try
            {
                var count = await _tcpSocketClientBase.ReceiveAsync(buffer, offset, size, cancellationToken);
                operation.ResultValue = count;
                return operation.Complete();
            }
            catch (Exception ex)
            {
                operation.Exception = ex;
                return OperationResult.CreateFailedResult(operation, 0);
            }
        }



        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                DisposableUtility.Dispose(ref _tcpSocketClientBase);
        }

        public void UseLogger(ILogger logger)
        {
            throw new NotImplementedException();
        }

        public OperationResult Connect()
        {
            return ConnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public OperationResult Disconnect()
        {
            return DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> ConnectAsync()
        {
            OperationResult connect = new OperationResult();
            await _tcpSocketClientBase.Connect();
            connect.IsSuccess = _tcpSocketClientBase.Connected;
            return connect.Complete();
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            OperationResult disConnect = new OperationResult();
            await _tcpSocketClientBase.Close();
            disConnect.IsSuccess = _tcpSocketClientBase.Connected;
            return disConnect.Complete();
        }
    }
}

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.PLC
{
    public class SiemensClient : S7Communication, IClient
    {
        TcpClientAdapter _tcpClientAdapter;
        private AsyncLock _lock = new AsyncLock();
        public  IPEndPoint IPEndPoint { get; private set; }
        public SiemensClient(string ip, int port, SiemensVersion siemensVersion, byte slot = 0, byte rack = 0):base(new S7EthernetTransport(new TcpClientAdapter(ip, port)))
        {
            _tcpClientAdapter = (TcpClientAdapter)this.Transport.StreamResource;
            Slot = slot;
            Rack = rack;
            SiemensVersion = siemensVersion;
            if (!IPAddress.TryParse(ip, out IPAddress address))
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            IPEndPoint = new IPEndPoint(address, port);
        }

        public ILogger Logger { get; set; }
        public TimeSpan ConnectTimeout 
        {
            get 
            {
                if (_tcpClientAdapter != null)
                {
                   return _tcpClientAdapter.ConnectTimeout;
                }
                else
                {
                    return default;
                }
            }
            set 
            { 
              if(_tcpClientAdapter!=null)
                {
                    _tcpClientAdapter.ConnectTimeout = value;
                }
            } 
        }
        public TimeSpan ReceiveTimeout
        {
            get
            {
                if (_tcpClientAdapter != null)
                {
                    return _tcpClientAdapter.ReceiveTimeout;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (_tcpClientAdapter != null)
                {
                    _tcpClientAdapter.ReceiveTimeout = value;
                }
            }
        }
        public TimeSpan SendTimeout
        {
            get
            {
                if (_tcpClientAdapter != null)
                {
                    return _tcpClientAdapter.SendTimeout;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (_tcpClientAdapter != null)
                {
                    _tcpClientAdapter.SendTimeout = value;
                }

            }
        }
        public bool Connected
        {
            get
            {
                if (_tcpClientAdapter != null)
                {
                    return _tcpClientAdapter.Connected;
                }
                else
                {
                    return false;
                }
            }
        }
        public int Retries
        {
            get
            {
                if (Transport != null)
                {
                    return Transport.Retries;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (Transport != null)
                {
                    Transport.Retries = value;
                }

            }
        }
        public TimeSpan WaitToRetryMilliseconds
        {
            get
            {
                if (Transport != null)
                {
                    return Transport.WaitToRetryMilliseconds;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (Transport != null)
                {
                    Transport.WaitToRetryMilliseconds = value;
                }

            }
        }
        public bool IsLongConnection { get; set; } = true;
        public TimeSpan ResponseInterval { get; set; }

        public OperationResult Connect()
        {
            return ConnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> ConnectAsync()
        {
            OperationResult result = new OperationResult();
            var connect = await _tcpClientAdapter.ConnectAsync();
            if (connect.IsSuccess)
            {
                var init = await this.InitAsync();
                if (init.IsSuccess)
                {
                    return OperationResult.CreateSuccessResult(init);
                }
                else
                {
                    return OperationResult.CreateFailedResult(init);

                }
            }
            else
            {
                return OperationResult.CreateFailedResult(connect);

            }
        }

        public OperationResult Disconnect()
        {
            return DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            return await _tcpClientAdapter.DisconnectAsync();

        }


        internal override async ValueTask<OperationResult<byte[]>> ReadAsync(string address, int length, bool isBit = false)
        {
            if(IsLongConnection)
            {
                if(!Connected)
                {
                    return OperationResult.CreateFailedResult<byte[]>("客户端没有连接");
                }
                else
                {
                    return await base.ReadAsync(address,length,isBit);
                }

            }
            else
            {
                if (!Connected)
                {
                   var connect = await ConnectAsync();
                    if(connect.IsSuccess)
                    {
                        return await base.ReadAsync(address, length, isBit);

                    }
                    else
                    {
                        return OperationResult.CreateFailedResult<byte[]>("短连接失败");

                    }
                }
                else
                {
                    var result = await base.ReadAsync(address, length, isBit);
                    await DisconnectAsync();
                    return result;
                }

            }
        }

        internal override async Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            if (IsLongConnection)
            {
                if (!Connected)
                {
                    return OperationResult.CreateFailedResult<byte[]>("客户端没有连接");
                }
                else
                {
                    return await base.WriteAsync(address, data, isBit);
                }

            }
            else
            {
                if (!Connected)
                {
                    var connect = await ConnectAsync();
                    if (connect.IsSuccess)
                    {
                        return await base.WriteAsync(address, data, isBit);

                    }
                    else
                    {
                        return OperationResult.CreateFailedResult<byte[]>("短连接失败");

                    }
                }
                else
                {
                    var result = await base.WriteAsync(address, data, isBit);
                    await DisconnectAsync();
                    return result;
                }

            }

        }
    }
}

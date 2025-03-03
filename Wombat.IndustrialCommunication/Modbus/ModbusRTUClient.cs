using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.Modbus
{
    public class ModbusRTUClient : ModbusRTU, IClient
    {
        SerialPortAdapter _serialPortAdapter;
        private AsyncLock _lock = new AsyncLock();

        public ModbusRTUClient(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None
            ) :base(new DeviceMessageTransport(new SerialPortAdapter(portName, baudRate,dataBits,stopBits,parity,handshake)))
        {
            _serialPortAdapter = (SerialPortAdapter)this.Transport.StreamResource;

        }


        public ILogger Logger { get; set; }
        public TimeSpan ConnectTimeout 
        {
            get 
            {
                if (_serialPortAdapter != null)
                {
                   return _serialPortAdapter.ConnectTimeout;
                }
                else
                {
                    return default;
                }
            }
            set 
            { 
              if(_serialPortAdapter!=null)
                {
                    _serialPortAdapter.ConnectTimeout = value;
                }
            } 
        }
        public TimeSpan ReceiveTimeout
        {
            get
            {
                if (_serialPortAdapter != null)
                {
                    return _serialPortAdapter.ReceiveTimeout;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (_serialPortAdapter != null)
                {
                    _serialPortAdapter.ReceiveTimeout = value;
                }
            }
        }
        public TimeSpan SendTimeout
        {
            get
            {
                if (_serialPortAdapter != null)
                {
                    return _serialPortAdapter.SendTimeout;
                }
                else
                {
                    return default;
                }
            }
            set
            {
                if (_serialPortAdapter != null)
                {
                    _serialPortAdapter.SendTimeout = value;
                }

            }
        }
        public bool Connected
        {
            get
            {
                if (_serialPortAdapter != null)
                {
                    return _serialPortAdapter.Connected;
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
            return await _serialPortAdapter.ConnectAsync();
        }

        public OperationResult Disconnect()
        {
            return DisconnectAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<OperationResult> DisconnectAsync()
        {
            return await _serialPortAdapter.DisconnectAsync();

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

        private async Task<OperationResult<T>> WriteCoreAsync<T>(Func<Task<OperationResult<T>>> writeOperation)
        {
            if (IsLongConnection)
            {
                if (!Connected)
                {
                    return OperationResult.CreateFailedResult<T>("客户端没有连接");
                }
                return await writeOperation();
            }

            if (!Connected)
            {
                var connect = await ConnectAsync();
                if (!connect.IsSuccess)
                {
                    return OperationResult.CreateFailedResult<T>("短连接失败");
                }
            }

            try
            {
                return await writeOperation();
            }
            finally
            {
                if (!IsLongConnection)
                {
                    await DisconnectAsync();
                }
            }
        }

        internal override async Task<OperationResult> WriteAsync(string address, byte[] data, bool isBit = false)
        {
            return await HandleWriteAsync(() => base.WriteAsync(address, data, isBit));
        }

        public override async Task<OperationResult> WriteAsync(string address, bool[] data)
        {
            return await HandleWriteAsync(() => base.WriteAsync(address, data));
        }

        public override async Task<OperationResult> WriteAsync(string address, bool data)
        {
            return await HandleWriteAsync(() => base.WriteAsync(address, data));
        }

        private async Task<OperationResult> HandleWriteAsync(Func<Task<OperationResult>> writeAction)
        {
            if (IsLongConnection)
            {
                if (!Connected)
                {
                    return OperationResult.CreateFailedResult<byte[]>("客户端没有连接");
                }
                return await writeAction();
            }
            else
            {
                if (!Connected)
                {
                    var connect = await ConnectAsync();
                    if (!connect.IsSuccess)
                    {
                        return OperationResult.CreateFailedResult<byte[]>("短连接失败");
                    }
                }

                var result = await writeAction();
                if (!IsLongConnection)
                {
                    await DisconnectAsync();
                }
                return result;
            }
        }


    }
}

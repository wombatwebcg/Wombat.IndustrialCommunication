using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Ports;
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
    public class SerialPortAdapter : IStreamResource
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public StopBits StopBits { get; set; } = StopBits.One;
        public Parity Parity { get; set; } = Parity.None;
        public Handshake Handshake { get; set; } = Handshake.None;

        private AsyncLock _lock = new AsyncLock();

        /// <summary>
        /// 串行端口对象
        /// </summary>
        protected internal SerialPort _serialPort;
        public SerialPortAdapter(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None)
        {
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            Handshake = handshake;
            Parity = parity;
            StopBits = stopBits;
            _lock = new AsyncLock();
            _serialPort = new SerialPort();
        }

        public string Version => nameof(SerialPortAdapter);
        public ILogger Logger { get; set; }
        public TimeSpan WaiteInterval { get; set; }
        public bool Connected => _serialPort?.IsOpen ?? false;
        public TimeSpan ConnectTimeout
        {
            get;
            set;
        }
        public TimeSpan ReceiveTimeout
        {
            get
            {

                if (_serialPort != null)
                {
                    return TimeSpan.FromMilliseconds(_serialPort.ReadTimeout);
                }
                return default;
            }
            set
            {
                if (_serialPort != null)
                {
                    _serialPort.ReadTimeout = (int)value.TotalMilliseconds;
                }
            }
        }
        public TimeSpan SendTimeout
        {
            get
            {

                if (_serialPort != null)
                {
                    return TimeSpan.FromMilliseconds(_serialPort.WriteTimeout);
                }
                return default;
            }
            set
            {
                if (_serialPort != null)
                {
                    _serialPort.WriteTimeout = (int)value.TotalMilliseconds;
                }
            }
        }

        public async Task<OperationResult> Send(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            using (await _lock.LockAsync())
            {
                OperationResult operation = new OperationResult();
                try
                {
                    await _serialPort.BaseStream.WriteAsync(buffer, offset, size, cancellationToken);
                    return operation.Complete();
                }
                catch (Exception ex)
                {
                    operation.Exception = ex;
                    return OperationResult.CreateFailedResult(operation);
                }
            }
        }

        public async Task<OperationResult<int>> Receive(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            using (await _lock.LockAsync())
            {

                OperationResult<int> operation = new OperationResult<int>();
                try
                {
                    var count = await _serialPort.BaseStream.ReadAsync(buffer, offset, size, cancellationToken);
                    operation.ResultValue = count;
                    return operation.Complete();
                }
                catch (Exception ex)
                {
                    operation.Exception = ex;
                    return OperationResult.CreateFailedResult(operation, 0);
                }
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
                DisposableUtility.Dispose(ref _serialPort);
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
            _serialPort.PortName = PortName ?? throw new ArgumentNullException(nameof(PortName));
            _serialPort.BaudRate = BaudRate;
            _serialPort.Parity = Parity;
            _serialPort.DataBits = DataBits;
            _serialPort.StopBits = StopBits;
            _serialPort.Handshake = Handshake;
            _serialPort.Close();
            _serialPort.Open();
            await  _serialPort.BaseStream.FlushAsync();
            connect.IsSuccess = _serialPort.IsOpen;
            return connect;
        }

        public  Task<OperationResult> DisconnectAsync()
        {
            OperationResult connect = new OperationResult();
            _serialPort.Close();
            return Task.FromResult(connect);
        }
    }
}

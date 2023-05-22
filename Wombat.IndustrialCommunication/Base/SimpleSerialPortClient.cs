using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net;
using System.Text;
using Wombat.ObjectConversionExtention;
using Microsoft.Extensions.Logging;
using Wombat.Infrastructure;
using Wombat.Core;

namespace Wombat.IndustrialCommunication
{
    public class SimpleSerialPortClient : SerialPortBase
    {
        private AsyncLock @lock;

        public override bool Connected => base.Connected;

        public SimpleSerialPortClient():base()
        {
            @lock = new AsyncLock();
        }

        public SimpleSerialPortClient(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None) : base(portName, baudRate, dataBits, stopBits, parity, handshake)
        {
            @lock = new AsyncLock();

        }

        internal override OperationResult<byte[]> InterpretAndExtractMessageData(byte[] command)
        {
            using (@lock.Lock())
            {
                OperationResult<byte[]> result = new OperationResult<byte[]>();

                if (Connected != true)
                {
                    var connectResult = Connect();
                    if (!connectResult.IsSuccess)
                    {
                        connectResult.Message = $"自动连接失败";
                        return new OperationResult<byte[]>(connectResult);
                    }
                }
                try
                {
                    result = base.InterpretAndExtractMessageData(command);

                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Message = ex.Message;
                }
                finally
                {
                    if (!IsUseLongConnect) Disconnect();
                }

                return result;
            }
        }

    }
}

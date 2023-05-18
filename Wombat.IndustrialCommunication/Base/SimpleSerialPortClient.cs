using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net;
using System.Text;
using Wombat.ObjectConversionExtention;
using Microsoft.Extensions.Logging;
using Wombat.Infrastructure;

namespace Wombat.IndustrialCommunication
{
    public class SimpleSerialPortClient : SerialPortBase
    {
        private AdvancedHybirdLock _advancedHybirdLock;

        public override bool IsConnect => base.IsConnect;

        public SimpleSerialPortClient():base()
        {
            _advancedHybirdLock = new AdvancedHybirdLock();
        }

        public SimpleSerialPortClient(string portName, int baudRate = 9600, int dataBits = 8, StopBits stopBits = StopBits.One, Parity parity = Parity.None, Handshake handshake = Handshake.None) : base(portName, baudRate, dataBits, stopBits, parity, handshake)
        {
            _advancedHybirdLock = new AdvancedHybirdLock();

        }

        public override OperationResult<byte[]> SendPackageReliable(byte[] command)
        {
            _advancedHybirdLock.Enter();
            OperationResult<byte[]> result = new OperationResult<byte[]>();
            
            if (IsConnect != true)
            {
                var connectResult = Connect();
                if (!connectResult.IsSuccess)
                {
                    connectResult.Message = $"自动连接失败";
                    _advancedHybirdLock.Leave();
                    return new OperationResult<byte[]>(connectResult);
                }
            }
            try
            {
                 result = base.SendPackageReliable(command);

            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
                _advancedHybirdLock.Leave();
            }
            finally
            {
                if (!IsUseLongConnect) Disconnect();
            }

            _advancedHybirdLock.Leave();
            return result;

        }

    }
}

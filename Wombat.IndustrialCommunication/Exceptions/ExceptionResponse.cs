using System;
using System.Collections.Generic;
using System.Text;
using Wombat.IndustrialCommunication.Abstractions;

namespace Wombat.IndustrialCommunication.Exceptions
{
    public class ExceptionResponse : IDeviceMessage
    {
        private byte[] _mssageFrame;

        public byte[] ProtocolMessageFrame => _mssageFrame;

        public string RegisterAddress { get ; set; }

        public int Length { get; set; }

        public int DataLength { get; set; }

        public int ProtocolDataNumber => -1;


        public void Initialize(byte[] frame)
        {
            if(frame!=null)
            {
                _mssageFrame = new byte[frame.Length];
                Array.Copy(frame, _mssageFrame, frame.Length);

            }
        }
    }
}

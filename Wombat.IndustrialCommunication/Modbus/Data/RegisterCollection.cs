using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;


namespace Wombat.IndustrialCommunication.Modbus.Data
{


    public class RegisterCollection : Collection<ushort>, IModbusMessageDataCollection
    {
        public RegisterCollection()
        {
        }

        public RegisterCollection(byte[] bytes)
: this((IList<ushort>)ModbusUtility.NetworkBytesToHostUInt16(bytes))
        {
        }

        public RegisterCollection(params ushort[] registers)
: this((IList<ushort>)registers)
        {
        }

        public RegisterCollection(IList<ushort> registers)
: base(registers.IsReadOnly ? new List<ushort>(registers) : registers)
        {
        }

        public byte[] NetworkBytes
        {
            get
            {
                var bytes = new MemoryStream(ByteCount);

                foreach (ushort register in this)
                {
                    var b = BitConverter.GetBytes((ushort)IPAddress.HostToNetworkOrder((short)register));
                    bytes.Write(b, 0, b.Length);
                }

                return bytes.ToArray();
            }
        }

        public byte ByteCount
        {
            get { return (byte)(Count * 2); }
        }

        public override string ToString()
        {
            return String.Concat("{", String.Join(", ", this.Select(v => v.ToString()).ToArray()), "}");
        }
    }
}

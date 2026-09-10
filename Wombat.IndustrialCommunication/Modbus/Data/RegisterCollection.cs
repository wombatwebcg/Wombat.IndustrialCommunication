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
: this(NetworkBytesToHostUInt16(bytes))
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

        private static ushort[] NetworkBytesToHostUInt16(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length % 2 != 0)
                throw new FormatException("Array bytes must contain an even number of bytes.");

            var registers = new ushort[bytes.Length / 2];
            for (int i = 0; i < registers.Length; i++)
                registers[i] = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(bytes, i * 2));
            return registers;
        }
    }
}

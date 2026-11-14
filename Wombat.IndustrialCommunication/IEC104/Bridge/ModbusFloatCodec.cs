using System;

namespace Wombat.IndustrialCommunication.Extensions.IEC104.ModbusBridge
{
    /// <summary>
    /// IEEE754 单精度与 Modbus 双寄存器的互转。寄存器顺序由字序决定，字节序固定为高字节在前。
    /// </summary>
    public static class ModbusFloatCodec
    {
        /// <summary>把 float 拆成两个寄存器，第一个寄存器为低地址。</summary>
        public static void Encode(float value, ModbusFloatWordOrder order, out ushort first, out ushort second)
        {
            var bits = ToBits(value);
            var high = (ushort)((bits >> 16) & 0xFFFF);
            var low = (ushort)(bits & 0xFFFF);

            if (order == ModbusFloatWordOrder.Cdab)
            {
                first = low;
                second = high;
            }
            else
            {
                first = high;
                second = low;
            }
        }

        /// <summary>把两个寄存器拼回 float。</summary>
        public static float Decode(ushort first, ushort second, ModbusFloatWordOrder order)
        {
            var high = order == ModbusFloatWordOrder.Cdab ? second : first;
            var low = order == ModbusFloatWordOrder.Cdab ? first : second;

            var bits = (int)(((uint)high << 16) | low);
            return FromBits(bits);
        }

        private static int ToBits(float value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24);
        }

        private static float FromBits(int bits)
        {
            var bytes = new[]
            {
                (byte)(bits & 0xFF),
                (byte)((bits >> 8) & 0xFF),
                (byte)((bits >> 16) & 0xFF),
                (byte)((bits >> 24) & 0xFF),
            };

            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}

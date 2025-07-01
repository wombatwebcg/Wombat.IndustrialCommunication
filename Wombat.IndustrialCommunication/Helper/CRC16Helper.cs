using System;
using System.Linq;

namespace Wombat.IndustrialCommunication
{
    /// <summary>
    /// CRC16Helper验证
    /// </summary>
    internal class CRC16Helper
    {
        /// <summary>
        /// 验证CRC16Helper校验码
        /// </summary>
        /// <param name="value">校验数据</param>
        /// <param name="poly">多项式码</param>
        /// <param name="crcInit">校验码初始值</param>
        /// <returns></returns>
        public static bool ValidateCRC(byte[] value, ushort poly = 0xA001, ushort crcInit = 0xFFFF)
        {
            if (value == null || !value.Any())
                throw new ArgumentException("生成CRC16Helper的入参有误");

            var crc16 = GetCRC16(value.AsSpan<byte>(0, value.Length-2).ToArray(), poly, crcInit);
            if (crc16[0] == value[value.Length-2] && crc16[1] == value[value.Length - 1])
                return true;
            return false;
        }

        /// <summary>
        /// 计算CRC16Helper校验码
        /// </summary>
        /// <param name="value">校验数据</param>
        /// <param name="poly">多项式码</param>
        /// <param name="crcInit">校验码初始值</param>
        /// <returns></returns>
        public static byte[] GetCRC16(byte[] value, ushort poly = 0xA001, ushort crcInit = 0xFFFF)
        {
            if (value == null || !value.Any())
                throw new ArgumentException("生成CRC16的入参有误");

            ushort crc = crcInit;

            foreach (byte b in value)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ poly) : (ushort)(crc >> 1);
                }
            }

            // 小端序：低字节在前，高字节在后
            return new byte[] { (byte)(crc & 0x00FF), (byte)((crc & 0xFF00) >> 8) };
        }
    }
}
